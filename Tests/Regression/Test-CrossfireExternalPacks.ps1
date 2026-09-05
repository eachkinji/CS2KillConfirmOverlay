param([Parameter(Mandatory=$true)][string]$PackagesPath, [string]$InstallLocalState)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$service = Get-Content -Raw (Join-Path $root 'Widget/Services/Catalog/Games/CrossfireExternalAssetService.cs')
$models = Get-Content -Raw (Join-Path $root 'Widget/Services/Catalog/PackCatalogModels.cs')
$models = [regex]::Replace($models, '(?m)^using [^;]+;\r?\n', '')
$shim = @'
namespace Windows.ApplicationModel {
 public class Package {
  public static Package Current=new Package();
  public Windows.Storage.StorageFolder InstalledLocation=new Windows.Storage.StorageFolder{Path="installed"};
 }
}
namespace Windows.Storage {
 public class StorageFile {
  public string Path;
  public static Task<StorageFile> GetFileFromPathAsync(string p) {
   if(!File.Exists(p))throw new FileNotFoundException(p);
   return Task.FromResult(new StorageFile{Path=p});
  }
 }
 public class StorageFolder { public string Path; }
 public class ApplicationData {
  public static ApplicationData Current=new ApplicationData();
  public StorageFolder LocalFolder=new StorageFolder();
 }
}
namespace KillConfirmGameBar.Services {
 public static class PackCatalogService {public static Task RefreshCrossfireExternalPacksAsync()=>Task.CompletedTask;}
 public static class ExternalProbe {
  public static async Task Install(string source,string local,bool voice) {
   ApplicationData.Current.LocalFolder.Path=local;
   if(!await CrossfireExternalAssetService.TryInstallAsync(new StorageFolder{Path=source},voice))throw new Exception("Package not recognized");
  }
  public static void Verify(string local) {
   ApplicationData.Current.LocalFolder.Path=local;
   var catalog=new PackCatalog();
   catalog.IconPacks.Add(new IconPackItem{Key="custom_icon_existing",IsBuiltIn=false});
   catalog.IconPacks.Add(new IconPackItem{Key="default",IsBuiltIn=true});
   catalog.VoicePacks.Add(new VoicePackItem{Key="crossfire_swat_gr",IsBuiltIn=true});
   CrossfireExternalAssetService.RefreshCatalog(catalog);
   CrossfireExternalAssetService.RefreshCatalog(catalog);
   if(catalog.IconPacks.Count!=9||catalog.VoicePacks.Count!=11||catalog.IconPacks.Any(p=>p.IsBuiltIn)||catalog.VoicePacks.Any(p=>p.IsBuiltIn))throw new Exception("Discovery, duplicate prevention or retirement failed");
   foreach(var icon in catalog.IconPacks.Where(p=>p.FolderPath!=null)) {
    foreach(string name in new[]{"pack_head.png","badge_multi1.png","badge_c4.png","badge_c4defuse.png","badge_grenade.png","KillMark_Upgrade1.png","multi2_fx.png","badge_assault1.png"})
     if(!File.Exists(Path.Combine(icon.FolderPath,name)))throw new Exception("Missing layer: "+icon.Key+"/"+name);
   }
   foreach(var voice in catalog.VoicePacks)
    if(!File.Exists(Path.Combine(voice.FolderPath,"pack_head.png")))throw new Exception("Voice cover missing");
  }
 }
}
'@
Add-Type -TypeDefinition ($service + $models + $shim)
$temp = Join-Path $root ('Output/Tests/CrossfireExternal-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $temp -Force | Out-Null
$sandbox = Join-Path $temp 'LocalState'
$count = 0
foreach ($zip in Get-ChildItem -LiteralPath $PackagesPath -Filter '*.zip') {
    $extracted = Join-Path $temp ('zip-' + $count)
    [IO.Compression.ZipFile]::ExtractToDirectory($zip.FullName,$extracted)
    $folder = Get-ChildItem -LiteralPath $extracted -Directory | Select-Object -First 1
    $manifest = Get-Content -Raw (Join-Path $folder.FullName 'manifest.json') | ConvertFrom-Json
    $voice = $manifest.package_kind -eq 'crossfire_voice'
    if ($manifest.id -eq 'default') { $originalFolder = $folder.FullName }
    $null = [KillConfirmGameBar.Services.ExternalProbe]::Install($folder.FullName,$sandbox,$voice).GetAwaiter().GetResult()
    if ($InstallLocalState) {
        $null = [KillConfirmGameBar.Services.ExternalProbe]::Install($folder.FullName,$InstallLocalState,$voice).GetAwaiter().GetResult()
    }
    $count++
}
[KillConfirmGameBar.Services.ExternalProbe]::Verify($sandbox)
if ($count -ne 19) { throw "Expected 19 external packages; found $count" }
$null = [KillConfirmGameBar.Services.ExternalProbe]::Install($folder.FullName,$sandbox,$voice).GetAwaiter().GetResult()
[KillConfirmGameBar.Services.ExternalProbe]::Verify($sandbox)
# Preserve an unrelated creator folder that happens to use a reserved name.
$collision = Join-Path $temp 'CollisionState'
$marker = Join-Path $collision 'Packs/crossfire/icon_packs/default/keep.txt'
New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($marker)) -Force | Out-Null
Set-Content -LiteralPath $marker -Value 'creator content'
try {
    $null = [KillConfirmGameBar.Services.ExternalProbe]::Install($originalFolder,$collision,$false).GetAwaiter().GetResult()
    throw 'Unrelated creator folder was overwritten'
} catch { if ($_.Exception.ToString() -match 'Unrelated creator folder was overwritten') { throw } }
if ((Get-Content -Raw $marker).Trim() -ne 'creator content') { throw 'Creator content changed' }
# Wrong-library imports must fail before overwriting an installed package.
try {
    $null = [KillConfirmGameBar.Services.ExternalProbe]::Install($folder.FullName,$sandbox,(-not $voice)).GetAwaiter().GetResult()
    throw 'Wrong package kind was accepted'
} catch { if ($_.Exception.ToString() -match 'Wrong package kind was accepted') { throw } }
'PASS: 19 external packages installed through production installer; 8 icons/11 voices discovered; stable-key retirement, replacement, collision preservation, no duplicates, event/FX/badge layers, covers and wrong-library rejection.'

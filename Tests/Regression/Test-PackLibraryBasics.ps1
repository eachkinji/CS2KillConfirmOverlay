$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$files = @('Widget/Services/Catalog/Games/CrossfireExternalAssetService.cs',
    'Widget/Services/Catalog/PackCatalogModels.cs', 'Widget/Services/Catalog/PackLibraryNavigation.cs')
$code = ($files | ForEach-Object {
    [regex]::Replace((Get-Content -LiteralPath (Join-Path $root $_) -Raw), '(?m)^using [^;]+;\r?\n', '')
}) -join "`n"
$shim = @'
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Threading.Tasks;
using Windows.Storage;
namespace Windows.Storage {
 public class StorageFolder { public string Path; }
 public class StorageFile {
  public string Path;
  public static Task<StorageFile> GetFileFromPathAsync(string path) {
   if(!File.Exists(path)) throw new FileNotFoundException(path);
   return Task.FromResult(new StorageFile {Path=path});
  }
 }
 public class Values : Dictionary<string,object> {
  public new object this[string key] { get {object value;return TryGetValue(key,out value)?value:null;} set {base[key]=value;} }
 }
 public class ApplicationDataCompositeValue : Values { }
 public class Settings { public Values Values=new Values(); }
 public class ApplicationData {
  public static ApplicationData Current=new ApplicationData();
  public StorageFolder LocalFolder=new StorageFolder();
  public Settings LocalSettings=new Settings();
 }
}
namespace Windows.ApplicationModel {
 public class Package {
  public static Package Current=new Package();
  public StorageFolder InstalledLocation=new StorageFolder();
 }
}
namespace KillConfirmGameBar.Services {
 public enum GameStyleMode {Crossfire, Valorant, Csol}
 public static class GameStyleService { public static string ToStorageValue(GameStyleMode style)=>style.ToString().ToLowerInvariant(); }
 public static class PackCatalogService { public static Task RefreshCrossfireExternalPacksAsync()=>Task.CompletedTask; }
 public static class PackLibraryProbe {
  static void Check(bool ok,string message) {if(!ok)throw new Exception(message);}
  public static void Run(string installed,string local) {
   Windows.ApplicationModel.Package.Current.InstalledLocation.Path=installed;
   ApplicationData.Current.LocalFolder.Path=local;
   var catalog=new PackCatalog();
   catalog.IconPacks.Add(new IconPackItem{Key="vip",IsBuiltIn=true});
   catalog.IconPacks.Add(new IconPackItem{Key="custom_icon_saved",IsBuiltIn=false});
   catalog.VoicePacks.Add(new VoicePackItem{Key="crossfire_swat_bl",IsBuiltIn=true});
   CrossfireExternalAssetService.RefreshCatalog(catalog);
   CrossfireExternalAssetService.RefreshCatalog(catalog);
   Check(catalog.IconPacks.Count==2 && catalog.VoicePacks.Count==1,"Unexpected packs or duplicates");
   Check(catalog.IconPacks.Single(p=>p.Key=="default").IsBuiltIn,"Default icon absent");
   Check(catalog.VoicePacks.Single().Key=="crossfire_swat_gr" && catalog.VoicePacks.Single().IsBuiltIn,"Default voice absent");
   Check(File.Exists(new Uri(CrossfireExternalAssetService.VisualUri("Original","badge_multi1.png")).LocalPath),"Default icon does not resolve");
   Check(File.Exists(CrossfireExternalAssetService.DefaultVoiceFileAsync("common.wav").Result.Path),"Default voice does not resolve");
   string source=CrossfireExternalAssetService.BuiltInPath();
   CrossfireExternalAssetService.TryInstallAsync(new StorageFolder{Path=source},false).GetAwaiter().GetResult();
   CrossfireExternalAssetService.RefreshCatalog(catalog);
   Check(!catalog.IconPacks.Single(p=>p.Key=="default").IsBuiltIn,"Imported base override missing");
   Check(CrossfireExternalAssetService.ResolvePackPath("default")==CrossfireExternalAssetService.PackPath("default"),"Override path missing");
   Directory.Move(CrossfireExternalAssetService.PackPath("default"),Path.Combine(local,"removed-icon"));
   int revision=CrossfireExternalAssetService.Revision;
   CrossfireExternalAssetService.RefreshAfterRemoval(catalog);
   Check(CrossfireExternalAssetService.Revision>revision && catalog.IconPacks.Single(p=>p.Key=="default").IsBuiltIn,"Removal does not restore/invalidate base");
   Check(CrossfireExternalAssetService.ResolvePackPath("default")==source,"Base fallback path missing");
   CrossfireExternalAssetService.TryInstallAsync(new StorageFolder{Path=CrossfireExternalAssetService.BuiltInPath(true)},true).GetAwaiter().GetResult();
   CrossfireExternalAssetService.RefreshCatalog(catalog);
   Check(!catalog.VoicePacks.Single().IsBuiltIn,"Imported voice override missing");
   Directory.Move(CrossfireExternalAssetService.PackPath("crossfire_swat_gr",true),Path.Combine(local,"removed-voice"));
   CrossfireExternalAssetService.RefreshAfterRemoval(catalog);
   Check(catalog.VoicePacks.Single().IsBuiltIn,"Voice fallback missing");
   string[] expected={"https://pan.quark.cn/s/f93adc47c434?pwd=JEcL","https://pan.quark.cn/s/070d14fa9438?pwd=YwFG", "https://pan.quark.cn/s/52c6d57d73e9?pwd=cgCV","https://pan.quark.cn/s/9467261e2bd5?pwd=czBG"};
   int index=0;
   foreach(var style in new[]{GameStyleMode.Crossfire,GameStyleMode.Valorant}) foreach(bool voice in new[]{true,false}) {
    Check(PackLibraryNavigation.DownloadUrl(style,voice)==expected[index++],"Incorrect download target");
    PackLibraryNavigation.Request(style,voice);
    string game,tab;
    Check(PackLibraryNavigation.TryTake(out game,out tab) && game==GameStyleService.ToStorageValue(style) && tab==(voice?"voice":"icon"),"Incorrect navigation target");
    Check(!PackLibraryNavigation.TryTake(out game,out tab),"Request consumed twice");
   }
   PackLibraryNavigation.Request(GameStyleMode.Crossfire,true);
   ((ApplicationDataCompositeValue)ApplicationData.Current.LocalSettings.Values["PendingPackLibraryNavigation"])["created"]=0L;
   string g,t;
   Check(!PackLibraryNavigation.TryTake(out g,out t),"Stale request reopened");
   Check(PackLibraryNavigation.DownloadUrl(GameStyleMode.Csol,true)==null,"Unknown download target");
  }
 }
}
'@
Add-Type -TypeDefinition ($shim + $code)
$temp = Join-Path $root ('Output/Tests/PackLibrary-' + [guid]::NewGuid().ToString('N'))
$installed = Join-Path $temp 'Installed'
foreach ($mapping in @(
    @('iconpacks/default', 'Assets/GameStyles/crossfire/iconpacks/default'),
    @('soundpacks/crossfire_swat_gr', 'KillConfirmService/sounds/crossfire_swat_gr'))) {
    $target = Join-Path $installed $mapping[1]
    New-Item -ItemType Directory -Path $target -Force | Out-Null
    Get-ChildItem -LiteralPath (Join-Path $root ('SourceAssets/GameStyles/crossfire/' + $mapping[0])) -File |
        Copy-Item -Destination $target
}
[KillConfirmGameBar.Services.PackLibraryProbe]::Run($installed, (Join-Path $temp 'LocalState'))
'PASS: CF defaults, stable-key overrides, removal fallback/cache invalidation, four download links and one-shot game/tab navigation.'

param([string]$PackagesPath = '')
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
function Method([string]$text, [string]$name) {
    $m = [regex]::Match($text, '(?ms)^        private [^\r\n]+\b' + $name + '\([^)]*\).*?^        \}')
    if (-not $m.Success) { throw "Method not found: $name" }
    $m.Value
}
$format = Get-Content -Raw (Join-Path $root 'Widget/Services/Catalog/CrossfirePackFormat.cs')
$format = [regex]::Replace($format, '(?m)^using [^;]+;\r?\n', '')
$import = Get-Content -Raw (Join-Path $root 'Widget/Pages/Main/Packs/MainPage.PackFiles.cs')
$helpers = Get-Content -Raw (Join-Path $root 'Widget/Pages/Main/Packs/MainPage.PackFiles.Manifest.cs')
$layers = Get-Content -Raw (Join-Path $root 'Widget/Controls/Animations/Core/KillConfirmAnimation.CrossfireLayers.cs')
$collect = @('CollectRecognizedFilesAsync','TryGetIconFileVariantAsync','FindBestPackFolderAsync','FindBestPackFolderRecursiveAsync','CountRecognizedFilesAsync') | ForEach-Object { Method $import $_ }
$collect += Method $helpers 'TryGetFileAsync'
$layerMethods = @('LoadCrossfireExtraBitmapAsync','LoadCrossfireExtraLayersAsync','ClearCrossfireExtraCache') | ForEach-Object { Method $layers $_ }
$source = @'
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using KillConfirmGameBar.Services;
using Windows.Storage;
namespace Windows.Storage {
 public class StorageFile {
  public string Path; public string Name => System.IO.Path.GetFileName(Path); public string FileType => System.IO.Path.GetExtension(Path);
 }
 public class StorageFolder {
  public string Path;
  public Task<StorageFolder> GetFolderAsync(string name) { string p=System.IO.Path.Combine(Path,name); if(!Directory.Exists(p)) throw new DirectoryNotFoundException(p); return Task.FromResult(new StorageFolder{Path=p}); }
  public Task<StorageFile> GetFileAsync(string name) { string p=System.IO.Path.Combine(Path,name); if(!File.Exists(p)) throw new FileNotFoundException(p); return Task.FromResult(new StorageFile{Path=p}); }
  public Task<IReadOnlyList<StorageFolder>> GetFoldersAsync() => Task.FromResult((IReadOnlyList<StorageFolder>)Directory.GetDirectories(Path).Select(p=>new StorageFolder{Path=p}).ToArray());
  public Task<IReadOnlyList<StorageFile>> GetFilesAsync() => Task.FromResult((IReadOnlyList<StorageFile>)Directory.GetFiles(Path).Select(p=>new StorageFile{Path=p}).ToArray());
 }
}
class AudioSlotAliases {
 public static string[] SupportedAudioExtensions={".wav"};
 public static List<StorageFile> MatchSlotAudioFiles(IReadOnlyList<StorageFile> files,string name)=>new List<StorageFile>();
}
public class ImportProbe {
 static readonly string[] IconImageExtensions={".png",".tga",".jpg",".jpeg",".webp",".bmp"};
 public static async Task<Dictionary<string,string>> Read(string root) {
  var best=await FindBestPackFolderAsync(new StorageFolder{Path=root},CrossfirePackFormat.Files);
  var files=await CollectRecognizedFilesAsync(best,CrossfirePackFormat.Files);
  return files.ToDictionary(p=>p.Key,p=>p.Value.Path,StringComparer.OrdinalIgnoreCase);
 }
'@ + ($collect -join "`n") + "`n}`n" + $format + @'
public class CanvasBitmap { public string Name; public bool Disposed; public void Dispose(){Disposed=true;} }
static class PackCatalogService { public static bool IsImportedIconPackKey(string key)=>key.StartsWith("custom"); }
public class LayerProbe {
 enum KillFxMode { Off, Pack, Original }
 static KillFxMode _killFxMode;
 static string _iconPack;
 static double _brightnessBoost=0, _contrastBoost=0;
 static Dictionary<string,CanvasBitmap> CrossfireExtraCache=new Dictionary<string,CanvasBitmap>();
 static Dictionary<string,CanvasBitmap> images=new Dictionary<string,CanvasBitmap>(StringComparer.OrdinalIgnoreCase);
 class Code2KillAsset { public CanvasBitmap EventOverlay; public CanvasBitmap[] Sequence; }
 static Task<CanvasBitmap> TryLoadImportedIconBitmapAsync(string name)=>Task.FromResult(images.TryGetValue(name,out var b)?b:null);
 static void Check(bool pass,string message){if(!pass)throw new Exception(message);}
 public static async Task Run() {
  _iconPack="custom_test";_killFxMode=KillFxMode.Pack;
  foreach(string name in new[]{"killmark_multikill.png","killmark_headshot.png","killmark_knife.png","killmark_grenade.png","SPRITE_01.png","SPRITE_03.png","SPRITENORMAL_01.png","SPRITESPECIAL_01.png"}) images[name]=new CanvasBitmap{Name=name};
  var normal=new Code2KillAsset();await LoadCrossfireExtraLayersAsync(normal,"multi2");
  Check(normal.EventOverlay.Name=="killmark_multikill.png","Multi-kill event overlay");
  Check(normal.Sequence[0].Name=="SPRITENORMAL_01.png","Normal variant precedence");
  Check(normal.Sequence[1]==null && normal.Sequence[2].Name=="SPRITE_03.png","Missing frame keeps its slot; generic fallback");
  foreach(string action in new[]{"headshot_gold","multi6"}) {var a=new Code2KillAsset();await LoadCrossfireExtraLayersAsync(a,action);Check(a.Sequence[0].Name=="SPRITESPECIAL_01.png","Special variant");}
  foreach(string action in new[]{"knife","grenade","headshot"}) {var a=new Code2KillAsset();await LoadCrossfireExtraLayersAsync(a,action);Check(a.EventOverlay!=null,"Event overlay");}
  foreach(string action in new[]{"c4","c4defuse","firstkill","lastkill","assist","revenge"}) {var a=new Code2KillAsset();await LoadCrossfireExtraLayersAsync(a,action);Check(a.Sequence==null && a.EventOverlay==null,"Unrelated event overlay");}
  foreach(var mode in new[]{KillFxMode.Off,KillFxMode.Original}) {_killFxMode=mode;var a=new Code2KillAsset();await LoadCrossfireExtraLayersAsync(a,"multi2");Check(a.EventOverlay==null&&a.Sequence==null,"Mode isolation");}
  Check(CrossfirePackFormat.SequenceFrame(0)==0 && CrossfirePackFormat.SequenceFrame(74.99)==0 && CrossfirePackFormat.SequenceFrame(75)==1 && CrossfirePackFormat.SequenceFrame(749.99)==9 && CrossfirePackFormat.SequenceFrame(750)==-1,"75ms boundaries");
  Check(CrossfirePackFormat.Files.Distinct(StringComparer.OrdinalIgnoreCase).Count()==CrossfirePackFormat.Files.Length,"Unique slots");
  var cached=CrossfireExtraCache.Values.Where(b=>b!=null).ToArray();
  ClearCrossfireExtraCache();Check(cached.All(b=>b.Disposed),"Cached bitmaps disposed");
 }
'@ + ($layerMethods -join "`n") + "`n}`n"
Add-Type -TypeDefinition $source
[LayerProbe]::Run().GetAwaiter().GetResult() | Out-Null
$temp = Join-Path ([IO.Path]::GetTempPath()) ('cf-pack-test-' + [guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($temp) | Out-Null
try {
    $suite = Join-Path $temp 'wrapped/suite'
    [IO.Directory]::CreateDirectory((Join-Path $suite 'Sprite')) | Out-Null
    [IO.File]::WriteAllText((Join-Path $suite 'BADGE_HEADSHOT.PNG'), 'main')
    [IO.File]::WriteAllText((Join-Path $suite '[US]FIRSTKILL.PNG'), 'regional')
    [IO.File]::WriteAllText((Join-Path $suite 'KILLMARK_UPGRADE_NA_1.PNG'), 'regional badge')
    1..10 | ForEach-Object { [IO.File]::WriteAllText((Join-Path $suite ('Sprite/SPRITE_{0:00}.PNG' -f $_)), 'frame') }
    $files = [ImportProbe]::Read($temp).GetAwaiter().GetResult()
    if ($files.Count -ne 13 -or -not $files.ContainsKey('badge_headshot.png') -or -not $files.ContainsKey('FIRSTKILL.png') -or -not $files.ContainsKey('KillMark_Upgrade1.png')) { throw 'Nested folder selection or regional aliases failed' }
    $count = 0
    if ($PackagesPath) {
        foreach ($zip in Get-ChildItem -LiteralPath $PackagesPath -Filter '*.zip') {
            $extracted = Join-Path $temp ([guid]::NewGuid().ToString('N'))
            [IO.Compression.ZipFile]::ExtractToDirectory($zip.FullName,$extracted)
            $files = [ImportProbe]::Read($extracted).GetAwaiter().GetResult()
            if ($files.Count -eq 0) { throw "No recognized files: $($zip.Name)" }
            $head = @(Get-ChildItem -LiteralPath $extracted -Recurse -Filter 'pack_head.png')
            if ($head.Count -ne 1) { throw "Missing cover: $($zip.Name)" }
            $allFrames = @(Get-ChildItem -LiteralPath $extracted -Recurse -File | Where-Object Name -Match '^SPRITE(NORMAL|SPECIAL)?_\d{2}\.PNG$')
            foreach ($frame in $allFrames) { if (-not $files.ContainsKey($frame.Name)) { throw "Lost frame: $($zip.Name) / $($frame.Name)" } }
            $count++
        }
    }
    Write-Host "PASS: actual CF collector, parent selection, aliases, event overlays, timing, missing frames, modes and disposal; $count packages inspected."
} finally {
    $resolved = [IO.Path]::GetFullPath($temp)
    if ($resolved.StartsWith([IO.Path]::GetFullPath([IO.Path]::GetTempPath()),[StringComparison]::OrdinalIgnoreCase) -and [IO.Path]::GetFileName($resolved).StartsWith('cf-pack-test-')) {
        Remove-Item -LiteralPath $resolved -Recurse -Force
    }
}

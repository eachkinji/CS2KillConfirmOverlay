param()
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
function Method([string]$text, [string]$name) {
    $m = [regex]::Match($text, '(?ms)^        private [^\r\n]+\b' + $name + '\([^)]*\).*?^        \}')
    if (-not $m.Success) { throw "Method not found: $name" }
    $m.Value
}
$core = Join-Path $root 'Widget/Controls/Animations/Core'
$assets = Get-Content -Raw "$core/KillConfirmAnimation.Assets.cs"
$preload = Get-Content -Raw "$core/KillConfirmAnimation.Preloading.cs"
$overlays = Get-Content -Raw "$core/KillConfirmAnimation.AssetOverlays.cs"
$methods = @('LoadCodeKillAssetAsync','GetCodeKillCacheKey') | ForEach-Object { Method $assets $_ }
$methods += Method $preload 'GetCodeKillPreloadRequests'
$methods += Method $overlays 'SupportsWeaponBadgeForAsset'
$methods += Method $overlays 'NormalizeWeaponBadgeKey'
$source = @'
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
public class CanvasBitmap { public void Dispose() {} }
public class Code2KillAsset {
 public CanvasBitmap Main,Fx,Overlay,WeaponBadge; public object EventOverlay,Sequence; public string Action;
 public Code2KillAsset(CanvasBitmap main,CanvasBitmap fx,CanvasBitmap overlay,CanvasBitmap badge) {Main=main;Fx=fx;Overlay=overlay;WeaponBadge=badge;}
}
public class AnimationAsset {public Code2KillAsset Asset;}
public class PreloadProbe {
 static string _iconPack="test"; static int _killFxMode=1,_eliteEffectLevel=1,_weaponBadgeMode=1,_mainAnimationStyle=2,_resourceGeneration=0;
 static bool _brightnessBoost=false,_contrastBoost=false,_customPackHasKillFx=true,_customPackHasEliteOverlay=true,_customPackHasWeaponBadgeOverlay=true;
 static Dictionary<string,Code2KillAsset> CodeKillCache=new Dictionary<string,Code2KillAsset>();
 static int mainLoads, badgeLoads;
 static bool SupportsWeaponBadgeOverlay()=>_customPackHasWeaponBadgeOverlay;
 static bool TryGetCodeKillFiles(string a,out string main,out string folder,out string alternate,out string fx,out string fxFolder) {main=a;folder="";alternate="";fx=a;fxFolder="";return true;}
 static string GetEffectiveMainFileName(string a,string f)=>f;
 static Task<CanvasBitmap> LoadMainCodeKillBitmapAsync(string a,string b,string c,string d,string e) {mainLoads++;return Task.FromResult(new CanvasBitmap());}
 static Task<CanvasBitmap> LoadWeaponBadgeOverlayBitmapAsync(string a,string b) {badgeLoads++;return Task.FromResult(string.IsNullOrEmpty(b)?null:new CanvasBitmap());}
 static Task<CanvasBitmap> LoadKillFxOverlayBitmapAsync(string a,string b)=>Task.FromResult(new CanvasBitmap());
 static Task<CanvasBitmap> LoadEliteOverlayBitmapAsync(string a)=>Task.FromResult(new CanvasBitmap());
 static Task LoadCrossfireExtraLayersAsync(Code2KillAsset asset,string a) {asset.EventOverlay=new object();asset.Sequence=new object();return Task.CompletedTask;}
 static AnimationAsset CreateCodeKillAnimationAsset(Code2KillAsset a)=>new AnimationAsset{Asset=a};
 static void Check(bool ok,string reason) {if(!ok)throw new Exception(reason);}
 public static async Task Verify(string[] supported) {
  var p=new PreloadProbe();var requests=GetCodeKillPreloadRequests();
  Check(requests.Count==150,"Style 2 must warm all 150 event/badge combinations");
  Check(supported.All(a=>requests.Any(r=>r.Item1==a)),"A supported event is missing from preload");
  foreach(var r in requests)await p.LoadCodeKillAssetAsync(r.Item1,r.Item2);
  Check(mainLoads==25,"Badge variants decoded the same main texture again");
  int reads=badgeLoads;
  foreach(var r in requests) {
   var loaded=await p.LoadCodeKillAssetAsync(r.Item1,r.Item2);
   var basic=CodeKillCache[GetCodeKillCacheKey(r.Item1,"")];
   Check(ReferenceEquals(loaded.Asset.Main,basic.Main)&&ReferenceEquals(loaded.Asset.Sequence,basic.Sequence),"Variants must share preloaded layers");
  }
  Check(mainLoads==25&&badgeLoads==reads,"Warm playback unexpectedly loaded assets");
  string key=GetCodeKillCacheKey("headshot","elite");
  _killFxMode=2;Check(key!=GetCodeKillCacheKey("headshot","elite"),"FX settings collided");_killFxMode=1;
  _customPackHasKillFx=false;Check(key!=GetCodeKillCacheKey("headshot","elite"),"Capability refresh collided");_customPackHasKillFx=true;
  Check(CodeKillCache.ContainsKey(key),"Returning to previous settings discarded warm assets");
  _mainAnimationStyle=1;Check(GetCodeKillPreloadRequests().Count==55,"Style 1 badge restrictions");
  _weaponBadgeMode=0;Check(GetCodeKillPreloadRequests().Count==25,"Badges disabled");
 }
'@
$source += ($methods -join "`n") + "`n}"
Add-Type -TypeDefinition $source
$supported = [regex]::Matches((Method $assets 'TryGetCodeKillFiles'),'case "([^"]+)":') | ForEach-Object {$_.Groups[1].Value}
[PreloadProbe]::Verify([string[]]$supported).GetAwaiter().GetResult()
Write-Host 'PASS: all supported events covered; 150 style 2 combinations; 25 main decodes; zero additional loads on warm replay; setting/capability cache separation; style 1 and badges disabled.'

# Exercise the production playback method with an occupied preload gate.
$playback = Method (Get-Content -Raw "$core/KillConfirmAnimation.Playback.cs") 'PlayInternal'
$playbackSource = @'
using System;
using System.Threading;
using System.Threading.Tasks;
#pragma warning disable 0414
public class PlaybackProbe {
 class Metadata { public int FrameWidth=256,FrameHeight=256; }
 class AnimationAsset { public Metadata Metadata=new Metadata(); public object CodeAsset=new object(),ValorantAsset=null,BattlefieldAsset=null,CsolAsset=null; }
 class Timer { public TimeSpan Interval; public void Start() {} public void Stop() {} }
 enum Visibility {Visible,Collapsed}
 Visibility VisibilityState;
 bool _customSequencePlaying=false,_contentSizedViewport=false,_isBattlefield1CompactLayoutActive=false;
 int _resourceGeneration=0,_playToken=0,_currentFrame=0,_mainAnimationStyle=2,_targetPlaybackFps=60;
 const int FrameSequenceFps=60;
 Metadata _currentMetadata=null; object _currentCodeAsset=null,_currentValorantAsset=null,_currentBattlefieldAsset=null,_currentCsolAsset=null;
 Timer _timer=new Timer(); System.Diagnostics.Stopwatch _playbackClock=new System.Diagnostics.Stopwatch();
 SemaphoreSlim PreloadGate=new SemaphoreSlim(0,1);
 int frames,loaders,loading;
 TaskCompletionSource<bool> shown=new TaskCompletionSource<bool>();
 static class App {public static void Log(string text) {throw new Exception(text);} }
 void ShowLoadingProgress(int value) {loading++;}
 Task ShowLoadingProgressIfStillLoadingAsync(int token,IProgress<int> progress) {loading++;return Task.CompletedTask;}
 void HideLoadingProgress() {}
 void ApplyViewportSize(int width,int height) {}
 void ReleaseAllAnimationResourceCaches() {}
 void ShowFrame(int frame) {frames++;shown.TrySetResult(true);}
 public static async Task Verify() {
  var warm=new PlaybackProbe();
  warm.PlayInternal(p=>{warm.loaders++;return Task.FromResult(new AnimationAsset());},false,new AnimationAsset());
  if(warm.frames!=1||warm.loaders!=0||warm.loading!=0)throw new Exception("Cached CF playback waited for preload or displayed Loading");
  var cold=new PlaybackProbe();
  cold.PlayInternal(p=>{cold.loaders++;return Task.FromResult(new AnimationAsset());},false);
  if(cold.frames!=0||cold.loaders!=0||cold.loading!=0)throw new Exception("Cold playback bypassed serialization or displayed Loading");
  cold.PreloadGate.Release();
  if(await Task.WhenAny(cold.shown.Task,Task.Delay(2000))!=cold.shown.Task||cold.loaders!=1||cold.loading!=0)throw new Exception("Cold playback did not resume after preload");
 }
'@
$resets = [regex]::Matches($playback,'\b(Reset\w+)\(\);') | ForEach-Object { "void $($_.Groups[1].Value)() {}" }
# Only substitute the UI property name; control flow remains production code.
$playback = $playback.Replace('Visibility = Visibility.', 'VisibilityState = Visibility.')
$playbackSource += ($resets -join "`n") + "`n" + $playback + "`n}"
Add-Type -TypeDefinition $playbackSource -WarningAction SilentlyContinue
[PlaybackProbe]::Verify().GetAwaiter().GetResult()
Write-Host 'PASS: cached playback runs immediately while preload gate is occupied; cold playback remains serialized; neither CF path displays Loading.'

$format = Get-Content -Raw (Join-Path $root 'Widget/Services/Catalog/CrossfirePackFormat.cs')
$format = [regex]::Replace($format, '(?m)^using [^;]+;\r?\n', '')
$indexSource = @'
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using KillConfirmGameBar.Services;
public class StorageFile { public string Name; }
public class StorageFolder {
 public string Path; public int Enumerations;
 public StorageFile[] Files=new StorageFile[0];
 public Dictionary<string,StorageFolder> Children=new Dictionary<string,StorageFolder>();
 public Task<StorageFile[]> GetFilesAsync() {Enumerations++;return Task.FromResult(Files);}
 public Task<StorageFolder> GetFolderAsync(string name) {if(!Children.ContainsKey(name))throw new DirectoryNotFoundException();return Task.FromResult(Children[name]);}
}
public class IndexProbe {
 static readonly string[] ImportedIconImageExtensions={".png",".tga",".jpg",".jpeg",".webp",".bmp"};
 static readonly Dictionary<string,Task<Dictionary<string,StorageFile>>> ImportedCodeFileIndexes=new Dictionary<string,Task<Dictionary<string,StorageFile>>>();
 public static async Task Verify() {
  var root=new StorageFolder {Path="pack",Files=new[]{new StorageFile{Name="badge_multi1.TGA"}}};
  var sprite=new StorageFolder {Files=new[]{new StorageFile{Name="badge_multi1.png"},new StorageFile{Name="badge_multi2.png"}}};
  root.Children["Sprite"]=sprite;
  for(int i=0;i<20;i++) {
   if((await TryGetImportedIconFileAsync(root,"badge_multi1.png")).Name!="badge_multi1.TGA")throw new Exception("Root/extension precedence changed");
   if((await TryGetImportedIconFileAsync(root,"badge_multi2.png")).Name!="badge_multi2.png")throw new Exception("Sprite folder was not indexed");
   if(await TryGetImportedIconFileAsync(root,"missing.png")!=null)throw new Exception("Missing texture lookup");
  }
  if(root.Enumerations!=1||sprite.Enumerations!=1)throw new Exception("Repeated filesystem enumeration during preload");
 }
'@
$indexSource += (Method $assets 'IndexImportedCodeFilesAsync') + (Method $assets 'TryGetImportedIconFileAsync') + "`n}" + $format
Add-Type -TypeDefinition $indexSource
[IndexProbe]::Verify().GetAwaiter().GetResult()
Write-Host 'PASS: one directory scan per imported pack, root/Sprite precedence, case-insensitive extensions, missing optional folder/files.'


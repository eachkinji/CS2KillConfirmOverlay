#Requires -Version 7.0
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$sources=@()
foreach($game in @('Dagoujiao','Doubao')) {
    $sources += [IO.File]::ReadAllText((Join-Path $root "Widget/Controls/Animations/$game/$($game)Animation.Resources.cs"))
    $play=[IO.File]::ReadAllText((Join-Path $root "Widget/Controls/Animations/$game/$($game)Animation.Playback.cs"))
    $start=$play.IndexOf("        public async void Play$($game)Kill(")
    $end=$play.IndexOf("        private void Prepare$($game)Playback",$start)
    $sources += "namespace KillConfirmGameBar.Controls { public sealed partial class KillConfirmAnimation {`n"+$play.Substring($start,$end-$start)+"`n} }"
}
$shim=@'
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KillConfirmGameBar.Services;
using Microsoft.Graphics.Canvas;
using Windows.UI.Xaml;
namespace Microsoft.Graphics.Canvas { public class CanvasBitmap { public string Key; public int Disposals; public void Dispose(){Disposals++;} } }
namespace Microsoft.Graphics.Canvas.Effects { class Placeholder {} }
namespace Windows.Foundation { class Placeholder {} }
namespace Windows.UI { class Placeholder {} }
namespace Windows.UI.Xaml { public enum Visibility { Visible, Collapsed } }
namespace Windows.Storage {
 public class StorageFile { public string Path; }
 public class StorageFolder {
  public string Path="custom"; public static int Reads; public HashSet<string> Files=new HashSet<string>();
  public Task<StorageFile> GetFileAsync(string name) { Reads++; if(!Files.Contains(name)) throw new System.IO.FileNotFoundException(name); return Task.FromResult(new StorageFile{Path=Path+"/"+name}); }
 }
}
namespace KillConfirmGameBar { static class App { public static void LogCrash(string error) {} } }
namespace KillConfirmGameBar.Services {
 public class DagoujiaoSettingsValues {
  public int EpicKillCount=50; public bool HeadshotPriority=true;
  public double Opacity=.6,InitialScale=.5,MaximumScale=2,InitialPlaybackSpeed=.5,MaximumPlaybackSpeed=2,EpicPlaybackSpeed=1;
  public string HeadshotImageKey="head",EpicImageKey="epic",CommonAudioKey="common-audio",EpicAudioKey="epic-audio",HeadshotAudioKey="head-audio";
  public Dictionary<int,string> KillImageKeys=new Dictionary<int,string>();
 }
 public static class DagoujiaoSettingsStore {
  public const string DefaultCommonImageKey="common",DefaultHeadshotImageKey="head",DefaultEpicImageKey="epic";
  public const int MinimumEpicKillCount=3,MaximumEpicKillCount=50;
  public static int DurationReads; public static DagoujiaoSettingsValues Settings=new DagoujiaoSettingsValues();
  public static DagoujiaoSettingsValues Load()=>Settings;
  public static string ResolveImageKey(DagoujiaoSettingsValues s,int n,bool h)=>h&&s.HeadshotPriority?s.HeadshotImageKey:n>=s.EpicKillCount?s.EpicImageKey:s.KillImageKeys.TryGetValue(n,out var k)?k:"common";
  public static double ResolveProgress(int n,int epic)=>(Math.Max(1,Math.Min(epic-1,n))-1.0)/(epic-2.0);
  public static Task<double> GetPlaybackDurationMillisecondsAsync(DagoujiaoSettingsValues s,int n,bool h){DurationReads++;return Task.FromResult(n*100.0+(h?1:0));}
  public static string GetBuiltInFileName(string k)=>k.StartsWith("imported:")?null:k+".png";
  public static Task<Windows.Storage.StorageFile> GetImportedImageFileAsync(string k)=>Task.FromResult(new Windows.Storage.StorageFile{Path=k});
 }
 public static class PackCatalogService {
  public const string DagoujiaoAnimalsPackKey="animals";
  public static Windows.Storage.StorageFolder Folder;
  public static bool IsImportedIconPackKey(string k)=>k=="custom";
  public static Task<Windows.Storage.StorageFolder> GetImportedIconFolderAsync(string k)=>Task.FromResult(Folder);
 }
}
namespace KillConfirmGameBar.Controls {
 public sealed partial class KillConfirmAnimation {
  static string _iconPack="dagoujiao",_currentPackPreloadSignature;
  static int _resourceGeneration; static Task _startupPreloadTask,_currentPackPreloadTask;
  static Dictionary<string,CanvasBitmap> DagoujiaoImageCache=new Dictionary<string,CanvasBitmap>();
  static Dictionary<string,CanvasBitmap> DoubaoKillCache=new Dictionary<string,CanvasBitmap>();
  static int Loads; int _playToken,Prepared; bool Active; CanvasBitmap Last; double LastDuration; public Visibility Visibility;
  Task Ready=Task.CompletedTask;
  int BeginReplacementPlayback(){Active=false;Last=null;return ++_playToken;}
  Task WaitForCurrentPackPreloadAsync()=>Ready;
  static double Lerp(double a,double b,double x)=>a+(b-a)*x;
  static Task<CanvasBitmap> LoadBitmapFromApplicationUriAsync(string k){Loads++;return Task.FromResult(new CanvasBitmap{Key=k});}
  static Task<CanvasBitmap> LoadBitmapFromStorageFileAsync(Windows.Storage.StorageFile f){Loads++;return Task.FromResult(new CanvasBitmap{Key=f.Path});}
  void PrepareDagoujiaoPlayback(CanvasBitmap b,string k,double o,double s,double d){Last=b;LastDuration=d;Prepared++;Active=true;}
  void PrepareDoubaoPlayback(CanvasBitmap b,CanvasBitmap f,int n){Last=b;Prepared++;Active=true;}
  void ResetDagoujiaoState(){Active=false;Last=null;} void ResetDoubaoState(){Active=false;Last=null;}
  static void Check(bool x,string message){if(!x)throw new Exception(message);}
  static void Reset(){ClearDagoujiaoImageCache();ClearDoubaoIconCache();_doubaoFlashBitmapCache=null;Loads=0;DagoujiaoSettingsStore.DurationReads=0;Windows.Storage.StorageFolder.Reads=0;}
  public static void Run()=>RunAsync().GetAwaiter().GetResult();
  static async Task RunAsync(){
   var a=new KillConfirmAnimation();var s=DagoujiaoSettingsStore.Settings;
   s.KillImageKeys[1]="imported:custom.png";s.KillImageKeys[2]="imported:custom.png";
   Reset();await a.PreloadDagoujiaoAnimationsAsync(null);
   Check(DagoujiaoPreparedCache.Count==100&&Loads==4,"Configured 50-kill/headshot coverage or shared-image reuse failed.");
   Check(ReferenceEquals(GetPreparedDagoujiaoPlayback(1,false).Bitmap,GetPreparedDagoujiaoPlayback(2,false).Bitmap),"Same custom image loaded twice.");
   int loads=Loads,reads=DagoujiaoSettingsStore.DurationReads;
   a.PlayDagoujiaoKill(100,true);
   Check(a.Active&&a.LastDuration==5001&&Loads==loads&&DagoujiaoSettingsStore.DurationReads==reads,"Playback loaded image/duration instead of using prepared overkill/headshot slot.");
   var pending=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);a.Ready=pending.Task;
   int before=a.Prepared;a.PlayDagoujiaoKill(1,false);a.PlayDagoujiaoKill(3,false);
   Check(!a.Active&&a.Prepared==before,"Old picture survived a pending replacement.");
   pending.SetResult(true);for(int i=0;i<100&&a.Prepared==before;i++)await Task.Delay(1);
   Check(a.Prepared==before+1&&a.LastDuration==300,"Superseded pending event painted after latest event.");
   pending=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);a.Ready=pending.Task;
   before=a.Prepared;a.PlayDagoujiaoKill(1,false);_resourceGeneration++;pending.SetResult(true);await Task.Delay(20);
   Check(!a.Active&&a.Prepared==before,"Stale resource generation painted.");a.Ready=Task.CompletedTask;
   Reset();_iconPack="animals";await a.PreloadDagoujiaoAnimationsAsync(null);
   Check(Loads==1&&DagoujiaoPreparedCache.Count==100,"Animals image was not shared across prepared slots.");
   Reset();_iconPack="custom";PackCatalogService.Folder=new Windows.Storage.StorageFolder();
   foreach(string name in new[]{"1kill.png","headshot.png","epic.png","common.png"})PackCatalogService.Folder.Files.Add(name);
   await a.PreloadDagoujiaoAnimationsAsync(null);
   Check(Loads==4&&ReferenceEquals(GetPreparedDagoujiaoPlayback(2,false).Bitmap,GetPreparedDagoujiaoPlayback(49,false).Bitmap),"Custom common/epic fallback loaded duplicates.");
   loads=Loads;reads=Windows.Storage.StorageFolder.Reads;a.PlayDagoujiaoKill(49,false);a.PlayDagoujiaoKill(50,true);
   Check(Loads==loads&&Windows.Storage.StorageFolder.Reads==reads,"Custom playback performed storage I/O.");
   var oldImages=new HashSet<CanvasBitmap>(CollectDagoujiaoCachedBitmaps());
   CanvasBitmap displayed=a.Last;InvalidateDagoujiaoImageCache();
   Check(displayed.Disposals==0&&GetPreparedDagoujiaoPlayback(50,true)==null,"Invalidation disposed an active image or left stale prepared slots.");
   Check(oldImages.SetEquals(CollectDagoujiaoCachedBitmaps()),"Invalidation lost ownership of old images.");
   await a.PreloadDagoujiaoAnimationsAsync(null);InvalidateDagoujiaoImageCache();
   var releaseImages=new HashSet<CanvasBitmap>(CollectDagoujiaoCachedBitmaps());
   Check(releaseImages.Count==oldImages.Count*2&&releaseImages.IsSupersetOf(oldImages),"Repeated invalidation leaked a retired image generation.");
   ClearDagoujiaoImageCache();foreach(var bitmap in releaseImages)bitmap.Dispose();
   Check(new HashSet<CanvasBitmap>(CollectDagoujiaoCachedBitmaps()).Count==0,"Release retained retired bitmap ownership.");
   foreach(var bitmap in releaseImages)Check(bitmap.Disposals==1,"Retired bitmap was not disposed exactly once by the release owner.");
   ClearDagoujiaoImageCache();Check(GetPreparedDagoujiaoPlayback(50,true)==null,"Clear retained a prepared bitmap.");
   Reset();_iconPack="doubao";await a.PreloadDoubaoAnimationsAsync(null);Check(Loads==6,"Doubao did not preload five frames plus flash.");
   loads=Loads;a.PlayDoubaoKill(9);a.PlayDoubaoKill(1);Check(a.Active&&Loads==loads,"Doubao playback called a loader.");
   ClearDoubaoIconCache();a.PlayDoubaoKill(1);Check(!a.Active&&Loads==loads,"Doubao cache miss reloaded or left old picture visible.");
   string signature=DagoujiaoPreloadSettingsSignature();s.KillImageKeys[49]="imported:another.png";Check(signature!=DagoujiaoPreloadSettingsSignature(),"Configured image missing from signature.");
  }
 }
}
'@
# Namespace-level imports from the production files must precede all declarations.
$sources=@($shim)+$sources
$imports=@($sources | ForEach-Object {[regex]::Matches($_,'(?m)^using [^\r\n]+;') | ForEach-Object {$_.Value}}) | Select-Object -Unique
$bodies=$sources | ForEach-Object {[regex]::Replace($_,'(?m)^using [^\r\n]+;\r?\n','')}
Add-Type -TypeDefinition ("#pragma warning disable 0169,0414,0649`n"+($imports -join "`n")+"`n"+($bodies -join "`n"))
[KillConfirmGameBar.Controls.KillConfirmAnimation]::Run()
Write-Output 'PASS: real Dagoujiao/Doubao preparation and playback methods; 100 configured routes, shared images and custom fallbacks, prepared durations, zero playback storage/GPU loads, pending replacement/latest token, generation cancellation, deferred bitmap ownership/release, and settings signature.'

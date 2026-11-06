param()
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
function Method([string]$source, [string]$name) {
    $match = [regex]::Match($source, '(?ms)^        private [^\r\n]+\b' + $name + '\([^)]*\).*?^        \}')
    if (!$match.Success) { throw "Method not found: $name" }
    $match.Value
}
foreach ($renderer in @('Widget', 'CompatibilityHost/Renderer')) {
    $source = Get-Content -Raw (Join-Path $root "$renderer/Controls/Animations/Core/KillConfirmAnimation.Playback.cs")
    $methods = (Method $source 'BeginReplacementPlayback') + "`n" + (Method $source 'PlayInternal')
    $class = if ($renderer -eq 'Widget') { 'WidgetReplacementProbe' } else { 'DesktopReplacementProbe' }
    $harness = @'
using System;
using System.Threading;
using System.Threading.Tasks;
#pragma warning disable 0414,0649
public class PROBE {
 class Metadata { public int FrameWidth=200,FrameHeight=120; }
 class AnimationAsset { public Metadata Metadata=new Metadata(); public object CodeAsset=new object(),ValorantAsset=null,BattlefieldAsset=null,CsolAsset=null; }
 class Timer { public bool Running; public TimeSpan Interval; public void Start(){Running=true;} public void Stop(){Running=false;} }
 class Canvas { public int Invalidations; public void Invalidate(){Invalidations++;} }
 enum Visibility { Visible,Collapsed }
 Visibility VisibilityState;
 bool _customSequencePlaying,_isCalabiyauActive,_isBattlefieldTextOverlayActive,_contentSizedViewport,_isBattlefield1CompactLayoutActive;
 int _playToken,_resourceGeneration,_currentFrame,_mainAnimationStyle=2,_targetPlaybackFps=60;
 const int FrameSequenceFps=60;
 Metadata _currentMetadata; object _currentCodeAsset,_currentValorantAsset,_currentBattlefieldAsset,_currentCsolAsset;
 Timer _timer=new Timer(); Canvas SpriteCanvas=new Canvas();
 System.Diagnostics.Stopwatch _playbackClock=new System.Diagnostics.Stopwatch();
 SemaphoreSlim PreloadGate=new SemaphoreSlim(1,1);
 Task preload=Task.CompletedTask;
 TaskCompletionSource<bool> presented=NewSignal();
 static TaskCompletionSource<bool> logged=NewSignal();
 int frames,loads,cacheReleases;
 static class App { public static void Log(string text){logged.TrySetResult(true);} }
 Task WaitForCurrentPackPreloadAsync()=>preload;
 void HideLoadingProgress(){}
 void ReleaseAllAnimationResourceCaches(bool invalidatePreload=true){cacheReleases++;if(invalidatePreload)preload=null;}
 void ApplyViewportSize(int w,int h){Check(_currentMetadata.FrameWidth==w&&_currentMetadata.FrameHeight==h,"Viewport observed a previous asset");}
 void ShowFrame(int frame){frames++;presented.TrySetResult(true);}
 static TaskCompletionSource<bool> NewSignal()=>new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
 static void Check(bool value,string message){if(!value)throw new Exception(message);}
 static async Task Wait(Task task){if(await Task.WhenAny(task,Task.Delay(3000))!=task)throw new Exception("Playback did not complete");await task;}
 void SeedOldFrame(){_currentMetadata=new Metadata{FrameWidth=607,FrameHeight=436};_currentCodeAsset=new object();_customSequencePlaying=true;_isCalabiyauActive=true;_isBattlefieldTextOverlayActive=true;VisibilityState=Visibility.Visible;_timer.Start();_playbackClock.Start();}
 void CheckCleared(){Check(_currentMetadata==null&&_currentCodeAsset==null&&_currentValorantAsset==null&&_currentBattlefieldAsset==null&&_currentCsolAsset==null,"Old asset survived replacement");Check(!_timer.Running&&!_playbackClock.IsRunning&&VisibilityState==Visibility.Collapsed,"Old frame stayed visible while waiting");Check(!_customSequencePlaying&&!_isCalabiyauActive&&!_isBattlefieldTextOverlayActive&&SpriteCanvas.Invalidations>0,"Old game renderer remained active");}
 public static async Task Verify(){
  var waiting=new PROBE(); waiting.SeedOldFrame();var ready=NewSignal();waiting.preload=ready.Task;
  waiting.PlayInternal(p=>{waiting.loads++;return Task.FromResult(new AnimationAsset());});waiting.CheckCleared();
  Check(waiting.loads==0,"Playback loaded assets before configured preload");
  waiting.PlayInternal(p=>{waiting.loads++;return Task.FromResult(new AnimationAsset());});
  ready.SetResult(true);await Wait(waiting.presented.Task);
  Check(waiting.frames==1&&waiting.loads==1,"Superseded waiting request was loaded or presented");

  var gate=new PROBE();await gate.PreloadGate.WaitAsync();gate.SeedOldFrame();
  gate.PlayInternal(p=>{gate.loads++;return Task.FromResult(new AnimationAsset());},false);gate.CheckCleared();
  var newest=new AnimationAsset();gate.PlayInternal(p=>{throw new Exception("Cached asset loaded");},false,newest);
  Check(ReferenceEquals(gate._currentCodeAsset,newest.CodeAsset)&&gate._timer.Running,"Warm replacement was blocked by an old request");
  gate.PreloadGate.Release();await Task.Delay(50);
  Check(gate.loads==0&&gate.frames==1,"Superseded gate waiter executed its loader");

  foreach(bool fail in new[]{false,true}){
   logged=NewSignal();var race=new PROBE();var late=new TaskCompletionSource<AnimationAsset>(TaskCreationOptions.RunContinuationsAsynchronously);
   race.PlayInternal(p=>late.Task,false);var current=new AnimationAsset();race.PlayInternal(p=>Task.FromResult(current),false,current);
   if(fail){late.SetException(new Exception("Obsolete load failed"));await Wait(logged.Task);}else late.SetResult(new AnimationAsset());
   await Task.Delay(50);
   Check(ReferenceEquals(race._currentCodeAsset,current.CodeAsset)&&race._timer.Running&&race.VisibilityState==Visibility.Visible&&race.frames==1,"Obsolete completion or failure altered the newer playback");
  }
  var stopped=new PROBE();var pending=NewSignal();stopped.preload=pending.Task;
  stopped.PlayInternal(p=>{stopped.loads++;return Task.FromResult(new AnimationAsset());});stopped.BeginReplacementPlayback();pending.SetResult(true);await Task.Delay(50);
  Check(stopped.loads==0&&stopped.frames==0,"Cancelled request revived a stopped frame");
  var changed=new PROBE();var oldLoad=new TaskCompletionSource<AnimationAsset>(TaskCreationOptions.RunContinuationsAsynchronously);
  changed.PlayInternal(p=>oldLoad.Task,false);changed._resourceGeneration++;
  var newPreparation=NewSignal();changed.preload=newPreparation.Task;oldLoad.SetResult(new AnimationAsset());await Task.Delay(50);
  Check(changed.cacheReleases==1&&ReferenceEquals(changed.preload,newPreparation.Task),"Stale resource cleanup erased the newly registered preload");
  Check(changed.frames==0,"Old-generation asset was presented after a resource change");
 }
'@
    $resets = [regex]::Matches($methods, '\b(Reset\w+)\(\);') | ForEach-Object { "void $($_.Groups[1].Value)() {}" } | Select-Object -Unique
    $harness = $harness.Replace('PROBE', $class) + ($resets -join "`n") + "`n" + $methods.Replace('Visibility = Visibility.', 'VisibilityState = Visibility.') + "`n}"
    Add-Type -TypeDefinition $harness -WarningAction SilentlyContinue
    $type = $class -as [type]
    $type::Verify().GetAwaiter().GetResult()
    Write-Output "PASS: $renderer clears replacements immediately; only newest request presents; obsolete completion/failure cannot alter new playback; stale cleanup preserves the new preload."
}

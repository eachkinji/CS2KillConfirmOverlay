param()
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
function Method([string]$source, [string]$name) {
    $match = [regex]::Match($source, '(?ms)^        (?:private|public) [^\r\n]+\b' + $name + '\([^)]*\).*?^        \}')
    if (!$match.Success) { throw "Method not found: $name" }
    $match.Value
}
foreach ($renderer in @('Widget', 'CompatibilityHost/Renderer')) {
    $base = Join-Path $root "$renderer/Controls/Animations"
    $source = Get-Content -Raw "$base/Core/KillConfirmAnimation.Playback.cs"
    $start = $source.IndexOf('        private string _preparedPlaybackQueueKind;')
    $end = $source.IndexOf('        // A replacement owns the canvas immediately')
    if ($start -lt 0 -or $end -le $start) { throw 'Prepared queue methods not found' }
    $queue = $source.Substring($start, $end - $start)
    $core = Get-Content -Raw "$base/Core/KillConfirmAnimation.xaml.cs"
    if ((Method $core 'PlayBattlefield5Kill') -match '\+\+_playToken|_playToken\+\+') { throw 'Battlefield5 public entry cancels its own pending feed' }
    foreach ($game in @('Apex','ModernWarfare2019','Overwatch')) {
        $play = Get-Content -Raw "$base/$game/${game}Animation.Playback.cs"
        if ($play -match '(?:Load\w*BitmapAsync|Ensure\w+ReadyAsync|PreloadGate)') { throw "$game still loads during playback" }
        $expected = switch ($game) { 'Apex' { 2 } 'ModernWarfare2019' { 6 } 'Overwatch' { 1 } }
        if ([regex]::Matches($play, 'QueuePreparedPlayback\(').Count -ne $expected) { throw "$game event gate is incomplete" }
    }
    foreach ($game in @('Battlefield1','Battlefield5','Battlefield6','Battlefield2042','DeltaForce')) {
        $play = Get-Content -Raw "$base/$game/${game}Animation.Playback.cs"
        if ([regex]::Matches($play, 'QueuePreparedPlayback\(').Count -ne 1) { throw "$game event gate is incomplete" }
        if ($play -match '\+\+_playToken|_playToken\+\+') { throw "$game resets its prepared queue token during playback" }
        $entry = switch ($game) {
            'Battlefield1' { 'PlayBattlefield1CompositeKill' }
            'Battlefield5' { 'QueueBattlefield5ScrollingKill' }
            'Battlefield6' { 'PlayBattlefield6Kill' }
            'Battlefield2042' { 'AddBattlefield2042Event' }
            'DeltaForce' { 'AddDeltaForceEvent' }
        }
        if ((Method $play $entry) -match '(?:Load\w+IconAsync|Preload\w+Async|WaitForPreparedPlaybackAsync)') { throw "$game still loads during playback" }
    }
    $mw = Get-Content -Raw "$base/ModernWarfare2019/ModernWarfare2019Animation.Playback.cs"
    $events = (Method $mw 'PlayModernWarfare2019CrosshairKill') + "`n" + (Method $mw 'PlayModernWarfare2019Objective') + "`n" + (Method $mw 'IsModernWarfare2019RoundSettlement')
    $class = if ($renderer -eq 'Widget') { 'WidgetPreparedQueueProbe' } else { 'DesktopPreparedQueueProbe' }
    $harness = @'
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
#pragma warning disable 0414,0649
public class PROBE {
 int _playToken,_resourceGeneration;
 Task preparation=Task.CompletedTask;
 Task WaitForCurrentPackPreloadAsync()=>preparation;
 static class App { public static void Log(string message){} }
 void ReportDesktopPlaybackFailure(Exception error){}
 List<string> events=new List<string>();
 int _modernWarfare2019LastMoneyKillCount,_modernWarfare2019AccumulatedMoney,_modernWarfare2019MoneyReward,prepares;
 bool _isModernWarfare2019Active,_drawModernWarfare2019Primary,_modernWarfare2019KillMarkOnly,_modernWarfare2019IsAssist,_modernWarfare2019IsObjective;
 const double ModernWarfare2019MarkerEndMs=940;
 Stopwatch _playbackClock=new Stopwatch();
 class Canvas { public void Invalidate(){} } Canvas SpriteCanvas=new Canvas();
 void PrepareModernWarfare2019Playback(bool drawPrimary,bool drawLowerBanner,bool drawUpperBanner,bool isHeadshot,int killCount,int moneyReward,bool isAssist=false,bool killMarkOnly=false,string objectiveEventKind=null){
  prepares++;_isModernWarfare2019Active=true;_drawModernWarfare2019Primary=drawPrimary;_modernWarfare2019KillMarkOnly=killMarkOnly;_modernWarfare2019IsAssist=isAssist;_modernWarfare2019IsObjective=objectiveEventKind!=null;_modernWarfare2019MoneyReward=moneyReward;_playbackClock.Restart();events.Add("kill");
 }
 void QueueModernWarfare2019FeedItems(bool headshot,int count,bool assist,string objective,long now){events.Add(objective);}
 static TaskCompletionSource<bool> Signal()=>new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
 static void Check(bool value,string message){if(!value)throw new Exception(message);}
 static async Task Wait(Task task){if(await Task.WhenAny(task,Task.Delay(3000))!=task)throw new Exception("Queue did not complete");await task;}
 public static async Task Verify(){
  foreach(string kind in new[]{"Apex","ModernWarfare2019","Overwatch","Battlefield1","Battlefield5","Battlefield6","Battlefield2042","DeltaForce"}){
   var queued=new PROBE();var ready=Signal();queued.preparation=ready.Task;
   queued.QueuePreparedPlayback(kind,()=>queued.events.Add("1"));queued.QueuePreparedPlayback(kind,()=>queued.events.Add("2"));queued.QueuePreparedPlayback(kind,()=>queued.events.Add("3"));
   Check(queued.events.Count==0,"Feed changed before preload completed");ready.SetResult(true);await Wait(queued._preparedPlaybackQueue);
   Check(string.Join(",",queued.events)=="1,2,3","Preload dropped or reordered feed events");
   queued.QueuePreparedPlayback(kind,()=>queued.events.Add("4"));Check(queued.events.Count==4,"Warm feed yielded unnecessarily");
  }
  foreach(bool changeGeneration in new[]{false,true}){
   var canceled=new PROBE();var old=Signal();canceled.preparation=old.Task;
   canceled.QueuePreparedPlayback("Apex",()=>canceled.events.Add("stale"));Task stale=canceled._preparedPlaybackQueue;
   if(changeGeneration)canceled._resourceGeneration++;else canceled._playToken++;
   canceled.preparation=Task.CompletedTask;canceled.QueuePreparedPlayback("Apex",()=>canceled.events.Add("new"));
   Check(string.Join(",",canceled.events)=="new","New playback waited behind obsolete generation/token");
   old.SetResult(true);await Wait(stale);Check(canceled.events.Count==1,"Obsolete queue revived playback");
  }
  var switched=new PROBE();var pending=Signal();switched.preparation=pending.Task;switched.QueuePreparedPlayback("Apex",()=>switched.events.Add("old game"));Task oldGame=switched._preparedPlaybackQueue;
  switched.preparation=Task.CompletedTask;switched.QueuePreparedPlayback("Overwatch",()=>switched.events.Add("new game"));pending.SetResult(true);await Wait(oldGame);
  Check(string.Join(",",switched.events)=="new game","A prior game's queue modified current playback");
  var failed=new PROBE();failed.preparation=Task.FromException(new Exception("preload failure"));failed.QueuePreparedPlayback("Apex",()=>failed.events.Add("bad"));await Wait(failed._preparedPlaybackQueue);
  Check(failed.events.Count==0,"Failed preload displayed a partial animation");failed.preparation=Task.CompletedTask;failed.QueuePreparedPlayback("Apex",()=>{throw new Exception("event failure");});failed.QueuePreparedPlayback("Apex",()=>failed.events.Add("recovered"));await Wait(failed._preparedPlaybackQueue);
  Check(string.Join(",",failed.events)=="recovered","Failed event poisoned the remaining queue");
  var mw=new PROBE();var textures=Signal();mw.preparation=textures.Task;mw.PlayModernWarfare2019CrosshairKill(false,1,100);mw.PlayModernWarfare2019Objective("round_win",200);
  Check(mw.prepares==0&&mw.events.Count==0,"MW changed state before preload");textures.SetResult(true);await Wait(mw._preparedPlaybackQueue);
  Check(mw.prepares==1&&mw._modernWarfare2019MoneyReward==300&&string.Join(",",mw.events)=="kill,round_win","MW settlement replaced the pending kill or lost its reward");
 }
'@
    $harness = $harness.Replace('PROBE',$class) + "`n" + $queue + "`n" + $events + "`n}"
    Add-Type -TypeDefinition $harness -WarningAction SilentlyContinue
    $type = $class -as [type]
    $type::Verify().GetAwaiter().GetResult()
    Write-Output "PASS: $renderer queued events wait for preparation in order; warm events run immediately; stop/pack/game changes cancel old queues; failures do not publish; MW kill and settlement remain merged."
}

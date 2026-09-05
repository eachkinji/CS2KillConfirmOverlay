#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$engine = Join-Path $root 'Widget/Danmaku/Engine'
# Compile production scheduling, queue, settings and motion; stub only platform
# storage and text repositories so fake-clock checks do not require a UWP host.
$sources = @('DanmakuReactionPolicy.cs','DanmakuImpulseManager.cs','DanmakuLiveScheduler.cs','DanmakuPendingQueue.cs','DanmakuBatchComposer.cs','DanmakuMotion.cs') | ForEach-Object { Get-Content -Raw (Join-Path $engine $_) }
$eventSource = Get-Content -Raw (Join-Path $engine 'DanmakuEvent.cs')
$sources += $eventSource.Substring(0,$eventSource.IndexOf('    internal static class DanmakuEventClassifier')) + "`n}"
$sources += Get-Content -Raw (Join-Path $root 'Widget/Danmaku/DanmakuSettingsStore.cs')
$header = "using System; using System.Collections.Generic; using KillConfirmGameBar.Danmaku; using KillConfirmGameBar.Danmaku.Engine; using Windows.Storage; using Windows.UI.Text;`n"
$code = $header + (($sources | ForEach-Object { $_ -replace '(?m)^using .*;\r?\n','' }) -join "`n")
$code += @'
namespace Windows.Storage {
 public class ApplicationData { public static ApplicationData Current = new ApplicationData(); public ApplicationDataContainer LocalSettings = new ApplicationDataContainer(); }
 public class ApplicationDataContainer { public Store Values = new Store(); }
 public class Store : Dictionary<string,object> { public new object this[string k] { get { return ContainsKey(k)?base[k]:null; } set { base[k]=value; } } }
}
namespace Windows.UI.Text { public struct FontWeight {} public static class FontWeights { public static FontWeight Normal, SemiBold, Bold, ExtraBold; } }
namespace KillConfirmGameBar.Danmaku.Engine {
 internal class DanmakuSelectionHistory {}
 internal class SemanticEventProfile { public double ImpulseDurationSeconds=99, ImpulseStrength=1; }
 internal class AmbientProfile { public object PreferredTopics=null,PreferredStances=null,PreferredTargets=null,AllowedContexts=null; public double BaseIntervalSeconds=3.2,IntervalJitter=0; }
 internal static class SemanticProfileRepository { public static AmbientProfile Ambient=new AmbientProfile(); }
 internal class DanmakuSelectionResult { public string Text="text",RejectionReason=""; public int SourceIndex=1; public bool IsSuccess=true; }
 internal class DanmakuWeightEngine {
  public DanmakuWeightEngine(Random r) {}
  public DanmakuSelectionResult SelectEventDanmaku(DanmakuEventKind k,DanmakuSelectionHistory h,DanmakuMessageRole role,DanmakuSelectionHistory sessionHistory=null) {return new DanmakuSelectionResult();}
  public DanmakuSelectionResult SelectOpeningDanmaku(DanmakuSelectionHistory h,DanmakuMessageRole role,bool b) {return new DanmakuSelectionResult();}
  public DanmakuSelectionResult SelectSemanticDanmaku(object a,object b,object c,object d,DanmakuSelectionHistory h,DanmakuMessageRole role) {return new DanmakuSelectionResult();}
 }
}
public static class DanmakuPacingChecks {
 static void Check(bool ok,string message) {if(!ok)throw new Exception(message);}
 public static void Run() {
  foreach(DanmakuEventKind kind in Enum.GetValues(typeof(DanmakuEventKind))) {
   var start=DateTimeOffset.UtcNow;var now=start;var manager=new DanmakuImpulseManager();
   var impulse=manager.AddImpulse(new DanmakuEventContext{Kind=kind},new SemanticEventProfile(),start);
   var scheduler=new DanmakuLiveScheduler(manager,new DanmakuWeightEngine(new Random(1)),new DanmakuSelectionHistory(),new Random(1),()=>now);
   int count=0,bursts=0,followups=0;var times=new List<double>();
   for(int tick=0;tick<=300;tick++) { now=start.AddMilliseconds(tick*10);var step=scheduler.Step();
    if(step.Message!=null && step.Message.IsEventReaction) {count++;times.Add((now-start).TotalSeconds);if(step.DiagnosticRole.StartsWith("EventBurst:"))bursts++;else followups++;}
   }
   Check(count==5 && bursts==2 && followups==3,"Expected 2+3 for "+kind);
   Check(times[1]<=0.25 && times[2]-times[1]>=0.4 && times[4]<2,"Invalid timing for "+kind);
   Check(!manager.HasActiveImpulse(now),"Quota must stop dispatching");
   var test=new DanmakuBatchComposer(new Random(1)).Compose(new DanmakuEventContext{Kind=kind},999);
   Check(test.Count==5,"Preview must ignore oversized requested batch");
   Check((test[1].NotBefore-test[0].NotBefore).TotalSeconds==0.2 && (test[4].NotBefore-test[0].NotBefore).TotalSeconds==1.55,"Preview/live timing mismatch");
  }
  var clock=DateTimeOffset.UtcNow;var queue=new DanmakuPendingQueue(()=>clock);
  queue.Enqueue(new[]{new DanmakuMessage{Text="later",IsEventReaction=true,NotBefore=clock.AddSeconds(.5),ExpiresAt=clock.AddSeconds(2)},new DanmakuMessage{Text="ambient"}},12);
  DanmakuQueueItem item;Check(queue.TryDequeue(out item)&&item.Message.Text=="ambient","Future event must not block ambient");
  Check(!queue.TryPeek(out item),"Future event must wait");clock=clock.AddSeconds(.5);Check(queue.TryPeek(out item),"Due event must become eligible");
  clock=clock.AddSeconds(1.5);Check(!queue.TryDequeue(out item)&&queue.Count==0,"Backlogged event must expire at two seconds");
  Check(DanmakuReactionPolicies.EventMaximumActiveCount==9 && DanmakuReactionPolicies.ClampVisibleCount(999)==9,"Hard cap must be nine");
  var store=ApplicationData.Current.LocalSettings.Values;store[DanmakuSettingsStore.SpeedSettingKey]=2;store[DanmakuSettingsStore.DurationSettingKey]=3.0;
  Check(DanmakuSettingsStore.Speed==DanmakuSpeedMode.UltraSlow && DanmakuSettingsStore.DurationSeconds==15,"Legacy fast settings must migrate");
  Check(DanmakuMotion.ResolveFlightDuration(DanmakuSpeedMode.Fast,3,new Random(1))==12,"Legacy/custom short cap must not restore fast flight");
  var modes=new[]{DanmakuSpeedMode.UltraSlow,DanmakuSpeedMode.Leisurely,DanmakuSpeedMode.Drifting,DanmakuSpeedMode.Slowest};
  var durations=new[]{12.0,18.0,24.0,30.0};
  for(int i=0;i<modes.Length;i++){DanmakuSettingsStore.Speed=modes[i];DanmakuSettingsStore.DurationSeconds=3;
   Check(DanmakuMotion.ResolveFlightDuration(modes[i],DanmakuSettingsStore.DurationSeconds,new Random(1))==durations[i],"Cap must not accelerate slow flight");}
 }
}
'@
Add-Type -TypeDefinition $code -WarningAction SilentlyContinue
[DanmakuPacingChecks]::Run()
'PASS: production scheduler and preview emit 2+3 within 2 seconds; expired queue items drop; cap 9; legacy speed migration and 12/18/24/30-second flight verified.'

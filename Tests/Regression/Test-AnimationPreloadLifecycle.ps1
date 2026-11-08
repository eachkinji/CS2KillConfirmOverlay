param()
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
. (Join-Path $PSScriptRoot '..\Common\SourceView.ps1')
function Method([string]$text, [string]$name) {
    $match = [regex]::Match($text, '(?ms)^        (?:private|public) [^\r\n]+\b' + $name + '\([^)]*\).*?^        \}')
    if (!$match.Success) { throw "Missing production method: $name" }
    $match.Value
}
foreach ($variant in @('Widget', 'CompatibilityHost/Renderer')) {
    $core = Resolve-SourceDirectory $root "$variant/Controls/Animations/Core"
    $text = Get-Content -Raw (Join-Path $core 'KillConfirmAnimation.Preloading.cs')
    $className = if ($variant -eq 'Widget') { 'WidgetPreparationProbe' } else { 'DesktopPreparationProbe' }
    $members = @()
    foreach ($name in @('WaitForCurrentPackPreloadAsync', 'PreloadCurrentPackAnimationsAsync', 'PreloadCurrentPackAnimationsOnceAsync')) {
        $members += Method $text $name
    }
    foreach ($name in @('CurrentPackPreloadSignature', 'IsCurrentPackPreloaded', 'IsCurrentPackPreloading')) {
        $match = [regex]::Match($text, '(?ms)^        (?:private|public) [^\r\n]*\b' + $name + ' =>.*?;')
        if (!$match.Success) { throw "Missing production property: $name" }
        $members += $match.Value
    }
    $source = @'
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
public class CLASSNAME {
    static Task _currentPackPreloadTask;
    static string _currentPackPreloadSignature;
    static string _iconPack = "fixture";
    static int _resourceGeneration;
    static SemaphoreSlim PreloadGate = new SemaphoreSlim(1, 1);
    static Queue<TaskCompletionSource<bool>> pending = new Queue<TaskCompletionSource<bool>>();
    static int loads, stalePurges;
    static class GameStyleService { public static string Current="valorant"; public static bool IsDagoujiaoKey(string key)=>false; }
    static class ValorantPackService { public static bool IsValorantPackKey(string key)=>true; }
    static class ValorantExternalAssetService { public static string GetResourceRevision(string key)=>"revision1"; }
    static string GetCodeKillCacheKey(string a,string b)=>_iconPack;
    static string DagoujiaoPreloadSettingsSignature()=>"";
    void ReleaseCustomSequence() {}
    static void ReleaseAllAnimationResourceCaches(bool invalidatePreload=true) {
        stalePurges++;
        if(invalidatePreload) { _currentPackPreloadTask=null; _currentPackPreloadSignature=null; }
    }
    Task PreloadCurrentPackAnimationsCoreAsync(IProgress<int> progress) {
        loads++;
        progress?.Report(100);
        return pending.Dequeue().Task;
    }
    static TaskCompletionSource<bool> Next() {
        var gate=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        pending.Enqueue(gate);return gate;
    }
    static void Check(bool ok,string message) { if(!ok)throw new Exception(message); }
    public static async Task Verify() {
        var primary=new CLASSNAME();var secondary=new CLASSNAME();
        bool refused=false;
        try { await primary.WaitForCurrentPackPreloadAsync(); } catch(InvalidOperationException) { refused=true; }
        Check(refused&&loads==0,"Playback wait started an unrequested load");
        var first=Next();
        Task a=primary.PreloadCurrentPackAnimationsAsync(null);
        Task b=secondary.PreloadCurrentPackAnimationsAsync(null);
        Check(loads==1&&primary.IsCurrentPackPreloading&&!primary.IsCurrentPackPreloaded,"Concurrent controls did not share preparation or readiness was premature");
        first.SetResult(true);await Task.WhenAll(a,b);
        Check(primary.IsCurrentPackPreloaded&&!primary.IsCurrentPackPreloading,"Completed pack was not ready");
        await primary.WaitForCurrentPackPreloadAsync();
        await primary.PreloadCurrentPackAnimationsAsync(null);
        Check(loads==1,"Ready playback/configuration reloaded the pack");

        _resourceGeneration++;
        var oldGate=Next();Task old=primary.PreloadCurrentPackAnimationsAsync(null);
        _resourceGeneration++;
        var latestGate=Next();Task latest=secondary.PreloadCurrentPackAnimationsAsync(null);
        Check(!primary.IsCurrentPackPreloaded,"New generation inherited old readiness");
        oldGate.SetResult(true);
        bool canceled=false;try {await old;}catch(OperationCanceledException){canceled=true;}
        Check(canceled&&stalePurges==1,"Stale preload was published or not purged");
        latestGate.SetResult(true);await latest;
        Check(primary.IsCurrentPackPreloaded&&loads==3,"Stale cleanup erased the newer preload task");

        _resourceGeneration++;
        var failedGate=Next();Task failure=primary.PreloadCurrentPackAnimationsAsync(null);
        failedGate.SetException(new InvalidOperationException("fixture decode failure"));
        bool failed=false;try {await failure;}catch(InvalidOperationException){failed=true;}
        Check(failed&&!primary.IsCurrentPackPreloaded,"Failed preparation was marked ready");
        try {await secondary.WaitForCurrentPackPreloadAsync();}catch(InvalidOperationException){}
        Check(loads==4,"Playback retried failed preparation implicitly");
        var retryGate=Next();Task retry=primary.PreloadCurrentPackAnimationsAsync(null);
        retryGate.SetResult(true);await retry;
        Check(loads==5&&primary.IsCurrentPackPreloaded,"Explicit preparation could not retry");
    }
MEMBERS
}
'@
    $source = $source.Replace('CLASSNAME',$className).Replace('MEMBERS',($members -join "`n"))
    Add-Type -TypeDefinition $source
    $type = $className -as [type]
    $type::Verify().GetAwaiter().GetResult()
    Write-Host "PASS $variant : shared preparation, zero-load ready replay, stale-generation cancellation, newer-task preservation, failure readiness, explicit retry."
}

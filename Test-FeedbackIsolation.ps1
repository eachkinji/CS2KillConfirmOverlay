#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
$root = Join-Path $PSScriptRoot 'Widget/Controls/Animations'
$bf1 = Get-Content -Raw (Join-Path $root 'Battlefield1/Battlefield1Animation.Playback.cs')
$state = Get-Content -Raw (Join-Path $root 'Battlefield1/Battlefield1Animation.State.cs')
$events = Get-Content -Raw (Join-Path $root 'Shared/Battlefield/BattlefieldAnimation.Events.cs')
$pubg = Get-Content -Raw (Join-Path $root 'Pubg/PubgAnimation.Playback.cs')
$pubgModels = Get-Content -Raw (Join-Path $root 'Pubg/PubgAnimation.Data.cs')
function Get-Method([string]$source, [string]$name) {
    $match = [regex]::Match($source, '(?ms)^        private [^\r\n]+\b' + $name + '\([^)]*\).*?^        \}')
    if (-not $match.Success) { throw "Production method missing: $name" }
    $match.Value
}
$members = @(
    foreach ($name in @('PlayBattlefield1CompositeKill', 'PrepareBattlefield1TextOverlayPlayback', 'LoadBattlefield1PrimaryAsync')) {
        Get-Method $bf1 $name
    }
    Get-Method $state 'UpdateBattlefield1CompositeFrame'
    foreach ($name in @('NormalizeBattlefieldEventKind', 'IsBattlefieldTextOnlyEvent', 'IsRoundBonusEvent', 'IsRoundWinEvent', 'IsRoundLossEvent', 'IsObjectiveBonusEvent', 'GetObjectiveBonusLabel')) {
        Get-Method $events $name
    }
    Get-Method $pubg 'CreatePubgFeedItem'
    foreach ($name in @('PubgFeedItem', 'PubgFeedKind')) {
        $match = [regex]::Match($pubgModels, '(?ms)^        private (?:sealed class|enum) ' + $name + '\r?\n.*?^        \}')
        if (-not $match.Success) { throw "Production model missing: $name" }
        $match.Value
    }
)
# Exercise real BF1 dispatch, asynchronous loading and ticking with deterministic
# clocks and asset loading. GPU drawing and the waterfall's own renderer are stubbed.
$harness = @'
#pragma warning disable 0414, 0649
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
namespace Windows.UI.Xaml { public enum Visibility { Visible, Collapsed } }
public sealed class FeedbackIsolationChecks
{
    private const int BattlefieldFrameWidth = 607, BattlefieldFrameHeight = 260, Battlefield1FrameCount = 282, FrameSequenceFps = 60;
    private bool _isBattlefieldTextOverlayActive, _isBattlefield5ScrollingActive, _isBattlefield4HudActive,
        _isBattlefield2042HudActive, _isPubgHudActive, _isDeltaForceHudActive, _contentSizedViewport, _isBattlefield1CompactLayoutActive;
    private int _playToken, _currentFrame, viewportChanges, textEvents;
    private double _battlefieldPrimaryStartTimeMs;
    private object _currentCsolAsset, _currentCodeAsset, _currentValorantAsset;
    private BattlefieldKillAsset _currentBattlefieldAsset;
    private SpriteMetadata _currentMetadata;
    private readonly Clock _playbackClock = new Clock();
    private readonly Timer _timer = new Timer();
    private readonly Canvas SpriteCanvas = new Canvas();
    private readonly ScrollState _battlefield5ScrollState = new ScrollState();
    private readonly List<TaskCompletionSource<AnimationAsset>> loads = new List<TaskCompletionSource<AnimationAsset>>();
    private Windows.UI.Xaml.Visibility Visibility;
    private bool overlayVisible = true;
    private string lastEvent;
    private int lastReward;
    private sealed class Clock
    {
        public bool IsRunning;
        public double Ms;
        public TimeSpan Elapsed => TimeSpan.FromMilliseconds(Ms);
        public void Restart() { Ms = 0; IsRunning = true; }
        public void Stop() { IsRunning = false; }
    }
    private sealed class Timer
    {
        public bool IsEnabled;
        public TimeSpan Interval;
        public void Start() { IsEnabled = true; }
        public void Stop() { IsEnabled = false; }
    }
    private sealed class Canvas { public void Invalidate() {} }
    private sealed class BattlefieldKillAsset {}
    private sealed class SpriteMetadata { public int FrameWidth, FrameHeight, Frames, Fps; }
    private sealed class AnimationAsset { public SpriteMetadata Metadata; public BattlefieldKillAsset BattlefieldAsset; }
    private sealed class ScrollState
    {
        public readonly List<object> ActiveIcons = new List<object>();
        public readonly Queue<object> PendingIcons = new Queue<object>();
        public object KillFeedItem;
    }
    private void ApplyViewportSize(double width, double height) { viewportChanges++; }
    private void ApplyBattlefield1TextOnlyViewport() { viewportChanges++; _isBattlefield1CompactLayoutActive = true; }
    private void ApplyBattlefield1CompositionViewport(BattlefieldKillAsset asset) { viewportChanges++; _isBattlefield1CompactLayoutActive = true; }
    private void HideLoadingProgress() {}
    private void ResetBattlefield5ScrollingState() { _isBattlefield5ScrollingActive = false; }
    private void UpdateBattlefield5TextItems(double now) {}
    private bool HasBattlefieldTextOverlayVisible(double now) { return overlayVisible; }
    private void AddBattlefield1TextOverlayEvent(int count, bool headshot, bool knife, bool assist, string target, string weapon,
        int reward, string kind, int round, int epoch, double now)
    {
        textEvents++; lastEvent = kind; lastReward = reward;
    }
    private Task<AnimationAsset> LoadBattlefieldKillAssetAsync(string style, int count, bool headshot, bool knife, bool assist,
        string target, string weapon, int reward, string kind, int round, int epoch)
    {
        var source = new TaskCompletionSource<AnimationAsset>();
        loads.Add(source);
        return source.Task;
    }
    private static AnimationAsset Asset()
    {
        return new AnimationAsset { BattlefieldAsset = new BattlefieldKillAsset(),
            Metadata = new SpriteMetadata { Frames = 180, Fps = 60 } };
    }
    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    private void Event(string kind, int round = 1)
    {
        PlayBattlefield1CompositeKill(2, false, false, kind == "assist", "Player", "AK-47", 300, kind, round, round);
    }
    public static string Run()
    {
        string[] rewards = { "round_win", "round_loss", "bomb_plant", "bomb_defuse", "hostage_interact", "hostage_rescue", "assist" };
        foreach (string kind in rewards)
        {
            var app = new FeedbackIsolationChecks();
            app.PrepareBattlefield1TextOverlayPlayback();
            var asset = Asset();
            app._currentBattlefieldAsset = asset.BattlefieldAsset;
            app._currentMetadata = asset.Metadata;
            app._isBattlefield1CompactLayoutActive = true;
            app._contentSizedViewport = true;
            app._battlefieldPrimaryStartTimeMs = 250;
            app._playbackClock.Ms = 1000;
            app._currentFrame = 45;
            int token = app._playToken, viewport = app.viewportChanges;
            app.Event(kind, 2); // A reward may also carry a new round/money epoch.
            Check(app._currentBattlefieldAsset == asset.BattlefieldAsset && app._currentMetadata == asset.Metadata,
                "Reward replaced the active card or metadata: " + kind);
            Check(app._playToken == token && app._currentFrame == 45 && app._battlefieldPrimaryStartTimeMs == 250,
                "Reward restarted/cancelled the card: " + kind);
            Check(app.viewportChanges == viewport && app._isBattlefield1CompactLayoutActive && app._contentSizedViewport,
                "Reward resized or reset the card viewport: " + kind);
            Check(app.loads.Count == 0 && app.textEvents == 1 && app.lastEvent == kind && app.lastReward == 300,
                "Reward did not update only the waterfall: " + kind);
            app._playbackClock.Ms = 1250;
            app.UpdateBattlefield1CompositeFrame();
            Check(app._currentFrame == 60 && app._currentBattlefieldAsset == asset.BattlefieldAsset, "Card no longer progresses independently.");
            app._playbackClock.Ms = 3250;
            app.UpdateBattlefield1CompositeFrame();
            Check(app._currentBattlefieldAsset == null && app._timer.IsEnabled, "Card lifetime was extended, or waterfall stopped with it.");

            var pending = new FeedbackIsolationChecks();
            pending.Event("kill");
            int pendingToken = pending._playToken;
            pending._playbackClock.Ms = 100;
            pending.Event(kind);
            Check(pending._playToken == pendingToken && pending.loads.Count == 1, "Reward cancelled a pending card.");
            var loaded = Asset();
            pending.loads[0].SetResult(loaded);
            Check(pending._currentBattlefieldAsset == loaded.BattlefieldAsset && pending._battlefieldPrimaryStartTimeMs == 100,
                "Card failed to appear after intervening reward: " + kind);
        }
        var overlap = new FeedbackIsolationChecks();
        overlap.Event("kill"); overlap.Event("kill");
        overlap.loads[0].SetResult(Asset());
        Check(overlap._currentBattlefieldAsset == null, "Stale kill load replaced a newer kill.");
        var newest = Asset(); overlap.loads[1].SetResult(newest);
        Check(overlap._currentBattlefieldAsset == newest.BattlefieldAsset, "New kill did not replace previous kill.");
        overlap.overlayVisible = false;
        overlap._playbackClock.Ms = 10000;
        overlap.UpdateBattlefield1CompositeFrame();
        Check(!overlap._timer.IsEnabled && !overlap._playbackClock.IsRunning, "Completed playback did not stop.");
        var onlyReward = new FeedbackIsolationChecks(); onlyReward.Event("round_win");
        Check(onlyReward._timer.IsEnabled && onlyReward._currentBattlefieldAsset == null && onlyReward.loads.Count == 0,
            "Standalone waterfall event created a card or failed to start.");

        foreach (int reward in new[] { 0, 300, 16000, int.MaxValue })
        foreach (string kind in rewards)
        {
            var item = CreatePubgFeedItem(false, false, kind == "assist", "Player42", "AK-47", reward, kind);
            Check(!item.PlainText.Contains("+") && !System.Text.RegularExpressions.Regex.IsMatch(item.PlainText, "[0-9]"),
                "PUBG objective/round notification still contains an amount.");
        }
        var kill = CreatePubgFeedItem(true, false, false, "Player42", "AK-47", 300, "kill");
        Check(kill.Kind == PubgFeedKind.Headshot && kill.TargetName == "Player42" && kill.WeaponName == "AK-47",
            "Removing money changed PUBG kill details.");
        return "PASS: BF1 active/pending cards survive 7 reward/assist event types; independent expiry and newer-kill precedence; PUBG removes amounts but preserves kill details.";
    }
__MEMBERS__
}
'@
if (-not ('FeedbackIsolationChecks' -as [type])) {
    Add-Type -TypeDefinition $harness.Replace('__MEMBERS__', ($members -join "`n"))
}
[FeedbackIsolationChecks]::Run()

$combo = Get-Content -Raw (Join-Path $root 'Pubg/PubgAnimation.Combo.cs')
if ($combo -notmatch '_pubgHudState.CurrentCombo' -or $combo -notmatch 'combo.ToString\(CultureInfo.InvariantCulture\)') {
    throw 'PUBG elimination/assist counts must remain unchanged.'
}
'PASS: PUBG elimination/assist count rendering remains enabled.'

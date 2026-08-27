#Requires -Version 7.0
$ErrorActionPreference = 'Stop'

# Compile the actual text, cache preparation and event queue methods. Only GPU
# resources and font measurement are stubbed; this is not a visual rendering test.
$root = Join-Path $PSScriptRoot 'Widget/Controls/Animations'
$playback = Get-Content -Raw (Join-Path $root 'Battlefield2042/Battlefield2042Animation.Playback.cs')
$models = Get-Content -Raw (Join-Path $root 'Battlefield2042/Battlefield2042Animation.Models.cs')
$text = Get-Content -Raw (Join-Path $root 'Battlefield2042/Battlefield2042Animation.Text.cs')
$cache = Get-Content -Raw (Join-Path $root 'Battlefield2042/Battlefield2042Animation.FeedCache.cs')
$data = Get-Content -Raw (Join-Path $root 'Battlefield2042/Battlefield2042Animation.Data.cs')
$layout = Get-Content -Raw (Join-Path $root 'Battlefield2042/Battlefield2042Animation.FeedLayout.cs')
$events = Get-Content -Raw (Join-Path $root 'Shared/Battlefield/BattlefieldAnimation.Events.cs')
$money = Get-Content -Raw (Join-Path $root 'Battlefield5/Battlefield5Animation.Money.cs')
function Get-Method([string]$source, [string]$name) {
    $match = [regex]::Match($source, '(?ms)^        private [^\r\n]+\b' + $name + '\([^)]*\).*?^        \}')
    if (-not $match.Success) { throw "Production method missing: $name" }
    $match.Value
}
$members = @(
    foreach ($name in @('CreateBattlefield2042FeedItem', 'ResolveBattlefield2042EventLabel', 'FormatBattlefield2042MoneyReward', 'FormatBattlefield2042MoneyTotal')) {
        Get-Method $text $name
    }
    foreach ($name in @('NormalizeBattlefieldEventKind', 'IsRoundBonusEvent', 'IsRoundWinEvent', 'IsRoundLossEvent', 'IsObjectiveBonusEvent', 'GetObjectiveBonusLabel', 'GetObjectiveBonusLabelEnglish')) {
        Get-Method $events $name
    }
    foreach ($name in @('AddBattlefield2042Event', 'AddBattlefield2042FeedItem', 'AddBattlefield2042MoneyItem', 'EnsureBattlefield2042Scope', 'BeginBattlefield2042ExitSequence', 'RemoveFinishedBattlefield2042Items', 'GetBattlefield2042IconFileName')) {
        Get-Method $playback $name
    }
    Get-Method $cache 'PrepareBattlefield2042FeedItemCache'
    Get-Method $cache 'PrepareBattlefield2042MoneyItemCache'
    Get-Method $money 'NormalizeBattlefieldMoneyReward'
    Get-Method $money 'FormatBattlefieldMoney'
    Get-Method $layout 'ResolveBattlefield2042MoneyTotalX'
    Get-Method $layout 'ResolveBattlefield2042MoneyTotalScale'
    Get-Method $layout 'ResolveBattlefield2042MoneyFeedX'
    foreach ($name in @('Battlefield2042FeedItem', 'Battlefield2042MoneyItem', 'Battlefield2042KillIconItem', 'Battlefield2042HudState')) {
        $match = [regex]::Match($models, '(?ms)^        private sealed class ' + $name + '\r?\n.*?^        \}')
        if (-not $match.Success) { throw "Production model missing: $name" }
        $match.Value
    }
    [regex]::Matches($data, 'private const (?:int|double) \w+ = [0-9.]+;') | ForEach-Object Value
)
$harness = @'
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
public sealed class Battlefield2042FeedbackChecks
{
    private readonly Battlefield2042HudState _battlefield2042HudState = new Battlefield2042HudState();
    private readonly Stopwatch _playbackClock = new Stopwatch();
    private readonly object _battlefield2042TextFormat = new object();
    private readonly Canvas SpriteCanvas = new Canvas();
    private static readonly Color Battlefield2042EnemyColor = new Color();
    private enum UiLanguage { English, SimplifiedChinese }
    private static class LocalizationManager { public static UiLanguage Current; }
    private class Canvas { public void Invalidate() {} }
    private class CanvasBitmap {}
    private class Battlefield2042KillIconRenderCache {}
    private class Battlefield2042GlowCache : IDisposable { public void Dispose() {} }
    private struct Rect { public double Width; }
    private struct Color { public static Color FromArgb(int a, int r, int g, int b) { return new Color(); } }
    private static class Colors { public static Color White = new Color(); }
    private static Rect MeasureBattlefieldTextBounds(string s, object f) { return new Rect { Width = s.Length * 12 }; }
    private static double MeasureBattlefieldTextAdvance(string s, object f) { return s.Length * 12; }
    private static double MeasureBattlefieldTextWidth(string s, object f) { return s.Length * 12; }
    private static Battlefield2042GlowCache CreateBattlefield2042TextGlowCache(string s, double scale, Color c, double glow, object f) { return null; }
    private static Battlefield2042GlowCache CreateBattlefield2042RectangleGlowCache(double w, double h, Color c, double glow) { return null; }
    private static void AddBattlefieldMoneyReward(string style, int reward, int round, int epoch, double now) {}
    private static Task<CanvasBitmap> LoadBattlefield2042IconAsync(string file) { return Task.FromResult<CanvasBitmap>(null); }
    private static void PrepareBattlefield2042KillIconCache(Battlefield2042KillIconItem item) {}
    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }

    public static string Run()
    {
        double totalX = ResolveBattlefield2042MoneyTotalX();
        foreach (double amountWidth in new[] { 0.0, 20.0, 100.0, 250.0 })
        foreach (double exitEase in new[] { 0.0, 0.5, 1.0 })
        {
            double cursorRight = ResolveBattlefield2042MoneyFeedX(amountWidth, exitEase)
                + amountWidth + Battlefield2042MoneyCursorGap + Battlefield2042MoneyCursorWidth;
            Check(totalX - cursorRight >= Battlefield2042MoneyTotalGap - 0.001, "Total overlaps reward/cursor column.");
        }
        foreach (double width in new[] { 50.0, 100.0, 200.0, 400.0 })
        foreach (double requestedScale in new[] { 1.0, 1.48, 1.9, 2.5 })
        {
            double scale = ResolveBattlefield2042MoneyTotalScale(width, requestedScale);
            Check(scale > 0 && scale <= requestedScale, "Invalid total text scale.");
            Check(totalX + width * scale <= Battlefield2042FrameWidth - Battlefield2042MoneyTotalRightPadding + 0.001,
                "Long/pulsing total is clipped by the canvas.");
        }
        var cases = new[] {
            new[] { "kill", "击杀", "KILL" },
            new[] { "headshot", "爆头击杀", "HEADSHOT KILL" },
            new[] { "knife", "刀杀", "MELEE KILL" },
            new[] { "grenade", "雷杀", "GRENADE KILL" },
            new[] { "assist", "助攻", "ASSIST" },
            new[] { "bomb_plant", "安放炸弹", "BOMB PLANTED" },
            new[] { "bomb_defuse", "拆除炸弹", "BOMB DEFUSED" },
            new[] { "hostage_interact", "接触人质", "HOSTAGE SECURED" },
            new[] { "hostage_rescue", "救出人质", "HOSTAGE RESCUED" },
            new[] { "round_win", "回合胜利", "ROUND WON" },
            new[] { "round_loss", "回合失败", "ROUND LOST" }
        };
        int checkedRows = 0;
        foreach (bool chinese in new[] { false, true })
        foreach (int reward in new[] { -10, 0, 300, 16000 })
        foreach (var test in cases)
        {
            var app = new Battlefield2042FeedbackChecks();
            LocalizationManager.Current = chinese ? UiLanguage.SimplifiedChinese : UiLanguage.English;
            string kind = test[0];
            bool headshot = kind == "headshot", knife = kind == "knife", grenade = kind == "grenade", assist = kind == "assist";
            string eventKind = headshot || knife || grenade ? "kill" : kind;
            bool objective = IsObjectiveBonusEvent(eventKind) || IsRoundBonusEvent(eventKind);
            string expected = test[chinese ? 1 : 2];
            app.AddBattlefield2042Event(3, headshot, knife, grenade, assist, "Player", "AK-47", reward, eventKind, 1, 0);
            Check(app._battlefield2042HudState.FeedItems.Count == 1, "Missing description row: " + kind);
            Check(app._battlefield2042HudState.MoneyItems.Count == 1, "Missing paired amount row: " + kind);
            var row = app._battlefield2042HudState.FeedItems[0];
            var amount = app._battlefield2042HudState.MoneyItems[0];
            Check(row.EventLabel == expected && row.FullText.StartsWith(expected), "Event label not in rendered text: " + kind);
            Check(objective ? row.TargetName == "" && row.WeaponName == "" : row.TargetName == "Player", "Incorrect target information: " + kind);
            Check(!assist || row.WeaponName == "", "Assist has a weapon prefix.");
            Check(row.MoneyText == amount.Text && row.RevealTimeMs == amount.RevealTimeMs, "Amount and description diverged.");
            Check(amount.Text == (reward > 0 ? "+$" + reward.ToString("N0", CultureInfo.InvariantCulture) : ""), "Incorrect amount text.");
            Check(app._battlefield2042HudState.KillIconItems.Count == (objective ? 0 : 1), "Objective created a kill icon.");
            var normalized = CreateBattlefield2042FeedItem(headshot, knife, grenade, assist, null, null, reward,
                " " + eventKind.ToUpperInvariant() + " ", 0, chinese);
            Check(normalized.EventLabel == expected, "Event normalization changed label.");
            Check(normalized.TargetName == (objective ? "" : chinese ? "敌人" : "ENEMY"), "Missing-name fallback incorrect.");
            checkedRows++;
        }
        Check(FormatBattlefield2042MoneyTotal(16000, true) == "累计 $16,000", "Chinese total missing label.");
        Check(FormatBattlefield2042MoneyTotal(16000, false) == "TOTAL $16,000", "English total missing label.");
        Check(CreateBattlefield2042FeedItem(false, false, false, false, null, null, 0, "unknown", 0, true).EventLabel == "击杀", "Unknown event lost its fallback.");

        // Mix delayed kills, objectives, assists and zero-reward rows in one burst.
        var mixed = new Battlefield2042FeedbackChecks();
        for (int i = 0; i < 12; i++)
        {
            mixed.AddBattlefield2042Event(i + 1, false, false, false, i % 3 == 2,
                "Player", "AK-47", i % 2 == 0 ? 0 : 300,
                i % 3 == 0 ? "bomb_plant" : i % 3 == 1 ? "kill" : "assist", 1, 0);
        }
        Check(mixed._battlefield2042HudState.FeedItems.Count(x => !x.IsExiting) == 5, "Feed limit changed.");
        Check(mixed._battlefield2042HudState.MoneyItems.Count(x => !x.IsExiting) == 5, "Amount limit changed.");
        for (int i = 0; i < 12; i++)
        {
            var row = mixed._battlefield2042HudState.FeedItems[i];
            var amount = mixed._battlefield2042HudState.MoneyItems[i];
            Check(row.MoneyReward == amount.MoneyReward && row.RevealTimeMs == amount.RevealTimeMs
                && row.ExitStartTimeMs == amount.ExitStartTimeMs, "Mixed event rows became misaligned.");
        }
        mixed.BeginBattlefield2042ExitSequence(4000);
        mixed.RemoveFinishedBattlefield2042Items(10000);
        Check(mixed._battlefield2042HudState.FeedItems.Count == 0 && mixed._battlefield2042HudState.MoneyItems.Count == 0,
            "Description or amount remained after exit.");
        return "PASS: " + checkedRows + " localized event/reward cases; independent total column, zero rewards, mixed burst alignment, row limits and exit cleanup.";
    }
__MEMBERS__
}
'@
if (-not ('Battlefield2042FeedbackChecks' -as [type])) {
    Add-Type -TypeDefinition $harness.Replace('__MEMBERS__', ($members -join "`n"))
}
[Battlefield2042FeedbackChecks]::Run()

$routing = Get-Content -Raw (Join-Path $PSScriptRoot 'Widget/Pages/KillConfirmWidget/Animation/KillConfirmWidgetPage.Animation.cs')
if ($routing -notmatch 'PlayBattlefield2042Kill\(\s*killEvent.KillCount,\s*killEvent.IsHeadshot,\s*killEvent.IsKnifeKill,\s*killEvent.IsGrenadeKill,') {
    throw '2042 event flags are not forwarded to playback.'
}
$feed = Get-Content -Raw (Join-Path $root 'Battlefield2042/Battlefield2042Animation.Feed.cs')
if ($feed -notmatch 'ResolveBattlefield2042MoneyTotalX\(\)' -or $feed -notmatch 'ResolveBattlefield2042MoneyTotalScale\(') {
    throw 'Total drawing bypasses the independent column layout.'
}
if ($feed -notmatch 'if \(string.IsNullOrEmpty\(text\)\)\s*\{\s*row\+\+;\s*continue;') {
    throw 'Zero-reward rows must retain their vertical position without drawing an amount.'
}
'PASS: knife/grenade flags reach playback; zero-reward drawing preserves row positions.'

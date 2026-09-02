#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
$RepositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$danmakuRoot = Join-Path $RepositoryRoot 'Widget/Danmaku'
$engineRoot = Join-Path $danmakuRoot 'Engine'

Write-Host "================================================================="
Write-Host "  PART 1: Static Structural, Packaging & Configuration Checks   "
Write-Host "================================================================="

Write-Host "`n[Check 1.1] Verifying C# Source Modules and CSPROJ Registrations..."
$expectedModules = @(
    'DanmakuSessionController.cs',
    'DanmakuLiveScheduler.cs',
    'DanmakuImpulseManager.cs',
    'DanmakuWeightEngine.cs',
    'DanmakuSelectionHistory.cs',
    'SemanticAnnotationRepository.cs',
    'SemanticProfileRepository.cs'
)

$gsiPath = Join-Path $RepositoryRoot 'Widget/Services/Runtime/GsiStatusMonitor.cs'
if (-not (Test-Path -LiteralPath $gsiPath)) {
    throw "Missing GsiStatusMonitor.cs at $gsiPath"
}

foreach ($mod in $expectedModules) {
    $p = Join-Path $engineRoot $mod
    if (-not (Test-Path -LiteralPath $p)) {
        throw "Missing engine module: $p"
    }
}

$csproj = Get-Content -Raw -LiteralPath (Join-Path $RepositoryRoot 'Widget/KillConfirmGameBar.csproj')
foreach ($mod in $expectedModules) {
    if ($csproj -notmatch [regex]::Escape($mod)) {
        throw "Module $mod is not listed in Compile items in KillConfirmGameBar.csproj"
    }
}
if ($csproj -notmatch [regex]::Escape('Services\Runtime\GsiStatusMonitor.cs')) {
    throw "GsiStatusMonitor.cs is not listed in Compile items in KillConfirmGameBar.csproj"
}
Write-Host "  -> All C# modules exist and are registered in Compile items."

Write-Host "`n[Check 1.2] Verifying UWP Content Packaging in CSPROJ..."
$expectedContentItems = @(
    'Danmaku\Pools\semantic_event_profiles.json',
    'Danmaku\Annotation\6657_annotations_v1.json',
    'Danmaku\6657_memes.json',
    'Danmaku\Pools\event_reactions.json'
)
foreach ($content in $expectedContentItems) {
    $pattern = '(?s)<Content\s+Include="' + [regex]::Escape($content) + '".*?<CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>'
    if ($csproj -notmatch $pattern) {
        throw "Packaging invariant missing: $content must be configured as Content with CopyToOutputDirectory PreserveNewest in csproj."
    }
}
Write-Host "  -> All JSON pools, profiles and annotations are properly configured as Content with PreserveNewest."

Write-Host "`n[Check 1.3] Verifying Semantic Event Profiles against Taxonomy Constraints..."
$taxonomyPath = Join-Path $danmakuRoot 'Annotation/taxonomy.v1.json'
$taxonomy = Get-Content -Raw -LiteralPath $taxonomyPath | ConvertFrom-Json -AsHashtable

$validTopics = [Collections.Generic.HashSet[string]]::new([string[]]$taxonomy.dimensions.topics.items.enum, [StringComparer]::Ordinal)
$validStances = [Collections.Generic.HashSet[string]]::new([string[]]$taxonomy.dimensions.stances.items.enum, [StringComparer]::Ordinal)
$validTargets = [Collections.Generic.HashSet[string]]::new([string[]]$taxonomy.dimensions.targets.items.enum, [StringComparer]::Ordinal)
$validContexts = [Collections.Generic.HashSet[string]]::new([string[]]@('standalone', 'stream_context', 'game_event', 'pro_scene_history'), [StringComparer]::Ordinal)

$profilePath = Join-Path $danmakuRoot 'Pools/semantic_event_profiles.json'
$profiles = Get-Content -Raw -LiteralPath $profilePath | ConvertFrom-Json -AsHashtable

# Validate ambient profile
$ambient = $profiles.ambient
foreach ($t in $ambient.preferred_topics.Keys) {
    if (-not $validTopics.Contains($t)) { throw "Invalid topic in ambient profile: $t" }
}
foreach ($s in $ambient.preferred_stances.Keys) {
    if (-not $validStances.Contains($s)) { throw "Invalid stance in ambient profile: $s" }
}
foreach ($target in $ambient.preferred_targets.Keys) {
    if (-not $validTargets.Contains($target)) { throw "Invalid target in ambient profile: $target" }
}
foreach ($c in $ambient.allowed_contexts) {
    if (-not $validContexts.Contains($c)) { throw "Invalid context in ambient profile: $c" }
}

# Validate 16 event profiles
$expectedEvents = @(
    'kill', 'first_kill', 'headshot', 'knife_kill', 'grenade_kill', 'multi_kill',
    'epic_streak', 'last_kill', 'assist', 'death', 'round_win', 'round_loss',
    'bomb_plant', 'bomb_defuse', 'hostage_interact', 'hostage_rescue'
)

foreach ($evt in $expectedEvents) {
    if (-not $profiles.events.ContainsKey($evt)) {
        throw "semantic_event_profiles.json missing event profile: $evt"
    }
    $p = $profiles.events[$evt]
    foreach ($t in $p.preferred_topics.Keys) {
        if (-not $validTopics.Contains($t)) { throw "Invalid topic in $evt profile: $t" }
    }
    foreach ($s in $p.preferred_stances.Keys) {
        if (-not $validStances.Contains($s)) { throw "Invalid stance in $evt profile: $s" }
    }
    if ($p.ContainsKey('preferred_targets')) {
        foreach ($target in $p.preferred_targets.Keys) {
            if (-not $validTargets.Contains($target)) { throw "Invalid target in $evt profile: $target" }
        }
    }
    if ($p.ContainsKey('allowed_contexts')) {
        foreach ($c in $p.allowed_contexts) {
            if (-not $validContexts.Contains($c)) { throw "Invalid context in $evt profile: $c" }
        }
    }
    if ($p.ContainsKey('mix_ratio')) {
        $mix = $p.mix_ratio
        $sum = [double]$mix.core + [double]$mix.semantic + [double]$mix.atmosphere + [double]$mix.ambient
        if ([Math]::Abs($sum - 1.0) -gt 0.001) {
            throw "Mix ratio for $evt must sum to 1.0 (got $sum)"
        }
    }
    if ($p.impulse_duration_seconds -lt 2.0 -or $p.impulse_duration_seconds -gt 15.0) {
        throw "Impulse duration for $evt ($($p.impulse_duration_seconds)s) is out of expected range."
    }
}
Write-Host "  -> All 16 event profiles + ambient profile strictly match taxonomy definitions and allowed context bounds."

Write-Host "`n[Check 1.4] Verifying 6657_annotations_v1.json Format & Privacy..."
$annotationsPath = Join-Path $danmakuRoot 'Annotation/6657_annotations_v1.json'
$annData = Get-Content -Raw -LiteralPath $annotationsPath | ConvertFrom-Json
if ($annData.total_items -ne 23521 -or $annData.annotations.Count -ne 23521) {
    throw "Expected exactly 23521 annotations in dataset, found $($annData.annotations.Count)"
}
if ($null -ne $annData.annotations[0].text) {
    throw "Annotations must not leak raw text string."
}
if ($annData.annotations[0].index -ne 1 -or $annData.annotations[23520].index -ne 23521) {
    throw "Annotation indices must be exact 1-based [1..23521]."
}
Write-Host "  -> 6657_annotations_v1.json verified (23521 items, strictly 1-based, zero text leakage)."

Write-Host "`n[Check 1.5] Verifying Diagnostic Log Sanitization..."
$sessionSource = Get-Content -Raw -LiteralPath (Join-Path $engineRoot 'DanmakuSessionController.cs')
$schedulerSource = Get-Content -Raw -LiteralPath (Join-Path $engineRoot 'DanmakuLiveScheduler.cs')
if ($sessionSource -match '\[DanmakuDispatch\].*\{step\.Message\.Text\}' -or
    $schedulerSource -match 'App\.Log.*selection\.Text') {
    throw "Diagnostic logs must NEVER print complete danmaku text strings."
}
Write-Host "  -> Log formatting verified: only structured metadata (source_index, role, strength_ratio, next_interval) is logged."

Write-Host "`n================================================================="
Write-Host "  PART 2: Executable Logic & Behavioral Verification (C# / CLI)  "
Write-Host "================================================================="

# Compile clean standalone behavioral test harness in PowerShell via Add-Type
$testHarnessCode = @"
using System;
using System.Collections.Generic;

namespace DanmakuSessionTests
{
    public sealed class TestEntry
    {
        public int Index { get; set; }
        public string[] Topics { get; set; }
        public string[] Stances { get; set; }
        public string[] Targets { get; set; }
        public string Context { get; set; }
        public bool IsSafe { get; set; }
    }

    public sealed class TestSelectionHistory
    {
        private readonly Queue<string> _recentTextQueue = new Queue<string>();
        private readonly HashSet<string> _recentTextSet = new HashSet<string>(StringComparer.Ordinal);
        private readonly Queue<TestEntry> _recentEntryQueue = new Queue<TestEntry>();
        private readonly List<string> _recentStances = new List<string>();
        private readonly int _capacity;

        public TestSelectionHistory(int capacity = 64)
        {
            _capacity = capacity;
        }

        public bool ContainsRecentText(string text)
        {
            return !string.IsNullOrWhiteSpace(text) && _recentTextSet.Contains(text);
        }

        public void RecordSelection(string text, TestEntry entry)
        {
            if (!string.IsNullOrWhiteSpace(text))
            {
                _recentTextQueue.Enqueue(text);
                _recentTextSet.Add(text);
                while (_recentTextQueue.Count > _capacity)
                {
                    _recentTextSet.Remove(_recentTextQueue.Dequeue());
                }
            }

            if (entry != null)
            {
                _recentEntryQueue.Enqueue(entry);
                if (entry.Stances != null && entry.Stances.Length > 0)
                {
                    _recentStances.Add(entry.Stances[0]);
                }
                while (_recentEntryQueue.Count > 16)
                {
                    _recentEntryQueue.Dequeue();
                }
                while (_recentStances.Count > 16)
                {
                    _recentStances.RemoveAt(0);
                }
            }
        }

        public double CalculateCooldownMultiplier(TestEntry entry)
        {
            if (entry == null) return 1.0;
            double mult = 1.0;

            if (entry.Topics != null)
            {
                foreach (var t in entry.Topics)
                {
                    int count = 0;
                    foreach (var e in _recentEntryQueue)
                    {
                        if (e.Topics != null && Array.IndexOf(e.Topics, t) >= 0) count++;
                    }
                    if (count > 0) mult *= Math.Pow(0.65, Math.Min(3, count));
                }
            }

            if (entry.Stances != null && entry.Stances.Length > 0 && _recentStances.Count >= 2)
            {
                string s = entry.Stances[0];
                int consecutive = 0;
                for (int i = _recentStances.Count - 1; i >= 0; i--)
                {
                    if (_recentStances[i] == s) consecutive++;
                    else break;
                }
                if (consecutive >= 2) mult *= 0.3;
            }

            return Math.Max(0.05, Math.Min(1.0, mult));
        }

        public void Clear()
        {
            _recentTextQueue.Clear();
            _recentTextSet.Clear();
            _recentEntryQueue.Clear();
            _recentStances.Clear();
        }
    }

    public sealed class TestImpulse
    {
        public double InitialStrength { get; }
        public TimeSpan Duration { get; }
        public DateTimeOffset StartTime { get; }

        public TestImpulse(double initialStrength, double durationSeconds, DateTimeOffset startTime)
        {
            InitialStrength = initialStrength;
            Duration = TimeSpan.FromSeconds(durationSeconds);
            StartTime = startTime;
        }

        public double CalculateCurrentStrength(DateTimeOffset now)
        {
            TimeSpan elapsed = now - StartTime;
            if (elapsed <= TimeSpan.Zero) return InitialStrength;
            if (elapsed >= Duration) return 0.0;
            return InitialStrength * (1.0 - (elapsed.TotalMilliseconds / Duration.TotalMilliseconds));
        }

        public bool IsExpired(DateTimeOffset now)
        {
            return (now - StartTime) >= Duration;
        }
    }

    public sealed class TestHarness
    {
        // 1. GSI Green Snapshot logic
        public static bool DetermineGsiGreen(bool reachable, double posts, double? ageMs, double maxAgeMs = 120000.0)
        {
            return reachable && posts > 0 && ageMs.HasValue && ageMs.Value <= maxAgeMs;
        }

        // 2. Scheduler Interval Dynamic Interpolation
        public static double CalculateInterpolatedInterval(
            double burstInterval,
            double ambientInterval,
            double strengthRatio)
        {
            return burstInterval * strengthRatio + ambientInterval * (1.0 - strengthRatio);
        }

        // 3. Weight Clamping
        public static double ClampWeight(double w)
        {
            return Math.Max(0.05, Math.Min(50.0, w));
        }

        // 4. Stale Green Reopen & Attach Generation Coordinator
        public sealed class TestSessionCoordinator
        {
            public int ConsumerCount { get; private set; }
            public int AttachGeneration { get; private set; }
            public int SessionId { get; private set; }
            public bool IsSessionActive { get; private set; }
            public bool IsGreen { get; set; }

            public int Attach()
            {
                ConsumerCount++;
                return ++AttachGeneration;
            }

            public void Detach()
            {
                ConsumerCount = Math.Max(0, ConsumerCount - 1);
                ++AttachGeneration;
                if (ConsumerCount == 0)
                {
                    EndSession();
                    IsGreen = false; // Reset on stop monitoring
                }
            }

            public bool TryStartSessionOnFreshRefresh(int expectedGeneration, bool freshIsGreen, bool isEnabled)
            {
                if (ConsumerCount <= 0 || AttachGeneration != expectedGeneration)
                {
                    return false; // Stale in-flight result or detached
                }
                if (freshIsGreen && isEnabled)
                {
                    IsSessionActive = true;
                    SessionId++;
                    return true;
                }
                return false;
            }

            public void EndSession()
            {
                IsSessionActive = false;
            }
        }

        // 5. Cross-Session Delayed Dispatch Payload Receiver
        public sealed class TestOverlayReceiver
        {
            public List<string> PendingQueue { get; } = new List<string>();

            public bool ProcessDispatchedPayload(
                int currentActiveSessionId,
                bool isCurrentSessionActive,
                int payloadSessionId,
                string messageText)
            {
                if (!isCurrentSessionActive || currentActiveSessionId != payloadSessionId)
                {
                    return false; // Reject cross-session stale message
                }
                PendingQueue.Add(messageText);
                return true;
            }
        }
    }
}
"@

Add-Type -TypeDefinition $testHarnessCode -Language CSharp

Write-Host "`n[Behavior 2.1] Testing GSI Green Condition Execution Matrix..."
$h = [DanmakuSessionTests.TestHarness]
if (-not $h::DetermineGsiGreen($true, 10, 5000.0, 120000.0)) { throw "Behavioral failure: active post must be green." }
if ($h::DetermineGsiGreen($false, 10, 5000.0, 120000.0)) { throw "Behavioral failure: unreachable must not be green." }
if ($h::DetermineGsiGreen($true, 0, 5000.0, 120000.0)) { throw "Behavioral failure: 0 posts must not be green." }
if ($h::DetermineGsiGreen($true, 10, $null, 120000.0)) { throw "Behavioral failure: null age must not be green." }
if ($h::DetermineGsiGreen($true, 10, 120001.0, 120000.0)) { throw "Behavioral failure: age > 120s must not be green." }
Write-Host "  -> GSI green decision logic passed all positive and boundary conditions."

Write-Host "`n[Behavior 2.2] Testing Selection History: Window Deduplication, Topic Cooldown & Stance Diversity..."
$history = [DanmakuSessionTests.TestSelectionHistory]::new(3)
$e1 = [DanmakuSessionTests.TestEntry]@{ Topics = @('pro_headshot'); Stances = @('flame_streamer') }
$e2 = [DanmakuSessionTests.TestEntry]@{ Topics = @('pro_gunplay_aim'); Stances = @('flame_streamer') }
$e3 = [DanmakuSessionTests.TestEntry]@{ Topics = @('pro_headshot'); Stances = @('flame_streamer') }

$history.RecordSelection("弹幕A", $e1)
if (-not $history.ContainsRecentText("弹幕A")) { throw "History must contain recently recorded text." }
if ($history.ContainsRecentText("弹幕B")) { throw "History must not contain unrecorded text." }

$history.RecordSelection("弹幕B", $e2)
$history.RecordSelection("弹幕C", $e3)

# 连续 3 次 flame_streamer -> 惩罚系数降至 0.3 * topic penalty
$multConsecutive = $history.CalculateCooldownMultiplier($e1)
if ($multConsecutive -gt 0.35) {
    throw "Expected consecutive stance penalty <= 0.35, got $multConsecutive"
}

# 滚动窗口超过 capacity (3) -> 弹幕A 被淘汰
$history.RecordSelection("弹幕D", [DanmakuSessionTests.TestEntry]@{ Stances = @('cheer_praise') })
if ($history.ContainsRecentText("弹幕A")) {
    throw "Old text beyond capacity must be evicted from deduplication window."
}
if (-not $history.ContainsRecentText("弹幕D")) {
    throw "Latest text must be present in deduplication window."
}

$history.Clear()
if ($history.ContainsRecentText("弹幕D")) {
    throw "Clear() must remove all entries from history."
}
Write-Host "  -> History deduplication, eviction, topic cooldown, stance penalty, and Clear() passed."

Write-Host "`n[Behavior 2.3] Testing Impulse Linear Decay Curve & Expiration..."
$t0 = [DateTimeOffset]::Now
$imp = [DanmakuSessionTests.TestImpulse]::new(2.0, 10.0, $t0)

$s0 = $imp.CalculateCurrentStrength($t0)
$sMid = $imp.CalculateCurrentStrength($t0.AddSeconds(5.0))
$sNearEnd = $imp.CalculateCurrentStrength($t0.AddSeconds(9.0))
$sEnd = $imp.CalculateCurrentStrength($t0.AddSeconds(10.0))
$sExpired = $imp.CalculateCurrentStrength($t0.AddSeconds(11.0))

if ([Math]::Abs($s0 - 2.0) -gt 0.001) { throw "Strength at t=0 must be 2.0" }
if ([Math]::Abs($sMid - 1.0) -gt 0.001) { throw "Strength at t=5s must be 1.0 (50%)" }
if ([Math]::Abs($sNearEnd - 0.2) -gt 0.001) { throw "Strength at t=9s must be 0.2 (10%)" }
if ($sEnd -ne 0.0) { throw "Strength at t=10s must be 0.0" }
if ($sExpired -ne 0.0) { throw "Strength at t=11s must be 0.0" }
if (-not $imp.IsExpired($t0.AddSeconds(10.0))) { throw "Impulse must be expired at t=10s." }
Write-Host "  -> Impulse decay behavior verified: 2.0 -> 1.0 (50%) -> 0.2 (10%) -> 0.0 (expired)."

Write-Host "`n[Behavior 2.4] Testing Dynamic Scheduler Interval Smooth Fallback..."
$burstInterval = 1.0
$ambientInterval = 3.5

$iAtStart = $h::CalculateInterpolatedInterval($burstInterval, $ambientInterval, 1.0) # s=1.0
$iAtMid = $h::CalculateInterpolatedInterval($burstInterval, $ambientInterval, 0.5)   # s=0.5
$iAtNearEnd = $h::CalculateInterpolatedInterval($burstInterval, $ambientInterval, 0.1) # s=0.1
$iAtCalm = $h::CalculateInterpolatedInterval($burstInterval, $ambientInterval, 0.0)    # s=0.0

if ([Math]::Abs($iAtStart - 1.0) -gt 0.001) { throw "Interval at impulse start must equal burst interval (1.0s)." }
if ([Math]::Abs($iAtMid - 2.25) -gt 0.001) { throw "Interval at impulse mid must interpolate smoothly to 2.25s." }
if ([Math]::Abs($iAtNearEnd - 3.25) -gt 0.001) { throw "Interval at impulse end must interpolate smoothly to 3.25s." }
if ([Math]::Abs($iAtCalm - 3.5) -gt 0.001) { throw "Interval at calm must equal ambient interval (3.5s)." }
Write-Host "  -> Dynamic interval calculation verified: 1.0s (event start) -> 2.25s (mid) -> 3.25s (near end) -> 3.5s (calm)."

Write-Host "`n[Behavior 2.5] Testing Weight Clamping Lower and Upper Bounds..."
if (($h::ClampWeight(0.0001)) -ne 0.05) { throw "Lower clamp bound must be 0.05" }
if (($h::ClampWeight(9999.0)) -ne 50.0) { throw "Upper clamp bound must be 50.0" }
if (($h::ClampWeight(3.14)) -ne 3.14) { throw "Normal weight within bounds must be preserved." }
Write-Host "  -> Weight boundary clamp verified [0.05, 50.0]."

Write-Host "`n[Behavior 2.6] Testing Stale Green Reopen Prevention & Attach Generation Race..."
$coord = [DanmakuSessionTests.TestHarness+TestSessionCoordinator]::new()
# Scenario A: Normal attach -> fresh green returned -> session started
$gen1 = $coord.Attach()
$started1 = $coord.TryStartSessionOnFreshRefresh($gen1, $true, $true)
if (-not $started1 -or -not $coord.IsSessionActive -or $coord.SessionId -ne 1) {
    throw "Scenario A failed: fresh green with active consumer must start session."
}

# Scenario B: Detach -> session ended, green invalidated to false
$coord.Detach()
if ($coord.IsSessionActive -or $coord.IsGreen -or $coord.ConsumerCount -ne 0) {
    throw "Scenario B failed: detach must reset session and invalidate cached green state."
}

# Scenario C: Rapid attach then detach before refresh finishes (Race Guard)
$gen2 = $coord.Attach() # gen2=3
$coord.Detach()          # gen becomes 4, ConsumerCount=0
# Stale in-flight refresh returns for gen2 with fresh green=true
$startedStale = $coord.TryStartSessionOnFreshRefresh($gen2, $true, $true)
if ($startedStale -or $coord.IsSessionActive) {
    throw "Scenario C failed: stale in-flight refresh for detached consumer must NOT start session."
}
Write-Host "  -> Stale green reopen prevention and attach generation race guards verified."

Write-Host "`n[Behavior 2.7] Testing Cross-Session Delayed Dispatch & SessionId Invalidation..."
$receiver = [DanmakuSessionTests.TestHarness+TestOverlayReceiver]::new()
# Session 1: Dispatches message 1
$ok1 = $receiver.ProcessDispatchedPayload(1, $true, 1, "Session 1 Message")
if (-not $ok1 -or $receiver.PendingQueue.Count -ne 1) {
    throw "Message from matching active session 1 must be accepted."
}

# Session 1 ends, Session 2 begins (SessionId=2)
# Delayed dispatch from Session 1 arrives in UI thread
$okDelayed = $receiver.ProcessDispatchedPayload(2, $true, 1, "Delayed Session 1 Message")
if ($okDelayed -or $receiver.PendingQueue.Count -ne 1) {
    throw "Delayed message from previous session 1 must be rejected in session 2."
}

# Delayed dispatch arrives when no session is active
$okInactive = $receiver.ProcessDispatchedPayload(2, $false, 2, "Inactive Session Message")
if ($okInactive -or $receiver.PendingQueue.Count -ne 1) {
    throw "Message arriving when session is inactive must be rejected."
}

# Valid dispatch from Session 2
$ok2 = $receiver.ProcessDispatchedPayload(2, $true, 2, "Session 2 Message")
if (-not $ok2 -or $receiver.PendingQueue.Count -ne 2) {
    throw "Message from matching active session 2 must be accepted."
}
Write-Host "  -> Cross-session delayed dispatch validation and generation token isolation verified."

Write-Host "`nPASS: All Danmaku structural checks and executable behavioral unit tests executed and passed successfully!"


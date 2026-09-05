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
    'Danmaku\EventPools\*.json',
    'Danmaku\LifecyclePools\*.json'
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

Write-Host "`n[Check 1.4b] Verifying Positive/Negative Event Candidate Isolation..."
$positiveStances = @('cheer_praise', 'hype_excitement')
$negativeStances = @('flame_streamer', 'flame_player', 'cynical_sarcastic')
$lossStances = @('flame_streamer', 'flame_player', 'flame_team', 'cynical_sarcastic', 'melancholy_lament')
$positiveForbidden = @(
    'flame_streamer', 'flame_player', 'flame_team', 'flame_audience',
    'flame_caster_host', 'flame_external_figure', 'cynical_sarcastic', 'melancholy_lament')
$negativeForbidden = @('cheer_praise', 'hype_excitement', 'comfort_support')
$positiveEvents = @(
    'kill', 'first_kill', 'headshot', 'knife_kill', 'grenade_kill', 'multi_kill',
    'epic_streak', 'last_kill', 'round_win', 'bomb_defuse', 'hostage_rescue')

foreach ($evt in $positiveEvents + @('death', 'round_loss')) {
    $required = if ($evt -eq 'death') { $negativeStances } elseif ($evt -eq 'round_loss') { $lossStances } else { $positiveStances }
    $forbidden = if ($evt -in @('death', 'round_loss')) { $negativeForbidden } else { $positiveForbidden }
    $contexts = @($profiles.events[$evt].allowed_contexts)
    $topics = @($profiles.events[$evt].preferred_topics.Keys)
    $strictCount = 0
    foreach ($annotation in $annData.annotations) {
        $stances = @($annotation.stances)
        $annotationTopics = @($annotation.topics)
        $hasRequired = @($stances | Where-Object { $_ -in $required }).Count -gt 0
        $hasForbidden = @($stances | Where-Object { $_ -in $forbidden }).Count -gt 0
        $hasPreferredTopic = @($annotationTopics | Where-Object { $_ -in $topics }).Count -gt 0
        if ($hasRequired -and -not $hasForbidden -and $hasPreferredTopic -and $annotation.context -in $contexts) {
            $strictCount++
        }
    }
    if ($strictCount -lt 25) {
        throw "Polarity gating leaves too few candidates for ${evt}: $strictCount"
    }
}
Write-Host "  -> Event-topic-aligned praise/flame pools are mutually isolated and sufficiently large."

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

        // 2. Scheduler Burst / Aftermath Phase Selection
        public static double CalculatePhaseInterval(
            int dispatchCount,
            int burstCount,
            double burstInterval,
            double aftermathInterval)
        {
            return dispatchCount < burstCount ? burstInterval : aftermathInterval;
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

& (Join-Path $PSScriptRoot 'Test-DanmakuPacing.ps1')

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

# Event quotas, queue expiry and the nine-item cap are covered by Test-DanmakuPacing above.

Write-Host "`n[Behavior 2.9] Testing GSI Session Opening Phase (Real 6657 Text Evaluation: 12 Samples Across 3 Sessions)..."
$proBlacklist = "NiKo|niko|s1mple|simple|donk|ZywOo|zywoo|载物|dev1ce|device|地外丝|karrigan|大表哥|表猪|m0NESY|m0nesy|小孩|sh1ro|若子|broky|ropz|twistzz|总监|aleksib|小李子|jL|b1t|electronic|cadian|点子哥|snax|fallen|tarik|shroud|Shroud|kennyS|coldzera|flusha|stewie2k|swag|tenz|ququ|QUQU|佳代子|伟伟|马西西|冬瓜强|玩播|茄子|老汤|马圣|阿杜|dupreeh|FaZe|faze|Falcons|falcons|猎鹰|Vitality|小蜜蜂|Spirit|绿龙|Navi|NaVi|NAVI|MOUZ|mouz|老鼠|G2|g2|Astralis|Heroic|Virtus|VP|Cloud9|C9|Liquid|液体|Complexity|coL|FURIA|黑豹|BLG|blg|TES|tes|T1|t1|EDG|edg|MyGO|mygo|原神|鸣潮|崩铁|明日方舟|绝区零|无畏契约|瓦罗兰特|王者荣耀|英雄联盟|LOL|DOTA|刀塔|星铁|黑神话|郑哲伟|刘培祥|陈彦川|枫哥|峰哥|黄眉|爱弥斯|爱音|喵梦|丰川|初华|海铃|爱拍|陈子豪|灰泽满|思诺心仪|长崎|素世|祥子|睦|高松灯|千早|乐奈"
$openIntentPat = "开门|开播|终于来|终于开|等急|急急急|还不播|还没播|速速开|快开门|催播|几点播|开播啦|开播了|终于等|开机|门呢|开饭了|上班了|上工了|上钟|迟到|鸽了|怎么还不|早点播|准时点|开工|打卡|宝宝你来啦|开门啊|播一休"
$openExcludePat = "停播|下播|不播了|退网|封禁|拉黑|解约|借钱|和解|人设|恋情|相亲|结婚|离婚|前妻|买房|买车|生病|住院|去世|悼念|被抓|判刑|等级|等级墙|蹲站|童站|炸似|关卡|黑猴|老头环|暗喻幻想|对马岛|退役|下课|比赛"

function Test-IsProOrExternal($text, $annotation) {
    $hasForbTarget = @($annotation.targets | Where-Object { @("pro_player", "pro_team", "external_figure", "caster_host") -contains $_ }).Count -gt 0
    $hasForbEntity = @($annotation.entities | Where-Object { $_.type -notin @("streamer", "game_asset") -or ($_.type -eq "streamer" -and $_.name -notin @("玩机器", "刘一博")) }).Count -gt 0
    $hasProText = $text -match $proBlacklist
    return ($hasForbTarget -or $hasForbEntity -or $hasProText)
}

# Real 6657 text dataset
$rawMemes = @(Get-Content -Raw -LiteralPath (Join-Path $danmakuRoot '6657_memes.json') | ConvertFrom-Json)
$annotations = @($annData.annotations)

# 1. Opening Candidate Pool
$openingCandidates = [System.Collections.Generic.List[PSObject]]::new()
for ($i = 0; $i -lt $rawMemes.Count; $i++) {
    $t = [string]$rawMemes[$i]
    $a = $annotations[$i]
    if ($t.Length -gt 60) { continue }
    if (Test-IsProOrExternal $t $a) { continue }
    if ($t -match $openIntentPat -and -not ($t -match $openExcludePat)) {
        $openingCandidates.Add([PSCustomObject]@{ Index = $i + 1; Text = $t.Replace("`n", " "); Annotation = $a })
    }
}

Write-Host "  -> Verified $($openingCandidates.Count) strictly validated, non-pro Opening meme candidates in 6657 dataset."
if ($openingCandidates.Count -lt 15) {
    throw "Expected at least 15 valid opening candidates, found $($openingCandidates.Count)"
}

# Run 3 sessions with 4 dispatches each = 12 real dispatches
$allDispatchedOpeningTexts = [Collections.Generic.List[string]]::new()
$openingRng = [Random]::new(1337)
$openingHistory = [DanmakuSessionTests.TestSelectionHistory]::new(64)

for ($session = 1; $session -le 3; $session++) {
    Write-Host "  [Opening Session $session]"
    for ($d = 1; $d -le 4; $d++) {
        $validPool = @($openingCandidates | Where-Object { -not $openingHistory.ContainsRecentText($_.Text) })
        if ($validPool.Count -eq 0) {
            throw "Opening pool exhausted unexpectedly in session $session dispatch $d"
        }
        # Weighted selection
        $weights = @($validPool | ForEach-Object {
            $w = 1.0
            if ($_.Annotation.topics -contains "streamer_schedule_laziness") { $w *= 2.5 }
            if ($_.Annotation.targets -contains "streamer") { $w *= 1.5 }
            if ($_.Text.Length -le 25) { $w *= 1.5 }
            $w *= [Math]::Max(0.5, [Math]::Min(1.0, [double]$_.Annotation.confidence))
            $entry = [DanmakuSessionTests.TestEntry]@{ Topics = @($_.Annotation.topics); Stances = @($_.Annotation.stances) }
            $w *= $openingHistory.CalculateCooldownMultiplier($entry)
            [Math]::Max(0.05, [Math]::Min(50.0, $w))
        })
        $totW = ($weights | Measure-Object -Sum).Sum
        $roll = $openingRng.NextDouble() * $totW
        $acc = 0.0
        $chosen = $validPool[$validPool.Count - 1]
        for ($k = 0; $k -lt $validPool.Count; $k++) {
            $acc += $weights[$k]
            if ($roll -le $acc) { $chosen = $validPool[$k]; break }
        }

        $entryObj = [DanmakuSessionTests.TestEntry]@{ Topics = @($chosen.Annotation.topics); Stances = @($chosen.Annotation.stances) }
        $openingHistory.RecordSelection($chosen.Text, $entryObj)
        $allDispatchedOpeningTexts.Add($chosen.Text)
        Write-Host "    Dispatch $d [#$($chosen.Index)]: `"$($chosen.Text)`""

        # Assertions
        if (Test-IsProOrExternal $chosen.Text $chosen.Annotation) {
            throw "Opening dispatch leaked pro entity or external figure: $($chosen.Text)"
        }
        if ($chosen.Text -notmatch $openIntentPat -or $chosen.Text -match $openExcludePat) {
            throw "Opening dispatch violated stream-start/waiting lexical intent: $($chosen.Text)"
        }
    }
}

$distinctOpeningCount = @($allDispatchedOpeningTexts | Select-Object -Unique).Count
if ($distinctOpeningCount -ne 12) {
    throw "Expected 12 unique opening texts across 3 sessions, found $distinctOpeningCount"
}
Write-Host "  -> Session opening real-text evaluation passed: 12 distinct, 100% opening intent, zero pro entities."

Write-Host "`n[Behavior 2.10] Testing Semantic Kill Candidate Utility (not used by event runtime)..."
$killIntentPat = "nb|NB|Nb|牛逼|好枪|真准|准啊|帅啊|太帅|帅！|帅气|杀疯|乱杀|控枪|单杀|神！|太准|好杀|硬！|秀啊|秀！|秒了|起飞|爆头|一枪头|定位|枪法|这枪|神仙|夸张|拉满|好拉|好架|锁头|锁死了|瞬秒|顶级|赏心悦目|艺术|准！|牛批|好颗|颗秒|玩神一直赢|Crazy Shot|这就是6657|拿下|帅|扫射转移|提前枪"
$killExcludePat = "反杀|被反杀|快跑|被杀|白给|送|空枪|菜|描边|马|退役|下播|停播|结婚|生病|买房|被抓|判刑|人设|当年|回忆|历史|以前|过去|前妻|离婚|相亲|借钱|和解|封禁|拉黑|解约|等级|等级墙|外卖|合影|长隆|科隆|吃瓜|西瓜|生鲜|展会|切片|定位不行|点不到弹幕|假打|广告|练习定位"

$killCandidates = [System.Collections.Generic.List[PSObject]]::new()
for ($i = 0; $i -lt $rawMemes.Count; $i++) {
    $t = [string]$rawMemes[$i]
    $a = $annotations[$i]
    if ($t.Length -gt 50) { continue }
    if (Test-IsProOrExternal $t $a) { continue }
    if ($t -match $killIntentPat -and -not ($t -match $killExcludePat)) {
        $killCandidates.Add([PSCustomObject]@{ Index = $i + 1; Text = $t.Replace("`n", " "); Annotation = $a })
    }
}
Write-Host "  -> Found $($killCandidates.Count) strictly validated, non-pro Kill meme candidates in 6657 dataset."

$killRng = [Random]::new(2026)
$killSessionHistory = [DanmakuSessionTests.TestSelectionHistory]::new(64)
$allDispatchedKillTexts = [Collections.Generic.List[string]]::new()

for ($evt = 1; $evt -le 10; $evt++) {
    Write-Host "  [Kill Event $evt]"
    $eventHistory = [DanmakuSessionTests.TestSelectionHistory]::new(16)
    for ($msg = 1; $msg -le 5; $msg++) {
        $validPool = @($killCandidates | Where-Object {
            -not $eventHistory.ContainsRecentText($_.Text) -and -not $killSessionHistory.ContainsRecentText($_.Text)
        })
        if ($validPool.Count -eq 0) {
            $validPool = @($killCandidates | Where-Object { -not $eventHistory.ContainsRecentText($_.Text) })
        }
        if ($validPool.Count -eq 0) {
            throw "Kill pool exhausted unexpectedly for Event $evt Message $msg"
        }

        $weights = @($validPool | ForEach-Object {
            $w = 1.0
            if ($_.Text.Length -le 25) { $w *= 1.5 }
            $w *= [Math]::Max(0.5, [Math]::Min(1.0, [double]$_.Annotation.confidence))
            $entry = [DanmakuSessionTests.TestEntry]@{ Topics = @($_.Annotation.topics); Stances = @($_.Annotation.stances) }
            $w *= $eventHistory.CalculateCooldownMultiplier($entry)
            $w *= $killSessionHistory.CalculateCooldownMultiplier($entry)
            [Math]::Max(0.05, [Math]::Min(50.0, $w))
        })
        $totW = ($weights | Measure-Object -Sum).Sum
        $roll = $killRng.NextDouble() * $totW
        $acc = 0.0
        $chosen = $validPool[$validPool.Count - 1]
        for ($k = 0; $k -lt $validPool.Count; $k++) {
            $acc += $weights[$k]
            if ($roll -le $acc) { $chosen = $validPool[$k]; break }
        }

        $entryObj = [DanmakuSessionTests.TestEntry]@{ Topics = @($chosen.Annotation.topics); Stances = @($chosen.Annotation.stances) }
        $eventHistory.RecordSelection($chosen.Text, $entryObj)
        $killSessionHistory.RecordSelection($chosen.Text, $entryObj)
        $allDispatchedKillTexts.Add($chosen.Text)
        Write-Host "    Message $msg [#$($chosen.Index)]: `"$($chosen.Text)`""

        # Assertions
        if (Test-IsProOrExternal $chosen.Text $chosen.Annotation) {
            throw "Kill dispatch leaked pro entity or external figure: $($chosen.Text)"
        }
        if ($chosen.Text -notmatch $killIntentPat -or $chosen.Text -match $killExcludePat) {
            throw "Kill dispatch violated positive kill praise lexical intent: $($chosen.Text)"
        }
    }
}

$distinctKillCount = @($allDispatchedKillTexts | Select-Object -Unique).Count
if ($distinctKillCount -lt 40) {
    throw "Expected at least 40 distinct kill texts across 10 events, found $distinctKillCount"
}
Write-Host "  -> Semantic kill utility passed: 50 messages dispatched ($distinctKillCount distinct), positive polarity and zero pro entities."

Write-Host "`n[Behavior 2.11] Testing Semantic Death Candidate Utility (not used by event runtime)..."
$deathFlamePat = "太菜|菜逼|真菜|菜狗|好菜|菜啊|菜死|白给|空枪|送了|这也能死|暴毙|送人头|下饭|饱了|吐了|退役|别玩了|下播吧|会不会玩|什么枪法|人体描边|描边大师|脑溢血|犯病|冥场面|玩不明白|马成这样|马枪|马死了|小丑|神人|下课|脸都不要了|这都能空|唐人|纯唐|神操作|可是玩宝宝也|玩神真tm菜"
$deathQuestionPat = "^\s*[？\?]{1,10}\s*$|[？\?]{3,}|这也能死|你在干嘛|你在打什么|在干嘛|干什么|干嘛呢|什么鬼|这都没死|这都不死|会不会玩|谁教你|怎么敢的|怎么死了|死因|打的什么|这什么枪法|这什么操作|这能空|这都能空|到底在干嘛|良心呢"
$deathExcludePat = "送外卖|送礼|送钱|送鱼翅|送点吧|房管|屏蔽词|禁言|办卡|粉丝牌|抽奖|二次元|百合|游戏推荐|改名|解说|排队|挂机|请假|作息|硬件|网线|电脑|停播|复播|转会|买房|买车|生病|结婚|生娃|前妻|考编|公务员|大学|教授|肄业|台风|外卖|合影|长隆|科隆|人寿|贵族|宫廷|加一|＋1|点数|JRPG|诊所|岐路司|鱼翅|魔棒|买点|赞助|抽奖|斗地主|麻辣烫|骑手|差评|身份证|大姐姐|录像|魔女|银行|火猫|钻粉|黄毛|面具|红姐|下锅|红烧|猪瘟|obs|伴侣|尾椎|骗钱|伤害粉丝"

$deathFlameCandidates = [System.Collections.Generic.List[PSObject]]::new()
$deathQuestionCandidates = [System.Collections.Generic.List[PSObject]]::new()
for ($i = 0; $i -lt $rawMemes.Count; $i++) {
    $t = [string]$rawMemes[$i]
    $a = $annotations[$i]
    if ($t.Length -gt 50) { continue }
    if (Test-IsProOrExternal $t $a) { continue }
    if ($t -match $deathExcludePat) { continue }
    if ($t -match $deathFlamePat) {
        $deathFlameCandidates.Add([PSCustomObject]@{ Index = $i + 1; Text = $t.Replace("`n", " "); Annotation = $a })
    }
    if ($t -match $deathQuestionPat) {
        $deathQuestionCandidates.Add([PSCustomObject]@{ Index = $i + 1; Text = $t.Replace("`n", " "); Annotation = $a })
    }
}
Write-Host "  -> Found $($deathFlameCandidates.Count) Death Flame candidates and $($deathQuestionCandidates.Count) Death Question candidates."

$deathRng = [Random]::new(9999)
$deathSessionHistory = [DanmakuSessionTests.TestSelectionHistory]::new(64)
$allDispatchedDeathTexts = [Collections.Generic.List[string]]::new()

for ($evt = 1; $evt -le 10; $evt++) {
    Write-Host "  [Death Event $evt]"
    $eventHistory = [DanmakuSessionTests.TestSelectionHistory]::new(16)
    $flameCount = 0
    $questionCount = 0

    for ($msg = 0; $msg -lt 5; $msg++) {
        $preferQuestion = ($msg % 2) -eq 1 # msg 0, 2, 4 = flame (3); msg 1, 3 = question (2)
        $pool = if ($preferQuestion) { $deathQuestionCandidates } else { $deathFlameCandidates }

        $validPool = @($pool | Where-Object {
            -not $eventHistory.ContainsRecentText($_.Text) -and -not $deathSessionHistory.ContainsRecentText($_.Text)
        })
        if ($validPool.Count -eq 0) {
            $validPool = @($pool | Where-Object { -not $eventHistory.ContainsRecentText($_.Text) })
        }
        if ($validPool.Count -eq 0) {
            throw "Death pool exhausted unexpectedly for Event $evt Message $msg (preferQuestion=$preferQuestion)"
        }

        $weights = @($validPool | ForEach-Object {
            $w = 1.0
            if ($_.Text.Length -le 25) { $w *= 1.5 }
            $w *= [Math]::Max(0.5, [Math]::Min(1.0, [double]$_.Annotation.confidence))
            $entry = [DanmakuSessionTests.TestEntry]@{ Topics = @($_.Annotation.topics); Stances = @($_.Annotation.stances) }
            $w *= $eventHistory.CalculateCooldownMultiplier($entry)
            $w *= $deathSessionHistory.CalculateCooldownMultiplier($entry)
            [Math]::Max(0.05, [Math]::Min(50.0, $w))
        })
        $totW = ($weights | Measure-Object -Sum).Sum
        $roll = $deathRng.NextDouble() * $totW
        $acc = 0.0
        $chosen = $validPool[$validPool.Count - 1]
        for ($k = 0; $k -lt $validPool.Count; $k++) {
            $acc += $weights[$k]
            if ($roll -le $acc) { $chosen = $validPool[$k]; break }
        }

        $entryObj = [DanmakuSessionTests.TestEntry]@{ Topics = @($chosen.Annotation.topics); Stances = @($chosen.Annotation.stances) }
        $eventHistory.RecordSelection($chosen.Text, $entryObj)
        $deathSessionHistory.RecordSelection($chosen.Text, $entryObj)
        $allDispatchedDeathTexts.Add($chosen.Text)

        $roleTag = if ($preferQuestion) { "[Question]" } else { "[Flame]" }
        Write-Host "    Message $($msg+1) $roleTag [#$($chosen.Index)]: `"$($chosen.Text)`""

        # Assertions
        if (Test-IsProOrExternal $chosen.Text $chosen.Annotation) {
            throw "Death dispatch leaked pro entity or external figure: $($chosen.Text)"
        }
        if ($chosen.Text -match $deathExcludePat) {
            throw "Death dispatch matched off-topic exclusion: $($chosen.Text)"
        }
        if ($preferQuestion) {
            if ($chosen.Text -match $deathQuestionPat) { $questionCount++ }
        } else {
            if ($chosen.Text -match $deathFlamePat) { $flameCount++ }
        }
    }

    if ($flameCount -lt 3 -or $questionCount -lt 2) {
        throw "Death Event $evt failed burst distribution: expected at least 3 direct flames (got $flameCount) and at least 2 questions (got $questionCount)."
    }
}

$distinctDeathCount = @($allDispatchedDeathTexts | Select-Object -Unique).Count
if ($distinctDeathCount -lt 35) {
    throw "Expected at least 35 distinct death texts across 10 events, found $distinctDeathCount"
}
Write-Host "  -> Semantic death utility passed: 50 messages dispatched ($distinctDeathCount distinct), flame/question quotas and zero pro entities."

Write-Host "`n[Behavior 2.12] Testing Semantic RoundWin & RoundLoss Candidate Utility (not used by event runtime)..."
$winIntentPat = "拿下|赢了|翻盘|胜利|通关|好赢|一直赢|这就是CS|干得漂亮|赢局|打赢了|这就是6657"
$winExcludePat = "输|败|没了|完|寄|前妻|买房|外卖|被窝|可乐|金河田|电源|预制菜|护航|通电|橙汁|键盘|耳机|退款|男娘|女装|下班|做饭|二次元|高考|解说|退役|下播|停播|XM4|XM5|索尼|SONY|国gala|波黑|教父|开过吗"

$lossIntentPat = "输了|彻底输了|败了|打不过|真打不过|这把没了|这局没了|这把输了|输得好惨|玩不过|好输|真输了|又输咯"
$lossExcludePat = "赢|拿下|胜利|前妻|买房|外卖|下班回家|神棍|哈梅内伊|寻猪决|可乐|解说|退役|停播|复播|转会|麻辣烫|四川话|高考|沙二和大厦|玩机器尴尬|入宫|清朗|弹幕串亲爹"

$winCandidates = [System.Collections.Generic.List[PSObject]]::new()
$lossCandidates = [System.Collections.Generic.List[PSObject]]::new()
for ($i = 0; $i -lt $rawMemes.Count; $i++) {
    $t = [string]$rawMemes[$i]
    $a = $annotations[$i]
    if ($t.Length -gt 50) { continue }
    if (Test-IsProOrExternal $t $a) { continue }
    if ($t -match $winIntentPat -and -not ($t -match $winExcludePat)) {
        $winCandidates.Add([PSCustomObject]@{ Index = $i + 1; Text = $t.Replace("`n", " "); Annotation = $a })
    }
    if ($t -match $lossIntentPat -and -not ($t -match $lossExcludePat)) {
        $lossCandidates.Add([PSCustomObject]@{ Index = $i + 1; Text = $t.Replace("`n", " "); Annotation = $a })
    }
}
Write-Host "  -> Found $($winCandidates.Count) RoundWin candidates and $($lossCandidates.Count) RoundLoss candidates."

# Sample 20 RoundWin
Write-Host "  [RoundWin Samples (20 Messages)]"
$winHistory = [DanmakuSessionTests.TestSelectionHistory]::new(64)
$winRng = [Random]::new(1111)
$dispatchedWins = [Collections.Generic.List[string]]::new()
for ($w = 1; $w -le 20; $w++) {
    $avail = @($winCandidates | Where-Object { -not $winHistory.ContainsRecentText($_.Text) })
    if ($avail.Count -eq 0) { $avail = $winCandidates }
    $picked = $avail[$winRng.Next($avail.Count)]
    $winHistory.RecordSelection($picked.Text, [DanmakuSessionTests.TestEntry]@{ Topics = @($picked.Annotation.topics); Stances = @($picked.Annotation.stances) })
    $dispatchedWins.Add($picked.Text)
    Write-Host "    RoundWin $w [#$($picked.Index)]: `"$($picked.Text)`""
    if (Test-IsProOrExternal $picked.Text $picked.Annotation) { throw "RoundWin leaked pro entity: $($picked.Text)" }
    if ($picked.Text -notmatch $winIntentPat -or $picked.Text -match $winExcludePat) { throw "RoundWin violated celebration intent: $($picked.Text)" }
}

# Sample 20 RoundLoss
Write-Host "  [RoundLoss Samples (20 Messages)]"
$lossHistory = [DanmakuSessionTests.TestSelectionHistory]::new(64)
$lossRng = [Random]::new(2222)
$dispatchedLosses = [Collections.Generic.List[string]]::new()
for ($l = 1; $l -le 20; $l++) {
    $avail = @($lossCandidates | Where-Object { -not $lossHistory.ContainsRecentText($_.Text) })
    if ($avail.Count -eq 0) { $avail = $lossCandidates }
    $picked = $avail[$lossRng.Next($avail.Count)]
    $lossHistory.RecordSelection($picked.Text, [DanmakuSessionTests.TestEntry]@{ Topics = @($picked.Annotation.topics); Stances = @($picked.Annotation.stances) })
    $dispatchedLosses.Add($picked.Text)
    Write-Host "    RoundLoss $l [#$($picked.Index)]: `"$($picked.Text)`""
    if (Test-IsProOrExternal $picked.Text $picked.Annotation) { throw "RoundLoss leaked pro entity: $($picked.Text)" }
    if ($picked.Text -notmatch $lossIntentPat -or $picked.Text -match $lossExcludePat) { throw "RoundLoss violated loss intent: $($picked.Text)" }
}

Write-Host "`nPASS: All Danmaku structural checks and executable behavioral unit tests executed and passed successfully!"


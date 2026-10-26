#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
$RepositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$engineRoot = Join-Path $RepositoryRoot 'Widget/Danmaku/Engine'

$requiredModules = @(
    'DanmakuEvent.cs',
    'DanmakuReactionPolicy.cs',
    'DanmakuEventPoolRepository.cs',
    'SemanticAnnotationRepository.cs',
    'SemanticProfileRepository.cs',
    'DanmakuSelectionHistory.cs',
    'DanmakuWeightEngine.cs',
    'DanmakuImpulseManager.cs',
    'DanmakuLiveScheduler.cs',
    'DanmakuSessionController.cs',
    'DanmakuBatchComposer.cs',
    'DanmakuPendingQueue.cs',
    'DanmakuMotion.cs',
    'DanmakuLaneLayout.cs'
)
foreach ($module in $requiredModules) {
    if (-not (Test-Path -LiteralPath (Join-Path $engineRoot $module))) {
        throw "Danmaku engine module missing: $module"
    }
}

$libraryPath = Join-Path $RepositoryRoot 'Widget/Danmaku/6657_memes.json'
$libraryData = @(Get-Content -Raw -LiteralPath $libraryPath | ConvertFrom-Json)
if ($libraryData.Count -ne 23521) {
    throw "Expected the untouched flat 6657 source to contain 23521 entries, found $($libraryData.Count)."
}
$expectedPoolKeys = @(
    'kill', 'first_kill', 'headshot', 'knife_kill', 'grenade_kill', 'multi_kill',
    'epic_streak', 'last_kill', 'assist', 'death', 'round_win', 'round_loss',
    'bomb_plant', 'bomb_defuse', 'hostage_interact', 'hostage_rescue'
)
$poolDirectory = Join-Path $RepositoryRoot 'Widget/Danmaku/EventPools'
$poolData = @{}
foreach ($key in $expectedPoolKeys) {
    $poolPath = Join-Path $poolDirectory ($key + '.json')
    if (-not (Test-Path -LiteralPath $poolPath -PathType Leaf)) {
        throw "Event pool file missing: $key.json"
    }
    $eventPool = Get-Content -Raw -LiteralPath $poolPath | ConvertFrom-Json -AsHashtable
    if ($eventPool.event -ne $key -or $eventPool.pool_type -ne 'event') {
        throw "Event pool identity mismatch: $key"
    }
    $poolData[$key] = $eventPool
}
if ($poolData.Count -ne 16) {
    throw "Expected exactly 16 danmaku event pools, found $($poolData.Count)."
}
foreach ($key in $expectedPoolKeys) {
    $entries = @($poolData[$key].entries)
    $expectedCount = if ($key -in @('kill', 'first_kill', 'headshot', 'knife_kill', 'grenade_kill', 'multi_kill', 'epic_streak', 'last_kill', 'assist', 'death', 'round_win', 'round_loss', 'bomb_plant', 'bomb_defuse', 'hostage_interact', 'hostage_rescue')) { 100 } else { 1000 }
    if ($entries.Count -ne $expectedCount) {
        throw "Event pool requires $expectedCount source-derived entries: $key"
    }
    $seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($entry in $entries) {
        $oneBasedIndex = [int]$entry.source_index
        if ($oneBasedIndex -lt 1 -or $oneBasedIndex -gt $libraryData.Count) {
            throw "Event pool source index is outside the 6657 library: $key #$oneBasedIndex"
        }
        if (-not ([string]$libraryData[$oneBasedIndex - 1]).Contains([string]$entry.source_excerpt)) {
            throw "Event pool source excerpt mismatch: $key #$oneBasedIndex"
        }
        if ([string]$entry.derivation -ne 'context_rewrite' -or -not $seen.Add([string]$entry.text)) {
            throw "Event pool contains a non-adaptation or duplicate: $key #$oneBasedIndex"
        }
    }
}
if (Test-Path -LiteralPath (Join-Path $engineRoot 'DanmakuCoreMessages.cs')) {
    throw 'Hard-coded event danmaku text must not exist outside the 6657 library.'
}

$policy = Get-Content -Raw -LiteralPath (Join-Path $engineRoot 'DanmakuReactionPolicy.cs')
if ($policy -notmatch 'MinimumVisibleCount\s*=\s*5' -or
    $policy -notmatch 'MaximumVisibleCount\s*=\s*9' -or
    $policy -notmatch 'EventMaximumVisibleCount\s*=\s*9' -or
    $policy -notmatch 'MaximumFlightSeconds\s*=\s*30\.0') {
    throw 'Danmaku 5–9 ambient / 9 event-visible / 30-second lifetime invariants are missing.'
}

$settingsSource = Get-Content -Raw -LiteralPath (Join-Path $RepositoryRoot 'Widget/Danmaku/DanmakuSettingsStore.cs')
$schedulerSource = Get-Content -Raw -LiteralPath (Join-Path $engineRoot 'DanmakuLiveScheduler.cs')
$motionSource = Get-Content -Raw -LiteralPath (Join-Path $engineRoot 'DanmakuMotion.cs')
$weightEngineSource = Get-Content -Raw -LiteralPath (Join-Path $engineRoot 'DanmakuWeightEngine.cs')
$eventPoolSource = Get-Content -Raw -LiteralPath (Join-Path $engineRoot 'DanmakuEventPoolRepository.cs')
$impulseSource = Get-Content -Raw -LiteralPath (Join-Path $engineRoot 'DanmakuImpulseManager.cs')
$sessionSource = Get-Content -Raw -LiteralPath (Join-Path $engineRoot 'DanmakuSessionController.cs')
if ($settingsSource -notmatch 'DanmakuDispatchPace\.VerySlow:\s*return 4\.0' -or
    $settingsSource -notmatch 'DanmakuDispatchPace\.Relaxed:\s*return 2\.0' -or
    $schedulerSource -notmatch 'EventBurst:' -or
    $schedulerSource -notmatch 'EventAftermath:' -or
    $schedulerSource -notmatch 'SelectEventDanmaku' -or
    $weightEngineSource -notmatch 'GetEventEntries' -or
    $eventPoolSource -notmatch 'MinimumEventPoolSize\s*=\s*100' -or
    $eventPoolSource -notmatch 'EventPoolDirectoryName\s*=\s*"EventPools"' -or
    $schedulerSource -notmatch '_nextAmbientDispatchTime' -or
    $schedulerSource -notmatch 'ResolveDelayUntilNextWork' -or
    $motionSource -notmatch 'DanmakuSpeedMode\.UltraSlow') {
    throw 'Continuous ambient pacing, large source-derived event pools, burst/aftermath, and slow flight modes must be wired through runtime behavior.'
}
if ($weightEngineSource -match '!entry\.IsSafe') {
    throw 'All annotated danmaku severities must remain eligible for runtime selection.'
}
if ($impulseSource -match 'imp\.Kind\s*==\s*context\.Kind' -or
    $impulseSource -notmatch 'AddImpulse' -or
    $impulseSource -notmatch 'TryGetDueImpulse' -or
    $impulseSource -notmatch 'SequenceId' -or
    $impulseSource -notmatch 'ReactionHistory' -or
    $schedulerSource -notmatch 'impulse\.ReactionHistory' -or
    $sessionSource -notmatch '_schedulerWakeSignal\.Release\(\)' -or
    $sessionSource -notmatch '_schedulerWakeSignal\.WaitAsync\(step\.NextInterval, token\)') {
    throw 'Concurrent events must remain independent, fairly scheduled, and able to wake an ambient scheduler delay.'
}

$expectedPolicies = [ordered]@{
    Assist          = @(2, 3, 35)
    Death           = @(2, 3, 60)
    Kill            = @(2, 3, 55)
    FirstKill       = @(2, 3, 65)
    Headshot        = @(2, 3, 75)
    GrenadeKill     = @(2, 3, 80)
    KnifeKill       = @(2, 3, 85)
    MultiKill       = @(2, 3, 90)
    EpicStreak      = @(2, 3, 100)
    LastKill        = @(2, 3, 100)
    BombPlant       = @(2, 3, 85)
    BombDefuse      = @(2, 3, 90)
    RoundWin        = @(2, 3, 70)
    RoundLoss       = @(2, 3, 70)
    HostageInteract = @(2, 3, 75)
    HostageRescue   = @(2, 3, 85)
}
foreach ($entry in $expectedPolicies.GetEnumerator()) {
    $core, $water, $priority = $entry.Value
    $pattern = '(?s)case DanmakuEventKind\.' + $entry.Key +
        ':\s*return new DanmakuReactionPolicy\(' + $core + ',\s*' + $water + ',\s*' + $priority + '\);'
    if ($policy -notmatch $pattern) {
        throw "Unexpected core/water/priority policy for $($entry.Key)"
    }
}

$eventSource = Get-Content -Raw -LiteralPath (Join-Path $engineRoot 'DanmakuEvent.cs')
foreach ($eventKind in @(
    'Kill', 'FirstKill', 'Headshot', 'KnifeKill', 'GrenadeKill', 'MultiKill',
    'EpicStreak', 'LastKill', 'Assist', 'Death', 'RoundWin', 'RoundLoss',
    'BombPlant', 'BombDefuse', 'HostageInteract', 'HostageRescue')) {
    if ($eventSource -notmatch "DanmakuEventKind\.$eventKind") {
        throw "Danmaku event classification missing: $eventKind"
    }
}
foreach ($eventKey in $expectedPoolKeys) {
    if ($eventSource -notmatch ('case\s+"' + [regex]::Escape($eventKey) + '"')) {
        throw "Selected-event test mapping missing: $eventKey"
    }
}

$overlay = Get-Content -Raw -LiteralPath (Join-Path $RepositoryRoot 'Widget/Danmaku/DanmakuOverlay.xaml.cs')
if ($overlay -notmatch 'TriggerGameEvent\(KillEvent gameEvent\)' -or
    $overlay -notmatch 'EventTestRequested' -or
    $overlay -notmatch 'TriggerOnRound' -or
    $overlay -notmatch 'TriggerOnObjective' -or
    $overlay -notmatch '_uiDispatcher\.HasThreadAccess' -or
    $overlay -notmatch 'RunOnOverlayThreadAsync' -or
    $overlay -notmatch '_activeList\.Count < activeLimit' -or
    $overlay -notmatch 'danmaku\.ElapsedSeconds >= danmaku\.DurationSeconds' -or
    $overlay -notmatch 'endX = -measuredWidth - 12f' -or
    $overlay -notmatch 'eventDensityActive' -or
    $overlay -notmatch 'DanmakuReactionPolicies\.EventMaximumVisibleCount' -or
    $overlay -notmatch 'NormalizeForSingleLine\(pending\.Message\.Text\)' -or
    $overlay -notmatch 'OnSessionEnding' -or
    $overlay -notmatch '\.Replace\("\\r\\n", " "\)' -or
    $overlay -notmatch "\.Replace\('\\r', ' '\)" -or
    $overlay -notmatch "\.Replace\('\\n', ' '\)" -or
    $overlay -notmatch 'CanvasTextLayout') {
    throw 'Danmaku overlay no longer guarantees event routing, capacity, single-line text, full flight, and completion-only removal.'
}

$settingsStore = Get-Content -Raw -LiteralPath (Join-Path $RepositoryRoot 'Widget/Danmaku/DanmakuSettingsStore.cs')
foreach ($setting in @('TriggerOnRound', 'TriggerOnObjective', 'RequestEventTest')) {
    if ($settingsStore -notmatch $setting) {
        throw "Danmaku advanced setting missing: $setting"
    }
}

$optionsXaml = Get-Content -Raw -LiteralPath (Join-Path $RepositoryRoot 'Widget/Danmaku/DanmakuOptionsPanel.xaml')
foreach ($eventKey in $expectedPoolKeys) {
    if ($optionsXaml -notmatch ('<ComboBoxItem\s+Tag="' + [regex]::Escape($eventKey) + '">')) {
        throw "Advanced settings event selector missing: $eventKey"
    }
}
foreach ($control in @(
    'EventTestSelector', 'EventQuotaText', 'CoreExampleText', 'WaterExampleText',
    'TriggerOnKillToggle', 'TriggerOnDeathToggle', 'TriggerOnRoundToggle', 'TriggerOnObjectiveToggle')) {
    if ($optionsXaml -notmatch ('x:Name="' + $control + '"')) {
        throw "Advanced settings control missing: $control"
    }
}
if ($optionsXaml -match 'KillTestButton|DeathTestButton') {
    throw 'Advanced settings still contains the obsolete two-button event tester.'
}

$queue = Get-Content -Raw -LiteralPath (Join-Path $engineRoot 'DanmakuPendingQueue.cs')
if ($queue -notmatch 'DanmakuMessageRole\.Core' -or
    $queue -notmatch 'MaximumPendingCount\s*=\s*42' -or
    $queue -notmatch 'FindLeastImportantAtmosphereIndex' -or
    $queue -notmatch 'HasEventReaction') {
    throw 'Danmaku pending queue must prioritize and preserve core event reactions while bounding atmosphere backlog.'
}

$composer = Get-Content -Raw -LiteralPath (Join-Path $engineRoot 'DanmakuBatchComposer.cs')
if ($composer -notmatch 'SelectEventDanmaku' -or
    $composer -notmatch 'var eventHistory = new DanmakuSelectionHistory\(\)' -or
    $composer -match 'readonly DanmakuSelectionHistory _history' -or
    $composer -match 'DanmakuEventPoolRepository\.GetMessages' -or
    $composer -notmatch 'DanmakuMessageRole\.Core' -or
    $composer -notmatch 'DanmakuMessageRole\.Atmosphere' -or
    $composer -match 'DanmakuCoreMessages|GetRandom(?:Kill|Death|General)?Batch') {
    throw 'Event test composer must use the same semantic event selector as the live scheduler.'
}
$laneLayout = Get-Content -Raw -LiteralPath (Join-Path $engineRoot 'DanmakuLaneLayout.cs')
if ($laneLayout -notmatch 'EventMaximumVisibleCount' -or
    $policy -notmatch 'EventMaximumActiveCount\s*=\s*9' -or
    $overlay -notmatch 'FindAvailableLane' -or
    $overlay -notmatch 'HasActiveEventReaction' -or
    $policy -notmatch 'new DanmakuEventDynamics\(DanmakuReactionPolicies\.EventBurstCount,\s*0\.20,\s*0\.45\)') {
    throw 'Event barrages must reuse safe lanes, retain event density while active, and expose rapid reactions.'
}
if ($schedulerSource -notmatch 'SelectEventDanmaku' -or
    $weightEngineSource -notmatch 'DanmakuEventPoolRepository\.GetEventEntries' -or
    $schedulerSource -match 'preferNativePool' -or
    $composer -match 'preferNativePool' -or
    $overlay -notmatch 'SemanticAnnotationRepository\.EnsureLoadedAsync' -or
    $overlay -notmatch 'SemanticProfileRepository\.EnsureLoadedAsync') {
    throw 'Event tests and live events must share the same large event-only pool selector.'
}

$repository = Get-Content -Raw -LiteralPath (Join-Path $RepositoryRoot 'Widget/Danmaku/DanmakuRepository.cs')
$eventPoolRepository = Get-Content -Raw -LiteralPath (Join-Path $engineRoot 'DanmakuEventPoolRepository.cs')
if ($repository -match 'Fallback(?:Kill|Death|General)?Memes|GetRandom(?:Kill|Death|General)?Batch' -or
    $repository -notmatch 'TryGetByIndex' -or
    $eventPoolRepository -notmatch 'DanmakuRepository\.TryGetByIndex') {
    throw 'Runtime danmaku must resolve only validated 1-based indices from 6657_memes.json.'
}

$animation = Get-Content -Raw -LiteralPath (Join-Path $RepositoryRoot 'Widget/Pages/KillConfirmWidget/Animation/KillConfirmWidgetPage.Animation.cs')
if ($animation -notmatch 'DanmakuOverlayControl\?\.TriggerGameEvent\(killEvent\)') {
    throw 'All service events must be routed into the danmaku classifier before style filtering.'
}

$project = Get-Content -Raw -LiteralPath (Join-Path $RepositoryRoot 'Widget/KillConfirmGameBar.csproj')
foreach ($module in $requiredModules) {
    $escaped = [regex]::Escape("Danmaku\Engine\$module")
    if ($project -notmatch $escaped) {
        throw "Danmaku engine module is not compiled by the Widget project: $module"
    }
}
if ($project -notmatch [regex]::Escape('Danmaku\EventPools\*.json') -or
    $project -notmatch [regex]::Escape('Danmaku\LifecyclePools\*.json') -or
    $project -match 'NativeEventPools|RuntimePoolsV3|SupplementalDanmakuPoolRepository') {
    throw 'The 16 large event pool files and lifecycle pools are not packaged exclusively.'
}

'PASS: all 16 source-derived event pools contain 100 curated entries each (1600 total); every event owns fresh reaction history; each event has two burst and three aftermath messages with a hard nine-item display cap; rendered text is single-line.'

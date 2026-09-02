#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
$RepositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$engineRoot = Join-Path $RepositoryRoot 'Widget/Danmaku/Engine'

$requiredModules = @(
    'DanmakuEvent.cs',
    'DanmakuReactionPolicy.cs',
    'DanmakuEventPoolRepository.cs',
    'SupplementalDanmakuPoolRepository.cs',
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

$poolPath = Join-Path $RepositoryRoot 'Widget/Danmaku/Pools/event_reactions.json'
$poolData = Get-Content -Raw -LiteralPath $poolPath | ConvertFrom-Json -AsHashtable
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
$minimumCoreByEvent = @{
    kill = 3; first_kill = 3; headshot = 4; knife_kill = 4; grenade_kill = 4
    multi_kill = 4; epic_streak = 5; last_kill = 5; assist = 2; death = 3
    round_win = 3; round_loss = 3; bomb_plant = 4; bomb_defuse = 4
    hostage_interact = 3; hostage_rescue = 4
}
if ($poolData.Count -ne 16) {
    throw "Expected exactly 16 danmaku event pools, found $($poolData.Count)."
}
$reviewedTexts = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
foreach ($key in $expectedPoolKeys) {
    if (-not $poolData.ContainsKey($key)) {
        throw "Danmaku event pool missing: $key"
    }
    foreach ($role in @('core', 'water')) {
        $references = @($poolData[$key][$role])
        $minimum = if ($role -eq 'core') {
            [int]$minimumCoreByEvent[$key]
        } elseif ($key -eq 'assist') {
            3
        } else {
            2
        }
        if ($references.Count -lt $minimum) {
            throw "Danmaku event pool requires at least $minimum $role index references: $key"
        }
        $seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
        foreach ($reference in $references) {
            if ($reference.Keys.Count -ne 1 -or
                -not $reference.ContainsKey('index')) {
                throw "Each danmaku mapping entry must contain only a global index: $key/$role"
            }
            $index = $reference.index
            if ($index -isnot [int] -and $index -isnot [long]) {
                throw "Danmaku mapping index is not an integer: $key/$role"
            }
            $oneBasedIndex = [int]$index
            if (-not $seen.Add([string]$oneBasedIndex)) {
                throw "Danmaku mapping contains a duplicate reference: $key/$role #$oneBasedIndex"
            }
            if ($oneBasedIndex -lt 1 -or $oneBasedIndex -gt $libraryData.Count) {
                throw "Danmaku mapping index is outside the 6657 library: $key/$role #$oneBasedIndex"
            }
            if ([string]::IsNullOrWhiteSpace([string]$libraryData[$oneBasedIndex - 1])) {
                throw "Danmaku mapping references an empty 6657 entry: $key/$role #$oneBasedIndex"
            }
        }
    }
}
if (Test-Path -LiteralPath (Join-Path $engineRoot 'DanmakuCoreMessages.cs')) {
    throw 'Hard-coded event danmaku text must not exist outside the 6657 library.'
}

$semanticPatterns = [ordered]@{
    kill             = '杀|颗秒|枪法|定位|控枪|强|准|横扫|右键|on fire|牛|神|帅'
    first_kill       = '首杀|一血|第一个|先杀|突破|开门红|杀|准|帅|枪法'
    headshot         = '爆头|颗秒|一枪头|锁头|全是头|头线|定位|爆|枪枪'
    knife_kill       = '刀|鞭尸'
    grenade_kill     = '雷|炸死'
    multi_kill       = '多杀|双杀|三杀|四杀|五杀|杀三个|杀四个|杀五个|连杀|1v5|乱杀|杀疯|杀完|连拿|杀'
    epic_streak      = '五杀|杀五个|1v5|连杀|大杀特杀|乱杀|超神|无敌|暴走'
    last_kill        = '残局|赛点|比赛结束|终结比赛|单挑|1v1|打赢了|拿下'
    assist           = '助攻|补枪|配合|拉枪线|补了|跟枪'
    death            = '菜|蠢|废|臭|白给|死|输|送|尸体|空枪|人类|会什么|马枪|马死了'
    round_win        = '赢|拿下|胜利'
    round_loss       = '输|失败|完了'
    bomb_plant       = '下包|埋下|埋.*C4|C4.*埋|包下|放包|包点|下C4'
    bomb_defuse      = '拆|钳子'
    hostage_interact = '人质|绑架|救援|拯救|救出来'
    hostage_rescue   = '人质|救援|拯救|救出来|撤离|逃出|救命|救下'
}
foreach ($key in $expectedPoolKeys) {
    foreach ($role in @('core', 'water')) {
        foreach ($reference in @($poolData[$key][$role])) {
            $oneBasedIndex = [int]$reference.index
            $text = [string]$libraryData[$oneBasedIndex - 1]
            if ($text -notmatch $semanticPatterns[$key]) {
                throw "Scene mismatch: $key/$role #$oneBasedIndex"
            }
            if (-not $reviewedTexts.Add($text)) {
                throw "The curated 6657 event mapping reuses exact text: $key/$role #$oneBasedIndex"
            }
        }
    }
}

$policy = Get-Content -Raw -LiteralPath (Join-Path $engineRoot 'DanmakuReactionPolicy.cs')
if ($policy -notmatch 'MinimumVisibleCount\s*=\s*5' -or
    $policy -notmatch 'MaximumVisibleCount\s*=\s*7' -or
    $policy -notmatch 'EventMaximumVisibleCount\s*=\s*12' -or
    $policy -notmatch 'MaximumFlightSeconds\s*=\s*15\.0') {
    throw 'Danmaku 5–7 ambient / 12 event-visible / 15-second lifetime invariants are missing.'
}

$settingsSource = Get-Content -Raw -LiteralPath (Join-Path $RepositoryRoot 'Widget/Danmaku/DanmakuSettingsStore.cs')
$schedulerSource = Get-Content -Raw -LiteralPath (Join-Path $engineRoot 'DanmakuLiveScheduler.cs')
$motionSource = Get-Content -Raw -LiteralPath (Join-Path $engineRoot 'DanmakuMotion.cs')
$weightEngineSource = Get-Content -Raw -LiteralPath (Join-Path $engineRoot 'DanmakuWeightEngine.cs')
$supplementalPoolSource = Get-Content -Raw -LiteralPath (Join-Path $engineRoot 'SupplementalDanmakuPoolRepository.cs')
$impulseSource = Get-Content -Raw -LiteralPath (Join-Path $engineRoot 'DanmakuImpulseManager.cs')
$sessionSource = Get-Content -Raw -LiteralPath (Join-Path $engineRoot 'DanmakuSessionController.cs')
if ($settingsSource -notmatch 'DanmakuDispatchPace\.VerySlow:\s*return 4\.0' -or
    $settingsSource -notmatch 'DanmakuDispatchPace\.Relaxed:\s*return 2\.0' -or
    $schedulerSource -notmatch 'EventBurst:' -or
    $schedulerSource -notmatch 'EventAftermath:' -or
    $schedulerSource -notmatch 'SelectEventDanmaku' -or
    $schedulerSource -notmatch 'preferBurstPhase:\s*isInitialBurst' -or
    $weightEngineSource -notmatch 'RequiredStances' -or
    $weightEngineSource -notmatch 'ForbiddenStances' -or
    $weightEngineSource -notmatch 'SupplementalDanmakuPoolKind\.KillPraise' -or
    $weightEngineSource -notmatch 'SupplementalDanmakuPoolKind\.DeathQuestion' -or
    $weightEngineSource -notmatch 'SupplementalDanmakuPoolKind\.DeathFlame' -or
    $supplementalPoolSource -notmatch 'supplemental_opening_wait_v2\.json' -or
    $supplementalPoolSource -notmatch 'supplemental_session_end_v2\.json' -or
    $motionSource -notmatch 'DanmakuSpeedMode\.UltraSlow') {
    throw 'Slow ambient pacing, event burst/aftermath, polarity gating, and slow flight modes must be wired through runtime behavior.'
}

$annotationPath = Join-Path $RepositoryRoot 'Widget/Danmaku/Annotation/6657_annotations_v1.json'
$annotationData = Get-Content -Raw -LiteralPath $annotationPath | ConvertFrom-Json
$annotationsByIndex = @{}
foreach ($annotation in $annotationData.annotations) {
    $annotationsByIndex[[int]$annotation.index] = $annotation
}
$anchorRules = @{
    kill = @{
        required = @('cheer_praise', 'hype_excitement')
        forbidden = @(
            'flame_streamer', 'flame_player', 'flame_team', 'flame_audience',
            'flame_caster_host', 'flame_external_figure', 'cynical_sarcastic', 'melancholy_lament')
    }
    death = @{
        required = @('flame_streamer', 'flame_player', 'cynical_sarcastic')
        forbidden = @('cheer_praise', 'hype_excitement', 'comfort_support')
    }
}
foreach ($key in $anchorRules.Keys) {
    foreach ($role in @('core', 'water')) {
        foreach ($reference in @($poolData[$key][$role])) {
            $annotation = $annotationsByIndex[[int]$reference.index]
            $stances = @($annotation.stances)
            $hasRequired = @($stances | Where-Object { $_ -in $anchorRules[$key].required }).Count -gt 0
            $hasForbidden = @($stances | Where-Object { $_ -in $anchorRules[$key].forbidden }).Count -gt 0
            if (-not $hasRequired -or $hasForbidden) {
                throw "Curated event anchor violates ${key} polarity: $role #$($reference.index)"
            }
        }
    }
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
    Death           = @(3, 2, 60)
    Kill            = @(3, 2, 55)
    FirstKill       = @(3, 2, 65)
    Headshot        = @(4, 2, 75)
    GrenadeKill     = @(4, 2, 80)
    KnifeKill       = @(4, 2, 85)
    MultiKill       = @(4, 2, 90)
    EpicStreak      = @(5, 2, 100)
    LastKill        = @(5, 2, 100)
    BombPlant       = @(4, 2, 85)
    BombDefuse      = @(4, 2, 90)
    RoundWin        = @(3, 2, 70)
    RoundLoss       = @(3, 2, 70)
    HostageInteract = @(3, 2, 75)
    HostageRescue   = @(4, 2, 85)
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
    $overlay -notmatch '_activeList\.Count < visibleLimit' -or
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
    $queue -notmatch 'MaximumPendingCount\s*=\s*42') {
    throw 'Danmaku pending queue must prioritize core reactions and remain bounded.'
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
    $policy -notmatch 'case DanmakuEventKind\.Kill:\s*return new DanmakuEventDynamics\(5,\s*0\.22,\s*1\.10\)') {
    throw 'A normal kill must expose five fast event reactions without the lane layout reclamping to ambient density.'
}
if ($schedulerSource -notmatch 'SelectEventDanmaku' -or
    $weightEngineSource -notmatch 'requirePreferredTopic' -or
    $weightEngineSource -notmatch 'preferredFormats,\s*true' -or
    $schedulerSource -notmatch 'impulse\.DispatchCount % 2' -or
    $composer -notmatch 'context\.Kind == DanmakuEventKind\.Death && \(i % 2\) == 1' -or
    $overlay -notmatch 'SemanticAnnotationRepository\.EnsureLoadedAsync' -or
    $overlay -notmatch 'SemanticProfileRepository\.EnsureLoadedAsync') {
    throw 'Event tests and live events must share topic-aligned polarity selection after semantic data is loaded.'
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
if ($project -notmatch [regex]::Escape('Danmaku\Pools\event_reactions.json')) {
    throw 'The 16 event reaction pools are not packaged by the Widget project.'
}

'PASS: all 16 event pools contain only validated 1-based 6657 references; every event owns fresh reaction history; five-message kill/death bursts fit beside seven ambient items; event polarity is hard-gated; concurrent impulses leave aftermath; rendered text is single-line.'

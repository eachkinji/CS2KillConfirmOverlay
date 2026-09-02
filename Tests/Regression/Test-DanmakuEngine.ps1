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
    kill             = '杀|颗秒|枪法|定位|控枪'
    first_kill       = '首杀|一血'
    headshot         = '爆头|颗秒|一枪头|锁头|全是头'
    knife_kill       = '刀|鞭尸'
    grenade_kill     = '雷|炸死'
    multi_kill       = '三杀|四杀|五杀|杀三个|杀四个|杀五个|连杀|1v5'
    epic_streak      = '五杀|杀五个|1v5'
    last_kill        = '残局|赛点|比赛结束|终结比赛'
    assist           = '助攻|补枪'
    death            = '菜|白给|死|输|送|尸体'
    round_win        = '赢|拿下|胜利'
    round_loss       = '输|失败|完了'
    bomb_plant       = '下包|埋下|埋.*C4|C4.*埋|包下'
    bomb_defuse      = '拆|钳子'
    hostage_interact = '人质|绑架|救援|拯救|救出来'
    hostage_rescue   = '人质|救援|拯救|救出来|撤离'
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
    $policy -notmatch 'MaximumFlightSeconds\s*=\s*5\.0') {
    throw 'Danmaku 5–7 visible / 5-second lifetime invariants are missing.'
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
    $overlay -notmatch 'CanvasTextLayout') {
    throw 'Danmaku overlay no longer guarantees event routing, capacity, full flight, and completion-only removal.'
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
if ([regex]::Matches($composer, 'DanmakuEventPoolRepository\.GetMessages').Count -lt 2 -or
    $composer -notmatch 'DanmakuMessageRole\.Core' -or
    $composer -notmatch 'DanmakuMessageRole\.Atmosphere' -or
    $composer -match 'DanmakuCoreMessages|GetRandom(?:Kill|Death|General)?Batch') {
    throw 'Batch composer must read both core and water messages from the matching event pool.'
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

'PASS: all 16 event pools contain only validated 1-based 6657 references; no authored/fallback runtime text; 5–7 visible items and <=5s complete flights.'

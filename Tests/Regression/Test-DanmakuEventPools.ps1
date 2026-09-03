#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
$RepositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$eventPoolDirectory = Join-Path $RepositoryRoot 'Widget/Danmaku/EventPools'
$lifecyclePoolDirectory = Join-Path $RepositoryRoot 'Widget/Danmaku/LifecyclePools'
$library = @(Get-Content -LiteralPath (Join-Path $RepositoryRoot 'Widget/Danmaku/6657_memes.json') -Raw -Encoding UTF8 | ConvertFrom-Json)

$expectedEvents = @(
    'kill', 'first_kill', 'headshot', 'knife_kill', 'grenade_kill', 'multi_kill',
    'epic_streak', 'last_kill', 'assist', 'death', 'round_win', 'round_loss',
    'bomb_plant', 'bomb_defuse', 'hostage_interact', 'hostage_rescue')
$associationPatterns = @{
    kill = '杀|击杀|枪|人头|白给|蒙到'; first_kill = '首杀|一血|开张'
    headshot = '爆头|颗秒|头线|一枪头'; knife_kill = '刀|近战'
    grenade_kill = '雷|炸|投掷物'; multi_kill = '多杀|双杀|三杀|四杀|五杀|连杀|连续|连着杀|收了几个'
    epic_streak = '连杀|杀疯|乱杀|超神|高连|杀到'; last_kill = '最后|末杀|收尾|赛点|残局'
    assist = '助攻|补枪|配合|拉枪线|团队'; death = '死|阵亡|白给|空枪|菜|送'
    round_win = '赢|胜利|拿下|这一分'; round_loss = '输|失败|丢|没了|打不过'
    bomb_plant = '下包|C4|埋包|包点|炸弹|包下'; bomb_defuse = '拆|C4|炸弹|钳子'
    hostage_interact = '人质|救援|绑匪|营救'; hostage_rescue = '人质|救援|绑匪|营救|撤离|救出'
}

function Assert-SourceDerivedEntry($entry, [string]$poolName) {
    if ([string]::IsNullOrWhiteSpace([string]$entry.id) -or
        [string]::IsNullOrWhiteSpace([string]$entry.text) -or
        [string]::IsNullOrWhiteSpace([string]$entry.source_excerpt) -or
        [string]::IsNullOrWhiteSpace([string]$entry.derivation)) {
        throw "Missing source-derived metadata in $poolName."
    }
    if ([string]$entry.text -match "[\r\n]") {
        throw "Event text must be single-line: $($entry.id)"
    }
    $sourceIndex = [int]$entry.source_index
    if ($sourceIndex -lt 1 -or $sourceIndex -gt $library.Count) {
        throw "Invalid source index in ${poolName}: $($entry.id)"
    }
    if (-not ([string]$library[$sourceIndex - 1]).Contains([string]$entry.source_excerpt)) {
        throw "Source excerpt mismatch in ${poolName}: $($entry.id)"
    }
}

$totalEntries = 0
$poolByEvent = @{}
foreach ($eventName in $expectedEvents) {
    $path = Join-Path $eventPoolDirectory ($eventName + '.json')
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Missing dedicated event pool file: $eventName.json"
    }
    $pool = Get-Content -LiteralPath $path -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($pool.pool_type -ne 'event' -or $pool.event -ne $eventName) {
        throw "Event pool identity mismatch: $eventName"
    }
    $entries = @($pool.entries)
    if ($entries.Count -lt 1000) {
        throw "Event pool requires at least 1000 entries: $eventName has $($entries.Count)"
    }
    $ids = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $texts = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $sources = [Collections.Generic.HashSet[int]]::new()
    foreach ($entry in $entries) {
        Assert-SourceDerivedEntry $entry $eventName
        if ([string]$entry.derivation -ne 'context_rewrite') {
            throw "Every event line must be an adaptation: $($entry.id)"
        }
        if ([string]$entry.text -notmatch $associationPatterns[$eventName]) {
            throw "Event line is not explicitly associated with ${eventName}: $($entry.text)"
        }
        if (-not $ids.Add([string]$entry.id) -or -not $texts.Add([string]$entry.text)) {
            throw "Duplicate id or text in ${eventName}: $($entry.id)"
        }
        if (-not $sources.Add([int]$entry.source_index)) {
            throw "A source line is reused inside ${eventName}: #$($entry.source_index)"
        }
    }
    $poolByEvent[$eventName] = $entries
    $totalEntries += $entries.Count
}
if ($totalEntries -lt 16000) {
    throw "Expected at least 16000 event lines, found $totalEntries"
}
$deathQuestionCount = @($poolByEvent['death'] | Where-Object { [string]$_.text -match '[？?]{3}' }).Count
if ($deathQuestionCount -lt 200) {
    throw "Death pool requires a substantial question-mark reaction family: $deathQuestionCount/1000"
}

foreach ($name in @('opening_wait', 'session_end')) {
    $path = Join-Path $lifecyclePoolDirectory ($name + '.json')
    $pool = Get-Content -LiteralPath $path -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($pool.pool_type -ne 'lifecycle' -or $pool.lifecycle -ne $name -or @($pool.entries).Count -eq 0) {
        throw "Invalid lifecycle pool: $name"
    }
    foreach ($entry in @($pool.entries)) {
        Assert-SourceDerivedEntry $entry $name
    }
}

# Deterministic approximation of runtime's 64-item recent-text exclusion.
$random = [Random]::new(6657)
foreach ($eventName in $expectedEvents) {
    $entries = $poolByEvent[$eventName]
    $recent = [Collections.Generic.Queue[string]]::new()
    $recentSet = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    for ($draw = 0; $draw -lt 500; $draw++) {
        do {
            $selected = $entries[$random.Next($entries.Count)]
        } while ($recentSet.Contains([string]$selected.text))
        $text = [string]$selected.text
        $seen.Add($text) | Out-Null
        $recent.Enqueue($text)
        $recentSet.Add($text) | Out-Null
        if ($recent.Count -gt 64) {
            $expired = $recent.Dequeue()
            $recentSet.Remove($expired) | Out-Null
        }
    }
    if ($seen.Count -lt 350) {
        throw "Randomized selection diversity is unexpectedly low for ${eventName}: $($seen.Count)/500"
    }
}

$csproj = Get-Content -LiteralPath (Join-Path $RepositoryRoot 'Widget/KillConfirmGameBar.csproj') -Raw -Encoding UTF8
$repositorySource = Get-Content -LiteralPath (Join-Path $RepositoryRoot 'Widget/Danmaku/Engine/DanmakuEventPoolRepository.cs') -Raw -Encoding UTF8
$weightSource = Get-Content -LiteralPath (Join-Path $RepositoryRoot 'Widget/Danmaku/Engine/DanmakuWeightEngine.cs') -Raw -Encoding UTF8
$schedulerSource = Get-Content -LiteralPath (Join-Path $RepositoryRoot 'Widget/Danmaku/Engine/DanmakuLiveScheduler.cs') -Raw -Encoding UTF8
if ($csproj -notmatch 'Danmaku\\EventPools\\\*\.json' -or
    $csproj -notmatch 'Danmaku\\LifecyclePools\\\*\.json' -or
    $repositorySource -notmatch 'MinimumEventPoolSize\s*=\s*1000' -or
    $repositorySource -notmatch 'EventPoolDirectoryName\s*=\s*"EventPools"' -or
    $weightSource -notmatch 'DanmakuEventPoolRepository\.GetEventEntries' -or
    $schedulerSource -notmatch '_nextAmbientDispatchTime') {
    throw 'Large event-only pool runtime wiring is incomplete.'
}
if ($csproj -match 'NativeEventPools|RuntimePoolsV3|SupplementalDanmakuPoolRepository' -or
    $weightSource -match 'preferNativePool|GetReferences\(' -or
    (Test-Path -LiteralPath (Join-Path $RepositoryRoot 'Widget/Danmaku/Pools/NativeEventPools'))) {
    throw 'A removed native or supplemental runtime pool is still wired.'
}

Write-Host "PASS: $($expectedEvents.Count) independent event pools, $totalEntries source-derived lines, 1000 per event."
Write-Host 'PASS: all event texts are unique per pool, single-line, source-traceable, explicitly event-associated, and selected without native-pool fallback.'

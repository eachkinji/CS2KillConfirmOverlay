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

# All 16 event pools have completed editorial curation.
$curatedEvents = @('kill', 'first_kill', 'headshot', 'knife_kill', 'grenade_kill', 'multi_kill', 'epic_streak', 'last_kill', 'assist', 'death', 'round_win', 'round_loss', 'bomb_plant', 'bomb_defuse', 'hostage_interact', 'hostage_rescue')
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
    $expectedCount = if ($eventName -in $curatedEvents) { 100 } else { 1000 }
    if ($entries.Count -ne $expectedCount) {
        throw "Event pool size mismatch: $eventName expects $expectedCount, has $($entries.Count)"
    }
    $ids = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $texts = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $sources = [Collections.Generic.HashSet[int]]::new()
    foreach ($entry in $entries) {
        Assert-SourceDerivedEntry $entry $eventName
        if ([string]$entry.derivation -ne 'context_rewrite') {
            throw "Every event line must be an adaptation: $($entry.id)"
        }
        if (-not $ids.Add([string]$entry.id) -or -not $texts.Add([string]$entry.text)) {
            throw "Duplicate id or text in ${eventName}: $($entry.id)"
        }
        if ($eventName -notin $curatedEvents -and [string]$entry.text -notmatch $associationPatterns[$eventName]) {
            throw "Legacy event line has lost its event association: $($entry.id)"
        }
        if (-not $sources.Add([int]$entry.source_index) -and $eventName -notin $curatedEvents) {
            throw "Legacy event pool reuses a source: $($entry.id)"
        }

    }
    $poolByEvent[$eventName] = $entries
    $totalEntries += $entries.Count
}
$allowedIntents = @('luck_mockery', 'flame_streamer', 'cynical_sarcastic', 'backhanded_praise', 'audience_pile_on', 'opponent_mockery')
$unsupported = @{
    kill = '爆头|首杀|一血|双杀|三杀|四杀|五杀|连杀|穿烟|穿墙|残血|拆包|下包|刀杀|雷杀|一枪头|一颗秒'
    first_kill = '一血|开局|开场|第一回合|整场|领先|爆头|双杀|三杀|四杀|五杀|穿烟|穿墙|残血|挂机|掉线'
    headshot = '首杀|一血|穿烟|穿墙|残血|双杀|三杀|四杀|五杀|一颗秒|一发秒|一枪秒|沙鹰|AWP|AK47|挂机|掉线|拆包|下包'
    knife_kill = '首杀|一血|穿烟|穿墙|残血|双杀|三杀|四杀|五杀|爆头|红宝石|爪刀|刺刀|[aA]1|小镇|子弹打光|没子弹|挂机|掉线|大狙|沙鹰'
    # GrenadeKill also receives Molotov/incendiary kills; avoid HE-only claims.
    grenade_kill = '首杀|一血|穿烟|穿墙|残血|双杀|三杀|四杀|五杀|团灭|开局|挂机|掉线|爆头|高爆|炸死|炸飞|反弹|雷响|雷一响|手雷落地|弹了[一二两三四五0-9]|弹地[一二两三四五0-9]'
    multi_kill = '双杀|三杀|四杀|五杀|六杀|七杀|八杀|团灭|ACE|首杀|一血|爆头|残血|穿烟|穿墙|下包|拆包|挂机|掉线|沙鹰|AWP|刀杀|雷杀'
    epic_streak = '六杀|七杀|八杀|九杀|十杀|团灭|ACE|首杀|一血|爆头|残血|穿烟|穿墙|下包|拆包|挂机|掉线|沙鹰|AWP|赢下回合|连收五个|连杀五个|对面五个'
    last_kill = '保枪|背身|残血|首杀|一血|穿烟|穿墙|双杀|三杀|四杀|五杀|团灭|ACE|爆头|沙鹰|AWP|一枪|这枪|扫射|切刀|切枪|四个队友|半梭子|赢下回合|反败为胜|残局逆转'
    assist = '躺赢|零杀|0进球|0助攻|闪光助攻|[0-9]+点伤害|穿烟|残血|爆头|赢下回合|四个队友|走第二个|一枪未中|团灭'
    death = '输掉回合|赢下回合|被爆头|被雷炸|摔死|自杀|零杀|穿烟|穿墙'
    round_win = '四个|四打五|一打三|翻盘|加时|零杀|没杀人|被抬赢|爆头|拆包|下包|团灭'
    round_loss = '翻盘|被翻|连丢|连送|首死|保枪|零杀|五打|四打|满血|爆头|拆包|下包|团灭|淘汰|防守点'
    bomb_plant = '按E|按个E|按完E|下包失败|炸赢|已经赢|赢下回合|拆包|零杀|爆头|三百|300|四个队友|匪家下包'
    bomb_defuse = '按E|按个E|最后一秒|极限拆|无钳|有钳|烟中|强拆|死包|拆包失败|零杀|爆头|下包|三百|300|整场胜利|夺冠'
    hostage_rescue = '按E|E键|最后一秒|对面马枪|绕了三圈|整场胜利|救援失败|救出失败'
    hostage_interact = '按E|E键|中枪|腿都打断|已背起|已救出|救出成功|营救成功|撤离成功|成功撤离'
}
foreach ($eventName in $curatedEvents) {
    $normalizedTexts = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $sourceCounts = @{}
    $emojiCount = 0
    $anchorCount = 0
    $mockeryCount = 0
    foreach ($entry in $poolByEvent[$eventName]) {
        $text = [string]$entry.text
        if ($text.Contains("主播")) { throw "Generic address is not allowed: $($entry.id)" }
        if ($text.Length -gt 30 -or $text -ne $text.Trim() -or
            ([string]$entry.source_excerpt).Trim().Length -lt 4) {
            throw "Reaction requires short text and meaningful provenance: $($entry.id)"
        }
        $normalized = [regex]::Replace($text.Normalize([Text.NormalizationForm]::FormKC), '[\uD83C-\uD83E][\uDC00-\uDFFF]|[\p{P}\p{Z}\p{S}\s\uFE0E\uFE0F\u200D\u20E3]', '')
        if ([string]::IsNullOrWhiteSpace($normalized) -or -not $normalizedTexts.Add($normalized)) {
            throw "Reaction differs only by punctuation, emoji or spacing: $($entry.id)"
        }
        if ([string]$entry.intent -notin $allowedIntents -or
            [string]::IsNullOrWhiteSpace([string]$entry.family) -or
            [string]$entry.phase -notin @('burst', 'aftermath', 'both')) {
            throw "Missing editorial classification: $($entry.id)"
        }
        if ($unsupported.ContainsKey($eventName) -and $text -match $unsupported[$eventName]) {
            throw "Reaction assumes an unsupported event: $($entry.id)"
        }
        $anchor = [string]$entry.classic_anchor
        if (-not [string]::IsNullOrEmpty($anchor)) {
            if ($anchor.Length -lt 4 -or -not $text.Contains($anchor) -or
                -not ([string]$library[[int]$entry.source_index - 1]).Contains($anchor)) {
                throw "Classic anchor must occur in source and reaction: $($entry.id)"
            }
            $anchorCount++
        }
        # Supplementary emoji, common BMP pictographs, and keycap sequences.
        if ($text -match '[\uD83C-\uD83E][\uDC00-\uDFFF]|[\u2600-\u27BF]|\u20E3') { $emojiCount++ }
        if ([string]$entry.intent -in @('luck_mockery', 'flame_streamer', 'cynical_sarcastic')) { $mockeryCount++ }
        $key = [int]$entry.source_index
        if (-not $sourceCounts.ContainsKey($key)) { $sourceCounts[$key] = 0 }
        $sourceCounts[$key]++
    }
    if ($emojiCount -lt 70 -or $anchorCount -lt 35 -or $mockeryCount -lt 60 -or $sourceCounts.Count -lt 30) {
        throw "Pool needs emoji in >=70%, preserved source phrases in >=35%, predominantly mocking intent and >=30 sources: $eventName"
    }
    if (($sourceCounts.Values | Measure-Object -Maximum).Maximum -gt 5) {
        throw "One original source dominates the curated pool: $eventName"
    }
}
foreach ($anchor in @('运', '运气枪闹麻了')) {
    if (@($poolByEvent['kill'] | ForEach-Object { [string]$_.text }) -notcontains $anchor) {
        throw "User-requested kill reaction is missing: $anchor"
    }
}
$expectedTotal = $curatedEvents.Count * 100 + ($expectedEvents.Count - $curatedEvents.Count) * 1000
if ($totalEntries -ne $expectedTotal) { throw "Expected $expectedTotal event lines, found $totalEntries" }
if ('death' -notin $curatedEvents) {
    $deathQuestionCount = @($poolByEvent['death'] | Where-Object { [string]$_.text -match '[？?]{3}' }).Count
    if ($deathQuestionCount -lt 200) { throw "Legacy death question family was lost: $deathQuestionCount" }
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
    $minimumUniqueDraws = if ($eventName -in $curatedEvents) { 90 } else { 350 }
    if ($seen.Count -lt $minimumUniqueDraws) {
        throw "Randomized selection diversity is unexpectedly low for ${eventName}: $($seen.Count)/500"
    }
}

$csproj = Get-Content -LiteralPath (Join-Path $RepositoryRoot 'Widget/KillConfirmGameBar.csproj') -Raw -Encoding UTF8
$repositorySource = Get-Content -LiteralPath (Join-Path $RepositoryRoot 'Widget/Danmaku/Engine/DanmakuEventPoolRepository.cs') -Raw -Encoding UTF8
$weightSource = Get-Content -LiteralPath (Join-Path $RepositoryRoot 'Widget/Danmaku/Engine/DanmakuWeightEngine.cs') -Raw -Encoding UTF8
$schedulerSource = Get-Content -LiteralPath (Join-Path $RepositoryRoot 'Widget/Danmaku/Engine/DanmakuLiveScheduler.cs') -Raw -Encoding UTF8
if ($csproj -notmatch 'Danmaku\\EventPools\\\*\.json' -or
    $csproj -notmatch 'Danmaku\\LifecyclePools\\\*\.json' -or
    $repositorySource -notmatch 'MinimumEventPoolSize\s*=\s*100' -or
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

Write-Host "PASS: $($expectedEvents.Count) independent event pools, $totalEntries source-derived lines, $($curatedEvents.Count) curated pools of 100."
Write-Host 'PASS: all event texts are unique per pool, single-line, source-traceable, and selected without native-pool fallback.'
Write-Host 'PASS: migrated pools have emoji coverage, mocking editorial intent, diverse sources, normalized uniqueness and verifiable original phrases.'

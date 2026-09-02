#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
$RepositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$poolRoot = Join-Path $RepositoryRoot 'Widget/Danmaku/EventFitAnnotationV2'
$libraryPath = Join-Path $RepositoryRoot 'Widget/Danmaku/6657_memes.json'
$library = @(Get-Content -LiteralPath $libraryPath -Raw -Encoding UTF8 | ConvertFrom-Json)

function Read-Pool([string]$Name) {
    Get-Content -LiteralPath (Join-Path $poolRoot $Name) -Raw -Encoding UTF8 | ConvertFrom-Json
}

$opening = Read-Pool 'supplemental_opening_wait_v2.json'
$ending = Read-Pool 'supplemental_session_end_v2.json'
$kill = Read-Pool 'supplemental_kill_praise_v2.json'
$question = Read-Pool 'supplemental_death_question_v2.json'
$flame = Read-Pool 'supplemental_death_flame_source_v2.json'

if (@($opening.messages).Count -ne 140 -or
    @($ending.messages).Count -ne 10 -or
    @($kill.messages).Count -ne 140 -or
    @($question.messages).Count -ne 40 -or
    @($flame.messages).Count -ne 15) {
    throw 'Unexpected supplemental pool count.'
}

foreach ($message in @($opening.messages) + @($kill.messages) + @($question.messages)) {
    if ([string]::IsNullOrWhiteSpace([string]$message.text) -or [string]$message.text -match "[\r\n]") {
        throw "New curated supplemental text must be non-empty and single-line: $($message.id)"
    }
}

foreach ($message in @($ending.messages) + @($flame.messages)) {
    $sourceIndex = [int]$message.source_index
    if ($sourceIndex -lt 1 -or $sourceIndex -gt $library.Count) {
        throw "Invalid supplemental source index: $($message.id)"
    }
    if ([string]$library[$sourceIndex - 1] -cne [string]$message.source_text) {
        throw "Supplemental source text was modified: $($message.id)"
    }
}

$directOpening = @($opening.messages | Where-Object { $_.intent -in @('open_door', 'urge_start') })
if ($directOpening.Count -ne 80) {
    throw "Expected 80 direct opening/call-to-start entries, found $($directOpening.Count)."
}

$negativePattern = '菜|白给|空枪|马枪|打不中|没打中|十枪九空|别玩'
$allowedKillIntents = @('short_hype', 'aim_praise', 'kill_hype', 'aftermath_praise')
foreach ($message in $kill.messages) {
    if ([string]$message.intent -notin $allowedKillIntents -or [string]$message.text -match $negativePattern) {
        throw "Kill supplemental polarity mismatch: $($message.id) $($message.text)"
    }
}
foreach ($message in $question.messages) {
    if ([string]$message.text -notmatch '[？?]') {
        throw "Death question supplemental lacks a question mark: $($message.id)"
    }
}
foreach ($message in $flame.messages) {
    if ([string]$message.source_text -notmatch '菜|空枪|十枪九空|白给|别玩') {
        throw "Death flame supplemental lacks direct flame intent: $($message.id)"
    }
}

# Mirror the production burst contract: five kill praise messages; death alternates
# three direct flames with two questions, all sampled without replacement.
$killBurst = @($kill.messages | Where-Object { $_.phase -in @('burst', 'both') } | Select-Object -First 5)
$deathFlameBurst = @($flame.messages | Where-Object { $_.phase -in @('burst', 'both') } | Select-Object -First 3)
$deathQuestionBurst = @($question.messages | Where-Object { $_.phase -in @('burst', 'both') } | Select-Object -First 2)
if ($killBurst.Count -ne 5 -or $deathFlameBurst.Count -ne 3 -or $deathQuestionBurst.Count -ne 2) {
    throw 'Supplemental pools cannot satisfy the five-message kill/death burst quota.'
}

$deathBurst = @(
    [string]$deathFlameBurst[0].source_text,
    [string]$deathQuestionBurst[0].text,
    [string]$deathFlameBurst[1].source_text,
    [string]$deathQuestionBurst[1].text,
    [string]$deathFlameBurst[2].source_text
)
if (@($deathBurst | Select-Object -Unique).Count -ne 5) {
    throw 'Death burst unexpectedly repeats text.'
}

$endingFamilies = @($ending.messages.family | Select-Object -Unique)
if ($endingFamilies.Count -lt 4) {
    throw 'Session ending pool cannot dispatch four distinct semantic families.'
}

$csproj = Get-Content -LiteralPath (Join-Path $RepositoryRoot 'Widget/KillConfirmGameBar.csproj') -Raw -Encoding UTF8
$repositorySource = Get-Content -LiteralPath (Join-Path $RepositoryRoot 'Widget/Danmaku/Engine/SupplementalDanmakuPoolRepository.cs') -Raw -Encoding UTF8
$weightSource = Get-Content -LiteralPath (Join-Path $RepositoryRoot 'Widget/Danmaku/Engine/DanmakuWeightEngine.cs') -Raw -Encoding UTF8
$sessionSource = Get-Content -LiteralPath (Join-Path $RepositoryRoot 'Widget/Danmaku/Engine/DanmakuSessionController.cs') -Raw -Encoding UTF8
$overlaySource = Get-Content -LiteralPath (Join-Path $RepositoryRoot 'Widget/Danmaku/DanmakuOverlay.xaml.cs') -Raw -Encoding UTF8
foreach ($fileName in @(
    'supplemental_opening_wait_v2.json',
    'supplemental_session_end_v2.json',
    'supplemental_kill_praise_v2.json',
    'supplemental_death_question_v2.json',
    'supplemental_death_flame_source_v2.json')) {
    if ($csproj -notmatch [regex]::Escape($fileName) -or $repositorySource -notmatch [regex]::Escape($fileName)) {
        throw "Supplemental pool is not packaged and loaded: $fileName"
    }
}
if ($weightSource -notmatch 'SupplementalDanmakuPoolKind\.KillPraise' -or
    $weightSource -notmatch 'SupplementalDanmakuPoolKind\.DeathQuestion' -or
    $weightSource -notmatch 'SupplementalDanmakuPoolKind\.DeathFlame' -or
    $sessionSource -notmatch 'SelectSessionEndDanmaku\(4' -or
    $overlaySource -notmatch 'OnSessionEnding') {
    throw 'Supplemental pool runtime routing is incomplete.'
}

Write-Host 'PASS: five supplemental pools are packaged, source-safe, polarity-isolated, and wired to opening/end/kill/death runtime paths.'
Write-Host ('KILL TEST: ' + (($killBurst | ForEach-Object text) -join ' | '))
Write-Host ('DEATH TEST: ' + ($deathBurst -join ' | '))

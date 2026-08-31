param(
    [string]$SourceRoot = 'E:\Zac\Download\FModel\Output\KillBanner_Readable_Complete',
    [string]$OutputRoot = (Join-Path (Split-Path -Parent $PSScriptRoot) 'ValorantExternalPacks'),
    [switch]$CleanOutput
)

$ErrorActionPreference = 'Stop'

# Collection names shown by the package browser. Keys are Riot's cooked asset
# codenames; values are the public English and Simplified Chinese collection
# names. Variant suffixes are added by Get-DisplayNames below.
$ThemeNames = @{
    'Afterglow' = @('RGX 11z Pro', 'RGX 11z Pro')
    'Afterglow2' = @('RGX 11z Pro//2.0', 'RGX 11z Pro//2.0')
    'Anomaly' = @('Divergence', '光荣异象')
    'Antares' = @('Araxys', '归零者')
    'Aquarium' = @('Neptune', '海洋之星')
    'Arcade' = @('Radiant Entertainment System', '源能者娱乐系统')
    'Ashen' = @("Gaia's Vengeance", '盖亚的复仇')
    'Atlas' = @('Spectrum', '电音光谱')
    'Base' = @('Base', '基础')
    'BlastX' = @('BlastX', '爆破特工')
    'Bolt' = @('Bolt', '神中神罚')
    'Champs24' = @('Champions 2024', '2024全球冠军赛')
    'Circle' = @('Origin', '起源')
    'Comicbook' = @('Radiant Crisis 001', '源能者危机001')
    'Commando' = @('Phaseguard', '超时空卫队')
    'Coven' = @('Nocturnum', '黯夜怪谈')
    'Cyberknight' = @('Doombringer', '毁灭骑士')
    'Cyberpunk' = @('Glitchpop', '全息波普')
    'Daedalus' = @('ChronoVoid', '虚空遗器')
    'DemonStone' = @('Prelude to Chaos', '混沌序曲')
    'Dragon' = @('Elderflame', '上古龙炎')
    'Dynasty' = @('Imperium', '偃月苍龙')
    'Edge' = @('Singularity', '奇点')
    'Edge02' = @('Singularity', '奇点')
    'Ego2' = @('ORA by OneTap', 'ORA x 颗秒')
    'Esports' = @('Champions', '全球冠军赛')
    'Fallen' = @('Forsaken', '堕天遗武')
    'FantasySovereign' = @('Sovereign', '天界神兵')
    'Golem' = @("Dolmir's Revenge", '多弥尔的复仇')
    'Gunslinger' = @('Neo Frontier', '西部未来')
    'Hazard' = @('Bubblegum Deathwish', '末日泡泡')
    'Hellfire' = @('Primordium', '洪荒怒焰')
    'Hologram' = @('EX.O', '创·纪元')
    'HypeBeast' = @('Prime', '紫阙金琅')
    'HypeBeast02' = @('Prime//2.0', '紫阙金琅//2.0')
    'HypeDragon' = @('XERØFANG', '耀鳞威龙')
    'King' = @('Ruination', '破败军械')
    'Legion' = @('Aemondir', '日冕雄师')
    'LNY26' = @('Solarstride', '赤焰铁骥')
    'Macaron' = @('Arcane', '双城之战')
    'Magepunk' = @('Magepunk', '奇幻朋克')
    'Magepunk3' = @('Magepunk//3.0', '奇幻朋克//3.0')
    'Midas' = @('Champions 2025', '2025全球冠军赛')
    'MonkeyKing' = @('Valiant Hero', '盖世英雄')
    'Motorbike' = @('Overdrive', '机动狂飙')
    'Ninja' = @('Kuronami', '塑水宗')
    'Oblivion' = @('Ion', '离子武器')
    'Oblivion2' = @('Ion//2.0', '离子武器//2.0')
    'Oni' = @('Oni', '般若假面')
    'Permafrost' = @('Cryostasis', '冰点凝冻')
    'Protocol' = @('Protocol 781-A', '781-A协议')
    'Rogue' = @('Rogue', '逆命中队')
    'Rose' = @('Blackthorn', '荆刺圣骸')
    'SeaOfStars' = @('Holo Meridian', '银河子午线')
    'Skymage' = @('Aeris', '圣契命澜')
    'Snake' = @('Helix', '异鳞魔蛇')
    'SOL' = @('Sentinels of Light', '光明哨兵')
    'SOL2' = @('Sentinels of Light//2.0', '光明哨兵//2.0')
    'Soulstealer' = @('Reaver', '掠影')
    'Soulstealer3_' = @('Reaver//3.0', '掠影//3.0')
    'SOV2' = @('Sovereign//2.0', '天界神兵//2.0')
    'Sovereign' = @('Sovereign', '天界神兵')
    'SpecOps' = @('Recon', '侦察力量')
    'Spirit' = @('Mystbloom', '千灵华绽')
    'Starpower' = @('Evori Dreamwings', '艾沃莉的梦之翼')
    'Syndra' = @('CYRAX', '噬影者')
    'Valkyrie' = @('Valkyrie', '女武神')
    'VCT' = @('VCT', '无畏契约冠军巡回赛')
    'VoidBorn' = @('Blackspyre', '暗域界碑')
    'WaterBlaster' = @('SplashX', '滋滋X特攻队')
    'Yokai' = @('Ayakashi', '胧夜月华')
}

function Get-NativeData([string]$path) {
    $json = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
    $entry = $json | Where-Object { $_.Properties.KillBannerData } | Select-Object -First 1
    if (-not $entry) { throw "KillBannerData missing: $path" }
    return $entry.Properties.KillBannerData
}

function Get-DataValue($data, [string]$prefix, $fallback) {
    $property = $data.PSObject.Properties | Where-Object { $_.Name.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase) } | Select-Object -First 1
    if ($property) { return $property.Value }
    return $fallback
}

function Get-ObjectName($reference) {
    if (-not $reference -or -not $reference.ObjectName) { return $null }
    if ($reference.ObjectName -match "'([^']+)'$") { return $Matches[1] }
    return [string]$reference.ObjectName
}

function Get-TextureReferences($node) {
    $result = [System.Collections.Generic.List[object]]::new()
    function Visit($value) {
        if ($null -eq $value -or $value -is [string] -or $value -is [ValueType]) { return }
        if (($null -ne $value.PSObject.Properties['ObjectName']) -and
            ([string]$value.ObjectName).StartsWith("Texture2D'", [StringComparison]::OrdinalIgnoreCase)) {
            $result.Add($value)
            return
        }
        if ($value -is [System.Collections.IEnumerable]) {
            foreach ($item in $value) { Visit $item }
            return
        }
        foreach ($property in $value.PSObject.Properties) { Visit $property.Value }
    }
    Visit $node
    return $result
}

function Get-EventName($reference) {
    return Get-ObjectName $reference
}

function Get-Slug([string]$baseName) {
    $slug = ($baseName -replace '(?i)^KillBannerData_?', '').ToLowerInvariant()
    $slug = $slug -replace '[^a-z0-9]+', '_'
    return $slug.Trim('_')
}

function Get-DisplayNames([System.IO.FileInfo]$file) {
    $theme = $file.FullName.Substring($SourceRoot.Length + 1).Split('\')[0]
    $raw = $file.BaseName -replace '(?i)^KillBannerData_?', ''
    $variant = $raw
    $themeStem = $theme.TrimEnd('_')
    if ($raw.StartsWith($themeStem, [StringComparison]::OrdinalIgnoreCase)) {
        $variant = $raw.Substring($themeStem.Length).Trim('_', '-', ' ')
    }
    elseif ($raw -match '(?i)(?:^|_)v(\d+)$') {
        $variant = "v$($Matches[1])"
    }
    if ($variant -match '^(?i)standard$') { $variant = '' }
    if ($variant -match '(?i)(?:^|_)v(\d+)$') { $variant = "v$($Matches[1])" }
    if ($theme -eq 'FantasySovereign' -and $variant -eq 'Fantasy') { $variant = '' }
    $names = $ThemeNames[$theme]
    $english = if ($names) { $names[0] } else { $theme }
    $chinese = if ($names) { $names[1] } else { "瓦击杀横幅：$theme" }
    if (-not [string]::IsNullOrWhiteSpace($variant)) {
        if ($variant -match '^(?i)v(\d+)$') {
            $english += " (Variant $($Matches[1]))"
            $chinese += "（炫彩$($Matches[1])）"
        }
        else {
            $english += " — $variant"
            $variantZh = @{
                'Ignition' = '点燃'
                'Lawyer' = '律师'
                'Raja' = '王者'
                'Renegade' = '叛逆'
                'Watch' = '守望'
            }[$variant]
            if ([string]::IsNullOrWhiteSpace($variantZh)) { $variantZh = $variant }
            $chinese += "（款式：$variantZh）"
        }
    }
    return [pscustomobject]@{ English = $english; Chinese = $chinese }
}

function Find-Texture($reference) {
    $name = Get-ObjectName $reference
    if ([string]::IsNullOrWhiteSpace($name)) { return $null }
    $fileName = "$name.png"
    $preferredTheme = $null
    if ($reference.ObjectPath -match '/KillBanner/([^/]+)/') { $preferredTheme = $Matches[1] }
    if ($preferredTheme) {
        $preferredRoot = Join-Path $SourceRoot $preferredTheme
        if (Test-Path -LiteralPath $preferredRoot) {
            $found = Get-ChildItem -LiteralPath $preferredRoot -Recurse -File -Filter $fileName | Select-Object -First 1
            if ($found) { return $found }
        }
    }
    $matches = $script:PngIndex[$fileName.ToLowerInvariant()]
    if ($matches) { return $matches | Select-Object -First 1 }
    return $null
}

function Copy-ProfileTexture($reference, [string]$textureRoot, [bool]$required) {
    $source = Find-Texture $reference
    if (-not $source) {
        if ($required) { throw "Required texture was not extracted: $(Get-ObjectName $reference)" }
        return $null
    }
    Copy-Item -LiteralPath $source.FullName -Destination (Join-Path $textureRoot $source.Name) -Force
    return $source.Name
}

function Get-AudioSources([string]$eventName, [string]$preferredTheme) {
    if ([string]::IsNullOrWhiteSpace($eventName)) { return @() }
    $sources = @($script:AudioByEvent[$eventName.ToLowerInvariant()] | Where-Object {
        -not [string]::IsNullOrWhiteSpace($_) -and (Test-Path -LiteralPath $_)
    })
    if (-not [string]::IsNullOrWhiteSpace($preferredTheme)) {
        $themeAudioRoot = [IO.Path]::GetFullPath((Join-Path $SourceRoot "$preferredTheme\Audio"))
        $preferred = @($sources | Where-Object {
            [IO.Path]::GetFullPath($_).StartsWith(
                $themeAudioRoot + [IO.Path]::DirectorySeparatorChar,
                [StringComparison]::OrdinalIgnoreCase)
        })
        if ($preferred.Count -gt 0) { return $preferred }
    }
    return $sources | Select-Object -Unique
}

function Add-AudioSlot(
    [hashtable]$slots,
    [string]$slot,
    $reference,
    [string]$packageRoot,
    [string]$preferredTheme
) {
    $eventName = Get-EventName $reference
    $sources = @(Get-AudioSources $eventName $preferredTheme)
    if ($sources.Count -eq 0) {
        $slots[$slot] = @()
        return $false
    }
    $names = @()
    for ($index = 0; $index -lt $sources.Count; $index++) {
        $suffix = if ($index -eq 0) { '' } else { '__' + ($index + 1) }
        $targetName = "$slot$suffix.wav"
        Copy-Item -LiteralPath $sources[$index] -Destination (Join-Path $packageRoot $targetName) -Force
        $names += $targetName
    }
    $slots[$slot] = if ($names.Count -eq 1) { $names[0] } else { $names }
    return $true
}

function New-PackageZip([string]$sourceFolder, [string]$destination) {
    if (Test-Path -LiteralPath $destination) { Remove-Item -LiteralPath $destination -Force }
    [System.IO.Compression.ZipFile]::CreateFromDirectory(
        $sourceFolder,
        $destination,
        [System.IO.Compression.CompressionLevel]::Optimal,
        $false)
}

if (-not (Test-Path -LiteralPath $SourceRoot -PathType Container)) {
    throw "VALORANT export root not found: $SourceRoot"
}

$resolvedOutput = [IO.Path]::GetFullPath($OutputRoot)
$workspaceOuter = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot)).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
if (-not ($resolvedOutput + [IO.Path]::DirectorySeparatorChar).StartsWith($workspaceOuter, [StringComparison]::OrdinalIgnoreCase)) {
    throw "OutputRoot must stay under the workspace outer folder: $workspaceOuter"
}
if ($CleanOutput -and (Test-Path -LiteralPath $resolvedOutput)) {
    Remove-Item -LiteralPath $resolvedOutput -Recurse -Force
}

$iconOutput = Join-Path $resolvedOutput 'IconPacks'
$voiceOutput = Join-Path $resolvedOutput 'VoicePacks'
$stagingRoot = Join-Path $resolvedOutput '.staging'
New-Item -ItemType Directory -Force -Path $iconOutput, $voiceOutput, $stagingRoot | Out-Null

Add-Type -AssemblyName System.IO.Compression.FileSystem

$script:PngIndex = @{}
Get-ChildItem -LiteralPath $SourceRoot -Recurse -File -Filter '*.png' | ForEach-Object {
    $key = $_.Name.ToLowerInvariant()
    if (-not $script:PngIndex.ContainsKey($key)) { $script:PngIndex[$key] = @() }
    $script:PngIndex[$key] += $_
}

$script:AudioByEvent = @{}
# The extraction report contains the paths used at extraction time. Those paths
# can become stale when the readable export is moved, while each theme's Audio
# directory travels with the JSON. Build the lookup from the actual WAV files so
# every generated voice package is self-contained and importable.
Get-ChildItem -LiteralPath $SourceRoot -Recurse -File -Filter '*.wav' | ForEach-Object {
    $eventName = $_.BaseName -replace '__\d+$', ''
    $key = $eventName.ToLowerInvariant()
    if (-not $script:AudioByEvent.ContainsKey($key)) { $script:AudioByEvent[$key] = @() }
    $script:AudioByEvent[$key] += $_.FullName
}

$parentData = Get-NativeData (Join-Path $SourceRoot 'CommonAssets\KillBannerData_Parent.json')
$dataFiles = @(Get-ChildItem -LiteralPath $SourceRoot -Recurse -File -Filter 'KillBannerData*.json' | Where-Object {
    $top = $_.FullName.Substring($SourceRoot.Length + 1).Split('\')[0]
    $top -notin @('Base', 'CommonAssets', '_Reports', '_WwiseEventJSON')
} | Sort-Object FullName)

$results = @()
foreach ($file in $dataFiles) {
    $slug = Get-Slug $file.BaseName
    $displayNames = Get-DisplayNames $file
    $displayName = $displayNames.English
    $displayNameZhCn = $displayNames.Chinese
    $associationId = "valorant:$slug"
    $iconId = "valorant_icon_$slug"
    $voiceId = "valorant_voice_$slug"
    $data = Get-NativeData $file.FullName
    $sourceTheme = $file.FullName.Substring($SourceRoot.Length + 1).Split('\')[0]

    $iconStage = Join-Path $stagingRoot $iconId
    $voiceStage = Join-Path $stagingRoot $voiceId
    $textureStage = Join-Path $iconStage 'textures'
    New-Item -ItemType Directory -Force -Path $textureStage, $voiceStage | Out-Null

    $textureReferences = @(Get-TextureReferences $data)
    foreach ($reference in $textureReferences) {
        $sourceTexture = Find-Texture $reference
        if ($sourceTexture) {
            Copy-Item -LiteralPath $sourceTexture.FullName -Destination (Join-Path $textureStage $sourceTexture.Name) -Force
        }
    }

    $primary = Get-DataValue $data 'PrimaryColor_' (Get-DataValue $parentData 'PrimaryColor_' $null)
    $accent = if ($primary -and $primary.Hex) { '#' + $primary.Hex } else { '#57F2D1' }
    if ($file.FullName -match '\\HypeBeast(02)?\\') { $accent = '#FF8000' }

    $frameRef = Get-DataValue $data 'BackgroundFrame_TXT_' (Get-DataValue $parentData 'BackgroundFrame_TXT_' $null)
    $frameDissolveRef = Get-DataValue $data 'BackgroundFrame_DSLV_' (Get-DataValue $parentData 'BackgroundFrame_DSLV_' $null)
    $ringRef = Get-DataValue $data 'KillWheel-TXT_' (Get-DataValue $parentData 'KillWheel-TXT_' $null)
    $barRef = Get-DataValue $data 'KillWheel_Slice_Default_' $null
    if (-not $barRef -or -not (Find-Texture $barRef)) { $barRef = Get-DataValue $parentData 'KillWheel_Slice_Default_' $null }
    $barHoverRef = Get-DataValue $data 'KillWheel_Slice_Hover_' $null
    if (-not $barHoverRef -or -not (Find-Texture $barHoverRef)) { $barHoverRef = Get-DataValue $parentData 'KillWheel_Slice_Hover_' $null }
    $emblemRef = Get-DataValue $data 'Badge_Default_TXT_' $null
    if (-not $emblemRef -or -not (Find-Texture $emblemRef)) { $emblemRef = Get-DataValue $parentData 'Badge_Default_TXT_' $null }
    $badgeDissolveRef = Get-DataValue $data 'Badge_DSLV_' (Get-DataValue $parentData 'Badge_DSLV_' $null)
    $headshotOffset = Get-DataValue $data 'Badge_HeadshotOffset_' (Get-DataValue $parentData 'Badge_HeadshotOffset_' $null)
    $sliceSize = Get-DataValue $data 'KillWheel_Slice_Radius_' (Get-DataValue $parentData 'KillWheel_Slice_Radius_' 147.0)
    $bladeRef = $textureReferences | Where-Object { (Get-ObjectName $_) -match '(?i)blade' } | Select-Object -First 1
    $specialFrameRef = $textureReferences | Where-Object {
        $name = Get-ObjectName $_
        $name -match '(?i)frame' -and $name -notmatch '(?i)dissolve' -and $_ -ne $frameRef
    } | Select-Object -First 1

    $profile = [ordered]@{
        accent = $accent
        emblem = Copy-ProfileTexture $emblemRef $textureStage $true
        frame = Copy-ProfileTexture $frameRef $textureStage $false
        bar = Copy-ProfileTexture $barRef $textureStage $true
        bar_hover = Copy-ProfileTexture $barHoverRef $textureStage $true
        ring = Copy-ProfileTexture $ringRef $textureStage $false
        frame_dissolve = Copy-ProfileTexture $frameDissolveRef $textureStage $false
        badge_dissolve = Copy-ProfileTexture $badgeDissolveRef $textureStage $false
        blade = Copy-ProfileTexture $bladeRef $textureStage $false
        special_frame = Copy-ProfileTexture $specialFrameRef $textureStage $false
        headshot_x = if ($headshotOffset) { [double]$headshotOffset.X } else { 0.0 }
        headshot_y = if ($headshotOffset) { [double]$headshotOffset.Y } else { -20.0 }
        slice_size = [double]$sliceSize
    }

    $iconManifest = [ordered]@{
        format_version = 2
        package_kind = 'valorant_icon'
        id = $iconId
        association_id = $associationId
        display_name = $displayName
        display_name_zh_cn = $displayNameZhCn
        source_theme = $file.FullName.Substring($SourceRoot.Length + 1).Split('\')[0]
        source_data = $file.Name
        profile = $profile
    }
    $iconManifest | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $iconStage 'manifest.json') -Encoding utf8NoBOM

    $slots = [ordered]@{}
    $normalPrefixes = @('Sound_Kill_', 'Sound_DoubleKill_', 'Sound_TripleKill_', 'Sound_QuadraKill_', 'Sound_PentaKill_')
    for ($kill = 1; $kill -le 5; $kill++) {
        $reference = Get-DataValue $data $normalPrefixes[$kill - 1] (Get-DataValue $parentData $normalPrefixes[$kill - 1] $null)
        [void](Add-AudioSlot $slots "kill_$kill" $reference $voiceStage $sourceTheme)
    }

    $headshotPrefixes = @('Sound_Kill_Headshot_', 'Sound_DoubleKill_Headshot_', 'Sound_TripleKill_Headshot_', 'Sound_QuadraKill_Headshot_', 'Sound_PentaKill_Headshot_')
    $hasTierHeadshot = $false
    for ($kill = 1; $kill -le 5; $kill++) {
        $reference = Get-DataValue $data $headshotPrefixes[$kill - 1] (Get-DataValue $parentData $headshotPrefixes[$kill - 1] $null)
        if (Add-AudioSlot $slots "headshot_$kill" $reference $voiceStage $sourceTheme) { $hasTierHeadshot = $true }
    }
    $slots['headshot'] = @()

    $appearRef = Get-DataValue $data 'Sound_Appear_' (Get-DataValue $parentData 'Sound_Appear_' $null)
    $transitionRef = Get-DataValue $data 'Sound_ReAppear_' (Get-DataValue $parentData 'Sound_ReAppear_' $null)
    $hasAppear = Add-AudioSlot $slots 'appear' $appearRef $voiceStage $sourceTheme
    $hasTransition = Add-AudioSlot $slots 'transition' $transitionRef $voiceStage $sourceTheme

    if (-not (Get-ChildItem -LiteralPath $voiceStage -File -Filter '*.wav' | Select-Object -First 1)) {
        throw "Voice package contains no resolved audio: $($file.FullName)"
    }

    $slotGains = [ordered]@{ appear = 0.3; transition = 0.3 }
    $voiceManifest = [ordered]@{
        format_version = 2
        package_kind = 'valorant_voice'
        id = $voiceId
        association_id = $associationId
        display_name = $displayName
        display_name_zh_cn = $displayNameZhCn
        name = $displayName
        game_style = 'valorant'
        version = '2.0'
        source_theme = $file.FullName.Substring($SourceRoot.Length + 1).Split('\')[0]
        source_data = $file.Name
        audio = [ordered]@{
            base_gain = 1.0
            slots = $slots
            slot_gains = $slotGains
            overlay_slots = if ($hasAppear -or $hasTransition) { @('kill_1', 'kill_2', 'kill_3', 'kill_4', 'kill_5') } else { @() }
        }
    }
    $voiceManifest | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $voiceStage 'manifest.json') -Encoding utf8NoBOM

    $iconZip = Join-Path $iconOutput "$iconId.zip"
    $voiceZip = Join-Path $voiceOutput "$voiceId.zip"
    New-PackageZip $iconStage $iconZip
    New-PackageZip $voiceStage $voiceZip

    $results += [ordered]@{
        association_id = $associationId
        display_name = $displayName
        display_name_zh_cn = $displayNameZhCn
        icon_package = [IO.Path]::GetFileName($iconZip)
        voice_package = [IO.Path]::GetFileName($voiceZip)
        tier_headshot_audio = $hasTierHeadshot
        appear_audio = $hasAppear
        transition_audio = $hasTransition
    }
    Remove-Item -LiteralPath $iconStage, $voiceStage -Recurse -Force
    Write-Host "[$($results.Count)/$($dataFiles.Count)] $displayName"
}

$index = [ordered]@{
    format_version = 2
    generated_at_utc = [DateTime]::UtcNow.ToString('o')
    source_root = $SourceRoot
    package_count = $results.Count * 2
    association_count = $results.Count
    packages = $results
}
$index | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $resolvedOutput 'package-index.json') -Encoding utf8NoBOM
if (Test-Path -LiteralPath $stagingRoot) { Remove-Item -LiteralPath $stagingRoot -Recurse -Force }

Write-Host "Created $($results.Count) icon packages and $($results.Count) voice packages in $resolvedOutput"

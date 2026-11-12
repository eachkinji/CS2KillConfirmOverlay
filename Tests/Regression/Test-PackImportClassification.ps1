param([string]$PackageRoot='')
$ErrorActionPreference='Stop'
$repository=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
Add-Type -Path (Join-Path $repository 'Common/PackImportClassification.cs')
function Classify($m, [bool]$audioFiles=$false, [bool]$imageFiles=$false) {
    [KillConfirmCompatibility.Contracts.PackImportClassification]::Read(
        [string]$m.game_style,[string]$m.game,[string]$m.package_kind,[string]$m.kind,
        [string]$m.audio_kind,($null -ne $m.audio),$audioFiles,$imageFiles)
}
function Must-Reject([scriptblock]$Action) {
    $rejected=$false
    try { & $Action } catch { $rejected=$true }
    if(!$rejected){throw 'An incompatible pack was accepted.'}
}
$cf=Classify @{package_kind='crossfire_voice';audio=@{slots=@{}}}
$val=Classify @{package_kind='valorant_voice';audio=@{slots=@{}}}
if($cf.AudioKind -ne 'character_voice' -or $val.AudioKind -ne 'sound_effect'){throw 'Legacy native audio kinds were not recovered.'}
$cf.Validate('crossfire',$true); $val.Validate('valorant',$true)
Must-Reject {$cf.Validate('valorant',$true)}
Must-Reject {$val.Validate('crossfire',$true)}
Must-Reject {$val.Validate('valorant',$false)}
$legacyIcon=Classify @{game='crossfire';kind='icon'}
Must-Reject {$legacyIcon.Validate('crossfire',$true)}
Must-Reject {$legacyIcon.Validate('calabiyau',$false)}
Must-Reject {Classify @{game_style='calabiyau';package_kind='crossfire_voice'}}
Must-Reject {Classify @{package_kind='valorant_icon';audio_kind='sound_effect'}}
Must-Reject {Classify @{audio_kind='misspelled'}}
foreach($kind in @('sound_effect','character_voice')) {
    $typed=Classify @{game_style='calabiyau';package_kind='audio';audio_kind=$kind;audio=@{slots=@{}}}
    if($typed.AudioKind -ne $kind){throw 'Explicit type lost.'}
    $typed.Validate('calabiyau',$true)
    Must-Reject {$typed.Validate('valorant',$true)}
}
$rawAudio=Classify @{} $true $false
$rawImage=Classify @{} $false $true
foreach($game in @('crossfire','valorant','calabiyau')) {
    $rawAudio.Validate($game,$true);$rawImage.Validate($game,$false)
}
if($rawAudio.Game -or $rawImage.Game){throw 'A game was invented for raw assets.'}
Must-Reject {$rawAudio.Validate('crossfire',$false)}
Must-Reject {$rawImage.Validate('crossfire',$true)}
$aliases=@{
    overwatch=@('ow');modernwarfare2019=@('modernwarfare','mw2019','mw19');apex=@('apexlegends','apex_legends')
    battlefield1=@('bf1');battlefield5=@('bf5');battlefield4=@('bf4');battlefield2042=@('bf2042','2042')
    battlefield6=@('battlefield_6','bf6');deltaforce=@('delta','df');calabiyau=@('strinova','卡拉彼丘')
    doubao=@('豆包');dagoujiao=@('大狗叫');sanguosha=@('三国杀','sgs')
    cod2022=@('mw2022','mwii');titanfall2=@('tf2');leagueoflegends=@('lol','英雄联盟');rdr2=@('reddeadredemption2','荒野大镖客2');warthunder=@('wt','战争雷霆')
}
foreach($canonical in $aliases.Keys) {
    foreach($alias in $aliases[$canonical]) {
        $aliased=Classify @{game_style=(' '+$alias.ToUpperInvariant()+' ');game=$canonical;package_kind=' AUDIO ';audio_kind=' SFX ';audio=@{slots=@{}}}
        $aliased.Validate($canonical,$true);$aliased.Validate($alias,$true)
        if($aliased.Game -ne $canonical -or $aliased.AudioKind -ne 'sound_effect'){throw 'Game alias/case normalization changed pack semantics.'}
        Must-Reject {$aliased.Validate('crossfire',$true)}
    }
}
foreach($game in @('CROSSFIRE','VALORANT','CALABIYAU','CUSTOMMODULE')) {
    (Classify @{game_style=$game;package_kind='AUDIO';audio_kind='VOICE';audio=@{slots=@{}}}).Validate($game.ToLowerInvariant(),$true)
}
(Classify @{game_style='STRINOVA';package_kind='CALABIYAU_ICON'}).Validate('calabiyau',$false)
Must-Reject {(Classify @{game_style='unknown-game';audio_kind='voice'}).Validate('crossfire',$true)}
$mixed=Classify @{} $true $true
$mixed.Validate('custommodule',$false)
if($mixed.Game -or $mixed.Content -ne 'Unknown'){throw 'Mixed raw CustomModule assets were misclassified.'}
Write-Output 'PASS: typed/legacy and case-insensitive classification, known game aliases, unknown/wrong-game/type rejection, contradictory metadata, and raw/mixed manual imports.'
if(!$PackageRoot){return}
$root=[IO.Path]::GetFullPath($PackageRoot)
$catalog=Get-Content -LiteralPath (Join-Path $root '素材清单.json') -Raw -Encoding utf8 | ConvertFrom-Json
$games=@{
    '卡拉彼丘'='calabiyau';'无畏契约'='valorant';'穿越火线'='crossfire'
    '使命召唤：现代战争II'='cod2022';'泰坦陨落2'='titanfall2';'英雄联盟'='leagueoflegends';'荒野大镖客：救赎2'='rdr2';'战争雷霆'='warthunder'
}
$checked=0
foreach($entry in $catalog.packages) {
    $archive=[IO.Compression.ZipFile]::OpenRead((Join-Path $root $entry.file))
    try {
        $manifests=@($archive.Entries | Where-Object {[IO.Path]::GetFileName($_.FullName) -ieq 'manifest.json'})
        if($manifests.Count -ne 1){throw "Unexpected manifest count: $($entry.file)"}
        $reader=[IO.StreamReader]::new($manifests[0].Open())
        try {$manifest=$reader.ReadToEnd() | ConvertFrom-Json} finally {$reader.Dispose()}
        $classification=Classify $manifest
        $game=$games[$entry.game];$audio=$entry.category -ne '图标包'
        $classification.Validate($game,$audio)
        if($audio) {
            $expected=if($entry.category -eq '语音包'){'character_voice'}else{'sound_effect'}
            if($classification.AudioKind -ne $expected){throw "Wrong audio class: $($entry.file)"}
        }
        Must-Reject {$classification.Validate($game,!$audio)}
        foreach($other in $games.Values | Where-Object {$_ -ne $game}) {Must-Reject {$classification.Validate($other,$audio)}}
        $checked++
    } finally {$archive.Dispose()}
}
if($checked -ne $catalog.package_count){throw 'Incomplete package coverage.'}
Write-Output "PASS: $checked real ZIP manifests accepted in their own library, audio kinds recovered, opposite media and both other games rejected."

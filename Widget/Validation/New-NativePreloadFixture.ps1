param([string]$Output='')
$ErrorActionPreference='Stop'
$repo=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
if (!$Output) { $Output=Join-Path $repo ('Output/NativePreloadFixture-'+(Get-Date -Format 'yyyyMMdd-HHmmss')) }
$Output=[IO.Path]::GetFullPath($Output)
if (Test-Path -LiteralPath $Output) { throw 'Use a fresh fixture directory.' }
$profile=Join-Path $Output 'profile'
$original=Join-Path $profile 'Packs/default'
$sparse=Join-Path $profile 'Packs/custom_icon_cf_sparse_validation'
$payload=Join-Path $Output 'payload'
New-Item -ItemType Directory -Path $original,$sparse -Force | Out-Null
& robocopy (Join-Path $repo 'SourceAssets/DefaultPacks/crossfire/icon_packs/default') $original /E /R:1 /W:1 /NFL /NDL /NJH /NJS /NP | Out-Null
if ($LASTEXITCODE -ge 8) { throw 'Could not copy development CF fixture.' }
# Reproduce the reported large folder without copying the user's installed library.
# These unreferenced 1-pixel PNGs must never be transferred by sparse fallback.
$tiny=[Convert]::FromBase64String('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVQIHWP4z8DwHwAFgAI/ScLbtAAAAABJRU5ErkJggg==')
$count=@(Get-ChildItem -LiteralPath $original -File -Recurse).Count
for ($i=$count; $i -lt 982; $i++) { [IO.File]::WriteAllBytes((Join-Path $original ('unreferenced-'+$i+'.png')),$tiny) }
Copy-Item -LiteralPath (Join-Path $original 'badge_multi1.png') -Destination $sparse
$catalog=@{IconPackOrder=@();VoicePackOrder=@();VoicePacks=@();IconPacks=@(
    @{Key='default';DisplayName='CF original fixture';FolderPath=$original;IsBuiltIn=$false;IsVisibleInWidget=$true;OwnsFolder=$true;HasEliteOverlay=$true;HasKillFxOverlay=$true;HasWeaponBadgeOverlay=$true},
    @{Key='custom_icon_cf_sparse_validation';DisplayName='CF sparse fixture';FolderPath=$sparse;IsBuiltIn=$false;IsVisibleInWidget=$true;OwnsFolder=$true;HasEliteOverlay=$true;HasKillFxOverlay=$true;HasWeaponBadgeOverlay=$true},
    @{Key='sanguosha';DisplayName='Sanguosha native';IsBuiltIn=$true;IsVisibleInWidget=$true;OwnsFolder=$false}
)}
[IO.File]::WriteAllText((Join-Path $profile 'settings.json'),'{}')
[IO.File]::WriteAllText((Join-Path $profile 'pack-catalog.json'),($catalog | ConvertTo-Json -Depth 12 -Compress))
$atlas=Join-Path $payload 'Assets/GameStyles/sanguosha/killconfirm'
& robocopy (Join-Path $repo 'SourceAssets/GameStyles/sanguosha/killconfirm') $atlas '*.png' '*.sgsd.gz' /S /XD lz4 /R:1 /W:1 /NFL /NDL /NJH /NJS /NP | Out-Null
if ($LASTEXITCODE -ge 8) { throw 'Could not copy native Sanguosha development atlases.' }
[pscustomobject]@{CatalogProfile=$profile;Payload=$payload;OriginalFiles=982;AtlasFiles=@(Get-ChildItem -LiteralPath $atlas -File -Recurse).Count}

param(
    [Parameter(Mandatory)][string]$Payload,
    [string]$ProfileSource=(Join-Path $env:LOCALAPPDATA 'KillConfirmOverlay/UserData'),
    [switch]$Inspect
)
$ErrorActionPreference='Stop'
$repository=Split-Path $PSScriptRoot -Parent
$Payload=[IO.Path]::GetFullPath($Payload)
$ProfileSource=[IO.Path]::GetFullPath($ProfileSource)
$output=Join-Path $repository ('Output/StartupValidation-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
$profile=Join-Path $output 'profile'
New-Item -ItemType Directory -Path $profile -Force | Out-Null
foreach($name in @('settings.json','pack-catalog.json')) {
    Copy-Item -LiteralPath (Join-Path $ProfileSource $name) -Destination $profile
}
# Real independent copies: the voice-repair path must never write through a
# junction into the user's packs, and the test must not inherit their auth token.
& robocopy (Join-Path $ProfileSource 'Packs/valorant') (Join-Path $profile 'Packs/valorant') /E /R:1 /W:1 /NFL /NDL /NJH /NJS /NP
if($LASTEXITCODE -ge 8) {throw 'Could not copy the imported-library fixture.'}
$settingsPath=Join-Path $profile 'settings.json'
$settings=Get-Content -LiteralPath $settingsPath -Raw | ConvertFrom-Json -AsHashtable
$settings['GameStyleMode']=@{Kind='string';Text='valorant'}
[IO.File]::WriteAllText($settingsPath,($settings | ConvertTo-Json -Depth 10 -Compress))
$catalogPath=Join-Path $profile 'pack-catalog.json'
$catalog=(Get-Content -LiteralPath $catalogPath -Raw).Replace($ProfileSource.Replace('\','\\'),$profile.Replace('\','\\'))
[IO.File]::WriteAllText($catalogPath,$catalog)
New-Item -ItemType Directory -Path (Join-Path $profile 'CompatibilityDisplay') -Force | Out-Null
[IO.File]::WriteAllText((Join-Path $profile 'CompatibilityDisplay/display.json'), '{"Enabled":true,"FollowGame":false,"HideWhenInactive":false}')
$start=[Diagnostics.ProcessStartInfo]::new((Join-Path $Payload 'KillConfirmControlPanel.exe'))
$start.UseShellExecute=$false
$start.WindowStyle=[Diagnostics.ProcessWindowStyle]::Hidden
$start.Environment['KILLCONFIRM_DATA_ROOT']=$profile
$start.Environment['KILLCONFIRM_INSTALL_ROOT']=$Payload
$start.Environment['KILLCONFIRM_UI_VALIDATION']='1'
$start.Environment['KILLCONFIRM_STARTUP_VALIDATION']='1'
if ($Inspect) { $start.Environment['KILLCONFIRM_UI_INSPECTION']='1' }
$process=[Diagnostics.Process]::Start($start)
if ($Inspect) { Write-Output "Inspection PID: $($process.Id); isolated profile: $profile"; return }
if(!$process.WaitForExit(115000)) {$process.Kill();throw 'Large-library startup/dropdown validation timed out.'}
$pass=Join-Path $profile 'ui-pass.txt'
if(!(Test-Path -LiteralPath $pass)) {throw (Get-Content -LiteralPath (Join-Path $profile 'ui-failure.txt') -Raw)}
Get-Content -LiteralPath $pass
Write-Output "Artifacts: $output"

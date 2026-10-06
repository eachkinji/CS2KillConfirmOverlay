param([string]$Payload='', [switch]$RuntimeOnly)
$ErrorActionPreference='Stop'
$repository=Split-Path $PSScriptRoot -Parent
if(!$Payload) {$Payload=Join-Path $repository 'Output/StandaloneTest'}
$Payload=[IO.Path]::GetFullPath($Payload)
$output=Join-Path $repository ('Output/StandaloneValidation-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
function Invoke-PanelCheck([string]$name,[string]$flag,[string]$result) {
    $profile=Join-Path $output $name
    $start=[Diagnostics.ProcessStartInfo]::new((Join-Path $Payload 'KillConfirmGameBar.exe'))
    $start.UseShellExecute=$false
    $start.Environment['KILLCONFIRM_DATA_ROOT']=$profile
    $start.Environment['KILLCONFIRM_INSTALL_ROOT']=$Payload
    $start.Environment[$flag]='1'
    $process=[Diagnostics.Process]::Start($start)
    if(!$process.WaitForExit(60000)) {$process.Kill();throw "$name timed out."}
    $pass=Join-Path $profile "$result-pass.txt"
    $failure=Join-Path $profile "$result-failure.txt"
    if(!(Test-Path -LiteralPath $pass)) {if(Test-Path -LiteralPath $failure) {throw (Get-Content -LiteralPath $failure -Raw)};throw "$name did not complete; inspect $profile/control-panel.log"}
    Get-Content -LiteralPath $pass
}
if(!$RuntimeOnly) {Invoke-PanelCheck 'ui' 'KILLCONFIRM_UI_VALIDATION' 'ui'}
Invoke-PanelCheck 'runtime' 'KILLCONFIRM_RUNTIME_VALIDATION' 'runtime'
Write-Host "Validation artifacts: $output"

#Requires -Version 7.0
param([Parameter(Mandatory=$true)][string]$Payload)
$ErrorActionPreference='Stop'
$repository=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$fixture=Join-Path $repository ('Output/LegacyDisplay-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixture | Out-Null
$start=[Diagnostics.ProcessStartInfo]::new((Join-Path ([IO.Path]::GetFullPath($Payload)) 'KillConfirmGameBar.exe'))
$start.UseShellExecute=$false
$start.ArgumentList.Add('--validate-legacy-profile')
$start.Environment['KILLCONFIRM_DATA_ROOT']=Join-Path $fixture 'UserData'
$start.Environment['KILLCONFIRM_INSTALL_ROOT']=[IO.Path]::GetFullPath($Payload)
$start.Environment['KILLCONFIRM_LEGACY_VALIDATION']='1'
$process=[Diagnostics.Process]::Start($start)
if(!$process.WaitForExit(60000)) {$process.Kill();throw 'Actual legacy display validation timed out'}
$failure=Join-Path $fixture 'legacy-failure.txt'
if(Test-Path -LiteralPath $failure){throw (Get-Content -LiteralPath $failure -Raw)}
$pass=Join-Path $fixture 'legacy-pass.txt'
if(!(Test-Path -LiteralPath $pass)){throw "Validation did not complete: $fixture"}
Get-Content -LiteralPath $pass

#Requires -Version 7.0
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$PrerequisiteRoot=Join-Path $root ('Output/PrerequisiteSelection-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $PrerequisiteRoot | Out-Null
$results=[Collections.Generic.List[object]]::new()
$script:submitted=[Collections.Generic.List[string]]::new()
$script:installed=[Collections.Generic.HashSet[string]]::new()
function ConvertFrom-Utf8Base64 {param($Value); [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($Value))}
function Write-InstallLog {param($Message)}
function Write-InstallStage {param($Number,$Total,$Name,$Detail)}
function Add-InstallResult {param($Status,$Item,$Detail); $results.Add([pscustomobject]@{Status=$Status;Item=$Item;Detail=$Detail})}
function Get-ErrorReason {param($ErrorRecord); $ErrorRecord.Exception.Message}
. (Join-Path $root 'Installer/Scripts/Install/Prerequisites.ps1')
foreach($item in $Prerequisites){Set-Content -LiteralPath (Join-Path $PrerequisiteRoot $item.FileName) -Value 'fixture'}
function Get-Process {return @()}
function Test-PrerequisiteInstalled {param($Prerequisite); $script:installed.Contains($Prerequisite.PackageName)}
function Get-InstalledPrerequisitePackage {param($Prerequisite); if($script:installed.Contains($Prerequisite.PackageName)){[pscustomobject]@{Version=$Prerequisite.MinimumVersion;Architecture='X64'}}}
function Add-AppxPackageCompat {
 param($PackagePath,[switch]$ForceUpdate)
 $item=$Prerequisites|Where-Object {$_.FileName -eq (Split-Path -Leaf $PackagePath)}|Select-Object -First 1
 $script:submitted.Add($item.Component)
 if($script:simulateTimeout){$script:AppxDeploymentTimedOut=$true;throw [TimeoutException]::new('fixture timeout')}
 [void]$script:installed.Add($item.PackageName)
}
$SkippedPrerequisites=@('xaml','gamebar')
Install-RequiredComponents -Confirmed
if(($script:submitted -join ',') -ne 'vcdesktop,vcuwp,netframework,netruntime'){throw 'Deselected prerequisite was deployed, or selection/order was ignored'}
# Exercise the real installer phase: a stalled prerequisite must bypass the
# widget stage, leaving the already-installed ordinary application available.
$script:submitted.Clear();$script:installed.Clear()
$SkippedPrerequisites=@();$script:simulateTimeout=$true;$script:AppxDeploymentTimedOut=$false
$InstallPrerequisites=$true;$PrerequisitesConfirmed=$true;$compatibilityFallback=$false
$script:widgetAttempts=0
function Install-OverlayPackage {$script:widgetAttempts++}
function Test-OverlayPackageInstalled {throw 'Widget verification must not run after timeout'}
function Test-XboxGameBarAvailable {return $true}
$entry=Get-Content -LiteralPath (Join-Path $root 'Installer/Install-KillConfirm.ps1') -Raw
$phase=[regex]::Match($entry,'(?s)    Write-InstallStage -Number 3.*?(?=    Write-InstallStage -Number 5)').Value
if(!$phase){throw 'Installer deployment phases missing'}
. ([scriptblock]::Create($phase))
if(!$compatibilityFallback -or $script:widgetAttempts -ne 0 -or $script:submitted.Count -ne 1){throw 'Timed-out prerequisite continued to later deployments instead of compatibility fallback'}
Write-Output 'PASS: individual dependency choices are honored; a prerequisite timeout bypasses later deployments and retains compatibility installation.'

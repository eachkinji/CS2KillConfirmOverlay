#Requires -Version 7.0
$ErrorActionPreference='Stop'
$repository=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$fixture=Join-Path $repository ('Output/LegacyPanel-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixture | Out-Null
$PackageName='KillConfirmGameBar.Overlay'
$legacyPackage=[pscustomobject]@{PackageFullName='KillConfirmGameBar.Overlay_4.5.1.0_x64__5jgcw66eyez0m';PackageFamilyName='KillConfirmGameBar.Overlay_5jgcw66eyez0m';InstallLocation=$fixture}
$script:installed=$legacyPackage
$script:events=[Collections.Generic.List[string]]::new()
$script:failMigration=$false;$script:failRemoval=$false
function Get-AppxPackage {param($Name);$script:installed}
function Write-InstallLog {param($Message)}
function Add-InstallResult {param($Status,$Item,$Detail)}
. (Join-Path $repository 'Installer/Scripts/Install/LegacyUpgrade.ps1')
function Stop-OverlayRuntimeForUpdate {$script:events.Add('stop')}
function Invoke-LegacyProfilePreparation {param($Package);$script:events.Add('migrate');if($script:failMigration){throw 'fixture corrupt data'};[pscustomobject]@{Success=$true;BackupPath=$fixture}}
function Invoke-AppxDeploymentWorker {param($Parameters);if($Parameters.Operation -ne 'Remove' -or $Parameters.PackageFullName -ne $legacyPackage.PackageFullName){throw 'Wrong package removal'};$script:events.Add('remove');if($script:failRemoval){throw [TimeoutException]::new('fixture removal timeout')};$script:installed=$null}
# Exactly the same mandatory cleanup runs when optional components are skipped.
foreach($SkipGameBar in @($true,$false)) {
 $script:installed=$legacyPackage;$script:events.Clear()
 Complete-LegacyControlPanelUpgrade
 if(($script:events -join ',') -ne 'stop,migrate,remove' -or $script:installed){throw 'Legacy panel was retained or removed before migration'}
}
$script:installed=$legacyPackage;$script:events.Clear();$script:failMigration=$true
$failed=$false;try{Complete-LegacyControlPanelUpgrade}catch{$failed=$true}
if(!$failed -or $script:events.Contains('remove') -or !$script:installed){throw 'Migration failure removed the old package'}
$script:failMigration=$false;$script:failRemoval=$true;$script:events.Clear()
$failed=$false;try{Complete-LegacyControlPanelUpgrade}catch{$failed=$_.Exception -is [TimeoutException]}
if(!$failed -or !$script:installed){throw 'Removal failure was silently treated as a successful upgrade'}
$script:failRemoval=$false
New-Item -ItemType Directory -Path (Join-Path $fixture 'Bridge') | Out-Null
Set-Content -LiteralPath (Join-Path $fixture 'Bridge/killconfirm-widget-bridge.exe') -Value 'new widget'
$script:events.Clear();Complete-LegacyControlPanelUpgrade
if($script:events.Count){throw 'Current widget-only package was treated as an old control panel'}
$entry=Get-Content -LiteralPath (Join-Path $repository 'Installer/Install-KillConfirm.ps1') -Raw
if($entry.IndexOf('    Complete-LegacyControlPanelUpgrade') -gt $entry.IndexOf('    $compatibilityFallback = -not')){throw 'Legacy cleanup remains conditional on optional Game Bar installation'}
Write-Output 'PASS: skip/full installation retires the old panel after migration; failed migration/removal never reports completion; current widget remains installed.'

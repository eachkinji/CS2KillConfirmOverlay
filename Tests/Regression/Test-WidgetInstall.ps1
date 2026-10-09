# Verify fresh widget installs and same-version repairs with an unpackaged core.
$ErrorActionPreference = 'Stop'
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$fixture = Join-Path $repository ('Output/WidgetInstall-' + [guid]::NewGuid().ToString('N'))
$ScriptRoot = Join-Path $fixture 'Payload'
$OverlayRoot = Join-Path $ScriptRoot 'OverlayPackage'
$packageRoot = Join-Path $fixture 'RegisteredWidget'
New-Item -ItemType Directory -Path $OverlayRoot,(Join-Path $packageRoot 'Bridge') -Force | Out-Null
foreach ($name in @('widget.msixbundle','widget.cer')) { Set-Content -LiteralPath (Join-Path $OverlayRoot $name) -Value 'fixture' }
New-Item -ItemType Directory -Path (Join-Path $OverlayRoot 'Dependencies/x64') -Force | Out-Null
Set-Content -LiteralPath (Join-Path $OverlayRoot 'Dependencies/x64/xaml.appx') -Value 'deselected fixture'
$Prerequisites=@([pscustomobject]@{Component='xaml';PackageName='Microsoft.UI.Xaml.2.8'})
$SkippedPrerequisites=@('xaml')
$bridge = Join-Path $packageRoot 'Bridge/killconfirm-widget-bridge.exe'
Set-Content -LiteralPath $bridge -Value 'fixture'
$script:registered = [pscustomobject]@{Version='4.5.1.55';Status='Ok';InstallLocation=$packageRoot;PackageFamilyName='WidgetFixture';PackageFullName='WidgetFixture_56'}
$PackageName = 'WidgetFixture'
$script:registrations = 0
$script:loopbacks = 0
$results = [Collections.Generic.List[object]]::new()
function Write-InstallLog { param($Message) }
function Add-InstallResult { param($Status,$Item,$Detail); $results.Add([pscustomobject]@{Status=$Status;Item=$Item;Detail=$Detail}) }
function Import-PackageCertificate { param($CertificatePath); [pscustomobject]@{ImportedCount=1} }
function Get-AppxIdentityFromPackageFile { param($PackagePath); if($PackagePath.EndsWith('xaml.appx')){[pscustomobject]@{Name='Microsoft.UI.Xaml.2.8';Version=[version]'8.2310.30001.0'}}else{[pscustomobject]@{Name='WidgetFixture';Version=[version]'4.5.1.56'}} }
function Get-AppxPackage { param($Name); $script:registered }
function Get-InstalledOverlayPackage { $script:registered }
function Update-InstalledPackageContext { $script:registered }
function Add-AppxPackageCompat { param($PackagePath,[switch]$ForceUpdate,[switch]$DeferWhenInUse); $script:registrations++; $script:registered.Version='4.5.1.56' }
function Enable-LoopbackExemptionVerified { param($AppPackageFamilyName); $script:loopbacks++ }
. (Join-Path $repository 'Installer/Scripts/Install/Overlay.ps1')
function Stop-OverlayRuntimeForUpdate { }
# Match the entry script: payload verification must succeed before loopback setup.
function Install-WidgetFixture {
    Install-OverlayPackage
    Test-OverlayPackageInstalled
    Enable-LoopbackExemptionVerified -AppPackageFamilyName 'WidgetFixture'
}
Install-WidgetFixture
if ($script:registrations -ne 1 -or $script:loopbacks -ne 1) { throw 'Fresh widget installation skipped loopback setup.' }
if (Test-Path -LiteralPath (Join-Path $packageRoot 'KillConfirmService/cskillconfirm.exe')) { throw 'Fixture accidentally contains a packaged core.' }
Install-WidgetFixture
if ($script:registrations -ne 1 -or $script:loopbacks -ne 2) { throw 'Same-version repair did not restore loopback access.' }
Remove-Item -LiteralPath $bridge
$rejected = $false
try { Install-WidgetFixture } catch { $rejected = $_.Exception.Message.Contains('桥接程序不存在') }
if (!$rejected -or $script:loopbacks -ne 2) { throw 'A registered widget with a missing launch bridge was accepted.' }
Write-Host 'PASS: fresh widget registration and same-version repair configure loopback without a packaged core; missing launch bridge is rejected.'

$ErrorActionPreference='Stop'
$repository=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$output=Join-Path $repository ('Output/InstallerWithoutAppx-'+[Guid]::NewGuid().ToString('N'))
$ScriptRoot=Join-Path $output 'Install/Payload'
$savedLocal=$env:LOCALAPPDATA
$SkipGameBar=$false
$results=[Collections.Generic.List[object]]::new()
function Add-InstallResult {param($Status,$Item,$Detail);$results.Add([pscustomobject]@{Status=$Status;Item=$Item;Detail=$Detail})}
function Get-Service {throw 'MpsSvc does not exist on this test computer.'}
function Get-AppxPackage {throw 'AppX must not be required for main installation.'}
function Test-XboxGameBarAvailable {throw 'Game Bar is unavailable.'}
try {
    $env:LOCALAPPDATA=Join-Path $output 'Profile'
    $ordinary=Split-Path $ScriptRoot -Parent
    New-Item -ItemType Directory -Path $ScriptRoot,(Join-Path $ordinary 'KillConfirmService') -Force | Out-Null
    foreach($name in @('KillConfirmGameBar.exe','KillConfirmCompatibility.exe','KillConfirmService/cskillconfirm.exe','coreclr.dll','Microsoft.UI.Xaml.dll','vcruntime140.dll')) {Set-Content -LiteralPath (Join-Path $ordinary $name) -Value 'file fixture'}
    . (Join-Path $repository 'Installer/Scripts/Install/Desktop.ps1')
    Install-DesktopApplication
    if(Test-OptionalGameBarEnvironment) {throw 'Missing firewall service did not skip optional Game Bar.'}
    $registration=Get-Content -LiteralPath (Join-Path $env:LOCALAPPDATA 'KillConfirmOverlay/install-root.txt') -Raw
    if($registration -ne [IO.Path]::GetFullPath($ordinary)) {throw 'Ordinary installation was not registered.'}
    . (Join-Path $repository 'Installer/Scripts/CompatibilityDisplay/Install-CompatibilityDisplay.ps1')
    Initialize-CompatibilityDisplayFallback
    if($results.Count -ne 2 -or @($results | Where-Object Status -ne 'Success').Count) {throw 'Ordinary components failed without AppX.'}
    $SkipGameBar=$true
    if(Test-OptionalGameBarEnvironment) {throw 'Explicitly disabled widget still attempted AppX.'}
    Write-Host 'PASS: ordinary installation and compatibility availability with no firewall, Game Bar or AppX; optional widget can be skipped.'
}
finally {$env:LOCALAPPDATA=$savedLocal}

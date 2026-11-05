# Exercise the deployment request and its real worker without registering apps.
$ErrorActionPreference = 'Stop'
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$fixture = Join-Path $repository ('Output/WidgetDeploymentShutdown-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixture | Out-Null
$script:deploymentCalls = [Collections.Generic.List[object]]::new()
function Write-InstallLog { param($Message) }
function Add-AppxPackage {
    [CmdletBinding()]
    param([string]$Path, [switch]$ForceUpdateFromAnyVersion,
          [switch]$ForceTargetApplicationShutdown, [switch]$DeferRegistrationWhenPackagesAreInUse)
    $deploymentCalls.Add(@{} + $PSBoundParameters)
}
. (Join-Path $repository 'Installer/Scripts/Install/Appx.ps1')
function Invoke-AppxDeploymentWorker {
    param([hashtable]$Parameters)
    $requestPath = Join-Path $fixture 'request.json'
    $resultPath = Join-Path $fixture 'result.json'
    $Parameters | ConvertTo-Json | Set-Content -LiteralPath $requestPath -Encoding UTF8
    & (Join-Path $repository 'Installer/Scripts/Install/AppxDeploymentWorker.ps1') -RequestPath $requestPath -ResultPath $resultPath
    $result = Get-Content -LiteralPath $resultPath -Raw | ConvertFrom-Json
    if (!$result.Success) { throw $result.Message }
}
$script:AppxDeploymentTimedOut = $false
Add-AppxPackageCompat -PackagePath 'framework.appx'
Add-AppxPackageCompat -PackagePath 'widget.msixbundle' -ForceUpdate -CloseTargetApplications
Add-AppxPackageCompat -PackagePath 'widget.msixbundle' -ForceUpdate -CloseTargetApplications -DeferWhenInUse
Add-AppxPackageCompat -PackagePath 'legacy.msixbundle' -DeferWhenInUse
if ($script:deploymentCalls.Count -ne 4) { throw 'Deployment worker did not forward every request.' }
if ($script:deploymentCalls[0].ContainsKey('ForceTargetApplicationShutdown')) { throw 'Shared framework deployment attempted to close applications.' }
foreach ($call in @($script:deploymentCalls[1], $script:deploymentCalls[2])) {
    if (!$call.ForceTargetApplicationShutdown -or !$call.ForceUpdateFromAnyVersion -or $call.ContainsKey('DeferRegistrationWhenPackagesAreInUse')) {
        throw 'Widget update did not preserve target-only shutdown through the worker.'
    }
}
if (!$script:deploymentCalls[3].DeferRegistrationWhenPackagesAreInUse -or $script:deploymentCalls[3].ContainsKey('ForceTargetApplicationShutdown')) {
    throw 'Non-forcing deferred deployment changed behavior.'
}

# Simulate an older AppX command that supports deferral but cannot close only
# the target package. Capability detection must also protect the real worker.
function Add-AppxPackage {
    [CmdletBinding()]
    param([string]$Path, [switch]$ForceUpdateFromAnyVersion,
          [switch]$DeferRegistrationWhenPackagesAreInUse)
    $deploymentCalls.Add(@{} + $PSBoundParameters)
}
Add-AppxPackageCompat -PackagePath 'older-widget.msixbundle' -ForceUpdate -CloseTargetApplications -DeferWhenInUse
Add-AppxPackageCompat -PackagePath 'older-widget.msixbundle' -CloseTargetApplications
if ($script:deploymentCalls.Count -ne 6) { throw 'Older-command requests did not reach the deployment worker.' }
$deferredCall = $script:deploymentCalls[4]
if (!$deferredCall.ForceUpdateFromAnyVersion -or !$deferredCall.DeferRegistrationWhenPackagesAreInUse -or $deferredCall.ContainsKey('ForceTargetApplicationShutdown')) {
    throw 'A command without target shutdown did not fall back to explicitly requested deferral.'
}
$ordinaryCall = $script:deploymentCalls[5]
if ($ordinaryCall.Count -ne 2 -or !$ordinaryCall.ContainsKey('Path') -or !$ordinaryCall.ContainsKey('ErrorAction')) {
    throw 'Unsupported shutdown added optional parameters when deferral was not requested.'
}

# The oldest supported command shape accepts none of the optional switches.
function Add-AppxPackage {
    [CmdletBinding()]
    param([string]$Path)
    $deploymentCalls.Add(@{} + $PSBoundParameters)
}
Add-AppxPackageCompat -PackagePath 'oldest-widget.msixbundle' -ForceUpdate -CloseTargetApplications -DeferWhenInUse
if ($script:deploymentCalls.Count -ne 7) { throw 'Oldest-command request did not reach the deployment worker.' }
$oldestCall = $script:deploymentCalls[6]
if ($oldestCall.Count -ne 2 -or !$oldestCall.ContainsKey('Path') -or !$oldestCall.ContainsKey('ErrorAction')) {
    throw 'Unsupported optional switches reached the oldest deployment command.'
}
'PASS: widget updates close only the target package through the real request/worker path; shared frameworks never request shutdown; older commands defer only when requested and never receive unsupported switches.' | Tee-Object -FilePath (Join-Path $fixture 'pass.txt')

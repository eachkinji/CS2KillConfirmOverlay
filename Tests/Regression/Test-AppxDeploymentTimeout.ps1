#Requires -Version 7.0
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$fixture=Join-Path $root ('Output/DeploymentTimeout-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixture | Out-Null
$LogPath=Join-Path $fixture 'install.log'
$script:messages=[Collections.Generic.List[string]]::new()
function Write-InstallLog {param($Message); $script:messages.Add([string]$Message)}
. (Join-Path $root 'Installer/Scripts/Install/Appx.ps1')
$worker=Join-Path $fixture 'worker.ps1'
# Run real hidden Windows PowerShell workers, without deploying any packages.
$workerSource=@'
param($RequestPath,$ResultPath)
$request=Get-Content -LiteralPath $RequestPath -Raw|ConvertFrom-Json
$PID|Set-Content -LiteralPath $request.PidPath
switch($request.Mode){
 'success' { @{Success=$true}|ConvertTo-Json|Set-Content -LiteralPath $ResultPath }
 'error' { @{Success=$false;Message='simulated deployment error';Details='retained diagnostic';ActivityId='fixture'}|ConvertTo-Json|Set-Content -LiteralPath $ResultPath; exit 1 }
 'hang' { Start-Sleep -Seconds 60 }
}
'@
[IO.File]::WriteAllText($worker,$workerSource,[Text.UTF8Encoding]::new($true))
$pidFile=Join-Path $fixture 'worker.pid'
$script:AppxDeploymentTimedOut=$false
Invoke-AppxDeploymentWorker -Parameters @{Mode='success';PidPath=$pidFile} -TimeoutSeconds 5 -WorkerPath $worker
if($script:AppxDeploymentTimedOut){throw 'Successful deployment set timeout flag'}
$caught=$false
try {Invoke-AppxDeploymentWorker -Parameters @{Mode='error';PidPath=$pidFile} -TimeoutSeconds 5 -WorkerPath $worker}
catch {$caught=$_.Exception.Message -eq 'simulated deployment error'}
if(!$caught -or (Get-Content -LiteralPath $LogPath -Raw) -notmatch 'retained diagnostic'){throw 'Deployment errors lost their diagnostics'}
$caught=$false
# A nonexistent package exercises the real Windows PowerShell deployment worker
# and error serialization without registering or modifying an application.
try {Invoke-AppxDeploymentWorker -Parameters @{Path=(Join-Path $fixture 'nonexistent.msix')} -TimeoutSeconds 5}
catch {$caught=$_.Exception -is [InvalidOperationException]}
if(!$caught -or $script:AppxDeploymentTimedOut){throw 'Real worker failed to return a bounded package-path error'}
$clock=[Diagnostics.Stopwatch]::StartNew()
$caught=$false
try {Invoke-AppxDeploymentWorker -Parameters @{Mode='hang';PidPath=$pidFile} -TimeoutSeconds 2 -WorkerPath $worker}
catch {$caught=$_.Exception -is [TimeoutException]}
$workerPid=[int](Get-Content -LiteralPath $pidFile)
if(!$caught -or !$script:AppxDeploymentTimedOut -or $clock.Elapsed.TotalSeconds -gt 6 -or (Get-Process -Id $workerPid -ErrorAction SilentlyContinue)){throw 'Hung deployment did not return within deadline and stop its own worker'}
# Once the deployment service times out, never enqueue more components.
$caught=$false
try {Add-AppxPackageCompat -PackagePath 'must-not-be-submitted.msix'}
catch {$caught=$_.Exception -is [TimeoutException]}
if(!$caught){throw 'More deployment work was submitted after timeout'}
Write-Output 'PASS: success, retained deployment errors, bounded hung-worker cleanup, and no further deployment after timeout.'

param([Parameter(Mandatory)][string]$Payload)
$ErrorActionPreference='Stop'
$repository=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$output=Join-Path $repository ('Output/InstallerShutdown-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
$processes=@()
try {
    foreach($name in @('owned','unrelated')) {
        $root=Join-Path $output $name
        $service=Join-Path $root 'KillConfirmService'
        New-Item -ItemType Directory -Path $service -Force | Out-Null
        Copy-Item -LiteralPath (Join-Path ([IO.Path]::GetFullPath($Payload)) 'KillConfirmService/cskillconfirm.exe') -Destination $service
        $listener=[Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback,0)
        $listener.Start();$port=$listener.LocalEndpoint.Port;$listener.Stop()
        $start=[Diagnostics.ProcessStartInfo]::new((Join-Path $service 'cskillconfirm.exe'))
        $start.UseShellExecute=$false;$start.CreateNoWindow=$true;$start.WindowStyle='Hidden'
        $start.Environment['KILLCONFIRM_INSTALL_ROOT']=$root
        $start.Environment['KILLCONFIRM_DATA_ROOT']=Join-Path $root 'profile'
        $start.ArgumentList.Add('--port');$start.ArgumentList.Add($port.ToString())
        $processes += [Diagnostics.Process]::Start($start)
    }
    Start-Sleep -Milliseconds 1000
    if($processes[0].HasExited -or $processes[1].HasExited){throw 'Service fixtures did not stay running.'}
    & pwsh -NoProfile -File (Join-Path $repository 'Installer/Stop-RuntimeForUpdate.ps1') -InstallRoot (Join-Path $output 'owned') -Isolated
    if($LASTEXITCODE -ne 0){throw 'Installer shutdown script failed.'}
    if(!$processes[0].WaitForExit(3000)){throw 'Owned service survived installer preflight.'}
    $processes[1].Refresh()
    if($processes[1].HasExited){throw 'Unrelated service was stopped.'}
    [IO.File]::WriteAllText((Join-Path $output 'pass.txt'),'PASS: installer preflight stops a real running KillConfirm service; identically named service outside the installation remains running.')
    Get-Content -LiteralPath (Join-Path $output 'pass.txt')
    Write-Output "Artifacts: $output"
} finally {foreach($process in $processes){if(!$process.HasExited){$process.Kill();$process.WaitForExit()};$process.Dispose()}}

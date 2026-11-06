param([Parameter(Mandatory)][string]$Payload)
$ErrorActionPreference='Stop'
$repository=Split-Path $PSScriptRoot -Parent
$Payload=[IO.Path]::GetFullPath($Payload)
$output=Join-Path $repository ('Output/CloseValidation-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
$results=[Collections.Generic.List[object]]::new()

function Start-CloseCase([string]$Name,[string]$Mode,[string]$Action) {
    $profile=Join-Path $output $Name
    New-Item -ItemType Directory -Path $profile -Force | Out-Null
    $listener=[Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback,0)
    $listener.Start(); $port=$listener.LocalEndpoint.Port; $listener.Stop()
    $settings=@{'LocalService.Port'=@{Kind='int';Text=$port.ToString()}}
    [IO.File]::WriteAllText((Join-Path $profile 'settings.json'),($settings | ConvertTo-Json -Compress))
    $start=[Diagnostics.ProcessStartInfo]::new((Join-Path $Payload 'KillConfirmControlPanel.exe'))
    $start.UseShellExecute=$false; $start.WindowStyle='Hidden'; $start.CreateNoWindow=$true
    $start.Environment['KILLCONFIRM_TEST_WIDGET_PID']=$PID.ToString()
    $start.Environment['KILLCONFIRM_DATA_ROOT']=$profile
    $start.Environment['KILLCONFIRM_INSTALL_ROOT']=$Payload
    $start.Environment['KILLCONFIRM_UI_VALIDATION']='0'
    $start.Environment['KILLCONFIRM_CLOSE_VALIDATION']=$Mode
    $start.Environment['KILLCONFIRM_CLOSE_ACTION']=$Action
    $process=[Diagnostics.Process]::Start($start)
    try {
        $deadline=[DateTime]::UtcNow.AddSeconds(45)
        $readyPath=Join-Path $profile 'close-ready.json'
        $failure=Join-Path $profile 'close-failure.txt'
        do {
            if(Test-Path -LiteralPath $failure) {throw (Get-Content -LiteralPath $failure -Raw)}
            if(Test-Path -LiteralPath $readyPath) {
                # The panel can exit immediately after publishing this small file.
                try {$ready=Get-Content -LiteralPath $readyPath -Raw | ConvertFrom-Json} catch {$ready=$null}
                if($ready) {return [pscustomobject]@{Name=$Name;Mode=$Mode;Action=$Action;Profile=$profile;Process=$process;Ready=$ready}}
            }
            if($process.HasExited) {throw "$Name exited before lifecycle validation: $profile"}
            Start-Sleep -Milliseconds 100
        } while([DateTime]::UtcNow -lt $deadline)
        throw "$Name did not become ready: $profile"
    } catch {
        # Only this exact Process object was launched by the case. Its owned
        # children observe its death even if startup failed before the PID report.
        if(!$process.HasExited) {$process.Kill(); $null=$process.WaitForExit(5000)}
        $process.Dispose()
        throw
    }
}

function Get-RecordedProcess([int]$RecordedPid,[long]$StartTicks) {
    if($RecordedPid -le 0 -or $RecordedPid -eq $PID) {return $null}
    $candidate=$null
    try {
        $candidate=[Diagnostics.Process]::GetProcessById($RecordedPid)
        $prefix=$Payload.TrimEnd('\','/')+[IO.Path]::DirectorySeparatorChar
        if(!$candidate.HasExited -and $candidate.StartTime.ToUniversalTime().Ticks -eq $StartTicks -and
            $candidate.MainModule.FileName.StartsWith($prefix,[StringComparison]::OrdinalIgnoreCase)) {return $candidate}
    } catch {}
    if($candidate) {$candidate.Dispose()}
    return $null
}

function Test-RecordedAlive([int]$RecordedPid,[long]$StartTicks) {
    $candidate=Get-RecordedProcess $RecordedPid $StartTicks
    if(!$candidate) {return $false}
    $candidate.Dispose()
    return $true
}

function Stop-CloseCase($Case) {
    if(!$Case) {return}
    foreach($role in @('Panel','Renderer','Service')) {
        $candidate=Get-RecordedProcess $Case.Ready.($role+'Pid') $Case.Ready.($role+'StartTicks')
        if(!$candidate) {continue}
        try {if(!$candidate.HasExited) {$candidate.Kill(); $null=$candidate.WaitForExit(5000)}} finally {$candidate.Dispose()}
    }
    $Case.Process.Dispose()
}

$guard=$null
try {
    # A second profile intentionally shares the exact same installation. A
    # directory-wide process sweep would kill it and fail this regression.
    $guard=Start-CloseCase 'other-profile-guard' 'gamebar' 'hold'
    foreach($definition in @(
        @('compatibility-normal','compatibility','normal'),
        @('compatibility-kill','compatibility','kill'),
        @('compatibility-failfast','compatibility','failfast'),
        @('gamebar-normal','gamebar','normal'),
        @('gamebar-kill','gamebar','kill')
    )) {
        $case=$null
        try {
            $case=Start-CloseCase $definition[0] $definition[1] $definition[2]
            if($case.Mode -eq 'compatibility' -and !$case.Ready.RendererRegistered) {throw "$($case.Name) did not prove renderer service registration before closing."}
            if($case.Action -eq 'kill') {$case.Process.Kill()}
            if(!$case.Process.WaitForExit(30000)) {throw "$($case.Name) panel did not exit."}
            if($case.Mode -eq 'gamebar') {
                Start-Sleep -Milliseconds 800
                if(!(Test-RecordedAlive $case.Ready.ServicePid $case.Ready.ServiceStartTicks)) {throw "$($case.Name) terminated the service with a live widget peer."}
            } else {
                $deadline=[DateTime]::UtcNow.AddSeconds(15)
                do {
                    $rendererAlive=Test-RecordedAlive $case.Ready.RendererPid $case.Ready.RendererStartTicks
                    $serviceAlive=Test-RecordedAlive $case.Ready.ServicePid $case.Ready.ServiceStartTicks
                    if(!$rendererAlive -and !$serviceAlive) {break}
                    Start-Sleep -Milliseconds 100
                } while([DateTime]::UtcNow -lt $deadline)
                if($rendererAlive -or $serviceAlive) {throw "$($case.Name) left owned processes: renderer=$rendererAlive service=$serviceAlive"}
                $configuration=Get-Content -LiteralPath (Join-Path $case.Profile 'CompatibilityDisplay/display.json') -Raw | ConvertFrom-Json
                if(!$configuration.Enabled) {throw "$($case.Name) changed the saved compatibility mode."}
            }
            if(!(Test-RecordedAlive $guard.Ready.PanelPid $guard.Ready.PanelStartTicks) -or
                !(Test-RecordedAlive $guard.Ready.ServicePid $guard.Ready.ServiceStartTicks)) {throw "$($case.Name) terminated the other profile sharing this installation."}
            $results.Add([pscustomobject]@{Case=$case.Name;Pass=$true;Profile=$case.Profile;Pids=$case.Ready})
            Write-Output "PASS: $($case.Name); other profile remained alive."
        } finally {Stop-CloseCase $case}
    }
    [IO.File]::WriteAllText((Join-Path $output 'results.json'),($results | ConvertTo-Json -Depth 6))
} finally {Stop-CloseCase $guard}
Write-Output "Artifacts: $output"

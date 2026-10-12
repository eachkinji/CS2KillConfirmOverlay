param([Parameter(Mandatory)][string]$Payload)
$ErrorActionPreference='Stop'
$repository=Split-Path $PSScriptRoot -Parent
$Payload=[IO.Path]::GetFullPath($Payload)
$output=Join-Path $repository ('Output/CloseValidation-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
foreach($mode in @('compatibility','gamebar')) {
    $profile=Join-Path $output $mode
    New-Item -ItemType Directory -Path $profile -Force | Out-Null
    $listener=[Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback,0)
    $listener.Start(); $port=$listener.LocalEndpoint.Port; $listener.Stop()
    $settings=@{'LocalService.Port'=@{Kind='int';Text=$port.ToString()}}
    [IO.File]::WriteAllText((Join-Path $profile 'settings.json'),($settings | ConvertTo-Json -Compress))
    $start=[Diagnostics.ProcessStartInfo]::new((Join-Path $Payload 'KillConfirmGameBar.exe'))
    $start.UseShellExecute=$false
    $start.Environment['KILLCONFIRM_DATA_ROOT']=$profile
    $start.Environment['KILLCONFIRM_INSTALL_ROOT']=$Payload
    $start.Environment['KILLCONFIRM_CLOSE_VALIDATION']=$mode
    $process=[Diagnostics.Process]::Start($start)
    if(!$process.WaitForExit(45000)) {$process.Kill();throw "$mode close timed out: $profile"}
    $failure=Join-Path $profile 'close-failure.txt'
    if(Test-Path -LiteralPath $failure) {throw (Get-Content -LiteralPath $failure -Raw)}
    $readyPath=Join-Path $profile 'close-ready.json'
    if(!(Test-Path -LiteralPath $readyPath)) {throw "$mode close did not run: $profile"}
    $ready=Get-Content -LiteralPath $readyPath -Raw | ConvertFrom-Json
    $deadline=[DateTime]::UtcNow.AddSeconds(15)
    do {
        # Wait for the exit helper too, before starting the next case. Its
        # remaining sweep must not terminate the next case's newly started core.
        $prefix=$Payload.TrimEnd('\','/')+[IO.Path]::DirectorySeparatorChar
        $remaining=Get-CimInstance Win32_Process | Where-Object {$_.ExecutablePath -and $_.ExecutablePath.StartsWith($prefix,[StringComparison]::OrdinalIgnoreCase)}
        if(!$remaining) {break}
        Start-Sleep -Milliseconds 200
    } while([DateTime]::UtcNow -lt $deadline)
    if($remaining) {throw "$mode left runtime processes behind: $($remaining.ProcessId -join ',')"}
    if($mode -eq 'gamebar' -and !(Test-Path (Join-Path $profile 'close-hidden-pass.txt'))) {throw 'Game Bar background check did not pass.'}
    if($mode -eq 'compatibility') {
        $configuration=Get-Content (Join-Path $profile 'CompatibilityDisplay/display.json') -Raw | ConvertFrom-Json
        if(!$configuration.Enabled) {throw 'Closing changed the saved compatibility mode.'}
    }
    Write-Output "PASS: $mode real window-close lifecycle; all expected processes stopped after full exit."
}
Write-Output "Artifacts: $output"

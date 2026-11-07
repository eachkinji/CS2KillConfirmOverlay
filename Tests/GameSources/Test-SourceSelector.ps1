param([string]$Payload = '')
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if (!$Payload) { $Payload = Join-Path $repo 'ControlPanel/bin/x64/Debug/net8.0-windows10.0.19041.0/win-x64' }
$profile = Join-Path $repo ('Output/NavigationRedesign/source-ui-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $profile -Force | Out-Null
# Exercise the real selector against an isolated service. Never bind the game's port 2999.
$reservation = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 0)
$reservation.Start()
$port = $reservation.LocalEndpoint.Port
$reservation.Stop()
[IO.File]::WriteAllText((Join-Path $profile 'settings.json'), (@{'LocalService.Port'=@{Kind='int';Text="$port"}} | ConvertTo-Json -Depth 3))
$serviceStart = [Diagnostics.ProcessStartInfo]::new((Join-Path $repo 'KillConfirmService/target/debug/cskillconfirm.exe'))
$serviceStart.UseShellExecute = $false
$serviceStart.CreateNoWindow = $true
$serviceStart.WorkingDirectory = Join-Path $repo 'Widget/KillConfirmService'
foreach ($argument in @('--port', "$port", '--volume', '0')) { $serviceStart.ArgumentList.Add($argument) }
$serviceStart.Environment['KILLCONFIRM_DATA_ROOT'] = $profile
$service = [Diagnostics.Process]::Start($serviceStart)
$panel = $null
try {
    $deadline = [DateTimeOffset]::UtcNow.AddSeconds(20)
    $ready = $false
    while ([DateTimeOffset]::UtcNow -lt $deadline) {
        if ($service.HasExited) { throw 'Isolated service exited.' }
        try {
            $token = [IO.File]::ReadAllText((Join-Path $profile 'service-auth-token.txt')).Trim()
            $null = Invoke-RestMethod "http://127.0.0.1:$port/health" -Headers @{'x-killconfirm-token'=$token} -TimeoutSec 1
            $ready = $true
            break
        } catch { Start-Sleep -Milliseconds 200 }
    }
    if (!$ready) { throw 'Isolated service did not become ready.' }
    $start = [Diagnostics.ProcessStartInfo]::new((Join-Path $Payload 'KillConfirmControlPanel.exe'))
    $start.UseShellExecute = $false
    $start.WindowStyle = 'Hidden'
    $start.Environment['KILLCONFIRM_DATA_ROOT'] = $profile
    $start.Environment['KILLCONFIRM_INSTALL_ROOT'] = $Payload
    $start.Environment['KILLCONFIRM_UI_VALIDATION'] = '1'
    $start.Environment['KILLCONFIRM_GAME_SOURCE_VALIDATION'] = '1'
    $panel = [Diagnostics.Process]::Start($start)
    Write-Output "Source selector validation profile: $profile"
    $deadline = [DateTimeOffset]::UtcNow.AddSeconds(90)
    while (!$panel.WaitForExit(1000) -and [DateTimeOffset]::UtcNow -lt $deadline) { }
    if (!$panel.HasExited) { throw 'Source selector validation timed out.' }
    $failure = Join-Path $profile 'source-ui-failure.txt'
    if (Test-Path -LiteralPath $failure) { throw [IO.File]::ReadAllText($failure) }
    $pass = Join-Path $profile 'source-ui-pass.txt'
    if (!(Test-Path -LiteralPath $pass)) { throw 'Source selector exited without a pass report.' }
    Get-Content -LiteralPath $pass
} finally {
    foreach ($ownedProcess in @($panel, $service)) {
        if ($ownedProcess -and !$ownedProcess.HasExited) { $ownedProcess.Kill(); $ownedProcess.WaitForExit(5000) | Out-Null }
    }
}

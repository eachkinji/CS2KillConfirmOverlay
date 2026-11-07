param([string]$Payload = '')
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if (!$Payload) { $Payload = Join-Path $repo 'ControlPanel/bin/x64/Debug/net8.0-windows10.0.19041.0/win-x64' }
$profile = Join-Path $repo ('Output/NavigationRedesign/validation-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $profile -Force | Out-Null
# Isolated preferences and a non-service port prevent changes to the running installation.
[IO.File]::WriteAllText((Join-Path $profile 'settings.json'), '{"LocalService.Port":{"Kind":"int","Text":"59997"}}')
$start = [Diagnostics.ProcessStartInfo]::new((Join-Path $Payload 'KillConfirmControlPanel.exe'))
$start.UseShellExecute = $false
$start.WindowStyle = 'Hidden'
$start.Environment['KILLCONFIRM_DATA_ROOT'] = $profile
$start.Environment['KILLCONFIRM_INSTALL_ROOT'] = $Payload
$start.Environment['KILLCONFIRM_UI_VALIDATION'] = '1'
$start.Environment['KILLCONFIRM_SIDEBAR_VALIDATION'] = '1'
$process = [Diagnostics.Process]::Start($start)
Write-Output "Navigation validation profile: $profile"
try {
    $deadline = [DateTimeOffset]::UtcNow.AddSeconds(90)
    $failure = Join-Path $profile 'ui-failure.txt'
    while (!$process.WaitForExit(1000) -and [DateTimeOffset]::UtcNow -lt $deadline) {
        if (Test-Path -LiteralPath $failure) { throw [IO.File]::ReadAllText($failure) }
    }
    if (Test-Path -LiteralPath $failure) { throw [IO.File]::ReadAllText($failure) }
    if (!$process.HasExited) { throw 'Navigation validation timed out.' }
    $pass = Join-Path $profile 'ui-pass.txt'
    if (!(Test-Path -LiteralPath $pass)) { throw 'Navigation validation exited without a pass report.' }
    Get-Content -LiteralPath $pass
} finally {
    if (!$process.HasExited) { $process.Kill(); $process.WaitForExit(5000) | Out-Null }
}

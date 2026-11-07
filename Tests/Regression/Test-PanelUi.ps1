param(
    [string]$Payload = '',
    [ValidateSet('Full', 'Settings', 'Navigation', 'AudioChannels')]
    [string[]]$Checks = @('Full', 'Settings', 'AudioChannels'),
    [ValidateSet('zh-CN', 'en-US')][string]$Language = 'zh-CN'
)
$ErrorActionPreference = 'Stop'
$taskRepo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if (!$Payload) { $Payload = Join-Path $taskRepo 'ControlPanel/bin/x64/Debug/net8.0-windows10.0.19041.0/win-x64' }
$taskOutput = Join-Path $taskRepo ('Output/UIStandardization/validation-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff') + '-' + $Language)
$taskFlags = @{ Full = ''; Settings = 'KILLCONFIRM_SETTINGS_VALIDATION'; Navigation = 'KILLCONFIRM_SIDEBAR_VALIDATION'; AudioChannels = 'KILLCONFIRM_AUDIO_CHANNEL_VALIDATION' }
foreach ($taskCheck in $Checks) {
    $taskProfile = Join-Path $taskOutput $taskCheck
    New-Item -ItemType Directory -Path $taskProfile -Force | Out-Null
    $taskSettings = @{
        'LocalService.Port' = @{ Kind = 'int'; Text = '59997' }
        'UiLanguage' = @{ Kind = 'string'; Text = $Language }
    }
    [IO.File]::WriteAllText((Join-Path $taskProfile 'settings.json'), ($taskSettings | ConvertTo-Json -Depth 4))
    $taskStart = [Diagnostics.ProcessStartInfo]::new((Join-Path $Payload 'KillConfirmControlPanel.exe'))
    $taskStart.UseShellExecute = $false
    $taskStart.WindowStyle = 'Hidden'
    foreach ($taskKey in @($taskStart.Environment.Keys | Where-Object { $_ -like 'KILLCONFIRM_*' })) { $taskStart.Environment.Remove($taskKey) | Out-Null }
    $taskStart.Environment['KILLCONFIRM_DATA_ROOT'] = $taskProfile
    $taskStart.Environment['KILLCONFIRM_INSTALL_ROOT'] = [IO.Path]::GetFullPath($Payload)
    $taskStart.Environment['KILLCONFIRM_UI_VALIDATION'] = '1'
    if ($taskFlags[$taskCheck]) { $taskStart.Environment[$taskFlags[$taskCheck]] = '1' }
    $taskProcess = [Diagnostics.Process]::Start($taskStart)
    Write-Output "$taskCheck ($Language): $taskProfile"
    try {
        $taskDeadline = [DateTimeOffset]::UtcNow.AddSeconds(125)
        while (!$taskProcess.WaitForExit(1000) -and [DateTimeOffset]::UtcNow -lt $taskDeadline) { }
        if (!$taskProcess.HasExited) { throw "$taskCheck timed out: $taskProfile" }
        $taskFailure = Join-Path $taskProfile 'ui-failure.txt'
        if (Test-Path -LiteralPath $taskFailure) { throw [IO.File]::ReadAllText($taskFailure) }
        $taskPass = Join-Path $taskProfile 'ui-pass.txt'
        if (!(Test-Path -LiteralPath $taskPass)) { throw "$taskCheck did not report success; inspect $taskProfile/control-panel.log" }
        Get-Content -LiteralPath $taskPass
    } finally {
        if (!$taskProcess.HasExited) { $taskProcess.Kill(); $taskProcess.WaitForExit(5000) | Out-Null }
        $taskProcess.Dispose()
    }
}
Write-Output "UI validation artifacts: $taskOutput"

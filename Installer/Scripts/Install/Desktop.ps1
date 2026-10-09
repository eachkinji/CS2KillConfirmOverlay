# Main program registration uses ordinary files and shortcuts, never AppX.
function Install-DesktopApplication {
    $ordinaryRoot = Split-Path -Parent $ScriptRoot
    foreach ($relative in @('KillConfirmGameBar.exe','KillConfirmCompatibility.exe','KillConfirmService/cskillconfirm.exe','coreclr.dll','Microsoft.UI.Xaml.dll','vcruntime140.dll')) {
        if (-not (Test-Path -LiteralPath (Join-Path $ordinaryRoot $relative) -PathType Leaf)) { throw "普通主程序文件缺失：$relative" }
    }
    $registration = Join-Path $env:LOCALAPPDATA 'KillConfirmOverlay'
    New-Item -ItemType Directory -Path $registration -Force | Out-Null
    [IO.File]::WriteAllText((Join-Path $registration 'install-root.txt'), [IO.Path]::GetFullPath($ordinaryRoot), [Text.UTF8Encoding]::new($false))
    Add-InstallResult -Status Success -Item '控制面板、后台与兼容显示' -Detail '普通 EXE 已安装，不需要 MSIX、Game Bar 或 Windows 防火墙服务'
}
function Test-OptionalGameBarEnvironment {
    if ($SkipGameBar) { return $false }
    try {
        $firewall = Get-Service -Name MpsSvc -ErrorAction Stop
        if ($firewall.Status -ne 'Running') { return $false }
        return (Test-XboxGameBarAvailable) -or ($InstallPrerequisites -and $SkippedPrerequisites -notcontains "gamebar")
    }
    catch { return $false }
}

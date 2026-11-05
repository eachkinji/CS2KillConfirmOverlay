# Main program registration uses ordinary files and shortcuts, never AppX.
function Install-DesktopApplication {
    $ordinaryRoot = Split-Path -Parent $ScriptRoot
    foreach ($relative in @('KillConfirmControlPanel.exe','KillConfirmCompatibility.exe','KillConfirmService/cskillconfirm.exe','coreclr.dll','Microsoft.UI.Xaml.dll','vcruntime140.dll')) {
        if (-not (Test-Path -LiteralPath (Join-Path $ordinaryRoot $relative) -PathType Leaf)) { throw "普通主程序文件缺失：$relative" }
    }
    $registration = Join-Path $env:LOCALAPPDATA 'KillConfirmOverlay'
    New-Item -ItemType Directory -Path $registration -Force | Out-Null
    [IO.File]::WriteAllText((Join-Path $registration 'install-root.txt'), [IO.Path]::GetFullPath($ordinaryRoot), [Text.UTF8Encoding]::new($false))
    Add-InstallResult -Status Success -Item '控制面板、后台与兼容显示' -Detail '普通 EXE 已安装，不需要 MSIX、Game Bar 或 Windows 防火墙服务'
}
function Initialize-MaterialLibrary {
    $ordinaryRoot=Split-Path -Parent $ScriptRoot
    $registration=Join-Path $env:LOCALAPPDATA 'KillConfirmOverlay'
    if ([string]::IsNullOrWhiteSpace($MaterialRoot)) {
        $existing=Join-Path $registration 'UserData/resource-library.txt'
        $MaterialRoot=if(Test-Path -LiteralPath $existing){[IO.File]::ReadAllText($existing).Trim()}else{Join-Path ([IO.Path]::GetPathRoot($ordinaryRoot)) 'KillConfirm素材包'}
    }
    Write-InstallLog "素材包存放位置：$MaterialRoot"
    $start=New-Object Diagnostics.ProcessStartInfo
    $start.FileName=Join-Path $ordinaryRoot 'KillConfirmControlPanel.exe'
    $start.UseShellExecute=$false
    $start.CreateNoWindow=$true
    $start.WindowStyle='Hidden'
    $start.Arguments='--set-material-root "'+$MaterialRoot.TrimEnd('\')+'"'
    $process=[Diagnostics.Process]::Start($start)
    $process.WaitForExit()
    if($process.ExitCode -ne 0){throw '素材迁移失败，旧素材已保留。请查看 control-panel.log 后重新安装或在高级设置中选择空目录。'}
    $owner=[Security.Principal.WindowsIdentity]::GetCurrent().User.Value
    & icacls.exe $MaterialRoot /grant ('*'+$owner+':(OI)(CI)M') /Q | Out-Null
    if($LASTEXITCODE -ne 0){throw '素材目录写入权限设置失败，请选择当前用户可写的目录。'}
    Add-InstallResult -Status Success -Item '素材包存放位置'  -Detail $MaterialRoot
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

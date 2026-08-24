param(
    [switch]$SkipLoopback = $false,
    [switch]$SkipGsiConfig = $false,
    [switch]$OpenGameBar = $false,
    [switch]$InstallPrerequisites = $false
)

$ErrorActionPreference = "Stop"

$ScriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$OverlayRoot = Join-Path $ScriptRoot "OverlayPackage"
$PrerequisiteRoot = Join-Path $ScriptRoot "Prerequisites"
$PackageName = "KillConfirmGameBar.Overlay"
$PackageFamilyName = $null
$LogPath = Join-Path $env:TEMP "KillConfirmGameBar_Install.log"
$ResultPath = Join-Path $env:TEMP "KillConfirmGameBar_Install_Result.txt"
$StatusPath = Join-Path $env:TEMP "KillConfirmGameBar_Install_Status.ini"
$RuntimeLogRoot = $null
$InstallResults = New-Object System.Collections.Generic.List[object]

# Installer features are dot-sourced so they share this entry script's state.
$InstallModuleRoot = Join-Path $ScriptRoot "Scripts\Install"
$InstallModules = @(
    "Common.ps1",
    "Appx.ps1",
    "Prerequisites.ps1",
    "GameBar.ps1",
    "Overlay.ps1",
    "Cs2.ps1"
)
foreach ($moduleName in $InstallModules) {
    $modulePath = Join-Path $InstallModuleRoot $moduleName
    if (-not (Test-Path -LiteralPath $modulePath -PathType Leaf)) {
        throw "安装器模块缺失：$modulePath"
    }
    . $modulePath
}

try {
    try {
        if (Test-Path $LogPath) {
            Remove-Item -LiteralPath $LogPath -Force
        }
        foreach ($staleResultPath in @($ResultPath, $StatusPath)) {
            if (Test-Path -LiteralPath $staleResultPath) {
                Remove-Item -LiteralPath $staleResultPath -Force
            }
        }
    }
    catch {
        # Continue even if an older log is locked. Add-Content will report any later write problem.
    }

    if ($InstallPrerequisites) {
        try {
            Install-RequiredComponents
        }
        catch {
            Add-InstallResult -Status Error -Item "外部前置依赖检查" -Detail ((Get-ErrorReason $_) + "；已继续安装 Game Bar 检查、主程序和后续配置")
        }
    }
    else {
        Write-InstallLog "Dependency-free installer selected. Prerequisite detection and installation are disabled."
        Add-InstallResult -Status Success -Item "外部前置依赖" -Detail "无依赖更新版按设计不检查、不安装离线依赖"
    }

    try {
        Confirm-XboxGameBarAvailable
        Add-InstallResult -Status Success -Item "Xbox Game Bar 可用性" -Detail "已检测到 Xbox Game Bar"
    }
    catch {
        Add-InstallResult -Status Error -Item "Xbox Game Bar 可用性" -Detail ((Get-ErrorReason $_) + "；已打开商店页面，并继续尝试安装主程序")
    }

    try {
        Repair-XboxGameBarEnvironment
    }
    catch {
        Add-InstallResult -Status Error -Item "Game Bar 环境修复" -Detail ((Get-ErrorReason $_) + "；已继续安装 Kill Confirm 主程序")
    }

    try {
        Install-OverlayPackage
        Test-OverlayPackageInstalled
    }
    catch {
        $mainAlreadyReported = @($InstallResults | Where-Object { $_.Item -eq "Kill Confirm Overlay 主程序" -and $_.Status -eq "Error" }).Count -gt 0
        if (-not $mainAlreadyReported) {
            Add-InstallResult -Status Error -Item "Kill Confirm Overlay 主程序" -Detail ((Get-ErrorReason $_) + "；后续 CFG 和回环配置仍会继续执行")
        }
    }

    if (-not $SkipGsiConfig) {
        try {
            Install-Cs2GsiConfig
        }
        catch {
            Add-InstallResult -Status Error -Item "CS2 GSI 配置" -Detail ((Get-ErrorReason $_) + "；不影响继续处理回环配置")
        }
    }
    else {
        Add-InstallResult -Status Warning -Item "CS2 GSI 配置" -Detail "已通过命令行参数跳过"
    }

    if (-not $SkipLoopback) {
        try {
            if (-not $PackageFamilyName) {
                Update-InstalledPackageContext | Out-Null
            }
            Write-InstallLog "Adding loopback exemption for $PackageFamilyName..."
            & CheckNetIsolation.exe LoopbackExempt -a "-n=$PackageFamilyName"
            if ($LASTEXITCODE -ne 0) {
                throw "CheckNetIsolation 返回退出码 $LASTEXITCODE"
            }
            Add-InstallResult -Status Success -Item "本机回环通信权限" -Detail "Widget 可以访问 127.0.0.1 上的伴随服务"
        }
        catch {
            Add-InstallResult -Status Error -Item "本机回环通信权限" -Detail (Get-ErrorReason $_)
        }
    }
    else {
        Add-InstallResult -Status Warning -Item "本机回环通信权限" -Detail "已通过命令行参数跳过"
    }

    if ($OpenGameBar) {
        try {
            Start-Sleep -Milliseconds 800
            Start-Process "ms-gamebar:" | Out-Null
            Add-InstallResult -Status Success -Item "打开 Xbox Game Bar" -Detail "已发送启动请求"
        }
        catch {
            Add-InstallResult -Status Warning -Item "打开 Xbox Game Bar" -Detail (Get-ErrorReason $_)
        }
    }

    Write-InstallLog "Kill Confirm installation pass finished."
}
catch {
    Add-InstallResult -Status Error -Item "安装器未预期错误" -Detail (Get-ErrorReason $_)
}
finally {
    try {
        if ($InstallResults.Count -eq 0) {
            Add-InstallResult -Status Error -Item "安装器" -Detail "没有生成任何安装结果，请查看详细日志"
        }
        Show-InstallSummary
    }
    catch {
        Write-InstallLog "Failed to show the installation summary: $($_.Exception.Message)"
        Write-Host "安装流程已经执行完毕，但诊断窗口显示失败。完整日志：$LogPath"
    }
}

# Every installation stage reports its own Success/Warning/Error result and is
# intentionally non-blocking. Do not leak a stale native-command exit code
# (reg.exe, gpresult.exe, CheckNetIsolation.exe) back to the Inno Setup host.
$global:LASTEXITCODE = 0
exit 0

param(
    [switch]$SkipLoopback = $false,
    [switch]$SkipGameBar = $false,
    [switch]$SkipGsiConfig = $false,
    [switch]$OpenGameBar = $false,
    [switch]$InstallPrerequisites = $false,
    [switch]$PrerequisitesConfirmed = $false,
    [string]$InstallerVariant = "Unknown",
    [string]$InstallerVersion = "Unknown",
    [string]$InstallerBuildTimeUtc = "Unknown",
    [string]$InstallerSourceCommit = "Unknown",
    [string]$InstallerSourcePath = ""
)

$ErrorActionPreference = "Stop"
# Inno Setup decodes redirected stdout/stderr as UTF-8, including Chinese and
# result symbols. Suppress console progress records; stages are logged below.
$ProgressPreference = "SilentlyContinue"
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)

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
$InstallSessionId = [guid]::NewGuid().ToString("D")
$InstallStartedAt = Get-Date
$InstallStartedAtUtc = $InstallStartedAt.ToUniversalTime()
$InstallerSourceFileName = if ([string]::IsNullOrWhiteSpace($InstallerSourcePath)) {
    "Unknown"
}
else {
    Split-Path -Leaf $InstallerSourcePath
}
$DeclaredInstallerVariant = switch ($InstallerVariant.Trim().ToLowerInvariant()) {
    "withdependencies" { "WithDependencies" }
    "nodependencies" { "NoDependencies" }
    default { "Unknown" }
}
$EffectiveInstallerVariant = if ($InstallPrerequisites) { "WithDependencies" } else { "NoDependencies" }
$DeclaredInstallerVariantDisplayName = switch ($DeclaredInstallerVariant) {
    "WithDependencies" { "有依赖版（新人用）" }
    "NoDependencies" { "无依赖版（更新用）" }
    default { "未知/未声明" }
}
$EffectiveInstallerVariantDisplayName = if ($InstallPrerequisites) {
    "有依赖模式（检查并按需安装离线依赖）"
}
else {
    "无依赖模式（跳过离线依赖）"
}
$InstallerVariantKnown = $DeclaredInstallerVariant -ne "Unknown"
$InstallerModeMatches = -not $InstallerVariantKnown -or $DeclaredInstallerVariant -eq $EffectiveInstallerVariant
$InstallerModeConsistency = if (-not $InstallerVariantKnown) {
    "无法校验（安装包未声明类型）"
}
elseif ($InstallerModeMatches) {
    "一致"
}
else {
    "不一致：安装包类型与实际执行参数冲突"
}
$InstallMetadataLines = @(
    "安装包类型：$DeclaredInstallerVariantDisplayName [$DeclaredInstallerVariant]",
    "实际执行模式：$EffectiveInstallerVariantDisplayName [$EffectiveInstallerVariant]",
    "依赖安装已由外层安装器确认：$PrerequisitesConfirmed",
    "模式一致性：$InstallerModeConsistency",
    "应用版本：$InstallerVersion",
    "安装包文件名：$InstallerSourceFileName",
    "安装包完整路径：$(if ([string]::IsNullOrWhiteSpace($InstallerSourcePath)) { 'Unknown' } else { $InstallerSourcePath })",
    "源码提交：$InstallerSourceCommit",
    "安装器构建时间（UTC）：$InstallerBuildTimeUtc",
    "安装开始时间（本地）：$($InstallStartedAt.ToString('yyyy-MM-dd HH:mm:ss zzz'))",
    "安装开始时间（UTC）：$($InstallStartedAtUtc.ToString('yyyy-MM-ddTHH:mm:ss.fffZ'))",
    "安装会话 ID：$InstallSessionId",
    "计算机/用户：$env:COMPUTERNAME / $env:USERDOMAIN\$env:USERNAME",
    "操作系统：$([Environment]::OSVersion.VersionString)；架构：$env:PROCESSOR_ARCHITECTURE",
    "PowerShell：$($PSVersionTable.PSVersion)；进程位数：$([IntPtr]::Size * 8)-bit"
)

# Installer features are dot-sourced so they share this entry script's state.
$InstallModuleRoot = Join-Path $ScriptRoot "Scripts\Install"
$InstallModules = @(
    "Common.ps1",
    "Desktop.ps1",
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
    $Host.UI.RawUI.WindowTitle = "Kill Confirm Overlay - 安装进度"
}
catch {
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

    Initialize-InstallLogHeader
    if ($InstallerVariantKnown -and -not $InstallerModeMatches) {
        Add-InstallResult `
            -Status Warning `
            -Item "安装包类型与执行模式" `
            -Detail "声明为 $DeclaredInstallerVariant，实际执行 $EffectiveInstallerVariant；请保留本日志并检查打包参数"
    }

    Write-InstallStage -Number 1 -Total 7 -Name "普通主程序" -Detail "控制面板、后台与兼容显示"
    Install-DesktopApplication
    $compatibilityFallback = -not (Test-OptionalGameBarEnvironment)
    Write-InstallStage -Number 2 -Total 7 -Name "显示方式检测" -Detail "Game Bar 为可选组件"
    if ($compatibilityFallback) {
        Add-InstallResult -Status Warning -Item "可选 Game Bar" -Detail "已跳过 Game Bar 小组件；控制面板与兼容显示已正常安装（Game Bar 或防火墙服务不可用时会自动跳过）"
    }
    Write-InstallStage -Number 3 -Total 7 -Name "Game Bar 前置依赖" -Detail "兼容显示不需要此项"
    if ($InstallPrerequisites -and -not $compatibilityFallback) {
        try { Install-RequiredComponents -Confirmed:$PrerequisitesConfirmed }
        catch { Add-InstallResult -Status Warning -Item "Game Bar 前置依赖" -Detail ((Get-ErrorReason $_) + "；兼容显示可直接使用") }
    }
    Write-InstallStage -Number 4 -Total 7 -Name "可选 Game Bar 小组件" -Detail "独立 MSIX；失败不影响普通主程序"
    if (-not $compatibilityFallback) {
        try { Install-OverlayPackage; Test-OverlayPackageInstalled }
        catch {
            $compatibilityFallback = $true
            # The optional installer retains its detailed diagnostics; classify them
            # as warnings so a failed widget is never reported as a failed main app.
            foreach ($result in $InstallResults) { if ($result.Item -eq "Kill Confirm Overlay 主程序") { $result.Item="可选 Game Bar 小组件"; if($result.Status -eq "Error") { $result.Status="Warning" } } }
            Add-InstallResult -Status Warning -Item "可选 Game Bar 小组件" -Detail ((Get-ErrorReason $_) + "；可直接使用兼容显示")
        }
    }

    Write-InstallStage -Number 5 -Total 7 -Name "CS2 GSI 配置" -Detail $(if ($SkipGsiConfig) { "已通过参数跳过" } else { "查找 CS2 并写入当前 CFG" })
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

    Write-InstallStage -Number 6 -Total 7 -Name "本机通信权限" -Detail $(if ($SkipLoopback) { "已通过参数跳过" } else { "配置 Widget 与本地服务通信" })
    if (-not $SkipLoopback -and -not $compatibilityFallback) {
        try {
            if (-not $PackageFamilyName) {
                Update-InstalledPackageContext | Out-Null
            }
            Enable-LoopbackExemptionVerified -AppPackageFamilyName $PackageFamilyName
            Add-InstallResult -Status Success -Item "本机回环通信权限" -Detail "已写入并从系统列表回读确认；Widget 可以访问 127.0.0.1 上的伴随服务"
        }
        catch {
            Add-InstallResult -Status Warning -Item "本机回环通信权限" -Detail (Get-ErrorReason $_)
        }
    }
    else {
        Add-InstallResult -Status Warning -Item "本机回环通信权限" -Detail "已通过命令行参数跳过"
    }

    if ($compatibilityFallback) {
        . (Join-Path $ScriptRoot "Scripts/CompatibilityDisplay/Install-CompatibilityDisplay.ps1")
        Initialize-CompatibilityDisplayFallback
    }

    if ($OpenGameBar -and -not $compatibilityFallback) {
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
        Write-InstallStage -Number 7 -Total 7 -Name "生成安装结果" -Detail "汇总成功、提示和失败项目"
        if ($InstallResults.Count -eq 0) {
            Add-InstallResult -Status Error -Item "安装器" -Detail "没有生成任何安装结果，请查看详细日志"
        }
        Show-InstallSummary
    }
    catch {
        Write-InstallLog "Failed to show the installation summary: $($_.Exception.Message)"
        Write-Host "安装流程已经执行完毕，但诊断报告生成失败。完整日志：$LogPath"
    }
}

# Every installation stage reports its own Success/Warning/Error result and is
# intentionally non-blocking. Do not leak a stale native-command exit code
# (reg.exe, gpresult.exe, CheckNetIsolation.exe) back to the Inno Setup host.
$global:LASTEXITCODE = 0
exit 0

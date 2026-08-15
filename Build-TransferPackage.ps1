param(
    [string]$Configuration = "Release",
    [string]$Platform = "x64",
    [string]$MsBuildPath = "",
    [string]$VcInstallPath = "",
    [switch]$DisableSigning,
    [string]$CertificatePfxPath = "",
    [string]$CertificatePassword = "",
    [string]$CertificateThumbprint = "",
    [string]$CertificateCerPath = ""
)

$ErrorActionPreference = "Stop"

$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
$WorkspaceRoot = Split-Path -Parent $Root
$ManifestPath = Join-Path $Root "Package\Package.appxmanifest"

if (-not (Test-Path $ManifestPath)) {
    throw "Package.appxmanifest was not found at $ManifestPath"
}

[xml]$Manifest = Get-Content $ManifestPath
$Version = $Manifest.Package.Identity.Version
if (-not $Version) {
    throw "Could not read package version from $ManifestPath"
}

$PackageFolderName = "KillConfirmGameBar.Package_{0}_{1}_{2}_Test" -f $Version, $Platform, $Configuration
$PackageFileName = "KillConfirmGameBar.Package_{0}_{1}_{2}.msix" -f $Version, $Platform, $Configuration
$PackageOutputFolder = "Integrated_{0}_Package" -f $Configuration
$PackageSourceRoot = Join-Path $Root ("Package\AppPackages\{0}\{1}" -f $PackageOutputFolder, $PackageFolderName)
$AppPackagesRoot = Join-Path $Root "Package\AppPackages"
$TransferRoot = Join-Path $WorkspaceRoot ("KillConfirmGameBar_Transfer_{0}_有依赖-新人用" -f $Version)
$TransferZip = "{0}.zip" -f $TransferRoot
$NoDependenciesTransferRoot = Join-Path $WorkspaceRoot ("KillConfirmGameBar_Transfer_{0}_无依赖-更新用" -f $Version)
$NoDependenciesTransferZip = "{0}.zip" -f $NoDependenciesTransferRoot
$ExpectedPackageFamilyName = "KillConfirmGameBar.Overlay_5jgcw66eyez0m"
$PrerequisiteSourceRoot = Join-Path $WorkspaceRoot "Vclibs"
$PrerequisiteFileNames = @(
    "Microsoft.UI.Xaml.Appx",
    "vclibs.appx",
    "vclibs2.appx",
    "gamebar.AppxBundle"
)

foreach ($prerequisiteFileName in $PrerequisiteFileNames) {
    $prerequisiteSourcePath = Join-Path $PrerequisiteSourceRoot $prerequisiteFileName
    if (-not (Test-Path -LiteralPath $prerequisiteSourcePath -PathType Leaf)) {
        throw "Required prerequisite package was not found: $prerequisiteSourcePath"
    }
}

$resolvedWorkspaceRoot = [System.IO.Path]::GetFullPath($WorkspaceRoot)
$resolvedTransferRoot = [System.IO.Path]::GetFullPath($TransferRoot)
$resolvedNoDependenciesTransferRoot = [System.IO.Path]::GetFullPath($NoDependenciesTransferRoot)
foreach ($resolvedOutputRoot in @($resolvedTransferRoot, $resolvedNoDependenciesTransferRoot)) {
    if (-not $resolvedOutputRoot.StartsWith($resolvedWorkspaceRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to write outside the workspace root: $resolvedOutputRoot"
    }
}

$KillConfirmProcessNames = @(
    "cskillconfirm",
    "TestXboxGameBar",
    "KillConfirmOverlay",
    "KillConfirmGameBar",
    "GameBar",
    "GameBarFTServer",
    "GameBarPresenceWriter"
)

Get-Process -Name $KillConfirmProcessNames -ErrorAction SilentlyContinue | Stop-Process -Force

$buildIntegratedArgs = @{
    Configuration = $Configuration
    Platform = $Platform
    MsBuildPath = $MsBuildPath
}
if ($VcInstallPath) {
    $buildIntegratedArgs.VcInstallPath = $VcInstallPath
}
if ($DisableSigning) {
    $buildIntegratedArgs.DisableSigning = $true
}
if ($CertificatePfxPath) {
    $buildIntegratedArgs.CertificatePfxPath = $CertificatePfxPath
}
if ($CertificatePassword) {
    $buildIntegratedArgs.CertificatePassword = $CertificatePassword
}
if ($CertificateThumbprint) {
    $buildIntegratedArgs.CertificateThumbprint = $CertificateThumbprint
}

& (Join-Path $Root "Build-IntegratedPackage.ps1") @buildIntegratedArgs

if (-not (Test-Path (Join-Path $PackageSourceRoot $PackageFileName))) {
    $ProducedPackage = Get-ChildItem -LiteralPath $AppPackagesRoot -Filter "*.msix" -Recurse -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -like "*$Version*" -and $_.Name -like "*$Platform*" } |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1

    if ($ProducedPackage) {
        $PackageSourceRoot = $ProducedPackage.DirectoryName
        $PackageFileName = $ProducedPackage.Name
    }
}

if (-not (Test-Path $PackageSourceRoot)) {
    throw "Expected package folder was not produced: $PackageSourceRoot"
}

if (-not (Test-Path (Join-Path $PackageSourceRoot $PackageFileName))) {
    throw "Expected package file was not produced: $(Join-Path $PackageSourceRoot $PackageFileName)"
}

if (Test-Path $TransferRoot) {
    Remove-Item -LiteralPath $TransferRoot -Recurse -Force
}

if (Test-Path $TransferZip) {
    Remove-Item -LiteralPath $TransferZip -Force
}

if (Test-Path $NoDependenciesTransferRoot) {
    Remove-Item -LiteralPath $NoDependenciesTransferRoot -Recurse -Force
}

if (Test-Path $NoDependenciesTransferZip) {
    Remove-Item -LiteralPath $NoDependenciesTransferZip -Force
}

$OverlayTransferRoot = Join-Path $TransferRoot "OverlayPackage"
$PrerequisiteTransferRoot = Join-Path $TransferRoot "Prerequisites"

New-Item -ItemType Directory -Force -Path $OverlayTransferRoot | Out-Null
New-Item -ItemType Directory -Force -Path $PrerequisiteTransferRoot | Out-Null

foreach ($prerequisiteFileName in $PrerequisiteFileNames) {
    Copy-Item -LiteralPath (Join-Path $PrerequisiteSourceRoot $prerequisiteFileName) -Destination $PrerequisiteTransferRoot -Force
}

Copy-Item -LiteralPath (Join-Path $PackageSourceRoot $PackageFileName) -Destination $OverlayTransferRoot -Force

$PackageCertificate = Get-ChildItem -LiteralPath $PackageSourceRoot -Filter "*.cer" -File -ErrorAction SilentlyContinue | Select-Object -First 1
if ($PackageCertificate) {
    Copy-Item -LiteralPath $PackageCertificate.FullName -Destination $OverlayTransferRoot -Force
}
elseif ($CertificateCerPath) {
    Copy-Item -LiteralPath $CertificateCerPath -Destination $OverlayTransferRoot -Force
}

$DependencySourceRoot = Join-Path $PackageSourceRoot "Dependencies\$Platform"
if (Test-Path $DependencySourceRoot) {
    $DependencyTargetRoot = Join-Path $OverlayTransferRoot "Dependencies\$Platform"
    New-Item -ItemType Directory -Force -Path $DependencyTargetRoot | Out-Null
    Get-ChildItem -LiteralPath $DependencySourceRoot -Include "*.appx", "*.msix" -File -Recurse |
        Copy-Item -Destination $DependencyTargetRoot -Force
}

$InstallScript = @'
param(
    [switch]$SkipLoopback = $false,
    [switch]$SkipGsiConfig = $false,
    [switch]$OpenGameBar = $false
)

$ErrorActionPreference = "Stop"
$InstallPrerequisites = __INSTALL_PREREQUISITES__

$ScriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$OverlayRoot = Join-Path $ScriptRoot "OverlayPackage"
$PrerequisiteRoot = Join-Path $ScriptRoot "Prerequisites"
$PackageName = "KillConfirmGameBar.Overlay"
$PackageFamilyName = $null
$LogPath = Join-Path $env:TEMP "KillConfirmGameBar_Install.log"
$RuntimeLogRoot = $null
$InstallResults = New-Object System.Collections.Generic.List[object]

function Add-InstallResult {
    param(
        [ValidateSet("Success", "Warning", "Error")][string]$Status,
        [string]$Item,
        [string]$Detail = ""
    )

    $symbol = switch ($Status) {
        "Success" { "✅" }
        "Warning" { "⚠️" }
        default { "❌" }
    }
    $script:InstallResults.Add([pscustomobject]@{
        Status = $Status
        Item = $Item
        Detail = $Detail
    })
    Write-InstallLog ("{0} {1}{2}" -f $symbol, $Item, $(if ($Detail) { " - $Detail" } else { "" }))
}

function Get-ErrorReason {
    param([System.Management.Automation.ErrorRecord]$ErrorRecord)

    if (-not $ErrorRecord) {
        return "未知错误"
    }
    $message = $ErrorRecord.Exception.Message
    $hresult = $ErrorRecord.Exception.HResult
    if ($hresult) {
        return ("{0} (HRESULT 0x{1:X8})" -f $message, ($hresult -band 0xffffffffL))
    }
    return $message
}

function Show-InstallSummary {
    $errorCount = @($InstallResults | Where-Object Status -eq "Error").Count
    $warningCount = @($InstallResults | Where-Object Status -eq "Warning").Count
    $successCount = @($InstallResults | Where-Object Status -eq "Success").Count
    $title = if ($errorCount -gt 0) {
        "Kill Confirm 安装诊断 - 有问题需要处理"
    }
    elseif ($warningCount -gt 0) {
        "Kill Confirm 安装诊断 - 已完成但有提示"
    }
    else {
        "Kill Confirm 安装诊断 - 全部成功"
    }

    $summaryLines = @(
        "安装流程已经执行完毕，不会因为单项失败而跳过后续安装。",
        "成功 $successCount 项，提示 $warningCount 项，失败 $errorCount 项。",
        ""
    )
    foreach ($result in $InstallResults) {
        $symbol = switch ($result.Status) {
            "Success" { "✅" }
            "Warning" { "⚠️" }
            default { "❌" }
        }
        $summaryLines += ("{0} {1}" -f $symbol, $result.Item)
        if ($result.Detail) {
            $summaryLines += ("    原因/说明：{0}" -f $result.Detail)
        }
    }
    $summaryLines += ""
    $summaryLines += "完整日志：$LogPath"

    $logText = ""
    try {
        if (Test-Path -LiteralPath $LogPath) {
            $logText = Get-Content -LiteralPath $LogPath -Raw -ErrorAction Stop
        }
    }
    catch {
        $logText = "日志读取失败：$($_.Exception.Message)"
    }

    try {
        Add-Type -AssemblyName System.Windows.Forms -ErrorAction Stop
        Add-Type -AssemblyName System.Drawing -ErrorAction Stop
        $form = New-Object System.Windows.Forms.Form
        $form.Text = $title
        $form.StartPosition = "CenterScreen"
        $form.Size = New-Object System.Drawing.Size(820, 680)
        $form.MinimumSize = New-Object System.Drawing.Size(680, 520)
        $form.TopMost = $true

        $textBox = New-Object System.Windows.Forms.TextBox
        $textBox.Multiline = $true
        $textBox.ReadOnly = $true
        $textBox.ScrollBars = "Both"
        $textBox.WordWrap = $false
        $textBox.Dock = "Fill"
        $textBox.Font = New-Object System.Drawing.Font("Segoe UI", 10)
        $textBox.Text = (($summaryLines -join [Environment]::NewLine) +
            [Environment]::NewLine + [Environment]::NewLine +
            "================ 详细日志 ================" +
            [Environment]::NewLine + $logText)

        $buttonPanel = New-Object System.Windows.Forms.FlowLayoutPanel
        $buttonPanel.Dock = "Bottom"
        $buttonPanel.Height = 48
        $buttonPanel.FlowDirection = "RightToLeft"
        $buttonPanel.Padding = New-Object System.Windows.Forms.Padding(8)

        $closeButton = New-Object System.Windows.Forms.Button
        $closeButton.Text = "关闭"
        $closeButton.Width = 100
        $closeButton.Add_Click({ $form.Close() })

        $copyButton = New-Object System.Windows.Forms.Button
        $copyButton.Text = "复制诊断信息"
        $copyButton.Width = 130
        $copyButton.Add_Click({
            try { [System.Windows.Forms.Clipboard]::SetText($textBox.Text) } catch {}
        })

        $openLogButton = New-Object System.Windows.Forms.Button
        $openLogButton.Text = "打开日志文件"
        $openLogButton.Width = 130
        $openLogButton.Add_Click({
            try { Start-Process notepad.exe -ArgumentList ('"{0}"' -f $LogPath) | Out-Null } catch {}
        })

        $buttonPanel.Controls.Add($closeButton)
        $buttonPanel.Controls.Add($copyButton)
        $buttonPanel.Controls.Add($openLogButton)
        $form.Controls.Add($textBox)
        $form.Controls.Add($buttonPanel)
        $form.AcceptButton = $closeButton
        [void]$form.ShowDialog()
    }
    catch {
        Write-Host ($summaryLines -join [Environment]::NewLine)
    }
}

function ConvertFrom-Utf8Base64 {
    param([string]$Value)
    return [System.Text.Encoding]::UTF8.GetString([System.Convert]::FromBase64String($Value))
}

$Prerequisites = @(
    [pscustomobject]@{
        Order = 1
        DisplayName = "Microsoft UI XAML Framework 2.8 (x64)"
        ChineseDisplayName = (ConvertFrom-Utf8Base64 -Value "TWljcm9zb2Z0IFVJIFhBTUwgMi44IOahhuaetiAoeDY0KQ==")
        PackageName = "Microsoft.UI.Xaml.2.8"
        Architecture = "X64"
        MinimumVersion = [version]"8.2310.30001.0"
        FileName = "Microsoft.UI.Xaml.Appx"
    },
    [pscustomobject]@{
        Order = 2
        DisplayName = "Microsoft Visual C++ UWP Desktop Runtime (x64)"
        ChineseDisplayName = (ConvertFrom-Utf8Base64 -Value "TWljcm9zb2Z0IFZpc3VhbCBDKysgVVdQIERlc2t0b3Ag6L+Q6KGM5bqTICh4NjQp")
        PackageName = "Microsoft.VCLibs.140.00.UWPDesktop"
        Architecture = "X64"
        MinimumVersion = [version]"14.0.33728.0"
        FileName = "vclibs.appx"
    },
    [pscustomobject]@{
        Order = 3
        DisplayName = "Microsoft Visual C++ UWP Runtime (x64)"
        ChineseDisplayName = (ConvertFrom-Utf8Base64 -Value "TWljcm9zb2Z0IFZpc3VhbCBDKysgVVdQIOi/kOihjOW6kyAoeDY0KQ==")
        PackageName = "Microsoft.VCLibs.140.00"
        Architecture = "X64"
        MinimumVersion = [version]"14.0.33519.0"
        FileName = "vclibs2.appx"
    },
    [pscustomobject]@{
        Order = 4
        DisplayName = "Xbox Game Bar"
        ChineseDisplayName = "Xbox Game Bar"
        PackageName = "Microsoft.XboxGamingOverlay"
        Architecture = "X64"
        MinimumVersion = [version]"7.326.6011.0"
        FileName = "gamebar.AppxBundle"
    }
)

function Write-InstallLog {
    param([string]$Message)
    $line = "[{0}] {1}" -f (Get-Date -Format "yyyy-MM-dd HH:mm:ss"), $Message
    try {
        Add-Content -LiteralPath $LogPath -Value $line -Encoding UTF8 -ErrorAction Stop
    }
    catch {
        Write-Host "[Log write failed: $($_.Exception.Message)]"
    }
    Write-Host $Message
}

function Test-XboxGameBarAvailable {
    return $null -ne (Get-AppxPackage -Name "Microsoft.XboxGamingOverlay" -ErrorAction SilentlyContinue |
        Sort-Object Version -Descending |
        Select-Object -First 1)
}

function Confirm-XboxGameBarAvailable {
    if (Test-XboxGameBarAvailable) {
        $gameBar = Get-AppxPackage -Name "Microsoft.XboxGamingOverlay" -ErrorAction SilentlyContinue |
            Sort-Object Version -Descending |
            Select-Object -First 1
        Write-InstallLog "Xbox Game Bar package is available: $($gameBar.Version)"
        return
    }

    Write-InstallLog "Xbox Game Bar is still unavailable after prerequisite handling. Opening Microsoft Store fallback."
    try {
        Start-Process "ms-windows-store://pdp/?ProductId=9NZKPSTSNW4P" | Out-Null
    }
    catch {
        Write-InstallLog "Could not open the Xbox Game Bar Microsoft Store page: $($_.Exception.Message)"
    }

    throw "Xbox Game Bar is not installed. Install it from Microsoft Store, then run this installer again."
}

function Set-RegistryDwordAndReport {
    param(
        [string]$Path,
        [string]$Name,
        [int]$Value,
        [string]$Item,
        [string]$SuccessDetail
    )

    try {
        if (-not (Test-Path -LiteralPath $Path)) {
            New-Item -Path $Path -Force -ErrorAction Stop | Out-Null
        }
        New-ItemProperty -LiteralPath $Path -Name $Name -PropertyType DWord -Value $Value -Force -ErrorAction Stop | Out-Null
        $actual = [int](Get-ItemPropertyValue -LiteralPath $Path -Name $Name -ErrorAction Stop)
        if ($actual -ne $Value) {
            throw "写入后验证失败：期望 $Value，实际 $actual"
        }
        Add-InstallResult -Status Success -Item $Item -Detail $SuccessDetail
    }
    catch {
        Add-InstallResult -Status Error -Item $Item -Detail ((Get-ErrorReason $_) + "；已继续后续安装")
    }
}

function Backup-GameBarRegistry {
    $backupRoot = Join-Path $env:TEMP ("KillConfirmGameBar_Registry_Backup_{0}" -f (Get-Date -Format "yyyyMMdd_HHmmss"))
    try {
        New-Item -ItemType Directory -Path $backupRoot -Force -ErrorAction Stop | Out-Null
        $exports = @(
            @{ Key = "HKLM\SOFTWARE\Policies\Microsoft\Windows\GameDVR"; File = "HKLM-GameDVR-Policy.reg" },
            @{ Key = "HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\GameDVR"; File = "HKCU-GameDVR.reg" },
            @{ Key = "HKCU\System\GameConfigStore"; File = "HKCU-GameConfigStore.reg" },
            @{ Key = "HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Explorer"; File = "HKCU-Explorer-Policy.reg" }
        )
        $exportedCount = 0
        foreach ($export in $exports) {
            & reg.exe export $export.Key (Join-Path $backupRoot $export.File) /y 2>$null | Out-Null
            if ($LASTEXITCODE -eq 0) {
                $exportedCount++
            }
        }
        Add-InstallResult -Status Success -Item "Game Bar 注册表备份" -Detail ("已备份 {0} 项到：{1}" -f $exportedCount, $backupRoot)
    }
    catch {
        Add-InstallResult -Status Warning -Item "Game Bar 注册表备份" -Detail ((Get-ErrorReason $_) + "；修复仍会继续")
    }
    return $backupRoot
}

function Repair-XboxGameBarEnvironment {
    Write-InstallLog "Repairing Xbox Game Bar policy, user settings, shortcut, and service state..."
    $backupRoot = Backup-GameBarRegistry

    Set-RegistryDwordAndReport `
        -Path "HKLM:\SOFTWARE\Policies\Microsoft\Windows\GameDVR" `
        -Name "AllowGameDVR" -Value 1 `
        -Item "Game DVR 计算机策略" `
        -SuccessDetail "AllowGameDVR=1，已允许游戏录制和广播"

    Set-RegistryDwordAndReport `
        -Path "HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\GameDVR" `
        -Name "AppCaptureEnabled" -Value 1 `
        -Item "当前用户 Game Bar 开关" `
        -SuccessDetail "AppCaptureEnabled=1"

    Set-RegistryDwordAndReport `
        -Path "HKCU:\System\GameConfigStore" `
        -Name "GameDVR_Enabled" -Value 1 `
        -Item "当前用户 Game DVR 开关" `
        -SuccessDetail "GameDVR_Enabled=1"

    Set-RegistryDwordAndReport `
        -Path "HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\GameDVR" `
        -Name "VKMToggleGameBar" -Value 1 `
        -Item "Game Bar 快捷键开关" `
        -SuccessDetail "VKMToggleGameBar=1"

    Set-RegistryDwordAndReport `
        -Path "HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\GameDVR" `
        -Name "VKToggleGameBar" -Value 71 `
        -Item "Game Bar 快捷键按键" `
        -SuccessDetail "VKToggleGameBar=71（G 键）"

    Set-RegistryDwordAndReport `
        -Path "HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Explorer" `
        -Name "NoWinKeys" -Value 0 `
        -Item "Windows 组合键策略" `
        -SuccessDetail "NoWinKeys=0；Win+G/Win+R/Win+E 未被该策略禁止"

    try {
        $serviceKeys = @(Get-ChildItem -LiteralPath "HKLM:\SYSTEM\CurrentControlSet\Services" -ErrorAction Stop |
            Where-Object { $_.PSChildName -eq "BcastDVRUserService" -or $_.PSChildName -like "BcastDVRUserService_*" })
        if ($serviceKeys.Count -eq 0) {
            Add-InstallResult -Status Warning -Item "Game DVR 用户服务" -Detail "没有找到 BcastDVRUserService 服务项；没有创建不完整的新服务项"
        }
        else {
            $serviceFailures = New-Object System.Collections.Generic.List[string]
            foreach ($serviceKey in $serviceKeys) {
                try {
                    New-ItemProperty -LiteralPath $serviceKey.PSPath -Name "Start" -PropertyType DWord -Value 3 -Force -ErrorAction Stop | Out-Null
                    $actual = [int](Get-ItemPropertyValue -LiteralPath $serviceKey.PSPath -Name "Start" -ErrorAction Stop)
                    if ($actual -ne 3) {
                        throw "Start 写入后为 $actual"
                    }
                }
                catch {
                    $serviceFailures.Add(("{0}: {1}" -f $serviceKey.PSChildName, (Get-ErrorReason $_)))
                }
            }
            if ($serviceFailures.Count -eq 0) {
                Add-InstallResult -Status Success -Item "Game DVR 用户服务" -Detail ("{0} 个服务项已恢复为手动/触发启动" -f $serviceKeys.Count)
            }
            else {
                Add-InstallResult -Status Error -Item "Game DVR 用户服务" -Detail (($serviceFailures -join "；") + "；已继续后续安装")
            }
        }
    }
    catch {
        Add-InstallResult -Status Error -Item "Game DVR 用户服务" -Detail ((Get-ErrorReason $_) + "；已继续后续安装")
    }

    if (Test-Path -LiteralPath "Registry::HKEY_CLASSES_ROOT\ms-gamebar") {
        Add-InstallResult -Status Success -Item "ms-gamebar 启动协议" -Detail "协议已注册"
    }
    else {
        Add-InstallResult -Status Error -Item "ms-gamebar 启动协议" -Detail "协议未注册；Win+G 和 URI 启动可能无响应"
    }

    try {
        $scancodeMap = Get-ItemPropertyValue `
            -LiteralPath "HKLM:\SYSTEM\CurrentControlSet\Control\Keyboard Layout" `
            -Name "Scancode Map" -ErrorAction SilentlyContinue
        if ($null -ne $scancodeMap) {
            Add-InstallResult -Status Warning -Item "键盘按键映射" -Detail "检测到系统 Scancode Map；它可能禁用了 Windows 键，安装器不会自动删除该自定义映射"
        }
        else {
            Add-InstallResult -Status Success -Item "键盘按键映射" -Detail "未检测到系统 Scancode Map"
        }
    }
    catch {
        Add-InstallResult -Status Warning -Item "键盘按键映射" -Detail (Get-ErrorReason $_)
    }

    try {
        $policyReportPath = Join-Path $backupRoot "Applied-Group-Policy.html"
        & gpresult.exe /h $policyReportPath /f 2>$null | Out-Null
        if ($LASTEXITCODE -eq 0 -and (Test-Path -LiteralPath $policyReportPath -PathType Leaf)) {
            Add-InstallResult -Status Success -Item "实际生效组策略报告" -Detail "已保存：$policyReportPath"
        }
        else {
            Add-InstallResult -Status Warning -Item "实际生效组策略报告" -Detail "gpresult 返回退出码 $LASTEXITCODE；不影响继续安装"
        }
    }
    catch {
        Add-InstallResult -Status Warning -Item "实际生效组策略报告" -Detail ((Get-ErrorReason $_) + "；不影响继续安装")
    }
}

function Get-AppxIdentityFromPackageFile {
    param([string]$PackagePath)

    try {
        Add-Type -AssemblyName System.IO.Compression.FileSystem -ErrorAction SilentlyContinue
        $zip = [System.IO.Compression.ZipFile]::OpenRead($PackagePath)
        try {
            $entry = $zip.GetEntry("AppxManifest.xml")
            if (-not $entry) {
                return $null
            }

            $reader = New-Object System.IO.StreamReader($entry.Open())
            try {
                [xml]$manifest = $reader.ReadToEnd()
            }
            finally {
                $reader.Dispose()
            }

            if (-not $manifest.Package.Identity.Name) {
                return $null
            }

            return [pscustomobject]@{
                Name = $manifest.Package.Identity.Name
                Version = [version]$manifest.Package.Identity.Version
                Publisher = $manifest.Package.Identity.Publisher
            }
        }
        finally {
            $zip.Dispose()
        }
    }
    catch {
        Write-InstallLog "Could not inspect package identity for ${PackagePath}: $($_.Exception.Message)"
        return $null
    }
}

function Test-AppxPackageInstalled {
    param([string]$PackagePath)

    $identity = Get-AppxIdentityFromPackageFile -PackagePath $PackagePath
    if (-not $identity) {
        return $false
    }

    $installed = Get-AppxPackage -Name $identity.Name -ErrorAction SilentlyContinue |
        Sort-Object Version -Descending |
        Select-Object -First 1
    if (-not $installed) {
        return $false
    }

    try {
        return ([version]$installed.Version -ge $identity.Version)
    }
    catch {
        return $true
    }
}

function Write-AppxFailureDetails {
    param([System.Management.Automation.ErrorRecord]$ErrorRecord)

    Write-InstallLog ("Install failed: {0}" -f $ErrorRecord.Exception.Message)
    $details = ($ErrorRecord | Format-List * -Force | Out-String)
    Add-Content -LiteralPath $LogPath -Value $details -Encoding UTF8

    $activityId = $null
    if ($ErrorRecord.Exception -and $ErrorRecord.Exception.ActivityId) {
        $activityId = $ErrorRecord.Exception.ActivityId
    }

    if ($activityId) {
        try {
            Write-InstallLog "AppX deployment activity id: $activityId"
            $activityLog = Get-AppPackageLog -ActivityID $activityId -ErrorAction Stop | Out-String
            Add-Content -LiteralPath $LogPath -Value $activityLog -Encoding UTF8
        }
        catch {
            Write-InstallLog "Could not read AppX activity log: $($_.Exception.Message)"
        }
    }

    try {
        $events = Get-WinEvent -LogName "Microsoft-Windows-AppXDeploymentServer/Operational" -MaxEvents 30 -ErrorAction Stop |
            Select-Object TimeCreated, Id, LevelDisplayName, ProviderName, Message |
            Format-List |
            Out-String
        Add-Content -LiteralPath $LogPath -Value $events -Encoding UTF8
    }
    catch {
        Write-InstallLog "Could not read AppX deployment event log: $($_.Exception.Message)"
    }
}

function Get-InstalledOverlayPackage {
    $package = Get-AppxPackage -Name $PackageName -ErrorAction SilentlyContinue |
        Sort-Object Version -Descending |
        Select-Object -First 1

    if (-not $package) {
        throw "MSIX install finished, but $PackageName is not registered for this user."
    }

    return $package
}

function Update-InstalledPackageContext {
    $package = Get-InstalledOverlayPackage
    $script:PackageFamilyName = $package.PackageFamilyName
    $script:RuntimeLogRoot = Join-Path $env:LOCALAPPDATA "Packages\$PackageFamilyName\LocalState"
    return $package
}

function Import-PackageCertificate {
    param([string]$CertificatePath)

    $storeLocations = @(
        "Cert:\CurrentUser\TrustedPeople",
        "Cert:\CurrentUser\Root",
        "Cert:\LocalMachine\TrustedPeople",
        "Cert:\LocalMachine\Root"
    )

    $importedCount = 0
    $lastFailure = ""
    foreach ($storeLocation in $storeLocations) {
        try {
            $cert = Import-Certificate -FilePath $CertificatePath -CertStoreLocation $storeLocation -ErrorAction Stop
            Write-InstallLog "Certificate imported: $storeLocation $($cert.Thumbprint)"
            $importedCount++
        }
        catch {
            $lastFailure = $_.Exception.Message
            Write-InstallLog "Certificate import skipped for ${storeLocation}: $($_.Exception.Message)"
        }
    }
    return [pscustomobject]@{ ImportedCount = $importedCount; LastFailure = $lastFailure }
}

function Add-AppxPackageCompat {
    param(
        [string]$PackagePath,
        [switch]$ForceUpdate,
        [switch]$DeferWhenInUse,
        [switch]$UseSystemVolume
    )

    $command = Get-Command Add-AppxPackage -ErrorAction Stop
    $addPackageParams = @{
        Path = $PackagePath
        ErrorAction = "Stop"
    }

    if ($ForceUpdate -and $command.Parameters.ContainsKey("ForceUpdateFromAnyVersion")) {
        $addPackageParams.ForceUpdateFromAnyVersion = $true
    }
    if ($DeferWhenInUse -and $command.Parameters.ContainsKey("DeferRegistrationWhenPackagesAreInUse")) {
        $addPackageParams.DeferRegistrationWhenPackagesAreInUse = $true
    }
    if ($UseSystemVolume -and $command.Parameters.ContainsKey("Volume")) {
        $systemVolume = Get-AppxVolume -ErrorAction Stop |
            Where-Object { $_.IsSystemVolume -and -not $_.IsOffline } |
            Select-Object -First 1
        if (-not $systemVolume) {
            throw "The trusted system AppX volume could not be found."
        }

        $addPackageParams.Volume = $systemVolume
        Write-InstallLog "Using trusted system AppX volume: $($systemVolume.PackageStorePath)"
    }
    Write-InstallLog "Add-AppxPackage path: $PackagePath"
    Write-InstallLog ("Add-AppxPackage switches: ForceUpdateFromAnyVersion={0}; DeferRegistrationWhenPackagesAreInUse={1}; SystemVolume={2}" -f `
        $addPackageParams.ContainsKey("ForceUpdateFromAnyVersion"), `
        $addPackageParams.ContainsKey("DeferRegistrationWhenPackagesAreInUse"), `
        $addPackageParams.ContainsKey("Volume"))
    try {
        Add-AppxPackage @addPackageParams
        Write-InstallLog "Add-AppxPackage succeeded: $(Split-Path -Leaf $PackagePath)"
    }
    catch {
        Write-AppxFailureDetails -ErrorRecord $_
        throw
    }
}

function Get-InstalledPrerequisitePackage {
    param([pscustomobject]$Prerequisite)

    return Get-AppxPackage -Name $Prerequisite.PackageName -ErrorAction SilentlyContinue |
        Where-Object { $null -eq $_.Architecture -or ([string]$_.Architecture -eq $Prerequisite.Architecture) } |
        Sort-Object { [version]$_.Version } -Descending |
        Select-Object -First 1
}

function Test-PrerequisiteInstalled {
    param([pscustomobject]$Prerequisite)

    $installed = Get-InstalledPrerequisitePackage -Prerequisite $Prerequisite
    if (-not $installed) {
        return $false
    }

    try {
        return ([version]$installed.Version -ge $Prerequisite.MinimumVersion)
    }
    catch {
        return $false
    }
}

function Confirm-PrerequisiteInstall {
    param([object[]]$MissingPrerequisites)

    $isChinese = [System.Globalization.CultureInfo]::CurrentUICulture.Name -like "zh-*"
    if ($isChinese) {
        $missingLines = $MissingPrerequisites | ForEach-Object {
            "  {0}. {1}" -f $_.Order, $_.ChineseDisplayName
        }
        $title = ConvertFrom-Utf8Base64 -Value "S2lsbCBDb25maXJtIE92ZXJsYXkgLSDlv4XpnIDnu4Tku7Y="
        $message = @(
            (ConvertFrom-Utf8Base64 -Value "5qOA5rWL5Yiw5Lul5LiL5b+F6ZyA57uE5Lu25pyq5a6J6KOF5oiW54mI5pys6L+H5pen77ya"),
            "",
            ($missingLines -join [Environment]::NewLine),
            "",
            (ConvertFrom-Utf8Base64 -Value "5a6J6KOF56iL5bqP5bCG5oyJ6aG65bqP5a6J6KOF57y65aSx57uE5Lu277yM54S25ZCO5YaN5a6J6KOFIEtpbGwgQ29uZmlybSBPdmVybGF544CC"),
            (ConvertFrom-Utf8Base64 -Value "6L+Z5Lqb57uE5Lu25p2l6Ieq5a6J6KOF5YyF5YaF6ZmE55qEIE1pY3Jvc29mdCDnprvnur/lronoo4Xmlofku7bjgII="),
            (ConvertFrom-Utf8Base64 -Value "5aaC5p6c6YCJ5oup5ZCm77yM5LuN5Lya57un57ut5a6J6KOF5pys5L2T77yM5L2G57y65bCR6L+Z5Lqb57uE5Lu25pe26L2v5Lu25bCG5peg5rOV5q2j5bi46L+Q6KGM44CC"),
            "",
            (ConvertFrom-Utf8Base64 -Value "5piv5ZCm546w5Zyo5a6J6KOF77yf")
        ) -join [Environment]::NewLine
    }
    else {
        $missingLines = $MissingPrerequisites | ForEach-Object {
            "  {0}. {1}" -f $_.Order, $_.DisplayName
        }
        $title = "Kill Confirm Overlay - Required components"
        $message = @(
            "The following required components are missing or outdated:",
            "",
            ($missingLines -join [Environment]::NewLine),
            "",
            "Setup will install the missing components in order before installing Kill Confirm Overlay.",
            "These components use the bundled Microsoft offline packages.",
            "If you choose No, setup will continue, but the software will not work correctly without these components.",
            "",
            "Install them now?"
        ) -join [Environment]::NewLine
    }

    Add-Type -AssemblyName System.Windows.Forms -ErrorAction Stop
    $result = [System.Windows.Forms.MessageBox]::Show(
        $message,
        $title,
        [System.Windows.Forms.MessageBoxButtons]::YesNo,
        [System.Windows.Forms.MessageBoxIcon]::Warning,
        [System.Windows.Forms.MessageBoxDefaultButton]::Button1)
    return ($result -eq [System.Windows.Forms.DialogResult]::Yes)
}

function Install-RequiredComponents {
    Write-InstallLog "Checking required Microsoft UI XAML, VCLibs, and Xbox Game Bar packages..."
    if (-not (Test-Path -LiteralPath $PrerequisiteRoot -PathType Container)) {
        Add-InstallResult -Status Error -Item "离线依赖目录" -Detail "未找到：$PrerequisiteRoot"
        return
    }

    $missingPrerequisites = @($Prerequisites |
        Sort-Object Order |
        Where-Object { -not (Test-PrerequisiteInstalled -Prerequisite $_) })

    foreach ($prerequisite in ($Prerequisites | Sort-Object Order)) {
        $installed = Get-InstalledPrerequisitePackage -Prerequisite $prerequisite
        if ($installed) {
            Write-InstallLog ("Prerequisite detected: {0} {1} ({2})" -f $prerequisite.PackageName, $installed.Version, $installed.Architecture)
        }
        else {
            Write-InstallLog ("Prerequisite missing: {0}" -f $prerequisite.PackageName)
        }
    }

    if ($missingPrerequisites.Count -eq 0) {
        Write-InstallLog "All required Microsoft UI XAML, VCLibs, and Xbox Game Bar packages are already installed."
        foreach ($prerequisite in ($Prerequisites | Sort-Object Order)) {
            $installed = Get-InstalledPrerequisitePackage -Prerequisite $prerequisite
            Add-InstallResult -Status Success -Item $prerequisite.ChineseDisplayName -Detail ("已安装，版本 {0}" -f $installed.Version)
        }
        return
    }

    try {
        $confirmed = Confirm-PrerequisiteInstall -MissingPrerequisites $missingPrerequisites
    }
    catch {
        Add-InstallResult -Status Error -Item "依赖安装确认窗口" -Detail (Get-ErrorReason $_)
        $confirmed = $false
    }
    if (-not $confirmed) {
        Write-InstallLog "The user declined installation of required components. Continuing overlay installation with a compatibility warning."
        foreach ($prerequisite in $missingPrerequisites) {
            Add-InstallResult -Status Warning -Item $prerequisite.ChineseDisplayName -Detail "未安装或版本过低；用户未确认安装，已继续后续流程"
        }
        return
    }

    $gameBarProcesses = @(
        Get-Process -Name "GameBar", "GameBarFTServer", "GameBarPresenceWriter" -ErrorAction SilentlyContinue
    )
    if ($gameBarProcesses.Count -gt 0) {
        try {
            Write-InstallLog ("Stopping Xbox Game Bar processes before prerequisite installation: {0}" -f (($gameBarProcesses | ForEach-Object { "$($_.ProcessName)#$($_.Id)" }) -join ", "))
            $gameBarProcesses | Stop-Process -Force
            Start-Sleep -Milliseconds 800
        }
        catch {
            Add-InstallResult -Status Warning -Item "关闭 Xbox Game Bar" -Detail ((Get-ErrorReason $_) + "；仍会继续安装依赖")
        }
    }

    foreach ($prerequisite in ($Prerequisites | Sort-Object Order)) {
        if (Test-PrerequisiteInstalled -Prerequisite $prerequisite) {
            Write-InstallLog ("Prerequisite already satisfies requirement: {0}" -f $prerequisite.PackageName)
            $installed = Get-InstalledPrerequisitePackage -Prerequisite $prerequisite
            Add-InstallResult -Status Success -Item $prerequisite.ChineseDisplayName -Detail ("已安装，版本 {0}" -f $installed.Version)
            continue
        }

        $packagePath = Join-Path $PrerequisiteRoot $prerequisite.FileName
        if (-not (Test-Path -LiteralPath $packagePath -PathType Leaf)) {
            Add-InstallResult -Status Error -Item $prerequisite.ChineseDisplayName -Detail "离线安装包不存在：$packagePath"
            continue
        }

        try {
            Write-InstallLog ("Installing prerequisite {0}/{1}: {2} (minimum {3})" -f $prerequisite.Order, $Prerequisites.Count, $prerequisite.PackageName, $prerequisite.MinimumVersion)
            Add-AppxPackageCompat -PackagePath $packagePath -ForceUpdate

            if (-not (Test-PrerequisiteInstalled -Prerequisite $prerequisite)) {
                throw "安装命令执行完成，但没有检测到满足最低版本 $($prerequisite.MinimumVersion) 的组件"
            }

            $installed = Get-InstalledPrerequisitePackage -Prerequisite $prerequisite
            Write-InstallLog ("Prerequisite validated: {0} {1}" -f $prerequisite.PackageName, $installed.Version)
            Add-InstallResult -Status Success -Item $prerequisite.ChineseDisplayName -Detail ("安装成功，版本 {0}" -f $installed.Version)
        }
        catch {
            Add-InstallResult -Status Error -Item $prerequisite.ChineseDisplayName -Detail ((Get-ErrorReason $_) + "；已继续安装下一个组件")
        }
    }

    Write-InstallLog "Required component pass finished. Any individual failures were preserved and the installer continued."
}

function Install-OverlayPackage {
    Write-InstallLog "Install script root: $ScriptRoot"
    Write-InstallLog "Overlay root: $OverlayRoot"
    Write-InstallLog "PowerShell: $($PSVersionTable.PSVersion)"
    Write-InstallLog "OS: $([System.Environment]::OSVersion.VersionString)"
    Write-InstallLog "Process bitness: Is64BitProcess=$([System.Environment]::Is64BitProcess); Is64BitOS=$([System.Environment]::Is64BitOperatingSystem)"

    if (-not (Test-Path $OverlayRoot)) {
        throw "OverlayPackage was not found under $ScriptRoot"
    }

    $processNames = @(
        "cskillconfirm",
        "TestXboxGameBar",
        "KillConfirmOverlay",
        "KillConfirmGameBar",
        "GameBar",
        "GameBarFTServer",
        "GameBarPresenceWriter"
    )
    $runningProcesses = @(Get-Process -Name $processNames -ErrorAction SilentlyContinue)
    if ($runningProcesses.Count -gt 0) {
        try {
            Write-InstallLog ("Stopping running processes: {0}" -f (($runningProcesses | ForEach-Object { "$($_.ProcessName)#$($_.Id)" }) -join ", "))
            $runningProcesses | Stop-Process -Force
        }
        catch {
            Add-InstallResult -Status Warning -Item "关闭正在运行的旧程序" -Detail ((Get-ErrorReason $_) + "；仍会继续安装")
        }
    }
    else {
        Write-InstallLog "No Kill Confirm/Game Bar processes needed stopping."
    }
    Start-Sleep -Milliseconds 800

    $msix = Get-ChildItem -LiteralPath $OverlayRoot -Filter "*.msix" -File | Select-Object -First 1
    if (-not $msix) {
        throw "MSIX package was not found under $OverlayRoot"
    }

    $cert = Get-ChildItem -LiteralPath $OverlayRoot -Filter "*.cer" -File | Select-Object -First 1
    if ($cert) {
        Write-InstallLog "Installing package certificate: $($cert.FullName)"
        $certificateResult = Import-PackageCertificate -CertificatePath $cert.FullName
        if ($certificateResult.ImportedCount -gt 0) {
            Add-InstallResult -Status Success -Item "Kill Confirm 包签名证书" -Detail ("已写入 {0} 个证书库" -f $certificateResult.ImportedCount)
        }
        else {
            Add-InstallResult -Status Error -Item "Kill Confirm 包签名证书" -Detail ("所有证书库均导入失败：{0}" -f $certificateResult.LastFailure)
        }
    }
    else {
        Write-InstallLog "No package certificate found beside MSIX."
        Add-InstallResult -Status Error -Item "Kill Confirm 包签名证书" -Detail "主程序旁边没有找到 .cer 证书文件"
    }

    $dependencies = @()
    $dependencyRoot = Join-Path $OverlayRoot "Dependencies\x64"
    if (Test-Path $dependencyRoot) {
        $dependencies = @(Get-ChildItem -LiteralPath $dependencyRoot -Include "*.appx", "*.msix" -File -Recurse | ForEach-Object { $_.FullName })
    }
    Write-InstallLog "Dependency root: $dependencyRoot"
    Write-InstallLog "Dependency count: $($dependencies.Count)"

    foreach ($dependency in $dependencies) {
        $dependencyName = Split-Path -Leaf $dependency
        $identity = Get-AppxIdentityFromPackageFile -PackagePath $dependency
        if ($identity) {
            Write-InstallLog "Dependency identity: $dependencyName => $($identity.Name) $($identity.Version)"
        }
        if (Test-AppxPackageInstalled -PackagePath $dependency) {
            Write-InstallLog "Dependency already installed: $dependencyName"
            Add-InstallResult -Status Success -Item ("主程序依赖：{0}" -f $dependencyName) -Detail "已安装且版本满足要求"
            continue
        }

        try {
            Write-InstallLog "Installing dependency: $dependencyName"
            Add-AppxPackageCompat -PackagePath $dependency
            if (-not (Test-AppxPackageInstalled -PackagePath $dependency)) {
                throw "安装命令完成后验证失败"
            }
            Add-InstallResult -Status Success -Item ("主程序依赖：{0}" -f $dependencyName) -Detail "安装并验证成功"
        }
        catch {
            Add-InstallResult -Status Error -Item ("主程序依赖：{0}" -f $dependencyName) -Detail ((Get-ErrorReason $_) + "；已继续安装其他依赖和主程序")
        }
    }

    Write-InstallLog "Installing MSIX package: $($msix.Name)"
    $msixIdentity = Get-AppxIdentityFromPackageFile -PackagePath $msix.FullName
    if ($msixIdentity) {
        Write-InstallLog "MSIX identity: $($msixIdentity.Name) $($msixIdentity.Version)"

        $installedMsix = Get-AppxPackage -Name $msixIdentity.Name -ErrorAction SilentlyContinue |
            Sort-Object Version -Descending |
            Select-Object -First 1
        if ($installedMsix) {
            $installedVersion = [version]$installedMsix.Version
            $installedStatus = [string]$installedMsix.Status
            Write-InstallLog "Installed MSIX detected: $installedVersion; Status=$installedStatus; Location=$($installedMsix.InstallLocation)"
            if ($installedVersion -eq $msixIdentity.Version -and $installedStatus -eq "Ok") {
                Write-InstallLog "The same healthy MSIX version is already registered. Skipping package replacement and continuing repair steps."
                Add-InstallResult -Status Success -Item "Kill Confirm Overlay 主程序" -Detail ("版本 {0} 已安装且状态正常；继续执行修复步骤" -f $installedVersion)
                return
            }
        }
    }
    Add-AppxPackageCompat -PackagePath $msix.FullName -ForceUpdate -DeferWhenInUse -UseSystemVolume
    $installedPackage = Get-InstalledOverlayPackage
    Add-InstallResult -Status Success -Item "Kill Confirm Overlay 主程序" -Detail ("安装成功，版本 {0}" -f $installedPackage.Version)
}

function Test-OverlayPackageInstalled {
    $package = Update-InstalledPackageContext

    Write-InstallLog "MSIX package registered: $($package.PackageFamilyName)"
    Write-InstallLog "Package full name: $($package.PackageFullName)"
    Write-InstallLog "Install location: $($package.InstallLocation)"
    Write-InstallLog "Runtime logs: $RuntimeLogRoot"
}

function Get-CounterStrikeInstallRoot {
    # Follow steamlocate-rs: locate Steam, enumerate its libraries, then
    # resolve app 730 from appmanifest_730.acf instead of guessing a localized
    # or user-selected game folder name. If registry data is missing, try only
    # safe Steam roots: standard install folders and a running steam.exe.
    $steamRoots = New-Object System.Collections.Generic.List[string]
    foreach ($registryPath in @(
        "HKLM:\Software\WOW6432Node\Valve\Steam",
        "HKLM:\Software\Valve\Steam",
        "HKCU:\Software\Valve\Steam"
    )) {
        try {
            $steam = Get-ItemProperty -Path $registryPath -ErrorAction Stop
            foreach ($property in @("InstallPath", "SteamPath")) {
                $candidate = $steam.$property
                if ($candidate -and (Test-Path -LiteralPath $candidate -PathType Container)) {
                    $fullPath = [System.IO.Path]::GetFullPath(($candidate -replace "/", "\"))
                    if (-not $steamRoots.Contains($fullPath)) {
                        $steamRoots.Add($fullPath)
                    }
                }
            }
        }
        catch {
        }
    }

    foreach ($candidate in @(
        $(if (${env:ProgramFiles(x86)}) { Join-Path ${env:ProgramFiles(x86)} "Steam" }),
        $(if ($env:ProgramFiles) { Join-Path $env:ProgramFiles "Steam" }),
        $(if ($env:LOCALAPPDATA) { Join-Path $env:LOCALAPPDATA "Programs\Steam" })
    )) {
        if ($candidate -and (Test-Path -LiteralPath $candidate -PathType Container)) {
            $fullPath = [System.IO.Path]::GetFullPath($candidate)
            if (-not $steamRoots.Contains($fullPath)) {
                $steamRoots.Add($fullPath)
            }
        }
    }

    try {
        $runningSteam = Get-Process -Name steam -ErrorAction Stop | Select-Object -First 1
        if ($runningSteam.Path) {
            $candidate = Split-Path -Parent $runningSteam.Path
            $fullPath = [System.IO.Path]::GetFullPath($candidate)
            if ((Test-Path -LiteralPath $fullPath -PathType Container) -and -not $steamRoots.Contains($fullPath)) {
                $steamRoots.Add($fullPath)
                Write-InstallLog "Running steam.exe fallback root: $fullPath"
            }
        }
    }
    catch {
    }

    if ($steamRoots.Count -eq 0) {
        Write-InstallLog "Steam was not found in the registry, standard folders, or a running steam.exe process."
        return $null
    }

    $libraries = New-Object System.Collections.Generic.List[string]
    foreach ($steamRoot in $steamRoots) {
        if (-not $libraries.Contains($steamRoot)) {
            $libraries.Add($steamRoot)
        }
        $libraryFolders = Join-Path $steamRoot "steamapps\libraryfolders.vdf"
        if (Test-Path -LiteralPath $libraryFolders -PathType Leaf) {
            foreach ($line in Get-Content -LiteralPath $libraryFolders -ErrorAction SilentlyContinue) {
                if ($line -match '^\s*"path"\s+"([^"]+)"') {
                    $candidate = $matches[1] -replace "\\\\", "\"
                    if (Test-Path -LiteralPath $candidate -PathType Container) {
                        $fullPath = [System.IO.Path]::GetFullPath($candidate)
                        if (-not $libraries.Contains($fullPath)) {
                            $libraries.Add($fullPath)
                        }
                    }
                }
            }
        }
    }

    foreach ($library in $libraries) {
        $manifestPath = Join-Path $library "steamapps\appmanifest_730.acf"
        if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
            continue
        }

        $manifestText = Get-Content -LiteralPath $manifestPath -Raw -ErrorAction SilentlyContinue
        if ($manifestText -and $manifestText -match '(?im)^\s*"installdir"\s+"([^"]+)"') {
            $installRoot = Join-Path $library ("steamapps\common\{0}" -f $matches[1])
            if (Test-Path -LiteralPath $installRoot -PathType Container) {
                $resolved = [System.IO.Path]::GetFullPath($installRoot)
                Write-InstallLog "steamlocate-style app 730 root: $resolved"
                return $resolved
            }
        }
    }

    Write-InstallLog "steamlocate-style lookup did not find appmanifest_730.acf in any Steam library."
    return $null
}

function Install-Cs2GsiConfig {
    $configLines = @(
        '"KillConfirmGameBar"',
        '{',
        ' "uri" "http://127.0.0.1:10087/"',
        ' "timeout" "0.5"',
        ' "buffer"  "0.05"',
        ' "throttle" "0.05"',
        ' "heartbeat" "15.0"',
        ' "auth"',
        ' {',
        '   "token" "killconfirm"',
        ' }',
        ' "data"',
        ' {',
        '   "provider"           "1"',
        '   "map"                "1"',
        '   "round"              "1"',
        '   "bomb"               "1"',
        '   "player_id"          "1"',
        '   "player_state"       "1"',
        '   "player_weapons"     "1"',
        '   "player_match_stats" "1"',
        ' }',
        '}'
    )

    $installed = $false
    $installRoot = Get-CounterStrikeInstallRoot
    if ($installRoot) {
        $cfgRoot = Join-Path $installRoot "game\csgo\cfg"
        if (Test-Path -LiteralPath $cfgRoot -PathType Container) {
        $cfgPath = Join-Path $cfgRoot "gamestate_integration_killconfirm.cfg"
        Set-Content -LiteralPath $cfgPath -Value $configLines -Encoding ASCII
        Write-InstallLog "CS2 GSI config installed: $cfgPath"
        Add-InstallResult -Status Success -Item "CS2 GSI 配置" -Detail "已写入：$cfgPath"
        $installed = $true
        }
    }

    if (-not $installed) {
        Write-Warning "CS2 cfg folder was not found. If kill events do not trigger, install gamestate_integration_killconfirm.cfg manually."
        Add-InstallResult -Status Warning -Item "CS2 GSI 配置" -Detail "没有找到 CS2 的 game\csgo\cfg 目录；可在插件内稍后配置"
    }

    $runningCs2 = @(Get-Process -Name "cs2" -ErrorAction SilentlyContinue)
    if ($runningCs2.Count -gt 0) {
        $message = "CS2 is currently running. Close and reopen CS2 so it reloads gamestate_integration_killconfirm.cfg."
        Write-InstallLog $message
        Write-Warning $message
    }
}

try {
    try {
        if (Test-Path $LogPath) {
            Remove-Item -LiteralPath $LogPath -Force
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
        Add-InstallResult -Status Warning -Item "外部前置依赖" -Detail "无依赖更新版按设计跳过检测与离线安装"
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
'@

$Readme = @'
KillConfirmGameBar transfer package

What is inside:
- OverlayPackage: the Xbox Game Bar MSIX package and its dependencies
- Prerequisites: offline Microsoft UI XAML, VCLibs, and Xbox Game Bar packages
- Install-KillConfirm.ps1: one-click install script

Use on another PC:
1. Right-click Install-KillConfirm.ps1 and run with PowerShell
2. Press Win+G
3. Launch Kill Confirm Overlay from Xbox Game Bar
4. Use the panel power button or Check button if you want to verify status

Notes:
- Before installing the overlay, the install script detects Microsoft UI XAML 2.8, the two required x64 VCLibs packages, and Xbox Game Bar. Missing or outdated components are shown to the user and installed in the required order after approval.
- If Xbox Game Bar is still missing after offline prerequisite handling, the installer opens its Microsoft Store page and asks the user to run setup again after installation.
- The companion service is embedded inside the MSIX package.
- The widget starts its packaged companion service directly from the installed app.
- The install script installs the MSIX package directly instead of requiring Visual Studio developer scripts.
- The install script tries to create CS2's gamestate_integration_killconfirm.cfg automatically.
- The widget talks to 127.0.0.1 internally, so the install script adds the required loopback exemption.
- If Xbox Game Bar was open during install, close it and open it again.
- The installer does not auto-open the widget URI because some Windows installs do not register ms-gamebarwidget links.
- Package family name for loopback: KillConfirmGameBar.Overlay_5jgcw66eyez0m

KillConfirmGameBar transfer package - Chinese quick guide

1. Right-click Install-KillConfirm.ps1 and choose Run with PowerShell.
2. Press Win+G.
3. Open Kill Confirm Overlay in Xbox Game Bar.
4. If the status is not green, use the panel Check button and the CFG check area.
'@

$InstallScriptWithDependencies = $InstallScript.Replace("__INSTALL_PREREQUISITES__", '$true')
$InstallScriptWithoutDependencies = $InstallScript.Replace("__INSTALL_PREREQUISITES__", '$false')

function Write-Utf8BomTextFile {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Value
    )

    # Windows PowerShell 5.1 treats UTF-8 without a BOM as the active ANSI
    # code page. The generated installer script contains Chinese diagnostics,
    # so a missing BOM can corrupt quoted strings and make the script fail to
    # parse before it is able to create its log.
    $utf8Bom = New-Object System.Text.UTF8Encoding($true)
    [System.IO.File]::WriteAllText($Path, $Value, $utf8Bom)
}

$WithDependenciesInstallScriptPath = Join-Path $TransferRoot "Install-KillConfirm.ps1"
Write-Utf8BomTextFile -Path $WithDependenciesInstallScriptPath -Value $InstallScriptWithDependencies
Set-Content -LiteralPath (Join-Path $TransferRoot "README.txt") -Value $Readme -Encoding UTF8

Compress-Archive -Path (Join-Path $TransferRoot "*") -DestinationPath $TransferZip -Force

New-Item -ItemType Directory -Force -Path $NoDependenciesTransferRoot | Out-Null
Get-ChildItem -LiteralPath $TransferRoot -Force | Copy-Item -Destination $NoDependenciesTransferRoot -Recurse -Force

$NoDependenciesPrerequisiteRoot = Join-Path $NoDependenciesTransferRoot "Prerequisites"
if (Test-Path -LiteralPath $NoDependenciesPrerequisiteRoot) {
    Remove-Item -LiteralPath $NoDependenciesPrerequisiteRoot -Recurse -Force
}

$NoDependenciesMsixDependencyRoot = Join-Path $NoDependenciesTransferRoot "OverlayPackage\Dependencies"
if (Test-Path -LiteralPath $NoDependenciesMsixDependencyRoot) {
    Remove-Item -LiteralPath $NoDependenciesMsixDependencyRoot -Recurse -Force
}

$WithoutDependenciesInstallScriptPath = Join-Path $NoDependenciesTransferRoot "Install-KillConfirm.ps1"
Write-Utf8BomTextFile -Path $WithoutDependenciesInstallScriptPath -Value $InstallScriptWithoutDependencies

foreach ($generatedInstallScriptPath in @($WithDependenciesInstallScriptPath, $WithoutDependenciesInstallScriptPath)) {
    $scriptBytes = [System.IO.File]::ReadAllBytes($generatedInstallScriptPath)
    if ($scriptBytes.Length -lt 3 -or $scriptBytes[0] -ne 0xEF -or $scriptBytes[1] -ne 0xBB -or $scriptBytes[2] -ne 0xBF) {
        throw "Generated PowerShell install script is missing its UTF-8 BOM: $generatedInstallScriptPath"
    }

    $parseTokens = $null
    $parseErrors = $null
    [System.Management.Automation.Language.Parser]::ParseFile(
        $generatedInstallScriptPath,
        [ref]$parseTokens,
        [ref]$parseErrors) | Out-Null
    if ($parseErrors.Count -gt 0) {
        $parseSummary = ($parseErrors | ForEach-Object { "line $($_.Extent.StartLineNumber): $($_.Message)" }) -join "; "
        throw "Generated PowerShell install script failed validation: $generatedInstallScriptPath ($parseSummary)"
    }
}

$NoDependenciesReadme = $Readme + @'

Dependency-free edition:
- Microsoft UI XAML, VCLibs, and Xbox Game Bar packages are not included.
- The installer does not detect, prompt for, or install prerequisites.
- Use this edition only when the required components are already installed.
'@
Set-Content -LiteralPath (Join-Path $NoDependenciesTransferRoot "README.txt") -Value $NoDependenciesReadme -Encoding UTF8
Compress-Archive -Path (Join-Path $NoDependenciesTransferRoot "*") -DestinationPath $NoDependenciesTransferZip -Force

$resolvedAppPackagesRoot = [System.IO.Path]::GetFullPath($AppPackagesRoot)
if ($resolvedAppPackagesRoot.StartsWith($resolvedWorkspaceRoot, [System.StringComparison]::OrdinalIgnoreCase) -and (Test-Path $AppPackagesRoot)) {
    Get-ChildItem -LiteralPath $AppPackagesRoot -Force | ForEach-Object {
        Remove-Item -LiteralPath $_.FullName -Recurse -Force
    }
}

Write-Host ""
Write-Host ("Transfer folder: {0}" -f $TransferRoot)
Write-Host ("Transfer zip:    {0}" -f $TransferZip)
Write-Host ("No-deps folder:  {0}" -f $NoDependenciesTransferRoot)
Write-Host ("No-deps zip:     {0}" -f $NoDependenciesTransferZip)
Write-Host ("MSIX package:    {0}" -f (Join-Path $OverlayTransferRoot $PackageFileName))
Write-Host ("Package family:  {0}" -f $ExpectedPackageFamilyName)

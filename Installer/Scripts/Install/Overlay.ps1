# Overlay process shutdown, package installation, and verification.
function Get-OverlayRuntimeProcesses {
    $processNames = @(
        "cskillconfirm",
        "TestXboxGameBar",
        "KillConfirmOverlay",
        "KillConfirmGameBar",
        "GameBar",
        "GameBarFTServer",
        "GameBarPresenceWriter",
        "GameBarElevatedFT_Alias"
    )
    return @(Get-Process -Name $processNames -ErrorAction SilentlyContinue)
}

function Stop-OverlayRuntimeForUpdate {
    $lastFailures = New-Object System.Collections.Generic.List[string]
    for ($attempt = 1; $attempt -le 5; $attempt++) {
        $runningProcesses = @(Get-OverlayRuntimeProcesses)
        if ($runningProcesses.Count -eq 0) {
            if ($attempt -eq 1) {
                Write-InstallLog "No Kill Confirm/Game Bar processes needed stopping."
            }
            else {
                Write-InstallLog "Kill Confirm and Xbox Game Bar processes are fully stopped."
            }
            Add-InstallResult -Status Success -Item "关闭正在运行的旧程序" -Detail "已确认小组件、后台服务和 Xbox Game Bar 均已退出"
            return
        }

        $runningText = ($runningProcesses | ForEach-Object { "$($_.ProcessName)#$($_.Id)" }) -join ", "
        Write-InstallLog ("Stopping running processes (attempt {0}/5): {1}" -f $attempt, $runningText)
        foreach ($process in $runningProcesses) {
            try {
                Stop-Process -Id $process.Id -Force -ErrorAction Stop
            }
            catch {
                $lastFailures.Add(("{0}#{1}: {2}" -f $process.ProcessName, $process.Id, (Get-ErrorReason $_)))
            }
        }
        Start-Sleep -Milliseconds 800
    }

    $remaining = @(Get-OverlayRuntimeProcesses)
    if ($remaining.Count -gt 0) {
        $remainingText = ($remaining | ForEach-Object { "$($_.ProcessName)#$($_.Id)" }) -join ", "
        $failureText = if ($lastFailures.Count -gt 0) {
            "；停止失败：" + (($lastFailures | Select-Object -Unique) -join "；")
        }
        else {
            ""
        }
        throw "无法完全关闭旧版小组件或 Xbox Game Bar（仍在运行：$remainingText）$failureText。安装器将继续尝试延迟更新。"
    }
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

    $deferMainUpdate = $false
    try {
        Stop-OverlayRuntimeForUpdate
    }
    catch {
        # A stale widget or Game Bar process should not prevent the installer
        # from attempting the actual MSIX update. AppX deployment will report
        # its own failure if the package is still in use.
        $deferMainUpdate = $true
        Add-InstallResult -Status Warning -Item "关闭正在运行的旧程序" -Detail ((Get-ErrorReason $_) + "；仍会继续尝试更新主程序")
        Write-InstallLog "旧版进程未能完全退出；继续尝试安装新版 MSIX，并在系统支持时延迟注册。"
    }

    $packageFile = Get-ChildItem -LiteralPath $OverlayRoot -Filter "*.msixbundle" -File | Select-Object -First 1
    if (-not $packageFile) {
        # Compatibility fallback for legacy development payloads. Official
        # upgrades always carry a bundle so a bundle-managed install remains
        # on the same Windows deployment chain.
        $packageFile = Get-ChildItem -LiteralPath $OverlayRoot -Filter "*.msix" -File | Select-Object -First 1
    }
    if (-not $packageFile) {
        throw "MSIX Bundle package was not found under $OverlayRoot"
    }

    try {
        $cert = Get-ChildItem -LiteralPath $OverlayRoot -Filter "*.cer" -File | Select-Object -First 1
        if ($cert) {
            Write-InstallLog "Installing package certificate: $($cert.FullName)"
            $certificateResult = Import-PackageCertificate -CertificatePath $cert.FullName
            if ($certificateResult.ImportedCount -gt 0) {
                Add-InstallResult -Status Success -Item "Kill Confirm 包签名证书" -Detail ("已写入 {0} 个 TrustedPeople 证书库" -f $certificateResult.ImportedCount)
            }
            else {
                Add-InstallResult -Status Error -Item "Kill Confirm 包签名证书" -Detail ("所有 TrustedPeople 证书库均导入失败：{0}" -f $certificateResult.LastFailure)
            }
        }
        else {
            Write-InstallLog "No package certificate found beside the MSIX Bundle."
            Add-InstallResult -Status Error -Item "Kill Confirm 包签名证书" -Detail "主程序旁边没有找到 .cer 证书文件"
        }
    }
    catch {
        Add-InstallResult -Status Error -Item "Kill Confirm 包签名证书" -Detail ((Get-ErrorReason $_) + "；仍会继续尝试安装主程序")
    }

    $dependencies = @()
    $dependencyRoot = Join-Path $OverlayRoot "Dependencies\x64"
    try {
        if (Test-Path $dependencyRoot) {
            $dependencies = @(Get-ChildItem -LiteralPath $dependencyRoot -Include "*.appx", "*.msix" -File -Recurse | ForEach-Object { $_.FullName })
        }
    }
    catch {
        Add-InstallResult -Status Error -Item "枚举主程序依赖" -Detail ((Get-ErrorReason $_) + "；仍会继续尝试安装主程序")
        $dependencies = @()
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

    Write-InstallLog "Installing AppX package: $($packageFile.Name)"
    $packageIdentity = Get-AppxIdentityFromPackageFile -PackagePath $packageFile.FullName
    if ($packageIdentity) {
        Write-InstallLog "Package identity: $($packageIdentity.Name) $($packageIdentity.Version)"

        $installedMsix = Get-AppxPackage -Name $packageIdentity.Name -ErrorAction SilentlyContinue |
            Sort-Object Version -Descending |
            Select-Object -First 1
        if ($installedMsix) {
            $installedVersion = [version]$installedMsix.Version
            $installedStatus = [string]$installedMsix.Status
            Write-InstallLog "Installed MSIX detected: $installedVersion; Status=$installedStatus; Location=$($installedMsix.InstallLocation)"
            if ($installedVersion -gt $packageIdentity.Version) {
                throw "检测到更高版本 $installedVersion；当前安装包版本为 $($packageIdentity.Version)，已拒绝降级。"
            }
            if ($installedVersion -eq $packageIdentity.Version -and $installedStatus -eq "Ok") {
                Write-InstallLog "The same healthy MSIX version is already registered. Skipping package replacement and continuing repair steps."
                Add-InstallResult -Status Success -Item "Kill Confirm Overlay 主程序" -Detail ("版本 {0} 已安装且状态正常；继续执行修复步骤" -f $installedVersion)
                return
            }
        }
    }
    Add-AppxPackageCompat `
        -PackagePath $packageFile.FullName `
        -ForceUpdate `
        -DeferWhenInUse:$deferMainUpdate
    $installedPackage = Get-InstalledOverlayPackage
    if ($packageIdentity) {
        $installedVersion = [version]$installedPackage.Version
        if ($installedVersion -lt $packageIdentity.Version) {
            if ($deferMainUpdate) {
                Add-InstallResult -Status Warning -Item "Kill Confirm Overlay 主程序" -Detail ("新版 {0} 已提交延迟更新；当前仍为 {1}。请关闭 Xbox Game Bar，必要时重启 Windows 后完成更新" -f $packageIdentity.Version, $installedVersion)
                return
            }
            throw "MSIX Bundle 安装命令执行完成，但已注册版本仍为 $installedVersion，目标版本为 $($packageIdentity.Version)。"
        }
    }
    $serviceExecutable = Join-Path $installedPackage.InstallLocation "KillConfirmService\cskillconfirm.exe"
    if (-not (Test-Path -LiteralPath $serviceExecutable -PathType Leaf)) {
        throw "MSIX 已注册，但后台服务文件不存在：$serviceExecutable"
    }
    Add-InstallResult -Status Success -Item "Kill Confirm Overlay 主程序" -Detail ("安装成功，版本 {0}" -f $installedPackage.Version)
}

function Test-OverlayPackageInstalled {
    $package = Update-InstalledPackageContext

    Write-InstallLog "MSIX package registered: $($package.PackageFamilyName)"
    Write-InstallLog "Package full name: $($package.PackageFullName)"
    Write-InstallLog "Install location: $($package.InstallLocation)"
    Write-InstallLog "Runtime logs: $RuntimeLogRoot"
}

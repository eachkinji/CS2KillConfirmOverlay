# Xbox Game Bar detection and environment repair.
function Test-XboxGameBarAvailable {
    $requirement = $Prerequisites |
        Where-Object PackageName -eq "Microsoft.XboxGamingOverlay" |
        Select-Object -First 1
    return $null -ne $requirement -and (Test-PrerequisiteInstalled -Prerequisite $requirement)
}

function Confirm-XboxGameBarAvailable {
    $requirement = $Prerequisites |
        Where-Object PackageName -eq "Microsoft.XboxGamingOverlay" |
        Select-Object -First 1
    $gameBar = if ($requirement) {
        Get-InstalledPrerequisitePackage -Prerequisite $requirement
    }
    else {
        $null
    }

    if (Test-XboxGameBarAvailable) {
        Write-InstallLog "Xbox Game Bar package is available: $($gameBar.Version); Status=$($gameBar.Status)"
        return
    }

    if ($gameBar) {
        Write-InstallLog "Xbox Game Bar is installed but does not satisfy requirements: Version=$($gameBar.Version); Status=$($gameBar.Status); Minimum=$($requirement.MinimumVersion)"
    }
    Write-InstallLog "Xbox Game Bar is still unavailable after prerequisite handling. Opening Microsoft Store fallback."
    try {
        Start-Process "ms-windows-store://pdp/?ProductId=9NZKPSTSNW4P" | Out-Null
    }
    catch {
        Write-InstallLog "Could not open the Xbox Game Bar Microsoft Store page: $($_.Exception.Message)"
    }

    throw "Xbox Game Bar is missing, damaged, or older than required version $($requirement.MinimumVersion). Update it from Microsoft Store, then run this installer again."
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
        $regPath = Get-SystemToolPath "reg.exe"
        $exportedCount = 0
        foreach ($export in $exports) {
            & $regPath export $export.Key (Join-Path $backupRoot $export.File) /y 2>$null | Out-Null
            if ($LASTEXITCODE -eq 0) {
                $exportedCount++
            }
        }
        if ($exportedCount -eq $exports.Count) {
            Add-InstallResult -Status Success -Item "Game Bar 注册表备份" -Detail ("已备份 {0} 项到：{1}" -f $exportedCount, $backupRoot)
        }
        else {
            Add-InstallResult -Status Warning -Item "Game Bar 注册表备份" -Detail ("仅成功备份 {0}/{1} 项到：{2}；修复仍会继续" -f $exportedCount, $exports.Count, $backupRoot)
        }
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
        # 属性不存在时 Get-ItemPropertyValue 会抛 E_INVALIDARG (0x80070057)，
        # 即使带 -ErrorAction SilentlyContinue 也会被 catch 成 Warning。
        # 用 Get-ItemProperty 检查属性是否存在，缺失即"未检测到"（正常）。
        $layoutKey = Get-ItemProperty `
            -LiteralPath "HKLM:\SYSTEM\CurrentControlSet\Control\Keyboard Layout" `
            -ErrorAction SilentlyContinue
        $hasScancodeMap = $null -ne $layoutKey -and `
            $null -ne $layoutKey.PSObject.Properties["Scancode Map"]
        if ($hasScancodeMap) {
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
        $gpresultPath = Get-SystemToolPath "gpresult.exe"
        if (-not (Test-Path -LiteralPath $gpresultPath -PathType Leaf)) {
            Add-InstallResult -Status Warning -Item "实际生效组策略报告" -Detail "未找到 gpresult.exe 工具；该报告仅用于诊断，不影响安装和程序运行"
        }
        else {
            # Do not pipe gpresult through PowerShell's native-command stream. Some
            # Windows builds surface its diagnostic output as a misleading
            # 0x80131501 "invalid pointer" PowerShell exception.
            $gpresultProcess = Start-Process `
                -FilePath $gpresultPath `
                -ArgumentList @("/h", ('"{0}"' -f $policyReportPath), "/f") `
                -WindowStyle Hidden `
                -Wait `
                -PassThru `
                -ErrorAction Stop
            $gpresultExitCode = $gpresultProcess.ExitCode
            if ($gpresultExitCode -eq 0 -and (Test-Path -LiteralPath $policyReportPath -PathType Leaf)) {
                Add-InstallResult -Status Success -Item "实际生效组策略报告" -Detail "已保存：$policyReportPath"
            }
            else {
                Add-InstallResult -Status Warning -Item "实际生效组策略报告" -Detail "gpresult 返回退出码 $gpresultExitCode；该报告仅用于诊断，不影响安装和程序运行"
            }
        }
    }
    catch {
        Add-InstallResult -Status Warning -Item "实际生效组策略报告" -Detail ((Get-ErrorReason $_) + "；该报告仅用于诊断，不影响安装和程序运行")
    }
}

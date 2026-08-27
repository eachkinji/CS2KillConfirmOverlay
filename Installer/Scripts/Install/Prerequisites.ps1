# Offline prerequisite definitions and installation.
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
    },
    [pscustomobject]@{
        Order = 5
        DisplayName = "Microsoft .NET Native Framework 2.2 (x64)"
        ChineseDisplayName = (ConvertFrom-Utf8Base64 -Value "TWljcm9zb2Z0IC5ORVQgTmF0aXZlIOahhuaetiAyLjIgKHg2NCk=")
        PackageName = "Microsoft.NET.Native.Framework.2.2"
        Architecture = "X64"
        MinimumVersion = [version]"2.2.29512.0"
        FileName = "Microsoft.NET.Native.Framework.2.2.x64.appx"
    },
    [pscustomobject]@{
        Order = 6
        DisplayName = "Microsoft .NET Native Runtime 2.2 (x64)"
        ChineseDisplayName = (ConvertFrom-Utf8Base64 -Value "TWljcm9zb2Z0IC5ORVQgTmF0aXZlIOi/kOihjOaXtiAyLjIgKHg2NCk=")
        PackageName = "Microsoft.NET.Native.Runtime.2.2"
        Architecture = "X64"
        MinimumVersion = [version]"2.2.28604.0"
        FileName = "Microsoft.NET.Native.Runtime.2.2.x64.appx"
    }
)

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
    $installedStatus = [string]$installed.Status
    if ($installedStatus -and $installedStatus -ne "Ok") {
        return $false
    }
    $installedStatus = [string]$installed.Status
    if ($installedStatus -and $installedStatus -ne "Ok") {
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
    param([switch]$Confirmed)

    Write-InstallLog "Checking required Microsoft UI XAML, VCLibs, .NET Native, and Xbox Game Bar packages..."
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
        Write-InstallLog "All required Microsoft UI XAML, VCLibs, .NET Native, and Xbox Game Bar packages are already installed."
        foreach ($prerequisite in ($Prerequisites | Sort-Object Order)) {
            $installed = Get-InstalledPrerequisitePackage -Prerequisite $prerequisite
            Add-InstallResult -Status Success -Item $prerequisite.ChineseDisplayName -Detail ("已安装，版本 {0}" -f $installed.Version)
        }
        return
    }

    $installationApproved = [bool]$Confirmed
    if ($installationApproved) {
        Write-InstallLog "Prerequisite installation was already confirmed in the outer setup manager. Skipping the secondary confirmation dialog."
    }
    else {
        try {
            $installationApproved = Confirm-PrerequisiteInstall -MissingPrerequisites $missingPrerequisites
        }
        catch {
            Add-InstallResult -Status Error -Item "依赖安装确认窗口" -Detail (Get-ErrorReason $_)
            $installationApproved = $false
        }
    }
    if (-not $installationApproved) {
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
            Write-InstallLog "Windows 正在部署该组件，较慢的电脑可能需要几分钟。请不要关闭安装进度窗口。"
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

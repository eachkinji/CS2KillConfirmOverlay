function Get-DesktopRoot {
    $installed = [IO.Path]::GetFullPath((Join-Path $ScriptRoot '..\Desktop'))
    if (Test-Path -LiteralPath (Join-Path $installed 'KillConfirmCompatibility.exe')) { return $installed }
    return (Join-Path $ScriptRoot 'Desktop')
}

function Install-DesktopControlPanel {
    $desktop = Get-DesktopRoot
    foreach ($file in @('KillConfirmCompatibility.exe', 'KillConfirmCompatibility.dll', 'coreclr.dll', 'Microsoft.Graphics.Canvas.dll', 'KillConfirmService\cskillconfirm.exe')) {
        if (-not (Test-Path -LiteralPath (Join-Path $desktop $file) -PathType Leaf)) { throw "桌面主程序文件缺失：$file" }
    }
    $profile = Join-Path $env:LOCALAPPDATA 'KillConfirmOverlay\DesktopData'
    New-Item -ItemType Directory -Path $profile -Force | Out-Null
    [IO.File]::WriteAllText((Join-Path $profile 'desktop-location.txt'), (Join-Path $desktop 'KillConfirmCompatibility.exe'), [Text.UTF8Encoding]::new($false))
    $script:RuntimeLogRoot = $profile
    Add-InstallResult -Status Success -Item '桌面主程序' -Detail '控制面板和兼容显示已安装，可独立运行，无需 MSIX、商店或 Game Bar'
}

function Install-OptionalGameBarWidget {
    $optionalResultsStart = $InstallResults.Count
    try {
        if (-not (Test-XboxGameBarAvailable)) { throw '未安装 Xbox Game Bar' }
        $firewall = Get-Service -Name MpsSvc -ErrorAction SilentlyContinue
        if (-not $firewall -or $firewall.Status -ne 'Running') { throw 'Windows 防火墙服务不可用，跳过 MSIX 安装' }
        Install-OverlayPackage
        Test-OverlayPackageInstalled
        return $true
    }
    catch {
        # The desktop application is already installed. Optional MSIX errors
        # must not make the whole installer report that the main app failed.
        for ($optionalIndex = $optionalResultsStart; $optionalIndex -lt $InstallResults.Count; $optionalIndex++) {
            if ($InstallResults[$optionalIndex].Status -eq 'Error') { $InstallResults[$optionalIndex].Status = 'Warning' }
        }
        Add-InstallResult -Status Warning -Item 'Game Bar 可选组件' -Detail ((Get-ErrorReason $_) + '；兼容显示仍可正常使用，可稍后重新运行安装器补装')
        return $false
    }
}

function Enable-DesktopCompatibilityDefault {
    $folder = Join-Path $RuntimeLogRoot 'CompatibilityDisplay'
    $path = Join-Path $folder 'display.json'
    if (-not (Test-Path -LiteralPath $path)) {
        New-Item -ItemType Directory -Path $folder -Force | Out-Null
        $legacy = Join-Path $env:LOCALAPPDATA 'Packages\KillConfirmGameBar.Overlay_5jgcw66eyez0m\LocalState\CompatibilityDisplay\display.json'
        if (Test-Path -LiteralPath $legacy) { Copy-Item -LiteralPath $legacy -Destination $path; return }
        @{ Version = 2; Enabled = $true; FollowGame = $true; HideWhenInactive = $true; FramesPerSecond = 60; ScreenName = ''; Layouts = @{} } | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $path -Encoding UTF8
    }
}

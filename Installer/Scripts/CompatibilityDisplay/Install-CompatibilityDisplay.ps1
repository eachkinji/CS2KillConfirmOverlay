# Isolated first-install fallback. Existing display preferences are preserved.
function Initialize-CompatibilityDisplayFallback {
    try {
        $package = Get-AppxPackage -Name $PackageName -ErrorAction Stop | Sort-Object Version -Descending | Select-Object -First 1
        if (-not $package) { return }
        $folder = Join-Path $env:LOCALAPPDATA "Packages/$($package.PackageFamilyName)/LocalState/CompatibilityDisplay"
        $configuration = Join-Path $folder 'display.json'
        if (-not (Test-Path -LiteralPath $configuration)) {
            New-Item -ItemType Directory -Path $folder -Force | Out-Null
            @{ Version = 1; Enabled = $true; FollowGame = $true; HideWhenInactive = $true; ScreenName = ''; FramesPerSecond = 60; EditRequest = 0; TestRequest = 0; Layouts = @{} } |
                ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $configuration -Encoding UTF8
        }
        Add-InstallResult -Status Success -Item '兼容显示' -Detail '已准备兼容显示，可从开始菜单打开本程序调整布局'
    }
    catch { Add-InstallResult -Status Warning -Item '兼容显示' -Detail ('请在高级设置手动开启：' + (Get-ErrorReason $_)) }
}

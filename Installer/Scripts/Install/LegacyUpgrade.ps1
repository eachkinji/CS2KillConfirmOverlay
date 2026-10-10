# A split-version upgrade must retire the old packaged control panel even when
# the optional Game Bar components were deselected or cannot be deployed.
function Get-LegacyControlPanelPackage {
    foreach ($package in @(Get-AppxPackage -Name $PackageName -ErrorAction Stop)) {
        if (-not $package) {continue}
        if ($package.PackageFamilyName -ne 'KillConfirmGameBar.Overlay_5jgcw66eyez0m') {
            throw '旧版包身份不匹配，已停止自动迁移和卸载。'
        }
        if (-not (Test-Path -LiteralPath (Join-Path $package.InstallLocation 'Bridge/killconfirm-widget-bridge.exe') -PathType Leaf)) {
            return $package
        }
    }
    return $null
}

function Invoke-LegacyProfilePreparation {
    param($Package, [int]$TimeoutSeconds=600)
    $ordinaryRoot=Split-Path -Parent $ScriptRoot
    $profile=Join-Path $env:LOCALAPPDATA 'KillConfirmOverlay'
    New-Item -ItemType Directory -Path $profile -Force | Out-Null
    $requestPath=Join-Path $profile ('upgrade-request-'+[guid]::NewGuid().ToString('N')+'.json')
    $resultPath=$requestPath+'.result'
    $process=New-Object Diagnostics.Process
    try {
        @{PackageFullName=$Package.PackageFullName;InstallLocation=$Package.InstallLocation} |
            ConvertTo-Json | Set-Content -LiteralPath $requestPath -Encoding UTF8
        $process.StartInfo.FileName=Join-Path $ordinaryRoot 'KillConfirmGameBar.exe'
        $process.StartInfo.Arguments='--prepare-legacy-upgrade "'+$requestPath+'" "'+$resultPath+'"'
        $process.StartInfo.UseShellExecute=$false
        $process.StartInfo.CreateNoWindow=$true
        if (-not $process.Start()) {throw '无法启动旧数据迁移。'}
        $clock=[Diagnostics.Stopwatch]::StartNew()
        $nextHeartbeat=15
        while (-not $process.WaitForExit(500)) {
            if ($clock.Elapsed.TotalSeconds -ge $TimeoutSeconds) {throw '旧数据迁移超时；旧版尚未卸载，数据保留。'}
            if ($clock.Elapsed.TotalSeconds -ge $nextHeartbeat) {
                Write-InstallLog ('旧数据备份与校验中：已等待 {0} 秒。' -f [int]$clock.Elapsed.TotalSeconds)
                $nextHeartbeat+=15
            }
        }
        if (-not (Test-Path -LiteralPath $resultPath -PathType Leaf)) {throw '旧数据迁移未返回验证结果；旧版尚未卸载。'}
        $result=Get-Content -LiteralPath $resultPath -Raw -Encoding UTF8 | ConvertFrom-Json
        if (-not $result.Success) {throw ('旧数据迁移失败；旧版尚未卸载：'+$result.Message)}
        if (-not (Test-Path -LiteralPath (Join-Path $result.BackupPath 'verified.json') -PathType Leaf)) {throw '迁移备份缺少校验记录；旧版尚未卸载。'}
        return $result
    }
    finally {
        try {if ($process.Id -and -not $process.HasExited) {$process.Kill();[void]$process.WaitForExit(1000)}} catch {}
        $process.Dispose()
        Remove-Item -LiteralPath $requestPath,$resultPath -Force -ErrorAction SilentlyContinue
    }
}

function Complete-LegacyControlPanelUpgrade {
    $legacy=Get-LegacyControlPanelPackage
    if (-not $legacy) {return}
    Write-InstallLog ('检测到分离前的控制面板：'+$legacy.PackageFullName)
    Stop-OverlayRuntimeForUpdate
    $migration=Invoke-LegacyProfilePreparation -Package $legacy
    Add-InstallResult -Status Success -Item '旧配置、图标包和语音包迁移' -Detail ('迁移与文件校验完成；备份：'+$migration.BackupPath)
    # Always retire the old whole application first. A later widget deployment
    # failure must leave the independent panel as the only control panel.
    Invoke-AppxDeploymentWorker -Parameters @{Operation='Remove';PackageFullName=$legacy.PackageFullName}
    if (Get-LegacyControlPanelPackage) {throw '旧控制面板卸载后仍被系统注册，升级未完成；已迁移的数据和备份均保留。'}
    Add-InstallResult -Status Success -Item '移除旧控制面板' -Detail '旧整合版已卸载；控制面板仅使用独立 EXE，新小组件按所选组件单独安装'
}

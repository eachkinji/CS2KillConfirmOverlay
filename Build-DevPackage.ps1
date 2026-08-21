<#
.SYNOPSIS
    本地测试轻量化打包与部署脚本
    快速增量编译 Rust、同步素材、构建并签名 MSIX，支持一键安装到本机测试。

.PARAMETER Configuration
    构建配置: Debug 或 Release（默认 Release）。

.PARAMETER Platform
    目标平台: x64（默认）。

.PARAMETER Install
    打包完成后自动安装并注册到本机 Game Bar（含证书导入、回环权限豁免及 CS2 GSI 配置）。

.PARAMETER SkipRust
    跳过 Rust 服务的编译（仅修改前端 UI 时可用以极大加速打包）。

.PARAMETER OutputDir
    输出目录，默认 .\Output\Dev。
#>
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [ValidateSet("x64", "x86", "arm64")]
    [string]$Platform = "x64",
    [switch]$Install,
    [switch]$SkipRust,
    [string]$OutputDir = ""
)

$ErrorActionPreference = "Stop"

$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
$WorkspaceRoot = Split-Path -Parent $Root
$ServiceRoot = Join-Path $Root "KillConfirmService"
$WidgetRoot = Join-Path $Root "Widget"
$PackageRoot = Join-Path $Root "Package"
$PackageProjectPath = Join-Path $PackageRoot "KillConfirmGameBar.Package.wapproj"
$PackagedServiceRoot = Join-Path $WidgetRoot "KillConfirmService"
$CertPfx = Join-Path $WidgetRoot "LOCAL_SIGNING_KEY.pfx"
$CertCer = Join-Path $WidgetRoot "KillConfirmGameBar_TemporaryKey.cer"
$PackageFamilyName = "KillConfirmGameBar.Overlay_5jgcw66eyez0m"

if (-not $OutputDir) {
    $OutputDir = Join-Path $Root "Output\Dev"
}
$OutputDir = [System.IO.Path]::GetFullPath($OutputDir)
New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host " Kill Confirm Overlay - 本地开发测试轻量打包" -ForegroundColor Cyan
Write-Host " 配置: $Configuration | 平台: $Platform | 自动安装: $(if ($Install) { '是' } else { '否' })" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# 1. 查找工具链
$VsWhere = Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\Installer\vswhere.exe"
$MsBuildPath = $null
$VcInstallPath = $null

if (Test-Path $VsWhere) {
    $VsInstallPath = & $VsWhere -latest -products * -requires Microsoft.Component.MSBuild -property installationPath
    if ($VsInstallPath) {
        $MsBuildPath = @(
            (Join-Path $VsInstallPath "MSBuild\Current\Bin\amd64\MSBuild.exe"),
            (Join-Path $VsInstallPath "MSBuild\Current\Bin\MSBuild.exe")
        ) | Where-Object { Test-Path $_ } | Select-Object -First 1
    }

    $VcInstallPath = & $VsWhere -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath | Select-Object -First 1
}

if (-not $MsBuildPath) {
    $MsBuildCmd = Get-Command msbuild -ErrorAction SilentlyContinue
    if ($MsBuildCmd) { $MsBuildPath = $MsBuildCmd.Source }
}

if (-not $MsBuildPath -or -not (Test-Path $MsBuildPath)) {
    throw "未找到 MSBuild.exe，请确保已安装 Visual Studio 或 Build Tools。"
}

$SignToolCandidates = @(
    "C:\Program Files (x86)\Windows Kits\10\bin\10.0.26100.0\x64\signtool.exe",
    "C:\Program Files (x86)\Windows Kits\10\bin\10.0.22621.0\x64\signtool.exe",
    "C:\Program Files (x86)\Windows Kits\10\bin\10.0.19041.0\x64\signtool.exe",
    (Join-Path ${env:ProgramFiles(x86)} "Windows Kits\10\bin\x64\signtool.exe")
)
$SignToolPath = $SignToolCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $SignToolPath) {
    $SignToolCmd = Get-Command signtool -ErrorAction SilentlyContinue
    if ($SignToolCmd) { $SignToolPath = $SignToolCmd.Source }
}

# 2. 编译 Rust 服务
if (-not $SkipRust) {
    Write-Host "`n[1/4] 编译 Rust 后端服务..." -ForegroundColor Yellow
    $CargoCmd = Get-Command cargo -ErrorAction SilentlyContinue
    $CargoPath = if ($CargoCmd) { $CargoCmd.Source } else { Join-Path $env:USERPROFILE ".cargo\bin\cargo.exe" }
    if (-not (Test-Path $CargoPath)) {
        throw "未找到 cargo.exe，请先安装 Rust。"
    }

    $VsDevCmd = if ($VcInstallPath) { Join-Path $VcInstallPath "Common7\Tools\VsDevCmd.bat" } else { $null }
    Push-Location $ServiceRoot
    try {
        if ($VsDevCmd -and (Test-Path $VsDevCmd)) {
            $buildCommand = '"' + $VsDevCmd + '" -arch=x64 -host_arch=x64 && "' + $CargoPath + '" build --release'
            & $env:ComSpec /c $buildCommand
        }
        else {
            & $CargoPath build --release
        }
        if ($LASTEXITCODE -ne 0) {
            throw "Rust 服务端编译失败 (ExitCode: $LASTEXITCODE)"
        }
    }
    finally {
        Pop-Location
    }

    New-Item -ItemType Directory -Force -Path $PackagedServiceRoot | Out-Null
    Copy-Item -LiteralPath (Join-Path $ServiceRoot "target\release\cskillconfirm.exe") -Destination (Join-Path $PackagedServiceRoot "cskillconfirm.exe") -Force
    
    $settingsLauncher = Join-Path $ServiceRoot "target\release\killconfirm-settings-launcher.exe"
    if (Test-Path $settingsLauncher) {
        Copy-Item -LiteralPath $settingsLauncher -Destination (Join-Path $PackagedServiceRoot "killconfirm-settings-launcher.exe") -Force
    }

    # 同步 sound 目录
    $PackagedSoundsRoot = Join-Path $PackagedServiceRoot "sounds"
    if (Test-Path $PackagedSoundsRoot) {
        Remove-Item -LiteralPath $PackagedSoundsRoot -Recurse -Force
    }
    Copy-Item -LiteralPath (Join-Path $ServiceRoot "sounds") -Destination $PackagedSoundsRoot -Recurse -Force

    # 补充 dagoujiao / doubao 内置语音：源素材在 SourceAssets，不在 ServiceRoot\sounds 里
    $DagoujiaoSrc = Join-Path $Root "SourceAssets\GameStyles\dagoujiao\soundpacks\dagoujiao"
    $DoubaoSrc    = Join-Path $Root "SourceAssets\GameStyles\doubao\soundpacks\doubao"
    foreach ($pair in @(@($DagoujiaoSrc, "dagoujiao"), @($DoubaoSrc, "doubao"))) {
        $src = $pair[0]; $name = $pair[1]
        if (Test-Path $src) {
            $dst = Join-Path $PackagedSoundsRoot $name
            New-Item -ItemType Directory -Force -Path $dst | Out-Null
            Copy-Item -Path "$src\*.wav" -Destination $dst -Force
        }
    }
}
else {
    Write-Host "`n[1/4] 跳过 Rust 编译 (-SkipRust)" -ForegroundColor DarkGray
}

# 3. 编译打包 MSIX
Write-Host "`n[2/4] 调用 MSBuild 编译打包 MSIX ($Configuration/$Platform)..." -ForegroundColor Yellow
$TempAppxDir = Join-Path $OutputDir "TempAppPackages\"
$MsBuildArgs = @(
    $PackageProjectPath,
    "/restore",
    "/p:Configuration=$Configuration",
    "/p:Platform=$Platform",
    "/p:AppxPackageDir=$TempAppxDir",
    "/t:Rebuild",
    "/verbosity:minimal"
)

& $MsBuildPath @MsBuildArgs
if ($LASTEXITCODE -ne 0) {
    throw "MSBuild 打包失败 (ExitCode: $LASTEXITCODE)"
}

$msixFile = Get-ChildItem -LiteralPath $TempAppxDir -Filter "*.msix" -Recurse -File |
    Where-Object { $_.FullName -notlike "*\obj\*" } |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1

if (-not $msixFile) {
    throw "在 $TempAppxDir 下未找到生成的 .msix 文件！"
}

$FinalMsixPath = Join-Path $OutputDir $msixFile.Name
Copy-Item -LiteralPath $msixFile.FullName -Destination $FinalMsixPath -Force

# 4. 签名与证书输出
Write-Host "`n[3/4] 对 MSIX 执行 Authenticode 数字签名..." -ForegroundColor Yellow
if ($SignToolPath -and (Test-Path $CertPfx)) {
    & $SignToolPath sign /fd SHA256 /f $CertPfx /p "test" $FinalMsixPath
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "SignTool 签名退出码: $LASTEXITCODE"
    }
}
else {
    Write-Warning "未找到 signtool 或证书文件，跳过额外显式重签名。"
}

$FinalCerPath = Join-Path $OutputDir "KillConfirmGameBar_TemporaryKey.cer"
if (Test-Path $CertCer) {
    Copy-Item -LiteralPath $CertCer -Destination $FinalCerPath -Force
}

# 清理临时打包文件夹
if (Test-Path $TempAppxDir) {
    Remove-Item -LiteralPath $TempAppxDir -Recurse -Force -ErrorAction SilentlyContinue
}

$msixSizeMb = [math]::Round((Get-Item $FinalMsixPath).Length / 1MB, 2)
Write-Host "`n[4/4] 打包完成！" -ForegroundColor Green
Write-Host "  产物目录: $OutputDir" -ForegroundColor White
Write-Host "  MSIX安装包: $FinalMsixPath ($msixSizeMb MB)" -ForegroundColor Green
Write-Host "  签名证书: $FinalCerPath" -ForegroundColor White

# 5. 本地一键安装
if ($Install) {
    Write-Host "`n----------------------------------------------------------" -ForegroundColor Cyan
    Write-Host " 开始自动本地部署与配置..." -ForegroundColor Cyan
    Write-Host "----------------------------------------------------------" -ForegroundColor Cyan

    # 终止旧进程
    $processNames = @("cskillconfirm", "TestXboxGameBar", "KillConfirmGameBar", "GameBar", "GameBarFTServer", "GameBarPresenceWriter")
    Get-Process -Name $processNames -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep -Milliseconds 600

    # 导入证书到当前用户证书库
    if (Test-Path $FinalCerPath) {
        try {
            Import-Certificate -FilePath $FinalCerPath -CertStoreLocation "Cert:\CurrentUser\TrustedPeople" -ErrorAction Stop | Out-Null
            Import-Certificate -FilePath $FinalCerPath -CertStoreLocation "Cert:\CurrentUser\Root" -ErrorAction Stop | Out-Null
            Write-Host " [√] 证书已导入到当前用户受信任证书库" -ForegroundColor Green
        }
        catch {
            Write-Warning "证书自动导入提示: $($_.Exception.Message)"
        }
    }

    # 安装 MSIX
    Write-Host " 正在安装 MSIX 包到系统..." -ForegroundColor Yellow
    $addParams = @{
        Path = $FinalMsixPath
        ForceUpdateFromAnyVersion = $true
        DeferRegistrationWhenPackagesAreInUse = $true
        ErrorAction = "Stop"
    }
    try {
        Add-AppxPackage @addParams
        Write-Host " [√] MSIX 应用包安装成功！" -ForegroundColor Green
    }
    catch {
        Write-Host " [X] MSIX 安装失败: $($_.Exception.Message)" -ForegroundColor Red
        throw
    }

    # 配置网络回环豁免权限
    try {
        & CheckNetIsolation.exe LoopbackExempt -a "-n=$PackageFamilyName" | Out-Null
        Write-Host " [√] 本机回环通信权限 (LoopbackExempt) 已配置" -ForegroundColor Green
    }
    catch {
        Write-Warning "配置 LoopbackExempt 失败: $($_.Exception.Message)"
    }

    # 检查/写入 CS2 GSI 配置
    try {
        $steamReg = Get-ItemProperty -Path "HKCU:\Software\Valve\Steam" -ErrorAction SilentlyContinue
        $steamPath = if ($steamReg -and $steamReg.SteamPath) { $steamReg.SteamPath -replace "/", "\" } else { "${env:ProgramFiles(x86)}\Steam" }
        $vdfPath = Join-Path $steamPath "steamapps\libraryfolders.vdf"
        $libraries = @($steamPath)
        if (Test-Path $vdfPath) {
            foreach ($line in (Get-Content $vdfPath)) {
                if ($line -match '^\s*"path"\s+"([^"]+)"') {
                    $p = $matches[1] -replace "\\\\", "\"
                    if (Test-Path $p) { $libraries += $p }
                }
            }
        }

        $cs2CfgWritten = $false
        foreach ($lib in $libraries) {
            $cfgDir = Join-Path $lib "steamapps\common\Counter-Strike Global Offensive\game\csgo\cfg"
            if (Test-Path $cfgDir) {
                $gsiSource = Join-Path $ServiceRoot "gsi\gamestate_integration_killconfirm.cfg"
                if (Test-Path $gsiSource) {
                    Copy-Item -LiteralPath $gsiSource -Destination (Join-Path $cfgDir "gamestate_integration_killconfirm.cfg") -Force
                    Write-Host " [√] CS2 GSI 配置文件已写入: $cfgDir" -ForegroundColor Green
                    $cs2CfgWritten = $true
                    break
                }
            }
        }
        if (-not $cs2CfgWritten) {
            Write-Host " [i] 未检测到 CS2 cfg 目录，可在插件设置界面手动写入 GSI 配置" -ForegroundColor DarkGray
        }
    }
    catch {
        Write-Warning "自动写入 GSI 异常: $($_.Exception.Message)"
    }

    Write-Host "`n==========================================================" -ForegroundColor Green
    Write-Host " 本地安装部署完成！按 Win+G 即可在 Game Bar 中使用最新版本。" -ForegroundColor Green
    Write-Host "==========================================================" -ForegroundColor Green
}
else {
    Write-Host "`n提示: 若要一键打包并直接安装到本机，可带 -Install 参数运行:" -ForegroundColor Cyan
    Write-Host "  .\Build-DevPackage.ps1 -Install`n" -ForegroundColor White
}
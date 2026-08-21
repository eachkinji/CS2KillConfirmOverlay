<#
.SYNOPSIS
    完整的 Release 安装包打包脚本
    全自动完成 Rust 服务端编译、素材同步、MSIX 构建与签名、离线前置组件准备以及 Inno Setup EXE 安装包生成。

.PARAMETER Configuration
    构建配置: Release (默认) 或 Debug。

.PARAMETER Platform
    目标架构: x64 (默认)。

.PARAMETER SkipWithDependencies
    仅生成无依赖的轻量更新安装包（跳过打包体积较大的离线运行库）。

.PARAMETER OutputDir
    最终输出目录，默认 .\Output。
#>
param(
    [ValidateSet("Release", "Debug")]
    [string]$Configuration = "Release",
    [ValidateSet("x64", "x86", "arm64")]
    [string]$Platform = "x64",
    [switch]$SkipWithDependencies,
    [string]$OutputDir = ""
)

$ErrorActionPreference = "Stop"

$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
$WorkspaceRoot = Split-Path -Parent $Root
$ManifestPath = Join-Path $Root "Package\Package.appxmanifest"
$InstallerScript = Join-Path $Root "Installer\KillConfirmGameBar.iss"

if (-not (Test-Path $ManifestPath)) {
    throw "未找到 Package.appxmanifest: $ManifestPath"
}
if (-not (Test-Path $InstallerScript)) {
    throw "未找到 Inno Setup 脚本: $InstallerScript"
}

[xml]$Manifest = Get-Content $ManifestPath
$Version = $Manifest.Package.Identity.Version
if (-not $Version) {
    throw "无法从 Package.appxmanifest 中读取包版本号！"
}

if (-not $OutputDir) {
    $OutputDir = Join-Path $Root "Output"
}
$OutputDir = [System.IO.Path]::GetFullPath($OutputDir)
New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host " Kill Confirm Overlay - 完整 Release 安装包打包" -ForegroundColor Cyan
Write-Host " 版本: $Version | 配置: $Configuration | 平台: $Platform" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# 1. 首先通过 Build-DevPackage 构建出最新的 MSIX 与签名文件
Write-Host "`n[第 1 步/3] 构建核心应用 MSIX 包与二进制..." -ForegroundColor Yellow
$DevOutputDir = Join-Path $OutputDir "TempDevPackage"
& (Join-Path $Root "Build-DevPackage.ps1") -Configuration $Configuration -Platform $Platform -OutputDir $DevOutputDir

$msixFile = Get-ChildItem -LiteralPath $DevOutputDir -Filter "*.msix" -File | Select-Object -First 1
$cerFile = Get-ChildItem -LiteralPath $DevOutputDir -Filter "*.cer" -File | Select-Object -First 1
if (-not $msixFile) {
    throw "MSIX 构建产物丢失！"
}

# 2. 准备 Transfer 安装环境目录
Write-Host "`n[第 2 步/3] 组装依赖与安装组件..." -ForegroundColor Yellow
$TransferRoot = Join-Path $OutputDir "TempTransfer_WithDeps"
$NoDepsTransferRoot = Join-Path $OutputDir "TempTransfer_NoDeps"

foreach ($dir in @($TransferRoot, $NoDepsTransferRoot)) {
    if (Test-Path $dir) { Remove-Item -LiteralPath $dir -Recurse -Force }
    New-Item -ItemType Directory -Force -Path $dir | Out-Null
}

$PrerequisiteSourceRoot = if (Test-Path (Join-Path $Root "Vclibs")) { Join-Path $Root "Vclibs" } else { Join-Path $WorkspaceRoot "Vclibs" }
$PrerequisiteFileNames = @(
    "Microsoft.UI.Xaml.Appx",
    "vclibs.appx",
    "vclibs2.appx",
    "Microsoft.NET.Native.Framework.2.2.x64.appx",
    "Microsoft.NET.Native.Runtime.2.2.x64.appx",
    "gamebar.AppxBundle"
)

# 复制 Overlay 主程序与证书
foreach ($targetRoot in @($TransferRoot, $NoDepsTransferRoot)) {
    $overlayDir = Join-Path $targetRoot "OverlayPackage"
    New-Item -ItemType Directory -Force -Path $overlayDir | Out-Null
    Copy-Item -LiteralPath $msixFile.FullName -Destination (Join-Path $overlayDir $msixFile.Name) -Force
    if ($cerFile) {
        Copy-Item -LiteralPath $cerFile.FullName -Destination (Join-Path $overlayDir $cerFile.Name) -Force
    }
}

# 复制离线前置组件（有依赖版）
$prereqTargetDir = Join-Path $TransferRoot "Prerequisites"
New-Item -ItemType Directory -Force -Path $prereqTargetDir | Out-Null
if (Test-Path $PrerequisiteSourceRoot) {
    foreach ($fn in $PrerequisiteFileNames) {
        $pPath = Join-Path $PrerequisiteSourceRoot $fn
        if (Test-Path $pPath) {
            Copy-Item -LiteralPath $pPath -Destination (Join-Path $prereqTargetDir $fn) -Force
        }
    }
}

# 3. 查找 Inno Setup 编译器
$InnoCandidates = @(
    (Join-Path $env:LOCALAPPDATA "Programs\Inno Setup 6\ISCC.exe"),
    (Join-Path ${env:ProgramFiles(x86)} "Inno Setup 6\ISCC.exe"),
    (Join-Path $env:ProgramFiles "Inno Setup 6\ISCC.exe")
)
$InnoCmd = Get-Command iscc -ErrorAction SilentlyContinue
$InnoCompilerPath = if ($InnoCmd) { $InnoCmd.Source } else { $InnoCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1 }

if (-not $InnoCompilerPath -or -not (Test-Path $InnoCompilerPath)) {
    Write-Warning "未检测到 Inno Setup 6 (ISCC.exe)。已生成 Transfer 目录但无法编译 EXE 安装包。请安装 Inno Setup 6 后重试。"
    return
}

# 4. 编译 Inno Setup 安装包 EXE
Write-Host "`n[第 3 步/3] 调用 Inno Setup 生成最终安装包 EXE..." -ForegroundColor Yellow

function Invoke-InnoCompile {
    param(
        [string]$TransferPath,
        [string]$InternalSuffix,
        [string]$FinalFileName,
        [bool]$SkipPrerequisites
    )

    $innoArgs = @(
        ("/DMyAppVersion={0}" -f $Version),
        ("/DTransferRoot={0}" -f $TransferPath),
        ("/DInstallerOutputSuffix={0}" -f $InternalSuffix),
        ("/DSkipPrerequisites={0}" -f $(if ($SkipPrerequisites) { 1 } else { 0 })),
        $InstallerScript
    )

    & $InnoCompilerPath @innoArgs
    if ($LASTEXITCODE -ne 0) {
        throw "Inno Setup 编译失败 (ExitCode: $LASTEXITCODE)"
    }

    $rawFile = Join-Path $Root ("Output\KillConfirmGameBar_Setup_{0}{1}.exe" -f $Version, $InternalSuffix)
    $finalOutput = Join-Path $OutputDir $FinalFileName
    if (Test-Path $rawFile) {
        Move-Item -LiteralPath $rawFile -Destination $finalOutput -Force
    }
}

if (-not $SkipWithDependencies) {
    $withName = "KillConfirmGameBar_Setup_{0}_有依赖-新人用.exe" -f $Version
    Write-Host " 正在生成: $withName ..." -ForegroundColor Cyan
    Invoke-InnoCompile -TransferPath $TransferRoot -InternalSuffix "_WithDeps" -FinalFileName $withName -SkipPrerequisites $false
}

$noName = "KillConfirmGameBar_Setup_{0}_无依赖-更新用.exe" -f $Version
Write-Host " 正在生成: $noName ..." -ForegroundColor Cyan
Invoke-InnoCompile -TransferPath $NoDepsTransferRoot -InternalSuffix "_NoDeps" -FinalFileName $noName -SkipPrerequisites $true

# 清理临时中间目录
Remove-Item -LiteralPath $DevOutputDir -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $TransferRoot -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $NoDepsTransferRoot -Recurse -Force -ErrorAction SilentlyContinue

Write-Host "`n==========================================================" -ForegroundColor Green
Write-Host " Release 安装包打包完成！" -ForegroundColor Green
Get-ChildItem -LiteralPath $OutputDir -Filter "*.exe" | ForEach-Object {
    $sizeMb = [math]::Round($_.Length / 1MB, 2)
    Write-Host ("  [√] {0} ({1} MB)" -f $_.FullName, $sizeMb) -ForegroundColor Green
}
Write-Host "==========================================================" -ForegroundColor Green
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

.PARAMETER SkipRust
    跳过未变化的 Rust 服务编译，复用现有 Release 二进制以加快打包。

.PARAMETER OutputDir
    最终输出目录，默认 .\Output。
#>
param(
    [ValidateSet("Release", "Debug")]
    [string]$Configuration = "Release",
    [ValidateSet("x64", "x86", "arm64")]
    [string]$Platform = "x64",
    [switch]$SkipWithDependencies,
    [switch]$SkipRust,
    [string]$MsBuildPath = "",
    [switch]$DisableSigning,
    [string]$CertificatePfxPath = "",
    [string]$CertificatePassword = "",
    [string]$CertificateThumbprint = "",
    [string]$CertificateCerPath = "",
    [string]$OutputDir = ""
)

$ErrorActionPreference = "Stop"

$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
$WorkspaceRoot = Split-Path -Parent $Root
$ManifestPath = Join-Path $Root "Package\Package.appxmanifest"
$InstallerScript = Join-Path $Root "Installer\KillConfirmGameBar.iss"
$InstallerPayloadScript = Join-Path $Root "Installer\Install-KillConfirm.ps1"
$InstallerPayloadModuleRoot = Join-Path $Root "Installer\Scripts\Install"
$InstallerPayloadReadme = Join-Path $Root "Installer\README.txt"

if (-not (Test-Path $ManifestPath)) {
    throw "未找到 Package.appxmanifest: $ManifestPath"
}
if (-not (Test-Path $InstallerScript)) {
    throw "未找到 Inno Setup 脚本: $InstallerScript"
}
if (-not (Test-Path $InstallerPayloadScript)) {
    throw "未找到安装载荷脚本: $InstallerPayloadScript"
}
if (-not (Test-Path -LiteralPath $InstallerPayloadModuleRoot -PathType Container)) {
    throw "未找到安装载荷模块目录: $InstallerPayloadModuleRoot"
}

$InstallerPayloadModuleFiles = @(
    Get-ChildItem -LiteralPath $InstallerPayloadModuleRoot -File -Filter "*.ps1" |
        Sort-Object Name
)
if ($InstallerPayloadModuleFiles.Count -eq 0) {
    throw "安装载荷模块目录为空: $InstallerPayloadModuleRoot"
}
$InstallerPayloadScripts = @($InstallerPayloadScript) + @($InstallerPayloadModuleFiles.FullName)

foreach ($payloadScriptPath in $InstallerPayloadScripts) {
    $parseTokens = $null
    $parseErrors = $null
    $installerPayloadSource = Get-Content -LiteralPath $payloadScriptPath -Raw -Encoding UTF8
    [System.Management.Automation.Language.Parser]::ParseInput(
        $installerPayloadSource,
        $payloadScriptPath,
        [ref]$parseTokens,
        [ref]$parseErrors) | Out-Null
    if ($parseErrors.Count -gt 0) {
        $parseSummary = ($parseErrors | ForEach-Object { "line $($_.Extent.StartLineNumber): $($_.Message)" }) -join "; "
        throw "安装载荷脚本语法检查失败 ($payloadScriptPath): $parseSummary"
    }
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
if (Test-Path -LiteralPath $DevOutputDir -PathType Container) {
    Remove-Item -LiteralPath $DevOutputDir -Recurse -Force
}
$devBuildArgs = @{
    Configuration = $Configuration
    Platform = $Platform
    OutputDir = $DevOutputDir
}
if ($SkipRust) {
    $devBuildArgs.SkipRust = $true
}
if ($MsBuildPath) {
    $devBuildArgs.MsBuildPath = $MsBuildPath
}
if ($DisableSigning) {
    $devBuildArgs.DisableSigning = $true
}
if ($CertificatePfxPath) {
    $devBuildArgs.CertificatePfxPath = $CertificatePfxPath
}
if ($CertificatePassword) {
    $devBuildArgs.CertificatePassword = $CertificatePassword
}
if ($CertificateThumbprint) {
    $devBuildArgs.CertificateThumbprint = $CertificateThumbprint
}
if ($CertificateCerPath) {
    $devBuildArgs.CertificateCerPath = $CertificateCerPath
}
& (Join-Path $Root "Build-DevPackage.ps1") @devBuildArgs

$msixFiles = @(Get-ChildItem -LiteralPath $DevOutputDir -Filter "*.msix" -File)
$cerFiles = @(Get-ChildItem -LiteralPath $DevOutputDir -Filter "*.cer" -File)
if ($msixFiles.Count -ne 1) {
    throw "预期生成 1 个 MSIX，实际找到 $($msixFiles.Count) 个：$DevOutputDir"
}
if ($cerFiles.Count -ne 1) {
    throw "预期生成 1 个签名证书 .cer，实际找到 $($cerFiles.Count) 个；不能生成用户无法建立信任的安装包。"
}
$msixFile = $msixFiles[0]
$cerFile = $cerFiles[0]

$packageSignature = Get-AuthenticodeSignature -LiteralPath $msixFile.FullName
$publicCertificate = [System.Security.Cryptography.X509Certificates.X509Certificate2]::new($cerFile.FullName)
if (-not $packageSignature.SignerCertificate) {
    throw "MSIX 没有可读取的 Authenticode 签名：$($msixFile.FullName)"
}
if (-not [string]::Equals(
        $packageSignature.SignerCertificate.Thumbprint,
        $publicCertificate.Thumbprint,
        [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "MSIX 签名证书与安装包携带的 .cer 不一致。"
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

if (-not $SkipWithDependencies) {
    if (-not (Test-Path -LiteralPath $PrerequisiteSourceRoot -PathType Container)) {
        throw "离线依赖目录不存在：$PrerequisiteSourceRoot"
    }
    $missingPrerequisiteFiles = @($PrerequisiteFileNames | Where-Object {
        $candidatePath = Join-Path $PrerequisiteSourceRoot $_
        (-not (Test-Path -LiteralPath $candidatePath -PathType Leaf)) -or ((Get-Item -LiteralPath $candidatePath).Length -le 0)
    })
    if ($missingPrerequisiteFiles.Count -gt 0) {
        throw "有依赖版安装包缺少离线组件：$($missingPrerequisiteFiles -join '、')"
    }
}

# 复制 Overlay 主程序与证书
foreach ($targetRoot in @($TransferRoot, $NoDepsTransferRoot)) {
    $overlayDir = Join-Path $targetRoot "OverlayPackage"
    New-Item -ItemType Directory -Force -Path $overlayDir | Out-Null
    Copy-Item -LiteralPath $msixFile.FullName -Destination (Join-Path $overlayDir $msixFile.Name) -Force
    if ($cerFile) {
        Copy-Item -LiteralPath $cerFile.FullName -Destination (Join-Path $overlayDir $cerFile.Name) -Force
    }

    # Windows PowerShell 5.1 needs a UTF-8 BOM to parse Chinese diagnostics in
    # both the entry script and its dot-sourced modules reliably.
    $utf8Bom = New-Object System.Text.UTF8Encoding($true)
    foreach ($payloadScriptPath in $InstallerPayloadScripts) {
        $payloadRelativePath = if ([string]::Equals(
                $payloadScriptPath,
                $InstallerPayloadScript,
                [System.StringComparison]::OrdinalIgnoreCase)) {
            "Install-KillConfirm.ps1"
        }
        else {
            Join-Path "Scripts\Install" (Split-Path -Leaf $payloadScriptPath)
        }
        $payloadTargetPath = Join-Path $targetRoot $payloadRelativePath
        $payloadTargetDirectory = Split-Path -Parent $payloadTargetPath
        New-Item -ItemType Directory -Force -Path $payloadTargetDirectory | Out-Null
        $payloadText = Get-Content -LiteralPath $payloadScriptPath -Raw -Encoding UTF8
        [System.IO.File]::WriteAllText($payloadTargetPath, $payloadText, $utf8Bom)
    }
    if (Test-Path -LiteralPath $InstallerPayloadReadme) {
        Copy-Item -LiteralPath $InstallerPayloadReadme -Destination (Join-Path $targetRoot "README.txt") -Force
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
    throw "未检测到 Inno Setup 6 (ISCC.exe)，无法生成最终 EXE 安装包。"
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

    $rawFile = Join-Path $Root ("Output\KillConfirmGameBar_Setup_{0}{1}.exe" -f $Version, $InternalSuffix)
    $finalOutput = Join-Path $OutputDir $FinalFileName
    foreach ($stalePath in @($rawFile, $finalOutput) | Select-Object -Unique) {
        if (Test-Path -LiteralPath $stalePath -PathType Leaf) {
            Remove-Item -LiteralPath $stalePath -Force
        }
    }

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

    if (-not (Test-Path -LiteralPath $rawFile -PathType Leaf)) {
        throw "Inno Setup 返回成功，但没有生成预期产物：$rawFile"
    }
    Move-Item -LiteralPath $rawFile -Destination $finalOutput -Force
    if (-not (Test-Path -LiteralPath $finalOutput -PathType Leaf)) {
        throw "最终安装包移动后不存在：$finalOutput"
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

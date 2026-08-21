<#
.SYNOPSIS
    快速本地打包 Kill Confirm Overlay: 只产出 MSIX + 证书。
    不编译 Rust 服务、不拷贝动画/音效资源、不生成安装器/依赖/exe。

.DESCRIPTION
    直接调用 MSBuild 编译 Package\KillConfirmGameBar.Package.wapproj，
    使用项目内已配置好的测试签名证书(Widget\KillConfirmGameBar_TemporaryKey.pfx, 密码 test)。
    打包完成后把 .msix 和 .cer 复制到指定的输出目录。

.PARAMETER Configuration
    Debug 或 Release, 默认 Release。

.PARAMETER Platform
    x64 (默认)。

.PARAMETER OutputDir
    输出目录, 默认 Package\AppPackages\QuickMsix。

.PARAMETER MsBuildPath
    可选的 MSBuild.exe 绝对路径, 缺省自动查找。

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File .\Build-QuickMsix.ps1
.EXAMPLE
    powershell -ExecutionPolicy Bypass -File .\Build-QuickMsix.ps1 -Configuration Debug -OutputDir D:\tmp\msix
#>
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [ValidateSet("x64", "x86", "arm64")]
    [string]$Platform = "x64",
    [string]$OutputDir = "",
    [string]$MsBuildPath = ""
)

$ErrorActionPreference = "Stop"

$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
$PackageRoot = Join-Path $Root "Package"
$PackageProjectPath = Join-Path $PackageRoot "KillConfirmGameBar.Package.wapproj"

if (-not (Test-Path $PackageProjectPath)) {
    throw "Packaging project not found: $PackageProjectPath"
}

if (-not $MsBuildPath -or -not (Test-Path $MsBuildPath)) {
    $VsWhere = Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\Installer\vswhere.exe"
    $MsBuildPath = ""
    if (Test-Path $VsWhere) {
        $VsInstallPath = & $VsWhere -latest -products * -requires Microsoft.Component.MSBuild -property installationPath
        if ($VsInstallPath) {
            $MsBuildPath = @(
                (Join-Path $VsInstallPath "MSBuild\Current\Bin\amd64\MSBuild.exe"),
                (Join-Path $VsInstallPath "MSBuild\Current\Bin\MSBuild.exe")
            ) | Where-Object { Test-Path $_ } | Select-Object -First 1
        }
    }
    if (-not $MsBuildPath) {
        throw "MSBuild was not found. Pass -MsBuildPath to this script."
    }
}
Write-Host "[MSBuild] $MsBuildPath"

if (-not $OutputDir) {
    $OutputDir = Join-Path $PackageRoot "AppPackages\QuickMsix"
}
$OutputDir = [System.IO.Path]::GetFullPath($OutputDir)
New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

$AppxPackageDir = Join-Path $OutputDir "AppPackages\"
$MsBuildArgs = @(
    $PackageProjectPath,
    "/restore",
    "/p:Configuration=$Configuration",
    "/p:Platform=$Platform",
    "/p:AppxPackageDir=$AppxPackageDir",
    "/t:Rebuild",
    "/verbosity:minimal"
)
Write-Host "[Build] $Configuration/$Platform rebuild..."
& $MsBuildPath @MsBuildArgs
if ($LASTEXITCODE -ne 0) {
    throw "MSBuild package build failed with exit code $LASTEXITCODE"
}

$msix = Get-ChildItem -LiteralPath $AppxPackageDir -Filter "*.msix" -Recurse -File |
    Where-Object { $_.FullName -notlike "*\obj\*" } |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1
if (-not $msix) {
    throw "No .msix was produced under $AppxPackageDir"
}

Copy-Item -LiteralPath $msix.FullName -Destination (Join-Path $OutputDir $msix.Name) -Force
$cerSource = Join-Path $Root "Widget\KillConfirmGameBar_TemporaryKey.cer"
if (Test-Path $cerSource) {
    Copy-Item -LiteralPath $cerSource -Destination (Join-Path $OutputDir "KillConfirmGameBar_TemporaryKey.cer") -Force
}
else {
    $cer = Get-ChildItem -LiteralPath (Split-Path -Parent $msix.FullName) -Filter "*.cer" -File | Select-Object -First 1
    if ($cer) {
        Copy-Item -LiteralPath $cer.FullName -Destination (Join-Path $OutputDir $cer.Name) -Force
    }
}

Write-Host ""
Write-Host "[DONE] Output directory: $OutputDir"
Write-Host "[MSIX] $(Join-Path $OutputDir $msix.Name)"
Write-Host "[CERT] KillConfirmGameBar_TemporaryKey.cer"
Write-Host ""
Write-Host "Install:"
Write-Host "  PowerShell (as admin):  Add-AppxPackage -Path ""$(Join-Path $OutputDir $msix.Name)"""
Write-Host "  (If asked, trust the certificate: double-click the .cer and install into Trusted People / Root, then run Add-AppxPackage.)"

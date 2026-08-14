param(
    [string]$Configuration = "Release",
    [string]$Platform = "x64",
    [string]$MsBuildPath = "",
    [string]$VcInstallPath = "",
    [string]$InnoCompilerPath = "",
    [switch]$DisableSigning,
    [string]$CertificatePfxPath = "",
    [string]$CertificatePassword = "",
    [string]$CertificateThumbprint = "",
    [string]$CertificateCerPath = ""
)

$ErrorActionPreference = "Stop"

$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
$WorkspaceRoot = Split-Path -Parent $Root
$ManifestPath = Join-Path $Root "Package\Package.appxmanifest"
$InstallerScript = Join-Path $Root "Installer\KillConfirmGameBar.iss"

if (-not (Test-Path $ManifestPath)) {
    throw "Package.appxmanifest was not found at $ManifestPath"
}

if (-not (Test-Path $InstallerScript)) {
    throw "Installer script was not found at $InstallerScript"
}

[xml]$Manifest = Get-Content $ManifestPath
$Version = $Manifest.Package.Identity.Version
if (-not $Version) {
    throw "Could not read package version from $ManifestPath"
}

$TransferRoot = Join-Path $WorkspaceRoot ("KillConfirmGameBar_Transfer_{0}_有依赖-新人用" -f $Version)
$NoDependenciesTransferRoot = Join-Path $WorkspaceRoot ("KillConfirmGameBar_Transfer_{0}_无依赖-更新用" -f $Version)

$buildTransferArgs = @{
    Configuration = $Configuration
    Platform = $Platform
    MsBuildPath = $MsBuildPath
}
if ($VcInstallPath) {
    $buildTransferArgs.VcInstallPath = $VcInstallPath
}
if ($DisableSigning) {
    $buildTransferArgs.DisableSigning = $true
}
if ($CertificatePfxPath) {
    $buildTransferArgs.CertificatePfxPath = $CertificatePfxPath
}
if ($CertificatePassword) {
    $buildTransferArgs.CertificatePassword = $CertificatePassword
}
if ($CertificateThumbprint) {
    $buildTransferArgs.CertificateThumbprint = $CertificateThumbprint
}
if ($CertificateCerPath) {
    $buildTransferArgs.CertificateCerPath = $CertificateCerPath
}

& (Join-Path $Root "Build-TransferPackage.ps1") @buildTransferArgs

if (-not (Test-Path $TransferRoot)) {
    throw "Expected transfer folder was not produced: $TransferRoot"
}

if (-not (Test-Path $NoDependenciesTransferRoot)) {
    throw "Expected dependency-free transfer folder was not produced: $NoDependenciesTransferRoot"
}

if (-not $InnoCompilerPath) {
    $Inno = Get-Command iscc -ErrorAction SilentlyContinue
    if ($Inno) {
        $InnoCompilerPath = $Inno.Source
    }
    else {
        $Candidates = @(
            (Join-Path $env:LOCALAPPDATA "Programs\Inno Setup 6\ISCC.exe"),
            (Join-Path ${env:ProgramFiles(x86)} "Inno Setup 6\ISCC.exe"),
            (Join-Path $env:ProgramFiles "Inno Setup 6\ISCC.exe")
        )
        $InnoCompilerPath = $Candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
    }
}

if (-not $InnoCompilerPath -or -not (Test-Path $InnoCompilerPath)) {
    throw "Inno Setup compiler was not found. Install Inno Setup 6, then run this script again."
}

New-Item -ItemType Directory -Force -Path (Join-Path $Root "Output") | Out-Null

function Invoke-InstallerCompile {
    param(
        [string]$TransferPath,
        [string]$OutputSuffix,
        [bool]$SkipPrerequisites
    )

    $innoArgs = @(
        ("/DMyAppVersion={0}" -f $Version),
        ("/DTransferRoot={0}" -f $TransferPath),
        ("/DInstallerOutputSuffix={0}" -f $OutputSuffix),
        ("/DSkipPrerequisites={0}" -f $(if ($SkipPrerequisites) { 1 } else { 0 }))
    )
    $innoArgs += $InstallerScript

    & $InnoCompilerPath @innoArgs
    if ($LASTEXITCODE -ne 0) {
        throw "Inno Setup failed with exit code $LASTEXITCODE"
    }
}

Invoke-InstallerCompile -TransferPath $TransferRoot -OutputSuffix "_有依赖-新人用" -SkipPrerequisites $false
Invoke-InstallerCompile -TransferPath $NoDependenciesTransferRoot -OutputSuffix "_无依赖-更新用" -SkipPrerequisites $true

$SetupWithDependenciesPath = Join-Path $Root ("Output\KillConfirmGameBar_Setup_{0}_有依赖-新人用.exe" -f $Version)
$SetupNoDependenciesPath = Join-Path $Root ("Output\KillConfirmGameBar_Setup_{0}_无依赖-更新用.exe" -f $Version)
foreach ($setupPath in @($SetupWithDependenciesPath, $SetupNoDependenciesPath)) {
    if (-not (Test-Path $setupPath)) {
        throw "Expected installer was not produced: $setupPath"
    }
}

Write-Host ""
Write-Host ("Installer with dependencies: {0}" -f $SetupWithDependenciesPath)
Write-Host ("Installer without dependencies: {0}" -f $SetupNoDependenciesPath)

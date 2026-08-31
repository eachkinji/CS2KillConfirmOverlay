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

.PARAMETER CertificatePfxPath
    用于最终 MSIX 签名的 PFX；CI 会传入正式签名证书。
#>
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [ValidateSet("x64", "x86", "arm64")]
    [string]$Platform = "x64",
    [switch]$Install,
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
$ServiceRoot = Join-Path $Root "KillConfirmService"
$WidgetRoot = Join-Path $Root "Widget"
$PackageRoot = Join-Path $Root "Package"
$PackageProjectPath = Join-Path $PackageRoot "KillConfirmGameBar.Package.wapproj"
$PackagedServiceRoot = Join-Path $WidgetRoot "KillConfirmService"
$DefaultCertPfx = Join-Path $WidgetRoot "LOCAL_SIGNING_KEY.pfx"
$DefaultCertCer = Join-Path $WidgetRoot "KillConfirmGameBar_TemporaryKey.cer"
if (-not $CertificatePfxPath -and (Test-Path $DefaultCertPfx)) {
    $CertificatePfxPath = $DefaultCertPfx
    if (-not $CertificatePassword) {
        $CertificatePassword = "test"
    }
}
if (-not $CertificateCerPath -and (Test-Path $DefaultCertCer)) {
    $CertificateCerPath = $DefaultCertCer
}
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
$VcInstallPath = $null

if (-not $MsBuildPath -and (Test-Path $VsWhere)) {
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

}
else {
    Write-Host "`n[1/4] 跳过 Rust 编译 (-SkipRust)" -ForegroundColor DarkGray
}

# SourceAssets is the checked-in source of truth for every built-in sound pack.
# KillConfirmService/sounds and Widget/KillConfirmService are generated/ignored,
# so a clean CI checkout must never depend on either directory already existing.
$PackagedSoundsRoot = Join-Path $PackagedServiceRoot "sounds"
if (Test-Path $PackagedSoundsRoot) {
    Remove-Item -LiteralPath $PackagedSoundsRoot -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $PackagedSoundsRoot | Out-Null

$SourceGameStylesRoot = Join-Path $Root "SourceAssets\GameStyles"
$copiedSoundPackNames = @{}
$copiedSoundPackCount = 0
foreach ($styleFolder in (Get-ChildItem -LiteralPath $SourceGameStylesRoot -Directory)) {
    $soundPacksRoot = Join-Path $styleFolder.FullName "soundpacks"
    if (-not (Test-Path $soundPacksRoot -PathType Container)) {
        continue
    }

    foreach ($soundPack in (Get-ChildItem -LiteralPath $soundPacksRoot -Directory)) {
        $normalizedPackName = $soundPack.Name.ToLowerInvariant()
        if ($copiedSoundPackNames.ContainsKey($normalizedPackName)) {
            throw "内置语音包目录名重复: $($soundPack.Name)"
        }

        $destination = Join-Path $PackagedSoundsRoot $soundPack.Name
        New-Item -ItemType Directory -Force -Path $destination | Out-Null
        Copy-Item -Path (Join-Path $soundPack.FullName "*") -Destination $destination -Recurse -Force
        $copiedSoundPackNames[$normalizedPackName] = $true
        $copiedSoundPackCount++
    }
}
if ($copiedSoundPackCount -eq 0) {
    throw "SourceAssets 中未找到任何内置语音包。"
}
Write-Host "  已从 SourceAssets 同步 $copiedSoundPackCount 个内置语音包。" -ForegroundColor DarkGray

$PackagedFfmpegRoot = Join-Path $PackagedServiceRoot "ffmpeg"
$resolvedFfmpegRoot = [IO.Path]::GetFullPath($PackagedFfmpegRoot)
$resolvedServiceRoot = [IO.Path]::GetFullPath($PackagedServiceRoot).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
if (-not $resolvedFfmpegRoot.StartsWith($resolvedServiceRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw "拒绝清理服务目录之外的 FFmpeg 路径: $resolvedFfmpegRoot"
}
if (Test-Path -LiteralPath $resolvedFfmpegRoot) { Remove-Item -LiteralPath $resolvedFfmpegRoot -Recurse -Force }
& (Join-Path $Root 'Build-FFmpegDependency.ps1') -Destination $PackagedFfmpegRoot
if (-not (Test-Path -LiteralPath (Join-Path $PackagedFfmpegRoot 'ffmpeg.exe'))) { throw "FFmpeg 依赖准备失败" }
Write-Host "  已准备精简分发的 LGPL FFmpeg（仅 ffmpeg.exe）。" -ForegroundColor DarkGray

# 3. 编译打包 MSIX Bundle。正式与开发安装都必须使用 Bundle，确保
# 已由 Bundle 注册的主包和语言资源包可以沿用 Windows 的正常升级链。
Write-Host "`n[2/4] 调用 MSBuild 编译打包 MSIX Bundle ($Configuration/$Platform)..." -ForegroundColor Yellow
$TempAppxDir = Join-Path $OutputDir "TempAppPackages"
if (Test-Path -LiteralPath $TempAppxDir -PathType Container) {
    Remove-Item -LiteralPath $TempAppxDir -Recurse -Force
}
$MsBuildArgs = @(
    $PackageProjectPath,
    "/restore",
    "/p:Configuration=$Configuration",
    "/p:Platform=$Platform",
    "/p:AppxBundle=Always",
    "/p:AppxBundlePlatforms=$Platform",
    "/p:AppxPackageDir=$TempAppxDir",
    # Rebuild runs the AppX Clean target, which can unregister an installed
    # developer-signed package with the same identity. Build still regenerates
    # the deleted package output while preserving the local installation.
    "/t:Build",
    "/verbosity:minimal"
)

& $MsBuildPath @MsBuildArgs
if ($LASTEXITCODE -ne 0) {
    throw "MSBuild 打包失败 (ExitCode: $LASTEXITCODE)"
}

$bundleFile = Get-ChildItem -LiteralPath $TempAppxDir -Filter "*.msixbundle" -Recurse -File |
    Where-Object { $_.FullName -notlike "*\obj\*" } |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1

if (-not $bundleFile) {
    throw "在 $TempAppxDir 下未找到生成的 .msixbundle 文件！"
}

$FinalPackagePath = Join-Path $OutputDir $bundleFile.Name
Copy-Item -LiteralPath $bundleFile.FullName -Destination $FinalPackagePath -Force

# Fail closed if a packaging regression produces an installable UWP shell
# without the FullTrust companion registration or executable.
Add-Type -AssemblyName System.IO.Compression.FileSystem
$bundleArchive = [System.IO.Compression.ZipFile]::OpenRead($FinalPackagePath)
$mainPackageStream = $null
$archive = $null
try {
    $bundleManifestEntry = $bundleArchive.Entries |
        Where-Object { $_.FullName -eq "AppxMetadata/AppxBundleManifest.xml" } |
        Select-Object -First 1
    $mainPackageEntry = $bundleArchive.Entries |
        Where-Object { $_.FullName -like "*.msix" -and $_.FullName -notlike "*language-*" } |
        Sort-Object Length -Descending |
        Select-Object -First 1
    if (-not $bundleManifestEntry -or -not $mainPackageEntry) {
        throw "MSIX Bundle 缺少 Bundle 清单或主应用 MSIX"
    }

    $mainPackageStream = New-Object System.IO.MemoryStream
    $entryStream = $mainPackageEntry.Open()
    try {
        $entryStream.CopyTo($mainPackageStream)
    }
    finally {
        $entryStream.Dispose()
    }
    $mainPackageStream.Position = 0
    $archive = [System.IO.Compression.ZipArchive]::new(
        $mainPackageStream,
        [System.IO.Compression.ZipArchiveMode]::Read,
        $false)

    $manifestEntry = $archive.Entries | Where-Object { $_.FullName -eq "AppxManifest.xml" } | Select-Object -First 1
    $serviceEntry = $archive.Entries | Where-Object { $_.FullName -eq "KillConfirmService/cskillconfirm.exe" } | Select-Object -First 1
    $ffmpegEntry = $archive.Entries | Where-Object { $_.FullName -eq "KillConfirmService/ffmpeg/ffmpeg.exe" } | Select-Object -First 1
    $ffmpegLicenseEntry = $archive.Entries | Where-Object { $_.FullName -eq "KillConfirmService/ffmpeg/LICENSE.txt" } | Select-Object -First 1
    $ffmpegSourceEntry = $archive.Entries | Where-Object { $_.FullName -eq "KillConfirmService/ffmpeg/SOURCE.txt" } | Select-Object -First 1
    if (-not $manifestEntry) {
        throw "MSIX Bundle 的主应用包缺少 AppxManifest.xml"
    }

    $reader = New-Object System.IO.StreamReader($manifestEntry.Open())
    try {
        $packagedManifestText = $reader.ReadToEnd()
    }
    finally {
        $reader.Dispose()
    }

    $requiredManifestMarkers = @(
        'windows.fullTrustProcess',
        'KillConfirmService\cskillconfirm.exe',
        'GroupId="ServicePort10087"',
        'Name="runFullTrust"'
    )
    foreach ($marker in $requiredManifestMarkers) {
        if (-not $packagedManifestText.Contains($marker)) {
            throw "MSIX 后台服务注册不完整，缺少清单标记: $marker"
        }
    }
    if (-not $serviceEntry) {
        throw "MSIX Bundle 的主应用包缺少 KillConfirmService/cskillconfirm.exe"
    }
    if (-not $ffmpegEntry -or $ffmpegEntry.Length -lt 50MB -or -not $ffmpegLicenseEntry -or -not $ffmpegSourceEntry) {
        throw "MSIX Bundle 的主应用包缺少完整的 FFmpeg 运行文件、许可证或源码信息"
    }

    & (Join-Path $Root "Tests\Regression\Test-CrossfireEventIcons.ps1") -PackageArchive $archive
}
finally {
    if ($archive) {
        $archive.Dispose()
    }
    elseif ($mainPackageStream) {
        $mainPackageStream.Dispose()
    }
    $bundleArchive.Dispose()
}

# 4. 签名与证书输出
Write-Host "`n[3/4] 对 MSIX Bundle 执行 Authenticode 数字签名..." -ForegroundColor Yellow
if (-not $DisableSigning -and $SignToolPath -and (Test-Path $CertificatePfxPath)) {
    $signingCertificate = [System.Security.Cryptography.X509Certificates.X509Certificate2]::new(
        $CertificatePfxPath,
        $CertificatePassword)
    $thumbprintMatches = -not $CertificateThumbprint -or [string]::Equals(
        $signingCertificate.Thumbprint,
        $CertificateThumbprint,
        [System.StringComparison]::OrdinalIgnoreCase)
    if (-not $thumbprintMatches) {
        throw "PFX 证书指纹与请求的签名证书不一致。"
    }

    & $SignToolPath sign /fd SHA256 /f $CertificatePfxPath /p $CertificatePassword $FinalPackagePath
    if ($LASTEXITCODE -ne 0) {
        throw "SignTool 签名失败 (ExitCode: $LASTEXITCODE)"
    }
}
elseif ($DisableSigning) {
    Write-Host "  已按参数禁用额外签名。" -ForegroundColor DarkGray
}
else {
    Write-Warning "未找到 signtool 或证书文件，跳过额外显式重签名。"
}

$FinalCerPath = Join-Path $OutputDir "KillConfirmGameBar_TemporaryKey.cer"
if ($CertificateCerPath -and (Test-Path $CertificateCerPath)) {
    Copy-Item -LiteralPath $CertificateCerPath -Destination $FinalCerPath -Force
}

# 清理临时打包文件夹
if (Test-Path $TempAppxDir) {
    Remove-Item -LiteralPath $TempAppxDir -Recurse -Force -ErrorAction SilentlyContinue
}

$packageSizeMb = [math]::Round((Get-Item $FinalPackagePath).Length / 1MB, 2)
Write-Host "`n[4/4] 打包完成！" -ForegroundColor Green
Write-Host "  产物目录: $OutputDir" -ForegroundColor White
Write-Host "  MSIX Bundle 安装包: $FinalPackagePath ($packageSizeMb MB)" -ForegroundColor Green
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
            Write-Host " [√] 证书已导入到当前用户 TrustedPeople 证书库" -ForegroundColor Green
        }
        catch {
            Write-Warning "证书自动导入提示: $($_.Exception.Message)"
        }
    }

    # 安装 MSIX Bundle
    Write-Host " 正在安装 MSIX Bundle 到系统..." -ForegroundColor Yellow
    $addParams = @{
        Path = $FinalPackagePath
        ForceUpdateFromAnyVersion = $true
        DeferRegistrationWhenPackagesAreInUse = $true
        ErrorAction = "Stop"
    }
    try {
        Add-AppxPackage @addParams
        Write-Host " [√] MSIX Bundle 安装成功！" -ForegroundColor Green
    }
    catch {
        Write-Host " [X] MSIX Bundle 安装失败: $($_.Exception.Message)" -ForegroundColor Red
        throw
    }

    # 配置网络回环豁免权限
    try {
        $checkNetPath = if ($env:SystemRoot -and (Test-Path (Join-Path $env:SystemRoot "System32\CheckNetIsolation.exe"))) { Join-Path $env:SystemRoot "System32\CheckNetIsolation.exe" } else { "C:\Windows\System32\CheckNetIsolation.exe" }
        if (-not (Test-Path $checkNetPath)) { $checkNetPath = "CheckNetIsolation.exe" }
        & $checkNetPath LoopbackExempt -a "-n=$PackageFamilyName" | Out-Null
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

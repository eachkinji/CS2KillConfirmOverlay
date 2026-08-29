#Requires -Version 7.0
param([Parameter(Mandatory)][string]$Destination)
$ErrorActionPreference = 'Stop'
$assetUrl = 'https://api.github.com/repos/BtbN/FFmpeg-Builds/releases/assets/534132580'
$archiveSha256 = 'D905AAD3D2D5E999184D1EC6870EDBA731A5D2E0DA9E0D8D8F874F106B64BFEE'
$cacheRoot = Join-Path $env:LOCALAPPDATA 'KillConfirmBuildCache/ffmpeg-9.0.1-lgpl'
$archive = Join-Path $cacheRoot 'ffmpeg-lgpl.zip'
New-Item -ItemType Directory -Force -Path $cacheRoot | Out-Null
if (-not (Test-Path -LiteralPath $archive) -or (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash -ne $archiveSha256) {
    $download = "$archive.download"
    Remove-Item -LiteralPath $download -Force -ErrorAction SilentlyContinue
    Invoke-WebRequest -Uri $assetUrl -Headers @{ Accept = 'application/octet-stream' } -OutFile $download
    if ((Get-FileHash -LiteralPath $download -Algorithm SHA256).Hash -ne $archiveSha256) {
        Remove-Item -LiteralPath $download -Force
        throw 'FFmpeg archive checksum mismatch.'
    }
    Move-Item -LiteralPath $download -Destination $archive -Force
}
if ((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash -ne $archiveSha256) { throw 'FFmpeg archive checksum mismatch.' }
$extract = Join-Path $cacheRoot 'extract'
if (-not (Test-Path -LiteralPath (Join-Path $extract 'ffmpeg.exe'))) {
    $resolvedExtract = [IO.Path]::GetFullPath($extract)
    $resolvedCache = [IO.Path]::GetFullPath($cacheRoot).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    if (-not $resolvedExtract.StartsWith($resolvedCache, [StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe FFmpeg cache path.' }
    if (Test-Path -LiteralPath $resolvedExtract) { Remove-Item -LiteralPath $resolvedExtract -Recurse -Force }
    Expand-Archive -LiteralPath $archive -DestinationPath $extract
    $root = Get-ChildItem -LiteralPath $extract -Directory | Select-Object -First 1
    Copy-Item -LiteralPath (Join-Path $root.FullName 'bin/ffmpeg.exe') -Destination $extract -Force
    Copy-Item -LiteralPath (Join-Path $root.FullName 'LICENSE.txt') -Destination $extract -Force
}
New-Item -ItemType Directory -Force -Path $Destination | Out-Null
Copy-Item -LiteralPath (Join-Path $extract 'ffmpeg.exe'),(Join-Path $extract 'LICENSE.txt') -Destination $Destination -Force
$lines = @(
    'FFmpeg 9.0.1 LGPLv3 build by BtbN (invoked as a separate process for video import)'
    'Build and corresponding source information: https://github.com/BtbN/FFmpeg-Builds'
    'FFmpeg upstream source revision: https://github.com/FFmpeg/FFmpeg/commit/e47273f4d9'
    "Binary archive SHA-256: $archiveSha256"
)
[IO.File]::WriteAllLines((Join-Path $Destination 'SOURCE.txt'), $lines)

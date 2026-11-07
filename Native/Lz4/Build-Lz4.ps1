param(
    [ValidateSet('x64','x86','ARM64','arm64')][string]$Platform = 'x64',
    [ValidateSet('desktop','uwp')][string]$Runtime = 'desktop'
)
$ErrorActionPreference = 'Stop'
$architecture = $Platform.ToLowerInvariant()
$outputDirectory = Join-Path $PSScriptRoot ('bin\' + $Runtime + '\' + $architecture)
New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
$library = Join-Path $outputDirectory 'KillConfirmLz4.dll'
$inputs = @('lz4.c','lz4.h','Build-Lz4.ps1') | ForEach-Object { Get-Item -LiteralPath (Join-Path $PSScriptRoot $_) }
if ((Test-Path -LiteralPath $library) -and (Get-Item -LiteralPath $library).LastWriteTimeUtc -ge ($inputs.LastWriteTimeUtc | Sort-Object -Descending | Select-Object -First 1)) { return }
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
if (!(Test-Path -LiteralPath $vswhere)) { throw 'Visual Studio C++ Build Tools are required to build the bundled LZ4 decoder.' }
$vsInstallation = (& $vswhere -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath | Select-Object -First 1)
if (!$vsInstallation) { throw 'Visual Studio C++ compiler was not found.' }
$vcvars = Join-Path $vsInstallation 'VC\Auxiliary\Build\vcvarsall.bat'
$vcArchitecture = switch ($architecture) { 'x64' {'amd64'} 'x86' {'amd64_x86'} 'arm64' {'amd64_arm64'} }
$appPlatform = if ($Runtime -eq 'uwp') { ' uwp' } else { '' }
$appLibraries = if ($Runtime -eq 'uwp') { ' WindowsApp.lib' } else { '' }
$commands = @(
    '@echo off',
    ('call "' + $vcvars + '" ' + $vcArchitecture + $appPlatform + ' >nul'),
    'if errorlevel 1 exit /b 1',
    ('cl /nologo /O2 /MD /LD /DWINAPI_FAMILY=WINAPI_FAMILY_APP /DLZ4_DLL_EXPORT=1 /Fo"' + (Join-Path $outputDirectory 'lz4.obj') + '" "' + (Join-Path $PSScriptRoot 'lz4.c') + '" /link /APPCONTAINER /DYNAMICBASE /NXCOMPAT' + $appLibraries + ' /OUT:"' + $library + '" /IMPLIB:"' + (Join-Path $outputDirectory 'KillConfirmLz4.lib') + '"'),
    'exit /b %errorlevel%'
)
$buildScript = Join-Path $outputDirectory 'build-native.cmd'
$commands | Set-Content -LiteralPath $buildScript -Encoding ascii
& $buildScript
if ($LASTEXITCODE -ne 0 -or !(Test-Path -LiteralPath $library)) { throw "LZ4 $Platform native build failed." }

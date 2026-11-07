param([ValidateSet('x64','x86','arm64')][string]$Platform = 'x64')
$ErrorActionPreference = 'Stop'
$outputDirectory = Join-Path $PSScriptRoot "bin\$Platform"
New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
$library = Join-Path $outputDirectory 'KillConfirmComposition.dll'
$inputs = @('Composition.cpp','Build-Composition.ps1') | ForEach-Object { Get-Item -LiteralPath (Join-Path $PSScriptRoot $_) }
if ((Test-Path -LiteralPath $library) -and (Get-Item -LiteralPath $library).LastWriteTimeUtc -ge ($inputs.LastWriteTimeUtc | Sort-Object -Descending | Select-Object -First 1)) { return }
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
$vs = (& $vswhere -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath | Select-Object -First 1)
if (!$vs) { throw 'Visual Studio C++ Build Tools are required for DirectComposition.' }
$vcvars = Join-Path $vs 'VC\Auxiliary\Build\vcvarsall.bat'
$architecture = switch ($Platform) { 'x64' {'amd64'} 'x86' {'amd64_x86'} 'arm64' {'amd64_arm64'} }
$commands = @('@echo off', ('call "' + $vcvars + '" ' + $architecture + ' >nul'), 'if errorlevel 1 exit /b 1',
    ('cl /nologo /O2 /MT /LD /EHsc /std:c++17 /utf-8 /Fo"' + $outputDirectory + '\Composition.obj" "' + $PSScriptRoot + '\Composition.cpp" /link /DYNAMICBASE /NXCOMPAT d3d11.lib dxgi.lib dcomp.lib user32.lib /OUT:"' + $library + '" /IMPLIB:"' + $outputDirectory + '\Composition.lib"'), 'exit /b %errorlevel%')
$buildScript = Join-Path $outputDirectory 'build-native.cmd'
$commands | Set-Content -LiteralPath $buildScript -Encoding ascii
& $buildScript
if ($LASTEXITCODE -ne 0) { throw 'DirectComposition native build failed.' }

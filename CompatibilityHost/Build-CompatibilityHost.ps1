param(
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Release',
    [ValidateSet('x64', 'x86', 'arm64')][string]$Platform = 'x64'
)
$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path $PSScriptRoot -Parent
$publishDirectory = Join-Path $repositoryRoot 'Widget/CompatibilityHost'
$project = Join-Path $PSScriptRoot 'CompatibilityHost.csproj'
Write-Host "Building independent compatibility display ($Configuration/$Platform)..." -ForegroundColor Yellow
$resolved = [IO.Path]::GetFullPath($publishDirectory)
$widgetRoot = [IO.Path]::GetFullPath((Join-Path $repositoryRoot 'Widget')).TrimEnd('\') + '\'
if (-not $resolved.StartsWith($widgetRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'Invalid compatibility staging directory.' }
if (Test-Path -LiteralPath $resolved) { Remove-Item -LiteralPath $resolved -Recurse -Force }
& dotnet publish $project -c $Configuration -r "win-$Platform" -p:PlatformTarget=$Platform --self-contained true -o $publishDirectory --nologo
if ($LASTEXITCODE -ne 0) { throw 'Compatibility display build failed.' }
if (-not (Test-Path -LiteralPath (Join-Path $publishDirectory 'KillConfirmCompatibility.exe'))) { throw 'Compatibility host was not published.' }

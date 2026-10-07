#Requires -Version 7.0
param(
    [Parameter(Mandatory)][string]$CrossfirePack,
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Debug',
    [string]$OutputDirectory,
    [switch]$SkipBuild
)
$ErrorActionPreference = 'Stop'
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $repository ('Output/CompatibilityValidation-' + (Get-Date -Format 'yyyyMMdd-HHmmss')) }
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
if (-not $SkipBuild) {
    & dotnet build (Join-Path $repository 'CompatibilityHost/CompatibilityHost.csproj') -c $Configuration --nologo -v:q
    if ($LASTEXITCODE -ne 0) { throw 'Compatibility host build failed.' }
}
# All test data and extracted media stay in a fresh, isolated output profile.
$resourceFolder = Join-Path $OutputDirectory 'isolated-profile/Packs/crossfire/icon_packs/default'
New-Item -ItemType Directory -Path $resourceFolder -Force | Out-Null
$archive = [IO.Compression.ZipFile]::OpenRead([IO.Path]::GetFullPath($CrossfirePack))
try {
    foreach ($entry in $archive.Entries) {
        if (-not $entry.Name) { continue }
        $relative = ($entry.FullName -split '/', 2)[1]
        $destination = [IO.Path]::GetFullPath((Join-Path $resourceFolder $relative))
        if (-not $destination.StartsWith($resourceFolder.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Invalid resource archive entry.' }
        New-Item -ItemType Directory -Path (Split-Path $destination -Parent) -Force | Out-Null
        [IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $destination, $true)
    }
}
finally { $archive.Dispose() }
$executable = Join-Path $repository "CompatibilityHost/bin/$Configuration/net8.0-windows10.0.19041.0/win-x64/KillConfirmCompatibility.exe"
$arguments = @('--validate', ('"' + $OutputDirectory + '"'), ('"' + (Join-Path $repository 'Widget') + '"'))
$start=[Diagnostics.ProcessStartInfo]::new($executable)
$start.UseShellExecute=$false
$start.Arguments=$arguments -join ' '
$start.Environment['KILLCONFIRM_DATA_ROOT']=Join-Path $OutputDirectory 'isolated-profile'
$process=[Diagnostics.Process]::Start($start)
if (-not $process.WaitForExit(240000)) { $process.Kill(); throw 'Compatibility validation timed out.' }
if ($process.ExitCode -ne 0) { throw (Get-Content -LiteralPath (Join-Path $OutputDirectory 'failure.txt') -Raw) }
Get-Content -LiteralPath (Join-Path $OutputDirectory 'contracts.txt')
Get-Content -LiteralPath (Join-Path $OutputDirectory 'results.txt')
Get-Content -LiteralPath (Join-Path $OutputDirectory 'runtime.txt')
Write-Host "PASS: independent desktop renderer, all 15 game styles, mouse/focus modes. Artifacts: $OutputDirectory" -ForegroundColor Green

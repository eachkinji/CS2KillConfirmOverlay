param([string]$AssetsRoot='', [string]$CrossfirePacksRoot='', [switch]$SkipBuild)
$ErrorActionPreference='Stop'
$repository=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if(!$AssetsRoot) {$AssetsRoot=Join-Path $repository 'Widget'}
if(!$SkipBuild) {
    & dotnet build (Join-Path $repository 'CompatibilityHost/CompatibilityHost.csproj') -c Release --nologo -v:q
    if($LASTEXITCODE -ne 0) {throw 'Renderer build failed.'}
}
$output=Join-Path $repository ('Output/PresentationValidation-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
$start=[Diagnostics.ProcessStartInfo]::new((Join-Path $repository 'CompatibilityHost/bin/Release/net8.0-windows10.0.19041.0/win-x64/KillConfirmCompatibility.exe'))
$start.UseShellExecute=$false
$start.WindowStyle=[Diagnostics.ProcessWindowStyle]::Hidden
$start.ArgumentList.Add('--validate-presentation')
$start.ArgumentList.Add($output)
$start.ArgumentList.Add([IO.Path]::GetFullPath($AssetsRoot))
$start.Environment['KILLCONFIRM_DATA_ROOT']=Join-Path $output 'isolated-profile'
if($CrossfirePacksRoot) {$start.Environment['KILLCONFIRM_CF_TEST_PACKS']=[IO.Path]::GetFullPath($CrossfirePacksRoot)}
$process=[Diagnostics.Process]::Start($start)
if(!$process.WaitForExit(180000)) {$process.Kill();throw 'Presentation validation timed out.'}
if($process.ExitCode -ne 0) {throw (Get-Content -LiteralPath (Join-Path $output 'failure.txt') -Raw)}
Get-Content -LiteralPath (Join-Path $output 'pass.txt')
Write-Host "Artifacts: $output"

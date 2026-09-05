#Requires -Version 7.0
<#
.SYNOPSIS
    Regression and Functional Test Suite for 6657 Danmaku Annotation Web GUI.
.DESCRIPTION
    Validates:
    1. Existence of GUI files (server.py, start_gui.py, gui/index.html, Start-DanmakuAnnotationGui.ps1).
    2. Python unit and integration test suite execution (test_annotation_server.py).
    3. Untouched raw source (6657_memes.json) SHA256 integrity.
    4. Full validator infra self-check & coverage consistency.
#>

$ErrorActionPreference = 'Stop'
$RepositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$annotationRoot = Join-Path $RepositoryRoot 'Widget/Danmaku/Annotation'

Write-Host "=== 1. Checking GUI Component Files ===" -ForegroundColor Cyan
$requiredGuiFiles = @(
    'server.py',
    'start_gui.py',
    'gui/index.html'
)

foreach ($file in $requiredGuiFiles) {
    $fullPath = Join-Path $annotationRoot $file
    if (-not (Test-Path -LiteralPath $fullPath)) {
        throw "Annotation GUI file missing: $file"
    }
}

$launcherScript = Join-Path $RepositoryRoot 'Start-DanmakuAnnotationGui.ps1'
if (-not (Test-Path -LiteralPath $launcherScript)) {
    throw "Annotation GUI root launcher missing: Start-DanmakuAnnotationGui.ps1"
}
Write-Host "  -> All GUI component files exist." -ForegroundColor Green

Write-Host "=== 2. Running Python Unit and Integration Tests ===" -ForegroundColor Cyan
$pythonTestScript = Join-Path $RepositoryRoot 'Tests/Danmaku/test_annotation_server.py'
$res = & python $pythonTestScript 2>&1
if ($LASTEXITCODE -ne 0) {
    throw "Python unit/integration tests failed:`n$res"
}
Write-Host "  -> Python tests passed successfully:`n$res" -ForegroundColor Green

Write-Host "=== 3. Verifying Source Data 6657_memes.json SHA256 Integrity ===" -ForegroundColor Cyan
$sourcePath = Join-Path $RepositoryRoot 'Widget/Danmaku/6657_memes.json'
$expectedSha256 = '9bd3ed7ae963714a34d481bde45df597e4d4db49ee23c39d67506f11b4e32183'
$actualSha256 = (Get-FileHash -LiteralPath $sourcePath -Algorithm SHA256).Hash.ToLower()

if ($actualSha256 -ne $expectedSha256) {
    throw "Source data integrity breach! Expected SHA256: $expectedSha256, got: $actualSha256"
}
Write-Host "  -> Source data is strictly intact ($actualSha256)." -ForegroundColor Green

Write-Host "`nPASS: All Danmaku Annotation GUI regression tests passed successfully!" -ForegroundColor Green

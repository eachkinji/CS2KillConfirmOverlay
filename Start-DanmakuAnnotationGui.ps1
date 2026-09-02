#Requires -Version 7.0
<#
.SYNOPSIS
    Launch 6657 Danmaku Annotation Web GUI.
.DESCRIPTION
    Starts the local Python server on 127.0.0.1 and opens the annotation review interface in the default browser.
.PARAMETER Port
    HTTP port (default: 8765).
.PARAMETER NoBrowser
    Do not launch the browser automatically.
#>
[CmdletBinding()]
param(
    [int]$Port = 8765,
    [switch]$NoBrowser
)

$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath($PSScriptRoot)
$startScript = Join-Path $repoRoot 'Widget/Danmaku/Annotation/start_gui.py'

if (-not (Test-Path -LiteralPath $startScript)) {
    throw "Annotation GUI launcher script not found at: $startScript"
}

$pyArgs = @($startScript, '--repo-root', $repoRoot, '--port', $Port)
if ($NoBrowser) {
    $pyArgs += '--no-browser'
}

Write-Host "Starting 6657 Danmaku Annotation Web GUI on http://127.0.0.1:$Port ..." -ForegroundColor Cyan
& python @pyArgs

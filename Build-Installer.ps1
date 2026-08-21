<#
.SYNOPSIS
    向后兼容转发脚本：直接调用 Build-ReleaseInstaller.ps1
#>
param(
    [string]$Configuration = "Release",
    [string]$Platform = "x64",
    [switch]$SkipWithDependencies,
    [string]$OutputDir = ""
)

$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
& (Join-Path $Root "Build-ReleaseInstaller.ps1") @PSBoundParameters
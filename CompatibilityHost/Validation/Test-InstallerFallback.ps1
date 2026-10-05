#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$output = Join-Path $repository ('Output/CompatibilityInstaller-' + [Guid]::NewGuid().ToString('N'))
$previousLocalAppData = $env:LOCALAPPDATA
$PackageName = 'Compatibility.Test'
function Get-AppxPackage { [pscustomobject]@{ Version = [Version]'1.0'; PackageFamilyName = 'Compatibility.Test_Family' } }
function Add-InstallResult { param($Status, $Item, $Detail); if ($Status -ne 'Success') { throw "$Item`: $Detail" } }
function Get-ErrorReason { param($ErrorRecord); $ErrorRecord.Exception.Message }
try {
    $env:LOCALAPPDATA = $output
    . (Join-Path $repository 'Installer/Scripts/CompatibilityDisplay/Install-CompatibilityDisplay.ps1')
    Initialize-CompatibilityDisplayFallback
    $path = Join-Path $output 'Packages/Compatibility.Test_Family/LocalState/CompatibilityDisplay/display.json'
    $config = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
    if (-not $config.Enabled -or -not $config.FollowGame -or $config.FramesPerSecond -ne 60) { throw 'Fresh installation did not enable compatibility defaults.' }
    $config.Enabled = $false
    $config.ScreenName = 'Saved monitor'
    $config | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $path -Encoding utf8
    $saved = Get-Content -LiteralPath $path -Raw
    Initialize-CompatibilityDisplayFallback
    if ((Get-Content -LiteralPath $path -Raw) -ne $saved) { throw 'Installer replaced existing display preferences.' }
    Write-Host 'PASS: missing-Game-Bar fallback defaults and preservation of existing preferences.' -ForegroundColor Green
}
finally { $env:LOCALAPPDATA = $previousLocalAppData }

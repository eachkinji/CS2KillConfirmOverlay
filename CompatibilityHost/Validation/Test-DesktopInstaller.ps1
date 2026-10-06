#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$output = Join-Path $repository ('Output/DesktopInstaller-' + [Guid]::NewGuid().ToString('N'))
$previousLocalAppData = $env:LOCALAPPDATA
$ScriptRoot = Join-Path $output 'Payload'
New-Item -ItemType Directory -Path $ScriptRoot -Force | Out-Null
$desktop = Join-Path $output 'Desktop'
foreach ($file in @('KillConfirmCompatibility.exe', 'KillConfirmCompatibility.dll', 'coreclr.dll', 'Microsoft.Graphics.Canvas.dll', 'KillConfirmService/cskillconfirm.exe')) {
    $target = Join-Path $desktop $file
    New-Item -ItemType Directory -Path (Split-Path $target) -Force | Out-Null
    [IO.File]::WriteAllText($target, 'Isolated installer fixture')
}
function Add-InstallResult { param($Status, $Item, $Detail); $script:InstallResults.Add([pscustomobject]@{ Status = $Status; Item = $Item; Detail = $Detail }) }
function Get-ErrorReason { param($ErrorRecord); $ErrorRecord.Exception.Message }
function Test-XboxGameBarAvailable { $script:Scenario -ne 'NoGameBar' }
function Get-Service { param($Name, $ErrorAction); if ($script:Scenario -ne 'NoFirewall') { [pscustomobject]@{Status = 'Running'} } }
function Install-OverlayPackage { $script:WidgetAttempts++; if ($script:Scenario -eq 'MsixFailure') { Add-InstallResult Error 'Kill Confirm MSIX' '0x80073D0A'; throw '0x80073D0A: deployment failed' } }
function Test-OverlayPackageInstalled { }
try {
    . (Join-Path $repository 'Installer/Scripts/Install/Desktop.ps1')
    foreach ($case in @('NoGameBar', 'NoFirewall', 'MsixFailure', 'Success')) {
        $script:Scenario = $case; $script:WidgetAttempts = 0
        $script:InstallResults = [Collections.Generic.List[object]]::new()
        $env:LOCALAPPDATA = Join-Path $output $case
        Install-DesktopControlPanel
        $installed = Install-OptionalGameBarWidget
        if ($installed -ne ($case -eq 'Success')) { throw "Incorrect result: $case" }
        if ($InstallResults | Where-Object Status -eq 'Error') { throw "Optional failure made desktop installation fail: $case" }
        if ($case -in @('NoGameBar', 'NoFirewall') -and $WidgetAttempts -ne 0) { throw 'Attempted MSIX with unavailable prerequisites.' }
        if (-not $installed) { Enable-DesktopCompatibilityDefault }
        if (-not (Test-Path (Join-Path $RuntimeLogRoot 'desktop-location.txt'))) { throw 'Desktop launch location missing.' }
        if (-not $installed) {
            $configPath = Join-Path $RuntimeLogRoot 'CompatibilityDisplay/display.json'
            $config = Get-Content $configPath -Raw | ConvertFrom-Json
            if (-not $config.Enabled) { throw 'Compatibility default missing.' }
            $config.ScreenName = 'Saved screen'; $config | ConvertTo-Json -Depth 8 | Set-Content $configPath
            $saved = Get-Content $configPath -Raw; Enable-DesktopCompatibilityDefault
            if ((Get-Content $configPath -Raw) -ne $saved) { throw 'Existing preferences were replaced.' }
        }
        Write-Host "PASS: $case; independent desktop retained; optional component result correct."
    }
} finally { $env:LOCALAPPDATA = $previousLocalAppData }

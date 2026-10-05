param(
    [Parameter(Mandatory)][IO.Compression.ZipArchive]$Archive,
    [Parameter(Mandatory)][string]$ManifestText
)
$ErrorActionPreference = 'Stop'
foreach ($name in @('KillConfirmCompatibility.exe', 'KillConfirmCompatibility.dll', 'Microsoft.Graphics.Canvas.dll', 'Microsoft.Graphics.Canvas.Interop.dll', 'WinRT.Runtime.dll', 'PresentationFramework.dll', 'coreclr.dll')) {
    $entry = $Archive.GetEntry("CompatibilityHost/$name")
    if (-not $entry -or $entry.Length -eq 0) { throw "Independent compatibility runtime is missing: CompatibilityHost/$name" }
}
if (-not $ManifestText.Contains('GroupId="CompatibilityDisplay"') -or -not $ManifestText.Contains('--open-compatibility-display')) {
    throw 'Compatibility launch registration is missing from the packaged manifest.'
}
if ($Archive.GetEntry('CompatibilityHost/KillConfirmGameBar.exe')) { throw 'Legacy widget files were mixed into the compatibility host.' }
Write-Host '  Independent compatibility executable, renderer and runtime verified.' -ForegroundColor DarkGray

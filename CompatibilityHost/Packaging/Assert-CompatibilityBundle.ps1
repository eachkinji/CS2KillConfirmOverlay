param(
    [Parameter(Mandatory)][IO.Compression.ZipArchive]$Archive,
    [Parameter(Mandatory)][string]$ManifestText
)
$ErrorActionPreference='Stop'
if(!$Archive.GetEntry('Bridge/killconfirm-widget-bridge.exe') -or !$ManifestText.Contains('Bridge\killconfirm-widget-bridge.exe')) {throw 'Optional Game Bar launch bridge is missing.'}
foreach($entry in $Archive.Entries) {
    if($entry.FullName -match '^(CompatibilityHost/|KillConfirmService/|Assets/GameStyles/|Assets/KillConfirmCode/|Pages/Main/)' -or $entry.FullName -match '(^|/)(cskillconfirm|KillConfirmCompatibility)\.exe$') {throw "Ordinary runtime/resource duplicated inside widget: $($entry.FullName)"}
}
Write-Host '  Widget-only MSIX verified; ordinary control panel, service, renderer and large resources are external.'

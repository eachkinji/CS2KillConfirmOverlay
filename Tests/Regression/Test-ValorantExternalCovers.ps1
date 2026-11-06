param(
    [Parameter(Mandatory = $true)][string]$LibraryRoot,
    [string]$Executable,
    [int]$TimeoutSeconds = 120
)

$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
if (-not $Executable) {
    $Executable = Join-Path $repo 'ControlPanel\bin\x64\Release\net8.0-windows10.0.19041.0\win-x64\KillConfirmControlPanel.exe'
}
$Executable = (Resolve-Path -LiteralPath $Executable).Path
$LibraryRoot = (Resolve-Path -LiteralPath $LibraryRoot).Path
$valorantRoot = Join-Path $LibraryRoot 'valorant'
if (-not (Test-Path -LiteralPath (Join-Path $valorantRoot 'icon_packs') -PathType Container)) {
    throw 'LibraryRoot must contain valorant/icon_packs.'
}
$runRoot = Join-Path $repo ('Output\ValorantCoverRegression-' + [guid]::NewGuid().ToString('N'))
$profile = Join-Path $runRoot 'profile'
New-Item -ItemType Directory -Path $profile -Force | Out-Null

function Get-LibrarySnapshot {
    Get-ChildItem -LiteralPath $valorantRoot -File -Recurse |
        Sort-Object FullName | Select-Object FullName, Length, LastWriteTimeUtc | ConvertTo-Json -Depth 3 -Compress
}

# The application validator discovers only icon packs, never the audio repair
# path. Check the entire supplied library still has identical file metadata.
$before = Get-LibrarySnapshot
$start = [Diagnostics.ProcessStartInfo]::new($Executable)
$start.UseShellExecute = $false
$start.CreateNoWindow = $true
$start.WindowStyle = 'Hidden'
$start.EnvironmentVariables['KILLCONFIRM_DATA_ROOT'] = $profile
$start.EnvironmentVariables['KILLCONFIRM_INSTALL_ROOT'] = Split-Path -Parent $Executable
$start.EnvironmentVariables['KILLCONFIRM_UI_VALIDATION'] = '1'
$start.EnvironmentVariables['KILLCONFIRM_UI_INSPECTION'] = '0'
$start.EnvironmentVariables['KILLCONFIRM_AUDIO_CHANNEL_VALIDATION'] = '1'
$start.EnvironmentVariables['KILLCONFIRM_VALORANT_COVER_LIBRARY'] = $LibraryRoot
$validationProcess = $null
try {
    $validationProcess = [Diagnostics.Process]::Start($start)
    if (-not $validationProcess.WaitForExit($TimeoutSeconds * 1000)) { throw 'Cover UI validation timed out.' }
    $failure = Join-Path $profile 'ui-failure.txt'
    if (Test-Path -LiteralPath $failure) { throw (Get-Content -LiteralPath $failure -Raw) }
    foreach ($file in @('ui-pass.txt', 'valorant-cover-validation.txt', 'valorant-external-covers.png')) {
        if (-not (Test-Path -LiteralPath (Join-Path $profile $file))) { throw "Missing validation result: $file" }
    }
    Get-Content -LiteralPath (Join-Path $profile 'valorant-cover-validation.txt')
    Write-Output "Evidence: $runRoot"
}
finally {
    if ($validationProcess -and -not $validationProcess.HasExited) {
        $validationProcess.Kill()
        $validationProcess.WaitForExit(5000) | Out-Null
    }
    $unchanged = $before -eq (Get-LibrarySnapshot)
    [pscustomobject]@{ UserLibraryUnchanged = $unchanged } | ConvertTo-Json |
        Set-Content -LiteralPath (Join-Path $runRoot 'library-readonly-result.json') -Encoding UTF8
    if (-not $unchanged) { throw 'The supplied library changed during cover UI validation.' }
}

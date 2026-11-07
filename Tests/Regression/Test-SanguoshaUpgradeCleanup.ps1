#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$fixture = Join-Path $repository ('Output/SanguoshaUpgradeCleanup-' + [guid]::NewGuid().ToString('N'))
$install = Join-Path $fixture 'installation'
New-Item -ItemType Directory -Path $install -Force | Out-Null
$source = Get-Content -LiteralPath (Join-Path $repository 'Installer/KillConfirmGameBar.iss') -Raw
$deletes = [regex]::Match($source, '(?s)\[InstallDelete\](.*?)(?=\[Files\])').Groups[1].Value
$lines = @($deletes -split '\r?\n' | Where-Object { $_ -match '^Type: .*sanguosha' })
if ($lines.Count -ne 13) { throw 'Expected 13 precise retired Sanguosha cleanup targets.' }
$retired = foreach ($line in $lines) {
    $relative = [regex]::Match($line, 'Name: "\{app\}\\([^"]+)"').Groups[1].Value
    if (!$relative -or $relative.Contains('*') -or $relative.Contains('..')) { throw "Unsafe cleanup target: $line" }
    $target = [IO.Path]::GetFullPath((Join-Path $install $relative))
    if (!$target.StartsWith($install + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Fixture target escaped installation.' }
    $marker = if ($line.StartsWith('Type: files;')) { $target } else { Join-Path $target 'old-resource.txt' }
    New-Item -ItemType Directory -Path (Split-Path $marker -Parent) -Force | Out-Null
    [IO.File]::WriteAllText($marker, 'retired built-in')
    $target
}
$retained = @(
    'Assets/GameStyles/sanguosha/killconfirm/lianpo/retained.txt',
    'KillConfirmService/sounds/sanguosha/retained.txt',
    'UserData/Packs/sanguosha_guanjie/imported.txt',
    'Assets/GameStyles/valorant/retained.txt'
)
foreach ($relative in $retained) {
    $target = Join-Path $install $relative
    New-Item -ItemType Directory -Path (Split-Path $target -Parent) -Force | Out-Null
    [IO.File]::WriteAllText($target, 'keep')
}
$compiler = @(
    (Join-Path $env:LOCALAPPDATA 'Programs/Inno Setup 6/ISCC.exe'),
    (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6/ISCC.exe')
) | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (!$compiler) { throw 'Inno Setup compiler missing.' }
$script = @"
[Setup]
AppName=Sanguosha upgrade cleanup fixture
AppVersion=1.0
DefaultDirName=$install
Uninstallable=no
PrivilegesRequired=lowest
OutputDir=.
OutputBaseFilename=CleanupFixture
[InstallDelete]
$($lines -join "`n")
"@
$scriptPath = Join-Path $fixture 'cleanup.iss'
[IO.File]::WriteAllText($scriptPath, $script, [Text.UTF8Encoding]::new($true))
& $compiler /Q $scriptPath
if ($LASTEXITCODE) { throw 'Cleanup fixture compilation failed.' }
$process = Start-Process -FilePath (Join-Path $fixture 'CleanupFixture.exe') -ArgumentList '/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART' -WindowStyle Hidden -PassThru
if (!$process.WaitForExit(15000)) { $process.Kill(); throw 'Cleanup fixture timed out.' }
if ($process.ExitCode) { throw "Cleanup fixture failed: $($process.ExitCode)" }
foreach ($target in $retired) {
    if (Test-Path -LiteralPath $target) { throw "Retired resource survived upgrade: $target" }
}
foreach ($relative in $retained) {
    if ([IO.File]::ReadAllText((Join-Path $install $relative)) -ne 'keep') { throw "Retained resource changed: $relative" }
}
Write-Output 'PASS: upgrade removed all 13 retired built-in targets and preserved the anniversary pack, imported content and other games.'
Write-Output "Artifacts: $fixture"

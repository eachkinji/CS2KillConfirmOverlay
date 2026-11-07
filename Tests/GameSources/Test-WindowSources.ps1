$ErrorActionPreference = 'Stop'
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$sources = Join-Path $repository 'CompatibilityHost/Windows/GameSources'
$types = Add-Type -Path @((Join-Path $sources 'GameWindowSource.cs'),
    (Join-Path $sources 'CounterStrike/CounterStrikeWindow.cs'),
    (Join-Path $sources 'LeagueOfLegends/LeagueOfLegendsWindow.cs')) -PassThru
$type = $types | Where-Object Name -EQ 'GameWindowSource'
$method = $type.GetMethod('Matches', [Reflection.BindingFlags]'NonPublic,Static')
foreach ($case in @(
    @('lol', 'League of Legends', $true), @('lol', 'league of legends', $true),
    @('lol', 'LeagueClientUx', $false), @('lol', 'RiotClientServices', $false),
    @('lol', 'cs2', $false), @('lol', 'csgo', $false),
    @('cs2', 'cs2', $true), @('cs2', 'csgo', $false),
    @('csgo_legacy', 'csgo', $true), @('csgo_legacy', 'cs2', $false),
    @('cs2', 'League of Legends', $false))) {
    $actual = $method.Invoke($null, @($case[0], $case[1]))
    if ($actual -ne $case[2]) { throw "Window routing failed: $($case[0]) / $($case[1])" }
}
'PASS: 11 window-source cases; selected game only, no LOL launcher/lobby windows.'

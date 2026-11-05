param([string]$Executable = '', [string]$ResultPath = '')
$ErrorActionPreference = 'Stop'
$serviceRoot = Split-Path -Parent $PSScriptRoot
if (-not $Executable) { $Executable = Join-Path $serviceRoot 'target\release\cskillconfirm.exe' }
$Executable = (Resolve-Path -LiteralPath $Executable).Path
$testRoot = Join-Path $serviceRoot ('target\audio-channel-smoke-' + [guid]::NewGuid().ToString('N'))
$voiceRoot = Join-Path $testRoot 'voice'
New-Item -ItemType Directory -Path $voiceRoot -Force | Out-Null
# Isolate both configuration and authentication from the installed application.
$testToken = '1234567890abcdef1234567890abcdef'
Set-Content -LiteralPath (Join-Path $testRoot 'service-auth-token.txt') -Value $testToken -NoNewline
$sourceRoot = [IO.Path]::GetFullPath((Join-Path $serviceRoot '..\SourceAssets\GameStyles\calabiyau\soundpacks\calabiyau'))
Copy-Item -LiteralPath (Join-Path $sourceRoot '1kill.wav') -Destination (Join-Path $voiceRoot 'line.wav')
@{
    id='smoke_character'; game_style='calabiyau'; audio_kind='character_voice'
    audio=@{ base_gain=1; slots=@{kill_1='line.wav';kill_2='line.wav'}; slot_delays_ms=@{kill_1=1000};slot_cooldowns_ms=@{kill_1=10000} }
} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $voiceRoot 'manifest.json') -Encoding utf8
@{ VoicePacks=@(@{Key='smoke_character';FolderPath=$voiceRoot;AudioKind=1;GameStyle='calabiyau';AssociatedIconKeys=@('calabiyau')});IconPacks=@() } |
    ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $testRoot 'pack-catalog.json') -Encoding utf8NoBOM
Set-Content -LiteralPath (Join-Path $testRoot 'not-a-pack.txt') -Value 'private test fixture'
$listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 0)
$listener.Start()
$testPort = $listener.LocalEndpoint.Port
$listener.Stop()
$oldDataRoot = $env:KILLCONFIRM_DATA_ROOT
$env:KILLCONFIRM_DATA_ROOT = $testRoot
$checks = [Collections.Generic.List[string]]::new()
$process = $null
try {
    $process = Start-Process -FilePath $Executable -ArgumentList @('--port', $testPort, '--preset', 'crossfire_swat_gr', '--volume', '0', '--developer-mode') -WorkingDirectory $serviceRoot -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $testRoot 'stdout.log') -RedirectStandardError (Join-Path $testRoot 'stderr.log')
    $baseUri = 'http://127.0.0.1:' + $testPort
    $headers = @{'x-killconfirm-token'=$testToken}
    $ready = $false
    for ($attempt=0; $attempt -lt 60; $attempt++) {
        if ($process.HasExited) { throw 'Isolated audio test service exited during startup.' }
        try { $null = Invoke-RestMethod -Uri ($baseUri + '/health') -Headers $headers; $ready=$true; break } catch { Start-Sleep -Milliseconds 100 }
    }
    if (-not $ready) { throw 'Isolated audio test service did not become ready.' }
    $snapshot = Invoke-RestMethod -Uri ($baseUri + '/shared/state') -Headers $headers
    $sharedVoice = $snapshot.catalog.VoicePacks[0]
    if ($sharedVoice.AudioKind -ne 1 -or $sharedVoice.GameStyle -ne 'calabiyau' -or $sharedVoice.AssociatedIconKeys[0] -ne 'calabiyau') { throw 'Shared catalog lost typed audio metadata.' }
    $checks.Add('shared snapshot retains audio category, game and icon associations')
    $resourcePath = [Uri]::EscapeDataString((Join-Path $voiceRoot 'manifest.json'))
    $sharedResource = Invoke-WebRequest -Uri ($baseUri + '/shared/resource?path=' + $resourcePath) -Headers $headers
    if ($sharedResource.StatusCode -ne 200) { throw 'User-selected external audio folder is unavailable to the widget.' }
    $checks.Add('widget broker can read an external catalog audio folder')
    $unselectedPath = [Uri]::EscapeDataString((Join-Path $testRoot 'not-a-pack.txt'))
    $denied = Invoke-WebRequest -Uri ($baseUri + '/shared/resource?path=' + $unselectedPath) -Headers $headers -SkipHttpErrorCheck
    if ($denied.StatusCode -ne 403) { throw 'Audio catalog broker exposed an unselected neighboring file.' }
    $checks.Add('widget broker rejects unselected neighboring files')
    function Read-Channels { Invoke-RestMethod -Uri ($baseUri + '/audio/channels') -Headers $headers }
    function Post-Json([string]$route, $body) { Invoke-RestMethod -Uri ($baseUri + $route) -Headers $headers -Method Post -ContentType 'application/json' -Body ($body | ConvertTo-Json -Depth 12 -Compress) }
    $initial = Read-Channels
    if ($initial.sound_effect -or $initial.character_voice.preset -ne 'crossfire_swat_gr') { throw 'Legacy CF selection did not migrate to only the voice channel.' }
    $checks.Add('legacy CF migrates to voice only')
    $selection = @{
        sound_effect=@{preset='calabiyau';custom_path=$sourceRoot;volume_percent=70;enabled=$true;interrupt_previous=$true}
        character_voice=@{preset='smoke_character';custom_path=$voiceRoot;volume_percent=35;enabled=$true;interrupt_previous=$true}
    }
    $selected = Post-Json '/audio/channels' $selection
    if ($selected.sound_effect.volume_percent -ne 70 -or $selected.character_voice.volume_percent -ne 35) { throw 'Independent channel volumes were not retained.' }
    $primary = Invoke-RestMethod -Uri ($baseUri + '/soundpack') -Headers $headers
    if ($primary.preset -ne 'calabiyau') { throw 'Legacy primary preset view did not follow the new effect channel.' }
    $checks.Add('atomic two-channel selection and legacy primary view')
    $bad = @{sound_effect=$selection.sound_effect;character_voice=@{preset='missing';custom_path=(Join-Path $testRoot 'missing')}}
    $response = Invoke-WebRequest -Uri ($baseUri + '/audio/channels') -Headers $headers -Method Post -ContentType 'application/json' -Body ($bad | ConvertTo-Json -Depth 8) -SkipHttpErrorCheck
    if ($response.StatusCode -ne 400) { throw 'Invalid second pack was accepted.' }
    $afterFailure = Read-Channels
    if (($afterFailure | ConvertTo-Json -Depth 8 -Compress) -ne ($selected | ConvertTo-Json -Depth 8 -Compress)) { throw 'Failed transaction changed active selections.' }
    $checks.Add('invalid second pack preserves both previous selections')
    $wrongCategory = @{sound_effect=$selection.character_voice;character_voice=$null}
    $response = Invoke-WebRequest -Uri ($baseUri + '/audio/channels') -Headers $headers -Method Post -ContentType 'application/json' -Body ($wrongCategory | ConvertTo-Json -Depth 8) -SkipHttpErrorCheck
    if ($response.StatusCode -ne 400) { throw 'A character voice was accepted as an effect pack.' }
    $checks.Add('explicit audio category mismatch rejected')
    $null = Post-Json '/audio/interrupt-previous' @{enabled=$false}
    $afterInterrupt = Read-Channels
    if ($afterInterrupt.sound_effect.interrupt_previous -or -not $afterInterrupt.character_voice.interrupt_previous) { throw 'Legacy interrupt option did not remain independent from voice.' }
    $checks.Add('legacy interruption changes effects without changing voice policy')
    foreach ($channel in @('sound_effect','character_voice','all')) {
        $null = Invoke-RestMethod -Uri ($baseUri + '/test/2?audio=true&audio_channel=' + $channel + '&headshot=true') -Headers $headers
    }
    Start-Sleep -Milliseconds 350
    $checks.Add('both channels and channel-specific previews accept kill events')
    $null = Post-Json '/soundpack' @{preset='bf1'}
    $legacy = Read-Channels
    if ($legacy.sound_effect.preset -ne 'bf1' -or $legacy.character_voice) { throw 'Legacy non-CF preset did not restore one effect channel.' }
    $checks.Add('legacy other-game API restores effects only without stale voice')
    $none = Post-Json '/audio/channels' @{sound_effect=$null;character_voice=$null}
    if ($none.sound_effect -or $none.character_voice) { throw 'Disabling both channels failed.' }
    $primary = Invoke-RestMethod -Uri ($baseUri + '/soundpack') -Headers $headers
    if ($primary.preset -ne 'none') { throw 'No-channel state retained a stale primary pack.' }
    $checks.Add('empty channels clear the primary view')
    $log = Get-Content -LiteralPath (Join-Path $testRoot 'service.log') -Raw
    if ($log -match 'failed to play|playback failed|failed to decode|failed to add file') { throw 'Service logged an audio playback failure.' }
    $checks.Add('no decode or playback errors in isolated runtime')
    $result = @{passed=$true;checks=@($checks.ToArray());testRoot=$testRoot;executable=$Executable}
    if ($ResultPath) { $result | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $ResultPath -Encoding utf8 }
    $result | ConvertTo-Json -Depth 8
}
finally {
    if ($process -and -not $process.HasExited) {
        try { $null = Post-Json '/shutdown' @{}; $null = $process.WaitForExit(5000) } catch {}
        if (-not $process.HasExited) { Stop-Process -Id $process.Id -Force }
    }
    $env:KILLCONFIRM_DATA_ROOT = $oldDataRoot
}

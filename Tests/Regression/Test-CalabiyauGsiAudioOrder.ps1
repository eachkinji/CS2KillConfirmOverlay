param([Parameter(Mandatory)][string]$Executable, [string]$ResultPath = '')
$ErrorActionPreference = 'Stop'
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$serviceRoot = Join-Path $repository 'KillConfirmService'
$Executable = (Resolve-Path -LiteralPath $Executable).Path
$testRoot = Join-Path $repository ('Output/CalabiyauGsiAudioOrder-' + [guid]::NewGuid().ToString('N'))
$sourceAudio = Join-Path $repository 'SourceAssets/GameStyles/calabiyau/soundpacks/calabiyau/1kill.wav'
$token = '1234567890abcdef1234567890abcdef'
$null = New-Item -ItemType Directory -Path $testRoot
Set-Content -LiteralPath (Join-Path $testRoot 'service-auth-token.txt') -Value $token -NoNewline
$packs = @{}
foreach ($definition in @(
    @{key='effect';style='calabiyau';kind='sound_effect'},
    @{key='voice';style='calabiyau';kind='character_voice'},
    @{key='other';style='valorant';kind='sound_effect'}
)) {
    $folder = Join-Path $testRoot $definition.key
    $null = New-Item -ItemType Directory -Path $folder
    $audioFile = $definition.key + '-gsi.wav'
    Copy-Item -LiteralPath $sourceAudio -Destination (Join-Path $folder $audioFile)
    @{
        id=$definition.key;game_style=$definition.style;audio_kind=$definition.kind
        audio_routing='generic_event';audio=@{base_gain=1;slots=@{kill_1=$audioFile}}
    } | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $folder 'manifest.json') -Encoding utf8
    $packs[$definition.key] = @{preset=$definition.key;custom_path=$folder;enabled=$true;volume_percent=100;interrupt_previous=$true}
}
$disabledVoice = @{} + $packs.voice
$disabledVoice.enabled = $false
$cases = @(
    @{name='both';channels=@{sound_effect=$packs.effect;character_voice=$packs.voice};expected=@('effect','voice');ordered=$true},
    @{name='effect_only';channels=@{sound_effect=$packs.effect;character_voice=$null};expected=@('effect');ordered=$true},
    @{name='voice_only';channels=@{sound_effect=$null;character_voice=$packs.voice};expected=@('voice');ordered=$true},
    @{name='disabled_calabiyau_other_game';channels=@{sound_effect=$packs.other;character_voice=$disabledVoice};expected=@('other');ordered=$false},
    @{name='disabled_all';channels=@{sound_effect=$null;character_voice=$disabledVoice};expected=@();ordered=$false},
    @{name='legacy_calabiyau';legacy=$true;expected=@('effect');ordered=$true}
)
$listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 0)
$listener.Start(); $port = $listener.LocalEndpoint.Port; $listener.Stop()
$baseUri = 'http://127.0.0.1:' + $port
$headers = @{'x-killconfirm-token'=$token}
$oldDataRoot = $env:KILLCONFIRM_DATA_ROOT
$process = $null
$results = [Collections.Generic.List[object]]::new()
function Post-Json([string]$route, $value) {
    Invoke-RestMethod -Uri ($baseUri + $route) -Headers $headers -Method Post -ContentType 'application/json' -Body ($value | ConvertTo-Json -Depth 12 -Compress)
}
function Read-Trace {
    $path = Join-Path $testRoot 'service-trace.log'
    if (Test-Path -LiteralPath $path) { @(Get-Content -LiteralPath $path) }
}
function Gsi-Payload([string]$playerId, [int]$kills) {
    @{
        auth=@{token='killconfirm'}
        map=@{
            mode='competitive';name='de_dust2';phase='live';round=1;round_wins=@{};num_matches_to_win_series=0
            team_ct=@{score=0;consecutive_round_losses=0;timeouts_remaining=1;matches_won_this_series=0}
            team_t=@{score=0;consecutive_round_losses=0;timeouts_remaining=1;matches_won_this_series=0}
        }
        round=@{phase='live'}
        player=@{
            steamid=$playerId;name='isolated-gsi-test';team='CT'
            state=@{health=100;armor=100;helmet=$true;flashed=0;smoked=0;burning=0;money=1000;round_kills=$kills;round_killhs=0;equip_value=4500;defusekit=$false}
            match_stats=@{kills=$kills;assists=0;deaths=0;mvps=0;score=0}
        }
    }
}
try {
    $env:KILLCONFIRM_DATA_ROOT = $testRoot
    $process = Start-Process -FilePath $Executable -ArgumentList @('--port',$port,'--volume','0','--developer-mode') -WorkingDirectory $serviceRoot -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $testRoot 'stdout.log') -RedirectStandardError (Join-Path $testRoot 'stderr.log')
    $ready = $false
    for ($attempt=0; $attempt -lt 60; $attempt++) {
        if ($process.HasExited) { throw 'Isolated service exited before readiness.' }
        try { $null = Invoke-RestMethod -Uri ($baseUri + '/health') -Headers $headers; $ready=$true; break } catch { Start-Sleep -Milliseconds 100 }
    }
    if (!$ready) { throw 'Isolated service did not become ready.' }
    $scenario = 0
    foreach ($case in $cases) {
        if ($case.legacy) {
            $null = Post-Json '/soundpack' @{preset='effect';custom_path=$packs.effect.custom_path}
        } else {
            $null = Post-Json '/audio/channels' $case.channels
        }
        $cursor = (Invoke-RestMethod -Uri ($baseUri + '/events?skip_backlog=true') -Headers $headers).cursor
        $before = @(Read-Trace).Count
        $playerId = '76561198000000' + (++$scenario).ToString('000')
        $null = Post-Json '/' (Gsi-Payload $playerId 0)
        $null = Post-Json '/' (Gsi-Payload $playerId 1)
        $null = Post-Json '/' (Gsi-Payload $playerId 1)
        $batch = Invoke-RestMethod -Uri ($baseUri + '/events?after=' + $cursor + '&wait_ms=250') -Headers $headers
        $events = @($batch.events)
        if ($events.Count -ne 1 -or $events[0].event_kind -ne 'kill' -or $events[0].kill_count -ne 1) {
            throw ($case.name + ': baseline/kill/repeated payload did not emit exactly one kill.')
        }
        # Observe the real playback resolver, not the /test endpoint. This is a
        # publication-order smoke check; it does not inject state-lock contention.
        $audioLines = @()
        for ($attempt=0; $attempt -lt 40; $attempt++) {
            $audioLines = @(Read-Trace | Select-Object -Skip $before | Where-Object { $_ -match 'Lua returned [1-9][0-9]* sound entries' })
            if ($audioLines.Count -ge $case.expected.Count) { break }
            Start-Sleep -Milliseconds 50
        }
        Start-Sleep -Milliseconds 200
        $audioLines = @(Read-Trace | Select-Object -Skip $before | Where-Object { $_ -match 'Lua returned [1-9][0-9]* sound entries' })
        if ($audioLines.Count -ne $case.expected.Count) { throw ($case.name + ': enabled channels did not each resolve exactly once.') }
        foreach ($key in $case.expected) {
            $matchesForPack = @($audioLines | Where-Object { $_.Contains($key + '-gsi.wav') })
            if ($matchesForPack.Count -ne 1) { throw ($case.name + ': missing or duplicate ' + $key + ' audio.') }
        }
        if ($case.ordered) {
            foreach ($line in $audioLines) {
                if ($line -notmatch '\[unix_ms=(\d+)\]') { throw 'Audio trace has no timestamp.' }
                if ([long]$Matches[1] -lt [long]$events[0].published_unix_ms) { throw ($case.name + ': audio was resolved before event publication.') }
            }
        }
        $results.Add(@{scenario=$case.name;events=$events.Count;audio_resolutions=$audioLines.Count;publication_order_checked=$case.ordered})
    }
    $log = Get-Content -LiteralPath (Join-Path $testRoot 'service.log') -Raw
    if ($log -match '(?i)failed to play|playback failed|failed to decode|failed to add file|GSI payload rejected') { throw 'Runtime reported a GSI or playback failure.' }
    $result = @{passed=$true;executable=$Executable;test_root=$testRoot;scenarios=@($results.ToArray());scope='Real GSI publication and channel-resolution smoke test; no injected lock contention.'}
    $result | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $testRoot 'result.json') -Encoding utf8
    if ($ResultPath) { $result | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $ResultPath -Encoding utf8 }
    $result | ConvertTo-Json -Depth 8
} finally {
    if ($process -and !$process.HasExited) {
        try { $null = Post-Json '/shutdown' @{}; $null=$process.WaitForExit(5000) } catch {}
        if (!$process.HasExited) { Stop-Process -Id $process.Id -Force }
    }
    $env:KILLCONFIRM_DATA_ROOT = $oldDataRoot
}

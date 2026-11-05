param([string]$Executable='', [string]$PackRoot='', [string]$OutputDir='')
$ErrorActionPreference='Stop'
$repository=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
if(!$Executable){$Executable=Join-Path $repository 'KillConfirmService/target/release/cskillconfirm.exe'}
if(!$PackRoot){$PackRoot=Join-Path $repository 'Output/CalabiyauAudioPacks37'}
if(!$OutputDir){$OutputDir=Join-Path $repository ('Output/CalabiyauAudioValidation-'+[guid]::NewGuid().ToString('N'))}
$OutputDir=[IO.Path]::GetFullPath($OutputDir)
$prefix=[IO.Path]::GetFullPath((Join-Path $repository 'Output')).TrimEnd('\')+'\'
if(!$OutputDir.StartsWith($prefix,[StringComparison]::OrdinalIgnoreCase)){throw 'Validation output must stay in repository Output.'}
New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
$profile=Join-Path $OutputDir 'profile'
New-Item -ItemType Directory -Path $profile -Force | Out-Null
$token=[guid]::NewGuid().ToString('N')+[guid]::NewGuid().ToString('N')
[IO.File]::WriteAllText((Join-Path $profile 'service-auth-token.txt'),$token)
$listener=[Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback,0)
$listener.Start();$port=$listener.LocalEndpoint.Port;$listener.Stop()
$start=[Diagnostics.ProcessStartInfo]::new([IO.Path]::GetFullPath($Executable))
$start.UseShellExecute=$false
$start.WindowStyle=[Diagnostics.ProcessWindowStyle]::Hidden
$start.WorkingDirectory=Join-Path $repository 'KillConfirmService'
foreach($arg in @('--port',"$port",'--volume','0','--developer-mode')){$start.ArgumentList.Add($arg)}
$start.Environment['KILLCONFIRM_DATA_ROOT']=$profile
$process=[Diagnostics.Process]::Start($start)
$base="http://127.0.0.1:$port"
$headers=@{'x-killconfirm-token'=$token}
$checked=[Collections.Generic.List[object]]::new()
try {
    $ready=$false
    for($i=0;$i -lt 100;$i++){
        if($process.HasExited){throw 'Validation service exited.'}
        try{$null=Invoke-RestMethod "$base/health" -Headers $headers -TimeoutSec 1;$ready=$true;break}catch{Start-Sleep -Milliseconds 100}
    }
    if(!$ready){throw 'Validation service did not become ready.'}
    function Set-Channels($channels){
        $body=[Text.Encoding]::UTF8.GetBytes(($channels|ConvertTo-Json -Depth 12 -Compress))
        Invoke-RestMethod "$base/audio/channels" -Headers $headers -Method Post -ContentType 'application/json; charset=utf-8' -Body $body
    }
    $index=Get-Content -LiteralPath (Join-Path $PackRoot 'index.json') -Raw -Encoding utf8|ConvertFrom-Json
    $effects=$null;$voice=$null
    foreach($pack in $index){
        $archive=Join-Path $PackRoot $pack.file
        if((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash -ne $pack.sha256){throw "Archive checksum failed: $($pack.id)"}
        $destination=Join-Path $OutputDir ('packs/'+$pack.id)
        [IO.Compression.ZipFile]::ExtractToDirectory($archive,$destination)
        $manifestFile=Get-ChildItem -LiteralPath $destination -Recurse -Filter manifest.json -File|Select-Object -First 1
        $manifest=Get-Content -LiteralPath $manifestFile.FullName -Raw -Encoding utf8|ConvertFrom-Json
        $selection=@{preset=$manifest.id;custom_path=$manifestFile.DirectoryName;volume_percent=100;enabled=$true;interrupt_previous=$true}
        if($manifest.audio_kind -eq 'character_voice'){$voice=$selection}else{$effects=$selection}
        $response=Set-Channels @{sound_effect=$effects;character_voice=$voice}
        if($response.($manifest.audio_kind).preset -ne $manifest.id){throw "Pack selected on the wrong channel: $($pack.id)"}
        foreach($count in @(1,2,5,8)){
            $null=Invoke-RestMethod "$base/test/$($count)?audio=true&audio_channel=all&headshot=true" -Headers $headers
        }
        $checked.Add(@{id=$manifest.id;kind=$manifest.audio_kind;name=$manifest.display_name;slots=@($manifest.audio.slots.PSObject.Properties.Name).Count})
    }
    Start-Sleep -Milliseconds 800
    $null=Set-Channels @{sound_effect=$null;character_voice=$null}
    $log=Get-Content -LiteralPath (Join-Path $profile 'service.log') -Raw
    if($log -match 'failed to play|playback failed|failed to decode|failed to add file'){throw 'A native module failed runtime decoding/playback; inspect isolated service.log.'}
    $report=@{passed=$true;packs=$checked.Count;sound_effect_packs=@($checked|Where-Object kind -EQ 'sound_effect').Count;
        character_voice_packs=@($checked|Where-Object kind -EQ 'character_voice').Count;muted=$true;checks=$checked.ToArray()}
    $report|ConvertTo-Json -Depth 8|Set-Content -LiteralPath (Join-Path $OutputDir 'result.json') -Encoding utf8
    Write-Output "PASS: $($checked.Count) real native audio packages loaded; both channels previewed without decoder errors. $OutputDir"
}
finally {
    if(!$process.HasExited){
        try{$null=Invoke-RestMethod "$base/shutdown" -Headers $headers -Method Post;$null=$process.WaitForExit(5000)}catch{}
        if(!$process.HasExited){$process.Kill()}
    }
}

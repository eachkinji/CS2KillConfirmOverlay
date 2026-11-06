param(
    [Parameter(Mandatory)][string]$Payload,
    [Parameter(Mandatory)][string]$LibraryRoot,
    [string]$SourceProfile = (Join-Path $env:LOCALAPPDATA 'KillConfirmOverlay/UserData')
)
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$Payload=(Resolve-Path -LiteralPath $Payload).Path
$LibraryRoot=(Resolve-Path -LiteralPath $LibraryRoot).Path
$profile=Join-Path $repo ('Output/PackScroll42-'+[guid]::NewGuid().ToString('N'))
$packs=Join-Path $profile 'Packs'
New-Item -ItemType Directory -Path $packs -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $SourceProfile 'pack-catalog.json') -Destination $profile
# Imported Calabiyau covers are read through the isolated catalog. VAL packs
# are discovered from the library instead, so copy both external folders;
# audio slot repair must run only on these copies, never on the user's files.
foreach($relative in @('valorant/icon_packs','valorant/voice_packs','calabiyau/voice_packs')) {
    $source=Join-Path $LibraryRoot $relative
    $target=Join-Path $packs $relative
    New-Item -ItemType Directory -Path $target -Force | Out-Null
    & robocopy $source $target /E /R:1 /W:1 /NFL /NDL /NJH /NJS /NP | Out-Null
    if($LASTEXITCODE -ge 8){throw "Could not copy pack fixture: $relative"}
}
# Calabiyau discovery rebuilds its icon index from ResourceLibrary.Root. A
# cover-only browsing fixture needs the original manifest, cover and first
# clip marker; the hundreds of animation textures are never played here.
$calabiyauSource=Join-Path $LibraryRoot 'calabiyau/icon_packs'
$calabiyauTarget=Join-Path $packs 'calabiyau/icon_packs'
& robocopy $calabiyauSource $calabiyauTarget manifest.json pack_head.png lower-1.json.gz /S /R:1 /W:1 /NFL /NDL /NJH /NJS /NP | Out-Null
if($LASTEXITCODE -ge 8){throw 'Could not copy Calabiyau cover fixture.'}
$before=Get-ChildItem -LiteralPath $LibraryRoot -File -Recurse | Sort-Object FullName | Select-Object FullName,Length,LastWriteTimeUtc | ConvertTo-Json -Compress
$settings=@{'LocalService.Port'=@{Kind='int';Text='10095'}; 'ResourceLibrary.Root'=@{Kind='string';Text=$packs}; 'GameStyleMode'=@{Kind='string';Text='valorant'}}
[IO.File]::WriteAllText((Join-Path $profile 'settings.json'),($settings | ConvertTo-Json -Depth 4))
$display=Join-Path $profile 'CompatibilityDisplay'
New-Item -ItemType Directory -Path $display -Force | Out-Null
[IO.File]::WriteAllText((Join-Path $display 'display.json'),'{"Version":2,"Enabled":true,"GameBarBlocked":true,"FollowGame":false,"HideWhenInactive":false}')
$start=[Diagnostics.ProcessStartInfo]::new((Join-Path $Payload 'KillConfirmControlPanel.exe'))
$start.UseShellExecute=$false; $start.WindowStyle='Hidden'
$start.Environment['KILLCONFIRM_DATA_ROOT']=$profile
$start.Environment['KILLCONFIRM_INSTALL_ROOT']=$Payload
$start.Environment['KILLCONFIRM_UI_VALIDATION']='1'
$start.Environment['KILLCONFIRM_SCROLL_VALIDATION']='1'
$process=$null
try {
    $process=[Diagnostics.Process]::Start($start)
    Write-Output "Scroll stress profile: $profile"
    $deadline=[DateTimeOffset]::UtcNow.AddSeconds(110)
    while(!$process.WaitForExit(1000) -and [DateTimeOffset]::UtcNow -lt $deadline) { }
    if(!$process.HasExited){throw 'Pack scrolling validation timed out.'}
    $failure=Join-Path $profile 'ui-failure.txt'
    if(Test-Path -LiteralPath $failure){throw [IO.File]::ReadAllText($failure)}
    $pass=Join-Path $profile 'ui-pass.txt'
    if(!(Test-Path -LiteralPath $pass)){throw 'Control panel exited without a scrolling pass report; inspect crash logs.'}
    Get-Content -LiteralPath $pass
} finally {
    if($process -and !$process.HasExited){$process.Kill();$process.WaitForExit(5000) | Out-Null}
    $after=Get-ChildItem -LiteralPath $LibraryRoot -File -Recurse | Sort-Object FullName | Select-Object FullName,Length,LastWriteTimeUtc | ConvertTo-Json -Compress
    $unchanged=$before -eq $after
    [IO.File]::WriteAllText((Join-Path $profile 'library-readonly-result.json'),(@{UserLibraryUnchanged=$unchanged} | ConvertTo-Json))
    if(!$unchanged){throw 'The source library changed during scrolling validation.'}
}

param([Parameter(Mandatory)][string]$Payload)
$ErrorActionPreference='Stop'
$repository=Split-Path $PSScriptRoot -Parent
$output=Join-Path $repository ('Output/ResourceBroker-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
$profile=Join-Path $output 'profile'
$library=Join-Path $output '外置素材库'
New-Item -ItemType Directory -Path $profile,$library -Force | Out-Null
[IO.File]::WriteAllText((Join-Path $library 'fixture.png'),'material fixture bytes')
[IO.File]::WriteAllText((Join-Path $output 'outside.txt'),'not a material')
[IO.File]::WriteAllText((Join-Path $profile 'settings.json'),(@{'ResourceLibrary.Root'=@{Kind='string';Text=$library}}|ConvertTo-Json -Depth 5))
$listener=[Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback,0)
$listener.Start();$port=$listener.LocalEndpoint.Port;$listener.Stop()
$start=[Diagnostics.ProcessStartInfo]::new((Join-Path ([IO.Path]::GetFullPath($Payload)) 'KillConfirmService/cskillconfirm.exe'))
$start.UseShellExecute=$false;$start.CreateNoWindow=$true;$start.WindowStyle='Hidden'
$start.Environment['KILLCONFIRM_DATA_ROOT']=$profile
$start.Environment['KILLCONFIRM_INSTALL_ROOT']=[IO.Path]::GetFullPath($Payload)
$start.ArgumentList.Add('--port');$start.ArgumentList.Add($port.ToString())
$process=[Diagnostics.Process]::Start($start)
try {
    $base="http://127.0.0.1:$port"
    $snapshot=$null
    for($attempt=0;$attempt -lt 40;$attempt++) {
        try {
            $headers=@{'x-killconfirm-token'=[IO.File]::ReadAllText((Join-Path $profile 'service-auth-token.txt')).Trim()}
            $snapshot=Invoke-RestMethod "$base/shared/state" -Headers $headers -TimeoutSec 1
            break
        } catch {Start-Sleep -Milliseconds 100}
    }
    if(!$snapshot -or $snapshot.libraryRoot -ne $library){throw 'Material root was not shared with the widget.'}
    if($snapshot.installRoot -ne [IO.Path]::GetFullPath($Payload)){throw 'Installation root was not shared with the widget.'}
    $download=Join-Path $output 'download.png'
    Invoke-WebRequest "$base/shared/resource?path=$([Uri]::EscapeDataString((Join-Path $library 'fixture.png')))" -Headers $headers -OutFile $download
    if((Get-FileHash $download).Hash -ne (Get-FileHash (Join-Path $library 'fixture.png')).Hash){throw 'Resource bytes changed.'}
    $listing=Invoke-RestMethod "$base/shared/folder?path=$([Uri]::EscapeDataString($library))" -Headers $headers
    if($listing.files -notcontains 'fixture.png'){throw 'Relocated material directory listing failed.'}
    foreach($case in @(@{Headers=@{};Path=(Join-Path $library 'fixture.png');Status=401},@{Headers=$headers;Path=(Join-Path $output 'outside.txt');Status=403})) {
        try {Invoke-WebRequest "$base/shared/resource?path=$([Uri]::EscapeDataString($case.Path))" -Headers $case.Headers | Out-Null;throw 'Unexpected access allowed.'}
        catch {if([int]$_.Exception.Response.StatusCode -ne $case.Status){throw}}
    }
    [IO.File]::WriteAllText((Join-Path $output 'pass.txt'),'PASS: real service shares configured external material path; exact file bytes and listing; authenticated requests; sibling-folder denial.')
    Get-Content -LiteralPath (Join-Path $output 'pass.txt')
    Write-Output "Artifacts: $output"
} finally {if(!$process.HasExited){$process.Kill();$process.WaitForExit()};$process.Dispose()}

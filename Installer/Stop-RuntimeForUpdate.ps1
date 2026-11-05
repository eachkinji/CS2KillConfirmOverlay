param([Parameter(Mandatory)][string]$InstallRoot, [switch]$ListOnly, [switch]$Isolated)
$ErrorActionPreference='Stop'
$roots=@([IO.Path]::GetFullPath($InstallRoot).TrimEnd('\')+'\')
$registration=Join-Path $env:LOCALAPPDATA 'KillConfirmOverlay/install-root.txt'
if(!$Isolated -and (Test-Path -LiteralPath $registration)) {
    $old=[IO.File]::ReadAllText($registration).Trim()
    if($old -and [IO.Path]::IsPathRooted($old)) {$roots+=([IO.Path]::GetFullPath($old).TrimEnd('\')+'\')}
}
$names=@('KillConfirmControlPanel','KillConfirmGameBar','KillConfirmCompatibility','cskillconfirm','killconfirm-settings-launcher','killconfirm-widget-bridge','KillConfirmOverlay','TestXboxGameBar')
function Get-OwnedRuntime {
    foreach($process in @(Get-Process -Name $names -ErrorAction SilentlyContinue)) {
        $path=$process.Path
        if(!$path){throw "Cannot inspect runtime process $($process.Id)."}
        $owned=$false
        foreach($root in $roots) {if($path.StartsWith($root,[StringComparison]::OrdinalIgnoreCase)) {$owned=$true}}
        if(!$Isolated -and (Split-Path (Split-Path $path -Parent) -Leaf) -like 'KillConfirmGameBar.Overlay_*') {$owned=$true}
        if($owned -and $process.Id -ne $PID) {$process}
    }
}
if($ListOnly) {Get-OwnedRuntime | Select-Object Id,ProcessName,Path; exit 0}
try {
    foreach($process in @(Get-OwnedRuntime)) {try {$null=$process.CloseMainWindow()} catch {}}
    Start-Sleep -Milliseconds 600
    for($attempt=0;$attempt -lt 6;$attempt++) {
        $remaining=@(Get-OwnedRuntime)
        if(!$remaining.Count) {exit 0}
        foreach($process in $remaining) {
            try {Stop-Process -Id $process.Id -Force -ErrorAction Stop} catch {if(Get-Process -Id $process.Id -ErrorAction SilentlyContinue){Write-Warning $_}}
        }
        Start-Sleep -Milliseconds 500
    }
    if(@(Get-OwnedRuntime).Count) {throw 'KillConfirm processes are still running.'}
    exit 0
} catch {Write-Error $_; exit 1}

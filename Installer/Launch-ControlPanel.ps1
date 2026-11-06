param([Parameter(Mandatory = $true)][string]$InstallRoot)

$ErrorActionPreference = 'Stop'
try {
    # ExecAsOriginalUser normally restores Setup's unelevated initiating token.
    # If Setup itself was launched elevated, never pass that token to the app.
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]::new($identity)
    if ($principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        exit 42
    }
    $panelRoot = [IO.Path]::GetFullPath($InstallRoot)
    $panelPath = Join-Path $panelRoot 'KillConfirmControlPanel.exe'
    if (-not (Test-Path -LiteralPath $panelPath -PathType Leaf)) { exit 43 }
    $start = [Diagnostics.ProcessStartInfo]::new()
    $start.FileName = $panelPath
    $start.WorkingDirectory = $panelRoot
    $start.UseShellExecute = $false
    $process = [Diagnostics.Process]::Start($start)
    if ($null -eq $process) { exit 44 }
    $process.Dispose()
    exit 0
}
catch {
    Write-Error $_ -ErrorAction Continue
    exit 44
}

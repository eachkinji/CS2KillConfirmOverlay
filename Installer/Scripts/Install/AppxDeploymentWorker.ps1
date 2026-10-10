param([string]$RequestPath,[string]$ResultPath)
$ErrorActionPreference='Stop'
$ProgressPreference='SilentlyContinue'
$request=Get-Content -LiteralPath $RequestPath -Raw -Encoding UTF8 | ConvertFrom-Json
$parameters=@{Path=[string]$request.Path;ErrorAction='Stop'}
if($request.ForceUpdateFromAnyVersion){$parameters.ForceUpdateFromAnyVersion=$true}
if($request.DeferRegistrationWhenPackagesAreInUse){$parameters.DeferRegistrationWhenPackagesAreInUse=$true}
try {
    if ($request.Operation -eq 'Remove') {
        if ([string]$request.PackageFullName -notmatch '^KillConfirmGameBar\.Overlay_.*_5jgcw66eyez0m$') {throw 'Unexpected package removal identity.'}
        Remove-AppxPackage -Package ([string]$request.PackageFullName) -ErrorAction Stop
    }
    else { Add-AppxPackage @parameters }
    $result=@{Success=$true}
}
catch {
    $result=@{Success=$false;Message=$_.Exception.Message;Details=($_ | Format-List * -Force | Out-String);ActivityId=[string]$_.Exception.ActivityId}
}
$result | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $ResultPath -Encoding UTF8
if(!$result.Success){exit 1}

param([string]$Payload='')
$ErrorActionPreference='Stop'
$repository=Split-Path $PSScriptRoot -Parent
if(!$Payload) {$Payload=Join-Path $repository 'Output/StandaloneTest'}
$Payload=[IO.Path]::GetFullPath($Payload)
$name='KillConfirmGameBar.WidgetValidation'
if(Get-AppxPackage -Name $name -ErrorAction SilentlyContinue) {throw 'A widget bridge validation package already exists.'}
$output=Join-Path $repository ('Output/WidgetBridge-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
$layout=Join-Path $output 'package'
New-Item -ItemType Directory -Path (Join-Path $layout 'Assets') -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $repository 'KillConfirmService/target/release/killconfirm-widget-bridge.exe') -Destination $layout
foreach($logo in @('StoreLogo.png','Square150x150Logo.scale-200.png','Square44x44Logo.scale-200.png')) {Copy-Item -LiteralPath (Join-Path $repository "Widget/Assets/$logo") -Destination (Join-Path $layout "Assets/$logo")}
@'
<Package xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10" xmlns:uap="http://schemas.microsoft.com/appx/manifest/uap/windows10" xmlns:rescap="http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities" IgnorableNamespaces="uap rescap">
 <Identity Name="KillConfirmGameBar.WidgetValidation" Publisher="CN=WidgetValidation" Version="1.0.0.0" ProcessorArchitecture="x64" />
 <Properties><DisplayName>Widget bridge validation</DisplayName><PublisherDisplayName>Validation</PublisherDisplayName><Logo>Assets\StoreLogo.png</Logo></Properties>
 <Resources><Resource Language="en-us" /></Resources>
 <Dependencies><TargetDeviceFamily Name="Windows.Desktop" MinVersion="10.0.19041.0" MaxVersionTested="10.0.26100.0" /></Dependencies>
 <Applications><Application Id="Probe" Executable="killconfirm-widget-bridge.exe" EntryPoint="Windows.FullTrustApplication"><uap:VisualElements AppListEntry="none" DisplayName="Validation" Description="Validation" BackgroundColor="transparent" Square150x150Logo="Assets\Square150x150Logo.scale-200.png" Square44x44Logo="Assets\Square44x44Logo.scale-200.png" /></Application></Applications>
 <Capabilities><rescap:Capability Name="runFullTrust" /></Capabilities>
</Package>
'@ | Set-Content -LiteralPath (Join-Path $layout 'AppxManifest.xml') -Encoding utf8
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
[ComImport,Guid("2e941141-7f97-4756-ba1d-9decde894a3d"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IWidgetBridgeActivation { [PreserveSig] int ActivateApplication([MarshalAs(UnmanagedType.LPWStr)] string id,[MarshalAs(UnmanagedType.LPWStr)] string arguments,int options,out uint pid); }
public static class WidgetBridgeActivation {
 public static void Activate(string id,string arguments) {var type=Type.GetTypeFromCLSID(new Guid("45BA127D-10A8-46EA-8AB7-56EA9078943C"));var activation=(IWidgetBridgeActivation)Activator.CreateInstance(type);uint pid;Marshal.ThrowExceptionForHR(activation.ActivateApplication(id,arguments,3,out pid));}
}
'@
$base='http://127.0.0.1:10095'
$headers=$null
try {
    Add-AppxPackage -Register (Join-Path $layout 'AppxManifest.xml')
    $package=Get-AppxPackage -Name $name -ErrorAction Stop
    $cache=Join-Path $env:LOCALAPPDATA "Packages/$($package.PackageFamilyName)/LocalState"
    New-Item -ItemType Directory -Path $cache -Force | Out-Null
    [IO.File]::WriteAllText((Join-Path $cache 'install-root.txt'),$Payload)
    [WidgetBridgeActivation]::Activate(($package.PackageFamilyName+'!Probe'),'--port 10095')
    $data=Join-Path $cache 'Standalone/UserData'
    $identity=$null
    for($attempt=0;$attempt -lt 40;$attempt++) {
        try {
            $headers=@{'x-killconfirm-token'=(Get-Content -LiteralPath (Join-Path $data 'service-auth-token.txt') -Raw).Trim()}
            $identity=Invoke-RestMethod "$base/shared/identity" -Headers $headers -TimeoutSec 1
            break
        } catch {Start-Sleep -Milliseconds 250}
    }
    if(!$identity -or $identity.packageIdentityCode -ne 15700) {throw 'Packaged bridge did not start the real service without package identity.'}
    foreach($case in @(@{Kind='bool';Text='True'},@{Kind='int';Text='17'},@{Kind='double';Text='1.25'},@{Kind='string';Text='shared'})) {
        Invoke-RestMethod "$base/shared/setting" -Headers $headers -Method Post -ContentType 'application/json' -Body (@{key="BridgeTest.$($case.Kind)";value=$case}|ConvertTo-Json) | Out-Null
    }
    $snapshot=Invoke-RestMethod "$base/shared/state" -Headers $headers
    if($snapshot.settings.'BridgeTest.int'.Text -ne '17') {throw 'Shared settings merge failed.'}
    $asset='Assets/GameStyles/overwatch/killconfirm/textures/kill_icon_white.png'
    $file=Join-Path $output 'shared-texture.png'
    Invoke-WebRequest "$base/shared/resource?asset=true&path=$([Uri]::EscapeDataString($asset))" -Headers $headers -OutFile $file
    if((Get-FileHash $file).Hash -ne (Get-FileHash (Join-Path $Payload $asset)).Hash) {throw 'Shared texture differs from ordinary resources.'}
    foreach($request in @(@{Url="$base/shared/state";Headers=@{};Code=401},@{Url="$base/shared/resource?asset=true&path=KillConfirmGameBar.exe";Headers=$headers;Code=403})) {
        try {Invoke-WebRequest $request.Url -Headers $request.Headers | Out-Null;throw 'Resource access unexpectedly succeeded.'}
        catch {if([int]$_.Exception.Response.StatusCode -ne $request.Code) {throw}}
    }
    [IO.File]::WriteAllText((Join-Path $output 'pass.txt'),'PASS: packaged widget bridge starts unpackaged core; shared typed settings, exact resource bytes, authorization and resource-path boundaries.')
    Get-Content -LiteralPath (Join-Path $output 'pass.txt')
}
finally {
    if($headers) {try {Invoke-RestMethod "$base/shutdown" -Method Post -Headers $headers | Out-Null} catch {}}
    $package=Get-AppxPackage -Name $name -ErrorAction SilentlyContinue
    if($package -and $package.Name -eq $name) {Remove-AppxPackage -Package $package.PackageFullName}
}

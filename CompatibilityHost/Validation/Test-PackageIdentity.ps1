#Requires -Version 7.0
param([switch]$SkipPublish)
$ErrorActionPreference = 'Stop'
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$packageName = 'KillConfirmCompatibility.Validation'
if (Get-AppxPackage -Name $packageName -ErrorAction SilentlyContinue) { throw 'An identity validation package already exists; refusing to overwrite it.' }
if (-not $SkipPublish) { & (Join-Path $repository 'CompatibilityHost/Build-CompatibilityHost.ps1') -Configuration Debug }
$output = Join-Path $repository ('Output/CompatibilityIdentity-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
$layout = Join-Path $output 'package'
New-Item -ItemType Directory -Path (Join-Path $layout 'Assets/KillConfirmCode/Csol4') -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $repository 'Widget/CompatibilityHost') -Destination $layout -Recurse
Copy-Item -LiteralPath (Join-Path $layout 'CompatibilityHost/KillConfirmCompatibility.exe') -Destination (Join-Path $layout 'CompatibilityHost/ProbeSupervisor.exe')
Copy-Item -LiteralPath (Join-Path $layout 'CompatibilityHost/KillConfirmCompatibility.exe') -Destination (Join-Path $layout 'CompatibilityHost/ForeignProbe.exe')
New-Item -ItemType Directory -Path (Join-Path $layout 'KillConfirmService') -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $repository 'KillConfirmService/target/release/cskillconfirm.exe') -Destination (Join-Path $layout 'KillConfirmService/cskillconfirm.exe')
$legacyCanvas = Join-Path $repository 'Widget/bin/x64/Debug/Microsoft.Graphics.Canvas.dll'
if (Test-Path -LiteralPath $legacyCanvas) { Copy-Item -LiteralPath $legacyCanvas -Destination $layout }
Copy-Item -LiteralPath (Join-Path $repository 'Widget/Assets/KillConfirmCode/Csol4/3kill.png') -Destination (Join-Path $layout 'Assets/KillConfirmCode/Csol4/3kill.png')
foreach ($name in @('StoreLogo.png', 'Square150x150Logo.scale-200.png', 'Square44x44Logo.scale-200.png')) {
    Copy-Item -LiteralPath (Join-Path $repository "Widget/Assets/$name") -Destination (Join-Path $layout "Assets/$name")
}
@'
<Package xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10" xmlns:uap="http://schemas.microsoft.com/appx/manifest/uap/windows10" xmlns:rescap="http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities" IgnorableNamespaces="uap rescap">
  <Identity Name="KillConfirmCompatibility.Validation" Publisher="CN=CompatibilityValidation" Version="1.0.0.0" ProcessorArchitecture="x64" />
  <Properties><DisplayName>Compatibility validation</DisplayName><PublisherDisplayName>Validation</PublisherDisplayName><Logo>Assets\StoreLogo.png</Logo></Properties>
  <Resources><Resource Language="en-us" /></Resources>
  <Dependencies><TargetDeviceFamily Name="Windows.Desktop" MinVersion="10.0.19041.0" MaxVersionTested="10.0.26100.0" /></Dependencies>
  <Applications><Application Id="Probe" Executable="CompatibilityHost\ProbeSupervisor.exe" EntryPoint="Windows.FullTrustApplication"><uap:VisualElements AppListEntry="none" DisplayName="Validation" Description="Validation" BackgroundColor="transparent" Square150x150Logo="Assets\Square150x150Logo.png" Square44x44Logo="Assets\Square44x44Logo.png" /></Application></Applications>
  <Capabilities><rescap:Capability Name="runFullTrust" /></Capabilities>
  <Extensions><Extension Category="windows.activatableClass.inProcessServer"><InProcessServer><Path>Microsoft.Graphics.Canvas.dll</Path><ActivatableClass ActivatableClassId="Microsoft.Graphics.Canvas.CanvasDevice" ThreadingModel="both" /><ActivatableClass ActivatableClassId="Microsoft.Graphics.Canvas.CanvasBitmap" ThreadingModel="both" /></InProcessServer></Extension></Extensions>
</Package>
'@ | Set-Content -LiteralPath (Join-Path $layout 'AppxManifest.xml') -Encoding utf8
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
[ComImport, Guid("2e941141-7f97-4756-ba1d-9decde894a3d"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface ICompatibilityProbeActivation {
    [PreserveSig] int ActivateApplication([MarshalAs(UnmanagedType.LPWStr)] string id, [MarshalAs(UnmanagedType.LPWStr)] string arguments, int options, out uint processId);
}
public static class CompatibilityProbeActivation {
    public static uint Activate(string id, string arguments) {
        var type = Type.GetTypeFromCLSID(new Guid("45BA127D-10A8-46EA-8AB7-56EA9078943C"));
        var activation = (ICompatibilityProbeActivation)Activator.CreateInstance(type);
        uint pid; Marshal.ThrowExceptionForHR(activation.ActivateApplication(id, arguments, 3, out pid)); return pid;
    }
}
'@
try {
    Add-AppxPackage -Register (Join-Path $layout 'AppxManifest.xml')
    $package = Get-AppxPackage -Name $packageName -ErrorAction Stop
    $probeProcessId = [CompatibilityProbeActivation]::Activate(($package.PackageFamilyName + '!Probe'), ('--package-probe "' + $output + '"'))
    $process = [Diagnostics.Process]::GetProcessById($probeProcessId)
    if (-not $process.WaitForExit(30000)) { $process.Kill(); throw 'Identity probe timed out.' }
    if (-not (Test-Path -LiteralPath (Join-Path $output 'package.txt'))) { throw (Get-Content -LiteralPath (Join-Path $output 'package-failure.txt') -Raw -ErrorAction SilentlyContinue) }
    Get-Content -LiteralPath (Join-Path $output 'package.txt')
    Get-Content -LiteralPath (Join-Path $output 'mode-stop.txt')
    Write-Host "Identity artifacts: $output"
}
finally {
    $package = Get-AppxPackage -Name $packageName -ErrorAction SilentlyContinue
    if ($package -and $package.Name -eq $packageName) { Remove-AppxPackage -Package $package.PackageFullName }
}

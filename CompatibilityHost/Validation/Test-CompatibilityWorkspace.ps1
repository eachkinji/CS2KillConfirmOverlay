#Requires -Version 7.0
param([ValidateSet('Debug', 'Release')][string]$Configuration = 'Release')
$ErrorActionPreference = 'Stop'
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$packageName = 'KillConfirmCompatibility.UIValidation'
if (Get-AppxPackage -Name $packageName -ErrorAction SilentlyContinue) { throw 'UI validation package already exists; refusing to overwrite it.' }
$source = Join-Path $repository "Package/bin/x64/$Configuration"
$output = Join-Path $repository ('Output/CompatibilityUI-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
$layout = Join-Path $output 'package'
New-Item -ItemType Directory -Path $layout -Force | Out-Null
$mainPackage = Get-ChildItem -LiteralPath $source -Filter '*.msix' | Where-Object { $_.Name -match '_x64(?:_Debug)?\.msix$' -and $_.Name -notmatch '^~' } | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (-not $mainPackage) { throw "Build a $Configuration main MSIX first." }
# The packaging recipe maps some binaries directly from Widget/ilc and CompatibilityHost.
# Inspect the actual package, rather than assuming the staging folder is complete.
[IO.Compression.ZipFile]::ExtractToDirectory($mainPackage.FullName, $layout)
foreach ($metadata in @('AppxSignature.p7x', 'AppxBlockMap.xml', '[Content_Types].xml')) {
    $metadataPath = Join-Path $layout $metadata
    if (Test-Path -LiteralPath $metadataPath) { Remove-Item -LiteralPath $metadataPath -Force }
}
$manifest = Join-Path $layout 'AppxManifest.xml'
[xml]$xml = Get-Content -LiteralPath $manifest -Raw
$xml.Package.Identity.Name = $packageName
$xml.Package.Properties.DisplayName = 'Compatibility UI validation'
foreach ($application in $xml.Package.Applications.Application) {
    $application.VisualElements.DisplayName = 'Compatibility UI validation'
    if ($application.Extensions) { [void]$application.RemoveChild($application.Extensions) }
}
$xml.Save($manifest)
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
[ComImport, Guid("2e941141-7f97-4756-ba1d-9decde894a3d"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface ICompatibilityUiActivation {
    [PreserveSig] int ActivateApplication([MarshalAs(UnmanagedType.LPWStr)] string id, [MarshalAs(UnmanagedType.LPWStr)] string arguments, int options, out uint processId);
}
public static class CompatibilityUiActivation {
    public static uint Activate(string id) {
        var type = Type.GetTypeFromCLSID(new Guid("45BA127D-10A8-46EA-8AB7-56EA9078943C"));
        var activation = (ICompatibilityUiActivation)Activator.CreateInstance(type);
        uint pid; int result = activation.ActivateApplication(id, "", 2, out pid);
        if (result < 0) throw new Exception("Activation failed: 0x" + result.ToString("X8")); return pid;
    }
}
'@
$probeProcess = $null
$createdDependencies = @()
try {
    $dependencyFiles = @(
        (Join-Path $env:USERPROFILE '.nuget/packages/runtime.win10-x64.microsoft.net.uwpcoreruntimesdk/2.2.14/tools/Appx/Microsoft.NET.CoreRuntime.2.2.appx'),
        (Join-Path $env:USERPROFILE '.nuget/packages/runtime.win10-x64.microsoft.net.uwpcoreruntimesdk/2.2.14/tools/Appx/Microsoft.NET.CoreFramework.Debug.2.2.appx'),
        'C:/Program Files (x86)/Microsoft SDKs/Windows Kits/10/ExtensionSDKs/Microsoft.VCLibs/14.0/Appx/Debug/x64/Microsoft.VCLibs.x64.Debug.14.00.appx'
    )
    foreach ($dependencyFile in $(if ($Configuration -eq 'Debug') { $dependencyFiles } else { @() })) {
        $archive = [IO.Compression.ZipFile]::OpenRead($dependencyFile)
        try {
            $reader = [IO.StreamReader]::new($archive.GetEntry('AppxManifest.xml').Open())
            try { [xml]$dependencyManifest = $reader.ReadToEnd() } finally { $reader.Dispose() }
            $dependencyName = [string]$dependencyManifest.Package.Identity.Name
        } finally { $archive.Dispose() }
        if (-not (Get-AppxPackage -Name $dependencyName | Where-Object Architecture -eq X64)) {
            Add-AppxPackage -Path $dependencyFile
            $createdDependencies = @($dependencyName) + $createdDependencies
        }
    }
    Add-AppxPackage -Register $manifest
    $package = Get-AppxPackage -Name $packageName -ErrorAction Stop
    $probeProcessId = [CompatibilityUiActivation]::Activate(($package.PackageFamilyName + '!App'))
    $probeProcess = [Diagnostics.Process]::GetProcessById($probeProcessId)
    $state = Join-Path $env:LOCALAPPDATA ('Packages/' + $package.PackageFamilyName + '/LocalState')
    $deadline = (Get-Date).AddSeconds(90)
    do {
        Start-Sleep -Milliseconds 500
        if (Test-Path -LiteralPath (Join-Path $state 'ui-failure.txt')) { throw (Get-Content -LiteralPath (Join-Path $state 'ui-failure.txt') -Raw) }
        if (Test-Path -LiteralPath (Join-Path $state 'ui-pass.txt')) { [IO.File]::WriteAllText((Join-Path $output 'ui-pass.txt'), [IO.File]::ReadAllText((Join-Path $state 'ui-pass.txt'))); Get-Content (Join-Path $output 'ui-pass.txt'); return }
    } while ((Get-Date) -lt $deadline -and -not $probeProcess.HasExited)
    if (Test-Path (Join-Path $state 'gamebar-widget.log')) { Copy-Item (Join-Path $state 'gamebar-widget.log') $output }
    throw "UI validation did not complete. Logs: $output"
}
finally {
    if ($state -and (Test-Path -LiteralPath $state)) {
        foreach ($png in Get-ChildItem -LiteralPath $state -Filter 'ui-*.png') {
            [IO.File]::WriteAllBytes((Join-Path $output $png.Name), [IO.File]::ReadAllBytes($png.FullName))
        }
        foreach ($name in @('gamebar-widget.log', 'ui-failure.txt', 'ui-pass.txt')) {
            $file = Join-Path $state $name
            if (Test-Path -LiteralPath $file) { [IO.File]::WriteAllText((Join-Path $output $name), [IO.File]::ReadAllText($file)) }
        }
    }
    if ($probeProcess -and -not $probeProcess.HasExited) { $probeProcess.Kill(); $probeProcess.WaitForExit(5000) | Out-Null }
    $package = Get-AppxPackage -Name $packageName -ErrorAction SilentlyContinue
    if ($package -and $package.Name -eq $packageName) { Remove-AppxPackage -Package $package.PackageFullName }
    foreach ($name in $createdDependencies) {
        $dependency = Get-AppxPackage -Name $name | Where-Object Architecture -eq X64
        if ($dependency) { Remove-AppxPackage -Package $dependency.PackageFullName }
    }
}

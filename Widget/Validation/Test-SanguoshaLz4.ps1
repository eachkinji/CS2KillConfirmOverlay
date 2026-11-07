param(
    [ValidateSet('Prepare','Pixels','Performance','QuickPerformance','StaticPerformance','Collect','Remove')][string]$Action='Prepare',
    [string]$BuildDirectory='',
    [string]$PackagePath='',
    [string]$ProfileDirectory='',
    [string]$EvidenceDirectory=''
)
$ErrorActionPreference='Stop'
$repository=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$name='KillConfirmGameBar.SwitchValidation'
if (!$BuildDirectory) {$BuildDirectory=Join-Path $repository 'Output/SanguoshaProduction-20261107/UwpBuild'}
if (!$ProfileDirectory) {$ProfileDirectory=Join-Path $repository 'Widget/Assets/GameStyles/sanguosha/killconfirm/lz4/uwp-2750x1548-s1p71875'}
if (!$EvidenceDirectory) {$EvidenceDirectory=Join-Path $repository 'Output/SanguoshaLz4Integration-20261107/uwp-evidence'}
if (-not ('SgsLz4Activation' -as [type])) {
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
[ComImport,Guid("2e941141-7f97-4756-ba1d-9decde894a3d"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface ISgsLz4Activation { [PreserveSig] int ActivateApplication([MarshalAs(UnmanagedType.LPWStr)] string id,[MarshalAs(UnmanagedType.LPWStr)] string arguments,int options,out uint pid); }
public static class SgsLz4Activation {
 public static uint Activate(string id,string arguments) {var t=Type.GetTypeFromCLSID(new Guid("45BA127D-10A8-46EA-8AB7-56EA9078943C"));var a=(ISgsLz4Activation)Activator.CreateInstance(t);uint pid;Marshal.ThrowExceptionForHR(a.ActivateApplication(id,arguments,0,out pid));return pid;}
}
[ComImport,Guid("F27C3930-8029-4AD1-94E3-3DBA417810C1"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface ISgsLz4DebugSettings {
 [PreserveSig] int EnableDebugging([MarshalAs(UnmanagedType.LPWStr)] string name,IntPtr debugger,IntPtr environment);
 [PreserveSig] int DisableDebugging([MarshalAs(UnmanagedType.LPWStr)] string name);
}
public static class SgsLz4Lifetime {
 static ISgsLz4DebugSettings Settings() {return (ISgsLz4DebugSettings)Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("B1AEC16F-2383-4852-B0E9-8F0B1DC66B4D")));}
 public static void Enable(string name) {Marshal.ThrowExceptionForHR(Settings().EnableDebugging(name,IntPtr.Zero,IntPtr.Zero));}
 public static void Disable(string name) {Marshal.ThrowExceptionForHR(Settings().DisableDebugging(name));}
}
'@
}
if ($Action -eq 'Prepare') {
    $manifest=Join-Path $BuildDirectory 'AppxManifest.xml'
    [xml]$xml=Get-Content -LiteralPath $manifest -Raw
    if ($xml.Package.Identity.Name -ne $name) {throw 'Refusing to register a non-isolated package.'}
    $existing=Get-AppxPackage -Name $name -ErrorAction SilentlyContinue
    if ($existing) {throw 'Isolated test package already registered. Collect/remove it explicitly before preparing a new build.'}
    if (!$PackagePath) {
        $signed=Get-ChildItem -LiteralPath (Join-Path $repository 'Output/SanguoshaProduction-20261107/UwpPackage') -Recurse -File -Filter 'KillConfirmGameBar*.msix' | Sort-Object LastWriteTime -Descending | Select-Object -First 1
        if ($signed) {$PackagePath=$signed.FullName}
    }
    if (!$PackagePath) {throw 'Build a signed isolated MSIX; do not enable machine-wide developer mode for this test.'}
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip=[IO.Compression.ZipFile]::OpenRead($PackagePath)
    try {
        $reader=[IO.StreamReader]::new($zip.GetEntry('AppxManifest.xml').Open())
        try {[xml]$packageManifest=$reader.ReadToEnd()} finally {$reader.Dispose()}
        if ($packageManifest.Package.Identity.Name -ne $name) {throw 'Refusing a non-isolated MSIX.'}
    } finally {$zip.Dispose()}
    Add-AppxPackage -Path $PackagePath
}
$package=Get-AppxPackage -Name $name -ErrorAction Stop
if (!$package -or $package.Name -ne $name) {throw 'Isolated SGS validation package is not registered.'}
$local=Join-Path $env:LOCALAPPDATA "Packages/$($package.PackageFamilyName)/LocalState"
$result=Join-Path $local 'SGS-LZ4-test'
if ($Action -eq 'Prepare') {
    $fixture=Join-Path $local 'Fixture'
    New-Item -ItemType Directory -Path $fixture -Force | Out-Null
    [IO.File]::WriteAllText((Join-Path $fixture 'settings.json'),'{"GameStyleMode":{"Kind":"string","Text":"sanguosha"},"IconPack":{"Kind":"string","Text":"sanguosha"},"IconPack.sanguosha":{"Kind":"string","Text":"sanguosha"},"LocalService.Port":{"Kind":"int","Text":"10095"},"CompatibilityDisplay.Enabled":{"Kind":"bool","Text":"False"}}')
    [IO.File]::WriteAllText((Join-Path $fixture 'pack-catalog.json'),'{"Version":1,"IconPacks":[],"VoicePacks":[]}')
    $source=Join-Path $repository 'Widget/Assets/GameStyles/sanguosha/killconfirm'
    $cache=Join-Path $local 'SharedResources-v51/Assets/GameStyles/sanguosha/killconfirm'
    foreach ($folder in Get-ChildItem -LiteralPath $source -Directory) {
        $clip=Join-Path $folder.FullName ($folder.Name+'.sgsd.gz')
        if (!(Test-Path -LiteralPath $clip)) {continue}
        $target=Join-Path $cache $folder.Name
        New-Item -ItemType Directory -Path $target -Force | Out-Null
        Copy-Item -LiteralPath $clip -Destination $target
    }
    $tier='shenweizhengqiankun'
    Get-ChildItem -LiteralPath (Join-Path $source $tier) -Filter '*.png' -File | ForEach-Object {Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $cache $tier)}
    $profileId=Split-Path $ProfileDirectory -Leaf
    $targetProfile=Join-Path $cache ('lz4/'+$profileId)
    New-Item -ItemType Directory -Path $targetProfile -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $ProfileDirectory ($tier+'.sgslz4')) -Destination $targetProfile
    [pscustomobject]@{Package=$package.PackageFullName;LocalState=$local;Output=$result;Profile=$profileId;Activated=$false} | ConvertTo-Json
}
elseif ($Action -eq 'Pixels' -or $Action -eq 'Performance' -or $Action -eq 'QuickPerformance' -or $Action -eq 'StaticPerformance') {
    New-Item -ItemType Directory -Path $result -Force | Out-Null
    foreach ($leaf in @('failure.txt','phase.txt','environment.json','pixels.json','pixel-detail.json','performance.json','frames.json')) {
        $old=Join-Path $result $leaf
        if (Test-Path -LiteralPath $old) {Remove-Item -LiteralPath $old}
    }
    [SgsLz4Lifetime]::Enable($package.PackageFullName)
    $arguments=if ($Action -eq 'Pixels') {'pixels'} elseif ($Action -eq 'QuickPerformance') {'performance quick'} elseif ($Action -eq 'StaticPerformance') {'performance quick static'} else {'performance'}
    $processId=[SgsLz4Activation]::Activate(($package.PackageFamilyName+'!App'),$arguments)
    [pscustomobject]@{Pid=$processId;Mode=$Action;Output=$result;Package=$package.PackageFullName} | ConvertTo-Json
}
elseif ($Action -eq 'Collect') {
    New-Item -ItemType Directory -Path $EvidenceDirectory -Force | Out-Null
    if (Test-Path -LiteralPath $result) {foreach ($file in Get-ChildItem -LiteralPath $result -File) {[IO.File]::WriteAllBytes((Join-Path $EvidenceDirectory $file.Name),[IO.File]::ReadAllBytes($file.FullName))}}
    [SgsLz4Lifetime]::Disable($package.PackageFullName)
    [pscustomobject]@{Evidence=$EvidenceDirectory;Package=$package.PackageFullName} | ConvertTo-Json
}
elseif ($Action -eq 'Remove') {
    [SgsLz4Lifetime]::Disable($package.PackageFullName)
    Remove-AppxPackage -Package $package.PackageFullName
}

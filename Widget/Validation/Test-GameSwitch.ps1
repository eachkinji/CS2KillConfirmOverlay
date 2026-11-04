param([string]$Payload='Output/ClarityPanel-5.0.1.21')
$ErrorActionPreference='Stop'
$repository=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$Payload=[IO.Path]::GetFullPath((Join-Path $repository $Payload))
$name='KillConfirmGameBar.SwitchValidation'
if(Get-AppxPackage -Name $name -ErrorAction SilentlyContinue) {throw 'A game-switch validation package already exists.'}
$output=Join-Path $repository ('Output/WidgetSwitch-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
$profile=Join-Path $output 'profile'
New-Item -ItemType Directory -Path $profile -Force | Out-Null
$source=Join-Path $env:LOCALAPPDATA 'KillConfirmOverlay/UserData'
foreach($file in @('settings.json','pack-catalog.json')) {Copy-Item -LiteralPath (Join-Path $source $file) -Destination $profile}
& robocopy (Join-Path $source 'Packs/valorant') (Join-Path $profile 'Packs/valorant') /E /R:1 /W:1 /NFL /NDL /NJH /NJS /NP | Out-Null
if($LASTEXITCODE -ge 8) {throw 'Could not copy fixture packs.'}
$catalogPath=Join-Path $profile 'pack-catalog.json'
[IO.File]::WriteAllText($catalogPath,([IO.File]::ReadAllText($catalogPath).Replace($source.Replace('\','\\'),$profile.Replace('\','\\'))))
$settingsPath=Join-Path $profile 'settings.json'
$settings=Get-Content -LiteralPath $settingsPath -Raw | ConvertFrom-Json -AsHashtable
$settings['LocalService.Port']=@{Kind='int';Text='10095'}
$settings['GameStyleMode']=@{Kind='string';Text='crossfire'}
$settings['CompatibilityDisplay.Enabled']=@{Kind='bool';Text='False'}
[IO.File]::WriteAllText($settingsPath,($settings | ConvertTo-Json -Depth 10 -Compress))
New-Item -ItemType Directory -Path (Join-Path $profile 'CompatibilityDisplay') -Force | Out-Null
[IO.File]::WriteAllText((Join-Path $profile 'CompatibilityDisplay/display.json'),'{"Enabled":false,"GameBarBlocked":false}')
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
[ComImport,Guid("2e941141-7f97-4756-ba1d-9decde894a3d"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface ISwitchActivation { [PreserveSig] int ActivateApplication([MarshalAs(UnmanagedType.LPWStr)] string id,[MarshalAs(UnmanagedType.LPWStr)] string arguments,int options,out uint pid); }
public static class SwitchActivation {
 public static uint Activate(string id) {var type=Type.GetTypeFromCLSID(new Guid("45BA127D-10A8-46EA-8AB7-56EA9078943C"));var activation=(ISwitchActivation)Activator.CreateInstance(type);uint pid;Marshal.ThrowExceptionForHR(activation.ActivateApplication(id,"",0,out pid));return pid;}
}
[ComImport,Guid("F27C3930-8029-4AD1-94E3-3DBA417810C1"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface ISwitchDebugSettings {
 [PreserveSig] int EnableDebugging([MarshalAs(UnmanagedType.LPWStr)] string name,IntPtr debugger,IntPtr environment);
 [PreserveSig] int DisableDebugging([MarshalAs(UnmanagedType.LPWStr)] string name);
}
public static class SwitchLifetime {
 static ISwitchDebugSettings Settings() {return (ISwitchDebugSettings)Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("B1AEC16F-2383-4852-B0E9-8F0B1DC66B4D")));}
 public static void Enable(string name) {Marshal.ThrowExceptionForHR(Settings().EnableDebugging(name,IntPtr.Zero,IntPtr.Zero));}
 public static void Disable(string name) {Marshal.ThrowExceptionForHR(Settings().DisableDebugging(name));}
}
'@
$service=$null; $package=$null; $widgetProcessId=0
try {
    $testPackage=Get-ChildItem -LiteralPath (Join-Path $repository 'Output/WidgetSwitchValidation') -Recurse -File -Filter '*.msix' | Select-Object -First 1
    $dependencies=@(Get-ChildItem -LiteralPath (Join-Path $testPackage.DirectoryName 'Dependencies/x64') -File -Filter '*.appx' | ForEach-Object FullName)
    Add-AppxPackage -Path $testPackage.FullName -DependencyPath $dependencies
    $package=Get-AppxPackage -Name $name -ErrorAction Stop
    # Only the isolated test package: prevent Windows suspending the test when
    # another window takes focus. Restore normal lifetime handling in finally.
    [SwitchLifetime]::Enable($package.PackageFullName)
    $cache=Join-Path $env:LOCALAPPDATA "Packages/$($package.PackageFamilyName)/LocalState"
    $fixture=Join-Path $cache 'Fixture'
    New-Item -ItemType Directory -Path $fixture -Force | Out-Null
    & robocopy $profile (Join-Path $fixture 'Data') /E /R:1 /W:1 /NFL /NDL /NJH /NJS /NP | Out-Null
    & robocopy (Join-Path $Payload 'Assets') (Join-Path $fixture 'Payload/Assets') /E /R:1 /W:1 /NFL /NDL /NJH /NJS /NP | Out-Null
    & robocopy (Join-Path $Payload 'DefaultPacks') (Join-Path $fixture 'Payload/DefaultPacks') /E /R:1 /W:1 /NFL /NDL /NJH /NJS /NP | Out-Null
    if($LASTEXITCODE -ge 8) {throw 'Could not copy isolated resources.'}
    [IO.File]::WriteAllText((Join-Path $fixture 'settings.json'),[IO.File]::ReadAllText($settingsPath))
    [IO.File]::WriteAllText((Join-Path $fixture 'pack-catalog.json'),[IO.File]::ReadAllText($catalogPath).Replace($profile.Replace('\','\\'),'C:\\FixtureRemote\\UserData'))
    $widgetProcessId=[SwitchActivation]::Activate(($package.PackageFamilyName+'!App'))
    for($attempt=0;$attempt -lt 1200;$attempt++) {
        if((Test-Path -LiteralPath (Join-Path $cache 'switch-pass.txt')) -or (Test-Path -LiteralPath (Join-Path $cache 'switch-failure.txt'))) {break}
        if (!(Get-Process -Id $widgetProcessId -ErrorAction SilentlyContinue)) {break}
        Start-Sleep -Milliseconds 200
    }
    foreach($file in @('switch-pass.txt','switch-failure.txt','switch-progress.txt','gamebar-widget.log')) {
        if(Test-Path -LiteralPath (Join-Path $cache $file)) {
            [IO.File]::WriteAllText((Join-Path $output $file),[IO.File]::ReadAllText((Join-Path $cache $file)))
        }
    }
    foreach($file in Get-ChildItem -LiteralPath $cache -File -Filter 'battlefield6-*') {
        # AppContainer files can carry EFS metadata unsupported by the artifact drive.
        [IO.File]::WriteAllBytes((Join-Path $output $file.Name), [IO.File]::ReadAllBytes($file.FullName))
    }
    if(!(Test-Path -LiteralPath (Join-Path $output 'switch-pass.txt'))) {
        if(Test-Path -LiteralPath (Join-Path $output 'switch-failure.txt')) {throw (Get-Content -LiteralPath (Join-Path $output 'switch-failure.txt') -Raw)}
        throw 'UWP game-switch validation timed out.'
    }
    Get-Content -LiteralPath (Join-Path $output 'switch-pass.txt')
}
finally {
    if($widgetProcessId -gt 0) {Stop-Process -Id $widgetProcessId -ErrorAction SilentlyContinue}
    if($service -and !$service.HasExited) {$service.Kill()}
    if($package -and $package.Name -eq $name) {
        [SwitchLifetime]::Disable($package.PackageFullName)
        Remove-AppxPackage -Package $package.PackageFullName
    }
    Write-Output "Artifacts: $output"
}

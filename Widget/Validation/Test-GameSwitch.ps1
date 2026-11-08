param([string]$Payload='', [string]$CatalogProfile='', [switch]$AudioChannelsOnly, [switch]$CalabiyauRenderOnly, [switch]$NativePreloadOnly, [switch]$GameSelectorOnly, [string]$PackagePath='')
$ErrorActionPreference='Stop'
$repository=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
if (!$Payload) { $Payload = 'C:\Program Files\Kill Confirm Overlay' }
$Payload=[IO.Path]::GetFullPath($(if ([IO.Path]::IsPathRooted($Payload)) {$Payload} else {Join-Path $repository $Payload}))
$name='KillConfirmGameBar.SwitchValidation'
if(Get-AppxPackage -Name $name -ErrorAction SilentlyContinue) {throw 'A game-switch validation package already exists.'}
$output=Join-Path $repository ('Output/WidgetSwitch-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
$profile=Join-Path $output 'profile'
New-Item -ItemType Directory -Path $profile -Force | Out-Null
if (($AudioChannelsOnly -or $NativePreloadOnly -or $GameSelectorOnly) -and !$CatalogProfile) { throw 'This validation requires an isolated CatalogProfile.' }
$source = if ($CatalogProfile) { [IO.Path]::GetFullPath($CatalogProfile) } else { Join-Path $env:LOCALAPPDATA 'KillConfirmOverlay/UserData' }
foreach($file in @('settings.json','pack-catalog.json')) {Copy-Item -LiteralPath (Join-Path $source $file) -Destination $profile}
$catalogPath=Join-Path $profile 'pack-catalog.json'
$catalog=Get-Content -LiteralPath $catalogPath -Raw | ConvertFrom-Json -AsHashtable
# Copy only packs referenced by this explicit fixture. This also supports a
# resource library moved away from the historical UserData/Packs directory.
foreach ($kind in @('IconPacks','VoicePacks')) {
    foreach ($pack in $catalog[$kind]) {
        if (!$pack['FolderPath'] -or !(Test-Path -LiteralPath $pack['FolderPath'] -PathType Container)) { continue }
        $packDestination=Join-Path $profile ('Packs/fixture/' + $pack['Key'])
        & robocopy $pack['FolderPath'] $packDestination /E /R:1 /W:1 /NFL /NDL /NJH /NJS /NP | Out-Null
        if($LASTEXITCODE -ge 8) {throw 'Could not copy referenced fixture pack.'}
        $pack['FolderPath']=$packDestination
    }
}
if ($AudioChannelsOnly) {
    foreach ($kind in @('sfx','voice')) {
        $key='calabiyau_'+$kind+'_fixture'
        $folder=Join-Path $profile ('Packs/fixture/'+$key)
        New-Item -ItemType Directory -Path $folder -Force | Out-Null
        $item=@{Key=$key; DisplayName=$(if($kind -eq 'sfx'){'卡拉彼丘 · 测试音效'}else{'卡拉彼丘 · 测试角色'}); FolderPath=$folder; IsBuiltIn=$false; IsVisibleInWidget=$true; OwnsFolder=$true; GameStyle='calabiyau'; AudioKind=$(if($kind -eq 'sfx'){0}else{1}); AssociatedIconKeys=@('calabiyau')}
        $catalog['VoicePacks'] += $item
    }
}
[IO.File]::WriteAllText($catalogPath,($catalog | ConvertTo-Json -Depth 20 -Compress))
$settingsPath=Join-Path $profile 'settings.json'
$settings=Get-Content -LiteralPath $settingsPath -Raw | ConvertFrom-Json -AsHashtable
foreach ($key in @($settings.Keys | Where-Object { $_ -like 'AudioChannels.v2.*' })) { $settings.Remove($key) }
$settings['LocalService.Port']=@{Kind='int';Text='10095'}
$settings['GameStyleMode']=@{Kind='string';Text='crossfire'}
$settings['VoicePack.crossfire']=@{Kind='string';Text='crossfire_swat_gr'}
$settings['VoicePack']=@{Kind='string';Text='crossfire_swat_gr'}
$settings['CompatibilityDisplay.Enabled']=@{Kind='bool';Text='False'}
[IO.File]::WriteAllText($settingsPath,($settings | ConvertTo-Json -Depth 20 -Compress))
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
    $testPackage=if($PackagePath) { Get-Item -LiteralPath $PackagePath } else { Get-ChildItem -LiteralPath (Join-Path $repository 'Output/WidgetSwitchValidation') -Recurse -File -Filter '*.msix' | Sort-Object LastWriteTime -Descending | Select-Object -First 1 }
    if (!$testPackage) { throw 'Build the isolated WidgetSwitchValidation package first.' }
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive=[IO.Compression.ZipFile]::OpenRead($testPackage.FullName)
    try {
        $manifestReader=[IO.StreamReader]::new($archive.GetEntry('AppxManifest.xml').Open())
        try { [xml]$testManifest=$manifestReader.ReadToEnd() } finally { $manifestReader.Dispose() }
        if ($testManifest.Package.Identity.Name -ne $name) { throw 'Refusing to deploy a non-isolated package for validation; rebuild the validation package.' }
    } finally { $archive.Dispose() }
    $dependencies=@(Get-ChildItem -LiteralPath (Join-Path $testPackage.DirectoryName 'Dependencies/x64') -File -Filter '*.appx' | ForEach-Object FullName)
    # Use the frameworks already installed for the production widget. Reapplying
    # dependency packages can demand closing Game Bar and unrelated Windows apps.
    Add-AppxPackage -Path $testPackage.FullName
    $package=Get-AppxPackage -Name $name -ErrorAction Stop
    # Only the isolated test package: prevent Windows suspending the test when
    # another window takes focus. Restore normal lifetime handling in finally.
    [SwitchLifetime]::Enable($package.PackageFullName)
    $cache=Join-Path $env:LOCALAPPDATA "Packages/$($package.PackageFamilyName)/LocalState"
    $fixture=Join-Path $cache 'Fixture'
    New-Item -ItemType Directory -Path $fixture -Force | Out-Null
    if ($AudioChannelsOnly) { [IO.File]::WriteAllText((Join-Path $fixture 'audio-channels-only'),'1') }
    if ($CalabiyauRenderOnly) { [IO.File]::WriteAllText((Join-Path $fixture 'calabiyau-render-only'),'1') }
    if ($NativePreloadOnly) { [IO.File]::WriteAllText((Join-Path $fixture 'native-preload-only'),'1') }
    if ($GameSelectorOnly) { [IO.File]::WriteAllText((Join-Path $fixture 'game-selector-only'),'1') }
    & robocopy $profile (Join-Path $fixture 'Data') /E /R:1 /W:1 /NFL /NDL /NJH /NJS /NP | Out-Null
    if (!$GameSelectorOnly) {
    & robocopy (Join-Path $Payload 'Assets') (Join-Path $fixture 'Payload/Assets') /E /R:1 /W:1 /NFL /NDL /NJH /NJS /NP | Out-Null
    if (!$NativePreloadOnly) { & robocopy (Join-Path $Payload 'DefaultPacks') (Join-Path $fixture 'Payload/DefaultPacks') /E /R:1 /W:1 /NFL /NDL /NJH /NJS /NP | Out-Null }
    }
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
    foreach($file in Get-ChildItem -LiteralPath $cache -File | Where-Object {$_.Name -like 'battlefield6-*' -or $_.Name -like 'audio-menu-*' -or $_.Name -like 'calabiyau-*' -or $_.Name -like 'compact-game-selector-*'}) {
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

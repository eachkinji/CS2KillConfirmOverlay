param(
    [ValidateSet('Debug','Release')][string]$Configuration='Release',
    [ValidateSet('x64')][string]$Platform='x64',
    [string]$OutputDir='',
    [string]$MsBuildPath=''
)
$ErrorActionPreference='Stop'
$repository=Split-Path $PSScriptRoot -Parent
if(!$OutputDir) {$OutputDir=Join-Path $repository 'Output/Standalone'}
$OutputDir=[IO.Path]::GetFullPath($OutputDir)
$allowed=[IO.Path]::GetFullPath((Join-Path $repository 'Output')).TrimEnd('\')+'\'
if(!$OutputDir.StartsWith($allowed,[StringComparison]::OrdinalIgnoreCase)) {throw 'Desktop staging must stay inside repository Output.'}
if(Test-Path -LiteralPath $OutputDir) {Remove-Item -LiteralPath $OutputDir -Recurse -Force}
New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
if(!$MsBuildPath) {
    $vswhere=Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
    $visualStudio=& $vswhere -latest -products * -requires Microsoft.Component.MSBuild -property installationPath
    $MsBuildPath=Join-Path $visualStudio 'MSBuild/Current/Bin/MSBuild.exe'
}
& $MsBuildPath (Join-Path $PSScriptRoot 'KillConfirmGameBar.csproj') /restore /t:Publish "/p:Configuration=$Configuration" /p:Platform=x64 "/p:PublishDir=$OutputDir/" /p:PublishReadyToRun=false /nologo /verbosity:minimal
if($LASTEXITCODE -ne 0) {throw 'Original desktop control panel publish failed.'}
$compiled=Join-Path $PSScriptRoot "bin/x64/$Configuration/net8.0-windows10.0.19041.0/win-x64"
# Dynamically adapted Page items are compiled by XAML targets after the SDK
# calculates publish items. Include their actual compiled views explicitly.
foreach($view in Get-ChildItem -LiteralPath $compiled -Recurse -File -Filter '*.xbf') {
    $target=Join-Path $OutputDir ([IO.Path]::GetRelativePath($compiled,$view.FullName))
    New-Item -ItemType Directory -Path (Split-Path $target -Parent) -Force | Out-Null
    Copy-Item -LiteralPath $view.FullName -Destination $target -Force
}
Copy-Item -Path (Join-Path $compiled '*.pri') -Destination $OutputDir -Force
$renderer=Join-Path $repository 'Output/RendererPublish'
& dotnet publish (Join-Path $repository 'CompatibilityHost/CompatibilityHost.csproj') -c $Configuration -r win-x64 --self-contained true -o $renderer --nologo
if($LASTEXITCODE -ne 0) {throw 'Desktop display publish failed.'}
# Both apps use the same .NET, WinRT and Win2D runtime. Keep one physical copy.
foreach($file in Get-ChildItem -LiteralPath $renderer -Recurse -File) {
    $relative=[IO.Path]::GetRelativePath($renderer,$file.FullName)
    $target=Join-Path $OutputDir $relative
    if(Test-Path -LiteralPath $target) {
        if($relative -in @('resources.pri','app.manifest')) {continue}
        # WindowsDesktop supplies the complete implementations of these three
        # assemblies; the Core framework carries forwarding/stub assemblies.
        if($relative -in @('Microsoft.VisualBasic.dll','System.Drawing.dll','WindowsBase.dll')) {Copy-Item -LiteralPath $file.FullName -Destination $target -Force; continue}
        if((Get-FileHash -LiteralPath $target).Hash -ne (Get-FileHash -LiteralPath $file.FullName).Hash) {throw "Conflicting shared runtime file: $relative"}
    } else {
        New-Item -ItemType Directory -Path (Split-Path $target -Parent) -Force | Out-Null
        Copy-Item -LiteralPath $file.FullName -Destination $target
    }
}
foreach($resource in @('Assets','Danmaku')) {
    Copy-Item -LiteralPath (Join-Path $repository "Widget/$resource") -Destination (Join-Path $OutputDir $resource) -Recurse -Force
}
$service=Join-Path $OutputDir 'KillConfirmService'
New-Item -ItemType Directory -Path $service -Force | Out-Null
foreach($binary in @('cskillconfirm.exe','killconfirm-settings-launcher.exe')) {
    Copy-Item -LiteralPath (Join-Path $repository "KillConfirmService/target/release/$binary") -Destination $service -Force
}
foreach($folder in @('sounds','ffmpeg')) {
    Copy-Item -LiteralPath (Join-Path $repository "Widget/KillConfirmService/$folder") -Destination (Join-Path $service $folder) -Recurse -Force
}
# App-local VC runtime: do not install a VC MSIX or require a machine-wide installer.
$vswhere=Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
$visualStudio=& $vswhere -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
$vc=Get-ChildItem -LiteralPath (Join-Path $visualStudio 'VC/Redist/MSVC') -Directory | Where-Object Name -Match '^\d+\.' | Sort-Object Name -Descending | Select-Object -First 1
$crt=Get-ChildItem -LiteralPath (Join-Path $vc.FullName 'x64') -Directory -Filter 'Microsoft.VC*.CRT' | Select-Object -First 1 -ExpandProperty FullName
if(!(Test-Path -LiteralPath $crt)) {throw 'App-local Visual C++ runtime not found.'}
Copy-Item -Path (Join-Path $crt '*.dll') -Destination $OutputDir -Force
Write-Host "Original control panel + shared service/display/resources: $OutputDir"

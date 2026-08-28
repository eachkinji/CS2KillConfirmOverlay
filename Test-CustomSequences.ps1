#Requires -Version 7.0
param([string]$OutputDirectory = '')
$ErrorActionPreference = 'Stop'
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $PSScriptRoot 'Output/CustomSequenceTests' }
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$framework = 'C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8'
if (-not (Test-Path (Join-Path $framework 'mscorlib.dll'))) {
    $framework = 'C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8.1'
}
if (-not (Test-Path (Join-Path $framework 'mscorlib.dll'))) { throw '.NET Framework 4.8 or 4.8.1 reference assemblies are required.' }
$sdk = 'C:\Program Files (x86)\Windows Kits\10\UnionMetadata\10.0.26100.0\Windows.winmd'
$vswhere = 'C:\Program Files (x86)\Microsoft Visual Studio\Installer\vswhere.exe'
$vs = & $vswhere -latest -products * -property installationPath
$compiler = Join-Path $vs 'MSBuild/Current/Bin/Roslyn/csc.exe'
$service = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'Widget/Services/CustomModule/CustomSequencePackService.cs'))
$service = "using ApplicationData = KillConfirmGameBar.Services.TestApplicationData;`n" + $service
$servicePath = Join-Path $OutputDirectory 'CustomSequencePackService.cs'
[IO.File]::WriteAllText($servicePath, $service)
$extraSources = @('Widget/Services/Styling/GameStyleService.cs', 'Widget/Services/Styling/GameStyleService.PackMappings.cs', 'Widget/Services/Styling/KillFeedbackFrameDefinition.cs', 'Widget/Services/Settings/General/KillFeedbackVisibilitySettingsStore.cs', 'Widget/Services/CustomModule/CustomModuleSettingsStore.cs', 'Widget/Services/CustomModule/CustomSequencePackService.Editing.cs', 'Widget/Services/Catalog/PackCatalogService.CustomModule.cs') | ForEach-Object {
    $copy = Join-Path $OutputDirectory ([IO.Path]::GetFileName($_))
    [IO.File]::WriteAllText($copy, "using ApplicationData = KillConfirmGameBar.Services.TestApplicationData;`n" + [IO.File]::ReadAllText((Join-Path $PSScriptRoot $_)))
    $copy
}
$references = @('mscorlib.dll','System.dll','System.Core.dll','System.Runtime.Serialization.dll','System.Xml.dll','System.IO.Compression.dll','System.IO.Compression.FileSystem.dll','Facades/System.Runtime.dll','Facades/System.Runtime.InteropServices.WindowsRuntime.dll') | ForEach-Object { '/r:' + (Join-Path $framework $_) }
$references += '/r:C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Runtime.WindowsRuntime.dll'
$exe = Join-Path $OutputDirectory 'CustomSequenceTests.exe'
& $compiler /nologo /target:exe /langversion:latest /nostdlib+ "/out:$exe" "/r:$sdk" @references (Join-Path $PSScriptRoot 'Widget/Services/CustomModule/CustomSequenceFormat.cs') $servicePath @extraSources (Join-Path $PSScriptRoot 'Tests/CustomSequences/Harness.cs')
if ($LASTEXITCODE -ne 0) { throw 'Custom-sequence harness compilation failed.' }
$fixtureRoot = Join-Path $OutputDirectory ([guid]::NewGuid().ToString('N'))
& $exe $fixtureRoot
if ($LASTEXITCODE -ne 0) { throw 'Custom-sequence regression checks failed.' }

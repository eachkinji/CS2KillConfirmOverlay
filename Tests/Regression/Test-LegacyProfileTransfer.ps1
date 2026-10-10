#Requires -Version 7.0
$ErrorActionPreference='Stop'
$repository=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$fixture=Join-Path $repository ('Output/LegacyTransfer-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixture | Out-Null
$project=@'
<Project Sdk="Microsoft.NET.Sdk">
 <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net8.0</TargetFramework><EnableDefaultCompileItems>false</EnableDefaultCompileItems></PropertyGroup>
 <ItemGroup>
  <Compile Include="../../../Tests/Regression/LegacyProfileTransferHarness.cs" />
  <Compile Include="../../../Common/LegacyProfileTransfer.cs" />
  <Compile Include="../../../Common/FileSettings.cs" />
  <Compile Include="../../../CompatibilityHost/Contracts/DisplayFiles.cs" />
  <Compile Include="../../../CompatibilityHost/Contracts/DisplayConfiguration.cs" />
 </ItemGroup>
</Project>
'@
$project=$project.Replace('../../../','../../')
$projectPath=Join-Path $fixture 'Transfer.csproj'
Set-Content -LiteralPath $projectPath -Value $project -Encoding UTF8
& dotnet run --project $projectPath -- (Join-Path $fixture 'Data')
if($LASTEXITCODE){throw 'Legacy transfer regression failed'}

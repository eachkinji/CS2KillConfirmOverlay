#Requires -Version 7.0
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$config=[IO.File]::ReadAllText((Join-Path $root 'CompatibilityHost/Contracts/DisplayConfiguration.cs'))
$files=[IO.File]::ReadAllText((Join-Path $root 'CompatibilityHost/Contracts/DisplayFiles.cs'))
$runtime=[IO.File]::ReadAllText((Join-Path $root 'CompatibilityHost/Integration/CompatibilityDisplayRuntime.cs'))
$guard=[regex]::Match($runtime,'public static bool IsEnabled =>[^;]+;').Value
$load=[regex]::Match($runtime,'(?ms)        public static DisplayConfiguration Load\(\).*?^        \}').Value
if(!$guard -or !$load){throw 'Mode guard methods missing'}
$harness=@'
using System;
using System.IO;
using System.Collections.Generic;
using KillConfirmCompatibility.Contracts;
using Windows.Storage;
namespace Windows.Storage {
 public class ApplicationData {
  public static ApplicationData Current = new ApplicationData();
  public LocalSettings LocalSettings = new LocalSettings();
 }
 public class LocalSettings { public Dictionary<string,object> Values = new Dictionary<string,object>(); }
}
public static class DisplayModeRegression {
 const string EnabledKey="CompatibilityDisplay.Enabled";
 public static string ConfigurationPath;
'@
$checks=@'
 public static void Run(string folder) {
  ConfigurationPath=Path.Combine(folder,"display.json");
  ApplicationData.Current.LocalSettings.Values[EnabledKey]=false;
  DisplayFiles.Write(ConfigurationPath,new DisplayConfiguration { Enabled=true });
  if(!IsEnabled) throw new Exception("Stale false widget setting allowed Game Bar during desktop display");
  DisplayFiles.Update(ConfigurationPath,c=>{c.Enabled=false;c.GameBarBlocked=true;});
  if(!IsEnabled) throw new Exception("Game Bar resumed before renderer shutdown acknowledgement");
  ApplicationData.Current.LocalSettings.Values[EnabledKey]=true;
  DisplayFiles.Update(ConfigurationPath,c=>c.GameBarBlocked=false);
  if(IsEnabled) throw new Exception("Stale true widget setting prevented Game Bar after renderer stopped");
  DisplayFiles.Update(ConfigurationPath,c=>c.Enabled=true);
  if(!IsEnabled) throw new Exception("Repeated desktop switch was not observed");
 }
}
'@
# Keep each source file in a separate compilation unit so its using directives
# remain valid. The real guard, loader and serialized contract are exercised.
$fixture=Join-Path $root ('Output/ModeGuardRegression-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixture | Out-Null
$paths=@('DisplayConfiguration.cs','DisplayFiles.cs','Checks.cs') | ForEach-Object {Join-Path $fixture $_}
[IO.File]::WriteAllText($paths[0],$config)
[IO.File]::WriteAllText($paths[1],$files)
[IO.File]::WriteAllText($paths[2],$harness+"`n"+$guard+"`n"+$load+"`n"+$checks)
Add-Type -Path $paths
[DisplayModeRegression]::Run($fixture)
Write-Output 'PASS: shared display configuration overrides stale widget settings, blocks during renderer shutdown, and restores Game Bar after acknowledgement.'

#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$source = [IO.File]::ReadAllText((Join-Path $root 'Widget/Pages/Main/Packs/Creation/MainPage.PackCreation.Import.cs'))
$queue = $source.Substring($source.IndexOf('    internal sealed class PackImportQueueResult'))
$header = @'
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
namespace KillConfirmGameBar {
internal static class App { public static void Log(string message) {} }
'@
$harness = @'
public static class PackQueueRegression {
 public static void Run() { CheckAsync().GetAwaiter().GetResult(); }
 static async Task CheckAsync() {
  var visits = new List<string>();
  int active = 0;
  var result = await KillConfirmGameBar.PackImportQueue.RunAsync(
   new[] { "first.zip", "broken.zip", "last.zip" }, x => x, async (file, index) => {
    if (++active != 1) throw new Exception("Imports overlapped");
    try {
     await Task.Yield();
     visits.Add(file);
     if (index == 1) throw new System.IO.InvalidDataException("Invalid manifest");
    } finally { active--; }
   });
  if (result.Succeeded != 2 || result.Failures.Count != 1
      || !result.Failures[0].Contains("broken.zip: Invalid manifest")
      || !visits.SequenceEqual(new[] { "first.zip", "broken.zip", "last.zip" }) || active != 0)
   throw new Exception("Mixed batch did not finish in order with isolated failure");
  var empty = await KillConfirmGameBar.PackImportQueue.RunAsync(
   new string[0], x => x, (file, index) => throw new Exception("Empty batch invoked importer"));
  if (empty.Succeeded != 0 || empty.Failures.Count != 0) throw new Exception("Empty selection failed");
  var retry = await KillConfirmGameBar.PackImportQueue.RunAsync(
   new[] { "fixed.zip" }, x => x, (file, index) => Task.CompletedTask);
  if (retry.Succeeded != 1 || retry.Failures.Count != 0) throw new Exception("Next batch retained previous errors");
 }
}
'@
Add-Type -TypeDefinition ($header + "`n" + $queue + "`n" + $harness)
[PackQueueRegression]::Run()
Write-Output 'PASS: sequential imports, asynchronous failure isolation, later-file continuation, empty selection, and fresh next batch.'

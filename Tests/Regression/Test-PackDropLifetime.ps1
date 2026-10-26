#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$source = Get-Content -Raw (Join-Path $root 'Widget/Pages/Main/Packs/Creation/MainPage.PackCreation.Import.cs')
$start = $source.IndexOf('        private async Task ImportDroppedPackZipsAsync(')
$method = $source.Substring($start, $source.IndexOf('        private async void OnCreateIconPackClick', $start) - $start)
$shim = @'
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
enum DataPackageOperation { None, Copy }
enum UiLanguage { SimplifiedChinese }
static class LocalizationManager {
 public static UiLanguage Current = UiLanguage.SimplifiedChinese;
 public static string Text(string key) => key;
}
static class StandardDataFormats { public const string StorageItems = "files"; }
class StorageFile { public string FileType; }
class Deferral {
 public int Completed;
 public void Complete() { if (++Completed != 1) throw new Exception("Drop completed twice"); }
}
class DataView {
 public bool Supported = true, Fail;
 public List<object> Items = new List<object>();
 public bool Contains(string format) => Supported;
 public async Task<List<object>> GetStorageItemsAsync() {
  await Task.Yield();
  if (Fail) throw new IOException("Data retrieval failed");
  return Items;
 }
}
class DragEventArgs {
 public bool Handled;
 public DataPackageOperation AcceptedOperation;
 public DataView DataView = new DataView();
 public Deferral Deferral = new Deferral();
 public Deferral GetDeferral() => Deferral;
}
public class DropRegression {
 bool _packZipDropInProgress;
 DragEventArgs current;
 int imports, errors;
 TaskCompletionSource<bool> gate;
 void SetPackImportBusy(bool busy) => _packZipDropInProgress = busy;
 async Task ImportSelectedPackFilesAsync(IReadOnlyList<StorageFile> files, bool voice, bool batch) {
  Check(current.Deferral.Completed == 1, "Native drag still active during import");
  Check(_packZipDropInProgress, "Busy guard released too early");
  Check(batch == (files.Count > 1), "Incorrect batch mode");
  imports++;
  if (gate != null) await gate.Task;
 }
 Task ShowMessageAsync(string title, string message) {
  Check(current.Deferral.Completed == 1, "Dialog opened with active native drag");
  errors++;
  return Task.CompletedTask;
 }
 static void Check(bool value, string error) { if (!value) throw new Exception(error); }
 public static void Run() => new DropRegression().Verify().GetAwaiter().GetResult();
 async Task Verify() {
  current = new DragEventArgs();
  current.DataView.Items.Add(new StorageFile { FileType = ".ZIP" });
  current.DataView.Items.Add(new StorageFile { FileType = ".zip" });
  gate = new TaskCompletionSource<bool>();
  Task pending = ImportDroppedPackZipsAsync(current, true);
  while (imports == 0 && !pending.IsCompleted) await Task.Yield();
  Check(imports == 1 && !pending.IsCompleted, "Import did not pause at slow work");
  var duplicate = new DragEventArgs();
  await ImportDroppedPackZipsAsync(duplicate, false);
  Check(duplicate.AcceptedOperation == DataPackageOperation.None && duplicate.Deferral.Completed == 0, "Overlapping drop accepted");
  gate.SetResult(true);
  await pending;
  Check(!_packZipDropInProgress && current.Deferral.Completed == 1, "Busy state not restored");
  gate = null;
  current = new DragEventArgs();
  current.DataView.Items.Add(new StorageFile { FileType = ".png" });
  await ImportDroppedPackZipsAsync(current, false);
  Check(errors == 1 && !_packZipDropInProgress, "Invalid drop leaked state");
  current = new DragEventArgs();
  current.DataView.Fail = true;
  await ImportDroppedPackZipsAsync(current, true);
  Check(errors == 2 && !_packZipDropInProgress, "Data failure leaked deferral");
  current = new DragEventArgs();
  current.DataView.Items.Add(new StorageFile { FileType = ".zip" });
  await ImportDroppedPackZipsAsync(current, false);
  Check(imports == 2 && !_packZipDropInProgress, "Retry failed");
 }
'@
Add-Type -TypeDefinition ($shim + "`n" + $method + "`n}")
[DropRegression]::Run()
Write-Output 'PASS: native drop completes before slow import and dialogs; busy rejection, invalid files, data failure, retry, and batch mode.'

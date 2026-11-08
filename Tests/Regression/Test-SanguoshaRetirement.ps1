#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
. (Join-Path $PSScriptRoot '..\Common\SourceView.ps1')
$fixture = Join-Path $root ('Output/SanguoshaRetirement-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixture | Out-Null
$style = [IO.File]::ReadAllText((Resolve-SourcePath $root 'Widget/Services/Styling/GameStyleService.Sanguosha.cs'))
$persistence = [IO.File]::ReadAllText((Resolve-SourcePath $root 'Widget/Pages/KillConfirmWidget/Packs/KillConfirmWidgetPage.PackSettings.Persistence.cs'))
$desktopPersistence = [IO.File]::ReadAllText((Join-Path $root 'CompatibilityHost/Integration/Home/Packs/CompatibilityHomeView.PackSettings.Persistence.cs'))
$harness = @'
#pragma warning disable CS0649
using System;
using System.IO;
using System.Collections.Generic;
using KillConfirmGameBar.Services;
namespace Windows.Storage {
 public sealed class SettingsValues : Dictionary<string, object> {
  public new object this[string key] { get => TryGetValue(key, out var value) ? value : null; set => base[key] = value; }
 }
 public sealed class ApplicationDataContainer { public SettingsValues Values = new SettingsValues(); }
 public sealed class StorageFolder { public string Path; }
 public sealed class ApplicationData {
  public static ApplicationData Current = new ApplicationData();
  public ApplicationDataContainer LocalSettings = new ApplicationDataContainer();
  public StorageFolder LocalFolder = new StorageFolder();
 }
}
namespace KillConfirmGameBar.Services {
 internal enum GameStyleMode { Sanguosha, Other }
 internal static partial class GameStyleService {
  public static string ToStorageValue(GameStyleMode style) => style == GameStyleMode.Sanguosha ? "sanguosha" : "other";
  public static GameStyleMode GetStyleForPackKey(string key) => IsSanguoshaKey(key) ? GameStyleMode.Sanguosha : GameStyleMode.Other;
 }
 internal sealed class KillEvent {
  public bool IsCombatEvent, IsAssist, IsKnifeKill, IsGrenadeKill, IsHeadshot;
  public int KillCount; public string EventKind;
 }
 internal sealed class SanguoshaGameplaySettingsValues {
  public bool KnifeSpecialIconPriority, GrenadeSpecialIconPriority, HeadshotSpecialIconPriority;
 }
 internal static class SanguoshaGameplaySettingsStore {
  public static SanguoshaGameplaySettingsValues Load() => new SanguoshaGameplaySettingsValues();
 }
}
namespace KillConfirmGameBar { public static class App { public static void Log(string value) { } } }
'@
$checks = @'
        private const string VoicePackSettingKey = "VoicePack";
        public static void ValidateRetirement(string directory)
        {
            Windows.Storage.ApplicationData.Current.LocalFolder.Path = directory;
            var values = Windows.Storage.ApplicationData.Current.LocalSettings.Values;
            foreach (string key in new[] { "VoicePack", "KillIconPack" })
            {
                string backup = GetPackSelectionFilePath(key, GameStyleMode.Sanguosha);
                foreach (string source in new[] { "scoped", "backup", "legacy" })
                {
                    values.Clear();
                    if (File.Exists(backup)) File.Delete(backup);
                    if (source == "backup") File.WriteAllText(backup, " SANGUOSHA_GUANJIE ");
                    else values[source == "scoped" ? key + ".sanguosha" : key] = " SANGUOSHA_GUANJIE ";
                    string result = LoadPackSettingForStyle(key, GameStyleMode.Sanguosha, "fallback");
                    if (result != "sanguosha" || (string)values[key + ".sanguosha"] != "sanguosha")
                        throw new Exception(key + "/" + source + " did not migrate to anniversary pack");
                }
                values[key + ".sanguosha"] = "sanguosha_custom";
                File.WriteAllText(backup, "sanguosha_guanjie");
                if (LoadPackSettingForStyle(key, GameStyleMode.Sanguosha, "fallback") != "sanguosha_custom")
                    throw new Exception("Retired backup overwrote a newer selection");
                SavePackSettingForStyle(key, GameStyleMode.Sanguosha, "sanguosha_guanjie");
                if (File.ReadAllText(backup) != "sanguosha") throw new Exception("Save recreated retired selection");
            }
        }
'@
# Exercise both production persistence implementations, including real recovery files.
$paths = @('Style.cs', 'WidgetPersistence.cs', 'DesktopPersistence.cs', 'Harness.cs') | ForEach-Object { Join-Path $fixture $_ }
[IO.File]::WriteAllText($paths[0], $style)
$position = $persistence.LastIndexOf("    }")
[IO.File]::WriteAllText($paths[1], $persistence.Insert($position, $checks))
$position = $desktopPersistence.LastIndexOf("    }")
[IO.File]::WriteAllText($paths[2], $desktopPersistence.Insert($position, $checks))
[IO.File]::WriteAllText($paths[3], $harness)
Add-Type -Path $paths -IgnoreWarnings
[KillConfirmGameBar.KillConfirmWidgetPage]::ValidateRetirement($fixture)
[KillConfirmGameBar.Features.CompatibilityDisplay.CompatibilityHomeView]::ValidateRetirement($fixture)
$retired = @('podi','guanjie2','guanjie3','guanjie4','guanjie5','guanjie6','guanjie7','dashatesha','shoujidonghua_chuizi','shoujidonghua_jian','shoujidonghua_fuzi2')
foreach ($base in @('Widget/Assets/GameStyles/sanguosha','SourceAssets/GameStyles/sanguosha')) {
    foreach ($clip in $retired) {
        if (Test-Path -LiteralPath (Join-Path $root "$base/killconfirm/$clip")) { throw "Retired shipped clip: $base/$clip" }
    }
}
if (Test-Path -LiteralPath (Join-Path $root 'SourceAssets/GameStyles/sanguosha/soundpacks/sanguosha_guanjie')) { throw 'Retired sound pack remains in build inputs.' }
Write-Output 'PASS: widget and desktop scoped/backup/legacy selections migrate; current selections stay authoritative; retired asset inputs are absent.'

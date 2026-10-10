using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using KillConfirmCompatibility.Contracts;

internal static class Program
{
    static void Main(string[] args)
    {
        string fixture = args[0];
        string old = Path.Combine(fixture, "old", "LocalState");
        string install = Path.Combine(fixture, "old", "WindowsApps");
        string current = Path.Combine(fixture, "new", "UserData");
        string backup = Path.Combine(fixture, "new", "Backup");
        string external = Path.Combine(fixture, "external");
        foreach (string folder in new[] { "Packs/valorant/icon_packs/imported", "Packs/valorant/voice_packs/imported", "BombAudio", "CustomPermanentData" })
        {
            Directory.CreateDirectory(Path.Combine(old, folder));
            File.WriteAllBytes(Path.Combine(old, folder, "asset.bin"), new byte[] { 1, 2, 3 });
        }
        Directory.CreateDirectory(Path.Combine(install, "Assets", "LegacyVoice"));
        File.WriteAllBytes(Path.Combine(install, "Assets", "LegacyVoice", "voice.wav"), new byte[] { 8, 9 });
        Directory.CreateDirectory(external);
        File.WriteAllBytes(Path.Combine(external, "image.png"), new byte[] { 4, 5 });
        JsonObject Pack(string key, string path) => new() { ["Key"] = key, ["FolderPath"] = path, ["DisplayName"] = key, ["IsBuiltIn"] = false, ["IsVisibleInWidget"] = true };
        var oldCatalog = new JsonObject {
            ["IconPacks"] = new JsonArray(Pack("imported_icon", Path.Combine(old, "Packs/valorant/icon_packs/imported")), Pack("external_icon", external)),
            ["VoicePacks"] = new JsonArray(Pack("imported_voice", Path.Combine(old, "Packs/valorant/voice_packs/imported")), Pack("package_voice", Path.Combine(install, "Assets/LegacyVoice"))) };
        File.WriteAllText(Path.Combine(old, "pack-catalog.json"), oldCatalog.ToJsonString());
        File.WriteAllText(Path.Combine(old, "CustomPermanentData", "paths.json"), new JsonObject { ["Audio"] = Path.Combine(old, "BombAudio/asset.bin") }.ToJsonString());
        Directory.CreateDirectory(current);
        File.WriteAllText(Path.Combine(current, "pack-catalog.json"), new JsonObject { ["IconPacks"] = new JsonArray(Pack("new_icon", external)), ["VoicePacks"] = new JsonArray() }.ToJsonString());
        var settings = new FileSettings(Path.Combine(current, "settings.json"));
        settings["Volume"] = 42;
        var legacySettings = new Dictionary<string, object> {
            ["Volume"] = 12, ["AudioEnabled"] = true, ["Double"] = 0.75, ["Long"] = 1234567890123L,
            ["IconPack"] = "imported_icon", ["VoicePack"] = "imported_voice",
            ["Nested"] = new JsonObject { ["Data"] = new JsonArray(new JsonObject { ["Path"] = Path.Combine(old, "BombAudio/asset.bin") }) }.ToJsonString(),
            ["Uri"] = "ms-appdata:///local/BombAudio/asset.bin",
            ["Slashes"] = Path.Combine(old, "BombAudio/asset.bin").Replace('\\', '/'),
            ["FileUri"] = new Uri(Path.GetFullPath(Path.Combine(old, "BombAudio/asset.bin"))).AbsoluteUri,
            ["GameBarRuntimeStatus"] = new object() };
        LegacyProfileTransfer.Prepare(current, old, install, backup, legacySettings);
        settings = new FileSettings(Path.Combine(current, "settings.json"));
        Check(Equals(settings["Volume"], 42) && Equals(settings["AudioEnabled"], true) && Equals(settings["Double"], 0.75) && Equals(settings["Long"], 1234567890123L), "typed settings/current choices were lost");
        Check((string)settings["IconPack"] == "imported_icon" && (string)settings["VoicePack"] == "imported_voice", "selected pack keys changed");
        Check(JsonNode.Parse((string)settings["Nested"])["Data"][0]["Path"].GetValue<string>() == Path.GetFullPath(Path.Combine(current, "BombAudio/asset.bin")), "nested setting path was not remapped");
        Check(Path.GetFullPath((string)settings["Uri"]) == Path.GetFullPath(Path.Combine(current, "BombAudio/asset.bin")), "local URI was not remapped");
        Check((string)settings["Slashes"] == Path.GetFullPath(Path.Combine(current, "BombAudio/asset.bin")) && (string)settings["FileUri"] == (string)settings["Slashes"], "slash/file URI variants were not remapped");
        var merged = JsonNode.Parse(File.ReadAllText(Path.Combine(current, "pack-catalog.json")));
        Check(merged["IconPacks"].AsArray().Count == 3 && merged["VoicePacks"].AsArray().Count == 2, "catalog lists were not merged");
        Check(merged["IconPacks"][2]["FolderPath"].GetValue<string>() == external, "external pack was relocated unnecessarily");
        string preservedAsset = merged["VoicePacks"][1]["FolderPath"].GetValue<string>();
        Check(File.Exists(Path.Combine(preservedAsset, "voice.wav")), "asset referenced inside WindowsApps was lost");
        Check(File.Exists(Path.Combine(current, "CustomPermanentData/asset.bin")) && JsonNode.Parse(File.ReadAllText(Path.Combine(current, "CustomPermanentData/paths.json")))["Audio"].GetValue<string>() == Path.GetFullPath(Path.Combine(current, "BombAudio/asset.bin")), "non-whitelisted data or manifest paths were lost");
        Check(File.Exists(Path.Combine(backup, "verified.json")) && File.ReadAllBytes(Path.Combine(backup, "LocalState/BombAudio/asset.bin")).SequenceEqual(new byte[] { 1, 2, 3 }), "verified external backup missing");
        // Simulate removal of the source package. Every migrated pack remains
        // accessible, and a subsequent preparation never duplicates list rows.
        Directory.Move(Path.Combine(fixture, "old"), Path.Combine(fixture, "retired-old"));
        foreach (string group in new[] { "IconPacks", "VoicePacks" })
            foreach (var item in merged[group].AsArray()) Check(Directory.Exists(item["FolderPath"].GetValue<string>()), "pack lost after old package removal");
        LegacyProfileTransfer.Prepare(current, Path.Combine(fixture, "retired-old/LocalState"), "", Path.Combine(fixture, "new/RetryBackup"), new Dictionary<string, object>());
        Check(JsonNode.Parse(File.ReadAllText(Path.Combine(current, "pack-catalog.json")))["IconPacks"].AsArray().Count == 3, "retry duplicated packs");
        string bad = Path.Combine(fixture, "bad");
        Directory.CreateDirectory(bad);
        File.WriteAllText(Path.Combine(bad, "pack-catalog.json"), new JsonObject { ["IconPacks"] = new JsonArray(Pack("missing", Path.Combine(bad, "missing"))) }.ToJsonString());
        bool rejected = false;
        try { LegacyProfileTransfer.Prepare(Path.Combine(fixture, "bad-new/UserData"), bad, "", Path.Combine(fixture, "bad-new/Backup"), new Dictionary<string, object>()); }
        catch (IOException) { rejected = true; }
        Check(rejected && !File.Exists(Path.Combine(fixture, "bad-new/migration-v49-complete.txt")), "missing pack was incorrectly accepted for package removal");
        Console.WriteLine("PASS: typed/current settings, both selected pack keys, merged catalogs, nested/URI paths, arbitrary imported data, external/package assets, verified backup, removal survival, repeat migration and missing-pack rejection.");
    }
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
}

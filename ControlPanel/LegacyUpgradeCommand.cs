using System.Text.Json.Nodes;
using KillConfirmCompatibility.Contracts;

namespace KillConfirmGameBar;

internal static class LegacyUpgradeCommand
{
    internal static async Task<bool> RunAsync(string[] arguments)
    {
        int index = Array.IndexOf(arguments, "--prepare-legacy-upgrade");
        if (index < 0) return false;
        if (index + 2 >= arguments.Length) throw new ArgumentException("Missing legacy upgrade request/result paths.");
        string resultPath = arguments[index + 2];
        try
        {
            var request = JsonNode.Parse(File.ReadAllText(arguments[index + 1])).AsObject();
            string packageName = request["PackageFullName"].GetValue<string>();
            if (!packageName.StartsWith("KillConfirmGameBar.Overlay_", StringComparison.Ordinal) ||
                !packageName.EndsWith("_5jgcw66eyez0m", StringComparison.Ordinal))
                throw new IOException("Unexpected legacy package identity.");
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string old = Path.Combine(local, "Packages", RuntimePaths.PackageFamily, "LocalState");
            string backup = Path.Combine(Path.GetDirectoryName(RuntimePaths.DataRoot), "LegacyBackups", packageName);
            var legacy = Windows.Management.Core.ApplicationDataManager.CreateForPackageFamily(RuntimePaths.PackageFamily);
            // An inaccessible settings store is an upgrade failure, never a
            // successful empty import followed by destruction of the old package.
            var values = legacy.LocalSettings.Values.ToDictionary(pair => pair.Key, pair => pair.Value);
            LegacyProfileTransfer.Prepare(RuntimePaths.DataRoot, old,
                request["InstallLocation"].GetValue<string>(), backup, values);
            // Materialize the same catalog and built-in registrations used by
            // the actual UI before reporting that the new profile is ready.
            BundledPacks.Register(new FileSettings(Path.Combine(RuntimePaths.DataRoot, FileSettings.FileName)));
            var expected = JsonNode.Parse(File.ReadAllText(Path.Combine(RuntimePaths.DataRoot, "pack-catalog.json")));
            var icons = await Services.PackCatalogService.GetAllIconPacksAsync();
            var voices = await Services.PackCatalogService.GetAllVoicePacksAsync();
            foreach (string group in new[] { "IconPacks", "VoicePacks" })
                if (expected[group] is JsonArray items)
                    foreach (var item in items)
                    {
                        if (item?["IsBuiltIn"]?.GetValue<bool>() == true) continue;
                        string key = item["Key"].GetValue<string>();
                        bool found = group == "IconPacks" ? icons.Any(pack => pack.Key == key) : voices.Any(pack => pack.Key == key);
                        if (!found) throw new IOException("Migrated pack could not be loaded by the control panel: " + key);
                    }
            File.WriteAllText(resultPath, new JsonObject { ["Success"] = true, ["BackupPath"] = backup,
                ["DataRoot"] = RuntimePaths.DataRoot }.ToJsonString());
        }
        catch (Exception error)
        {
            File.WriteAllText(resultPath, new JsonObject { ["Success"] = false, ["Message"] = error.Message }.ToJsonString());
        }
        return true;
    }
}

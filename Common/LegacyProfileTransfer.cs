using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json.Nodes;

namespace KillConfirmCompatibility.Contracts
{
    // Pure file migration, shared by installer preparation and regression fixtures.
    // A verified snapshot outside the package is required before package removal.
    public static class LegacyProfileTransfer
    {
        public static string Prepare(string root, string old, string oldInstall, string backup,
            IDictionary<string, object> legacySettings)
        {
            root = Path.GetFullPath(root);
            old = Path.GetFullPath(old);
            backup = Path.GetFullPath(backup);
            oldInstall = string.IsNullOrEmpty(oldInstall) ? "" : Path.GetFullPath(oldInstall).TrimEnd(Path.DirectorySeparatorChar);
            if (Within(backup, old) || Within(root, old) || Within(old, root) || Within(backup, root))
                throw new IOException("Migration source, destination and backup must have separate directory trees.");
            Directory.CreateDirectory(backup);
            var scalars = new JsonObject();
            foreach (var pair in legacySettings)
            {
                // This composite is a live widget heartbeat, not a user setting.
                if (pair.Key == "GameBarRuntimeStatus") continue;
                var scalar = SettingScalar.From(pair.Value);
                scalars[pair.Key] = new JsonObject { ["Kind"] = scalar.Kind, ["Text"] = scalar.Text };
            }
            WriteJson(Path.Combine(backup, "exported-settings.json"), scalars);
            CopyTree(old, Path.Combine(backup, "LocalState"), overwrite: true);

            Directory.CreateDirectory(root);
            string currentSettings = Path.Combine(root, FileSettings.FileName);
            if (File.Exists(currentSettings))
            {
                var current = JsonNode.Parse(File.ReadAllText(currentSettings)).AsObject();
                foreach (var pair in current)
                    _ = new SettingScalar { Kind = pair.Value["Kind"].GetValue<string>(), Text = pair.Value["Text"].GetValue<string>() }.Value;
            }
            var settings = new FileSettings(Path.Combine(root, FileSettings.FileName));
            string Rewrite(string value) => RewriteText(value, old, root, oldInstall, Path.Combine(root, "LegacyPackageAssets"), backup);
            foreach (var pair in legacySettings)
            {
                if (pair.Key == "GameBarRuntimeStatus") continue;
                if (!settings.ContainsKey(pair.Key)) settings[pair.Key] = pair.Value is string text ? Rewrite(text) : pair.Value;
            }
            // Repair paths from a previous incomplete migration without replacing
            // any of the user's more recent scalar choices.
            foreach (var pair in settings.ToArray())
                if (pair.Value is string text) settings[pair.Key] = Rewrite(text);

            foreach (string file in Files(old))
            {
                string relative = Path.GetRelativePath(old, file);
                string first = relative.Split(Path.DirectorySeparatorChar)[0];
                if (first.Equals("Standalone", StringComparison.OrdinalIgnoreCase) ||
                    first.Equals("Logs", StringComparison.OrdinalIgnoreCase) ||
                    relative.EndsWith(".log", StringComparison.OrdinalIgnoreCase) ||
                    relative.EndsWith(".lock", StringComparison.OrdinalIgnoreCase) ||
                    relative == "pack-catalog.json" || relative == FileSettings.FileName) continue;
                string target = Path.Combine(root, relative);
                if (File.Exists(target)) continue;
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                CopyVerified(file, target);
            }

            // Existing profiles can contain nested paths left by the former
            // migration too. Repair JSON settings/manifests in both generations.
            foreach (string file in Files(root).Where(file => file.EndsWith(".json", StringComparison.OrdinalIgnoreCase)).ToArray())
            {
                if (file == currentSettings || Path.GetFileName(file) == "pack-catalog.json") continue;
                JsonNode json;
                try { json = JsonNode.Parse(File.ReadAllText(file)); } catch (System.Text.Json.JsonException) { continue; }
                if (json == null) continue;
                RewriteNode(json, Rewrite);
                WriteJson(file, json);
            }

            string catalogPath = Path.Combine(root, "pack-catalog.json");
            string legacyCatalogPath = Path.Combine(old, "pack-catalog.json");
            var catalog = File.Exists(catalogPath) ? JsonNode.Parse(File.ReadAllText(catalogPath)).AsObject() : new JsonObject();
            if (File.Exists(legacyCatalogPath))
            {
                var legacy = JsonNode.Parse(File.ReadAllText(legacyCatalogPath)).AsObject();
                foreach (string group in new[] { "IconPacks", "VoicePacks" })
                {
                    var items = catalog[group] as JsonArray;
                    if (items == null) catalog[group] = items = new JsonArray();
                    var keys = new HashSet<string>(items.Select(item => item?["Key"]?.GetValue<string>() ?? ""), StringComparer.Ordinal);
                    if (legacy[group] is JsonArray source)
                        foreach (var item in source)
                            if (keys.Add(item?["Key"]?.GetValue<string>() ?? "")) items.Add(item.DeepClone());
                }
            }
            RewriteNode(catalog, Rewrite);
            foreach (string group in new[] { "IconPacks", "VoicePacks" })
                if (catalog[group] is JsonArray items)
                    foreach (var item in items)
                    {
                        if (item?["IsBuiltIn"]?.GetValue<bool>() == true) continue;
                        string path = item?["FolderPath"]?.GetValue<string>();
                        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path) || !Files(path).Any())
                            throw new IOException("Imported pack is unavailable: " + item?["DisplayName"]?.GetValue<string>());
                    }
            WriteJson(catalogPath, catalog);
            // The old package may now be removed. Normal startup will not reread
            // its deleted settings, and the installer retains a separate snapshot.
            File.WriteAllText(Path.Combine(Path.GetDirectoryName(root), "migration-v49-complete.txt"), "Verified installer migration completed.");
            WriteJson(Path.Combine(backup, "verified.json"), new JsonObject { ["DataRoot"] = root, ["VerifiedUtc"] = DateTimeOffset.UtcNow.ToString("O") });
            return backup;
        }

        private static string RewriteText(string text, string old, string root, string install, string assets, string backup)
        {
            if (string.IsNullOrEmpty(text)) return text;
            if (text.StartsWith("file:", StringComparison.OrdinalIgnoreCase) && Uri.TryCreate(text, UriKind.Absolute, out var uri) && uri.IsFile)
                return RewriteText(uri.LocalPath, old, root, install, assets, backup);
            if (text.StartsWith("ms-appdata:///local/", StringComparison.OrdinalIgnoreCase))
                return Path.Combine(root, Uri.UnescapeDataString(text.Substring("ms-appdata:///local/".Length)).Replace('/', Path.DirectorySeparatorChar));
            string full = Path.IsPathFullyQualified(text) ? Path.GetFullPath(text) : null;
            if (full != null && Within(full, old)) return Path.Combine(root, Path.GetRelativePath(old, full));
            if (!string.IsNullOrEmpty(install) && full != null && Within(full, install))
            {
                string relative = Path.GetRelativePath(install, full);
                string target = Path.Combine(assets, relative);
                string saved = Path.Combine(backup, "ReferencedPackageAssets", relative);
                if (Directory.Exists(full)) { CopyTree(full, saved, true); CopyTree(full, target, false); }
                else if (File.Exists(full)) { CopyVerified(full, saved); if (!File.Exists(target)) CopyVerified(full, target); }
                else throw new IOException("Referenced old package asset is missing: " + relative);
                return target;
            }
            string trimmed = text.TrimStart();
            if (trimmed.StartsWith("{") || trimmed.StartsWith("["))
            {
                JsonNode json;
                try { json = JsonNode.Parse(text); } catch (System.Text.Json.JsonException) { return text; }
                RewriteNode(json, value => RewriteText(value, old, root, install, assets, backup));
                return json.ToJsonString();
            }
            return text;
        }

        private static void RewriteNode(JsonNode node, Func<string, string> rewrite)
        {
            if (node is JsonObject obj)
                foreach (var pair in obj.ToArray())
                    if (pair.Value is JsonValue value && value.TryGetValue<string>(out string text)) obj[pair.Key] = rewrite(text);
                    else RewriteNode(pair.Value, rewrite);
            else if (node is JsonArray array)
                for (int i = 0; i < array.Count; i++)
                    if (array[i] is JsonValue value && value.TryGetValue<string>(out string text)) array[i] = rewrite(text);
                    else RewriteNode(array[i], rewrite);
        }

        private static bool Within(string path, string parent) =>
            path.Equals(parent, StringComparison.OrdinalIgnoreCase) || path.StartsWith(parent.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        private static IEnumerable<string> Files(string root)
        {
            if (!Directory.Exists(root)) yield break;
            foreach (var entry in new DirectoryInfo(root).EnumerateFileSystemInfos())
            {
                if ((entry.Attributes & FileAttributes.ReparsePoint) != 0) throw new IOException("Migration cannot verify a linked path: " + entry.Name);
                if (entry is DirectoryInfo) { foreach (string file in Files(entry.FullName)) yield return file; }
                else yield return entry.FullName;
            }
        }
        private static void CopyTree(string source, string target, bool overwrite)
        {
            foreach (string file in Files(source))
            {
                string destination = Path.Combine(target, Path.GetRelativePath(source, file));
                if (overwrite || !File.Exists(destination)) CopyVerified(file, destination);
            }
        }
        private static void CopyVerified(string source, string target)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            File.Copy(source, target, true);
            using var original = File.OpenRead(source);
            using var copied = File.OpenRead(target);
            if (!SHA256.HashData(original).SequenceEqual(SHA256.HashData(copied))) throw new IOException("Migration file verification failed: " + Path.GetFileName(source));
        }
        private static void WriteJson(string path, JsonNode value)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temporary = path + ".upgrade.tmp";
            File.WriteAllText(temporary, value.ToJsonString());
            File.Move(temporary, path, true);
        }
    }
}

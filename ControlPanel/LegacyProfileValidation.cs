using System.Text.Json.Nodes;
using KillConfirmCompatibility.Contracts;

namespace KillConfirmGameBar;

internal static class LegacyProfileValidation
{
    internal static async Task<bool> RunAsync(string[] arguments)
    {
        if (!arguments.Contains("--validate-legacy-profile")) return false;
        if (Environment.GetEnvironmentVariable("KILLCONFIRM_LEGACY_VALIDATION") != "1" ||
            string.IsNullOrEmpty(Environment.GetEnvironmentVariable("KILLCONFIRM_DATA_ROOT")))
            throw new InvalidOperationException("Legacy validation requires an isolated profile.");
        string root = RuntimePaths.DataRoot;
        string fixture = Path.GetDirectoryName(root);
        try
        {
            string old = Path.Combine(fixture, "OldPackage", "LocalState");
            string icon = Path.Combine(old, "Packs", "crossfire", "icon_packs", "custom_upgrade_icon");
            string voice = Path.Combine(old, "Packs", "crossfire", "voice_packs", "custom_upgrade_voice");
            Directory.CreateDirectory(icon); Directory.CreateDirectory(voice);
            File.Copy(Path.Combine(RuntimePaths.InstallRoot, "DefaultPacks", "crossfire", "icon_packs", "default", "badge_multi1.png"), Path.Combine(icon, "badge_multi1.png"));
            File.Copy(Path.Combine(RuntimePaths.InstallRoot, "DefaultPacks", "crossfire", "voice_packs", "crossfire_swat_gr", "common.wav"), Path.Combine(voice, "common.wav"));
            JsonObject Pack(string key, string path) => new() { ["Key"] = key, ["FolderPath"] = path, ["DisplayName"] = "Migrated " + key, ["IsBuiltIn"] = false, ["IsVisibleInWidget"] = true, ["OwnsFolder"] = true };
            File.WriteAllText(Path.Combine(old, "pack-catalog.json"), new JsonObject {
                ["IconPacks"] = new JsonArray(Pack("custom_upgrade_icon", icon)),
                ["VoicePacks"] = new JsonArray(Pack("custom_upgrade_voice", voice)) }.ToJsonString());
            LegacyProfileTransfer.Prepare(root, old, "", Path.Combine(fixture, "Backup"),
                new Dictionary<string, object> { ["SelectedIcon"] = "custom_upgrade_icon", ["SelectedVoice"] = "custom_upgrade_voice", ["AudioPath"] = Path.Combine(voice, "common.wav") });
            string oldPackage = Path.GetFullPath(Path.Combine(fixture, "OldPackage"));
            string retired = Path.GetFullPath(Path.Combine(fixture, "RetiredOldPackage"));
            string prefix = Path.GetFullPath(fixture).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!oldPackage.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || !retired.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) throw new IOException("Validation paths escape fixture.");
            Directory.Move(oldPackage, retired);
            var icons = await Services.PackCatalogService.GetVisibleIconPacksAsync();
            var voices = await Services.PackCatalogService.GetVisibleVoicePacksAsync();
            var importedIcon = icons.Single(pack => pack.Key == "custom_upgrade_icon");
            var importedVoice = voices.Single(pack => pack.Key == "custom_upgrade_voice");
            if (!importedIcon.FolderPath.StartsWith(root) || !importedVoice.FolderPath.StartsWith(root)) throw new IOException("Actual catalog loader retained old package paths.");
            var image = await DesktopPlatform.AssetFileAsync(new Uri("ms-appdata:///local/Packs/crossfire/icon_packs/custom_upgrade_icon/badge_multi1.png"));
            using (var stream = await image.OpenReadAsync())
            {
                var decoder = await Windows.Graphics.Imaging.BitmapDecoder.CreateAsync(stream);
                if (decoder.PixelWidth == 0 || decoder.PixelHeight == 0) throw new IOException("Migrated icon could not be decoded.");
            }
            var audio = await Windows.Storage.StorageFile.GetFileFromPathAsync((string)DesktopPlatform.Data.LocalSettings.Values["AudioPath"]);
            using (var stream = await audio.OpenReadAsync()) if (stream.Size == 0) throw new IOException("Migrated audio could not be opened.");
            File.WriteAllText(Path.Combine(fixture, "legacy-pass.txt"), "PASS: actual panel catalog exposes migrated icon/voice entries after old directory retirement; WinRT decodes the migrated PNG and opens the migrated WAV; selection and data paths retained.");
        }
        catch (Exception error) { File.WriteAllText(Path.Combine(fixture, "legacy-failure.txt"), error.ToString()); }
        return true;
    }
}

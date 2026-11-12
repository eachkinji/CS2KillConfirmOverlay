// Shared by Widget (UWP) and CompatibilityHost; C# 7.3 compatible.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Data.Json;
using Windows.Storage;

#if WINDOWS_UWP
namespace KillConfirmGameBar.Services
#else
namespace KillConfirmCompatibility.Services
#endif
{
    public static partial class PackCatalogService
    {
        public static readonly IReadOnlyList<string> Cod2022IconSlotFileNames = new[]
        {
            "hud_damage_feedback_marker.png",
            "hud_damage_feedback_burst.png",
            "hud_glow.png",
            "hud_splash_accent_diamond.png",
            "hud_splash_accent_reticle.png",
            "hud_splash_accent_chevrons_single.png",
            "hud_splash_notched_background_5.png",
            "hud_splash_notched_background_4.png",
            "hud_icon_splash_kill.png",
            "hud_icon_obit_kill.png",
            "hud_icon_obit_headshot.png",
            "hud_icon_obit_destroy.png",
            "widg_gradient_center_out.png",
            "widg_gradient_left_to_right.png"
        };

        public static bool IsCod2022IconPackKey(string key)
        {
            return !string.IsNullOrWhiteSpace(key)
                && (key.StartsWith("custom_cod2022_icon_", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(key, "cod2022_icon_standard", StringComparison.OrdinalIgnoreCase)
                    || GameStyleService.IsCod2022Key(key));
        }

        public static Task ImportCod2022IconPackAsync(StorageFolder folder)
            => ImportGameIconPackAsync("custom_cod2022_icon_", folder);

        public static Task CreateCod2022IconPackAsync(
            string displayName,
            IReadOnlyDictionary<string, StorageFile> selectedFiles,
            StorageFile headImageFile = null)
            => CreateGameIconPackAsync("cod2022", "custom_cod2022_icon_", displayName, selectedFiles, headImageFile);

        public static async Task<string> ImportCod2022IconManifestPackAsync(StorageFolder source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            StorageFile manifestFile = await source.GetFileAsync("manifest.json");
            JsonObject manifest = JsonObject.Parse(await FileIO.ReadTextAsync(manifestFile));
            string declaredId = manifest.GetNamedString("id", "");
            string key = !string.IsNullOrWhiteSpace(declaredId)
                && declaredId.IndexOfAny(Path.GetInvalidFileNameChars()) < 0
                && (string.Equals(declaredId, "cod2022_icon_standard", StringComparison.OrdinalIgnoreCase)
                    || declaredId.StartsWith("cod2022_icon_", StringComparison.OrdinalIgnoreCase)
                    || declaredId.StartsWith("custom_cod2022_icon_", StringComparison.OrdinalIgnoreCase))
                ? declaredId
                : "custom_cod2022_icon_" + Guid.NewGuid().ToString("N");

            string displayName = manifest.GetNamedString("display_name_zh_cn",
                manifest.GetNamedString("display_name", manifest.GetNamedString("name", source.DisplayName)));
            if (string.IsNullOrWhiteSpace(displayName))
            {
                displayName = "现代战争 II 图标包";
            }

            StorageFolder root = await GetGameIconPacksFolderAsync("cod2022");
            StorageFolder staged = await root.CreateFolderAsync(".import-cod2022-" + Guid.NewGuid().ToString("N"), CreationCollisionOption.FailIfExists);
            bool committed = false;
            try
            {
                await CopyAudioPackFolderAsync(source, staged);
                await staged.RenameAsync(SanitizeName(displayName), NameCollisionOption.GenerateUniqueName);

                var catalog = await LoadAsync();
                var previous = catalog.IconPacks.FirstOrDefault(p => string.Equals(p.Key, key, StringComparison.OrdinalIgnoreCase));
                var item = new IconPackItem
                {
                    Key = key,
                    DisplayName = displayName,
                    FolderPath = staged.Path,
                    IsBuiltIn = false,
                    IsVisibleInWidget = previous != null ? previous.IsVisibleInWidget : true,
                    OwnsFolder = true
                };

                var updated = catalog.IconPacks
                    .Select(p => string.Equals(p.Key, key, StringComparison.OrdinalIgnoreCase) ? item : p)
                    .ToList();
                if (previous == null)
                {
                    updated.Add(item);
                }
                catalog.IconPacks = updated;
                await SaveAsync(catalog);
                committed = true;

                if (previous != null && previous.OwnsFolder
                    && !string.IsNullOrWhiteSpace(previous.FolderPath)
                    && !string.Equals(previous.FolderPath, staged.Path, StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        if (Directory.Exists(previous.FolderPath))
                        {
                            Directory.Delete(previous.FolderPath, true);
                        }
                    }
                    catch
                    {
                    }
                }

                return displayName;
            }
            finally
            {
                if (!committed)
                {
                    try { await staged.DeleteAsync(StorageDeleteOption.PermanentDelete); } catch { }
                }
            }
        }
    }
}

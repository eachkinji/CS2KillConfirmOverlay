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
        public static readonly IReadOnlyList<string> Rdr2IconSlotFileNames = new[]
        {
            "hitmarker_shot_white.png",
            "hitmarker_kill_red.png",
            "hitmarker_kill_red_tapered.png",
            "hitmarker_critical_crimson.png",
            "hitmarker_perfect_kill_gold.png",
            "deadeye_kill_cross_red.png",
            "reticle_center_dot_default_white.png",
            "reticle_center_dot_enemy_red.png",
            "reticle_accuracy_circle.png",
            "honor_bg.png",
            "honor_bar_grad_half.png",
            "honor_background.png",
            "honor_good.png",
            "honor_bad.png",
            "honor_cowboy_badge_hd.png",
            "honor_skull_badge_hd.png",
            "honor_plus_sign.png",
            "honor_minus_sign.png",
            "overhead_skull.png",
            "overhead_deadeye.png"
        };

        public static bool IsRdr2IconPackKey(string key)
        {
            return !string.IsNullOrWhiteSpace(key)
                && (key.StartsWith("custom_rdr2_icon_", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(key, "rdr2_icon_standard", StringComparison.OrdinalIgnoreCase)
                    || GameStyleService.IsRdr2Key(key));
        }

        public static Task ImportRdr2IconPackAsync(StorageFolder folder)
            => ImportGameIconPackAsync("custom_rdr2_icon_", folder);

        public static Task CreateRdr2IconPackAsync(
            string displayName,
            IReadOnlyDictionary<string, StorageFile> selectedFiles,
            StorageFile headImageFile = null)
            => CreateGameIconPackAsync("rdr2", "custom_rdr2_icon_", displayName, selectedFiles, headImageFile);

        public static async Task<string> ImportRdr2IconManifestPackAsync(StorageFolder source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            StorageFile manifestFile = await source.GetFileAsync("manifest.json");
            JsonObject manifest = JsonObject.Parse(await FileIO.ReadTextAsync(manifestFile));
            string declaredId = manifest.GetNamedString("id", "");
            string key = !string.IsNullOrWhiteSpace(declaredId)
                && declaredId.IndexOfAny(Path.GetInvalidFileNameChars()) < 0
                && (string.Equals(declaredId, "rdr2_icon_standard", StringComparison.OrdinalIgnoreCase)
                    || declaredId.StartsWith("rdr2_icon_", StringComparison.OrdinalIgnoreCase)
                    || declaredId.StartsWith("custom_rdr2_icon_", StringComparison.OrdinalIgnoreCase))
                ? declaredId
                : "custom_rdr2_icon_" + Guid.NewGuid().ToString("N");

            string displayName = manifest.GetNamedString("display_name_zh_cn",
                manifest.GetNamedString("display_name", manifest.GetNamedString("name", source.DisplayName)));
            if (string.IsNullOrWhiteSpace(displayName))
            {
                displayName = "荒野大镖客 2 图标包";
            }

            StorageFolder root = await GetGameIconPacksFolderAsync("rdr2");
            StorageFolder staged = await root.CreateFolderAsync(".import-rdr2-" + Guid.NewGuid().ToString("N"), CreationCollisionOption.FailIfExists);
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

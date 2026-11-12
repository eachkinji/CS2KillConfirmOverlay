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
        public static readonly IReadOnlyList<string> WarThunderIconSlotFileNames = new[]
        {
            "streaks_event_bg.png",
            "streaks_first_blood.png",
            "streaks_multi_kill_bg.png",
            "streaks_multi_kill_ground_2_3_4_x.png",
            "streaks_multi_kill_air_2_3_4_x.png",
            "streaks_multi_kill_ship_2_3_4_x.png",
            "streaks_rank_2.png",
            "streaks_rank_3.png",
            "streaks_multi_kill_rank_4.png",
            "streaks_multi_kill_rank_x.png",
            "streaks_tank_bg.png",
            "streaks_heroic_sniper.png",
            "streaks_global_bg.png",
            "streaks_global_avenge_self.png",
            "streaks_marks_bg.png",
            "streaks_marks_killed_plane_20_ranks_higher.png",
            "kill_unit_icon.png",
            "hit_unit_icon.png",
            "engine_state_indicator.png",
            "gun_state_indicator.png",
            "turret_gear_state_indicator.png",
            "track_state_indicator.png",
            "crew.png",
            "ship_hit_citadel_icon.png"
        };

        public static bool IsWarThunderIconPackKey(string key)
        {
            return !string.IsNullOrWhiteSpace(key)
                && (key.StartsWith("custom_warthunder_icon_", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(key, "warthunder_icon_standard", StringComparison.OrdinalIgnoreCase)
                    || GameStyleService.IsWarThunderKey(key));
        }

        public static Task ImportWarThunderIconPackAsync(StorageFolder folder)
            => ImportGameIconPackAsync("custom_warthunder_icon_", folder);

        public static Task CreateWarThunderIconPackAsync(
            string displayName,
            IReadOnlyDictionary<string, StorageFile> selectedFiles,
            StorageFile headImageFile = null)
            => CreateGameIconPackAsync("warthunder", "custom_warthunder_icon_", displayName, selectedFiles, headImageFile);

        public static async Task<string> ImportWarThunderIconManifestPackAsync(StorageFolder source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            StorageFile manifestFile = await source.GetFileAsync("manifest.json");
            JsonObject manifest = JsonObject.Parse(await FileIO.ReadTextAsync(manifestFile));
            string declaredId = manifest.GetNamedString("id", "");
            string key = !string.IsNullOrWhiteSpace(declaredId)
                && declaredId.IndexOfAny(Path.GetInvalidFileNameChars()) < 0
                && (string.Equals(declaredId, "warthunder_icon_standard", StringComparison.OrdinalIgnoreCase)
                    || declaredId.StartsWith("warthunder_icon_", StringComparison.OrdinalIgnoreCase)
                    || declaredId.StartsWith("custom_warthunder_icon_", StringComparison.OrdinalIgnoreCase))
                ? declaredId
                : "custom_warthunder_icon_" + Guid.NewGuid().ToString("N");

            string displayName = manifest.GetNamedString("display_name_zh_cn",
                manifest.GetNamedString("display_name", manifest.GetNamedString("name", source.DisplayName)));
            if (string.IsNullOrWhiteSpace(displayName))
            {
                displayName = "战争雷霆 图标包";
            }

            StorageFolder root = await GetGameIconPacksFolderAsync("warthunder");
            StorageFolder staged = await root.CreateFolderAsync(".import-wt-" + Guid.NewGuid().ToString("N"), CreationCollisionOption.FailIfExists);
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

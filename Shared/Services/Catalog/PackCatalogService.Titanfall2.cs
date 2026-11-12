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
        public static readonly IReadOnlyList<string> Titanfall2IconSlotFileNames = new[]
        {
            "badge_kill_pilot.png",
            "badge_headshot.png",
            "badge_double_kill.png",
            "badge_multi_kill.png",
            "badge_kill_titan.png",
            "badge_first_strike.png",
            "badge_victory.png",
            "badge_wings.png",
            "badge_titanfall.png",
            "badge_eject.png",
            "hit_indicator.png",
            "hit_indicator_crit_bg.png",
        };

        public static bool IsTitanfall2IconPackKey(string key)
        {
            return !string.IsNullOrWhiteSpace(key)
                && (key.StartsWith("custom_titanfall2_icon_", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(key, "titanfall2_icon_standard", StringComparison.OrdinalIgnoreCase)
                    || GameStyleService.IsTitanfall2Key(key));
        }

        public static Task ImportTitanfall2IconPackAsync(StorageFolder folder)
            => ImportGameIconPackAsync("custom_titanfall2_icon_", folder);

        public static Task CreateTitanfall2IconPackAsync(string displayName,
            IReadOnlyDictionary<string, StorageFile> selectedFiles, StorageFile headImageFile = null)
            => CreateGameIconPackAsync("titanfall2", "custom_titanfall2_icon_", displayName, selectedFiles, headImageFile);

        public static async Task<string> ImportTitanfall2IconManifestPackAsync(StorageFolder source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            StorageFile manifestFile = await source.GetFileAsync("manifest.json");
            JsonObject manifest = JsonObject.Parse(await FileIO.ReadTextAsync(manifestFile));
            string declaredId = manifest.GetNamedString("id", "");
            string key = !string.IsNullOrWhiteSpace(declaredId)
                && declaredId.IndexOfAny(Path.GetInvalidFileNameChars()) < 0
                && (string.Equals(declaredId, "titanfall2_icon_standard", StringComparison.OrdinalIgnoreCase)
                    || declaredId.StartsWith("titanfall2_icon_", StringComparison.OrdinalIgnoreCase)
                    || declaredId.StartsWith("custom_titanfall2_icon_", StringComparison.OrdinalIgnoreCase))
                ? declaredId
                : "custom_titanfall2_icon_" + Guid.NewGuid().ToString("N");

            string displayName = manifest.GetNamedString("display_name_zh_cn",
                manifest.GetNamedString("display_name", manifest.GetNamedString("name", source.DisplayName)));
            if (string.IsNullOrWhiteSpace(displayName))
            {
                displayName = "泰坦陨落 2 图标包";
            }

            StorageFolder root = await GetGameIconPacksFolderAsync("titanfall2");
            StorageFolder staged = await root.CreateFolderAsync(".import-tf2-" + Guid.NewGuid().ToString("N"), CreationCollisionOption.FailIfExists);
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

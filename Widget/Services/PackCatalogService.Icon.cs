using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using KillConfirmGameBar.Helpers;
using Windows.Storage;

namespace KillConfirmGameBar.Services
{
    public static partial class PackCatalogService
    {
        public static string GetIconPackDisplayName(IconPackItem item)
        {
            if (item == null) return string.Empty;
            if (ValorantPackService.IsValorantPackKey(item.Key))
            {
                return ValorantPackService.GetDisplayName(item.Key);
            }

            if (item.IsBuiltIn)
            {
                string localized = LocalizationManager.Text(item.Key);
                return string.Equals(localized, item.Key, StringComparison.OrdinalIgnoreCase)
                    ? item.DisplayName
                    : localized;
            }
            return item.DisplayName;
        }

        public static async Task<IReadOnlyList<IconPackItem>> GetVisibleIconPacksAsync()
        {
            var catalog = await LoadAsync();
            IEnumerable<IconPackItem> visible = catalog.IconPacks
                .Where(p => p.IsVisibleInWidget && GameStyleService.IsVisibleForCurrentStyle(p.Key))
                .ToList();
            if (GameStyleService.Current == GameStyleMode.Valorant)
            {
                visible = visible.OrderBy(p => ValorantPackService.GetDisplayOrder(p.Key));
            }
            return visible.ToList();
        }

        public static async Task<IReadOnlyList<IconPackItem>> GetAllIconPacksAsync()
        {
            var catalog = await LoadAsync();
            return catalog.IconPacks;
        }

        public static async Task<IconPackItem> GetIconPackAsync(string key)
        {
            var catalog = await LoadAsync();
            return catalog.IconPacks.FirstOrDefault(p =>
                string.Equals(p.Key, key, StringComparison.OrdinalIgnoreCase));
        }

        public static bool IsImportedIconPackKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return false;
            return key.StartsWith("custom_icon_", StringComparison.OrdinalIgnoreCase)
                || key.StartsWith("custom_csol_icon_", StringComparison.OrdinalIgnoreCase)
                || key.StartsWith("custom_dagoujiao_icon_", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsCsolIconPackKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return false;
            return key.StartsWith("custom_csol_icon_", StringComparison.OrdinalIgnoreCase)
                || key.StartsWith("csol", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsDagoujiaoIconPackKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return false;
            return key.StartsWith("custom_dagoujiao_icon_", StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, "dagoujiao", StringComparison.OrdinalIgnoreCase);
        }

        public static async Task<StorageFolder> GetImportedIconFolderAsync(string key)
        {
            var item = await GetIconPackAsync(key);
            if (item == null || string.IsNullOrEmpty(item.FolderPath)) return null;

            try
            {
                return await StorageFolder.GetFolderFromPathAsync(item.FolderPath);
            }
            catch
            {
                return null;
            }
        }

        public static async Task<IconPackItem> RefreshImportedIconPackCapabilitiesAsync(string key)
        {
            var catalog = await LoadAsync();
            var item = catalog.IconPacks.FirstOrDefault(p => string.Equals(p.Key, key, StringComparison.OrdinalIgnoreCase));
            if (item == null || item.IsBuiltIn || string.IsNullOrWhiteSpace(item.FolderPath))
            {
                return item;
            }

            StorageFolder folder;
            try
            {
                folder = await StorageFolder.GetFolderFromPathAsync(item.FolderPath);
            }
            catch
            {
                return item;
            }

            IconPackCapabilities capabilities = await DetectIconPackCapabilitiesAsync(folder);
            bool changed = item.HasFxOverlay != capabilities.HasKillFxOverlay
                || item.HasKillFxOverlay != capabilities.HasKillFxOverlay
                || item.HasEliteOverlay != capabilities.HasEliteOverlay
                || item.HasWeaponBadgeOverlay != capabilities.HasWeaponBadgeOverlay;

            item.HasFxOverlay = capabilities.HasKillFxOverlay;
            item.HasKillFxOverlay = capabilities.HasKillFxOverlay;
            item.HasEliteOverlay = capabilities.HasEliteOverlay;
            item.HasWeaponBadgeOverlay = capabilities.HasWeaponBadgeOverlay;

            if (changed)
            {
                await SaveAsync(catalog);
            }

            return item;
        }

        public static async Task ImportIconPackAsync(StorageFolder folder)
        {
            IconPackCapabilities capabilities = await DetectIconPackCapabilitiesAsync(folder);
            var catalog = await LoadAsync();
            catalog.IconPacks.Add(new IconPackItem
            {
                Key = "custom_icon_" + Guid.NewGuid().ToString("N"),
                DisplayName = folder.DisplayName,
                FolderPath = folder.Path,
                IsBuiltIn = false,
                IsVisibleInWidget = true,
                OwnsFolder = false,
                HasFxOverlay = capabilities.HasKillFxOverlay,
                HasKillFxOverlay = capabilities.HasKillFxOverlay,
                HasEliteOverlay = capabilities.HasEliteOverlay,
                HasWeaponBadgeOverlay = capabilities.HasWeaponBadgeOverlay
            });
            await SaveAsync(catalog);
        }

        public static async Task ImportCsolIconPackAsync(StorageFolder folder)
        {
            var catalog = await LoadAsync();
            catalog.IconPacks.Add(new IconPackItem
            {
                Key = "custom_csol_icon_" + Guid.NewGuid().ToString("N"),
                DisplayName = folder.DisplayName,
                FolderPath = folder.Path,
                IsBuiltIn = false,
                IsVisibleInWidget = true,
                OwnsFolder = false
            });
            await SaveAsync(catalog);
        }

        public static async Task ImportDagoujiaoIconPackAsync(StorageFolder folder)
        {
            var catalog = await LoadAsync();
            catalog.IconPacks.Add(new IconPackItem
            {
                Key = "custom_dagoujiao_icon_" + Guid.NewGuid().ToString("N"),
                DisplayName = folder.DisplayName,
                FolderPath = folder.Path,
                IsBuiltIn = false,
                IsVisibleInWidget = true,
                OwnsFolder = false
            });
            await SaveAsync(catalog);
        }

        public static async Task<IconPackCapabilities> DetectIconPackCapabilitiesAsync(StorageFolder folder)
        {
            return new IconPackCapabilities
            {
                HasKillFxOverlay = await ContainsAnyFileAsync(folder,
                    "multi2_fx.png", "multi2_fx.tga",
                    "multi3_fx.png", "multi3_fx.tga",
                    "multi4_fx.png", "multi4_fx.tga",
                    "multi5_fx.png", "multi5_fx.tga",
                    "multi6_fx.png", "multi6_fx.tga"),
                HasEliteOverlay = await ContainsAnyFileAsync(folder,
                    "KillMark_Upgrade1.png", "KillMark_Upgrade1.tga",
                    "KillMark_Upgrade2.png", "KillMark_Upgrade2.tga",
                    "KillMark_Upgrade3.png", "KillMark_Upgrade3.tga",
                    "badge_knife_1.png", "badge_knife_1.tga",
                    "badge_knife_2.png", "badge_knife_2.tga",
                    "badge_knife_3.png", "badge_knife_3.tga"),
                HasWeaponBadgeOverlay = await ContainsAnyFileAsync(folder,
                    "badge_assault1.png", "badge_assault1.tga",
                    "badge_assault2.png", "badge_assault2.tga",
                    "badge_assault3.png", "badge_assault3.tga",
                    "badge_scout1.png", "badge_scout1.tga",
                    "badge_scout2.png", "badge_scout2.tga",
                    "badge_scout3.png", "badge_scout3.tga",
                    "badge_sniper1.png", "badge_sniper1.tga",
                    "badge_sniper2.png", "badge_sniper2.tga",
                    "badge_sniper3.png", "badge_sniper3.tga",
                    "badge_elite1.png", "badge_elite1.tga",
                    "badge_elite2.png", "badge_elite2.tga",
                    "badge_elite3.png", "badge_elite3.tga",
                    "badge_knife1.png", "badge_knife1.tga",
                    "badge_knife2.png", "badge_knife2.tga",
                    "badge_knife3.png", "badge_knife3.tga")
            };
        }

        private static async Task<bool> ContainsAnyFileAsync(StorageFolder folder, params string[] fileNames)
        {
            foreach (string name in fileNames)
            {
                foreach (string candidate in ExpandIconFileCandidates(name))
                {
                    try
                    {
                        await folder.GetFileAsync(candidate);
                        return true;
                    }
                    catch
                    {
                    }
                }
            }

            return false;
        }

        private static IEnumerable<string> ExpandIconFileCandidates(string fileName)
        {
            string extension = Path.GetExtension(fileName);
            if (extension.Equals(".png", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".tga", StringComparison.OrdinalIgnoreCase))
            {
                string withoutExtension = Path.ChangeExtension(fileName, null);
                foreach (string candidateExtension in IconImageExtensions)
                {
                    yield return withoutExtension + candidateExtension;
                }

                yield break;
            }

            yield return fileName;
        }

        public static async Task SetIconPackVisibilityAsync(string key, bool isVisible)
        {
            var catalog = await LoadAsync();
            var item = catalog.IconPacks.FirstOrDefault(p => p.Key == key);
            if (item != null)
            {
                item.IsVisibleInWidget = isVisible;
                SaveVisibilityOverride("icon", item.Key, isVisible);
                await SaveAsync(catalog);
            }
        }

        public static async Task RemoveCustomIconPackAsync(string key)
        {
            var catalog = await LoadAsync();
            var item = catalog.IconPacks.FirstOrDefault(p => p.Key == key);
            if (item != null && !item.IsBuiltIn)
            {
                catalog.IconPacks.Remove(item);
                await SaveAsync(catalog);
                if (item.OwnsFolder)
                {
                    try
                    {
                        var folder = await StorageFolder.GetFolderFromPathAsync(item.FolderPath);
                        await folder.DeleteAsync();
                    }
                    catch { }
                }
            }
        }

        public static async Task CreateIconPackAsync(string displayName, IReadOnlyDictionary<string, StorageFile> selectedFiles)
        {
            if (selectedFiles == null || selectedFiles.Count == 0)
            {
                return;
            }

            StorageFolder root = await GetOrCreatePackRootAsync("GeneratedIconPacks");
            StorageFolder packFolder = await root.CreateFolderAsync(
                SanitizeName(displayName),
                CreationCollisionOption.GenerateUniqueName);

            foreach (var pair in selectedFiles)
            {
                if (pair.Value == null)
                {
                    continue;
                }

                if (pair.Value.FileType.Equals(".tga", StringComparison.OrdinalIgnoreCase))
                {
                    await TgaDecoder.ConvertTgaToPngAsync(pair.Value, packFolder, pair.Key);
                }
                else
                {
                    await pair.Value.CopyAsync(packFolder, pair.Key, NameCollisionOption.ReplaceExisting);
                }
            }

            IconPackCapabilities capabilities = await DetectIconPackCapabilitiesAsync(packFolder);

            PackCatalog catalog = await LoadAsync();
            catalog.IconPacks.Add(new IconPackItem
            {
                Key = "custom_icon_" + Guid.NewGuid().ToString("N"),
                DisplayName = displayName,
                FolderPath = packFolder.Path,
                IsBuiltIn = false,
                IsVisibleInWidget = true,
                OwnsFolder = true,
                HasFxOverlay = capabilities.HasKillFxOverlay,
                HasKillFxOverlay = capabilities.HasKillFxOverlay,
                HasEliteOverlay = capabilities.HasEliteOverlay,
                HasWeaponBadgeOverlay = capabilities.HasWeaponBadgeOverlay
            });
            await SaveAsync(catalog);
        }

        // CSOL icon packs have a fixed 13-slot layout (no FX / Elite / Weapon
        // Badge overlays — the CSOL rendering path does not consume them).
        // The icon keys mirror the CF kill_<n> / headshot / knife / first_and_last
        // scheme so the existing icon resolver works unchanged.
        public static readonly IReadOnlyList<string> CsolIconSlotFileNames = new[]
        {
            "badge_headshot.png",
            "badge_knife.png",
            "badge_firstkill.png",
            "badge_lastkill.png",
            "multi2.png",
            "multi3.png",
            "multi4.png",
            "multi5.png",
            "multi6.png",
            "multi7.png",
            "multi8.png",
            "multi9.png",
            "multi10.png"
        };

        public static async Task CreateCsolIconPackAsync(string displayName, IReadOnlyDictionary<string, StorageFile> selectedFiles)
        {
            if (selectedFiles == null || selectedFiles.Count == 0)
            {
                return;
            }

            StorageFolder root = await GetOrCreatePackRootAsync("GeneratedCsolIconPacks");
            StorageFolder packFolder = await root.CreateFolderAsync(
                SanitizeName(displayName),
                CreationCollisionOption.GenerateUniqueName);

            foreach (var pair in selectedFiles)
            {
                if (pair.Value == null)
                {
                    continue;
                }

                if (pair.Value.FileType.Equals(".tga", StringComparison.OrdinalIgnoreCase))
                {
                    await TgaDecoder.ConvertTgaToPngAsync(pair.Value, packFolder, pair.Key);
                }
                else
                {
                    await pair.Value.CopyAsync(packFolder, pair.Key, NameCollisionOption.ReplaceExisting);
                }
            }

            PackCatalog catalog = await LoadAsync();
            catalog.IconPacks.Add(new IconPackItem
            {
                Key = "custom_csol_icon_" + Guid.NewGuid().ToString("N"),
                DisplayName = displayName,
                FolderPath = packFolder.Path,
                IsBuiltIn = false,
                IsVisibleInWidget = true,
                OwnsFolder = true,
                HasFxOverlay = false,
                HasKillFxOverlay = false,
                HasEliteOverlay = false,
                HasWeaponBadgeOverlay = false
            });
            await SaveAsync(catalog);
        }

        public static readonly IReadOnlyList<string> DagoujiaoIconSlotFileNames = new[]
        {
            "common.png",
            "headshot.png",
            "epic.jpg",
            "1kill.png",
            "2kill.png",
            "3kill.png",
            "4kill.png",
            "5kill.png"
        };

        public static async Task CreateDagoujiaoIconPackAsync(string displayName, IReadOnlyDictionary<string, StorageFile> selectedFiles)
        {
            if (selectedFiles == null || selectedFiles.Count == 0)
            {
                return;
            }

            StorageFolder root = await GetOrCreatePackRootAsync("GeneratedDagoujiaoIconPacks");
            StorageFolder packFolder = await root.CreateFolderAsync(
                SanitizeName(displayName),
                CreationCollisionOption.GenerateUniqueName);

            foreach (var pair in selectedFiles)
            {
                if (pair.Value == null)
                {
                    continue;
                }

                if (pair.Value.FileType.Equals(".tga", StringComparison.OrdinalIgnoreCase))
                {
                    await TgaDecoder.ConvertTgaToPngAsync(pair.Value, packFolder, pair.Key);
                }
                else
                {
                    await pair.Value.CopyAsync(packFolder, pair.Key, NameCollisionOption.ReplaceExisting);
                }
            }

            PackCatalog catalog = await LoadAsync();
            catalog.IconPacks.Add(new IconPackItem
            {
                Key = "custom_dagoujiao_icon_" + Guid.NewGuid().ToString("N"),
                DisplayName = displayName,
                FolderPath = packFolder.Path,
                IsBuiltIn = false,
                IsVisibleInWidget = true,
                OwnsFolder = true,
                HasFxOverlay = false,
                HasKillFxOverlay = false,
                HasEliteOverlay = false,
                HasWeaponBadgeOverlay = false
            });
            await SaveAsync(catalog);
        }
    }
}

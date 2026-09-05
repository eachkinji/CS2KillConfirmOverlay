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
        public static async Task CreateIconPackAsync(string displayName, IReadOnlyDictionary<string, StorageFile> selectedFiles, StorageFile headImageFile = null)
        {
            if (selectedFiles == null || selectedFiles.Count == 0)
            {
                return;
            }

            StorageFolder root = await GetGameIconPacksFolderAsync("crossfire");
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

            await WriteIconPackHeadImageAsync(packFolder, headImageFile);

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

        // CSOL icon packs use the exact filenames consumed by
        // KillConfirmAnimation.Csol. Keep this list aligned with that renderer;
        // CF-style badge_* / multi* names are not valid CSOL slots.
        public static readonly IReadOnlyList<string> CsolIconSlotFileNames = new[]
        {
            "1kill.png",
            "2kill.png",
            "3kill.png",
            "4kill.png",
            "5kill.png",
            "6kill.png",
            "7kill.png",
            "8kill.png",
            "9kill.png",
            "10kill.png",
            "headshot_kill.png",
            "melee_kill.png",
            "revenge.png",
            "firstkill.png",
            "assist.png"
        };

        public static async Task CreateCsolIconPackAsync(string displayName, IReadOnlyDictionary<string, StorageFile> selectedFiles, StorageFile headImageFile = null)
        {
            if (selectedFiles == null || selectedFiles.Count == 0)
            {
                return;
            }

            StorageFolder root = await GetGameIconPacksFolderAsync("csol");
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

            await WriteIconPackHeadImageAsync(packFolder, headImageFile);

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

        public static async Task CreateDagoujiaoIconPackAsync(string displayName, IReadOnlyDictionary<string, StorageFile> selectedFiles, StorageFile headImageFile = null)
        {
            if (selectedFiles == null || selectedFiles.Count == 0)
            {
                return;
            }

            StorageFolder root = await GetGameIconPacksFolderAsync("dagoujiao");
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

            await WriteIconPackHeadImageAsync(packFolder, headImageFile);

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

        public static async Task ImportDoubaoIconPackAsync(StorageFolder folder)
        {
            var catalog = await LoadAsync();
            catalog.IconPacks.Add(new IconPackItem
            {
                Key = "custom_doubao_icon_" + Guid.NewGuid().ToString("N"),
                DisplayName = folder.DisplayName,
                FolderPath = folder.Path,
                IsBuiltIn = false,
                IsVisibleInWidget = true,
                OwnsFolder = false
            });
            await SaveAsync(catalog);
        }

        // Doubao icon packs carry 5 independent per-kill images (1kill.png..5kill.png),
        // mirroring the Dagoujiao per-kill override pattern. The icon keys map straight
        // to the animation's kill-streak lookup so a folder-based pack works unchanged.
        public static readonly IReadOnlyList<string> DoubaoIconSlotFileNames = new[]
        {
            "1kill.png",
            "2kill.png",
            "3kill.png",
            "4kill.png",
            "5kill.png"
        };

        public static async Task CreateDoubaoIconPackAsync(string displayName, IReadOnlyDictionary<string, StorageFile> selectedFiles, StorageFile headImageFile = null)
        {
            if (selectedFiles == null || selectedFiles.Count == 0)
            {
                return;
            }

            StorageFolder root = await GetGameIconPacksFolderAsync("doubao");
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

            await WriteIconPackHeadImageAsync(packFolder, headImageFile);

            PackCatalog catalog = await LoadAsync();
            catalog.IconPacks.Add(new IconPackItem
            {
                Key = "custom_doubao_icon_" + Guid.NewGuid().ToString("N"),
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

        // ---- Battlefield / Delta Force icon packs (static-image override only) ----
        // These games draw a fixed set of static kill icons in the animation; users may
        // override just those. Dynamic frames / decorative textures stay built-in: the
        // animation loader falls back to ms-appx when a file is absent from the pack.

        public static readonly IReadOnlyList<string> Battlefield1IconSlotFileNames = new[]
        {
            "killicon_battlefield1_default.png",
            "killicon_battlefield1_headshot.png",
            "killicon_battlefield1_crit.png"
        };

        public static readonly IReadOnlyList<string> Battlefield5IconSlotFileNames = new[]
        {
            "killicon_battlefield5_default.png",
            "killicon_battlefield5_headshot.png",
            "killicon_battlefield5_assist.png"
        };

        public static readonly IReadOnlyList<string> Battlefield2042IconSlotFileNames = new[]
        {
            "NormalSkullSprite.png",
            "HeadshotSkullSprite.png",
            "AssistSprite.png"
        };

        public static readonly IReadOnlyList<string> DeltaForceIconSlotFileNames = new[]
        {
            "killicon_df_default.png",
            "killicon_df_headshot.png",
            "killicon_df_capture.png",
            "killicon_scrolling_assist.png"
        };

        public static Task CreateOverwatchIconPackAsync(string displayName, IReadOnlyDictionary<string, StorageFile> selectedFiles, StorageFile headImageFile = null)
            => CreateGameIconPackAsync("overwatch", "custom_overwatch_icon_", displayName, selectedFiles, headImageFile);
        public static Task CreateModernWarfare2019IconPackAsync(string displayName, IReadOnlyDictionary<string, StorageFile> selectedFiles, StorageFile headImageFile = null)
            => CreateGameIconPackAsync("modernwarfare2019", "custom_modernwarfare2019_icon_", displayName, selectedFiles, headImageFile);
        public static Task CreateApexIconPackAsync(string displayName, IReadOnlyDictionary<string, StorageFile> selectedFiles, StorageFile headImageFile = null)
            => CreateGameIconPackAsync("apex", "custom_apex_icon_", displayName, selectedFiles, headImageFile);

        public static bool IsBattlefield1IconPackKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return false;
            return key.StartsWith("custom_battlefield1_icon_", StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, "battlefield1", StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, "bf1", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsBattlefield5IconPackKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return false;
            return key.StartsWith("custom_battlefield5_icon_", StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, "battlefield5", StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, "bf5", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsBattlefield2042IconPackKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return false;
            return key.StartsWith("custom_battlefield2042_icon_", StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, "battlefield2042", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsDeltaForceIconPackKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return false;
            return key.StartsWith("custom_deltaforce_icon_", StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, "deltaforce", StringComparison.OrdinalIgnoreCase);
        }

        public static Task ImportBattlefield1IconPackAsync(StorageFolder folder)
            => ImportGameIconPackAsync("custom_battlefield1_icon_", folder);
        public static Task ImportBattlefield5IconPackAsync(StorageFolder folder)
            => ImportGameIconPackAsync("custom_battlefield5_icon_", folder);
        public static Task ImportBattlefield2042IconPackAsync(StorageFolder folder)
            => ImportGameIconPackAsync("custom_battlefield2042_icon_", folder);
        public static Task ImportDeltaForceIconPackAsync(StorageFolder folder)
            => ImportGameIconPackAsync("custom_deltaforce_icon_", folder);

        public static Task CreateBattlefield1IconPackAsync(string displayName, IReadOnlyDictionary<string, StorageFile> selectedFiles, StorageFile headImageFile = null)
            => CreateGameIconPackAsync("battlefield1", "custom_battlefield1_icon_", displayName, selectedFiles, headImageFile);
        public static Task CreateBattlefield5IconPackAsync(string displayName, IReadOnlyDictionary<string, StorageFile> selectedFiles, StorageFile headImageFile = null)
            => CreateGameIconPackAsync("battlefield5", "custom_battlefield5_icon_", displayName, selectedFiles, headImageFile);
        public static Task CreateBattlefield2042IconPackAsync(string displayName, IReadOnlyDictionary<string, StorageFile> selectedFiles, StorageFile headImageFile = null)
            => CreateGameIconPackAsync("battlefield2042", "custom_battlefield2042_icon_", displayName, selectedFiles, headImageFile);
        public static Task CreateDeltaForceIconPackAsync(string displayName, IReadOnlyDictionary<string, StorageFile> selectedFiles, StorageFile headImageFile = null)
            => CreateGameIconPackAsync("deltaforce", "custom_deltaforce_icon_", displayName, selectedFiles, headImageFile);

        private static async Task CreateGameIconPackAsync(string gameKey, string keyPrefix, string displayName, IReadOnlyDictionary<string, StorageFile> selectedFiles, StorageFile headImageFile)
        {
            if (selectedFiles == null || selectedFiles.Count == 0)
            {
                return;
            }

            StorageFolder root = await GetGameIconPacksFolderAsync(gameKey);
            StorageFolder packFolder = await root.CreateFolderAsync(
                SanitizeName(displayName), CreationCollisionOption.GenerateUniqueName);

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

            await WriteIconPackHeadImageAsync(packFolder, headImageFile);

            PackCatalog catalog = await LoadAsync();
            catalog.IconPacks.Add(new IconPackItem
            {
                Key = keyPrefix + Guid.NewGuid().ToString("N"),
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

        private static async Task ImportGameIconPackAsync(string keyPrefix, StorageFolder folder)
        {
            var catalog = await LoadAsync();
            catalog.IconPacks.Add(new IconPackItem
            {
                Key = keyPrefix + Guid.NewGuid().ToString("N"),
                DisplayName = folder.DisplayName,
                FolderPath = folder.Path,
                IsBuiltIn = false,
                IsVisibleInWidget = true,
                OwnsFolder = false
            });
            await SaveAsync(catalog);
        }

        // Writes the user-chosen head/cover image into the pack as pack_head.*.
        private static async Task WriteIconPackHeadImageAsync(StorageFolder packFolder, StorageFile headImageFile)
        {
            if (headImageFile == null)
            {
                return;
            }

            string extension = headImageFile.FileType;
            if (string.IsNullOrWhiteSpace(extension))
            {
                extension = ".png";
            }

            if (extension.Equals(".tga", StringComparison.OrdinalIgnoreCase))
            {
                await TgaDecoder.ConvertTgaToPngAsync(headImageFile, packFolder, "pack_head.png");
            }
            else
            {
                await headImageFile.CopyAsync(
                    packFolder,
                    "pack_head" + extension.ToLowerInvariant(),
                    NameCollisionOption.ReplaceExisting);
            }
        }
    }
}

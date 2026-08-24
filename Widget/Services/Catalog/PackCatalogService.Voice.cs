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
        public const string DagoujiaoAnimalsPackKey = "dagoujiao_animals";

        public static string GetVoicePackDisplayName(VoicePackItem item)
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

        public static bool IsImportedVoicePackKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return false;
            return key.StartsWith("custom_voice_", StringComparison.OrdinalIgnoreCase)
                || key.StartsWith("custom_csol_voice_", StringComparison.OrdinalIgnoreCase)
                || key.StartsWith("custom_dagoujiao_voice_", StringComparison.OrdinalIgnoreCase)
                || key.StartsWith("custom_doubao_voice_", StringComparison.OrdinalIgnoreCase)
                || key.StartsWith("custom_battlefield1_voice_", StringComparison.OrdinalIgnoreCase)
                || key.StartsWith("custom_battlefield5_voice_", StringComparison.OrdinalIgnoreCase)
                || key.StartsWith("custom_battlefield4_voice_", StringComparison.OrdinalIgnoreCase)
                || key.StartsWith("custom_battlefield2042_voice_", StringComparison.OrdinalIgnoreCase)
                || key.StartsWith("custom_deltaforce_voice_", StringComparison.OrdinalIgnoreCase)
                || key.StartsWith("custom_pubg_voice_", StringComparison.OrdinalIgnoreCase)
                || key.StartsWith("custom_valorant_voice_", StringComparison.OrdinalIgnoreCase)
                || key.StartsWith("custom_overwatch_voice_", StringComparison.OrdinalIgnoreCase)
                || key.StartsWith("custom_modernwarfare2019_voice_", StringComparison.OrdinalIgnoreCase)
                || key.StartsWith("custom_apex_voice_", StringComparison.OrdinalIgnoreCase);
        }

        // Event voice packs unify the old "event sound routing" concept into voice
        // packs: normal/headshot/knife/assist are just slots in a manifest pack.
        public static bool IsEventVoicePackKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return false;
            return key.StartsWith("custom_battlefield1_voice_", StringComparison.OrdinalIgnoreCase)
                || key.StartsWith("custom_battlefield5_voice_", StringComparison.OrdinalIgnoreCase)
                || key.StartsWith("custom_battlefield4_voice_", StringComparison.OrdinalIgnoreCase)
                || key.StartsWith("custom_battlefield2042_voice_", StringComparison.OrdinalIgnoreCase)
                || key.StartsWith("custom_deltaforce_voice_", StringComparison.OrdinalIgnoreCase)
                || key.StartsWith("custom_pubg_voice_", StringComparison.OrdinalIgnoreCase)
                || key.StartsWith("custom_apex_voice_", StringComparison.OrdinalIgnoreCase);
        }

        internal static (string GameKey, string KeyPrefix) GetEventVoicePackConfig(GameStyleMode style)
        {
            switch (style)
            {
                case GameStyleMode.Battlefield1:
                    return ("battlefield1", "custom_battlefield1_voice_");
                case GameStyleMode.Battlefield5:
                    return ("battlefield5", "custom_battlefield5_voice_");
                case GameStyleMode.Battlefield4:
                    return ("battlefield4", "custom_battlefield4_voice_");
                case GameStyleMode.Battlefield2042:
                    return ("battlefield2042", "custom_battlefield2042_voice_");
                case GameStyleMode.DeltaForce:
                    return ("deltaforce", "custom_deltaforce_voice_");
                case GameStyleMode.Pubg:
                    return ("pubg", "custom_pubg_voice_");
                case GameStyleMode.Apex:
                    return ("apex", "custom_apex_voice_");
                default:
                    return ("crossfire", "custom_voice_");
            }
        }

        public static bool IsCsolVoicePackKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return false;
            return key.StartsWith("custom_csol_voice_", StringComparison.OrdinalIgnoreCase)
                || key.StartsWith("csol", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsDagoujiaoVoicePackKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return false;
            return key.StartsWith("custom_dagoujiao_voice_", StringComparison.OrdinalIgnoreCase)
                || key.StartsWith("dagoujiao", StringComparison.OrdinalIgnoreCase);
        }

        public static async Task<IReadOnlyList<VoicePackItem>> GetVisibleVoicePacksAsync()
        {
            var catalog = await LoadAsync();
            IEnumerable<VoicePackItem> visible = catalog.VoicePacks
                .Where(p => p.IsVisibleInWidget && GameStyleService.IsVisibleForCurrentStyle(p.Key))
                .ToList();
            if (GameStyleService.Current == GameStyleMode.Valorant)
            {
                visible = visible.OrderBy(p => ValorantPackService.GetDisplayOrder(p.Key));
            }
            return visible.ToList();
        }

        public static async Task<IReadOnlyList<VoicePackItem>> GetAllVoicePacksAsync()
        {
            var catalog = await LoadAsync();
            return catalog.VoicePacks;
        }

        public static async Task<VoicePackItem> GetVoicePackAsync(string key)
        {
            var catalog = await LoadAsync();
            return catalog.VoicePacks.FirstOrDefault(p =>
                string.Equals(p.Key, key, StringComparison.OrdinalIgnoreCase));
        }

        public static async Task<StorageFolder> GetImportedVoiceFolderAsync(string key)
        {
            var item = await GetVoicePackAsync(key);
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

        public static async Task ImportVoicePackAsync(StorageFolder folder)
        {
            var catalog = await LoadAsync();
            catalog.VoicePacks.Add(new VoicePackItem
            {
                Key = "custom_voice_" + Guid.NewGuid().ToString("N"),
                DisplayName = folder.DisplayName,
                FolderPath = folder.Path,
                IsBuiltIn = false,
                IsVisibleInWidget = true,
                OwnsFolder = false
            });
            await SaveAsync(catalog);
        }

        public static async Task ImportCsolVoicePackAsync(StorageFolder folder)
        {
            var catalog = await LoadAsync();
            catalog.VoicePacks.Add(new VoicePackItem
            {
                Key = "custom_csol_voice_" + Guid.NewGuid().ToString("N"),
                DisplayName = folder.DisplayName,
                FolderPath = folder.Path,
                IsBuiltIn = false,
                IsVisibleInWidget = true,
                OwnsFolder = false
            });
            await SaveAsync(catalog);
        }

        public static async Task ImportDagoujiaoVoicePackAsync(StorageFolder folder)
        {
            var catalog = await LoadAsync();
            catalog.VoicePacks.Add(new VoicePackItem
            {
                Key = "custom_dagoujiao_voice_" + Guid.NewGuid().ToString("N"),
                DisplayName = folder.DisplayName,
                FolderPath = folder.Path,
                IsBuiltIn = false,
                IsVisibleInWidget = true,
                OwnsFolder = false
            });
            await SaveAsync(catalog);
        }

        public static bool IsDoubaoVoicePackKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return false;
            return key.StartsWith("custom_doubao_voice_", StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, "doubao", StringComparison.OrdinalIgnoreCase);
        }

        public static async Task ImportDoubaoVoicePackAsync(StorageFolder folder)
        {
            var catalog = await LoadAsync();
            catalog.VoicePacks.Add(new VoicePackItem
            {
                Key = "custom_doubao_voice_" + Guid.NewGuid().ToString("N"),
                DisplayName = folder.DisplayName,
                FolderPath = folder.Path,
                IsBuiltIn = false,
                IsVisibleInWidget = true,
                OwnsFolder = false
            });
            await SaveAsync(catalog);
        }

        public static async Task SetVoicePackVisibilityAsync(string key, bool isVisible)
        {
            var catalog = await LoadAsync();
            var item = catalog.VoicePacks.FirstOrDefault(p => p.Key == key);
            if (item != null)
            {
                item.IsVisibleInWidget = isVisible;
                SaveVisibilityOverride("voice", item.Key, isVisible);
                await SaveAsync(catalog);
            }
        }

        public static async Task RemoveCustomVoicePackAsync(string key)
        {
            var catalog = await LoadAsync();
            var item = catalog.VoicePacks.FirstOrDefault(p => p.Key == key);
            if (item != null && !item.IsBuiltIn)
            {
                catalog.VoicePacks.Remove(item);
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

        public static async Task CreateVoicePackAsync(string displayName, VoicePackBuildOptions options)
        {
            StorageFolder root = await GetGameVoicePacksFolderAsync("crossfire");
            StorageFolder packFolder = await root.CreateFolderAsync(
                SanitizeName(displayName),
                CreationCollisionOption.GenerateUniqueName);

            await CopySelectedVoiceFilesAsync(packFolder, options);

            if (options.HeadImageFile != null)
            {
                string extension = options.HeadImageFile.FileType;
                if (string.IsNullOrWhiteSpace(extension))
                {
                    extension = ".png";
                }

                if (extension.Equals(".tga", StringComparison.OrdinalIgnoreCase))
                {
                    await TgaDecoder.ConvertTgaToPngAsync(options.HeadImageFile, packFolder, "pack_head.png");
                }
                else
                {
                    await options.HeadImageFile.CopyAsync(
                        packFolder,
                        "pack_head" + extension.ToLowerInvariant(),
                        NameCollisionOption.ReplaceExisting);
                }
            }

            if (options.CommonOverlayFile != null)
            {
                await options.CommonOverlayFile.CopyAsync(
                    packFolder,
                    GetAudioTargetFileName("common_overlay.wav", options.CommonOverlayFile),
                    NameCollisionOption.ReplaceExisting);
            }
            else if (options.UseBuiltInDefaultCommonOverlay)
            {
                StorageFile builtInCommon = await StorageFile.GetFileFromApplicationUriAsync(
                    new Uri("ms-appx:///KillConfirmService/sounds/crossfire_swat_gr/common.wav"));
                await builtInCommon.CopyAsync(
                    packFolder,
                    "common_overlay.wav",
                    NameCollisionOption.ReplaceExisting);
            }

            await WriteGeneratedVoiceManifestAsync(
                packFolder,
                displayName,
                "crossfire",
                CrossfireSlotMapping,
                options.CommonOverlayEnabled);

            var catalog = await LoadAsync();
            catalog.VoicePacks.Add(new VoicePackItem
            {
                Key = "custom_voice_" + Guid.NewGuid().ToString("N"),
                DisplayName = displayName,
                FolderPath = packFolder.Path,
                IsBuiltIn = false,
                IsVisibleInWidget = true,
                OwnsFolder = true
            });
            await SaveAsync(catalog);
        }

        public static async Task CreateCsolVoicePackAsync(string displayName, VoicePackBuildOptions options)
        {
            StorageFolder root = await GetGameVoicePacksFolderAsync("csol");
            StorageFolder packFolder = await root.CreateFolderAsync(
                SanitizeName(displayName),
                CreationCollisionOption.GenerateUniqueName);

            await CopySelectedVoiceFilesAsync(packFolder, options);

            if (options.HeadImageFile != null)
            {
                string extension = options.HeadImageFile.FileType;
                if (string.IsNullOrWhiteSpace(extension))
                {
                    extension = ".png";
                }

                if (extension.Equals(".tga", StringComparison.OrdinalIgnoreCase))
                {
                    await TgaDecoder.ConvertTgaToPngAsync(options.HeadImageFile, packFolder, "pack_head.png");
                }
                else
                {
                    await options.HeadImageFile.CopyAsync(
                        packFolder,
                        "pack_head" + extension.ToLowerInvariant(),
                        NameCollisionOption.ReplaceExisting);
                }
            }

            await WriteGeneratedVoiceManifestAsync(
                packFolder,
                displayName,
                "csol",
                CsolSlotMapping,
                commonOverlayEnabled: null);

            var catalog = await LoadAsync();
            catalog.VoicePacks.Add(new VoicePackItem
            {
                Key = "custom_csol_voice_" + Guid.NewGuid().ToString("N"),
                DisplayName = displayName,
                FolderPath = packFolder.Path,
                IsBuiltIn = false,
                IsVisibleInWidget = true,
                OwnsFolder = true
            });
            await SaveAsync(catalog);
        }

        public static async Task CreateDagoujiaoVoicePackAsync(string displayName, VoicePackBuildOptions options)
        {
            StorageFolder root = await GetGameVoicePacksFolderAsync("dagoujiao");
            StorageFolder packFolder = await root.CreateFolderAsync(
                SanitizeName(displayName),
                CreationCollisionOption.GenerateUniqueName);

            await CopySelectedVoiceFilesAsync(packFolder, options);

            if (options.HeadImageFile != null)
            {
                string extension = options.HeadImageFile.FileType;
                if (string.IsNullOrWhiteSpace(extension))
                {
                    extension = ".png";
                }

                if (extension.Equals(".tga", StringComparison.OrdinalIgnoreCase))
                {
                    await TgaDecoder.ConvertTgaToPngAsync(options.HeadImageFile, packFolder, "pack_head.png");
                }
                else
                {
                    await options.HeadImageFile.CopyAsync(
                        packFolder,
                        "pack_head" + extension.ToLowerInvariant(),
                        NameCollisionOption.ReplaceExisting);
                }
            }

            await WriteGeneratedVoiceManifestAsync(
                packFolder,
                displayName,
                "dagoujiao",
                DagoujiaoSlotMapping,
                commonOverlayEnabled: null);

            var catalog = await LoadAsync();
            catalog.VoicePacks.Add(new VoicePackItem
            {
                Key = "custom_dagoujiao_voice_" + Guid.NewGuid().ToString("N"),
                DisplayName = displayName,
                FolderPath = packFolder.Path,
                IsBuiltIn = false,
                IsVisibleInWidget = true,
                OwnsFolder = true
            });
            await SaveAsync(catalog);
        }

        public static async Task CreateDoubaoVoicePackAsync(string displayName, VoicePackBuildOptions options)
        {
            StorageFolder root = await GetGameVoicePacksFolderAsync("doubao");
            StorageFolder packFolder = await root.CreateFolderAsync(
                SanitizeName(displayName),
                CreationCollisionOption.GenerateUniqueName);

            await CopySelectedVoiceFilesAsync(packFolder, options);

            if (options.HeadImageFile != null)
            {
                string extension = options.HeadImageFile.FileType;
                if (string.IsNullOrWhiteSpace(extension))
                {
                    extension = ".png";
                }

                if (extension.Equals(".tga", StringComparison.OrdinalIgnoreCase))
                {
                    await TgaDecoder.ConvertTgaToPngAsync(options.HeadImageFile, packFolder, "pack_head.png");
                }
                else
                {
                    await options.HeadImageFile.CopyAsync(
                        packFolder,
                        "pack_head" + extension.ToLowerInvariant(),
                        NameCollisionOption.ReplaceExisting);
                }
            }

            await WriteGeneratedVoiceManifestAsync(
                packFolder,
                displayName,
                "doubao",
                DoubaoSlotMapping,
                commonOverlayEnabled: null);

            var catalog = await LoadAsync();
            catalog.VoicePacks.Add(new VoicePackItem
            {
                Key = "custom_doubao_voice_" + Guid.NewGuid().ToString("N"),
                DisplayName = displayName,
                FolderPath = packFolder.Path,
                IsBuiltIn = false,
                IsVisibleInWidget = true,
                OwnsFolder = true
            });
            await SaveAsync(catalog);
        }

        // Event voice pack creation for 战地1/4/5/2042/三角洲. Same shape as the
        // other per-game creators: copy selected audio, write a manifest with the
        // game's game_style + EventSlotMapping, register a per-game prefixed key.
    }
}

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
                || key.StartsWith("custom_dagoujiao_voice_", StringComparison.OrdinalIgnoreCase);
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
                || string.Equals(key, "dagoujiao", StringComparison.OrdinalIgnoreCase);
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
            StorageFolder root = await GetOrCreatePackRootAsync("GeneratedVoicePacks");
            StorageFolder packFolder = await root.CreateFolderAsync(
                SanitizeName(displayName),
                CreationCollisionOption.GenerateUniqueName);

            foreach (var pair in options.SelectedFiles)
            {
                if (pair.Value != null)
                {
                    await pair.Value.CopyAsync(
                        packFolder,
                        GetAudioTargetFileName(pair.Key, pair.Value),
                        NameCollisionOption.ReplaceExisting);
                }
            }

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
            StorageFolder root = await GetOrCreatePackRootAsync("GeneratedCsolVoicePacks");
            StorageFolder packFolder = await root.CreateFolderAsync(
                SanitizeName(displayName),
                CreationCollisionOption.GenerateUniqueName);

            foreach (var pair in options.SelectedFiles)
            {
                if (pair.Value != null)
                {
                    await pair.Value.CopyAsync(
                        packFolder,
                        GetAudioTargetFileName(pair.Key, pair.Value),
                        NameCollisionOption.ReplaceExisting);
                }
            }

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
            StorageFolder root = await GetOrCreatePackRootAsync("GeneratedDagoujiaoVoicePacks");
            StorageFolder packFolder = await root.CreateFolderAsync(
                SanitizeName(displayName),
                CreationCollisionOption.GenerateUniqueName);

            foreach (var pair in options.SelectedFiles)
            {
                if (pair.Value != null)
                {
                    await pair.Value.CopyAsync(
                        packFolder,
                        GetAudioTargetFileName(pair.Key, pair.Value),
                        NameCollisionOption.ReplaceExisting);
                }
            }

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

        public static readonly IReadOnlyDictionary<string, string> DagoujiaoSlotMapping =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "common", "common" },
                { "headshot", "headshot" },
                { "epic", "epic" },
                { "jiaojiaojiao", "jiaojiaojiao" }
            };

        // Slot mapping used when writing manifests for CF voice packs.
        // Maps source-stem -> manifest-slot key.
        public static readonly IReadOnlyDictionary<string, string> CrossfireSlotMapping =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "common", "kill_1" },
                { "2", "kill_2" },
                { "3", "kill_3" },
                { "4", "kill_4" },
                { "5", "kill_5" },
                { "6", "kill_6" },
                { "7", "kill_7" },
                { "8", "kill_8" },
                { "headshot", "headshot" },
                { "knife", "knife" },
                { "firstandlast", "first_and_last" }
            };

        // Slot mapping for CSOL voice packs. The CSOL 10-kill voice pack uses
        // the same kill_<n> / headshot / knife / first_and_last keys as CF; only
        // the source file names differ (1.wav..10.wav vs common.wav, 2.wav...).
        public static readonly IReadOnlyDictionary<string, string> CsolSlotMapping =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "1", "kill_1" },
                { "2", "kill_2" },
                { "3", "kill_3" },
                { "4", "kill_4" },
                { "5", "kill_5" },
                { "6", "kill_6" },
                { "7", "kill_7" },
                { "8", "kill_8" },
                { "9", "kill_9" },
                { "10", "kill_10" },
                { "headshot", "headshot" },
                { "knife", "knife" },
                { "first", "first_and_last" },
                { "last", "first_and_last" },
                { "assist", "first_and_last" }
            };

        private static async Task WriteGeneratedVoiceManifestAsync(
            StorageFolder packFolder,
            string displayName,
            string gameStyle,
            IReadOnlyDictionary<string, string> slotMapping,
            IReadOnlyDictionary<string, bool> commonOverlayEnabled)
        {
            var slotsObj = new Windows.Data.Json.JsonObject();
            foreach (var pair in slotMapping)
            {
                string fileName = await FindAudioFileNameAsync(packFolder, pair.Key);
                if (!string.IsNullOrWhiteSpace(fileName))
                {
                    slotsObj[pair.Value] = Windows.Data.Json.JsonValue.CreateStringValue(fileName);
                }
            }

            if (string.Equals(gameStyle, "crossfire", StringComparison.OrdinalIgnoreCase))
            {
                string commonOverlayFile = await FindAudioFileNameAsync(packFolder, "common_overlay");
                if (!string.IsNullOrWhiteSpace(commonOverlayFile))
                {
                    slotsObj["common_overlay"] = Windows.Data.Json.JsonValue.CreateStringValue(commonOverlayFile);
                }
            }

            var overlaySlotsArray = new Windows.Data.Json.JsonArray();
            if (string.Equals(gameStyle, "crossfire", StringComparison.OrdinalIgnoreCase)
                && commonOverlayEnabled != null)
            {
                foreach (var pair in commonOverlayEnabled)
                {
                    if (pair.Value)
                    {
                        string stem = Path.GetFileNameWithoutExtension(pair.Key).ToLowerInvariant();
                        if (slotMapping.TryGetValue(stem, out string manifestSlot))
                        {
                            overlaySlotsArray.Add(Windows.Data.Json.JsonValue.CreateStringValue(manifestSlot));
                        }
                    }
                }
            }

            var audioObj = new Windows.Data.Json.JsonObject
            {
                ["base_gain"] = Windows.Data.Json.JsonValue.CreateNumberValue(1.0),
                ["slots"] = slotsObj,
                ["slot_gains"] = new Windows.Data.Json.JsonObject(),
                ["overlay_slots"] = overlaySlotsArray
            };

            var manifestObj = new Windows.Data.Json.JsonObject
            {
                ["id"] = Windows.Data.Json.JsonValue.CreateStringValue(packFolder.Name),
                ["name"] = Windows.Data.Json.JsonValue.CreateStringValue(displayName),
                ["game_style"] = Windows.Data.Json.JsonValue.CreateStringValue(gameStyle),
                ["version"] = Windows.Data.Json.JsonValue.CreateStringValue("1.0"),
                ["audio"] = audioObj
            };

            StorageFile manifestFile = await packFolder.CreateFileAsync("manifest.json", CreationCollisionOption.ReplaceExisting);
            await FileIO.WriteTextAsync(manifestFile, manifestObj.Stringify());
        }

        private static string GetAudioTargetFileName(string canonicalFileName, StorageFile sourceFile)
        {
            string baseName = Path.GetFileNameWithoutExtension(canonicalFileName);
            string extension = sourceFile?.FileType;
            if (string.IsNullOrWhiteSpace(extension)
                || !SupportedAudioExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            {
                extension = ".wav";
            }

            return baseName + extension.ToLowerInvariant();
        }

        private static async Task<string> FindAudioFileNameAsync(StorageFolder folder, string baseName)
        {
            foreach (string extension in SupportedAudioExtensions)
            {
                string fileName = baseName + extension;
                try
                {
                    await folder.GetFileAsync(fileName);
                    return fileName;
                }
                catch
                {
                }
            }

            return null;
        }
    }
}

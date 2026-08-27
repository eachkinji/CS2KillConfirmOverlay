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
        internal static async Task CreateEventVoicePackAsync(GameStyleMode style, string displayName, VoicePackBuildOptions options)
        {
            var (gameKey, keyPrefix) = GetEventVoicePackConfig(style);
            StorageFolder root = await GetGameVoicePacksFolderAsync(gameKey);
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
                gameKey,
                EventSlotMapping,
                commonOverlayEnabled: null);

            var catalog = await LoadAsync();
            catalog.VoicePacks.Add(new VoicePackItem
            {
                Key = keyPrefix + Guid.NewGuid().ToString("N"),
                DisplayName = displayName,
                FolderPath = packFolder.Path,
                IsBuiltIn = false,
                IsVisibleInWidget = true,
                OwnsFolder = true
            });
            await SaveAsync(catalog);
        }

        // Valorant voice packs. Valorant's built-in voice plays tier 1-5 streak
        // voices and a headshot voice (its sound.lua caps kills at 5), so a custom
        // Valorant pack exposes those same six slots on the manifest's generic
        // kill_1..kill_5 / headshot keys.
        public static readonly IReadOnlyDictionary<string, string> ValorantVoiceSlotMapping =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "1", "kill_1" },
                { "2", "kill_2" },
                { "3", "kill_3" },
                { "4", "kill_4" },
                { "5", "kill_5" },
                { "headshot", "headshot" }
            };

        public static async Task CreateValorantVoicePackAsync(string displayName, VoicePackBuildOptions options)
        {
            StorageFolder root = await GetGameVoicePacksFolderAsync("valorant");
            StorageFolder packFolder = await root.CreateFolderAsync(
                SanitizeName(displayName),
                CreationCollisionOption.GenerateUniqueName);

            await CopySelectedVoiceFilesAsync(packFolder, options);

            // A custom pack is self-contained: omitted slots retain the default
            // Valorant cue, including when this pack is exported or imported.
            foreach (string stem in ValorantVoiceSlotMapping.Keys)
            {
                if ((await FindAudioFileNamesAsync(packFolder, stem)).Count > 0)
                {
                    continue;
                }

                string fileName = stem + ".wav";
                StorageFile builtIn = await StorageFile.GetFileFromApplicationUriAsync(
                    new Uri("ms-appx:///KillConfirmService/sounds/"
                        + ValorantPackService.DefaultKey + "/" + fileName));
                await builtIn.CopyAsync(packFolder, fileName, NameCollisionOption.ReplaceExisting);
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
                "valorant",
                ValorantVoiceSlotMapping,
                commonOverlayEnabled: null);

            var catalog = await LoadAsync();
            catalog.VoicePacks.Add(new VoicePackItem
            {
                Key = "custom_valorant_voice_" + Guid.NewGuid().ToString("N"),
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

        // Dumboa packs carry 5 independent per-kill voices (1kill.wav..5kill.wav) that
        // map straight onto the manifest's kill_<n> slots, so the standard
        // resolve_audio streak path handles them without a service-side special case.
        public static readonly IReadOnlyDictionary<string, string> DoubaoSlotMapping =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "1kill", "kill_1" },
                { "2kill", "kill_2" },
                { "3kill", "kill_3" },
                { "4kill", "kill_4" },
                { "5kill", "kill_5" }
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
                { "firstandlast", "first_and_last" },
                { "grenade", "grenade" },
                { "bomb_plant", "bomb_plant" },
                { "bomb_defuse", "bomb_defuse" }
            };

        // Slot mapping for CSOL voice packs. The CSOL 10-kill voice pack uses
        // the same kill_<n> / headshot / knife / first_and_last keys as CF; the
        // source file names and the last/assist handling follow CSOL's actual
        // asset layout: there is no dedicated first-kill voice (first kill plays
        // the normal streak voice via the kill_1-cap logic); the last kill uses
        // the revenge voice loaded into first_and_last; the assist uses the
        // dedicated assist slot.
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
                { "revenge", "first_and_last" },
                { "assist", "assist" },
                { "grenade", "grenade" },
                { "bomb_plant", "bomb_plant" },
                { "bomb_defuse", "bomb_defuse" }
            };

        // Storage mapping shared by event-style packs. Each game's editor exposes
        // only the subset that game actually supports.
        public static readonly IReadOnlyDictionary<string, string> EventSlotMapping =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "normal", "kill_1" },
                { "headshot", "headshot" },
                { "knife", "knife" },
                { "assist", "assist" },
                { "bomb_plant", "bomb_plant" },
                { "bomb_defuse", "bomb_defuse" }
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
                IReadOnlyList<string> fileNames = await FindAudioFileNamesAsync(packFolder, pair.Key);
                if (fileNames.Count == 1)
                {
                    slotsObj[pair.Value] = Windows.Data.Json.JsonValue.CreateStringValue(fileNames[0]);
                }
                else if (fileNames.Count > 1)
                {
                    var variants = new Windows.Data.Json.JsonArray();
                    foreach (string fileName in fileNames)
                    {
                        variants.Add(Windows.Data.Json.JsonValue.CreateStringValue(fileName));
                    }
                    slotsObj[pair.Value] = variants;
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

        private static async Task CopySelectedVoiceFilesAsync(StorageFolder packFolder, VoicePackBuildOptions options)
        {
            if (options?.SelectedFileGroups != null)
            {
                foreach (var pair in options.SelectedFileGroups)
                {
                    IReadOnlyList<StorageFile> files = pair.Value ?? Array.Empty<StorageFile>();
                    int index = 0;
                    foreach (StorageFile file in files.Where(file => file != null))
                    {
                        string canonicalName = GetAudioTargetFileName(pair.Key, file);
                        string extension = Path.GetExtension(canonicalName);
                        string stem = Path.GetFileNameWithoutExtension(canonicalName);
                        string targetName = index == 0
                            ? canonicalName
                            : stem + "__" + (index + 1) + extension;
                        await file.CopyAsync(packFolder, targetName, NameCollisionOption.ReplaceExisting);
                        index++;
                    }
                }
                return;
            }

            if (options?.SelectedFiles == null)
            {
                return;
            }
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
        }

        private static async Task<IReadOnlyList<string>> FindAudioFileNamesAsync(StorageFolder folder, string baseName)
        {
            try
            {
                IReadOnlyList<StorageFile> files = await folder.GetFilesAsync();
                return files
                    .Where(file => SupportedAudioExtensions.Contains(file.FileType, StringComparer.OrdinalIgnoreCase))
                    .Where(file =>
                    {
                        string stem = Path.GetFileNameWithoutExtension(file.Name);
                        return string.Equals(stem, baseName, StringComparison.OrdinalIgnoreCase)
                            || stem.StartsWith(baseName + "__", StringComparison.OrdinalIgnoreCase);
                    })
                    .OrderBy(file => string.Equals(
                        Path.GetFileNameWithoutExtension(file.Name),
                        baseName,
                        StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                    .ThenBy(file => file.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(file => file.Name)
                    .ToList();
            }
            catch
            {
                return Array.Empty<string>();
            }
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

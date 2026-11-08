// Shared by Widget (UWP) and CompatibilityHost; extracted from both former copies.
// Per-host lines stay inside #if WINDOWS_UWP blocks. See Tools/SharedSource/extract_shared.py.
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
#if WINDOWS_UWP
using KillConfirmGameBar.Helpers;
#else
using KillConfirmCompatibility.Helpers;
#endif
using Windows.Storage;

#if WINDOWS_UWP
namespace KillConfirmGameBar.Services
#else
namespace KillConfirmCompatibility.Services
#endif
{
    public static partial class PackCatalogService
    {
        public static readonly IReadOnlyDictionary<string, string> OverwatchVoiceSlotMapping =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "kill", "kill_1" }
            };

        public static bool IsOverwatchVoicePackKey(string key)
        {
            return !string.IsNullOrWhiteSpace(key)
                && (string.Equals(key, "overwatch", StringComparison.OrdinalIgnoreCase)
                    || key.StartsWith("custom_overwatch_voice_", StringComparison.OrdinalIgnoreCase));
        }

        public static async Task CreateOverwatchVoicePackAsync(
            string displayName,
            VoicePackBuildOptions options)
        {
            StorageFolder root = await GetGameVoicePacksFolderAsync("overwatch");
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
                    await TgaDecoder.ConvertTgaToPngAsync(
                        options.HeadImageFile,
                        packFolder,
                        "pack_head.png");
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
                "overwatch",
                OverwatchVoiceSlotMapping,
                commonOverlayEnabled: null);

            var catalog = await LoadAsync();
            catalog.VoicePacks.Add(new VoicePackItem
            {
                Key = "custom_overwatch_voice_" + Guid.NewGuid().ToString("N"),
                DisplayName = displayName,
                FolderPath = packFolder.Path,
                IsBuiltIn = false,
                IsVisibleInWidget = true,
                OwnsFolder = true
            });
            await SaveAsync(catalog);
        }
    }
}

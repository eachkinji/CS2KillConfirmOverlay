using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KillConfirmGameBar.Helpers;
using Windows.Storage;

namespace KillConfirmGameBar.Services
{
    public static partial class PackCatalogService
    {
        public static readonly IReadOnlyDictionary<string, string> ModernWarfare2019VoiceSlotMapping =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "kill", "kill_1" },
                { "headshot", "headshot" },
                { "bomb_plant", "bomb_plant" },
                { "bomb_defuse", "bomb_defuse" }
            };

        public static async Task CreateModernWarfare2019VoicePackAsync(
            string displayName,
            VoicePackBuildOptions options)
        {
            StorageFolder root = await GetGameVoicePacksFolderAsync("modernwarfare2019");
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
                "modernwarfare2019",
                ModernWarfare2019VoiceSlotMapping,
                commonOverlayEnabled: null);

            var catalog = await LoadAsync();
            catalog.VoicePacks.Add(new VoicePackItem
            {
                Key = "custom_modernwarfare2019_voice_" + Guid.NewGuid().ToString("N"),
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

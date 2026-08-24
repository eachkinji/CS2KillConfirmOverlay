using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Threading.Tasks;
using Windows.Storage;

namespace KillConfirmGameBar.Services
{
    public static partial class PackCatalogService
    {
        public static async Task<StorageFolder> GetGameRootFolderAsync(string gameKey)
        {
            string key = string.IsNullOrWhiteSpace(gameKey) ? "crossfire" : gameKey.Trim().ToLowerInvariant();
            StorageFolder packsRoot = await ApplicationData.Current.LocalFolder.CreateFolderAsync("Packs", CreationCollisionOption.OpenIfExists);
            return await packsRoot.CreateFolderAsync(key, CreationCollisionOption.OpenIfExists);
        }

        public static async Task<StorageFolder> GetGameVoicePacksFolderAsync(string gameKey)
        {
            StorageFolder gameRoot = await GetGameRootFolderAsync(gameKey);
            return await gameRoot.CreateFolderAsync("voice_packs", CreationCollisionOption.OpenIfExists);
        }

        public static async Task<StorageFolder> GetGameIconPacksFolderAsync(string gameKey)
        {
            StorageFolder gameRoot = await GetGameRootFolderAsync(gameKey);
            return await gameRoot.CreateFolderAsync("icon_packs", CreationCollisionOption.OpenIfExists);
        }

        public static async Task<StorageFolder> GetGameStagingFolderAsync(string gameKey, bool isAudio)
        {
            StorageFolder gameRoot = await GetGameRootFolderAsync(gameKey);
            StorageFolder stagingRoot = await gameRoot.CreateFolderAsync("staging", CreationCollisionOption.OpenIfExists);
            return await stagingRoot.CreateFolderAsync(isAudio ? "audio" : "icons", CreationCollisionOption.OpenIfExists);
        }

        internal static async Task<int> ImportStagedMaterialsAsync(GameStyleMode game, bool isAudio, IReadOnlyList<StorageFile> files)
        {
            if (files == null || files.Count == 0)
            {
                return 0;
            }

            string gameKey = GameStyleService.ToStorageValue(game);
            StorageFolder stagingFolder = await GetGameStagingFolderAsync(gameKey, isAudio);
            int count = 0;
            foreach (StorageFile file in files)
            {
                if (file == null) continue;
                try
                {
                    await file.CopyAsync(stagingFolder, file.Name, NameCollisionOption.ReplaceExisting);
                    count++;
                }
                catch { }
            }
            return count;
        }

        public static async Task<IReadOnlyList<StorageFile>> GetStagedMaterialsAsync(string gameKey, bool isAudio)
        {
            try
            {
                StorageFolder stagingFolder = await GetGameStagingFolderAsync(gameKey, isAudio);
                return await stagingFolder.GetFilesAsync();
            }
            catch
            {
                return Array.Empty<StorageFile>();
            }
        }

        private static async Task<StorageFolder> GetOrCreatePackRootAsync(string folderName)
        {
            return await ApplicationData.Current.LocalFolder.CreateFolderAsync(folderName, CreationCollisionOption.OpenIfExists);
        }

        private static string SanitizeName(string displayName)
        {
            string value = string.IsNullOrWhiteSpace(displayName) ? "NewPack" : displayName.Trim();
            foreach (char ch in Path.GetInvalidFileNameChars())
            {
                value = value.Replace(ch, '_');
            }

            return string.IsNullOrWhiteSpace(value) ? "NewPack" : value;
        }

        private const string DoubaoLegacyMigratedKey = "Doubao.LegacyMigrated";
        private static bool _legacyMigrationTriggered;

        public static async Task MigrateLegacyDoubaoSettingsAsync()
        {
            ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
            if (localSettings.Values.TryGetValue(DoubaoLegacyMigratedKey, out object migrated) && migrated is bool b && b)
            {
                return;
            }

            localSettings.Values[DoubaoLegacyMigratedKey] = true;

            try
            {
                DoubaoSettingsValues settings = DoubaoSettingsStore.Load();
                bool hasCustomImage = false;
                bool hasCustomAudio = false;

                for (int i = 1; i <= 5; i++)
                {
                    if (settings.KillImageKeys.TryGetValue(i, out string imgKey)
                        && !string.IsNullOrWhiteSpace(imgKey)
                        && !imgKey.StartsWith("builtin:", StringComparison.OrdinalIgnoreCase))
                    {
                        hasCustomImage = true;
                    }

                    if (settings.KillAudioKeys.TryGetValue(i, out string audKey)
                        && !string.IsNullOrWhiteSpace(audKey)
                        && !audKey.StartsWith("builtin:", StringComparison.OrdinalIgnoreCase))
                    {
                        hasCustomAudio = true;
                    }
                }

                if (hasCustomImage)
                {
                    var imageFiles = new Dictionary<string, StorageFile>(StringComparer.OrdinalIgnoreCase);
                    for (int i = 1; i <= 5; i++)
                    {
                        string slotName = $"{i}kill.png";
                        string imgKey = settings.KillImageKeys.TryGetValue(i, out string k) ? k : null;
                        StorageFile file = null;
                        if (!string.IsNullOrWhiteSpace(imgKey) && !imgKey.StartsWith("builtin:", StringComparison.OrdinalIgnoreCase))
                        {
                            file = await DoubaoSettingsStore.GetImportedImageFileAsync(imgKey);
                        }

                        if (file == null)
                        {
                            try
                            {
                                file = await StorageFile.GetFileFromApplicationUriAsync(
                                    new Uri($"ms-appx:///Assets/GameStyles/doubao/killconfirm/textures/{slotName}"));
                            }
                            catch { }
                        }

                        if (file != null)
                        {
                            imageFiles[slotName] = file;
                        }
                    }

                    if (imageFiles.Count > 0)
                    {
                        await CreateDoubaoIconPackAsync("豆包旧数据", imageFiles);
                        PackCatalog cat = await LoadAsync();
                        IconPackItem pack = cat.IconPacks.LastOrDefault(p => p.DisplayName == "豆包旧数据" && p.Key.StartsWith("custom_doubao_icon_"));
                        if (pack != null)
                        {
                            localSettings.Values["KillIconPack.doubao"] = pack.Key;
                            localSettings.Values["KillIconPack"] = pack.Key;
                        }
                    }
                }

                if (hasCustomAudio)
                {
                    var audioFiles = new Dictionary<string, StorageFile>(StringComparer.OrdinalIgnoreCase);
                    for (int i = 1; i <= 5; i++)
                    {
                        string slotName = $"{i}kill.wav";
                        string audKey = settings.KillAudioKeys.TryGetValue(i, out string k) ? k : null;
                        StorageFile file = null;
                        if (!string.IsNullOrWhiteSpace(audKey) && !audKey.StartsWith("builtin:", StringComparison.OrdinalIgnoreCase))
                        {
                            string absPath = await DoubaoSettingsStore.ResolveAudioAbsolutePathAsync(audKey);
                            if (!string.IsNullOrWhiteSpace(absPath))
                            {
                                try
                                {
                                    file = await StorageFile.GetFileFromPathAsync(absPath);
                                }
                                catch { }
                            }
                        }

                        if (file == null)
                        {
                            try
                            {
                                file = await StorageFile.GetFileFromApplicationUriAsync(
                                    new Uri($"ms-appx:///KillConfirmService/sounds/doubao/{slotName}"));
                            }
                            catch { }
                        }

                        if (file != null)
                        {
                            audioFiles[slotName] = file;
                        }
                    }

                    if (audioFiles.Count > 0)
                    {
                        await CreateDoubaoVoicePackAsync("豆包旧数据", new VoicePackBuildOptions
                        {
                            SelectedFiles = audioFiles
                        });
                        PackCatalog cat = await LoadAsync();
                        VoicePackItem pack = cat.VoicePacks.LastOrDefault(p => p.DisplayName == "豆包旧数据" && p.Key.StartsWith("custom_doubao_voice_"));
                        if (pack != null)
                        {
                            localSettings.Values["VoicePack.doubao"] = pack.Key;
                            localSettings.Values["VoicePack"] = pack.Key;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                App.Log("MigrateLegacyDoubaoSettingsAsync failed: " + ex);
            }
        }
    }
}

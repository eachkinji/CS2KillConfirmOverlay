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
        private static async Task<PackCatalog> LoadAsync()
        {
            if (_cache != null)
            {
                return _cache;
            }

            await CatalogIoLock.WaitAsync();
            try
            {
                if (_cache != null)
                {
                    return _cache;
                }

                bool mustSave = false;
                StorageFolder localFolder = ApplicationData.Current.LocalFolder;
                try
                {
                    StorageFile file = await localFolder.GetFileAsync(CatalogFileName);
                    using (var stream = await file.OpenStreamForReadAsync())
                    {
                        var serializer = new DataContractJsonSerializer(typeof(PackCatalog));
                        _cache = (PackCatalog)serializer.ReadObject(stream);
                    }
                }
                catch
                {
                    _cache = CreateDefaultCatalog();
                    mustSave = true;
                }

                MergeMissingBuiltIns(_cache);
                mustSave |= RemoveRetiredBuiltIns(_cache);
                mustSave |= ApplyBuiltInVisibilityDefaultsIfNeeded(_cache);
                ApplyVisibilityOverrides(_cache);
                EnsureAtLeastOneVisibleVoice(_cache);
                EnsureAtLeastOneVisibleIcon(_cache);
                if (mustSave)
                {
                    await SaveCoreAsync(_cache, notify: false);
                }

                if (!_legacyMigrationTriggered)
                {
                    _legacyMigrationTriggered = true;
                    _ = Task.Run(async () => await MigrateLegacyDoubaoSettingsAsync());
                }

                return _cache;
            }
            finally
            {
                CatalogIoLock.Release();
            }
        }

        private static async Task SaveAsync(PackCatalog catalog)
        {
            await CatalogIoLock.WaitAsync();
            try
            {
                _cache = catalog;
                await SaveCoreAsync(catalog, notify: true);
            }
            finally
            {
                CatalogIoLock.Release();
            }
        }

        private static async Task SaveCoreAsync(PackCatalog catalog, bool notify)
        {
            try
            {
                StorageFolder localFolder = ApplicationData.Current.LocalFolder;
                StorageFile file = await localFolder.CreateFileAsync(
                    CatalogFileName,
                    CreationCollisionOption.ReplaceExisting);
                using (var stream = await file.OpenStreamForWriteAsync())
                {
                    var serializer = new DataContractJsonSerializer(typeof(PackCatalog));
                    serializer.WriteObject(stream, catalog);
                    await stream.FlushAsync();
                }

                if (notify)
                {
                    CatalogChanged?.Invoke(null, EventArgs.Empty);
                }
            }
            catch (Exception ex)
            {
                App.Log("Pack catalog save failed: " + ex);
            }
        }

        private static PackCatalog CreateDefaultCatalog()
        {
            var catalog = new PackCatalog
            {
                VoicePacks = new List<VoicePackItem>
                {
                    CreateBuiltInVoice("crossfire_swat_gr", "swat GR", true),
                    CreateBuiltInVoice("crossfire_swat_bl", "swat BL", true),
                    CreateBuiltInVoice("crossfire_flying_tiger_gr", "tiger GR", true),
                    CreateBuiltInVoice("crossfire_flying_tiger_bl", "tiger BL", true),
                    CreateBuiltInVoice("crossfire_v_sex", "American Girl", true),
                    CreateBuiltInVoice("crossfire_women_gr", "women GR", true),
                    CreateBuiltInVoice("crossfire_women_bl", "women BL", true),
                    CreateBuiltInVoice("crossfire_bunny_gr", "Bunny GR", true),
                    CreateBuiltInVoice("crossfire_bunny_bl", "Bunny BL", true),
                    CreateBuiltInVoice("crossfire_heart_judge_gr", "Heart Judge GR", true),
                    CreateBuiltInVoice("crossfire_heart_judge_bl", "Heart Judge BL", true),
                    CreateBuiltInVoice("csol4", "CSOL 10杀", true),
                    CreateBuiltInVoice("bf1", "Battlefield 1", true),
                    CreateBuiltInVoice("bf5", "Battlefield 5", true),
                    CreateBuiltInVoice("bf4", "Battlefield 4", true),
                    CreateBuiltInVoice("battlefield2042", "Battlefield 2042", true),
                    CreateBuiltInVoice("pubg", "PUBG", true),
                    CreateBuiltInVoice("deltaforce", "Delta Force", true),
                    CreateBuiltInVoice("doubao", "豆包", true),
                    CreateBuiltInVoice("dagoujiao", "大狗叫", true)
                },
                IconPacks = new List<IconPackItem>
                {
                    CreateBuiltInIcon("default", "鍘熺増", true),
                    CreateBuiltInIcon("vip", "VIP", true),
                    CreateBuiltInIcon("angelic_beast", "绀轰緥", true),
                    CreateBuiltInIcon("anniversary_10", "10周年庆", true),
                    CreateBuiltInIcon("anniversary_15", "15周年庆", true),
                    CreateBuiltInIcon("cfpl", "CFPL", true),
                    CreateBuiltInIcon("rankmach_2019_1", "排位赛-1", true),
                    CreateBuiltInIcon("rankmach_2019_2", "排位赛-2", true),
                    CreateBuiltInIcon("csol4", "CSOL 10杀", true),
                    CreateBuiltInIcon("bf1", "Battlefield 1", true),
                    CreateBuiltInIcon("bf5", "Battlefield 5", true),
                    CreateBuiltInIcon("bf4", "Battlefield 4", true),
                    CreateBuiltInIcon("battlefield2042", "Battlefield 2042", true),
                    CreateBuiltInIcon("pubg", "PUBG", true),
                    CreateBuiltInIcon("deltaforce", "Delta Force", true),
                    CreateBuiltInIcon("doubao", "豆包", true),
                    CreateBuiltInIcon("dagoujiao", "大狗叫", true)
                }
            };

            foreach (ValorantPackInfo pack in ValorantPackService.All)
            {
                catalog.VoicePacks.Add(CreateBuiltInVoice(pack.Key, pack.DisplayName, true));
                catalog.IconPacks.Add(CreateBuiltInIcon(pack.Key, pack.DisplayName, true));
            }

            return catalog;
        }

        private static VoicePackItem CreateBuiltInVoice(string key, string name, bool visible)
        {
            return new VoicePackItem
            {
                Key = key,
                DisplayName = name,
                IsBuiltIn = true,
                IsVisibleInWidget = visible
            };
        }

        private static IconPackItem CreateBuiltInIcon(string key, string name, bool visible)
        {
            return new IconPackItem
            {
                Key = key,
                DisplayName = name,
                IsBuiltIn = true,
                IsVisibleInWidget = visible
            };
        }

        private static void MergeMissingBuiltIns(PackCatalog catalog)
        {
            if (catalog.VoicePacks == null)
            {
                catalog.VoicePacks = new List<VoicePackItem>();
            }
            if (catalog.IconPacks == null)
            {
                catalog.IconPacks = new List<IconPackItem>();
            }

            foreach (VoicePackItem item in CreateDefaultCatalog().VoicePacks)
            {
                if (!catalog.VoicePacks.Any(entry => string.Equals(entry.Key, item.Key, StringComparison.OrdinalIgnoreCase)))
                {
                    catalog.VoicePacks.Add(item);
                }
            }

            foreach (IconPackItem item in CreateDefaultCatalog().IconPacks)
            {
                if (!catalog.IconPacks.Any(entry => string.Equals(entry.Key, item.Key, StringComparison.OrdinalIgnoreCase)))
                {
                    catalog.IconPacks.Add(item);
                }
            }
        }

        private static bool RemoveRetiredBuiltIns(PackCatalog catalog)
        {
            int removed = catalog.IconPacks.RemoveAll(item =>
                item.IsBuiltIn
                && string.Equals(item.Key, "legacy", StringComparison.OrdinalIgnoreCase));
            return removed > 0;
        }

        private static bool ApplyBuiltInVisibilityDefaultsIfNeeded(PackCatalog catalog)
        {
            ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
            object rawVersion = localSettings.Values[VisibilityDefaultsVersionKey];
            if (rawVersion is int version && version >= CurrentVisibilityDefaultsVersion)
            {
                return false;
            }

            foreach (VoicePackItem item in catalog.VoicePacks)
            {
                if (item.IsBuiltIn)
                {
                    item.IsVisibleInWidget = true;
                }
            }

            foreach (IconPackItem item in catalog.IconPacks)
            {
                if (!item.IsBuiltIn)
                {
                    continue;
                }

                item.IsVisibleInWidget = true;
            }

            localSettings.Values[VisibilityDefaultsVersionKey] = CurrentVisibilityDefaultsVersion;
            return true;
        }

        private static void ApplyVisibilityOverrides(PackCatalog catalog)
        {
            ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
            foreach (VoicePackItem item in catalog.VoicePacks)
            {
                object stored = localSettings.Values[GetVisibilitySettingKey("voice", item.Key)];
                if (stored is bool isVisible)
                {
                    item.IsVisibleInWidget = isVisible;
                }
            }

            foreach (IconPackItem item in catalog.IconPacks)
            {
                object stored = localSettings.Values[GetVisibilitySettingKey("icon", item.Key)];
                if (stored is bool isVisible)
                {
                    item.IsVisibleInWidget = isVisible;
                }
            }
        }

        private static void SaveVisibilityOverride(string kind, string key, bool isVisible)
        {
            ApplicationData.Current.LocalSettings.Values[GetVisibilitySettingKey(kind, key)] = isVisible;
        }

        private static string GetVisibilitySettingKey(string kind, string key)
        {
            return "PackVisibility." + kind + "." + (key ?? string.Empty).Trim().ToLowerInvariant();
        }

        private static void EnsureAtLeastOneVisibleVoice(PackCatalog catalog)
        {
            if (catalog.VoicePacks.Any(item => item.IsVisibleInWidget))
            {
                return;
            }

            VoicePackItem fallbackVoice = catalog.VoicePacks.FirstOrDefault(entry => string.Equals(entry.Key, DefaultVoiceKey, StringComparison.OrdinalIgnoreCase))
                ?? catalog.VoicePacks.FirstOrDefault();
            if (fallbackVoice != null)
            {
                fallbackVoice.IsVisibleInWidget = true;
            }
        }

        private static void EnsureAtLeastOneVisibleIcon(PackCatalog catalog)
        {
            if (catalog.IconPacks.Any(item => item.IsVisibleInWidget))
            {
                return;
            }

            IconPackItem fallbackIcon = catalog.IconPacks.FirstOrDefault(entry => string.Equals(entry.Key, DefaultIconKey, StringComparison.OrdinalIgnoreCase))
                ?? catalog.IconPacks.FirstOrDefault();
            if (fallbackIcon != null)
            {
                fallbackIcon.IsVisibleInWidget = true;
            }
        }

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

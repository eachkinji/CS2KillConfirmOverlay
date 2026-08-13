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
                mustSave |= ApplyBuiltInVisibilityDefaultsIfNeeded(_cache);
                ApplyVisibilityOverrides(_cache);
                EnsureAtLeastOneVisibleVoice(_cache);
                EnsureAtLeastOneVisibleIcon(_cache);
                if (mustSave)
                {
                    await SaveCoreAsync(_cache, notify: false);
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
                    CreateBuiltInVoice("deltaforce", "Delta Force", true)
                },
                IconPacks = new List<IconPackItem>
                {
                    CreateBuiltInIcon("default", "鍘熺増", true),
                    CreateBuiltInIcon("vip", "VIP", true),
                    CreateBuiltInIcon("legacy", "鑰佺増", false),
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
                    CreateBuiltInIcon("deltaforce", "Delta Force", true)
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

                item.IsVisibleInWidget = !string.Equals(
                    item.Key,
                    "legacy",
                    StringComparison.OrdinalIgnoreCase);
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
    }
}

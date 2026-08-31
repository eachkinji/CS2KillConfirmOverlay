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

                ValorantPackService.RefreshExternalPacks();

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
                RefreshExternalValorantEntries(_cache);
                mustSave |= RefreshBuiltInMetadata(_cache);
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
                    CreateBuiltInVoice("dagoujiao", "大狗叫", true),
                    CreateBuiltInVoice(DagoujiaoAnimalsPackKey, "Animals", true),
                    CreateBuiltInVoice("overwatch", "OverWatch", true),
                    CreateBuiltInVoice("modernwarfare2019", "Modern Warfare 2019", true),
                    CreateBuiltInVoice("custommodule", "瓦默认音效/图标", true),
                    CreateBuiltInVoice("apex", "Apex Legends", true)
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
                    CreateBuiltInIcon("dagoujiao", "大狗叫", true),
                    CreateBuiltInIcon(DagoujiaoAnimalsPackKey, "Animals", true),
                    CreateBuiltInIcon("overwatch", "OverWatch", true),
                    CreateBuiltInIcon("modernwarfare2019", "Modern Warfare 2019", true),
                    CreateBuiltInIcon("custommodule", "瓦默认音效/图标", true),
                    CreateBuiltInIcon("apex", "Apex Legends", true)
                }
            };

            foreach (ValorantPackInfo pack in ValorantPackService.All)
            {
                if (pack.HasBuiltInAudio)
                {
                    VoicePackItem voice = CreateBuiltInVoice(pack.Key, pack.DisplayName, true);
                    voice.AssociationId = pack.AssociationId;
                    catalog.VoicePacks.Add(voice);
                }
                IconPackItem icon = pack.IsExternal
                    ? new IconPackItem
                    {
                        Key = pack.Key,
                        DisplayName = pack.DisplayName,
                        FolderPath = pack.FolderPath,
                        IsBuiltIn = false,
                        IsVisibleInWidget = true,
                        OwnsFolder = true,
                        AssociationId = pack.AssociationId
                    }
                    : CreateBuiltInIcon(pack.Key, pack.DisplayName, true);
                icon.AssociationId = pack.AssociationId;
                catalog.IconPacks.Add(icon);
            }

            catalog.VoicePacks.AddRange(ValorantExternalAssetService.DiscoverExternalVoicePacks());

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

        private static void RefreshExternalValorantEntries(PackCatalog catalog)
        {
            if (catalog?.VoicePacks == null || catalog.IconPacks == null)
            {
                return;
            }

            // Build replacement lists and publish them with property assignment.
            // Readers may still hold the old list while an import refresh runs;
            // mutating that list in place used to crash their enumerators and two
            // racing refreshes could append the same package twice.
            var voicePacks = catalog.VoicePacks
                .Where(item => item.IsBuiltIn
                    || string.IsNullOrWhiteSpace(item.AssociationId)
                    || !item.Key.StartsWith("valorant_voice_", StringComparison.OrdinalIgnoreCase))
                .ToList();
            var iconPacks = catalog.IconPacks
                .Where(item => item.IsBuiltIn
                    || string.IsNullOrWhiteSpace(item.AssociationId)
                    || !item.Key.StartsWith("valorant_icon_", StringComparison.OrdinalIgnoreCase))
                .ToList();

            voicePacks.AddRange(ValorantExternalAssetService.DiscoverExternalVoicePacks());
            foreach (ValorantPackInfo pack in ValorantPackService.All.Where(item => item.IsExternal))
            {
                iconPacks.Add(new IconPackItem
                {
                    Key = pack.Key,
                    DisplayName = pack.DisplayName,
                    FolderPath = pack.FolderPath,
                    IsBuiltIn = false,
                    IsVisibleInWidget = true,
                    OwnsFolder = true,
                    AssociationId = pack.AssociationId
                });
            }

            catalog.VoicePacks = voicePacks
                .GroupBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.Last())
                .ToList();
            catalog.IconPacks = iconPacks
                .GroupBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.Last())
                .ToList();
            ApplyVisibilityOverrides(catalog);
        }

        public static async Task RefreshValorantExternalPacksAsync()
        {
            ValorantPackService.RefreshExternalPacks();
            PackCatalog catalog = await LoadAsync();
            RefreshExternalValorantEntries(catalog);
            CatalogChanged?.Invoke(null, EventArgs.Empty);
        }

        private static bool RefreshBuiltInMetadata(PackCatalog catalog)
        {
            bool changed = false;
            foreach (VoicePackItem valorantVoice in catalog.VoicePacks.Where(item =>
                item.IsBuiltIn && string.Equals(item.Key, ValorantPackService.DefaultKey, StringComparison.OrdinalIgnoreCase)))
            {
                if (!string.Equals(valorantVoice.AssociationId, "valorant:base", StringComparison.OrdinalIgnoreCase))
                {
                    valorantVoice.AssociationId = "valorant:base";
                    changed = true;
                }
            }
            foreach (IconPackItem valorantIcon in catalog.IconPacks.Where(item =>
                item.IsBuiltIn && string.Equals(item.Key, ValorantPackService.DefaultKey, StringComparison.OrdinalIgnoreCase)))
            {
                if (!string.Equals(valorantIcon.AssociationId, "valorant:base", StringComparison.OrdinalIgnoreCase))
                {
                    valorantIcon.AssociationId = "valorant:base";
                    changed = true;
                }
            }

            VoicePackItem voice = catalog.VoicePacks.FirstOrDefault(item =>
                item.IsBuiltIn
                && string.Equals(item.Key, "custommodule", StringComparison.OrdinalIgnoreCase));
            if (voice != null && !string.Equals(voice.DisplayName, "瓦默认音效/图标", StringComparison.Ordinal))
            {
                voice.DisplayName = "瓦默认音效/图标";
                changed = true;
            }

            IconPackItem icon = catalog.IconPacks.FirstOrDefault(item =>
                item.IsBuiltIn
                && string.Equals(item.Key, "custommodule", StringComparison.OrdinalIgnoreCase));
            if (icon != null && !string.Equals(icon.DisplayName, "瓦默认音效/图标", StringComparison.Ordinal))
            {
                icon.DisplayName = "瓦默认音效/图标";
                changed = true;
            }

            return changed;
        }

        private static bool RemoveRetiredBuiltIns(PackCatalog catalog)
        {
            int removedIcons = catalog.IconPacks.RemoveAll(item =>
                item.IsBuiltIn
                && (string.Equals(item.Key, "legacy", StringComparison.OrdinalIgnoreCase)
                    || (ValorantPackService.IsValorantPackKey(item.Key)
                        && ValorantPackService.Find(item.Key) == null)));
            int removedVoices = catalog.VoicePacks.RemoveAll(item =>
                item.IsBuiltIn
                && ValorantPackService.IsValorantPackKey(item.Key)
                && (ValorantPackService.Find(item.Key) == null
                    || !ValorantPackService.Find(item.Key).HasBuiltInAudio));
            return removedIcons + removedVoices > 0;
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

    }
}

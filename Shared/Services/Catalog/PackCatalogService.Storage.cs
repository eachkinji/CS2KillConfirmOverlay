// Shared by Widget (UWP) and CompatibilityHost; extracted from both former copies.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Threading.Tasks;
using Windows.Storage;

#if WINDOWS_UWP
namespace KillConfirmGameBar.Services
#else
namespace KillConfirmCompatibility.Services
#endif
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

#if WINDOWS_UWP
                // Directory scans, manifest parsing and legacy voice repairs
                // can take seconds in an imported library. Keep the first
                // window paint responsive and publish only the complete catalog.
                _cache = await Task.Run(LoadCoreAsync);

                if (!_legacyMigrationTriggered)
                {
                    _legacyMigrationTriggered = true;
                    _ = Task.Run(async () => await MigrateLegacyDoubaoSettingsAsync());
                }
#else
                _cache = await LoadCoreAsync();
#endif

                return _cache;
            }
            finally
            {
                CatalogIoLock.Release();
            }
        }

        private static async Task<PackCatalog> LoadCoreAsync()
        {
#if !SHARED_WIDGET_RUNTIME
            ValorantPackService.RefreshExternalPacks();
#endif
            bool mustSave = false;
            PackCatalog catalog;
            StorageFolder localFolder = ApplicationData.Current.LocalFolder;
            try
            {
                StorageFile file = await localFolder.GetFileAsync(CatalogFileName);
                using (var stream = await file.OpenStreamForReadAsync())
                    catalog = (PackCatalog)new DataContractJsonSerializer(typeof(PackCatalog)).ReadObject(stream);
            }
            catch
            {
                catalog = CreateDefaultCatalog();
                mustSave = true;
            }
            MergeMissingBuiltIns(catalog);
#if !SHARED_WIDGET_RUNTIME
            RefreshExternalValorantEntries(catalog);
            CrossfireExternalAssetService.RefreshCatalog(catalog);
            mustSave |= catalog.IconPacks.Any(p => p.IsBuiltIn && KillConfirmCompatibility.Contracts.CalabiyauPackage.IsExternalKey(p.Key));
            mustSave |= catalog.IconPacks.Any(p => KillConfirmCompatibility.Contracts.CalabiyauPacks.Find(p.Key) is var known && known != null && p.DisplayName != known.DisplayName);
            CalabiyauExternalAssetService.RefreshCatalog(catalog);
#endif
            mustSave |= RefreshBuiltInMetadata(catalog);
            mustSave |= RefreshSanguoshaBuiltInMetadata(catalog);
            mustSave |= MigrateAudioPackMetadata(catalog);
            foreach (var item in catalog.IconPacks)
            {
                var pack = KillConfirmCompatibility.Contracts.CalabiyauPacks.Find(item.Key);
                if (pack != null && item.DisplayName != pack.DisplayName)
                { item.DisplayName = pack.DisplayName; mustSave = true; }
            }
            mustSave |= RemoveRetiredBuiltIns(catalog);
#if WINDOWS_UWP
            mustSave |= ApplyBuiltInVisibilityDefaultsIfNeeded(catalog);
#endif
            ApplyVisibilityOverrides(catalog);
            EnsureAtLeastOneVisibleVoice(catalog);
            EnsureAtLeastOneVisibleIcon(catalog);
#if WINDOWS_UWP
            if (mustSave) await SaveCoreAsync(catalog, notify: false);
#else
            // The desktop host is a reader of the resource library. Only
            // the settings application owns migrations and catalog writes.
            _ = mustSave;
#endif
            return catalog;
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

                    CreateBuiltInVoice("csol4", "CSOL 10杀", true),
                    CreateBuiltInVoice("bf1", "Battlefield 1", true),
                    CreateBuiltInVoice("bf5", "Battlefield 5", true),
                    CreateBuiltInVoice("bf4", "Battlefield 4", true),
                    CreateBuiltInVoice("battlefield2042", "Battlefield 2042", true),
                    CreateBuiltInVoice("battlefield6", "Battlefield 6", true),
                    CreateBuiltInVoice("pubg", "PUBG", true),
                    CreateBuiltInVoice("deltaforce", "Delta Force", true),
                    CreateBuiltInVoice("calabiyau", "默认击杀", true),
                    CreateBuiltInVoice("doubao", "豆包", true),
                    CreateBuiltInVoice("sanguosha", "三国杀 · 十周年连破播报", true),
                    CreateBuiltInVoice("leagueoflegends", "英雄联盟 · 召唤师峡谷连杀音效", true),
                    CreateBuiltInVoice("dagoujiao", "大狗叫", true),
                    CreateBuiltInVoice(DagoujiaoAnimalsPackKey, "Animals", true),
                    CreateBuiltInVoice("overwatch", "OverWatch", true),
                    CreateBuiltInVoice("modernwarfare2019", "Modern Warfare 2019", true),
                    CreateBuiltInVoice("cod2022", "现代战争 II · 原生击杀提示音", true),
                    CreateBuiltInVoice("custommodule", "瓦默认音效/图标", true),
                    CreateBuiltInVoice("apex", "Apex Legends", true),
                    CreateBuiltInVoice("titanfall2", "泰坦陨落 2 · 铁驭击杀提示音", true),
                    CreateBuiltInVoice("rdr2", "荒野大镖客 2 · 原生善恶与死神之眼音效", true),
                    CreateBuiltInVoice("warthunder", "战争雷霆 · 原生击毁与勋章音效", true),
                    new VoicePackItem
                    {
                        Key = "warthunder_voice_zh",
                        DisplayName = "战争雷霆 · 中文车组与勋章播报",
                        IsBuiltIn = true,
                        IsVisibleInWidget = true,
                        AudioKind = AudioPackKind.CharacterVoice,
                        GameStyle = "warthunder"
                    },
                    new VoicePackItem
                    {
                        Key = "warthunder_voice_en",
                        DisplayName = "战争雷霆 · 英文原版勋章播报",
                        IsBuiltIn = true,
                        IsVisibleInWidget = true,
                        AudioKind = AudioPackKind.CharacterVoice,
                        GameStyle = "warthunder"
                    }
                },
                IconPacks = new List<IconPackItem>
                {

                    CreateBuiltInIcon("csol4", "CSOL 10杀", true),
                    CreateBuiltInIcon("bf1", "Battlefield 1", true),
                    CreateBuiltInIcon("bf5", "Battlefield 5", true),
                    CreateBuiltInIcon("bf4", "Battlefield 4", true),
                    CreateBuiltInIcon("battlefield2042", "Battlefield 2042", true),
                    CreateBuiltInIcon("battlefield6", "Battlefield 6", true),
                    CreateBuiltInIcon("pubg", "PUBG", true),
                    CreateBuiltInIcon("deltaforce", "Delta Force", true),
                    CreateBuiltInIcon("calabiyau", "卡拉彼丘 · 默认击杀", true),
                    CreateBuiltInIcon("doubao", "豆包", true),
                    CreateBuiltInIcon("sanguosha", "三国杀 · 十周年连破播报", true),
                    CreateBuiltInIcon("leagueoflegends", "英雄联盟 · 海克斯科技播报", true),
                    CreateBuiltInIcon("dagoujiao", "大狗叫", true),
                    CreateBuiltInIcon(DagoujiaoAnimalsPackKey, "Animals", true),
                    CreateBuiltInIcon("overwatch", "OverWatch", true),
                    CreateBuiltInIcon("modernwarfare2019", "Modern Warfare 2019", true),
                    CreateBuiltInIcon("cod2022", "现代战争 II · 战术 LUI 勋章与反馈", true),
                    CreateBuiltInIcon("custommodule", "瓦默认音效/图标", true),
                    CreateBuiltInIcon("apex", "Apex Legends", true),
                    CreateBuiltInIcon("titanfall2", "泰坦陨落 2 · 铁驭与泰坦勋章", true),
                    CreateBuiltInIcon("rdr2", "荒野大镖客 2 · 亡命之徒荣誉与准星", true),
                    CreateBuiltInIcon("warthunder", "战争雷霆 · 多图层勋章与 X-Ray 毁伤相机", true)
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

            var defaults = CreateDefaultCatalog();
            foreach (VoicePackItem item in defaults.VoicePacks)
            {
                if (!catalog.VoicePacks.Any(entry => string.Equals(entry.Key, item.Key, StringComparison.OrdinalIgnoreCase)))
                {
                    catalog.VoicePacks.Add(item);
                }
            }

            foreach (IconPackItem item in defaults.IconPacks)
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

        public static async Task RefreshCrossfireExternalPacksAsync()
        {
            PackCatalog catalog = await LoadAsync();
            CrossfireExternalAssetService.RefreshCatalog(catalog);
            ApplyVisibilityOverrides(catalog);
            await SaveAsync(catalog);
        }

        public static async Task RefreshCalabiyauExternalPacksAsync()
        {
            PackCatalog catalog = await LoadAsync();
            CalabiyauExternalAssetService.RefreshCatalog(catalog);
            ApplyVisibilityOverrides(catalog);
            await SaveAsync(catalog);
        }

        private static bool RefreshBuiltInMetadata(PackCatalog catalog)
        {
            bool changed = false;
            var calabiyauAudio = catalog.VoicePacks.FirstOrDefault(item => item.IsBuiltIn
                && string.Equals(item.Key, "calabiyau", StringComparison.OrdinalIgnoreCase));
            if (calabiyauAudio != null)
            {
                if (calabiyauAudio.DisplayName != "默认击杀")
                { calabiyauAudio.DisplayName = "默认击杀"; changed = true; }
                if (calabiyauAudio.AudioKind != AudioPackKind.SoundEffect)
                { calabiyauAudio.AudioKind = AudioPackKind.SoundEffect; changed = true; }
                if (calabiyauAudio.AssociatedIconKeys == null || !calabiyauAudio.AssociatedIconKeys.Contains("calabiyau"))
                { calabiyauAudio.AssociatedIconKeys = new List<string> { "calabiyau" }; changed = true; }
            }

            var tf2Audio = catalog.VoicePacks.FirstOrDefault(item => item.IsBuiltIn
                && string.Equals(item.Key, "titanfall2", StringComparison.OrdinalIgnoreCase));
            if (tf2Audio != null)
            {
                if (tf2Audio.DisplayName != "泰坦陨落 2 · 铁驭击杀提示音")
                { tf2Audio.DisplayName = "泰坦陨落 2 · 铁驭击杀提示音"; changed = true; }
                if (tf2Audio.AudioKind != AudioPackKind.SoundEffect)
                { tf2Audio.AudioKind = AudioPackKind.SoundEffect; changed = true; }
                if (!string.Equals(tf2Audio.GameStyle, "titanfall2", StringComparison.OrdinalIgnoreCase))
                { tf2Audio.GameStyle = "titanfall2"; changed = true; }
                if (tf2Audio.AssociatedIconKeys == null
                    || !tf2Audio.AssociatedIconKeys.Contains("titanfall2")
                    || !tf2Audio.AssociatedIconKeys.Contains("titanfall2_icon_standard"))
                { tf2Audio.AssociatedIconKeys = new List<string> { "titanfall2", "titanfall2_icon_standard" }; changed = true; }
            }
            var tf2Icon = catalog.IconPacks.FirstOrDefault(item => item.IsBuiltIn
                && string.Equals(item.Key, "titanfall2", StringComparison.OrdinalIgnoreCase));
            if (tf2Icon != null && tf2Icon.DisplayName != "泰坦陨落 2 · 铁驭与泰坦勋章")
            {
                tf2Icon.DisplayName = "泰坦陨落 2 · 铁驭与泰坦勋章";
                changed = true;
            }

            var lolAudio = catalog.VoicePacks.FirstOrDefault(item => item.IsBuiltIn
                && string.Equals(item.Key, "leagueoflegends", StringComparison.OrdinalIgnoreCase));
            if (lolAudio != null)
            {
                if (lolAudio.DisplayName != "英雄联盟 · 召唤师峡谷连杀音效")
                { lolAudio.DisplayName = "英雄联盟 · 召唤师峡谷连杀音效"; changed = true; }
                if (lolAudio.AudioKind != AudioPackKind.SoundEffect)
                { lolAudio.AudioKind = AudioPackKind.SoundEffect; changed = true; }
                if (!string.Equals(lolAudio.GameStyle, "leagueoflegends", StringComparison.OrdinalIgnoreCase))
                { lolAudio.GameStyle = "leagueoflegends"; changed = true; }
                if (lolAudio.AssociatedIconKeys == null
                    || !lolAudio.AssociatedIconKeys.Contains("leagueoflegends")
                    || !lolAudio.AssociatedIconKeys.Contains("leagueoflegends_icon_standard"))
                { lolAudio.AssociatedIconKeys = new List<string> { "leagueoflegends", "leagueoflegends_icon_standard" }; changed = true; }
            }
            var lolIcon = catalog.IconPacks.FirstOrDefault(item => item.IsBuiltIn
                && string.Equals(item.Key, "leagueoflegends", StringComparison.OrdinalIgnoreCase));
            if (lolIcon != null && lolIcon.DisplayName != "英雄联盟 · 海克斯科技播报")
            {
                lolIcon.DisplayName = "英雄联盟 · 海克斯科技播报";
                changed = true;
            }

            var rdr2Audio = catalog.VoicePacks.FirstOrDefault(item => item.IsBuiltIn
                && string.Equals(item.Key, "rdr2", StringComparison.OrdinalIgnoreCase));
            if (rdr2Audio != null)
            {
                if (rdr2Audio.DisplayName != "荒野大镖客 2 · 原生善恶与死神之眼音效")
                { rdr2Audio.DisplayName = "荒野大镖客 2 · 原生善恶与死神之眼音效"; changed = true; }
                if (rdr2Audio.AudioKind != AudioPackKind.SoundEffect)
                { rdr2Audio.AudioKind = AudioPackKind.SoundEffect; changed = true; }
                if (!string.Equals(rdr2Audio.GameStyle, "rdr2", StringComparison.OrdinalIgnoreCase))
                { rdr2Audio.GameStyle = "rdr2"; changed = true; }
                if (rdr2Audio.AssociatedIconKeys == null
                    || !rdr2Audio.AssociatedIconKeys.Contains("rdr2")
                    || !rdr2Audio.AssociatedIconKeys.Contains("rdr2_icon_standard"))
                { rdr2Audio.AssociatedIconKeys = new List<string> { "rdr2", "rdr2_icon_standard" }; changed = true; }
            }
            var rdr2Icon = catalog.IconPacks.FirstOrDefault(item => item.IsBuiltIn
                && string.Equals(item.Key, "rdr2", StringComparison.OrdinalIgnoreCase));
            if (rdr2Icon != null && rdr2Icon.DisplayName != "荒野大镖客 2 · 亡命之徒荣誉与准星")
            {
                rdr2Icon.DisplayName = "荒野大镖客 2 · 亡命之徒荣誉与准星";
                changed = true;
            }

            var wtAudio = catalog.VoicePacks.FirstOrDefault(item => item.IsBuiltIn
                && string.Equals(item.Key, "warthunder", StringComparison.OrdinalIgnoreCase));
            if (wtAudio != null)
            {
                if (wtAudio.DisplayName != "战争雷霆 · 原生击毁与勋章音效")
                { wtAudio.DisplayName = "战争雷霆 · 原生击毁与勋章音效"; changed = true; }
                if (wtAudio.AudioKind != AudioPackKind.SoundEffect)
                { wtAudio.AudioKind = AudioPackKind.SoundEffect; changed = true; }
                if (!string.Equals(wtAudio.GameStyle, "warthunder", StringComparison.OrdinalIgnoreCase))
                { wtAudio.GameStyle = "warthunder"; changed = true; }
                if (wtAudio.AssociatedIconKeys == null
                    || !wtAudio.AssociatedIconKeys.Contains("warthunder")
                    || !wtAudio.AssociatedIconKeys.Contains("warthunder_icon_standard"))
                { wtAudio.AssociatedIconKeys = new List<string> { "warthunder", "warthunder_icon_standard" }; changed = true; }
            }
            var wtVoiceZh = catalog.VoicePacks.FirstOrDefault(item => item.IsBuiltIn
                && string.Equals(item.Key, "warthunder_voice_zh", StringComparison.OrdinalIgnoreCase));
            if (wtVoiceZh != null)
            {
                if (wtVoiceZh.DisplayName != "战争雷霆 · 中文车组与勋章播报")
                { wtVoiceZh.DisplayName = "战争雷霆 · 中文车组与勋章播报"; changed = true; }
                if (wtVoiceZh.AudioKind != AudioPackKind.CharacterVoice)
                { wtVoiceZh.AudioKind = AudioPackKind.CharacterVoice; changed = true; }
                if (!string.Equals(wtVoiceZh.GameStyle, "warthunder", StringComparison.OrdinalIgnoreCase))
                { wtVoiceZh.GameStyle = "warthunder"; changed = true; }
            }
            var wtVoiceEn = catalog.VoicePacks.FirstOrDefault(item => item.IsBuiltIn
                && string.Equals(item.Key, "warthunder_voice_en", StringComparison.OrdinalIgnoreCase));
            if (wtVoiceEn != null)
            {
                if (wtVoiceEn.DisplayName != "战争雷霆 · 英文原版勋章播报")
                { wtVoiceEn.DisplayName = "战争雷霆 · 英文原版勋章播报"; changed = true; }
                if (wtVoiceEn.AudioKind != AudioPackKind.CharacterVoice)
                { wtVoiceEn.AudioKind = AudioPackKind.CharacterVoice; changed = true; }
                if (!string.Equals(wtVoiceEn.GameStyle, "warthunder", StringComparison.OrdinalIgnoreCase))
                { wtVoiceEn.GameStyle = "warthunder"; changed = true; }
            }
            var wtIcon = catalog.IconPacks.FirstOrDefault(item => item.IsBuiltIn
                && string.Equals(item.Key, "warthunder", StringComparison.OrdinalIgnoreCase));
            if (wtIcon != null && wtIcon.DisplayName != "战争雷霆 · 多图层勋章与 X-Ray 毁伤相机")
            {
                wtIcon.DisplayName = "战争雷霆 · 多图层勋章与 X-Ray 毁伤相机";
                changed = true;
            }

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

        private static bool RefreshSanguoshaBuiltInMetadata(PackCatalog catalog)
        {
            bool changed = false;
            var sgsVoice = catalog.VoicePacks.FirstOrDefault(p => p.IsBuiltIn && string.Equals(p.Key, "sanguosha", StringComparison.OrdinalIgnoreCase));
            if (sgsVoice != null && sgsVoice.DisplayName != "三国杀 · 十周年连破播报") { sgsVoice.DisplayName = "三国杀 · 十周年连破播报"; changed = true; }
            var sgsIcon = catalog.IconPacks.FirstOrDefault(p => p.IsBuiltIn && string.Equals(p.Key, "sanguosha", StringComparison.OrdinalIgnoreCase));
            if (sgsIcon != null && sgsIcon.DisplayName != "三国杀 · 十周年连破播报") { sgsIcon.DisplayName = "三国杀 · 十周年连破播报"; changed = true; }
            return changed;
        }

        private static bool RemoveRetiredBuiltIns(PackCatalog catalog)
        {
            int removedIcons = catalog.IconPacks.RemoveAll(item =>
                item.IsBuiltIn
                && (KillConfirmCompatibility.Contracts.CalabiyauPackage.IsExternalKey(item.Key)
                    || string.Equals(item.Key, "legacy", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(item.Key, "sanguosha_guanjie", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(item.Key, "sanguosha_equip", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(item.Key, "sanguosha_zzq", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(item.Key, "sanguosha_badge", StringComparison.OrdinalIgnoreCase)
                    || (ValorantPackService.IsValorantPackKey(item.Key)
                        && ValorantPackService.Find(item.Key) == null)));
            int removedVoices = catalog.VoicePacks.RemoveAll(item =>
                item.IsBuiltIn
                && (string.Equals(item.Key, "sanguosha_guanjie", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(item.Key, "sanguosha_general", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(item.Key, "sanguosha_card", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(item.Key, "sanguosha_zzq", StringComparison.OrdinalIgnoreCase)));
            removedVoices += catalog.VoicePacks.RemoveAll(item =>
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

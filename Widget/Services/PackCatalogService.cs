using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Storage;
using Windows.Storage.AccessCache;
using KillConfirmGameBar.Helpers;

namespace KillConfirmGameBar.Services
{
    [DataContract]
    public sealed class PackCatalog
    {
        [DataMember]
        public List<VoicePackItem> VoicePacks { get; set; } = new List<VoicePackItem>();

        [DataMember]
        public List<IconPackItem> IconPacks { get; set; } = new List<IconPackItem>();
    }

    [DataContract]
    public sealed class VoicePackItem
    {
        [DataMember]
        public string Key { get; set; }
        [DataMember]
        public string DisplayName { get; set; }
        [DataMember]
        public string FolderPath { get; set; }
        [DataMember]
        public bool IsBuiltIn { get; set; }
        [DataMember]
        public bool IsVisibleInWidget { get; set; }
        [DataMember]
        public bool OwnsFolder { get; set; }
    }

    [DataContract]
    public sealed class IconPackItem
    {
        [DataMember]
        public string Key { get; set; }
        [DataMember]
        public string DisplayName { get; set; }
        [DataMember]
        public string FolderPath { get; set; }
        [DataMember]
        public string FolderToken { get; set; }
        [DataMember]
        public bool IsBuiltIn { get; set; }
        [DataMember]
        public bool IsVisibleInWidget { get; set; }
        [DataMember]
        public bool OwnsFolder { get; set; }
        [DataMember]
        public bool HasFxOverlay { get; set; }
        [DataMember]
        public bool HasKillFxOverlay { get; set; }
        [DataMember]
        public bool HasEliteOverlay { get; set; }
        [DataMember]
        public bool HasWeaponBadgeOverlay { get; set; }
    }

    public sealed class IconPackCapabilities
    {
        public bool HasKillFxOverlay { get; set; }
        public bool HasEliteOverlay { get; set; }
        public bool HasWeaponBadgeOverlay { get; set; }
    }

    public sealed class VoicePackBuildOptions
    {
        public IReadOnlyDictionary<string, StorageFile> SelectedFiles { get; set; }
        public IReadOnlyDictionary<string, bool> CommonOverlayEnabled { get; set; }
        public bool UseBuiltInDefaultCommonOverlay { get; set; }
        public StorageFile CommonOverlayFile { get; set; }
        public StorageFile HeadImageFile { get; set; }
    }

    public static class PackCatalogService
    {
        private const string CatalogFileName = "pack-catalog.json";
        private const string VisibilityDefaultsVersionKey = "PackCatalogVisibilityDefaultsVersion";
        private const int CurrentVisibilityDefaultsVersion = 2;
        private const string DefaultVoiceKey = "crossfire_swat_gr";
        private const string DefaultIconKey = "default";
        private static readonly string[] SupportedAudioExtensions = { ".wav", ".mp3", ".m4a" };
        private static readonly string[] IconImageExtensions = { ".png", ".jpg", ".jpeg", ".webp", ".tga" };
        private static PackCatalog _cache;

        public static event EventHandler CatalogChanged;

        public static string GetVoicePackDisplayName(VoicePackItem item)
        {
            if (item == null) return string.Empty;
            if (item.IsBuiltIn)
            {
                return LocalizationManager.Text(item.Key);
            }
            return item.DisplayName;
        }

        public static string GetIconPackDisplayName(IconPackItem item)
        {
            if (item == null) return string.Empty;
            if (item.IsBuiltIn)
            {
                return LocalizationManager.Text(item.Key);
            }
            return item.DisplayName;
        }

        public static bool IsImportedVoicePackKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return false;
            return key.StartsWith("custom_voice_", StringComparison.OrdinalIgnoreCase);
        }

        public static async Task<IReadOnlyList<VoicePackItem>> GetVisibleVoicePacksAsync()
        {
            var catalog = await LoadAsync();
            return catalog.VoicePacks.Where(p => p.IsVisibleInWidget).ToList();
        }

        public static async Task<IReadOnlyList<IconPackItem>> GetVisibleIconPacksAsync()
        {
            var catalog = await LoadAsync();
            return catalog.IconPacks.Where(p => p.IsVisibleInWidget).ToList();
        }

        public static async Task<IReadOnlyList<VoicePackItem>> GetAllVoicePacksAsync()
        {
            var catalog = await LoadAsync();
            return catalog.VoicePacks;
        }

        public static async Task<IReadOnlyList<IconPackItem>> GetAllIconPacksAsync()
        {
            var catalog = await LoadAsync();
            return catalog.IconPacks;
        }

        public static async Task<VoicePackItem> GetVoicePackAsync(string key)
        {
            var catalog = await LoadAsync();
            return catalog.VoicePacks.FirstOrDefault(p => p.Key == key);
        }

        public static async Task<IconPackItem> GetIconPackAsync(string key)
        {
            var catalog = await LoadAsync();
            return catalog.IconPacks.FirstOrDefault(p => p.Key == key);
        }

        public static bool IsImportedIconPackKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return false;
            return key.StartsWith("custom_icon_", StringComparison.OrdinalIgnoreCase);
        }

        public static async Task<StorageFolder> GetImportedIconFolderAsync(string key)
        {
            var item = await GetIconPackAsync(key);
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

        public static async Task<IconPackItem> RefreshImportedIconPackCapabilitiesAsync(string key)
        {
            var catalog = await LoadAsync();
            var item = catalog.IconPacks.FirstOrDefault(p => string.Equals(p.Key, key, StringComparison.OrdinalIgnoreCase));
            if (item == null || item.IsBuiltIn || string.IsNullOrWhiteSpace(item.FolderPath))
            {
                return item;
            }

            StorageFolder folder;
            try
            {
                folder = await StorageFolder.GetFolderFromPathAsync(item.FolderPath);
            }
            catch
            {
                return item;
            }

            IconPackCapabilities capabilities = await DetectIconPackCapabilitiesAsync(folder);
            bool changed = item.HasFxOverlay != capabilities.HasKillFxOverlay
                || item.HasKillFxOverlay != capabilities.HasKillFxOverlay
                || item.HasEliteOverlay != capabilities.HasEliteOverlay
                || item.HasWeaponBadgeOverlay != capabilities.HasWeaponBadgeOverlay;

            item.HasFxOverlay = capabilities.HasKillFxOverlay;
            item.HasKillFxOverlay = capabilities.HasKillFxOverlay;
            item.HasEliteOverlay = capabilities.HasEliteOverlay;
            item.HasWeaponBadgeOverlay = capabilities.HasWeaponBadgeOverlay;

            if (changed)
            {
                await SaveAsync(catalog);
            }

            return item;
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

        public static async Task ImportIconPackAsync(StorageFolder folder)
        {
            IconPackCapabilities capabilities = await DetectIconPackCapabilitiesAsync(folder);
            var catalog = await LoadAsync();
            catalog.IconPacks.Add(new IconPackItem
            {
                Key = "custom_icon_" + Guid.NewGuid().ToString("N"),
                DisplayName = folder.DisplayName,
                FolderPath = folder.Path,
                IsBuiltIn = false,
                IsVisibleInWidget = true,
                OwnsFolder = false,
                HasFxOverlay = capabilities.HasKillFxOverlay,
                HasKillFxOverlay = capabilities.HasKillFxOverlay,
                HasEliteOverlay = capabilities.HasEliteOverlay,
                HasWeaponBadgeOverlay = capabilities.HasWeaponBadgeOverlay
            });
            await SaveAsync(catalog);
        }

        public static async Task<IconPackCapabilities> DetectIconPackCapabilitiesAsync(StorageFolder folder)
        {
            return new IconPackCapabilities
            {
                HasKillFxOverlay = await ContainsAnyFileAsync(folder,
                    "multi2_fx.png", "multi2_fx.tga",
                    "multi3_fx.png", "multi3_fx.tga",
                    "multi4_fx.png", "multi4_fx.tga",
                    "multi5_fx.png", "multi5_fx.tga",
                    "multi6_fx.png", "multi6_fx.tga"),
                HasEliteOverlay = await ContainsAnyFileAsync(folder,
                    "KillMark_Upgrade1.png", "KillMark_Upgrade1.tga",
                    "KillMark_Upgrade2.png", "KillMark_Upgrade2.tga",
                    "KillMark_Upgrade3.png", "KillMark_Upgrade3.tga",
                    "badge_knife_1.png", "badge_knife_1.tga",
                    "badge_knife_2.png", "badge_knife_2.tga",
                    "badge_knife_3.png", "badge_knife_3.tga"),
                HasWeaponBadgeOverlay = await ContainsAnyFileAsync(folder,
                    "badge_assault1.png", "badge_assault1.tga",
                    "badge_assault2.png", "badge_assault2.tga",
                    "badge_assault3.png", "badge_assault3.tga",
                    "badge_scout1.png", "badge_scout1.tga",
                    "badge_scout2.png", "badge_scout2.tga",
                    "badge_scout3.png", "badge_scout3.tga",
                    "badge_sniper1.png", "badge_sniper1.tga",
                    "badge_sniper2.png", "badge_sniper2.tga",
                    "badge_sniper3.png", "badge_sniper3.tga",
                    "badge_elite1.png", "badge_elite1.tga",
                    "badge_elite2.png", "badge_elite2.tga",
                    "badge_elite3.png", "badge_elite3.tga",
                    "badge_knife1.png", "badge_knife1.tga",
                    "badge_knife2.png", "badge_knife2.tga",
                    "badge_knife3.png", "badge_knife3.tga")
            };
        }

        private static async Task<bool> ContainsAnyFileAsync(StorageFolder folder, params string[] fileNames)
        {
            foreach (string name in fileNames)
            {
                foreach (string candidate in ExpandIconFileCandidates(name))
                {
                    try
                    {
                        await folder.GetFileAsync(candidate);
                        return true;
                    }
                    catch
                    {
                    }
                }
            }

            return false;
        }

        private static IEnumerable<string> ExpandIconFileCandidates(string fileName)
        {
            string extension = Path.GetExtension(fileName);
            if (extension.Equals(".png", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".tga", StringComparison.OrdinalIgnoreCase))
            {
                string withoutExtension = Path.ChangeExtension(fileName, null);
                foreach (string candidateExtension in IconImageExtensions)
                {
                    yield return withoutExtension + candidateExtension;
                }

                yield break;
            }

            yield return fileName;
        }

        public static async Task SetVoicePackVisibilityAsync(string key, bool isVisible)
        {
            var catalog = await LoadAsync();
            var item = catalog.VoicePacks.FirstOrDefault(p => p.Key == key);
            if (item != null)
            {
                item.IsVisibleInWidget = isVisible;
                await SaveAsync(catalog);
            }
        }

        public static async Task SetIconPackVisibilityAsync(string key, bool isVisible)
        {
            var catalog = await LoadAsync();
            var item = catalog.IconPacks.FirstOrDefault(p => p.Key == key);
            if (item != null)
            {
                item.IsVisibleInWidget = isVisible;
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

        public static async Task RemoveCustomIconPackAsync(string key)
        {
            var catalog = await LoadAsync();
            var item = catalog.IconPacks.FirstOrDefault(p => p.Key == key);
            if (item != null && !item.IsBuiltIn)
            {
                catalog.IconPacks.Remove(item);
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

            await WriteGeneratedVoiceLuaAsync(packFolder, options.CommonOverlayEnabled);

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

        private static async Task WriteGeneratedVoiceLuaAsync(
            StorageFolder packFolder,
            IReadOnlyDictionary<string, bool> commonOverlayEnabled)
        {
            var knownFiles = new[]
            {
                "common_overlay",
                "common",
                "2",
                "3",
                "4",
                "5",
                "6",
                "7",
                "8",
                "headshot",
                "knife",
                "firstandlast"
            };

            var available = new List<string>();
            foreach (string baseName in knownFiles)
            {
                string fileName = await FindAudioFileNameAsync(packFolder, baseName);
                if (!string.IsNullOrWhiteSpace(fileName))
                {
                    available.Add($"[\"{baseName}\"] = \"{fileName}\"");
                }
            }

            var overlayEntries = new List<string>();
            if (commonOverlayEnabled != null)
            {
                foreach (var pair in commonOverlayEnabled)
                {
                    string key = Path.GetFileNameWithoutExtension(pair.Key);
                    if (pair.Value && !string.IsNullOrWhiteSpace(key))
                    {
                        overlayEntries.Add($"[\"{key}\"] = true");
                    }
                }
            }

            string script =
$@"function get_sounds(ctx)
	local sounds = {{}}
	local base = ctx.base_dir .. ""/""
	local available = {{
    {string.Join(",\n    ", available)}
	}}
	local overlay_enabled = {{
    {string.Join(",\n    ", overlayEntries)}
	}}

	local common_overlay_played = false

	local function add_if_present(name)
		if available[name] then
			table.insert(sounds, base .. available[name])
		end
	end

	local function add_common_overlay_if_enabled(name)
		if common_overlay_played then
			return
		end
		if available[""common_overlay""] and overlay_enabled[name] then
			common_overlay_played = true
			table.insert(sounds, base .. available[""common_overlay""])
		end
	end

	if ctx.is_first_kill or ctx.is_last_kill then
		add_if_present(""firstandlast"")
		add_common_overlay_if_enabled(""firstandlast"")
		if #sounds > 0 then
			return sounds
		end
	end

	if ctx.play_main_audio and ctx.kill_count >= 2 then
		local voiced_kill_count = math.min(ctx.kill_count, 8)
		local name = tostring(voiced_kill_count)
		add_if_present(name)
		add_common_overlay_if_enabled(name)
	elseif ctx.is_knife_kill then
		add_if_present(""knife"")
		add_common_overlay_if_enabled(""knife"")
	elseif ctx.is_headshot then
		add_if_present(""headshot"")
		add_common_overlay_if_enabled(""headshot"")
	elseif ctx.play_main_audio and ctx.kill_count == 1 then
		add_if_present(""common"")
		add_common_overlay_if_enabled(""common"")
	end

	return sounds
end
";

            StorageFile luaFile = await packFolder.CreateFileAsync("sound.lua", CreationCollisionOption.ReplaceExisting);
            await FileIO.WriteTextAsync(luaFile, script);
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

        public static async Task CreateIconPackAsync(string displayName, IReadOnlyDictionary<string, StorageFile> selectedFiles)
        {
            if (selectedFiles == null || selectedFiles.Count == 0)
            {
                return;
            }

            StorageFolder root = await GetOrCreatePackRootAsync("GeneratedIconPacks");
            StorageFolder packFolder = await root.CreateFolderAsync(
                SanitizeName(displayName),
                CreationCollisionOption.GenerateUniqueName);

            foreach (var pair in selectedFiles)
            {
                if (pair.Value == null)
                {
                    continue;
                }

                if (pair.Value.FileType.Equals(".tga", StringComparison.OrdinalIgnoreCase))
                {
                    await TgaDecoder.ConvertTgaToPngAsync(pair.Value, packFolder, pair.Key);
                }
                else
                {
                    await pair.Value.CopyAsync(packFolder, pair.Key, NameCollisionOption.ReplaceExisting);
                }
            }

            IconPackCapabilities capabilities = await DetectIconPackCapabilitiesAsync(packFolder);

            PackCatalog catalog = await LoadAsync();
            catalog.IconPacks.Add(new IconPackItem
            {
                Key = "custom_icon_" + Guid.NewGuid().ToString("N"),
                DisplayName = displayName,
                FolderPath = packFolder.Path,
                IsBuiltIn = false,
                IsVisibleInWidget = true,
                OwnsFolder = true,
                HasFxOverlay = capabilities.HasKillFxOverlay,
                HasKillFxOverlay = capabilities.HasKillFxOverlay,
                HasEliteOverlay = capabilities.HasEliteOverlay,
                HasWeaponBadgeOverlay = capabilities.HasWeaponBadgeOverlay
            });
            await SaveAsync(catalog);
        }

        private static async Task<PackCatalog> LoadAsync()
        {
            if (_cache != null)
            {
                return _cache;
            }

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
                await SaveAsync(_cache);
            }

            MergeMissingBuiltIns(_cache);
            ApplyBuiltInVisibilityDefaultsIfNeeded(_cache);
            EnsureAtLeastOneVisibleVoice(_cache);
            EnsureAtLeastOneVisibleIcon(_cache);
            return _cache;
        }

        private static async Task SaveAsync(PackCatalog catalog)
        {
            _cache = catalog;
            try
            {
                StorageFolder localFolder = ApplicationData.Current.LocalFolder;
                StorageFile file = await localFolder.CreateFileAsync(CatalogFileName, CreationCollisionOption.ReplaceExisting);
                using (var stream = await file.OpenStreamForWriteAsync())
                {
                    var serializer = new DataContractJsonSerializer(typeof(PackCatalog));
                    serializer.WriteObject(stream, catalog);
                }

                CatalogChanged?.Invoke(null, EventArgs.Empty);
            }
            catch { }
        }

        private static PackCatalog CreateDefaultCatalog()
        {
            return new PackCatalog
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
                    CreateBuiltInVoice("crossfire_heart_judge_bl", "Heart Judge BL", true)
                },
                IconPacks = new List<IconPackItem>
                {
                    CreateBuiltInIcon("default", "鍘熺増", true),
                    CreateBuiltInIcon("vip", "VIP", true),
                    CreateBuiltInIcon("legacy", "鑰佺増", false),
                    CreateBuiltInIcon("angelic_beast", "绀轰緥", false)
                }
            };
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

        private static void ApplyBuiltInVisibilityDefaultsIfNeeded(PackCatalog catalog)
        {
            ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
            object rawVersion = localSettings.Values[VisibilityDefaultsVersionKey];
            if (rawVersion is int version && version >= CurrentVisibilityDefaultsVersion)
            {
                return;
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

                item.IsVisibleInWidget = string.Equals(item.Key, "default", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(item.Key, "vip", StringComparison.OrdinalIgnoreCase);
            }

            localSettings.Values[VisibilityDefaultsVersionKey] = CurrentVisibilityDefaultsVersion;
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

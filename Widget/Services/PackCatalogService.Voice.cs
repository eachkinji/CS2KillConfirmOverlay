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
            return key.StartsWith("custom_voice_", StringComparison.OrdinalIgnoreCase);
        }

        public static async Task<IReadOnlyList<VoicePackItem>> GetVisibleVoicePacksAsync()
        {
            var catalog = await LoadAsync();
            return catalog.VoicePacks
                .Where(p => p.IsVisibleInWidget && GameStyleService.IsVisibleForCurrentStyle(p.Key))
                .ToList();
        }

        public static async Task<IReadOnlyList<VoicePackItem>> GetAllVoicePacksAsync()
        {
            var catalog = await LoadAsync();
            return catalog.VoicePacks;
        }

        public static async Task<VoicePackItem> GetVoicePackAsync(string key)
        {
            var catalog = await LoadAsync();
            return catalog.VoicePacks.FirstOrDefault(p => p.Key == key);
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
    }
}

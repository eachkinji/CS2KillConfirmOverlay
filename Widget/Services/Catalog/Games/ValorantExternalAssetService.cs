using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Threading.Tasks;
using Windows.Storage;

namespace KillConfirmGameBar.Services
{
    /// <summary>
    /// Resolves VALORANT material installed outside the MSIX.  The external tree
    /// deliberately mirrors the packaged killconfirm tree so an extracted theme
    /// can be copied without a second conversion pass:
    /// LocalState\Packs\valorant\visual\_native\themes\...\textures\...
    /// </summary>
    internal static class ValorantExternalAssetService
    {
        private const string PacksFolderName = "Packs";
        private const string ValorantFolderName = "valorant";
        private const string VisualFolderName = "visual";
        private const string AudioFolderName = "audio";
        private const string PluginsFolderName = "plugins";
        private const string TextureFolderName = "textures";

        public static async Task<StorageFile> TryGetVisualTextureAsync(
            string packKey,
            string themeFolder,
            string fileName)
        {
            if (!IsSafeFileName(packKey)
                || !IsSafeRelativePath(themeFolder)
                || !IsSafeFileName(fileName))
            {
                return null;
            }

            string pluginRoot = Path.Combine(GetPluginsRootPath(), packKey);
            string[] candidates = string.Equals(themeFolder, "_native/shared", StringComparison.OrdinalIgnoreCase)
                ? new[]
                {
                    Path.Combine(pluginRoot, "shared", TextureFolderName, fileName),
                    Path.Combine(GetSharedRootPath(), TextureFolderName, fileName),
                    Path.Combine(GetVisualRootPath(), "_native", "shared", TextureFolderName, fileName)
                }
                : new[]
                {
                    Path.Combine(pluginRoot, TextureFolderName, fileName),
                    Path.Combine(
                        GetVisualRootPath(),
                        themeFolder.Replace('/', Path.DirectorySeparatorChar),
                        TextureFolderName,
                        fileName)
                };

            foreach (string candidate in candidates)
            {
                StorageFile file = await TryGetFileUnderRootAsync(GetValorantRootPath(), candidate);
                if (file != null)
                {
                    return file;
                }
            }

            return null;
        }

        /// <summary>
        /// Returns a complete external sound-pack folder when present. The
        /// service already understands custom_path, so no service-side plugin
        /// protocol is necessary.
        /// </summary>
        public static async Task<string> TryGetAudioPackFolderPathAsync(string packKey)
        {
            if (!IsSafeFileName(packKey))
            {
                return null;
            }

            try
            {
                string[] candidates =
                {
                    Path.Combine(GetPluginsRootPath(), packKey, AudioFolderName),
                    Path.Combine(GetAudioRootPath(), packKey)
                };
                foreach (string candidate in candidates)
                {
                    if (!IsChildPath(GetValorantRootPath(), Path.GetFullPath(candidate)))
                    {
                        continue;
                    }

                    StorageFolder folder;
                    try
                    {
                        folder = await StorageFolder.GetFolderFromPathAsync(candidate);
                    }
                    catch
                    {
                        continue;
                    }

                    bool hasAudio = (await folder.GetFilesAsync()).Any(IsSupportedAudioFile);
                    if (hasAudio)
                    {
                        return folder.Path;
                    }
                }
            }
            catch
            {
            }

            return null;
        }

        public static IReadOnlyList<ValorantPackInfo> DiscoverExternalPacks()
        {
            var result = new List<ValorantPackInfo>();
            string pluginsRoot = GetPluginsRootPath();
            if (!Directory.Exists(pluginsRoot))
            {
                return result;
            }

            foreach (string folderPath in Directory.EnumerateDirectories(pluginsRoot))
            {
                try
                {
                    string folderName = Path.GetFileName(folderPath);
                    string manifestPath = Path.Combine(folderPath, "manifest.json");
                    if (!IsSafeFileName(folderName) || !File.Exists(manifestPath))
                    {
                        continue;
                    }

                    ValorantExternalPackManifest manifest;
                    using (var stream = File.OpenRead(manifestPath))
                    {
                        var serializer = new DataContractJsonSerializer(typeof(ValorantExternalPackManifest));
                        manifest = serializer.ReadObject(stream) as ValorantExternalPackManifest;
                    }

                    if (!TryCreatePackInfo(folderName, folderPath, manifest, out ValorantPackInfo pack))
                    {
                        continue;
                    }

                    result.Add(pack);
                }
                catch (Exception ex)
                {
                    App.Log("Skipped invalid VALORANT external pack: " + ex.Message);
                }
            }

            return result;
        }

        public static string GetVisualRootPath()
        {
            return Path.Combine(
                ApplicationData.Current.LocalFolder.Path,
                PacksFolderName,
                ValorantFolderName,
                VisualFolderName);
        }

        public static string GetPluginsRootPath()
        {
            return Path.Combine(GetValorantRootPath(), PluginsFolderName);
        }

        public static string GetSharedRootPath()
        {
            return Path.Combine(GetValorantRootPath(), "shared");
        }

        public static string GetAudioRootPath()
        {
            return Path.Combine(
                ApplicationData.Current.LocalFolder.Path,
                PacksFolderName,
                ValorantFolderName,
                AudioFolderName);
        }

        public static async Task<StorageFolder> GetExternalAssetsFolderAsync()
        {
            StorageFolder packs = await ApplicationData.Current.LocalFolder.CreateFolderAsync(
                PacksFolderName,
                CreationCollisionOption.OpenIfExists);
            StorageFolder valorant = await packs.CreateFolderAsync(
                ValorantFolderName,
                CreationCollisionOption.OpenIfExists);
            await valorant.CreateFolderAsync(VisualFolderName, CreationCollisionOption.OpenIfExists);
            await valorant.CreateFolderAsync(AudioFolderName, CreationCollisionOption.OpenIfExists);
            await valorant.CreateFolderAsync(PluginsFolderName, CreationCollisionOption.OpenIfExists);
            await valorant.CreateFolderAsync("shared", CreationCollisionOption.OpenIfExists);
            return valorant;
        }

        public static string GetExternalEmblemUri(ValorantPackInfo pack)
        {
            if (pack == null || !IsSafeFileName(pack.Key) || !IsSafeFileName(pack.EmblemFile))
            {
                return null;
            }

            string emblemPath = Path.Combine(
                GetPluginsRootPath(),
                pack.Key,
                TextureFolderName,
                pack.EmblemFile);
            if (!File.Exists(emblemPath))
            {
                return null;
            }

            return "ms-appdata:///local/Packs/valorant/plugins/"
                + pack.Key
                + "/textures/"
                + pack.EmblemFile;
        }

        private static string GetValorantRootPath()
        {
            return Path.Combine(
                ApplicationData.Current.LocalFolder.Path,
                PacksFolderName,
                ValorantFolderName);
        }

        private static bool TryCreatePackInfo(
            string folderName,
            string folderPath,
            ValorantExternalPackManifest manifest,
            out ValorantPackInfo pack)
        {
            pack = null;
            if (manifest == null
                || manifest.FormatVersion != 1
                || manifest.Profile == null
                || !IsSafePackKey(manifest.Id)
                || !string.Equals(folderName, manifest.Id, StringComparison.OrdinalIgnoreCase)
                || !IsSafeFileName(manifest.Profile.Emblem)
                || !IsSafeFileName(manifest.Profile.Bar)
                || !IsSafeFileName(manifest.Profile.BarHover)
                || !IsOptionalSafeFileName(manifest.Profile.Frame)
                || !IsOptionalSafeFileName(manifest.Profile.Ring)
                || !IsOptionalSafeFileName(manifest.Profile.FrameDissolve)
                || !IsOptionalSafeFileName(manifest.Profile.BadgeDissolve)
                || !IsOptionalSafeFileName(manifest.Profile.Blade)
                || !IsOptionalSafeFileName(manifest.Profile.SpecialFrame)
                || !IsValidAccent(manifest.Profile.Accent)
                || !ProfileTexturesExist(folderPath, manifest.Profile))
            {
                return false;
            }

            string displayName = string.IsNullOrWhiteSpace(manifest.DisplayName)
                ? manifest.Id
                : manifest.DisplayName.Trim();
            string chineseName = string.IsNullOrWhiteSpace(manifest.DisplayNameZhCn)
                ? displayName
                : manifest.DisplayNameZhCn.Trim();
            bool hasAudio = Directory.Exists(Path.Combine(folderPath, AudioFolderName))
                && Directory.EnumerateFiles(Path.Combine(folderPath, AudioFolderName))
                    .Any(path => IsSupportedAudioExtension(Path.GetExtension(path)));

            pack = new ValorantPackInfo
            {
                Key = manifest.Id.ToLowerInvariant(),
                Folder = "plugins/" + folderName,
                DisplayName = displayName,
                ChineseDisplayName = chineseName,
                EmblemFile = manifest.Profile.Emblem,
                HasBuiltInAudio = hasAudio,
                IsExternal = true,
                Profile = new ValorantVisualProfileInfo
                {
                    Accent = manifest.Profile.Accent,
                    Emblem = manifest.Profile.Emblem,
                    Frame = manifest.Profile.Frame,
                    Bar = manifest.Profile.Bar,
                    BarHover = manifest.Profile.BarHover,
                    Ring = manifest.Profile.Ring,
                    FrameDissolve = manifest.Profile.FrameDissolve,
                    BadgeDissolve = manifest.Profile.BadgeDissolve,
                    Blade = manifest.Profile.Blade,
                    SpecialFrame = manifest.Profile.SpecialFrame,
                    HeadshotX = manifest.Profile.HeadshotX,
                    HeadshotY = manifest.Profile.HeadshotY,
                    SliceSize = manifest.Profile.SliceSize > 0 ? manifest.Profile.SliceSize : 147.0
                }
            };
            return true;
        }

        private static async Task<StorageFile> TryGetFileUnderRootAsync(string root, string candidate)
        {
            try
            {
                string fullPath = Path.GetFullPath(candidate);
                if (!IsChildPath(root, fullPath))
                {
                    return null;
                }

                return await StorageFile.GetFileFromPathAsync(fullPath);
            }
            catch
            {
                return null;
            }
        }

        private static bool IsSupportedAudioFile(StorageFile file)
        {
            return file != null && IsSupportedAudioExtension(file.FileType);
        }

        private static bool ProfileTexturesExist(
            string folderPath,
            ValorantExternalProfileManifest profile)
        {
            string textureRoot = Path.Combine(folderPath, TextureFolderName);
            string[] required =
            {
                profile.Emblem,
                profile.Bar,
                profile.BarHover,
                profile.Frame,
                profile.Ring,
                profile.FrameDissolve,
                profile.BadgeDissolve,
                profile.Blade,
                profile.SpecialFrame
            };
            return required
                .Where(fileName => !string.IsNullOrWhiteSpace(fileName))
                .All(fileName => File.Exists(Path.Combine(textureRoot, fileName)));
        }

        private static bool IsSupportedAudioExtension(string extension)
        {
            return string.Equals(extension, ".wav", StringComparison.OrdinalIgnoreCase)
                || string.Equals(extension, ".ogg", StringComparison.OrdinalIgnoreCase)
                || string.Equals(extension, ".mp3", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsSafePackKey(string key)
        {
            return IsSafeFileName(key)
                && key.StartsWith("valorant_", StringComparison.OrdinalIgnoreCase)
                && key.All(character => char.IsLetterOrDigit(character)
                    || character == '_'
                    || character == '-');
        }

        private static bool IsOptionalSafeFileName(string fileName)
        {
            return string.IsNullOrWhiteSpace(fileName) || IsSafeFileName(fileName);
        }

        private static bool IsValidAccent(string accent)
        {
            if (string.IsNullOrWhiteSpace(accent)
                || accent.Length != 7
                || accent[0] != '#')
            {
                return false;
            }

            return accent.Skip(1).All(character =>
                (character >= '0' && character <= '9')
                || (character >= 'a' && character <= 'f')
                || (character >= 'A' && character <= 'F'));
        }

        private static bool IsSafeRelativePath(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath)
                || Path.IsPathRooted(relativePath)
                || relativePath.IndexOf(':') >= 0)
            {
                return false;
            }

            return relativePath
                .Replace('\\', '/')
                .Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries)
                .All(segment => segment != "." && segment != "..");
        }

        private static bool IsSafeFileName(string fileName)
        {
            return !string.IsNullOrWhiteSpace(fileName)
                && fileName.IndexOfAny(Path.GetInvalidFileNameChars()) < 0
                && fileName.IndexOf('/') < 0
                && fileName.IndexOf('\\') < 0
                && fileName.IndexOf(':') < 0
                && fileName != "."
                && fileName != "..";
        }

        private static bool IsChildPath(string root, string candidate)
        {
            string normalizedRoot = Path.GetFullPath(root)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            return candidate.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
        }

        [DataContract]
        private sealed class ValorantExternalPackManifest
        {
            [DataMember(Name = "format_version")]
            public int FormatVersion { get; set; }

            [DataMember(Name = "id")]
            public string Id { get; set; }

            [DataMember(Name = "display_name")]
            public string DisplayName { get; set; }

            [DataMember(Name = "display_name_zh_cn")]
            public string DisplayNameZhCn { get; set; }

            [DataMember(Name = "profile")]
            public ValorantExternalProfileManifest Profile { get; set; }
        }

        [DataContract]
        private sealed class ValorantExternalProfileManifest
        {
            [DataMember(Name = "accent")]
            public string Accent { get; set; }

            [DataMember(Name = "emblem")]
            public string Emblem { get; set; }

            [DataMember(Name = "frame")]
            public string Frame { get; set; }

            [DataMember(Name = "bar")]
            public string Bar { get; set; }

            [DataMember(Name = "bar_hover")]
            public string BarHover { get; set; }

            [DataMember(Name = "ring")]
            public string Ring { get; set; }

            [DataMember(Name = "frame_dissolve")]
            public string FrameDissolve { get; set; }

            [DataMember(Name = "badge_dissolve")]
            public string BadgeDissolve { get; set; }

            [DataMember(Name = "blade")]
            public string Blade { get; set; }

            [DataMember(Name = "special_frame")]
            public string SpecialFrame { get; set; }

            [DataMember(Name = "headshot_x")]
            public double HeadshotX { get; set; }

            [DataMember(Name = "headshot_y")]
            public double HeadshotY { get; set; }

            [DataMember(Name = "slice_size")]
            public double SliceSize { get; set; }
        }
    }
}

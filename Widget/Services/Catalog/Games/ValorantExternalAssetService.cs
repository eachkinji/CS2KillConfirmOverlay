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
    /// Discovers and installs split VALORANT resource packages. Icon and voice
    /// packages have independent ids and share association_id.
    /// </summary>
    internal static class ValorantExternalAssetService
    {
        public const string IconPackageKind = "valorant_icon";
        public const string VoicePackageKind = "valorant_voice";
        private const string TextureFolderName = "textures";

        public static async Task<StorageFile> TryGetVisualTextureAsync(
            string packKey,
            string themeFolder,
            string fileName)
        {
            if (!IsSafePackKey(packKey) || !IsSafeFileName(fileName))
            {
                return null;
            }

            string candidate = Path.Combine(GetIconPacksRootPath(), packKey, TextureFolderName, fileName);
            return await TryGetFileUnderRootAsync(GetIconPacksRootPath(), candidate);
        }

        public static IReadOnlyList<ValorantPackInfo> DiscoverExternalPacks()
        {
            var result = new List<ValorantPackInfo>();
            foreach (string folderPath in EnumeratePackageFolders(GetIconPacksRootPath()))
            {
                try
                {
                    ValorantExternalPackManifest manifest = ReadManifest(folderPath);
                    if (TryCreateIconPackInfo(folderPath, manifest, out ValorantPackInfo pack))
                    {
                        result.Add(pack);
                    }
                }
                catch (Exception ex)
                {
                    App.Log("Skipped invalid VALORANT icon package: " + ex.Message);
                }
            }
            return result.OrderBy(pack => pack.DisplayName, StringComparer.OrdinalIgnoreCase).ToList();
        }

        public static IReadOnlyList<VoicePackItem> DiscoverExternalVoicePacks()
        {
            var result = new List<VoicePackItem>();
            foreach (string folderPath in EnumeratePackageFolders(GetVoicePacksRootPath()))
            {
                try
                {
                    ValorantExternalPackManifest manifest = ReadManifest(folderPath);
                    string folderName = Path.GetFileName(folderPath);
                    if (!IsValidCommonManifest(manifest, folderName, VoicePackageKind)
                        || !string.Equals(manifest.GameStyle, "valorant", StringComparison.OrdinalIgnoreCase)
                        || !Directory.EnumerateFiles(folderPath).Any(path => IsSupportedAudioExtension(Path.GetExtension(path))))
                    {
                        continue;
                    }

                    result.Add(new VoicePackItem
                    {
                        Key = manifest.Id.ToLowerInvariant(),
                        DisplayName = LocalizedDisplayName(manifest),
                        FolderPath = folderPath,
                        IsBuiltIn = false,
                        IsVisibleInWidget = true,
                        OwnsFolder = true,
                        AssociationId = manifest.AssociationId.Trim().ToLowerInvariant()
                    });
                }
                catch (Exception ex)
                {
                    App.Log("Skipped invalid VALORANT voice package: " + ex.Message);
                }
            }
            return result.OrderBy(pack => pack.DisplayName, StringComparer.OrdinalIgnoreCase).ToList();
        }

        public static string GetAssociationIdForVoicePack(string key)
        {
            if (string.Equals(key, ValorantPackService.DefaultKey, StringComparison.OrdinalIgnoreCase))
            {
                return "valorant:base";
            }

            return DiscoverExternalVoicePacks().FirstOrDefault(pack =>
                string.Equals(pack.Key, key, StringComparison.OrdinalIgnoreCase))?.AssociationId;
        }

        public static string FindVoicePackKeyByAssociation(string associationId)
        {
            if (string.Equals(associationId, "valorant:base", StringComparison.OrdinalIgnoreCase))
            {
                return ValorantPackService.DefaultKey;
            }

            return DiscoverExternalVoicePacks().FirstOrDefault(pack =>
                string.Equals(pack.AssociationId, associationId, StringComparison.OrdinalIgnoreCase))?.Key;
        }

        public static string FindIconPackKeyByAssociation(string associationId)
        {
            return ValorantPackService.All.FirstOrDefault(pack =>
                string.Equals(pack.AssociationId, associationId, StringComparison.OrdinalIgnoreCase))?.Key;
        }

        public static async Task<ValorantPackageInstallResult> InstallPackageAsync(
            StorageFolder sourceFolder,
            string expectedKind)
        {
            if (sourceFolder == null
                || (!string.Equals(expectedKind, IconPackageKind, StringComparison.Ordinal)
                    && !string.Equals(expectedKind, VoicePackageKind, StringComparison.Ordinal)))
            {
                throw new InvalidDataException("Invalid VALORANT package request.");
            }

            StorageFolder packageFolder = await FindManifestFolderAsync(sourceFolder);
            if (packageFolder == null)
            {
                throw new InvalidDataException("The package does not contain manifest.json.");
            }

            ValorantExternalPackManifest manifest = ReadManifest(packageFolder.Path);
            if (!IsValidCommonManifest(manifest, packageFolder.Name, expectedKind, requireFolderNameMatch: false))
            {
                throw new InvalidDataException("The VALORANT package manifest is invalid or has the wrong package_kind.");
            }

            string targetRootPath = string.Equals(expectedKind, IconPackageKind, StringComparison.Ordinal)
                ? GetIconPacksRootPath()
                : GetVoicePacksRootPath();
            Directory.CreateDirectory(targetRootPath);
            string targetPath = Path.GetFullPath(Path.Combine(targetRootPath, manifest.Id));
            if (!IsChildPath(targetRootPath, targetPath))
            {
                throw new InvalidDataException("The package id resolves outside the package library.");
            }

            if (string.Equals(expectedKind, IconPackageKind, StringComparison.Ordinal))
            {
                if (!TryCreateIconPackInfo(packageFolder.Path, manifest, out _, requireFolderNameMatch: false))
                {
                    throw new InvalidDataException("The icon package is missing one or more profile textures.");
                }
            }
            else if (!string.Equals(manifest.GameStyle, "valorant", StringComparison.OrdinalIgnoreCase)
                || !(await packageFolder.GetFilesAsync()).Any(IsSupportedAudioFile))
            {
                throw new InvalidDataException("The voice package contains no supported audio files.");
            }

            StorageFolder targetRoot = await StorageFolder.GetFolderFromPathAsync(targetRootPath);
            StorageFolder target = await targetRoot.CreateFolderAsync(
                manifest.Id,
                CreationCollisionOption.ReplaceExisting);
            await CopyFolderContentsAsync(packageFolder, target);

            return new ValorantPackageInstallResult
            {
                Id = manifest.Id.ToLowerInvariant(),
                AssociationId = manifest.AssociationId.Trim().ToLowerInvariant(),
                DisplayName = LocalizedDisplayName(manifest),
                PackageKind = expectedKind
            };
        }

        public static async Task<bool> IsPackageKindAsync(StorageFolder sourceFolder, string expectedKind)
        {
            if (sourceFolder == null
                || (!string.Equals(expectedKind, IconPackageKind, StringComparison.Ordinal)
                    && !string.Equals(expectedKind, VoicePackageKind, StringComparison.Ordinal)))
            {
                return false;
            }

            try
            {
                StorageFolder packageFolder = await FindManifestFolderAsync(sourceFolder);
                if (packageFolder == null)
                {
                    return false;
                }

                ValorantExternalPackManifest manifest = ReadManifest(packageFolder.Path);
                return IsValidCommonManifest(
                    manifest,
                    packageFolder.Name,
                    expectedKind,
                    requireFolderNameMatch: false);
            }
            catch (Exception ex)
            {
                App.Log("VALORANT package type detection failed: " + ex.Message);
                return false;
            }
        }

        public static string GetIconPacksRootPath()
        {
            return Path.Combine(GetValorantRootPath(), "icon_packs");
        }

        public static string GetVoicePacksRootPath()
        {
            return Path.Combine(GetValorantRootPath(), "voice_packs");
        }

        public static async Task<StorageFolder> GetExternalAssetsFolderAsync()
        {
            StorageFolder packs = await ApplicationData.Current.LocalFolder.CreateFolderAsync(
                "Packs", CreationCollisionOption.OpenIfExists);
            StorageFolder valorant = await packs.CreateFolderAsync(
                "valorant", CreationCollisionOption.OpenIfExists);
            await valorant.CreateFolderAsync("icon_packs", CreationCollisionOption.OpenIfExists);
            await valorant.CreateFolderAsync("voice_packs", CreationCollisionOption.OpenIfExists);
            return valorant;
        }

        public static string GetExternalEmblemUri(ValorantPackInfo pack)
        {
            if (pack == null || !pack.IsExternal || !IsSafePackKey(pack.Key) || !IsSafeFileName(pack.EmblemFile))
            {
                return null;
            }

            string emblemPath = Path.Combine(GetIconPacksRootPath(), pack.Key, TextureFolderName, pack.EmblemFile);
            if (!File.Exists(emblemPath))
            {
                return null;
            }

            return "ms-appdata:///local/Packs/valorant/icon_packs/"
                + pack.Key + "/textures/" + pack.EmblemFile;
        }

        private static string GetValorantRootPath()
        {
            return Path.Combine(ApplicationData.Current.LocalFolder.Path, "Packs", "valorant");
        }

        private static IEnumerable<string> EnumeratePackageFolders(string root)
        {
            return Directory.Exists(root) ? Directory.EnumerateDirectories(root) : Enumerable.Empty<string>();
        }

        private static ValorantExternalPackManifest ReadManifest(string folderPath)
        {
            string manifestPath = Path.Combine(folderPath, "manifest.json");
            if (!File.Exists(manifestPath))
            {
                return null;
            }

            using (var stream = File.OpenRead(manifestPath))
            {
                var serializer = new DataContractJsonSerializer(typeof(ValorantExternalPackManifest));
                return serializer.ReadObject(stream) as ValorantExternalPackManifest;
            }
        }

        private static bool TryCreateIconPackInfo(
            string folderPath,
            ValorantExternalPackManifest manifest,
            out ValorantPackInfo pack,
            bool requireFolderNameMatch = true)
        {
            pack = null;
            string folderName = Path.GetFileName(folderPath);
            if (!IsValidCommonManifest(manifest, folderName, IconPackageKind, requireFolderNameMatch)
                || manifest.Profile == null
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

            pack = new ValorantPackInfo
            {
                Key = manifest.Id.ToLowerInvariant(),
                Folder = "external/" + manifest.Id,
                FolderPath = folderPath,
                DisplayName = LocalizedDisplayName(manifest),
                ChineseDisplayName = manifest.DisplayNameZhCn,
                EmblemFile = manifest.Profile.Emblem,
                HasBuiltInAudio = false,
                IsExternal = true,
                AssociationId = manifest.AssociationId.Trim().ToLowerInvariant(),
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

        private static bool IsValidCommonManifest(
            ValorantExternalPackManifest manifest,
            string folderName,
            string expectedKind,
            bool requireFolderNameMatch = true)
        {
            return manifest != null
                && manifest.FormatVersion == 2
                && string.Equals(manifest.PackageKind, expectedKind, StringComparison.Ordinal)
                && IsSafePackKey(manifest.Id)
                && IsSafeAssociationId(manifest.AssociationId)
                && (!requireFolderNameMatch || string.Equals(folderName, manifest.Id, StringComparison.OrdinalIgnoreCase));
        }

        private static string LocalizedDisplayName(ValorantExternalPackManifest manifest)
        {
            string fallback = string.IsNullOrWhiteSpace(manifest.DisplayName) ? manifest.Id : manifest.DisplayName.Trim();
            return LocalizationManager.Current == UiLanguage.SimplifiedChinese
                && !string.IsNullOrWhiteSpace(manifest.DisplayNameZhCn)
                ? manifest.DisplayNameZhCn.Trim()
                : fallback;
        }

        private static async Task<StorageFolder> FindManifestFolderAsync(StorageFolder root)
        {
            if (await root.TryGetItemAsync("manifest.json") is StorageFile)
            {
                return root;
            }
            foreach (StorageFolder child in await root.GetFoldersAsync())
            {
                StorageFolder found = await FindManifestFolderAsync(child);
                if (found != null)
                {
                    return found;
                }
            }
            return null;
        }

        private static async Task CopyFolderContentsAsync(StorageFolder source, StorageFolder target)
        {
            foreach (StorageFile file in await source.GetFilesAsync())
            {
                await file.CopyAsync(target, file.Name, NameCollisionOption.ReplaceExisting);
            }
            foreach (StorageFolder child in await source.GetFoldersAsync())
            {
                StorageFolder targetChild = await target.CreateFolderAsync(
                    child.Name, CreationCollisionOption.ReplaceExisting);
                await CopyFolderContentsAsync(child, targetChild);
            }
        }

        private static async Task<StorageFile> TryGetFileUnderRootAsync(string root, string candidate)
        {
            try
            {
                string fullPath = Path.GetFullPath(candidate);
                return IsChildPath(root, fullPath) ? await StorageFile.GetFileFromPathAsync(fullPath) : null;
            }
            catch
            {
                return null;
            }
        }

        private static bool ProfileTexturesExist(string folderPath, ValorantExternalProfileManifest profile)
        {
            string textureRoot = Path.Combine(folderPath, TextureFolderName);
            return new[]
                {
                    profile.Emblem, profile.Bar, profile.BarHover, profile.Frame, profile.Ring,
                    profile.FrameDissolve, profile.BadgeDissolve, profile.Blade, profile.SpecialFrame
                }
                .Where(fileName => !string.IsNullOrWhiteSpace(fileName))
                .All(fileName => File.Exists(Path.Combine(textureRoot, fileName)));
        }

        private static bool IsSupportedAudioFile(StorageFile file)
        {
            return file != null && IsSupportedAudioExtension(file.FileType);
        }

        private static bool IsSupportedAudioExtension(string extension)
        {
            return string.Equals(extension, ".wav", StringComparison.OrdinalIgnoreCase)
                || string.Equals(extension, ".ogg", StringComparison.OrdinalIgnoreCase)
                || string.Equals(extension, ".mp3", StringComparison.OrdinalIgnoreCase)
                || string.Equals(extension, ".m4a", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsSafePackKey(string key)
        {
            return IsSafeFileName(key)
                && key.StartsWith("valorant_", StringComparison.OrdinalIgnoreCase)
                && key.All(character => char.IsLetterOrDigit(character) || character == '_' || character == '-');
        }

        private static bool IsSafeAssociationId(string value)
        {
            return !string.IsNullOrWhiteSpace(value)
                && value.Length <= 160
                && value.All(character => char.IsLetterOrDigit(character)
                    || character == '_' || character == '-' || character == ':' || character == '.');
        }

        private static bool IsOptionalSafeFileName(string fileName)
        {
            return string.IsNullOrWhiteSpace(fileName) || IsSafeFileName(fileName);
        }

        private static bool IsValidAccent(string accent)
        {
            return !string.IsNullOrWhiteSpace(accent)
                && accent.Length == 7
                && accent[0] == '#'
                && accent.Skip(1).All(Uri.IsHexDigit);
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
            [DataMember(Name = "package_kind")]
            public string PackageKind { get; set; }
            [DataMember(Name = "id")]
            public string Id { get; set; }
            [DataMember(Name = "association_id")]
            public string AssociationId { get; set; }
            [DataMember(Name = "display_name")]
            public string DisplayName { get; set; }
            [DataMember(Name = "display_name_zh_cn")]
            public string DisplayNameZhCn { get; set; }
            [DataMember(Name = "game_style")]
            public string GameStyle { get; set; }
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

    internal sealed class ValorantPackageInstallResult
    {
        public string Id { get; set; }
        public string AssociationId { get; set; }
        public string DisplayName { get; set; }
        public string PackageKind { get; set; }
    }
}

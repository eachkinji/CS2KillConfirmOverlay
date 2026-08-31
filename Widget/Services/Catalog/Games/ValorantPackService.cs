using System;
using System.Collections.Generic;
using System.Linq;

namespace KillConfirmGameBar.Services
{
    internal sealed class ValorantPackInfo
    {
        public string Key { get; set; }
        public string Folder { get; set; }
        public string DisplayName { get; set; }
        public string ChineseDisplayName { get; set; }

        /// <summary>
        /// Emblem texture file name inside the native theme's textures folder.
        /// </summary>
        public string EmblemFile { get; set; }

        /// <summary>
        /// Whether the native audio shipped with this visual pack is available
        /// in the core application. Only Base remains built in.
        /// </summary>
        public bool HasBuiltInAudio { get; set; }
        public bool IsExternal { get; set; }
        public string AssociationId { get; set; }
        public string FolderPath { get; set; }
        public ValorantVisualProfileInfo Profile { get; set; }
    }

    internal sealed class ValorantVisualProfileInfo
    {
        public string Accent { get; set; }
        public string Emblem { get; set; }
        public string Frame { get; set; }
        public string Bar { get; set; }
        public string BarHover { get; set; }
        public string Ring { get; set; }
        public string FrameDissolve { get; set; }
        public string BadgeDissolve { get; set; }
        public string Blade { get; set; }
        public string SpecialFrame { get; set; }
        public double HeadshotX { get; set; }
        public double HeadshotY { get; set; }
        public double SliceSize { get; set; }
    }

    internal static class ValorantPackService
    {
        public const string DefaultKey = "valorant_00000_base";

        // The public keys remain stable for saved settings. Their folders now point
        // directly at the replacement tree built from the cooked VALORANT exports.
        private static readonly ValorantPackInfo[] BuiltInPacks =
        {
            Pack("00000_base", "_native/themes/Base", "Base", "Base_Emblem.png", hasBuiltInAudio: true)
        };

        private static readonly object PacksLock = new object();
        private static IReadOnlyList<ValorantPackInfo> _packs = BuiltInPacks;

        public static IReadOnlyList<ValorantPackInfo> All => _packs;

        public static void RefreshExternalPacks()
        {
            IReadOnlyList<ValorantPackInfo> discovered = ValorantExternalAssetService.DiscoverExternalPacks();
            var builtInKeys = new HashSet<string>(
                BuiltInPacks.Select(pack => pack.Key),
                StringComparer.OrdinalIgnoreCase);
            ValorantPackInfo[] combined = BuiltInPacks
                .Concat(discovered.Where(pack => !builtInKeys.Contains(pack.Key)))
                .ToArray();
            lock (PacksLock)
            {
                _packs = combined;
            }
        }

        public static bool IsValorantPackKey(string key)
        {
            return !string.IsNullOrWhiteSpace(key)
                && key.Trim().StartsWith("valorant_", StringComparison.OrdinalIgnoreCase);
        }

        public static ValorantPackInfo Find(string key)
        {
            return All.FirstOrDefault(pack =>
                string.Equals(pack.Key, key, StringComparison.OrdinalIgnoreCase));
        }

        public static string GetFolder(string key)
        {
            return Find(key)?.Folder;
        }

        public static string GetDisplayName(string key)
        {
            ValorantPackInfo pack = Find(key);
            if (pack == null)
            {
                return key;
            }

            string localized = LocalizationManager.Text(pack.Key);
            if (!string.Equals(localized, pack.Key, StringComparison.Ordinal))
            {
                return localized;
            }

            return pack.IsExternal
                && LocalizationManager.Current == UiLanguage.SimplifiedChinese
                && !string.IsNullOrWhiteSpace(pack.ChineseDisplayName)
                ? pack.ChineseDisplayName
                : pack.DisplayName;
        }

        public static string GetEmblemFile(string key)
        {
            return Find(key)?.EmblemFile;
        }

        public static string GetEmblemUri(string key)
        {
            ValorantPackInfo pack = Find(key);
            string externalUri = ValorantExternalAssetService.GetExternalEmblemUri(pack);
            if (!string.IsNullOrWhiteSpace(externalUri))
            {
                return externalUri;
            }

            return pack == null || string.IsNullOrWhiteSpace(pack.EmblemFile)
                ? null
                : $"ms-appx:///Assets/GameStyles/valorant/killconfirm/{pack.Folder}/textures/{pack.EmblemFile}";
        }

        public static int GetDisplayOrder(string key)
        {
            IReadOnlyList<ValorantPackInfo> packs = All;
            int index = -1;
            for (int candidate = 0; candidate < packs.Count; candidate++)
            {
                if (string.Equals(packs[candidate].Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    index = candidate;
                    break;
                }
            }
            return index < 0 ? int.MaxValue : index;
        }

        private static ValorantPackInfo Pack(
            string keySuffix,
            string folder,
            string displayName,
            string emblemFile,
            bool hasBuiltInAudio = true)
        {
            return new ValorantPackInfo
            {
                Key = "valorant_" + keySuffix,
                Folder = folder,
                DisplayName = displayName,
                EmblemFile = emblemFile,
                HasBuiltInAudio = hasBuiltInAudio,
                AssociationId = "valorant:base"
            };
        }
    }
}

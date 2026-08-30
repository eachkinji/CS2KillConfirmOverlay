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
        /// Base is a visual-only selection. It has no matching six-slot audio
        /// set, so it must not be offered as a voice preset.
        /// </summary>
        public bool HasBuiltInAudio { get; set; }
        public bool IsExternal { get; set; }
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
        public const string DefaultKey = "valorant_00011_singularity_v1";

        // The public keys remain stable for saved settings. Their folders now point
        // directly at the replacement tree built from the cooked VALORANT exports.
        private static readonly ValorantPackInfo[] BuiltInPacks =
        {
            Pack("00000_base", "_native/themes/Base", "Base", "Base_Emblem.png", hasBuiltInAudio: false),
            Pack("00011_singularity_v1", "_native/themes/Edge02", "Singularity (Variant 1)", "Edge_EmblemV1.png"),
            Pack("00012_singularity_v2", "_native/themes/Edge02", "Singularity (Variant 2)", "Edge_EmblemV2.png"),
            Pack("00013_singularity_v3", "_native/themes/Edge02", "Singularity (Variant 3)", "Edge_EmblemV3.png"),
            Pack("00014_gaia_s_vengeance", "_native/themes/Ashen", "Gaia's Vengeance", "Ashen_Emblem.png"),
            Pack("00015_gaia_s_vengeance_v1", "_native/themes/Ashen", "Gaia's Vengeance (Variant 1 Blue)", "Ashen_Emblem_v1.png"),
            Pack("00016_gaia_s_vengeance_v2", "_native/themes/Ashen", "Gaia's Vengeance (Variant 2 Green)", "Ashen_Emblem_v2.png"),
            Pack("00017_gaia_s_vengeance_v3", "_native/themes/Ashen", "Gaia's Vengeance (Variant 3 Orange)", "Ashen_Emblem_v3.png"),
            Pack("00018_bubblegum_deathwish", "_native/themes/Hazard", "Bubblegum Deathwish", "Hazard_Emblem_Standard.png"),
            Pack("00019_bubblegum_deathwish_v1", "_native/themes/Hazard", "Bubblegum Deathwish (Variant 1 Yellow)", "Hazard_Emblem_Yellow.png"),
            Pack("00020_bubblegum_deathwish_v2", "_native/themes/Hazard", "Bubblegum Deathwish (Variant 2 Red)", "Hazard_Emblem_Red.png"),
            Pack("00021_bubblegum_deathwish_v3", "_native/themes/Hazard", "Bubblegum Deathwish (Variant 3)", "Hazard_Emblem_Green.png"),
            Pack("00022_champions_2021", "_native/themes/Esports", "Champions 2021", "Esports_Emblem.png"),
            Pack("00023_prelude_to_chaos_v1", "_native/themes/DemonStone", "Prelude to Chaos (Variant 1)", "Demonstone_Emblem_v1.png"),
            Pack("00024_prelude_to_chaos_v2", "_native/themes/DemonStone", "Prelude to Chaos (Variant 2)", "Demonstone_Emblem_v2.png"),
            Pack("00025_prelude_to_chaos_v3", "_native/themes/DemonStone", "Prelude to Chaos (Variant 3)", "Demonstone_Emblem_v3.png"),
            Pack("00026_primordium", "_native/themes/Hellfire", "Primordium", "Hellfire_Emblem.png"),
            Pack("00027_primordium_v1", "_native/themes/Hellfire", "Primordium (Variant 1 Venom)", "Hellfire_Emblem_V1.png"),
            Pack("00028_primordium_v2", "_native/themes/Hellfire", "Primordium (Variant 2 Cobalt)", "Hellfire_Emblem_V2.png"),
            Pack("00029_primordium_v3", "_native/themes/Hellfire", "Primordium (Variant 3 Gold)", "Hellfire_Emblem_V3.png"),
            Pack("00030_radiant_crisis_001", "_native/themes/Comicbook", "Radiant Crisis 001", "ComicBook_Emblem.png"),
            Pack("00031_rgx_11z_pro", "_native/themes/Afterglow", "RGX 11z Pro", "Afterglow_Emblem.png"),
            Pack("00032_rgx_11z_pro_v1", "_native/themes/Afterglow2", "RGX 11z Pro (Variant 1 Red)", "Afterglow_Emblem_v1.png"),
            Pack("00033_rgx_11z_pro_v2", "_native/themes/Afterglow2", "RGX 11z Pro (Variant 2 Blue)", "Afterglow_Emblem_v2.png"),
            Pack("00034_rgx_11z_pro_v3", "_native/themes/Afterglow2", "RGX 11z Pro (Variant 3 Yellow)", "Afterglow_Emblem_v3.png"),
            Pack("00009_prime", "_native/themes/HypeBeast", "Prime", "HypeBeast_Emblem.png"),
            Pack("00010_glitchpop", "_native/themes/Cyberpunk", "Glitchpop", "Cyberpunk_Emblem.png")
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
                HasBuiltInAudio = hasBuiltInAudio
            };
        }
    }
}

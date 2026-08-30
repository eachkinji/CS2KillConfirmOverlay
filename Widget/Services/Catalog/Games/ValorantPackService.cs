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

        /// <summary>
        /// Emblem texture file name inside the pack's textures/ folder. Each Valorant
        /// pack ships a distinct emblem (the pack's identifying badge); the file name is
        /// not derivable from the folder (e.g. 00014_gaia_s_vengeance uses
        /// killicon_valorant_gaia_emblem.png, and the bubblegum v1/v3 emblem files are
        /// swapped), so it is declared explicitly per pack.
        /// </summary>
        public string EmblemFile { get; set; }
    }

    internal static class ValorantPackService
    {
        public const string DefaultKey = "valorant_00011_singularity_v1";

        // Pack(folder, displayName, emblemFile). The emblem file lives at
        // Assets/GameStyles/valorant/killconfirm/{folder}/textures/{emblemFile}.
        private static readonly ValorantPackInfo[] Packs =
        {
            Pack("00011_singularity_v1", "Singularity V1", "killicon_valorant_singularity_v1_emblem.png"),
            Pack("00012_singularity_v2", "Singularity V2", "killicon_valorant_singularity_v2_emblem.png"),
            Pack("00013_singularity_v3", "Singularity V3", "killicon_valorant_singularity_v3_emblem.png"),
            Pack("00014_gaia_s_vengeance", "Gaia's Vengeance", "killicon_valorant_gaia_emblem.png"),
            Pack("00015_gaia_s_vengeance_v1", "Gaia's Vengeance V1", "killicon_valorant_gaia_v1_emblem.png"),
            Pack("00016_gaia_s_vengeance_v2", "Gaia's Vengeance V2", "killicon_valorant_gaia_v2_emblem.png"),
            Pack("00017_gaia_s_vengeance_v3", "Gaia's Vengeance V3", "killicon_valorant_gaia_v3_emblem.png"),
            Pack("00018_bubblegum_deathwish", "Bubblegum Deathwish", "killicon_valorant_bubblegum_deathwish_emblem.png"),
            Pack("00019_bubblegum_deathwish_v1", "Bubblegum Deathwish V1", "killicon_valorant_bubblegum_deathwish_v3_emblem.png"),
            Pack("00020_bubblegum_deathwish_v2", "Bubblegum Deathwish V2", "killicon_valorant_bubblegum_deathwish_v2_emblem.png"),
            Pack("00021_bubblegum_deathwish_v3", "Bubblegum Deathwish V3", "killicon_valorant_bubblegum_deathwish_v1_emblem.png"),
            Pack("00022_champions_2021", "Champions 2021", "killicon_valorant_champions_2021_emblem.png"),
            Pack("00023_prelude_to_chaos_v1", "Prelude to Chaos V1", "killicon_valorant_prelude_to_chaos_v1_emblem.png"),
            Pack("00024_prelude_to_chaos_v2", "Prelude to Chaos V2", "killicon_valorant_prelude_to_chaos_v2_emblem.png"),
            Pack("00025_prelude_to_chaos_v3", "Prelude to Chaos V3", "killicon_valorant_prelude_to_chaos_v3_emblem.png"),
            Pack("00026_primordium", "Primordium", "killicon_valorant_primordium_emblem.png"),
            Pack("00027_primordium_v1", "Primordium V1", "killicon_valorant_primordium_v1_emblem.png"),
            Pack("00028_primordium_v2", "Primordium V2", "killicon_valorant_primordium_v2_emblem.png"),
            Pack("00029_primordium_v3", "Primordium V3", "killicon_valorant_primordium_v3_emblem.png"),
            Pack("00030_radiant_crisis_001", "Radiant Crisis 001", "killicon_valorant_radiant_crisis_001_emblem.png"),
            Pack("00031_rgx_11z_pro", "RGX 11z Pro", "killicon_valorant_rgx_11z_pro_emblem.png"),
            Pack("00032_rgx_11z_pro_v1", "RGX 11z Pro V1", "killicon_valorant_rgx_11z_pro_v1_emblem.png"),
            Pack("00033_rgx_11z_pro_v2", "RGX 11z Pro V2", "killicon_valorant_rgx_11z_pro_v2_emblem.png"),
            Pack("00034_rgx_11z_pro_v3", "RGX 11z Pro V3", "killicon_valorant_rgx_11z_pro_v3_emblem.png"),
            Pack("00009_prime", "Prime", "killicon_valorant_prime_emblem.png"),
            Pack("00010_glitchpop", "Glitchpop", "killicon_valorant_glitchpop_emblem.png")
        };

        public static IReadOnlyList<ValorantPackInfo> All => Packs;

        public static bool IsValorantPackKey(string key)
        {
            return !string.IsNullOrWhiteSpace(key)
                && key.Trim().StartsWith("valorant_", StringComparison.OrdinalIgnoreCase);
        }

        public static ValorantPackInfo Find(string key)
        {
            return Packs.FirstOrDefault(pack =>
                string.Equals(pack.Key, key, StringComparison.OrdinalIgnoreCase));
        }

        public static string GetFolder(string key)
        {
            return Find(key)?.Folder;
        }

        public static string GetDisplayName(string key)
        {
            return Find(key)?.DisplayName ?? key;
        }

        public static string GetEmblemFile(string key)
        {
            return Find(key)?.EmblemFile;
        }

        public static int GetDisplayOrder(string key)
        {
            int index = Array.FindIndex(Packs, pack =>
                string.Equals(pack.Key, key, StringComparison.OrdinalIgnoreCase));
            return index < 0 ? int.MaxValue : index;
        }

        private static ValorantPackInfo Pack(string folder, string displayName, string emblemFile)
        {
            return new ValorantPackInfo
            {
                Key = "valorant_" + folder,
                Folder = folder,
                DisplayName = displayName,
                EmblemFile = emblemFile
            };
        }
    }
}

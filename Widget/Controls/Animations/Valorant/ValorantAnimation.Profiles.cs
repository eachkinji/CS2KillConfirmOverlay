using System;
using KillConfirmGameBar.Services;
using Windows.UI;

namespace KillConfirmGameBar.Controls
{
    public sealed partial class KillConfirmAnimation
    {
        private const double ValorantDemoVfxScale = 0.8075;
        private const double ValorantDemoDurationMs = 2600.0;
        private const double ValorantDemoFadeStartMs = 2380.0;
        private const double ValorantDemoFivePlusShowMs = 300.0;
        private const double ValorantDemoFrameCssWidth = 116.0;
        private const double ValorantDemoFrameCssHeight = 116.0;
        private const double ValorantDemoEmblemCssSize = 104.0;
        private const double ValorantDemoBladeCssSize = 70.0;
        private const double ValorantDemoHeadshotCssSize = 19.0;
        private static readonly Color ValorantDemoFlashColor = Color.FromArgb(255, 255, 42, 54);

        private static readonly int[][] ValorantDemoBarAngles =
        {
            new[] { 0 },
            new[] { -90, 90 },
            new[] { 0, -120, 120 },
            new[] { 0, -90, 90, 180 },
            new[] { 0, -72, 72, -144, 144 },
            new[] { 0, -60, 60, -120, 120, 180 }
        };

        private static ValorantDemoProfile GetValorantDemoProfile(string packKey)
        {
            string id = ExtractValorantDemoId(packKey);
            switch (id)
            {
                case "00010":
                    return new ValorantDemoProfile(id, "#2697f5", "killicon_valorant_glitchpop_emblem.png", "killicon_valorant_glitchpop_frame.png", "killicon_valorant_glitchpop_bar.png") { HeadshotY = -16, HeroFlame = false };
                case "00011":
                    return new ValorantDemoProfile(id, "#df7e49", "killicon_valorant_singularity_v1_emblem.png", "killicon_valorant_base_frame.png", "killicon_valorant_singularity_v1_bar.png") { HeadshotY = -10, HeroFlame = false, EmblemScale = 0.9, FrameWidthScale = 0.8 };
                case "00012":
                    return new ValorantDemoProfile(id, "#dcc971", "killicon_valorant_singularity_v2_emblem.png", "killicon_valorant_base_frame.png", "killicon_valorant_singularity_v2_bar.png") { HeadshotY = -10, HeroFlame = false, EmblemScale = 0.9, FrameWidthScale = 0.8 };
                case "00013":
                    return new ValorantDemoProfile(id, "#7e9edc", "killicon_valorant_singularity_v3_emblem.png", "killicon_valorant_base_frame.png", "killicon_valorant_singularity_v3_bar.png") { HeadshotY = -10, HeroFlame = false, EmblemScale = 0.9, FrameWidthScale = 0.8 };
                case "00014":
                    return new ValorantDemoProfile(id, "#f9545e", "killicon_valorant_gaia_emblem.png", "killicon_valorant_gaia_frame.png", "killicon_valorant_gaia_bar.png") { HeadshotX = -2, HeadshotY = -20, EmblemScale = 0.9, BarRadiusOffset = 4, IsGaia = true };
                case "00015":
                    return new ValorantDemoProfile(id, "#287ef3", "killicon_valorant_gaia_v1_emblem.png", "killicon_valorant_gaia_v1_frame.png", "killicon_valorant_gaia_v1_bar.png") { HeadshotX = -2, HeadshotY = -20, EmblemScale = 0.9, BarRadiusOffset = 4, IsGaia = true };
                case "00016":
                    return new ValorantDemoProfile(id, "#27b748", "killicon_valorant_gaia_v2_emblem.png", "killicon_valorant_gaia_v2_frame.png", "killicon_valorant_gaia_v2_bar.png") { HeadshotX = -2, HeadshotY = -20, EmblemScale = 0.9, BarRadiusOffset = 4, IsGaia = true };
                case "00017":
                    return new ValorantDemoProfile(id, "#f77124", "killicon_valorant_gaia_v3_emblem.png", "killicon_valorant_gaia_v3_frame.png", "killicon_valorant_gaia_v3_bar.png") { HeadshotX = -2, HeadshotY = -20, EmblemScale = 0.9, BarRadiusOffset = 4, IsGaia = true };
                case "00018":
                    return new ValorantDemoProfile(id, "#c94fb9", "killicon_valorant_bubblegum_deathwish_emblem.png", "killicon_valorant_bubblegum_deathwish_frame.png", "killicon_valorant_bubblegum_deathwish_bar.png") { Blade = "killicon_valorant_bubblegum_deathwish_blade.png", HeadshotY = -12, EmblemScale = 0.55 };
                case "00019":
                    return new ValorantDemoProfile(id, "#c98e4c", "killicon_valorant_bubblegum_deathwish_v3_emblem.png", "killicon_valorant_bubblegum_deathwish_frame.png", "killicon_valorant_bubblegum_deathwish_v1_bar.png") { Blade = "killicon_valorant_bubblegum_deathwish_blade.png", HeadshotY = -12, EmblemScale = 0.55 };
                case "00020":
                    return new ValorantDemoProfile(id, "#9d332f", "killicon_valorant_bubblegum_deathwish_v2_emblem.png", "killicon_valorant_bubblegum_deathwish_frame.png", "killicon_valorant_bubblegum_deathwish_v2_bar.png") { Blade = "killicon_valorant_bubblegum_deathwish_blade.png", HeadshotY = -12, EmblemScale = 0.55 };
                case "00021":
                    return new ValorantDemoProfile(id, "#6eb037", "killicon_valorant_bubblegum_deathwish_v1_emblem.png", "killicon_valorant_bubblegum_deathwish_frame.png", "killicon_valorant_bubblegum_deathwish_v3_bar.png") { Blade = "killicon_valorant_bubblegum_deathwish_blade.png", HeadshotY = -12, EmblemScale = 0.55 };
                case "00022":
                    return new ValorantDemoProfile(id, "#947046", "killicon_valorant_champions_2021_emblem.png", "killicon_valorant_base_frame.png", "killicon_valorant_champions_2021_bar.png") { HeadshotY = -12, EmblemScale = 0.6, FrameWidthScale = 0.8 };
                case "00023":
                    return new ValorantDemoProfile(id, "#f46e57", "killicon_valorant_prelude_to_chaos_v1_emblem.png", "killicon_valorant_base_frame.png", "killicon_valorant_prelude_to_chaos_v1_bar.png") { HeadshotY = -12, EmblemScale = 0.5, FrameWidthScale = 0.8, BarRadiusOffset = 9 };
                case "00024":
                    return new ValorantDemoProfile(id, "#10c110", "killicon_valorant_prelude_to_chaos_v2_emblem.png", "killicon_valorant_base_frame.png", "killicon_valorant_prelude_to_chaos_v2_bar.png") { HeadshotY = -12, EmblemScale = 0.5, FrameWidthScale = 0.8, BarRadiusOffset = 9 };
                case "00025":
                    return new ValorantDemoProfile(id, "#1168c1", "killicon_valorant_prelude_to_chaos_v3_emblem.png", "killicon_valorant_base_frame.png", "killicon_valorant_prelude_to_chaos_v3_bar.png") { HeadshotY = -12, EmblemScale = 0.5, FrameWidthScale = 0.8, BarRadiusOffset = 9 };
                case "00026":
                    return new ValorantDemoProfile(id, "#8f3e31", "killicon_valorant_primordium_emblem.png", "killicon_valorant_primordium_frame.png", "killicon_valorant_primordium_bar.png") { HeadshotY = -14, EmblemScale = 0.4, HaloRadius = 25 };
                case "00027":
                    return new ValorantDemoProfile(id, "#387a51", "killicon_valorant_primordium_v1_emblem.png", "killicon_valorant_primordium_frame.png", "killicon_valorant_primordium_v1_bar.png") { HeadshotY = -14, EmblemScale = 0.4, HaloRadius = 25 };
                case "00028":
                    return new ValorantDemoProfile(id, "#316884", "killicon_valorant_primordium_v2_emblem.png", "killicon_valorant_primordium_frame.png", "killicon_valorant_primordium_v2_bar.png") { HeadshotY = -14, EmblemScale = 0.4, HaloRadius = 25 };
                case "00029":
                    return new ValorantDemoProfile(id, "#8d6f43", "killicon_valorant_primordium_v3_emblem.png", "killicon_valorant_primordium_frame.png", "killicon_valorant_primordium_v3_bar.png") { HeadshotY = -14, EmblemScale = 0.4, HaloRadius = 25 };
                case "00030":
                    return new ValorantDemoProfile(id, "#73c0c4", "killicon_valorant_radiant_crisis_001_emblem.png", "killicon_valorant_base_frame.png", "killicon_valorant_radiant_crisis_001_bar.png") { HeadshotY = -12, EmblemScale = 0.5, FrameWidthScale = 0.8, HaloRadius = 25 };
                case "00031":
                    return new ValorantDemoProfile(id, "#a4ff96", "killicon_valorant_rgx_11z_pro_emblem.png", "killicon_valorant_rgx_11z_pro_frame.png", "killicon_valorant_rgx_11z_pro_bar.png") { HeadshotX = 0.85, HeadshotY = -21, EmblemScale = 0.35, FrameWidthScale = 0.8, HaloRadius = 25, UsesNativeAfterglowPlayback = ValorantPackService.UsesNativeAfterglowPlayback(packKey) };
                case "00032":
                    return new ValorantDemoProfile(id, "#f3414a", "killicon_valorant_rgx_11z_pro_v1_emblem.png", "killicon_valorant_rgx_11z_pro_frame.png", "killicon_valorant_rgx_11z_pro_v1_bar.png") { HeadshotY = -12, EmblemScale = 0.35, FrameWidthScale = 0.8, HaloRadius = 25 };
                case "00033":
                    return new ValorantDemoProfile(id, "#41baf3", "killicon_valorant_rgx_11z_pro_v2_emblem.png", "killicon_valorant_rgx_11z_pro_frame.png", "killicon_valorant_rgx_11z_pro_v2_bar.png") { HeadshotY = -12, EmblemScale = 0.35, FrameWidthScale = 0.8, HaloRadius = 25 };
                case "00034":
                    return new ValorantDemoProfile(id, "#f3a741", "killicon_valorant_rgx_11z_pro_v3_emblem.png", "killicon_valorant_rgx_11z_pro_frame.png", "killicon_valorant_rgx_11z_pro_v3_bar.png") { HeadshotY = -12, EmblemScale = 0.35, FrameWidthScale = 0.8, HaloRadius = 25 };
                default:
                    return new ValorantDemoProfile("00009", "#908ccd", "killicon_valorant_prime_emblem.png", "killicon_valorant_prime_frame.png", "killicon_valorant_bar.png") { HeadshotY = -16, HeroFlame = false };
            }
        }

        private static string ExtractValorantDemoId(string packKey)
        {
            if (string.IsNullOrWhiteSpace(packKey))
            {
                return "00009";
            }

            int marker = packKey.IndexOf("valorant_", StringComparison.OrdinalIgnoreCase);
            int start = marker >= 0 ? marker + "valorant_".Length : 0;
            return packKey.Length >= start + 5 ? packKey.Substring(start, 5) : "00009";
        }

        private sealed class ValorantDemoProfile
        {
            public ValorantDemoProfile(string id, string accentHex, string emblem, string frame, string bar)
            {
                Id = id;
                Accent = ParseValorantColor(accentHex);
                Emblem = emblem;
                Frame = frame;
                Bar = bar;
            }

            public string Id { get; }
            public Color Accent { get; }
            public string Emblem { get; }
            public string Frame { get; }
            public string Bar { get; }
            public string Blade { get; set; }
            public double HeadshotX { get; set; }
            public double HeadshotY { get; set; }
            public bool HeroFlame { get; set; } = true;
            public bool IsGaia { get; set; }
            public double EmblemScale { get; set; } = 1.0;
            public double FrameWidthScale { get; set; } = 1.0;
            public double BarRadiusOffset { get; set; }
            public double BaseParticleYOffset { get; set; } = 45.0;
            public double BaseParticleScale { get; set; } = 1.0;
            public double LargeSparksScale { get; set; } = 1.0;
            public double HaloRadius { get; set; } = 30.0;
            public bool UsesNativeAfterglowPlayback { get; set; }
        }

        private static Color ParseValorantColor(string hex)
        {
            return Color.FromArgb(
                255,
                Convert.ToByte(hex.Substring(1, 2), 16),
                Convert.ToByte(hex.Substring(3, 2), 16),
                Convert.ToByte(hex.Substring(5, 2), 16));
        }
    }
}

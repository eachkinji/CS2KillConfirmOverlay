using System;
using KillConfirmGameBar.Services;
using Windows.UI;

namespace KillConfirmGameBar.Controls
{
    public sealed partial class KillConfirmAnimation
    {
        private const double ValorantDemoVfxScale = 0.8075;
        private const double ValorantDemoFrameCssHeight = 116.0;

        private static ValorantDemoProfile GetValorantDemoProfile(string packKey)
        {
            ValorantVisualProfileInfo external = ValorantPackService.Find(packKey)?.Profile;
            if (external != null)
            {
                return new ValorantDemoProfile(
                    "external",
                    external.Accent,
                    external.Emblem,
                    external.Frame,
                    external.Bar,
                    external.BarHover)
                {
                    Ring = external.Ring,
                    FrameDissolve = external.FrameDissolve,
                    BadgeDissolve = external.BadgeDissolve,
                    Blade = external.Blade,
                    SpecialFrame = external.SpecialFrame,
                    HeadshotX = external.HeadshotX,
                    HeadshotY = external.HeadshotY,
                    SliceSize = external.SliceSize > 0 ? external.SliceSize : 147.0
                };
            }

            string id = ExtractValorantDemoId(packKey);
            switch (id)
            {
                case "00000":
                    return new ValorantDemoProfile(
                        id,
                        "#57F2D1",
                        "Base_Emblem.png",
                        null,
                        "Base_KillPip_Up.png",
                        "Base_KillPip_Hover.png");
                case "00010":
                    return new ValorantDemoProfile(id, "#68F5FF", "Cyberpunk_Emblem.png", "Cyberpunk_FrameBG.png", "Cyberpunk_KillPip_Hover.png", "Cyberpunk_KillPip_Up.png") { Ring = "Cyberpunk_RingBG.png", FrameDissolve = "Cyberpunk_FrameDissolve.png", BadgeDissolve = "Cyberpunk_BadgeDissolve.png", SliceSize = 172, HeadshotY = -10 };
                case "00011":
                    return EdgeProfile(id, "#F67A44", "V1");
                case "00012":
                    return EdgeProfile(id, "#D6B644", "V2");
                case "00013":
                    return EdgeProfile(id, "#436CBF", "V3");
                case "00014":
                    return AshenProfile(id, "#C01B1F", string.Empty);
                case "00015":
                    return AshenProfile(id, "#0871F7", "_v1");
                case "00016":
                    return AshenProfile(id, "#257133", "_v2");
                case "00017":
                    return AshenProfile(id, "#CB2C00", "_v3");
                case "00018":
                    return HazardProfile(id, "#AF00A3", "Standard", string.Empty, 0, 3.25);
                case "00019":
                    return HazardProfile(id, "#FFC359", "Yellow", "_v1", -1, -16.5);
                case "00020":
                    return HazardProfile(id, "#932B00", "Red", "_v2", 0.6, -4);
                case "00021":
                    return HazardProfile(id, "#06A600", "Green", "_v3", 8, 4);
                case "00022":
                    return new ValorantDemoProfile(id, "#C5B174", "Esports_Emblem.png", null, "Esports_KillPip_Up.png", "EsportsKillPip_Hover.png") { Ring = "Dragon_RingBG.png", SliceSize = 134 };
                case "00023":
                    return DemonStoneProfile(id, "#F35D45", "v1");
                case "00024":
                    return DemonStoneProfile(id, "#01BA01", "v2");
                case "00025":
                    return DemonStoneProfile(id, "#0D7DF5", "v3");
                case "00026":
                    return HellfireProfile(id, "#FE6D41", string.Empty);
                case "00027":
                    return HellfireProfile(id, "#84EAB6", "V1");
                case "00028":
                    return HellfireProfile(id, "#70C9F2", "V2");
                case "00029":
                    return HellfireProfile(id, "#F0D854", "V3");
                case "00030":
                    return new ValorantDemoProfile(id, "#FFC10F", "ComicBook_Emblem.png", "Dragon_FrameBG.png", "ComicBook_KillPip_Up.png", "ComicBook_KillPip_Hover.png") { Ring = "Dragon_RingBG.png", SliceSize = 135, HeadshotY = -19 };
                case "00031":
                    return AfterglowProfile(id, "#A4FF96", string.Empty);
                case "00032":
                    return AfterglowProfile(id, "#A73437", "v1");
                case "00033":
                    return AfterglowProfile(id, "#184DD4", "v2");
                case "00034":
                    return AfterglowProfile(id, "#CE842B", "v3");
                default:
                    // Unreal stores Prime's PrimaryColor as the linear value
                    // (1.0, 0.5, 0.0). FModel's #FFBC00 is its sRGB preview,
                    // not the multiplier passed to the particle widgets.
                    return new ValorantDemoProfile("00009", "#FF8000", "HypeBeast_Emblem.png", "HypeBeast__FrameBG.png", "HypeBeast_KillPip_Up.png", "HypeBeast_KillPip_Hover.png") { Ring = "HypeBeast_RingBG.png", FrameDissolve = "HypeBeast_FrameDissolve.png", BadgeDissolve = "HypeBeast_Emblem_Dissolve.png", SliceSize = 145, HeadshotY = -17 };
            }
        }

        private static ValorantDemoProfile EdgeProfile(string id, string color, string variant)
        {
            return new ValorantDemoProfile(id, color, $"Edge_Emblem{variant}.png", "Dragon_FrameBG.png", $"Edge_KillPip_Up{variant}.png", $"Edge_KillPip_Hover{variant}.png") { Ring = "FantasySovereign_RingBG.png", FrameDissolve = "Dragon_FrameDissolve.png", BadgeDissolve = "Cyberpunk_BadgeDissolve.png", SliceSize = 140, HeadshotY = -10 };
        }

        private static ValorantDemoProfile AshenProfile(string id, string color, string suffix)
        {
            return new ValorantDemoProfile(id, color, $"Ashen_Emblem{suffix}.png", "Ashen_FrameBG.png", $"Ashen_KillPip{suffix}_Up.png", $"Ashen_KillPip{suffix}_Hover.png") { Ring = "Dragon_RingBG.png", HeadshotX = -2, HeadshotY = -20 };
        }

        private static ValorantDemoProfile HazardProfile(string id, string color, string emblem, string suffix, double headshotX, double headshotY)
        {
            return new ValorantDemoProfile(id, color, $"Hazard_Emblem_{emblem}.png", "hazard_blank.png", $"Hazard_KillPip_Up{suffix}.png", $"Hazard_KillPip_Hover{suffix}.png") { Ring = "hazard_blank_ring.png", SpecialFrame = "Hazard_Frame_BG.png", Blade = "Hazard_Frame_Blade.png", SliceSize = 170, HeadshotX = headshotX, HeadshotY = headshotY };
        }

        private static ValorantDemoProfile DemonStoneProfile(string id, string color, string variant)
        {
            return new ValorantDemoProfile(id, color, $"Demonstone_Emblem_{variant}.png", "Demonstone_FrameBG.png", $"Demonstone_KillPip_Up_{variant}.png", $"Demonstone_KillPip_Hover_{variant}.png") { FrameDissolve = "Demonstone_FrameDissolve.png", SliceSize = 170, HeadshotY = -18 };
        }

        private static ValorantDemoProfile HellfireProfile(string id, string color, string variant)
        {
            string suffix = string.IsNullOrEmpty(variant) ? string.Empty : "_" + variant;
            return new ValorantDemoProfile(id, color, $"Hellfire_Emblem{suffix}.png", "HellFire_Frame.png", $"HellFire_KillPip_Up{suffix}.png", $"HellFire_KillPip_Hover{suffix}.png") { SliceSize = 152 };
        }

        private static ValorantDemoProfile AfterglowProfile(string id, string color, string variant)
        {
            string emblem = string.IsNullOrEmpty(variant) ? "Afterglow_Emblem.png" : $"Afterglow_Emblem_{variant}.png";
            string up = string.IsNullOrEmpty(variant) ? "Afterglow_KillPip_Up.png" : $"Afterglow_KillPip__{variant}_Up.png";
            string hover = string.IsNullOrEmpty(variant) ? "Afterglow_KillPip_Hover.png" : $"Afterglow_KillPip_{variant}_Hover.png";
            return new ValorantDemoProfile(id, color, emblem, "Afterglow_FrameBG.png", up, hover) { Ring = "Afterglow_RingBG.png", FrameDissolve = "Afterglow_FrameDissolve.png", BadgeDissolve = "Afterglow_Badge_Dissolve.png", SliceSize = 147, HeadshotX = 0.85, HeadshotY = -21 };
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
            public ValorantDemoProfile(string id, string accentHex, string emblem, string frame, string bar, string barHover)
            {
                Id = id;
                Accent = ParseValorantColor(accentHex);
                Emblem = emblem;
                Frame = frame;
                Bar = bar;
                BarHover = barHover;
            }

            public string Id { get; }
            public Color Accent { get; }
            public string Emblem { get; }
            public string Frame { get; }
            public string Bar { get; }
            public string BarHover { get; }
            public string Ring { get; set; }
            public string FrameDissolve { get; set; }
            public string BadgeDissolve { get; set; }
            public string Blade { get; set; }
            public string SpecialFrame { get; set; }
            public double HeadshotX { get; set; }
            public double HeadshotY { get; set; }
            public double SliceSize { get; set; } = 147.0;
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

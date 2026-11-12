// Shared by Widget (UWP) and CompatibilityHost; C# 7.3 compatible.
using System;
using System.Collections.Generic;
using Windows.Foundation;
using Windows.UI;

#if WINDOWS_UWP
using KillConfirmGameBar.Services;
namespace KillConfirmGameBar.Controls
#else
using KillConfirmCompatibility.Services;
namespace KillConfirmCompatibility.Controls
#endif
{
    public sealed partial class KillConfirmAnimation
    {
        // =====================================================================
        // Native Riot PROP v3 UIRect Reference Space: 1600 x 1200
        // Upper Layer ("BannerScene"): 760 x 300 mapped to [420..1180, 25..325]
        // Lower Layer ("KillCallout0"): 310 x 300 mapped to [1295..1605, 260..560]
        // Crosshair Layer ("HextechCrosshair"): 360 x 220
        // =====================================================================
        internal const double LeagueOfLegendsUpperFrameWidth = 760.0;
        internal const double LeagueOfLegendsUpperFrameHeight = 300.0;
        private const double LolBannerOffsetX = 420.0;
        private const double LolBannerOffsetY = 25.0;

        internal const double LeagueOfLegendsLowerFrameWidth = 310.0;
        internal const double LeagueOfLegendsLowerFrameHeight = 300.0;
        private const double LolCalloutOffsetX = 1295.0;
        private const double LolCalloutOffsetY = 260.0;
        private const double LolCalloutSlotStride = 70.0;
        private const int LolCalloutMaxSlots = 4;

        internal const double LeagueOfLegendsCrosshairFrameWidth = 360.0;
        internal const double LeagueOfLegendsCrosshairFrameHeight = 220.0;

        // Exact Riot Timeline Constants (milliseconds)
        private const double LolBannerNormalDurationMs = 3000.0;
        private const double LolBannerEpicDurationMs = 3800.0;
        private const double LolBannerIntroMs = 220.0;
        private const double LolBannerPentaSlamDownMs = 150.0;
        private const double LolBannerPentaSettleMs = 250.0;
        private const double LolBannerFadeOutMs = 350.0;
        private const double LolBannerMinHoldBeforeNextMs = 1350.0;

        private const double LolCalloutDurationMs = 4200.0;
        private const double LolCalloutSlideInMs = 240.0;
        private const double LolCalloutSlideDistancePx = 90.0;
        private const double LolCalloutSlotShiftMs = 250.0;
        private const double LolCalloutFadeOutMs = 350.0;
        private const double LolCalloutPulseRing1LifeMs = 800.0;
        private const double LolCalloutPulseRing2DelayMs = 180.0;
        private const double LolCalloutPulseRing2LifeMs = 400.0;

        private const double LolCrosshairHitDurationMs = 420.0;
        private const double LolCrosshairBountyDurationMs = 1350.0;

        // Riot Common_Announcements_Master Priority Table (0..100)
        private const int LolPriorityNormalKill = 69;
        private const int LolPriorityObjective = 69;
        private const int LolPriorityExecuted = 69;
        private const int LolPrioritySpree = 70;
        private const int LolPriorityDoubleKill = 76;
        private const int LolPriorityShutdown = 77;
        private const int LolPriorityTripleKill = 78;
        private const int LolPriorityQuadraKill = 82;
        private const int LolPriorityFirstBlood = 92;
        private const int LolPriorityAce = 95;
        private const int LolPriorityPentakill = 97;
        private const int LolPriorityMatchResult = 99;

        // Authentic Riot HUD Colors
        private static readonly Color LolAllyGlowBlue = Color.FromArgb(255, 0, 153, 255);     // #0099FF
        private static readonly Color LolEnemyGlowRed = Color.FromArgb(255, 227, 0, 0);      // #E30000
        private static readonly Color LolGoldStopTop = Color.FromArgb(255, 249, 227, 191);   // #F9E3BF
        private static readonly Color LolGoldStopMid = Color.FromArgb(255, 227, 199, 153);   // #E3C799
        private static readonly Color LolGoldStopBot = Color.FromArgb(255, 189, 149, 90);    // #BD955A
        private static readonly Color LolTextStrokeDark = Color.FromArgb(255, 8, 10, 14);    // #080A0E
        private static readonly Color LolHextechCyan = Color.FromArgb(255, 10, 200, 185);    // #0AC8B9
        private static readonly Color LolHextechGold = Color.FromArgb(255, 200, 170, 110);   // #C8AA6E

        private static readonly string[] LolBuiltinChampions =
        {
            "Yasuo",
            "Yone",
            "Zed",
            "Ahri",
            "Jinx",
            "LeeSin",
            "Darius",
            "Vayne",
            "Ezreal",
            "Lux"
        };

        private static readonly Dictionary<string, string> LolChampionAliasMap =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "yasuo", "Yasuo" }, { "亚索", "Yasuo" }, { "疾风剑豪", "Yasuo" },
                { "yone", "Yone" }, { "永恩", "Yone" }, { "封魔剑魂", "Yone" },
                { "zed", "Zed" }, { "劫", "Zed" }, { "影流之主", "Zed" },
                { "ahri", "Ahri" }, { "阿狸", "Ahri" }, { "九尾妖狐", "Ahri" },
                { "jinx", "Jinx" }, { "金克丝", "Jinx" }, { "暴走萝莉", "Jinx" },
                { "leesin", "LeeSin" }, { "lee sin", "LeeSin" }, { "李青", "LeeSin" }, { "盲僧", "LeeSin" },
                { "darius", "Darius" }, { "德莱厄斯", "Darius" }, { "诺克萨斯之手", "Darius" }, { "诺手", "Darius" },
                { "vayne", "Vayne" }, { "薇恩", "Vayne" }, { "暗夜猎手", "Vayne" },
                { "ezreal", "Ezreal" }, { "伊泽瑞尔", "Ezreal" }, { "探险家", "Ezreal" },
                { "lux", "Lux" }, { "拉克丝", "Lux" }, { "光辉女郎", "Lux" }
            };

        private static readonly Dictionary<string, Tuple<string, string>> LolChampionDisplayNames =
            new Dictionary<string, Tuple<string, string>>(StringComparer.OrdinalIgnoreCase)
            {
                { "Yasuo", Tuple.Create("疾风剑豪", "Yasuo") },
                { "Yone", Tuple.Create("封魔剑魂", "Yone") },
                { "Zed", Tuple.Create("影流之主", "Zed") },
                { "Ahri", Tuple.Create("九尾妖狐", "Ahri") },
                { "Jinx", Tuple.Create("暴走萝莉", "Jinx") },
                { "LeeSin", Tuple.Create("盲僧", "Lee Sin") },
                { "Darius", Tuple.Create("诺克萨斯之手", "Darius") },
                { "Vayne", Tuple.Create("暗夜猎手", "Vayne") },
                { "Ezreal", Tuple.Create("探险家", "Ezreal") },
                { "Lux", Tuple.Create("光辉女郎", "Lux") }
            };

        private static Rect ToLolBannerRect(double riotX, double riotY, double width, double height)
        {
            return new Rect(riotX - LolBannerOffsetX, riotY - LolBannerOffsetY, width, height);
        }

        private static Rect ToLolCalloutRect(double riotX, double riotY, double width, double height, double slotOffsetY)
        {
            return new Rect(riotX - LolCalloutOffsetX, riotY - LolCalloutOffsetY + slotOffsetY, width, height);
        }

        private static string ResolveLolChampionPortraitFile(string rawChampionOrPlayer, string fallbackChampion = null)
        {
            return ResolveLolChampionPortraitVariantFile(rawChampionOrPlayer, fallbackChampion, square: false);
        }

        private static string ResolveLolChampionSquarePortraitFile(string rawChampionOrPlayer, string fallbackChampion = null)
        {
            return ResolveLolChampionPortraitVariantFile(rawChampionOrPlayer, fallbackChampion, square: true);
        }

        private static string ResolveLolChampionPortraitVariantFile(
            string rawChampionOrPlayer,
            string fallbackChampion,
            bool square)
        {
            string suffix = square ? "_square.png" : "_circle.png";
            string altSuffix = square ? "_circle.png" : "_square.png";
            string candidate = (rawChampionOrPlayer ?? string.Empty).Trim();
            if (candidate.StartsWith("game_character_displayname_", StringComparison.OrdinalIgnoreCase))
            {
                candidate = candidate.Substring("game_character_displayname_".Length).Trim();
            }

            if (!string.IsNullOrEmpty(candidate))
            {
                if (LolChampionAliasMap.TryGetValue(candidate, out string mapped))
                {
                    return mapped + suffix;
                }

                string clean = candidate.Replace(" ", string.Empty);
                string directFile = clean + suffix;
                if (TryGetPreparedLeagueOfLegendsIcon(directFile) != null)
                {
                    return directFile;
                }

                string altFile = clean + altSuffix;
                if (TryGetPreparedLeagueOfLegendsIcon(altFile) != null)
                {
                    return altFile;
                }
            }

            if (!string.IsNullOrWhiteSpace(fallbackChampion))
            {
                string fb = LeagueOfLegendsSettingsStore.NormalizeChampion(fallbackChampion);
                return fb + suffix;
            }

            // Deterministic hash across the 10 iconic Summoner's Rift portraits
            string seedText = string.IsNullOrEmpty(candidate) ? "Ezreal" : candidate;
            int hash = 17;
            for (int i = 0; i < seedText.Length; i++)
            {
                hash = unchecked(hash * 31 + char.ToLowerInvariant(seedText[i]));
            }

            int idx = (hash & 0x7FFFFFFF) % LolBuiltinChampions.Length;
            return LolBuiltinChampions[idx] + suffix;
        }

        private static string ResolveLolActorDisplayName(
            string actorChampion,
            string actorName,
            string fallbackChampion,
            bool useChinese)
        {
            string champ = (actorChampion ?? string.Empty).Trim();
            if (champ.StartsWith("game_character_displayname_", StringComparison.OrdinalIgnoreCase))
            {
                champ = champ.Substring("game_character_displayname_".Length).Trim();
            }

            if (!string.IsNullOrEmpty(champ))
            {
                if (LolChampionAliasMap.TryGetValue(champ, out string canonical)
                    && LolChampionDisplayNames.TryGetValue(canonical, out Tuple<string, string> names))
                {
                    return useChinese ? names.Item1 : names.Item2;
                }

                return champ;
            }

            if (!string.IsNullOrWhiteSpace(actorName))
            {
                string trimmed = actorName.Trim();
                if (LolChampionAliasMap.TryGetValue(trimmed, out string canonicalName)
                    && LolChampionDisplayNames.TryGetValue(canonicalName, out Tuple<string, string> mappedNames))
                {
                    return useChinese ? mappedNames.Item1 : mappedNames.Item2;
                }

                return trimmed;
            }

            string defChamp = LeagueOfLegendsSettingsStore.NormalizeChampion(fallbackChampion);
            if (LolChampionDisplayNames.TryGetValue(defChamp, out Tuple<string, string> fallbackNames))
            {
                return useChinese ? fallbackNames.Item1 : fallbackNames.Item2;
            }

            return useChinese ? "召唤师" : "Summoner";
        }
    }
}

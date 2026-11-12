// Shared by Widget (UWP) and CompatibilityHost; C# 7.3 compatible.
using System;
using Windows.Storage;

#if WINDOWS_UWP
namespace KillConfirmGameBar.Services
#else
namespace KillConfirmCompatibility.Services
#endif
{
    internal static class LeagueOfLegendsSettingsStore
    {
        private const string HudLanguageKey = "LeagueOfLegendsHudLanguage";
        private const string TeamThemeKey = "LeagueOfLegendsTeamTheme";
        private const string ActorTextModeKey = "LeagueOfLegendsActorTextMode";
        private const string DefaultChampionKey = "LeagueOfLegendsDefaultChampion";
        private const string BannerModeKey = "LeagueOfLegendsBannerMode";
        private const string CsStreakModeKey = "LeagueOfLegendsCsStreakMode";
        private const string TextFlankingEnabledKey = "LeagueOfLegendsTextFlankingEnabled";
        private const string PulseVfxEnabledKey = "LeagueOfLegendsPulseVfxEnabled";
        private const string EnemyEventsEnabledKey = "LeagueOfLegendsEnemyEventsEnabled";

        public const string LanguageFollowUi = "auto";
        public const string LanguageSimplifiedChinese = "zh-CN";
        public const string LanguageEnglish = "en";

        public const string TeamBlue = "blue";
        public const string TeamRed = "red";
        public const string TeamGold = "gold";

        public const string ActorTextYou = "you";
        public const string ActorTextChampion = "champion";
        public const string ActorYou = ActorTextYou;
        public const string ActorHero = ActorTextChampion;

        public const string BannerAllKills = "all";
        public const string BannerHighlightsOnly = "highlights";
        public const string BannerEpicOnly = BannerHighlightsOnly;

        public const string StreakMultikill = "multikill";
        public const string StreakSpree = "spree";
        public const string StreakCombined = "combined";
        public const string StreakSpreeTiers = StreakSpree;

        public static readonly string[] BuiltInChampions =
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

        public static event EventHandler Changed;

        public static string LoadHudLanguage()
            => NormalizeHudLanguage(GameSourceSettingsStore.CombatSettings[HudLanguageKey] as string);

        public static void SaveHudLanguage(string value)
        {
            GameSourceSettingsStore.CombatSettings[HudLanguageKey] = NormalizeHudLanguage(value);
            Changed?.Invoke(null, EventArgs.Empty);
        }

        public static string NormalizeHudLanguage(string value)
        {
            if (string.Equals(value, LanguageSimplifiedChinese, StringComparison.OrdinalIgnoreCase))
                return LanguageSimplifiedChinese;
            if (string.Equals(value, LanguageEnglish, StringComparison.OrdinalIgnoreCase))
                return LanguageEnglish;
            return LanguageFollowUi;
        }

        public static bool ResolveUseChinese(bool uiIsSimplifiedChinese)
        {
            string mode = LoadHudLanguage();
            if (mode == LanguageSimplifiedChinese) return true;
            if (mode == LanguageEnglish) return false;
            return uiIsSimplifiedChinese;
        }

        public static string LoadTeamTheme()
            => NormalizeTeamTheme(GameSourceSettingsStore.CombatSettings[TeamThemeKey] as string);

        public static void SaveTeamTheme(string value)
        {
            GameSourceSettingsStore.CombatSettings[TeamThemeKey] = NormalizeTeamTheme(value);
            Changed?.Invoke(null, EventArgs.Empty);
        }

        public static string NormalizeTeamTheme(string value)
        {
            if (string.Equals(value, TeamRed, StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "enemy", StringComparison.OrdinalIgnoreCase))
                return TeamRed;
            if (string.Equals(value, TeamGold, StringComparison.OrdinalIgnoreCase))
                return TeamGold;
            return TeamBlue;
        }

        public static string LoadActorTextMode()
            => NormalizeActorTextMode(GameSourceSettingsStore.CombatSettings[ActorTextModeKey] as string);

        public static void SaveActorTextMode(string value)
        {
            GameSourceSettingsStore.CombatSettings[ActorTextModeKey] = NormalizeActorTextMode(value);
            Changed?.Invoke(null, EventArgs.Empty);
        }

        public static string NormalizeActorTextMode(string value)
        {
            if (string.Equals(value, ActorTextChampion, StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "hero", StringComparison.OrdinalIgnoreCase))
                return ActorTextChampion;
            return ActorTextYou;
        }

        public static string LoadDefaultChampion()
            => NormalizeChampion(GameSourceSettingsStore.CombatSettings[DefaultChampionKey] as string);

        public static void SaveDefaultChampion(string value)
        {
            GameSourceSettingsStore.CombatSettings[DefaultChampionKey] = NormalizeChampion(value);
            Changed?.Invoke(null, EventArgs.Empty);
        }

        public static string NormalizeChampion(string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                string trimmed = value.Trim();
                for (int i = 0; i < BuiltInChampions.Length; i++)
                {
                    if (string.Equals(BuiltInChampions[i], trimmed, StringComparison.OrdinalIgnoreCase))
                    {
                        return BuiltInChampions[i];
                    }
                }
            }

            return "Yasuo";
        }

        public static string NormalizeDefaultChampion(string value) => NormalizeChampion(value);

        public static string LoadBannerMode()
            => NormalizeBannerMode(GameSourceSettingsStore.CombatSettings[BannerModeKey] as string);

        public static void SaveBannerMode(string value)
        {
            GameSourceSettingsStore.CombatSettings[BannerModeKey] = NormalizeBannerMode(value);
            Changed?.Invoke(null, EventArgs.Empty);
        }

        public static string NormalizeBannerMode(string value)
        {
            if (string.Equals(value, BannerHighlightsOnly, StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "epic", StringComparison.OrdinalIgnoreCase))
                return BannerHighlightsOnly;
            return BannerAllKills;
        }

        public static string LoadCsStreakMode()
            => NormalizeCsStreakMode(GameSourceSettingsStore.CombatSettings[CsStreakModeKey] as string);

        public static void SaveCsStreakMode(string value)
        {
            GameSourceSettingsStore.CombatSettings[CsStreakModeKey] = NormalizeCsStreakMode(value);
            Changed?.Invoke(null, EventArgs.Empty);
        }

        public static string NormalizeCsStreakMode(string value)
        {
            if (string.Equals(value, StreakSpree, StringComparison.OrdinalIgnoreCase))
                return StreakSpree;
            if (string.Equals(value, StreakCombined, StringComparison.OrdinalIgnoreCase))
                return StreakCombined;
            return StreakMultikill;
        }

        public static bool LoadTextFlankingEnabled()
            => ReadBool(TextFlankingEnabledKey, true);

        public static void SaveTextFlankingEnabled(bool enabled)
        {
            GameSourceSettingsStore.CombatSettings[TextFlankingEnabledKey] = enabled;
            Changed?.Invoke(null, EventArgs.Empty);
        }

        public static bool LoadPulseVfxEnabled()
            => ReadBool(PulseVfxEnabledKey, true);

        public static void SavePulseVfxEnabled(bool enabled)
        {
            GameSourceSettingsStore.CombatSettings[PulseVfxEnabledKey] = enabled;
            Changed?.Invoke(null, EventArgs.Empty);
        }

        public static bool LoadEnemyEventsEnabled()
            => ReadBool(EnemyEventsEnabledKey, true);

        public static void SaveEnemyEventsEnabled(bool enabled)
        {
            GameSourceSettingsStore.CombatSettings[EnemyEventsEnabledKey] = enabled;
            Changed?.Invoke(null, EventArgs.Empty);
        }

        public static void Reset()
        {
            var settings = GameSourceSettingsStore.CombatSettings;
            settings[HudLanguageKey] = null;
            settings[TeamThemeKey] = null;
            settings[ActorTextModeKey] = null;
            settings[DefaultChampionKey] = null;
            settings[BannerModeKey] = null;
            settings[CsStreakModeKey] = null;
            settings[TextFlankingEnabledKey] = null;
            settings[PulseVfxEnabledKey] = null;
            settings[EnemyEventsEnabledKey] = null;
            Changed?.Invoke(null, EventArgs.Empty);
        }

        private static bool ReadBool(string key, bool fallback)
        {
            object raw = GameSourceSettingsStore.CombatSettings[key];
            if (raw is bool b) return b;
            if (raw is string s && bool.TryParse(s, out bool parsed)) return parsed;
            return fallback;
        }
    }
}

// Shared by Widget (UWP) and CompatibilityHost; C# 7.3 compatible.
using System;
using Windows.Storage;

#if WINDOWS_UWP
namespace KillConfirmGameBar.Services
#else
namespace KillConfirmCompatibility.Services
#endif
{
    internal static class Cod2022SettingsStore
    {
        private const string HudLanguageKey = "Cod2022HudLanguage";
        private const string HitmarkerSizeKey = "Cod2022HitmarkerSize";
        private const string SplashModeKey = "Cod2022SplashMode";
        private const string PointsDisplayModeKey = "Cod2022PointsDisplayMode";
        private const string LowerStyleKey = "Cod2022LowerStyle";
        private const string DecodeScrambleEnabledKey = "Cod2022DecodeScrambleEnabled";
        private const string SplashIconEnabledKey = "Cod2022SplashIconEnabled";

        public const string LanguageFollowUi = "auto";
        public const string LanguageSimplifiedChinese = "zh-CN";
        public const string LanguageEnglish = "en";

        public const string HitmarkerMedium = "medium";
        public const string HitmarkerLarge = "large";
        public const string HitmarkerSmall = "small";

        public const string SplashSpecialOnly = "special";
        public const string SplashAllKills = "always";

        public const string PointsXp = "xp";
        public const string PointsReward = "reward";
        public const string PointsOff = "off";

        public const string LowerNameplate = "nameplate";
        public const string LowerKilledMessage = "killed_message";

        public static event EventHandler Changed;

        public static string LoadHudLanguage()
        {
            string raw = GameSourceSettingsStore.CombatSettings[HudLanguageKey] as string;
            return NormalizeHudLanguage(raw);
        }

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

        public static string LoadHitmarkerSize()
        {
            string raw = GameSourceSettingsStore.CombatSettings[HitmarkerSizeKey] as string;
            return NormalizeHitmarkerSize(raw);
        }

        public static void SaveHitmarkerSize(string value)
        {
            GameSourceSettingsStore.CombatSettings[HitmarkerSizeKey] = NormalizeHitmarkerSize(value);
            Changed?.Invoke(null, EventArgs.Empty);
        }

        public static string NormalizeHitmarkerSize(string value)
        {
            if (string.Equals(value, HitmarkerLarge, StringComparison.OrdinalIgnoreCase))
                return HitmarkerLarge;
            if (string.Equals(value, HitmarkerSmall, StringComparison.OrdinalIgnoreCase))
                return HitmarkerSmall;
            return HitmarkerMedium;
        }

        public static string LoadSplashMode()
        {
            string raw = GameSourceSettingsStore.CombatSettings[SplashModeKey] as string;
            return NormalizeSplashMode(raw);
        }

        public static void SaveSplashMode(string value)
        {
            GameSourceSettingsStore.CombatSettings[SplashModeKey] = NormalizeSplashMode(value);
            Changed?.Invoke(null, EventArgs.Empty);
        }

        public static string NormalizeSplashMode(string value)
        {
            if (string.Equals(value, SplashAllKills, StringComparison.OrdinalIgnoreCase))
                return SplashAllKills;
            return SplashSpecialOnly;
        }

        public static string LoadPointsDisplayMode()
        {
            string raw = GameSourceSettingsStore.CombatSettings[PointsDisplayModeKey] as string;
            return NormalizePointsDisplayMode(raw);
        }

        public static void SavePointsDisplayMode(string value)
        {
            GameSourceSettingsStore.CombatSettings[PointsDisplayModeKey] = NormalizePointsDisplayMode(value);
            Changed?.Invoke(null, EventArgs.Empty);
        }

        public static string NormalizePointsDisplayMode(string value)
        {
            if (string.Equals(value, PointsReward, StringComparison.OrdinalIgnoreCase))
                return PointsReward;
            if (string.Equals(value, PointsOff, StringComparison.OrdinalIgnoreCase))
                return PointsOff;
            return PointsXp;
        }

        public static string LoadLowerStyle()
        {
            string raw = GameSourceSettingsStore.CombatSettings[LowerStyleKey] as string;
            return NormalizeLowerStyle(raw);
        }

        public static void SaveLowerStyle(string value)
        {
            GameSourceSettingsStore.CombatSettings[LowerStyleKey] = NormalizeLowerStyle(value);
            Changed?.Invoke(null, EventArgs.Empty);
        }

        public static string NormalizeLowerStyle(string value)
        {
            if (string.Equals(value, LowerKilledMessage, StringComparison.OrdinalIgnoreCase))
                return LowerKilledMessage;
            return LowerNameplate;
        }

        public static bool LoadDecodeScrambleEnabled()
        {
            return ReadBool(DecodeScrambleEnabledKey, true);
        }

        public static void SaveDecodeScrambleEnabled(bool enabled)
        {
            GameSourceSettingsStore.CombatSettings[DecodeScrambleEnabledKey] = enabled;
            Changed?.Invoke(null, EventArgs.Empty);
        }

        public static bool LoadSplashIconEnabled()
        {
            return ReadBool(SplashIconEnabledKey, true);
        }

        public static void SaveSplashIconEnabled(bool enabled)
        {
            GameSourceSettingsStore.CombatSettings[SplashIconEnabledKey] = enabled;
            Changed?.Invoke(null, EventArgs.Empty);
        }

        public static void Reset()
        {
            var settings = GameSourceSettingsStore.CombatSettings;
            settings[HudLanguageKey] = null;
            settings[HitmarkerSizeKey] = null;
            settings[SplashModeKey] = null;
            settings[PointsDisplayModeKey] = null;
            settings[LowerStyleKey] = null;
            settings[DecodeScrambleEnabledKey] = null;
            settings[SplashIconEnabledKey] = null;
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

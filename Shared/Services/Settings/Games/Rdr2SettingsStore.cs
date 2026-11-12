// Shared by Widget (UWP) and CompatibilityHost; C# 7.3 compatible.
using System;
using Windows.Storage;

#if WINDOWS_UWP
namespace KillConfirmGameBar.Services
#else
namespace KillConfirmCompatibility.Services
#endif
{
    internal static class Rdr2SettingsStore
    {
        private const string HudLanguageKey = "Rdr2HudLanguage";
        private const string HonorModeKey = "Rdr2HonorMode";
        private const string ReticleStyleKey = "Rdr2ReticleStyle";
        private const string BountyDisplayModeKey = "Rdr2BountyDisplayMode";
        private const string AccuracyCircleEnabledKey = "Rdr2AccuracyCircleEnabled";
        private const string LowerCaptionEnabledKey = "Rdr2LowerCaptionEnabled";
        private const string HonorPopupEnabledKey = "Rdr2HonorPopupEnabled";

        public const string LanguageFollowUi = "auto";
        public const string LanguageSimplifiedChinese = "zh-CN";
        public const string LanguageEnglish = "en";

        public const string HonorDynamic = "dynamic";
        public const string HonorHigh = "high_honor";
        public const string HonorLow = "low_honor";

        public const string ReticleScaleform = "scaleform";
        public const string ReticleDeadEye = "deadeye";
        public const string ReticleMinimal = "minimal";

        public const string BountyXp = "xp";
        public const string BountyCash = "cash";
        public const string BountyOff = "off";

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

        public static string LoadHonorMode()
        {
            string raw = GameSourceSettingsStore.CombatSettings[HonorModeKey] as string;
            return NormalizeHonorMode(raw);
        }

        public static void SaveHonorMode(string value)
        {
            GameSourceSettingsStore.CombatSettings[HonorModeKey] = NormalizeHonorMode(value);
            Changed?.Invoke(null, EventArgs.Empty);
        }

        public static string NormalizeHonorMode(string value)
        {
            if (string.Equals(value, HonorHigh, StringComparison.OrdinalIgnoreCase))
                return HonorHigh;
            if (string.Equals(value, HonorLow, StringComparison.OrdinalIgnoreCase))
                return HonorLow;
            return HonorDynamic;
        }

        public static string LoadReticleStyle()
        {
            string raw = GameSourceSettingsStore.CombatSettings[ReticleStyleKey] as string;
            return NormalizeReticleStyle(raw);
        }

        public static void SaveReticleStyle(string value)
        {
            GameSourceSettingsStore.CombatSettings[ReticleStyleKey] = NormalizeReticleStyle(value);
            Changed?.Invoke(null, EventArgs.Empty);
        }

        public static string NormalizeReticleStyle(string value)
        {
            if (string.Equals(value, ReticleDeadEye, StringComparison.OrdinalIgnoreCase))
                return ReticleDeadEye;
            if (string.Equals(value, ReticleMinimal, StringComparison.OrdinalIgnoreCase))
                return ReticleMinimal;
            return ReticleScaleform;
        }

        public static string LoadBountyDisplayMode()
        {
            string raw = GameSourceSettingsStore.CombatSettings[BountyDisplayModeKey] as string;
            return NormalizeBountyDisplayMode(raw);
        }

        public static void SaveBountyDisplayMode(string value)
        {
            GameSourceSettingsStore.CombatSettings[BountyDisplayModeKey] = NormalizeBountyDisplayMode(value);
            Changed?.Invoke(null, EventArgs.Empty);
        }

        public static string NormalizeBountyDisplayMode(string value)
        {
            if (string.Equals(value, BountyCash, StringComparison.OrdinalIgnoreCase))
                return BountyCash;
            if (string.Equals(value, BountyOff, StringComparison.OrdinalIgnoreCase))
                return BountyOff;
            return BountyXp;
        }

        public static bool LoadAccuracyCircleEnabled()
        {
            return ReadBool(AccuracyCircleEnabledKey, true);
        }

        public static void SaveAccuracyCircleEnabled(bool enabled)
        {
            GameSourceSettingsStore.CombatSettings[AccuracyCircleEnabledKey] = enabled;
            Changed?.Invoke(null, EventArgs.Empty);
        }

        public static bool LoadLowerCaptionEnabled()
        {
            return ReadBool(LowerCaptionEnabledKey, true);
        }

        public static void SaveLowerCaptionEnabled(bool enabled)
        {
            GameSourceSettingsStore.CombatSettings[LowerCaptionEnabledKey] = enabled;
            Changed?.Invoke(null, EventArgs.Empty);
        }

        public static bool LoadHonorPopupEnabled()
        {
            return ReadBool(HonorPopupEnabledKey, true);
        }

        public static void SaveHonorPopupEnabled(bool enabled)
        {
            GameSourceSettingsStore.CombatSettings[HonorPopupEnabledKey] = enabled;
            Changed?.Invoke(null, EventArgs.Empty);
        }

        public static void Reset()
        {
            var settings = GameSourceSettingsStore.CombatSettings;
            settings[HudLanguageKey] = null;
            settings[HonorModeKey] = null;
            settings[ReticleStyleKey] = null;
            settings[BountyDisplayModeKey] = null;
            settings[AccuracyCircleEnabledKey] = null;
            settings[LowerCaptionEnabledKey] = null;
            settings[HonorPopupEnabledKey] = null;
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

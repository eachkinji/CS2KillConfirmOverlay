// Shared by Widget (UWP) and CompatibilityHost.
using System;
using Windows.Storage;

#if WINDOWS_UWP
namespace KillConfirmGameBar.Services
#else
namespace KillConfirmCompatibility.Services
#endif
{
    internal static class Titanfall2SettingsStore
    {
        private const string HudLanguageKey = "Titanfall2HudLanguage";
        private const string ReticleStyleKey = "Titanfall2ReticleStyle";
        private const string CrosshairMedalEnabledKey = "Titanfall2CrosshairMedalEnabled";
        private const string HitIndicatorEnabledKey = "Titanfall2HitIndicatorEnabled";
        private const string MeterBonusEnabledKey = "Titanfall2MeterBonusEnabled";

        public const string LanguageFollowUi = "auto";
        public const string LanguageSimplifiedChinese = "zh-CN";
        public const string LanguageTraditionalChinese = "zh-TW";
        public const string LanguageEnglish = "en";

        public const string ReticleNone = "none";
        public const string ReticlePilotTriDot = "tri";
        public const string ReticleR201Wing = "wing";

        public static event EventHandler Changed;

        public static string LoadHudLanguage()
        {
            string raw = GameSourceSettingsStore.CombatSettings[HudLanguageKey] as string;
            return NormalizeHudLanguage(raw);
        }

        public static void SaveHudLanguage(string value)
        {
            string normalized = NormalizeHudLanguage(value);
            GameSourceSettingsStore.CombatSettings[HudLanguageKey] = normalized;
            Changed?.Invoke(null, EventArgs.Empty);
        }

        public static string NormalizeHudLanguage(string value)
        {
            if (string.Equals(value, LanguageSimplifiedChinese, StringComparison.OrdinalIgnoreCase))
                return LanguageSimplifiedChinese;
            if (string.Equals(value, LanguageTraditionalChinese, StringComparison.OrdinalIgnoreCase))
                return LanguageTraditionalChinese;
            if (string.Equals(value, LanguageEnglish, StringComparison.OrdinalIgnoreCase))
                return LanguageEnglish;
            return LanguageFollowUi;
        }

        public static string ResolveEffectiveLanguage(bool uiIsSimplifiedChinese)
        {
            string mode = LoadHudLanguage();
            if (mode == LanguageFollowUi)
            {
                return uiIsSimplifiedChinese ? LanguageSimplifiedChinese : LanguageEnglish;
            }

            return mode;
        }

        public static string LoadReticleStyle()
        {
            string raw = GameSourceSettingsStore.CombatSettings[ReticleStyleKey] as string;
            return NormalizeReticleStyle(raw);
        }

        public static void SaveReticleStyle(string value)
        {
            string normalized = NormalizeReticleStyle(value);
            GameSourceSettingsStore.CombatSettings[ReticleStyleKey] = normalized;
            Changed?.Invoke(null, EventArgs.Empty);
        }

        public static string NormalizeReticleStyle(string value)
        {
            if (string.Equals(value, ReticlePilotTriDot, StringComparison.OrdinalIgnoreCase))
                return ReticlePilotTriDot;
            if (string.Equals(value, ReticleR201Wing, StringComparison.OrdinalIgnoreCase))
                return ReticleR201Wing;
            return ReticleNone;
        }

        public static bool LoadCrosshairMedalEnabled()
        {
            return ReadBool(CrosshairMedalEnabledKey, true);
        }

        public static void SaveCrosshairMedalEnabled(bool enabled)
        {
            GameSourceSettingsStore.CombatSettings[CrosshairMedalEnabledKey] = enabled;
            Changed?.Invoke(null, EventArgs.Empty);
        }

        public static bool LoadHitIndicatorEnabled()
        {
            return ReadBool(HitIndicatorEnabledKey, true);
        }

        public static void SaveHitIndicatorEnabled(bool enabled)
        {
            GameSourceSettingsStore.CombatSettings[HitIndicatorEnabledKey] = enabled;
            Changed?.Invoke(null, EventArgs.Empty);
        }

        public static bool LoadMeterBonusEnabled()
        {
            return ReadBool(MeterBonusEnabledKey, true);
        }

        public static void SaveMeterBonusEnabled(bool enabled)
        {
            GameSourceSettingsStore.CombatSettings[MeterBonusEnabledKey] = enabled;
            Changed?.Invoke(null, EventArgs.Empty);
        }

        public static void Reset()
        {
            var settings = GameSourceSettingsStore.CombatSettings;
            settings[HudLanguageKey] = null;
            settings[ReticleStyleKey] = null;
            settings[CrosshairMedalEnabledKey] = null;
            settings[HitIndicatorEnabledKey] = null;
            settings[MeterBonusEnabledKey] = null;
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

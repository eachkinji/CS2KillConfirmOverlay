// Shared by Widget (UWP) and CompatibilityHost; C# 7.3 compatible.
using System;
using Windows.Storage;

#if WINDOWS_UWP
namespace KillConfirmGameBar.Services
#else
namespace KillConfirmCompatibility.Services
#endif
{
    internal static class WarThunderSettingsStore
    {
        private const string BattleModeKey = "WarThunderBattleMode";
        private const string HudLanguageKey = "WarThunderHudLanguage";
        private const string LowerDisplayModeKey = "WarThunderLowerDisplayMode";
        private const string CrosshairStyleKey = "WarThunderCrosshairStyle";
        private const string DamagedPartsEnabledKey = "WarThunderDamagedPartsEnabled";
        private const string WorldKillMarkEnabledKey = "WarThunderWorldKillMarkEnabled";
        private const string StreakAllKillsEnabledKey = "WarThunderStreakAllKillsEnabled";

        public const string BattleModeTank = "tank";
        public const string BattleModeAir = "air";
        public const string BattleModeShip = "ship";
        public const string BattleModeInfantry = "infantry";
        public const string ModeTank = BattleModeTank;
        public const string ModeAir = BattleModeAir;
        public const string ModeShip = BattleModeShip;
        public const string ModeInfantry = BattleModeInfantry;

        public const string LanguageFollowUi = "auto";
        public const string LanguageSimplifiedChinese = "zh-CN";
        public const string LanguageChinese = LanguageSimplifiedChinese;
        public const string LanguageEnglish = "en";

        public const string LowerXrayAndLog = "xray_and_log";
        public const string LowerXRayAndLog = LowerXrayAndLog;
        public const string LowerXrayOnly = "xray_only";
        public const string LowerXRayOnly = LowerXrayOnly;
        public const string LowerLogOnly = "log_only";

        public const string CrosshairMinimal = "minimal";
        public const string CrosshairReticle = "reticle";

        public static event EventHandler Changed;

        public static string LoadBattleMode()
        {
            string raw = GameSourceSettingsStore.CombatSettings[BattleModeKey] as string;
            return NormalizeBattleMode(raw);
        }

        public static void SaveBattleMode(string value)
        {
            GameSourceSettingsStore.CombatSettings[BattleModeKey] = NormalizeBattleMode(value);
            Changed?.Invoke(null, EventArgs.Empty);
        }

        public static string NormalizeBattleMode(string value)
        {
            if (string.Equals(value, BattleModeAir, StringComparison.OrdinalIgnoreCase))
                return BattleModeAir;
            if (string.Equals(value, BattleModeShip, StringComparison.OrdinalIgnoreCase))
                return BattleModeShip;
            if (string.Equals(value, BattleModeInfantry, StringComparison.OrdinalIgnoreCase))
                return BattleModeInfantry;
            return BattleModeTank;
        }

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

        public static string LoadLowerDisplayMode()
        {
            string raw = GameSourceSettingsStore.CombatSettings[LowerDisplayModeKey] as string;
            return NormalizeLowerDisplayMode(raw);
        }

        public static void SaveLowerDisplayMode(string value)
        {
            GameSourceSettingsStore.CombatSettings[LowerDisplayModeKey] = NormalizeLowerDisplayMode(value);
            Changed?.Invoke(null, EventArgs.Empty);
        }

        public static string NormalizeLowerDisplayMode(string value)
        {
            if (string.Equals(value, LowerXrayOnly, StringComparison.OrdinalIgnoreCase))
                return LowerXrayOnly;
            if (string.Equals(value, LowerLogOnly, StringComparison.OrdinalIgnoreCase))
                return LowerLogOnly;
            return LowerXrayAndLog;
        }

        public static string LoadCrosshairStyle()
        {
            string raw = GameSourceSettingsStore.CombatSettings[CrosshairStyleKey] as string;
            return NormalizeCrosshairStyle(raw);
        }

        public static void SaveCrosshairStyle(string value)
        {
            GameSourceSettingsStore.CombatSettings[CrosshairStyleKey] = NormalizeCrosshairStyle(value);
            Changed?.Invoke(null, EventArgs.Empty);
        }

        public static string NormalizeCrosshairStyle(string value)
        {
            if (string.Equals(value, CrosshairReticle, StringComparison.OrdinalIgnoreCase))
                return CrosshairReticle;
            return CrosshairMinimal;
        }

        public static bool LoadDamagedPartsEnabled()
        {
            return ReadBool(DamagedPartsEnabledKey, true);
        }

        public static void SaveDamagedPartsEnabled(bool enabled)
        {
            GameSourceSettingsStore.CombatSettings[DamagedPartsEnabledKey] = enabled;
            Changed?.Invoke(null, EventArgs.Empty);
        }

        public static bool LoadWorldKillMarkEnabled()
        {
            return ReadBool(WorldKillMarkEnabledKey, true);
        }

        public static void SaveWorldKillMarkEnabled(bool enabled)
        {
            GameSourceSettingsStore.CombatSettings[WorldKillMarkEnabledKey] = enabled;
            Changed?.Invoke(null, EventArgs.Empty);
        }

        public static bool LoadStreakAllKillsEnabled()
        {
            return ReadBool(StreakAllKillsEnabledKey, false);
        }

        public static void SaveStreakAllKillsEnabled(bool enabled)
        {
            GameSourceSettingsStore.CombatSettings[StreakAllKillsEnabledKey] = enabled;
            Changed?.Invoke(null, EventArgs.Empty);
        }

        public static void Reset()
        {
            var settings = GameSourceSettingsStore.CombatSettings;
            settings[BattleModeKey] = null;
            settings[HudLanguageKey] = null;
            settings[LowerDisplayModeKey] = null;
            settings[CrosshairStyleKey] = null;
            settings[DamagedPartsEnabledKey] = null;
            settings[WorldKillMarkEnabledKey] = null;
            settings[StreakAllKillsEnabledKey] = null;
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

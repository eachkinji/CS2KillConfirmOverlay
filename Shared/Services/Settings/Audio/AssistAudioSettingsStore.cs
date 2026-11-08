// Shared by Widget (UWP) and CompatibilityHost; extracted from both former copies.
// Per-host lines stay inside #if WINDOWS_UWP blocks. See Tools/SharedSource/extract_shared.py.
using Windows.Storage;

#if WINDOWS_UWP
namespace KillConfirmGameBar.Services
#else
namespace KillConfirmCompatibility.Services
#endif
{
    internal static class AssistAudioSettingsStore
    {
        private const string CrossfireSettingKey = "CrossfireAssistAudioEnabled";
        private const string ValorantSettingKey = "ValorantAssistAudioEnabled";
        private const string OverwatchSettingKey = "OverwatchAssistAudioEnabled";
        private const string ModernWarfare2019SettingKey = "ModernWarfare2019AssistAudioEnabled";

        public static bool IsSupported(GameStyleMode style)
        {
            return style == GameStyleMode.Valorant
                || style == GameStyleMode.Overwatch
                || style == GameStyleMode.ModernWarfare2019;
        }

        public static bool Load(GameStyleMode style)
        {
            string key = GetSettingKey(style);
            if (key == null)
            {
                return false;
            }

            object value = GameSourceSettingsStore.CombatSettings[key];
            if (value is bool boolValue)
            {
                return boolValue;
            }

            return value is string text && bool.TryParse(text, out bool parsed) && parsed;
        }

        public static void Save(GameStyleMode style, bool enabled)
        {
            string key = GetSettingKey(style);
            if (key != null)
            {
                GameSourceSettingsStore.CombatSettings[key] = enabled;
            }
        }

        private static string GetSettingKey(GameStyleMode style)
        {
            switch (style)
            {
                case GameStyleMode.Crossfire:
                    return CrossfireSettingKey;
                case GameStyleMode.Valorant:
                    return ValorantSettingKey;
                case GameStyleMode.Overwatch:
                    return OverwatchSettingKey;
                case GameStyleMode.ModernWarfare2019:
                    return ModernWarfare2019SettingKey;
                default:
                    return null;
            }
        }
    }
}

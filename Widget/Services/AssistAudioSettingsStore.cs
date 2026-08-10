using Windows.Storage;

namespace KillConfirmGameBar.Services
{
    internal static class AssistAudioSettingsStore
    {
        private const string CrossfireSettingKey = "CrossfireAssistAudioEnabled";
        private const string ValorantSettingKey = "ValorantAssistAudioEnabled";

        public static bool Load(GameStyleMode style)
        {
            string key = GetSettingKey(style);
            if (key == null)
            {
                return false;
            }

            object value = ApplicationData.Current.LocalSettings.Values[key];
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
                ApplicationData.Current.LocalSettings.Values[key] = enabled;
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
                default:
                    return null;
            }
        }
    }
}

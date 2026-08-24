using Windows.Storage;

namespace KillConfirmGameBar.Services
{
    internal static class ValorantPackSyncSettingsStore
    {
        private const string SettingKey = "ValorantVoiceIconPackSyncEnabled";

        public static bool Load()
        {
            object value = ApplicationData.Current.LocalSettings.Values[SettingKey];
            if (value is bool boolValue)
            {
                return boolValue;
            }

            if (value is string text && bool.TryParse(text, out bool parsed))
            {
                return parsed;
            }

            // Valorant's official voice and icon packs share the same skin key,
            // so paired selection is the least surprising default.
            return true;
        }

        public static void Save(bool enabled)
        {
            ApplicationData.Current.LocalSettings.Values[SettingKey] = enabled;
        }
    }
}

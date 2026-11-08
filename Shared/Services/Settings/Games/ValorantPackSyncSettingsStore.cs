// Shared by Widget (UWP) and CompatibilityHost; extracted from both former copies.
// Per-host lines stay inside #if WINDOWS_UWP blocks. See Tools/SharedSource/extract_shared.py.
using Windows.Storage;

#if WINDOWS_UWP
namespace KillConfirmGameBar.Services
#else
namespace KillConfirmCompatibility.Services
#endif
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

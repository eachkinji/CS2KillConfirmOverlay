using System;
using Windows.Storage;

namespace KillConfirmGameBar.Services
{
    internal static class AutoCloseOnGameExitSettingsStore
    {
        private const string SettingKey = "AutoCloseOnGameExit";

        internal static bool Load()
        {
            object value = ApplicationData.Current.LocalSettings.Values[SettingKey];
            return value is bool enabled && enabled;
        }

        internal static void Save(bool enabled)
        {
            ApplicationData.Current.LocalSettings.Values[SettingKey] = enabled;
        }
    }
}
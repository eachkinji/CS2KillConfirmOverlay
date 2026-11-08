using System;
using Windows.Storage;

namespace KillConfirmGameBar.Services
{
    internal static class AutoCloseOnGameExitSettingsStore
    {
        private static string ScopedKey => SettingKey + "_" + GameSourceSettingsStore.Current;
        private const string SettingKey = "AutoCloseOnGameExit";

        internal static bool Load()
        {
            var values = ApplicationData.Current.LocalSettings.Values;
            object value = values[ScopedKey] ?? (GameSourceSettingsStore.IsLol ? null : values[SettingKey]);
            return value is bool enabled && enabled;
        }

        internal static void Save(bool enabled)
        {
            ApplicationData.Current.LocalSettings.Values[ScopedKey] = enabled;
            if (!GameSourceSettingsStore.IsLol) ApplicationData.Current.LocalSettings.Values[SettingKey] = enabled;
        }
    }
}
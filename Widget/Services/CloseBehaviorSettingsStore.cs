using System;
using Windows.Storage;

namespace KillConfirmGameBar.Services
{
    internal static class CloseBehaviorSettingsStore
    {
        private const string SettingKey = "CloseWindowBehavior";
        internal const string KeepRunningMode = "tray";
        internal const string ExitMode = "exit";

        internal static string Load()
        {
            string value = ApplicationData.Current.LocalSettings.Values[SettingKey] as string;
            return string.Equals(value, KeepRunningMode, StringComparison.OrdinalIgnoreCase)
                ? KeepRunningMode
                : ExitMode;
        }

        internal static bool KeepRunningAfterSettingsClose =>
            string.Equals(Load(), KeepRunningMode, StringComparison.OrdinalIgnoreCase);

        internal static void Save(string mode)
        {
            ApplicationData.Current.LocalSettings.Values[SettingKey] =
                string.Equals(mode, KeepRunningMode, StringComparison.OrdinalIgnoreCase)
                    ? KeepRunningMode
                    : ExitMode;
        }
    }
}

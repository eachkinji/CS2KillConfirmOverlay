using System;
using System.IO;
using System.Threading.Tasks;
using KillConfirmCompatibility.Contracts;
using Windows.Storage;

namespace KillConfirmGameBar.Features.CompatibilityDisplay
{
    internal static class CompatibilityDisplayRuntime
    {
        private const string EnabledKey = "CompatibilityDisplay.Enabled";
        private static bool _launching;
        private static DateTimeOffset _lastLaunch;
        public static string ConfigurationPath => Path.Combine(ApplicationData.Current.LocalFolder.Path, DisplayFiles.FolderName, DisplayFiles.ConfigurationName);
        public static string StatusPath => Path.Combine(ApplicationData.Current.LocalFolder.Path, DisplayFiles.FolderName, DisplayFiles.StatusName);
        public static bool IsEnabled => ApplicationData.Current.LocalSettings.Values[EnabledKey] is bool enabled && enabled;
        public static DisplayConfiguration Load()
        {
            var config = DisplayFiles.Read<DisplayConfiguration>(ConfigurationPath) ?? new DisplayConfiguration();
            config.Normalize(); return config;
        }
        public static void Update(Action<DisplayConfiguration> change)
        {
            DisplayFiles.Update(ConfigurationPath, change);
            ApplicationData.Current.LocalSettings.Values[EnabledKey] = Load().Enabled;
        }
        public static DisplayStatus ReadStatus() => DisplayFiles.Read<DisplayStatus>(StatusPath);
        public static bool IsRunning(DisplayStatus status) => status != null && status.ProcessId > 0
            && Math.Abs(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - status.Timestamp) < 5000;
        public static async Task<bool> EnsureStartedAsync()
        {
            DisplayConfiguration config = Load();
            ApplicationData.Current.LocalSettings.Values[EnabledKey] = config.Enabled;
            if (!config.Enabled || IsRunning(ReadStatus())) return true;
            if (_launching || DateTimeOffset.UtcNow - _lastLaunch < TimeSpan.FromSeconds(4)) return true;
            _launching = true; _lastLaunch = DateTimeOffset.UtcNow;
            try { return await KillConfirmWidgetPage.TryLaunchFullTrustHelperAsync("CompatibilityDisplay"); }
            finally { _launching = false; }
        }
    }
}

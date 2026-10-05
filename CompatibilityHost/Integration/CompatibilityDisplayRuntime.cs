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
        private static readonly System.Threading.SemaphoreSlim ModeGate = new System.Threading.SemaphoreSlim(1, 1);
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
        }
        public static async Task<bool> SetModeAsync(bool compatibility)
        {
            await ModeGate.WaitAsync();
            try
            {
                ApplicationData.Current.LocalSettings.Values[EnabledKey] = true;
                long request = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                Update(c => { c.Enabled = compatibility; c.ModeRequest = request; });
                if (compatibility) return await EnsureStartedAsync();
                await KillConfirmWidgetPage.TryLaunchFullTrustHelperAsync("StopCompatibilityDisplay");
                for (int i = 0; i < 100; i++)
                {
                    DisplayStatus status = ReadStatus();
                    var stopped = DisplayFiles.Read<DisplayStopResult>(Path.Combine(Path.GetDirectoryName(ConfigurationPath), "stop-result.json"));
                    if ((stopped?.ModeRequest == request && stopped.Stopped) || (status != null && status.ProcessId == 0))
                    {
                        ApplicationData.Current.LocalSettings.Values[EnabledKey] = false;
                        return true;
                    }
                    await Task.Delay(100);
                }
                return false;
            }
            finally { ModeGate.Release(); }
        }
        public static DisplayStatus ReadStatus() => DisplayFiles.Read<DisplayStatus>(StatusPath);
        public static bool IsRunning(DisplayStatus status) => status != null && status.ProcessId > 0
            && Math.Abs(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - status.Timestamp) < 5000;
        public static async Task<bool> EnsureSelectedModeAsync()
        {
            if (Load().Enabled) return await EnsureStartedAsync();
            if (IsEnabled) return await SetModeAsync(false);
            return true;
        }
        public static async Task<bool> EnsureStartedAsync()
        {
            DisplayConfiguration config = Load();
            if (config.Enabled) ApplicationData.Current.LocalSettings.Values[EnabledKey] = true;
            if (!config.Enabled || IsRunning(ReadStatus())) return true;
            if (_launching || DateTimeOffset.UtcNow - _lastLaunch < TimeSpan.FromSeconds(4)) return true;
            _launching = true; _lastLaunch = DateTimeOffset.UtcNow;
            try { return await KillConfirmWidgetPage.TryLaunchFullTrustHelperAsync("CompatibilityDisplay"); }
            finally { _launching = false; }
        }
    }
}

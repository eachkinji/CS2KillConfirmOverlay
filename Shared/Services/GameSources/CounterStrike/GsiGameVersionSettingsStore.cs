// Shared by Widget (UWP) and CompatibilityHost; extracted from both former copies.
// Per-host lines stay inside #if WINDOWS_UWP blocks. See Tools/SharedSource/extract_shared.py.
using System;
using System.Threading.Tasks;
using Windows.Data.Json;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.Web.Http;

#if WINDOWS_UWP
namespace KillConfirmGameBar.Services
#else
namespace KillConfirmCompatibility.Services
#endif
{
    internal static class GsiGameVersionSettingsStore
    {
        public const string Cs2 = "cs2";
        public const string CsgoLegacy = "csgo_legacy";

        private const string SettingKey = "GsiGameVersion";
#if WINDOWS_UWP
        private static readonly Uri SettingsUri =
#else
        private static Uri SettingsUri =>
#endif
            LocalServiceEndpoints.Build("/gsi-game/settings");

        public static event EventHandler VersionChanged;

        public static string Load()
        {
            string value = ApplicationData.Current.LocalSettings.Values[SettingKey] as string;
            return Normalize(value);
        }

        public static void Save(string value)
        {
            string normalized = Normalize(value);
            string previous = Load();
            ApplicationData.Current.LocalSettings.Values[SettingKey] = normalized;
            if (!string.Equals(previous, normalized, StringComparison.Ordinal))
            {
                VersionChanged?.Invoke(null, EventArgs.Empty);
            }
        }

        public static async Task SyncAsync()
        {
            await GameSourceSettingsStore.RefreshAsync();
        }

        private static string Normalize(string value)
        {
            return string.Equals(value, CsgoLegacy, StringComparison.OrdinalIgnoreCase)
                ? CsgoLegacy
                : Cs2;
        }
    }
}

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
    internal static class InterruptPreviousKillAudioSettingsStore
    {
        private static string ScopedKey => SettingKey + "_" + GameSourceSettingsStore.Current;
        private const string SettingKey = "InterruptPreviousKillAudio";
#if WINDOWS_UWP
        private static readonly Uri SettingsUri =
#else
        private static Uri SettingsUri =>
#endif
            LocalServiceEndpoints.Build("/audio/interrupt-previous");

        internal static bool Load()
        {
            var values = ApplicationData.Current.LocalSettings.Values;
            object value = values[ScopedKey] ?? (GameSourceSettingsStore.Current == "cs2" ? values[SettingKey] : null);
            if (value is bool enabled)
            {
                return enabled;
            }

            if (value is string text && bool.TryParse(text, out bool parsed))
            {
                return parsed;
            }

            // Only a genuinely missing/unrecognized value receives the first-install default.
            return true;
        }

        internal static void Save(bool enabled)
        {
            ApplicationData.Current.LocalSettings.Values[ScopedKey] = enabled;
        }

        internal static async Task SyncAsync()
        {
            var request = new JsonObject
            {
                ["enabled"] = JsonValue.CreateBooleanValue(Load())
            };

            using (var client = await LocalServiceAuth.CreateHttpClientAsync())
            using (var content = new HttpStringContent(
                request.Stringify(),
                UnicodeEncoding.Utf8,
                "application/json"))
            using (HttpResponseMessage response = await client.PostAsync(SettingsUri, content))
            {
                response.EnsureSuccessStatusCode();
            }
        }
    }
}

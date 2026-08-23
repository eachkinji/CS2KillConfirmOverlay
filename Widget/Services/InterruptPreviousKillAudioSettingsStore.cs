using System;
using System.Threading.Tasks;
using Windows.Data.Json;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.Web.Http;

namespace KillConfirmGameBar.Services
{
    internal static class InterruptPreviousKillAudioSettingsStore
    {
        private const string SettingKey = "InterruptPreviousKillAudio";
        private static readonly Uri SettingsUri =
            LocalServiceEndpoints.Build("/audio/interrupt-previous");

        internal static bool Load()
        {
            object value = ApplicationData.Current.LocalSettings.Values[SettingKey];
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
            ApplicationData.Current.LocalSettings.Values[SettingKey] = enabled;
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

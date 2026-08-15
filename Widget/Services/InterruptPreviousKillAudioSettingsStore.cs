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
            new Uri("http://127.0.0.1:10087/audio/interrupt-previous");

        internal static bool Load()
        {
            object value = ApplicationData.Current.LocalSettings.Values[SettingKey];
            return value is bool enabled && enabled;
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

using System;
using System.Threading.Tasks;
using Windows.Data.Json;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.Web.Http;

namespace KillConfirmGameBar.Services
{
    internal static class GsiGameVersionSettingsStore
    {
        public const string Cs2 = "cs2";
        public const string CsgoLegacy = "csgo_legacy";

        private const string SettingKey = "GsiGameVersion";
        private static readonly Uri SettingsUri =
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
            var request = new JsonObject
            {
                ["version"] = JsonValue.CreateStringValue(Load())
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

        private static string Normalize(string value)
        {
            return string.Equals(value, CsgoLegacy, StringComparison.OrdinalIgnoreCase)
                ? CsgoLegacy
                : Cs2;
        }
    }
}

using System;
using System.Threading.Tasks;
using Windows.Data.Json;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.Web.Http;

namespace KillConfirmGameBar.Services
{
    internal static class DeveloperModeSettingsStore
    {
        private const string SettingKey = "DeveloperModeEnabled";
        private static readonly Uri DeveloperSettingsUri =
            LocalServiceEndpoints.Build("/developer/settings");
        private static bool _isEnabled = ReadStoredValue();

        public static event EventHandler<bool> Changed;

        public static bool IsEnabled => _isEnabled;

        public static void Save(bool enabled)
        {
            if (_isEnabled == enabled)
            {
                return;
            }

            _isEnabled = enabled;
            ApplicationData.Current.LocalSettings.Values[SettingKey] = enabled;
            Changed?.Invoke(null, enabled);
        }

        public static async Task SyncToServiceAsync()
        {
            var request = new JsonObject
            {
                ["enabled"] = JsonValue.CreateBooleanValue(_isEnabled)
            };

            using (HttpClient client = await LocalServiceAuth.CreateHttpClientAsync())
            using (var content = new HttpStringContent(
                request.Stringify(),
                UnicodeEncoding.Utf8,
                "application/json"))
            using (HttpResponseMessage response = await client.PostAsync(DeveloperSettingsUri, content))
            {
                response.EnsureSuccessStatusCode();
            }
        }

        private static bool ReadStoredValue()
        {
            object value = ApplicationData.Current.LocalSettings.Values[SettingKey];
            if (value is bool enabled)
            {
                return enabled;
            }

            return value is string text && bool.TryParse(text, out bool parsed) && parsed;
        }
    }
}

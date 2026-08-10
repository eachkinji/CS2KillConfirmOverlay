using System;
using System.Threading.Tasks;
using KillConfirmGameBar.Services;
using Windows.Data.Json;
using Windows.Storage.Streams;
using Windows.UI.Xaml.Controls;
using Windows.Web.Http;

namespace KillConfirmGameBar.Controls.Settings
{
    internal static class SharedStreakSettingsPanelSupport
    {
        private static readonly Uri SettingsUri = new Uri("http://127.0.0.1:10087/streak/settings");

        public static void Load(GameStyleMode style, ComboBox selector)
        {
            SharedStreakSettingsStore.Select(selector, SharedStreakSettingsStore.Load(style));
        }

        public static async Task SaveAndSyncAsync(GameStyleMode style, ComboBox selector)
        {
            string mode = SharedStreakSettingsStore.Read(selector);
            SharedStreakSettingsStore.Save(style, mode);

            try
            {
                var request = new JsonObject
                {
                    ["active"] = JsonValue.CreateBooleanValue(GameStyleService.Current == style),
                    ["streak_mode"] = JsonValue.CreateStringValue(mode),
                    ["assist_audio_enabled"] = JsonValue.CreateBooleanValue(
                        style == GameStyleMode.Valorant
                        && AssistAudioSettingsStore.Load(style)),
                    ["assist_audio_setting_active"] = JsonValue.CreateBooleanValue(
                        style == GameStyleMode.Valorant)
                };

                using (var client = await LocalServiceAuth.CreateHttpClientAsync())
                using (var content = new HttpStringContent(
                    request.Stringify(),
                    UnicodeEncoding.Utf8,
                    "application/json"))
                using (HttpResponseMessage response = await client.PostAsync(SettingsUri, content))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        App.Log("Set shared streak mode failed: status=" + response.StatusCode);
                    }
                }
            }
            catch (Exception ex)
            {
                App.Log("Set shared streak mode failed: " + ex.Message);
            }
        }
    }
}

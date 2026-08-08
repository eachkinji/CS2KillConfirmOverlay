using System;
using System.Threading.Tasks;
using KillConfirmGameBar.Controls.GameStyles;
using KillConfirmGameBar.Services;
using Windows.Data.Json;
using Windows.Storage.Streams;
using Windows.UI.Xaml.Controls;
using Windows.Web.Http;

namespace KillConfirmGameBar
{
    public sealed partial class KillConfirmWidgetPage
    {
        private async void OnCrossfireGameplaySettingChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressCrossfireGameplaySettingEvents)
            {
                return;
            }

            SaveCrossfireGameplaySettings();
            try
            {
                await EnsureServiceAvailableAsync();
            }
            catch (Exception ex)
            {
                App.Log("Set CrossFire gameplay settings failed: " + ex);
            }
        }

        private void LoadCrossfireGameplaySettings(CrossfireAdvancedEffectsPanel panel)
        {
            if (panel == null)
            {
                return;
            }

            CrossfireGameplaySettingsValues settings = CrossfireGameplaySettingsStore.Load();
            _suppressCrossfireGameplaySettingEvents = true;
            try
            {
                panel.SelectSettings(
                    settings.StreakMode,
                    settings.FirstKillSpecialAudio,
                    settings.LastKillSpecialAudio);
            }
            finally
            {
                _suppressCrossfireGameplaySettingEvents = false;
            }

            CrossfireGameplaySettingsStore.Save(settings);
        }

        private void SaveCrossfireGameplaySettings()
        {
            CrossfireGameplaySettingsValues fallback = CrossfireGameplaySettingsStore.Load();
            CrossfireAdvancedEffectsPanel panel = _crossfireAdvancedEffectsPanel;
            if (panel == null)
            {
                return;
            }

            CrossfireGameplaySettingsStore.Save(new CrossfireGameplaySettingsValues
            {
                StreakMode = panel.GetSelectedStreakMode(fallback.StreakMode),
                FirstKillSpecialAudio = panel.GetFirstKillSpecialAudio(fallback.FirstKillSpecialAudio),
                LastKillSpecialAudio = panel.GetLastKillSpecialAudio(fallback.LastKillSpecialAudio)
            });
        }

        private async Task SyncCrossfireGameplaySettingsAsync()
        {
            CrossfireGameplaySettingsValues settings = CrossfireGameplaySettingsStore.Load();
            if (_crossfireAdvancedEffectsPanel != null)
            {
                settings.StreakMode = _crossfireAdvancedEffectsPanel.GetSelectedStreakMode(settings.StreakMode);
                settings.FirstKillSpecialAudio = _crossfireAdvancedEffectsPanel.GetFirstKillSpecialAudio(
                    settings.FirstKillSpecialAudio);
                settings.LastKillSpecialAudio = _crossfireAdvancedEffectsPanel.GetLastKillSpecialAudio(
                    settings.LastKillSpecialAudio);
            }

            CrossfireGameplaySettingsStore.Save(settings);
            try
            {
                var request = new JsonObject
                {
                    ["active"] = JsonValue.CreateBooleanValue(
                        GameStyleService.Current == GameStyleMode.Crossfire),
                    ["streak_mode"] = JsonValue.CreateStringValue(settings.StreakMode),
                    ["first_kill_special_audio"] = JsonValue.CreateBooleanValue(
                        settings.FirstKillSpecialAudio),
                    ["last_kill_special_audio"] = JsonValue.CreateBooleanValue(
                        settings.LastKillSpecialAudio)
                };

                using (var client = await LocalServiceAuth.CreateHttpClientAsync())
                using (var content = new HttpStringContent(
                    request.Stringify(),
                    UnicodeEncoding.Utf8,
                    "application/json"))
                using (HttpResponseMessage response = await client.PostAsync(CrossfireSettingsUri, content))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        App.Log("Set CrossFire gameplay settings failed: status=" + response.StatusCode);
                    }
                }
            }
            catch (Exception ex)
            {
                App.Log("Set CrossFire gameplay settings failed: " + ex);
            }
        }
    }
}

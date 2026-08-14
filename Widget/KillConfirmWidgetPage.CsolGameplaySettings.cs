using System;
using System.Threading.Tasks;
using KillConfirmGameBar.Controls.GameStyles;
using KillConfirmGameBar.Services;
using Windows.Data.Json;
using Windows.Storage.Streams;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.Web.Http;

namespace KillConfirmGameBar
{
    public sealed partial class KillConfirmWidgetPage
    {
        private bool _suppressCsolGameplaySettingEvents;

        private async void OnCsolGameplaySettingChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressCsolGameplaySettingEvents)
            {
                return;
            }

            SaveCsolGameplaySettings();
            try
            {
                await EnsureServiceAvailableAsync();
            }
            catch (Exception ex)
            {
                App.Log("Set CSOL gameplay settings failed: " + ex);
            }
        }

        private void LoadCsolGameplaySettings(CsolAdvancedEffectsPanel panel)
        {
            if (panel == null)
            {
                return;
            }

            CsolVoiceSettingsValues settings = CsolVoiceSettingsStore.Load();
            string streakMode = SharedStreakSettingsStore.Load(GameStyleMode.Csol);
            _suppressCsolGameplaySettingEvents = true;
            try
            {
                panel.SelectSettings(
                    streakMode,
                    settings.SpecialVoicePriority,
                    settings.FirstKillIcon,
                    settings.LastKillIcon,
                    settings.VoicePicks);
            }
            finally
            {
                _suppressCsolGameplaySettingEvents = false;
            }

            SharedStreakSettingsStore.Save(GameStyleMode.Csol, streakMode);
        }

        private void SaveCsolGameplaySettings()
        {
            CsolAdvancedEffectsPanel panel = _csolAdvancedEffectsPanel;
            if (panel == null)
            {
                return;
            }

            string streakMode = SharedStreakSettingsStore.Normalize(
                panel.GetSelectedStreakMode(SharedStreakSettingsStore.LifeMode));
            SharedStreakSettingsStore.Save(GameStyleMode.Csol, streakMode);

            CsolVoiceSettingsValues fallback = CsolVoiceSettingsStore.Load();
            CsolVoiceSettingsStore.Save(new CsolVoiceSettingsValues
            {
                VoicePicks = panel.GetVoicePicks(),
                FirstKillIcon = panel.GetFirstKillIcon(fallback.FirstKillIcon),
                LastKillIcon = panel.GetLastKillIcon(fallback.LastKillIcon),
                SpecialVoicePriority = panel.GetSpecialVoicePriority(fallback.SpecialVoicePriority)
            });
        }

        private async Task SyncCsolGameplaySettingsAsync()
        {
            SaveCsolGameplaySettings();
            CsolVoiceSettingsValues settings = CsolVoiceSettingsStore.Load();
            try
            {
                var picks = new JsonObject();
                foreach (var pair in settings.VoicePicks)
                {
                    picks[pair.Key] = JsonValue.CreateStringValue(pair.Value);
                }

                var request = new JsonObject
                {
                    ["voice_picks"] = picks,
                    ["special_voice_priority"] = JsonValue.CreateBooleanValue(
                        settings.SpecialVoicePriority)
                };

                using (var client = await LocalServiceAuth.CreateHttpClientAsync())
                using (var content = new HttpStringContent(
                    request.Stringify(),
                    UnicodeEncoding.Utf8,
                    "application/json"))
                using (HttpResponseMessage response = await client.PostAsync(CsolSettingsUri, content))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        App.Log("Set CSOL settings failed: status=" + response.StatusCode);
                    }
                }
            }
            catch (Exception ex)
            {
                App.Log("Set CSOL settings failed: " + ex);
            }

            await SyncSharedStreakSettingsAsync();
        }
    }
}

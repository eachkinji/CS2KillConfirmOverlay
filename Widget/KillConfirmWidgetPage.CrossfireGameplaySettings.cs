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
        private async void OnCrossfireGameplaySettingChanged(object sender, RoutedEventArgs e)
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
                    settings.HeadshotSpecialAudioPriority,
                    settings.KnifeSpecialAudioPriority,
                    settings.HeadshotSpecialIconPriority,
                    settings.KnifeSpecialIconPriority,
                    settings.FirstKillSpecialAudio,
                    settings.LastKillSpecialAudio,
                    settings.FirstKillEffectEnabled,
                    settings.LastKillEffectEnabled,
                    settings.AssistAudioEnabled);
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
                HeadshotSpecialAudioPriority = panel.GetHeadshotSpecialAudioPriority(fallback.HeadshotSpecialAudioPriority),
                KnifeSpecialAudioPriority = panel.GetKnifeSpecialAudioPriority(fallback.KnifeSpecialAudioPriority),
                HeadshotSpecialIconPriority = panel.GetHeadshotSpecialIconPriority(fallback.HeadshotSpecialIconPriority),
                KnifeSpecialIconPriority = panel.GetKnifeSpecialIconPriority(fallback.KnifeSpecialIconPriority),
                FirstKillSpecialAudio = panel.GetFirstKillSpecialAudio(fallback.FirstKillSpecialAudio),
                LastKillSpecialAudio = panel.GetLastKillSpecialAudio(fallback.LastKillSpecialAudio),
                FirstKillEffectEnabled = panel.GetFirstKillEffectEnabled(fallback.FirstKillEffectEnabled),
                LastKillEffectEnabled = panel.GetLastKillEffectEnabled(fallback.LastKillEffectEnabled),
                AssistAudioEnabled = panel.GetAssistAudioEnabled(fallback.AssistAudioEnabled)
            });
        }

        private async Task SyncCrossfireGameplaySettingsAsync()
        {
            CrossfireGameplaySettingsValues settings = CrossfireGameplaySettingsStore.Load();
            if (_crossfireAdvancedEffectsPanel != null)
            {
                settings.StreakMode = _crossfireAdvancedEffectsPanel.GetSelectedStreakMode(settings.StreakMode);
                settings.HeadshotSpecialAudioPriority = _crossfireAdvancedEffectsPanel.GetHeadshotSpecialAudioPriority(
                    settings.HeadshotSpecialAudioPriority);
                settings.KnifeSpecialAudioPriority = _crossfireAdvancedEffectsPanel.GetKnifeSpecialAudioPriority(
                    settings.KnifeSpecialAudioPriority);
                settings.HeadshotSpecialIconPriority = _crossfireAdvancedEffectsPanel.GetHeadshotSpecialIconPriority(
                    settings.HeadshotSpecialIconPriority);
                settings.KnifeSpecialIconPriority = _crossfireAdvancedEffectsPanel.GetKnifeSpecialIconPriority(
                    settings.KnifeSpecialIconPriority);
                settings.FirstKillSpecialAudio = _crossfireAdvancedEffectsPanel.GetFirstKillSpecialAudio(
                    settings.FirstKillSpecialAudio);
                settings.LastKillSpecialAudio = _crossfireAdvancedEffectsPanel.GetLastKillSpecialAudio(
                    settings.LastKillSpecialAudio);
                settings.FirstKillEffectEnabled = _crossfireAdvancedEffectsPanel.GetFirstKillEffectEnabled(
                    settings.FirstKillEffectEnabled);
                settings.LastKillEffectEnabled = _crossfireAdvancedEffectsPanel.GetLastKillEffectEnabled(
                    settings.LastKillEffectEnabled);
                settings.AssistAudioEnabled = _crossfireAdvancedEffectsPanel.GetAssistAudioEnabled(
                    settings.AssistAudioEnabled);
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
                        settings.LastKillSpecialAudio),
                    ["headshot_special_audio_priority"] = JsonValue.CreateBooleanValue(
                        settings.HeadshotSpecialAudioPriority),
                    ["knife_special_audio_priority"] = JsonValue.CreateBooleanValue(
                        settings.KnifeSpecialAudioPriority),
                    ["assist_audio_enabled"] = JsonValue.CreateBooleanValue(
                        settings.AssistAudioEnabled)
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

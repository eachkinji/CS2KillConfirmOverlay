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
        private SanguoshaAdvancedEffectsPanel _sanguoshaAdvancedEffectsPanel;

        private SanguoshaAdvancedEffectsPanel EnsureSanguoshaAdvancedEffectsPanel()
        {
            if (_sanguoshaAdvancedEffectsPanel == null)
            {
                _sanguoshaAdvancedEffectsPanel = new SanguoshaAdvancedEffectsPanel();
                _sanguoshaAdvancedEffectsPanel.StreakModeSelectionChanged += OnSharedStreakModeSelectionChanged;
                _sanguoshaAdvancedEffectsPanel.PrioritySettingsChanged += OnSanguoshaPrioritySettingsChanged;
                LoadSharedStreakMode(GameStyleMode.Sanguosha);
                _sanguoshaAdvancedEffectsPanel.RefreshSettings();
            }

            _sanguoshaAdvancedEffectsPanel.RefreshSettings();
            return _sanguoshaAdvancedEffectsPanel;
        }

        private async void OnSanguoshaPrioritySettingsChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_sanguoshaAdvancedEffectsPanel == null) return;
            SanguoshaGameplaySettingsValues settings = _sanguoshaAdvancedEffectsPanel.GetPrioritySettings();
            SanguoshaGameplaySettingsStore.Save(settings);
            await SyncSanguoshaGameplaySettingsAsync();
        }

        private async Task SyncSanguoshaGameplaySettingsAsync()
        {
            try
            {
                SanguoshaGameplaySettingsValues settings = SanguoshaGameplaySettingsStore.Load();
                if (_sanguoshaAdvancedEffectsPanel != null)
                {
                    settings = _sanguoshaAdvancedEffectsPanel.GetPrioritySettings(settings);
                    SanguoshaGameplaySettingsStore.Save(settings);
                }

                var request = new JsonObject
                {
                    ["headshot_special_audio_priority"] = JsonValue.CreateBooleanValue(settings.HeadshotSpecialAudioPriority),
                    ["knife_special_audio_priority"] = JsonValue.CreateBooleanValue(settings.KnifeSpecialAudioPriority),
                    ["grenade_special_audio_priority"] = JsonValue.CreateBooleanValue(settings.GrenadeSpecialAudioPriority)
                };

                using (var client = await LocalServiceAuth.CreateHttpClientAsync())
                using (var content = new HttpStringContent(
                    request.Stringify(),
                    UnicodeEncoding.Utf8,
                    "application/json"))
                {
                    await client.PostAsync(LocalServiceEndpoints.Build("/sanguosha/settings"), content);
                }
            }
            catch (Exception ex)
            {
                App.Log("Set Sanguosha settings failed: " + ex.Message);
            }
        }
    }
}

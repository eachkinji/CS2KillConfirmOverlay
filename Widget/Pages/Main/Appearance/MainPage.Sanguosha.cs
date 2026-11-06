using System;
using System.Threading.Tasks;
using KillConfirmGameBar.Services;
using KillConfirmGameBar.Controls.GameStyles;
using Windows.Data.Json;
using Windows.Storage.Streams;
using Windows.UI.Xaml.Controls;
using Windows.Web.Http;

namespace KillConfirmGameBar
{
    public sealed partial class MainPage
    {
        private SanguoshaAdvancedEffectsPanel _sanguoshaAdvancedEffectsPanel;

        private SanguoshaAdvancedEffectsPanel EnsureSanguoshaAdvancedSettingsPanel()
        {
            if (_sanguoshaAdvancedEffectsPanel == null)
            {
                _sanguoshaAdvancedEffectsPanel = new SanguoshaAdvancedEffectsPanel();
                _sanguoshaAdvancedEffectsPanel.StreakModeSelectionChanged += OnStreakModeSelectionChanged;
                _sanguoshaAdvancedEffectsPanel.PrioritySettingsChanged += OnSanguoshaPrioritySettingsChanged;
            }

            _sanguoshaAdvancedEffectsPanel.SelectStreakMode(SharedStreakSettingsStore.Load(GameStyleMode.Sanguosha));
            _sanguoshaAdvancedEffectsPanel.RefreshSettings();
            return _sanguoshaAdvancedEffectsPanel;
        }

        private async void OnSanguoshaPrioritySettingsChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_sanguoshaAdvancedEffectsPanel == null) return;
            SanguoshaGameplaySettingsValues settings = _sanguoshaAdvancedEffectsPanel.GetPrioritySettings();
            SanguoshaGameplaySettingsStore.Save(settings);
            await TrySyncSanguoshaSettingsAsync(settings);
        }

        private static async Task TrySyncSanguoshaSettingsAsync(SanguoshaGameplaySettingsValues settings)
        {
            try
            {
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
                App.Log("Sync Sanguosha settings from desktop failed: " + ex.Message);
            }
        }
    }
}

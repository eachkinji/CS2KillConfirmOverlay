using System;
using System.Threading.Tasks;
using KillConfirmGameBar.Services;
using Windows.Data.Json;
using Windows.Storage.Streams;
using Windows.UI.Xaml.Controls;
using Windows.Web.Http;

namespace KillConfirmGameBar
{
    public sealed partial class KillConfirmWidgetPage
    {
        private async void OnSharedStreakModeSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressSharedStreakModeEvents)
            {
                return;
            }

            GameStyleMode style = GameStyleService.Current;
            ComboBox selector = GetSharedStreakModeSelector(style);
            if (!SharedStreakSettingsStore.IsSupported(style) || selector == null)
            {
                return;
            }

            SharedStreakSettingsStore.Save(style, SharedStreakSettingsStore.Read(selector));
            try
            {
                await EnsureServiceAvailableAsync();
            }
            catch (Exception ex)
            {
                App.Log("Set shared streak mode failed: " + ex);
            }
        }

        private void LoadSharedStreakMode(GameStyleMode style, ComboBox selector)
        {
            if (selector == null)
            {
                return;
            }

            _suppressSharedStreakModeEvents = true;
            try
            {
                SharedStreakSettingsStore.Select(selector, SharedStreakSettingsStore.Load(style));
            }
            finally
            {
                _suppressSharedStreakModeEvents = false;
            }
        }

        private ComboBox GetSharedStreakModeSelector(GameStyleMode style)
        {
            switch (style)
            {
                case GameStyleMode.Battlefield1:
                    return _battlefield1AdvancedEffectsPanel?.StreakModeSelectorControl;
                case GameStyleMode.Pubg:
                    return _pubgAdvancedEffectsPanel?.StreakModeSelectorControl;
                case GameStyleMode.Valorant:
                    return _valorantAdvancedEffectsPanel?.StreakModeSelectorControl;
                default:
                    return null;
            }
        }

        private async Task SyncSharedStreakSettingsAsync()
        {
            GameStyleMode style = GameStyleService.Current;
            bool active = SharedStreakSettingsStore.IsSupported(style);
            string mode = active ? SharedStreakSettingsStore.Load(style) : SharedStreakSettingsStore.LifeMode;
            ComboBox selector = active ? GetSharedStreakModeSelector(style) : null;
            if (selector != null)
            {
                mode = SharedStreakSettingsStore.Read(selector, mode);
                SharedStreakSettingsStore.Save(style, mode);
            }

            try
            {
                var request = new JsonObject
                {
                    ["active"] = JsonValue.CreateBooleanValue(active),
                    ["streak_mode"] = JsonValue.CreateStringValue(mode)
                };

                using (var client = await LocalServiceAuth.CreateHttpClientAsync())
                using (var content = new HttpStringContent(
                    request.Stringify(),
                    UnicodeEncoding.Utf8,
                    "application/json"))
                using (HttpResponseMessage response = await client.PostAsync(SharedStreakSettingsUri, content))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        App.Log("Set shared streak mode failed: status=" + response.StatusCode);
                    }
                }
            }
            catch (Exception ex)
            {
                App.Log("Set shared streak mode failed: " + ex);
            }
        }
    }
}

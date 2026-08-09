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
            if (!SharedStreakSettingsStore.IsSupported(style))
            {
                return;
            }

            SharedStreakSettingsStore.Save(
                style,
                ReadSharedStreakMode(style, SharedStreakSettingsStore.LifeMode));
            try
            {
                await EnsureServiceAvailableAsync();
            }
            catch (Exception ex)
            {
                App.Log("Set shared streak mode failed: " + ex);
            }
        }

        private void LoadSharedStreakMode(GameStyleMode style)
        {
            if (!SharedStreakSettingsStore.IsSupported(style))
            {
                return;
            }

            _suppressSharedStreakModeEvents = true;
            try
            {
                SelectSharedStreakMode(style, SharedStreakSettingsStore.Load(style));
            }
            finally
            {
                _suppressSharedStreakModeEvents = false;
            }
        }

        private string ReadSharedStreakMode(GameStyleMode style, string fallback)
        {
            switch (style)
            {
                case GameStyleMode.Valorant:
                    return _valorantAdvancedEffectsPanel?.GetSelectedStreakMode(fallback) ?? fallback;
                case GameStyleMode.Battlefield1:
                    return _battlefield1AdvancedEffectsPanel?.GetSelectedStreakMode(fallback) ?? fallback;
                case GameStyleMode.Battlefield5:
                    return _battlefield5AdvancedEffectsPanel?.GetSelectedStreakMode(fallback) ?? fallback;
                case GameStyleMode.Battlefield4:
                    return _battlefield4AdvancedEffectsPanel?.GetSelectedStreakMode(fallback) ?? fallback;
                case GameStyleMode.Battlefield2042:
                    return _battlefield2042AdvancedEffectsPanel?.GetSelectedStreakMode(fallback) ?? fallback;
                case GameStyleMode.Pubg:
                    return _pubgAdvancedEffectsPanel?.GetSelectedStreakMode(fallback) ?? fallback;
                case GameStyleMode.DeltaForce:
                    return _deltaForceAdvancedEffectsPanel?.GetSelectedStreakMode(fallback) ?? fallback;
                default:
                    return fallback;
            }
        }

        private void SelectSharedStreakMode(GameStyleMode style, string value)
        {
            switch (style)
            {
                case GameStyleMode.Valorant:
                    _valorantAdvancedEffectsPanel?.SelectStreakMode(value);
                    break;
                case GameStyleMode.Battlefield1:
                    _battlefield1AdvancedEffectsPanel?.SelectStreakMode(value);
                    break;
                case GameStyleMode.Battlefield5:
                    _battlefield5AdvancedEffectsPanel?.SelectStreakMode(value);
                    break;
                case GameStyleMode.Battlefield4:
                    _battlefield4AdvancedEffectsPanel?.SelectStreakMode(value);
                    break;
                case GameStyleMode.Battlefield2042:
                    _battlefield2042AdvancedEffectsPanel?.SelectStreakMode(value);
                    break;
                case GameStyleMode.Pubg:
                    _pubgAdvancedEffectsPanel?.SelectStreakMode(value);
                    break;
                case GameStyleMode.DeltaForce:
                    _deltaForceAdvancedEffectsPanel?.SelectStreakMode(value);
                    break;
            }
        }

        private async Task SyncSharedStreakSettingsAsync()
        {
            GameStyleMode style = GameStyleService.Current;
            bool active = SharedStreakSettingsStore.IsSupported(style);
            string mode = active
                ? SharedStreakSettingsStore.Load(style)
                : SharedStreakSettingsStore.LifeMode;
            if (active)
            {
                mode = ReadSharedStreakMode(style, mode);
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

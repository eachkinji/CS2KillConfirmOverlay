using System;
using System.Threading.Tasks;
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

        private async void OnValorantAssistAudioToggled(object sender, RoutedEventArgs e)
        {
            if (_suppressSharedStreakModeEvents)
            {
                return;
            }

            bool enabled = _valorantAdvancedEffectsPanel?.GetAssistAudioEnabled(
                AssistAudioSettingsStore.Load(GameStyleMode.Valorant)) ?? false;
            AssistAudioSettingsStore.Save(GameStyleMode.Valorant, enabled);
            try
            {
                await EnsureServiceAvailableAsync();
            }
            catch (Exception ex)
            {
                App.Log("Set VAL assist audio failed: " + ex);
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
                case GameStyleMode.Doubao:
                    return _doubaoAdvancedEffectsPanel?.GetSelectedStreakMode(fallback) ?? fallback;
                case GameStyleMode.Dagoujiao:
                    return _dagoujiaoAdvancedEffectsPanel?.GetSelectedStreakMode(fallback) ?? fallback;
                case GameStyleMode.Csol:
                    return _csolAdvancedEffectsPanel?.GetSelectedStreakMode(fallback) ?? fallback;
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
                case GameStyleMode.Doubao:
                    _doubaoAdvancedEffectsPanel?.SelectStreakMode(value);
                    break;
                case GameStyleMode.Dagoujiao:
                    _dagoujiaoAdvancedEffectsPanel?.SelectStreakMode(value);
                    break;
                case GameStyleMode.Csol:
                    _csolAdvancedEffectsPanel?.SelectStreakMode(value);
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
            bool assistAudioEnabled = false;
            if (active)
            {
                mode = ReadSharedStreakMode(style, mode);
                SharedStreakSettingsStore.Save(style, mode);
                if (style == GameStyleMode.Valorant)
                {
                    assistAudioEnabled = _valorantAdvancedEffectsPanel?.GetAssistAudioEnabled(
                        AssistAudioSettingsStore.Load(style))
                        ?? AssistAudioSettingsStore.Load(style);
                    AssistAudioSettingsStore.Save(style, assistAudioEnabled);
                }
            }

            try
            {
                var request = new JsonObject
                {
                    ["active"] = JsonValue.CreateBooleanValue(active),
                    ["streak_mode"] = JsonValue.CreateStringValue(mode),
                    ["assist_audio_enabled"] = JsonValue.CreateBooleanValue(assistAudioEnabled),
                    ["assist_audio_setting_active"] = JsonValue.CreateBooleanValue(
                        style == GameStyleMode.Valorant)
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

        private async Task SyncSpectatedKillEffectsAsync()
        {
            try
            {
                await SharedStreakSettingsStore.SyncSpectatedKillEffectsAsync();
            }
            catch (Exception ex)
            {
                App.Log("Set spectated player kill effects failed: " + ex);
            }
        }
    }
}

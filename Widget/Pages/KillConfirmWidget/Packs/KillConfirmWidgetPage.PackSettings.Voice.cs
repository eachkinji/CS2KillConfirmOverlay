using System;
using System.Threading.Tasks;
using KillConfirmGameBar.Services;
using Windows.Data.Json;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Xaml.Controls;
using Windows.Web.Http;

namespace KillConfirmGameBar
{
    public sealed partial class KillConfirmWidgetPage
    {
        private void LoadVoicePackSetting()
        {
            GameStyleMode style = GameStyleService.Current;
            string preset = LoadPackSettingForStyle(
                VoicePackSettingKey,
                style,
                GameStyleService.DefaultVoicePackKey(style));

            if (!TryApplyValorantVoicePackLoadOverride(ref preset)
                && GameStyleService.GetStyleForPackKey(preset) != style)
            {
                preset = GameStyleService.DefaultVoicePackKey(style);
            }

            preset = NormalizeVoicePackPreset(preset);
            SelectVoicePackPreset(preset);
            preset = GetSelectedVoicePackPreset();
            SavePackSettingForStyle(VoicePackSettingKey, style, preset);
        }

        private async Task SyncSelectedVoicePackAsync()
        {
            try
            {
                GameStyleMode requestStyle = GameStyleService.Current;
                string preset = GetEffectiveSelectedVoicePackPreset();
                if (string.IsNullOrWhiteSpace(preset))
                {
                    return;
                }

                VoicePackItem selectedPack = await PackCatalogService.GetVoicePackAsync(preset);
                var request = new JsonObject
                {
                    ["preset"] = JsonValue.CreateStringValue(preset)
                };
                if (selectedPack != null
                    && !selectedPack.IsBuiltIn
                    && !string.IsNullOrWhiteSpace(selectedPack.FolderPath))
                {
                    request["custom_path"] = JsonValue.CreateStringValue(selectedPack.FolderPath);
                    request["display_name"] = JsonValue.CreateStringValue(selectedPack.DisplayName ?? preset);
                }

                using (var client = await LocalServiceAuth.CreateHttpClientAsync())
                using (var content = new HttpStringContent(
                    request.Stringify(),
                    UnicodeEncoding.Utf8,
                    "application/json"))
                using (HttpResponseMessage response = await client.PostAsync(SoundPackUri, content))
                {
                    if (response.IsSuccessStatusCode)
                    {
                        string responseText = await response.Content.ReadAsStringAsync();
                        ApplyVoicePackResponse(responseText, requestStyle, preset);
                        if (requestStyle == GameStyleMode.Dagoujiao)
                        {
                            await DagoujiaoSettingsStore.SyncActiveVoicePackAudioAsync(preset);
                        }
                    }
                    else
                    {
                        App.Log("Voice pack sync failed: HTTP " + (int)response.StatusCode);
                    }
                }
            }
            catch (Exception ex)
            {
                App.Log("Voice pack sync failed without changing SVC health: " + ex);
            }
        }

        private string GetSelectedVoicePackPreset()
        {
            if (PackTestSectionView.VoicePackSelector.SelectedItem is ComboBoxItem item
                && item.Tag is string tag)
            {
                return tag;
            }

            GameStyleMode style = GameStyleService.Current;
            return LoadPackSettingForStyle(
                VoicePackSettingKey,
                style,
                GameStyleService.DefaultVoicePackKey(style));
        }

        private string GetEffectiveSelectedVoicePackPreset()
        {
            string valorantPreset = GetValorantEffectiveSelectedVoicePackPreset();
            if (!string.IsNullOrWhiteSpace(valorantPreset))
            {
                return valorantPreset;
            }

            string selectedPreset = GetSelectedVoicePackPreset();
            return GameStyleService.GetStyleForPackKey(selectedPreset) == GameStyleService.Current
                ? selectedPreset
                : GameStyleService.DefaultVoicePackKey(GameStyleService.Current);
        }

        private void SelectVoicePackPreset(string preset)
        {
            preset = NormalizeVoicePackPreset(preset);
            bool previousSuppression = _suppressVoicePackEvents;
            _suppressVoicePackEvents = true;
            try
            {
                foreach (object option in PackTestSectionView.VoicePackSelector.Items)
                {
                    if (option is ComboBoxItem item
                        && item.Tag is string tag
                        && string.Equals(tag, preset, StringComparison.OrdinalIgnoreCase))
                    {
                        PackTestSectionView.VoicePackSelector.SelectedItem = item;
                        return;
                    }
                }

                PackTestSectionView.VoicePackSelector.SelectedIndex = 0;
            }
            finally
            {
                _suppressVoicePackEvents = previousSuppression;
            }
        }

        private void ApplyVoicePackResponse(string responseText, GameStyleMode requestStyle, string requestedPreset)
        {
            try
            {
                if (GameStyleService.Current != requestStyle)
                {
                    return;
                }

                string currentSavedPreset = NormalizeVoicePackPreset(LoadPackSettingForStyle(
                    VoicePackSettingKey,
                    requestStyle,
                    GameStyleService.DefaultVoicePackKey(requestStyle)));
                if (!string.Equals(
                        currentSavedPreset,
                        NormalizeVoicePackPreset(requestedPreset),
                        StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                JsonObject json = JsonObject.Parse(responseText);
                string preset = NormalizeVoicePackPreset(json.GetNamedString("preset", GetSelectedVoicePackPreset()));
                if (!TryApplyValorantVoicePackResponse(ref preset)
                    && GameStyleService.GetStyleForPackKey(preset) != requestStyle)
                {
                    preset = GameStyleService.DefaultVoicePackKey(requestStyle);
                }

                SavePackSettingForStyle(VoicePackSettingKey, requestStyle, preset);
                SelectVoicePackPreset(preset);
            }
            catch (Exception)
            {
            }
        }

        private static string NormalizeVoicePackPreset(string preset)
        {
            if (string.IsNullOrWhiteSpace(preset))
            {
                return "crossfire_swat_gr";
            }

            if (PackCatalogService.IsImportedVoicePackKey(preset))
            {
                return preset;
            }

            string normalized = NormalizeBattlefieldVoicePackAlias(preset);
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                return normalized;
            }

            normalized = NormalizeCrossfireVoicePackAlias(preset);
            return string.IsNullOrWhiteSpace(normalized) ? preset : normalized;
        }
    }
}

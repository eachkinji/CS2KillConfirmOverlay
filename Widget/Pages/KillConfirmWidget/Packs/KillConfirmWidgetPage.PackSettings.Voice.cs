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
            LoadAudioChannelControls();
        }

        private async Task SyncSelectedVoicePackAsync()
        {
            await SyncAudioChannelsAsync();
        }

        private string GetSelectedVoicePackPreset()
        {
            return SelectedAudioPack(PackTestSectionView.VoicePackSelector);
        }

        private string GetEffectiveSelectedVoicePackPreset()
        {
            return GetSelectedVoicePackPreset();
        }

        private void SelectVoicePackPreset(string preset)
        {
            SelectAudioPack(PackTestSectionView.VoicePackSelector, preset);
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

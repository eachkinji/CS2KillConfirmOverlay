using System;
using KillConfirmGameBar.Controls.GameStyles;
using KillConfirmGameBar.Services;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace KillConfirmGameBar
{
    public sealed partial class KillConfirmWidgetPage
    {
        private bool TrySyncValorantIconPackForVoiceSelection(string preset)
        {
            if (GameStyleService.Current != GameStyleMode.Valorant
                || !ValorantPackSyncSettingsStore.Load()
                || !HasPackOption(PackTestSectionView.IconPackSelector, preset))
            {
                return false;
            }

            SavePackSettingForStyle(IconPackSettingKey, GameStyleService.Current, preset);
            SelectIconPack(preset);
            ConfigureAnimationIconPack(preset);
            _ = ApplyCustomPackOverlaySupportAsync(preset);
            WarmStartupAnimationCacheIfActive();
            return true;
        }

        private bool TrySyncValorantVoicePackForIconSelection(string iconPack)
        {
            // Custom voices have no paired icon pack. Keep an explicit custom
            // selection when the user changes skins, even with pairing enabled.
            if (GameStyleService.Current != GameStyleMode.Valorant
                || !ValorantPackSyncSettingsStore.Load()
                || PackCatalogService.IsImportedVoicePackKey(GetSelectedVoicePackPreset())
                || !HasPackOption(PackTestSectionView.VoicePackSelector, iconPack))
            {
                return false;
            }

            SavePackSettingForStyle(VoicePackSettingKey, GameStyleService.Current, iconPack);
            SelectVoicePackPreset(iconPack);
            return true;
        }

        private static bool HasPackOption(ComboBox selector, string key)
        {
            if (selector == null || string.IsNullOrWhiteSpace(key))
            {
                return false;
            }

            foreach (object option in selector.Items)
            {
                if (option is ComboBoxItem item
                    && item.Tag is string tag
                    && string.Equals(tag, key, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private bool TryApplyValorantLoadedIconPack(string iconPack)
        {
            // Initial pairing is applied after both selectors have loaded, with
            // the saved voice pack as the source of truth.
            return false;
        }

        private bool TryApplyValorantVoicePackLoadOverride(ref string preset)
        {
            // Voice and icon selections are persisted independently. The optional
            // pairing pass runs only after both selector lists are available.
            return false;
        }

        private string GetValorantEffectiveSelectedVoicePackPreset()
        {
            return GameStyleService.Current == GameStyleMode.Valorant
                ? GetSelectedVoicePackPreset()
                : null;
        }

        private bool TryApplyValorantVoicePackResponse(ref string preset)
        {
            if (GameStyleService.Current != GameStyleMode.Valorant)
            {
                return false;
            }

            if (ValorantPackSyncSettingsStore.Load())
            {
                TrySyncValorantIconPackForVoiceSelection(preset);
            }

            return true;
        }

        private void OnValorantPackSyncToggled(object sender, RoutedEventArgs e)
        {
            if (!(sender is ValorantAdvancedEffectsPanel panel))
            {
                return;
            }

            bool enabled = panel.GetPackSyncEnabled(true);
            ValorantPackSyncSettingsStore.Save(enabled);
            if (!enabled || GameStyleService.Current != GameStyleMode.Valorant)
            {
                return;
            }

            string voicePack = GetSelectedVoicePackPreset();
            TrySyncValorantIconPackForVoiceSelection(voicePack);
        }
    }
}

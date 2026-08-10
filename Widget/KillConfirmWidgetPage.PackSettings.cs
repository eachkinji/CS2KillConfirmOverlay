using System;
using System.Threading.Tasks;
using KillConfirmGameBar.Services;
using Windows.Data.Json;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.Web.Http;

namespace KillConfirmGameBar
{
    public sealed partial class KillConfirmWidgetPage
    {
        private async void OnVoicePackSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressVoicePackEvents)
            {
                return;
            }

            try
            {
                string preset = GetSelectedVoicePackPreset();
                if (string.IsNullOrWhiteSpace(preset))
                {
                    return;
                }

                ApplicationData.Current.LocalSettings.Values[VoicePackSettingKey] = preset;
                TrySyncValorantIconPackForVoiceSelection(preset);
                TrySyncBattlefieldIconPackForVoiceSelection(preset);

                await EnsureServiceAvailableAsync();
                await SyncSelectedVoicePackAsync();
            }
            catch (Exception ex)
            {
                App.Log("Voice pack selection failed without changing SVC health: " + ex);
            }
        }

        private void OnIconPackSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressIconPackEvents)
            {
                return;
            }

            string iconPack = GetSelectedIconPack();
            ApplicationData.Current.LocalSettings.Values[IconPackSettingKey] = iconPack;
            if (TrySyncValorantVoicePackForIconSelection(iconPack)
                || TrySyncBattlefieldVoicePackForIconSelection(iconPack))
            {
                _ = SyncSelectedVoicePackAsync();
            }

            Controls.KillConfirmAnimation.ConfigureIconPack(iconPack);

            // For custom packs, detect each overlay capability independently.
            _ = ApplyCustomPackOverlaySupportAsync(iconPack);

            UpdateEliteEffectSelectorState();
            UpdateKillFxSelectorState();
            UpdateWeaponBadgeSelectorState();

            if (_isPageActive)
            {
                _ = WarmStartupAnimationCacheAsync(0);
            }
        }

        private void LoadIconPackSetting()
        {
            GameStyleMode style = GameStyleService.Current;
            string iconPack = ApplicationData.Current.LocalSettings.Values[IconPackSettingKey] as string;
            if (string.IsNullOrWhiteSpace(iconPack)
                || GameStyleService.GetStyleForPackKey(iconPack) != style)
            {
                iconPack = GameStyleService.DefaultIconPackKey(style);
            }

            ApplicationData.Current.LocalSettings.Values[IconPackSettingKey] = iconPack;
            TryApplyValorantLoadedIconPack(iconPack);
            SelectIconPack(iconPack);
            Controls.KillConfirmAnimation.ConfigureIconPack(GetSelectedIconPack());
            _ = ApplyCustomPackOverlaySupportAsync(GetSelectedIconPack());
            UpdateEliteEffectSelectorState();
            UpdateKillFxSelectorState();
            UpdateWeaponBadgeSelectorState();
        }

        private string GetSelectedIconPack()
        {
            if (IconPackSelector?.SelectedItem is ComboBoxItem item
                && item.Tag is string tag
                && !string.IsNullOrWhiteSpace(tag))
            {
                return tag;
            }

            string stored = ApplicationData.Current.LocalSettings.Values[IconPackSettingKey] as string;
            if (!string.IsNullOrWhiteSpace(stored))
            {
                return stored;
            }

            return GameStyleService.DefaultIconPackKey(GameStyleService.Current);
        }

        private bool IsLegacyIconPackSelected()
        {
            return string.Equals(GetSelectedIconPack(), "legacy", StringComparison.OrdinalIgnoreCase);
        }

        private void SelectIconPack(string iconPack)
        {
            _suppressIconPackEvents = true;
            try
            {
                foreach (object option in IconPackSelector.Items)
                {
                    if (option is ComboBoxItem item
                        && item.Tag is string tag
                        && string.Equals(tag, iconPack, StringComparison.OrdinalIgnoreCase))
                    {
                        IconPackSelector.SelectedItem = item;
                        return;
                    }
                }

                IconPackSelector.SelectedIndex = 0;
            }
            finally
            {
                _suppressIconPackEvents = false;
            }
        }

        private void WarmStartupAnimationCacheIfActive()
        {
            if (_isPageActive)
            {
                _ = WarmStartupAnimationCacheAsync(0);
            }
        }
    }
}

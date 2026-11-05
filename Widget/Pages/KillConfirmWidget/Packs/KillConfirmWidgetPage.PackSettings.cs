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
            if (_suppressVoicePackEvents || _suppressAudioChannelEvents || !_packSelectorsInitialized)
            {
                return;
            }

            try
            {
                App.NoteAction("voice selection tag=" + ((PackTestSectionView.VoicePackSelector.SelectedItem as PackSelectorItem)?.Tag as string));
                if (Equals((PackTestSectionView.VoicePackSelector.SelectedItem as PackSelectorItem)?.Tag, PackLibraryNavigation.AddMoreTag))
                {
                    await OpenAudioLibraryAsync(AudioPackKind.SoundEffect, e);
                    return;
                }
                string preset = GetSelectedVoicePackPreset();
                if (string.IsNullOrWhiteSpace(preset))
                {
                    return;
                }

                SaveAudioSelections();
                await TrySyncIconForSoundEffectSelectionAsync(preset);

                await EnsureServiceAvailableAsync();
                await SyncSelectedVoicePackAsync();
            }
            catch (Exception ex)
            {
                App.Log("Voice pack selection failed without changing SVC health: " + ex);
            }
        }

        private async void OnIconPackSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressIconPackEvents || !_packSelectorsInitialized)
            {
                return;
            }

            try
            {
                App.NoteAction("icon selection tag=" + ((PackTestSectionView.IconPackSelector.SelectedItem as PackSelectorItem)?.Tag as string));
                if (Equals((PackTestSectionView.IconPackSelector.SelectedItem as PackSelectorItem)?.Tag, PackLibraryNavigation.AddMoreTag))
                {
                    await OpenPackLibraryAsync(false, e);
                    return;
                }
                GameStyleMode selectionStyle = GameStyleService.Current;
                string iconPack = GetSelectedIconPack();
                SavePackSettingForStyle(IconPackSettingKey, GameStyleService.Current, iconPack);
                bool linked = await TrySyncSoundEffectForIconSelectionAsync(iconPack);
                if (selectionStyle != GameStyleService.Current || !string.Equals(GetSelectedIconPack(), iconPack, StringComparison.OrdinalIgnoreCase)) return;
                if (linked)
                {
                    _ = SyncValorantVoicePackAfterIconSelectionAsync();
                }

                ConfigureAnimationIconPack(iconPack);

                // For custom packs, detect each overlay capability independently.
                _ = ApplyCustomPackOverlaySupportAsync(iconPack);

                UpdateEliteEffectSelectorState();
                UpdateKillFxSelectorState();
                UpdateWeaponBadgeSelectorState();
            }
            catch (Exception ex)
            {
                App.Log("Icon pack selection failed: " + ex);
            }
        }

        private async Task SyncValorantVoicePackAfterIconSelectionAsync()
        {
            try
            {
                await EnsureServiceAvailableAsync();
                await SyncSelectedVoicePackAsync();
            }
            catch (Exception ex)
            {
                App.Log("Valorant icon-to-voice pack sync failed: " + ex);
            }
        }

        private void LoadIconPackSetting()
        {
            GameStyleMode style = GameStyleService.Current;
            string iconPack = LoadPackSettingForStyle(
                IconPackSettingKey,
                style,
                GameStyleService.DefaultIconPackKey(style));

            TryApplyValorantLoadedIconPack(iconPack);
            SelectIconPack(iconPack);
            iconPack = GetSelectedIconPack();
            // Do not write the resolved value here. If the stored pack is
            // temporarily unavailable or was retired, SelectIconPack falls back
            // to the first visible item. Persisting that fallback would silently
            // overwrite the user's saved choice, so leave it untouched until the
            // user explicitly selects another pack.
            ConfigureAnimationIconPack(iconPack);
            _ = ApplyCustomPackOverlaySupportAsync(iconPack);
            UpdateEliteEffectSelectorState();
            UpdateKillFxSelectorState();
            UpdateWeaponBadgeSelectorState();
        }

        private string GetSelectedIconPack()
        {
            if (PackTestSectionView.IconPackSelector?.SelectedItem is PackSelectorItem item
                && item.Tag is string tag
                && tag != PackLibraryNavigation.AddMoreTag
                && !string.IsNullOrWhiteSpace(tag))
            {
                return tag;
            }

            GameStyleMode style = GameStyleService.Current;
            string stored = LoadPackSettingForStyle(
                IconPackSettingKey,
                style,
                GameStyleService.DefaultIconPackKey(style));
            if (!string.IsNullOrWhiteSpace(stored))
            {
                return stored;
            }

            return GameStyleService.DefaultIconPackKey(GameStyleService.Current);
        }

        private void SelectIconPack(string iconPack)
        {
            bool previousSuppression = _suppressIconPackEvents;
            _suppressIconPackEvents = true;
            try
            {
                foreach (object option in PackTestSectionView.IconPackSelector.Items)
                {
                    if (option is PackSelectorItem item
                        && item.Tag is string tag
                        && string.Equals(tag, iconPack, StringComparison.OrdinalIgnoreCase))
                    {
                        PackTestSectionView.IconPackSelector.SelectedItem = item;
                        return;
                    }
                }

                if (PackTestSectionView.IconPackSelector.Items.Count > 0)
                {
                    PackTestSectionView.IconPackSelector.SelectedIndex = 0;
                }
            }
            finally
            {
                _suppressIconPackEvents = previousSuppression;
            }
        }

        private void WarmStartupAnimationCacheIfActive()
        {
            if (_isPageActive)
            {
                _ = WarmStartupAnimationCacheAsync(0);
            }
        }

        private void ConfigureAnimationIconPack(string iconPack)
        {
            if (!Controls.KillConfirmAnimation.IsIconPackConfigured(iconPack))
            {
                LowerFeedbackAnimation?.ReleaseAnimationResourcesForPackChange();
                LowerBadgeAnimation?.ReleaseAnimationResourcesForPackChange();
                CrosshairFeedbackAnimation?.ReleaseAnimationResourcesForPackChange();
                UpperFeedbackAnimation?.ReleaseAnimationResourcesForPackChange();
            }

            Controls.KillConfirmAnimation.ConfigureIconPack(iconPack);
            LowerFeedbackAnimation?.RefreshPresentationLayout();
            LowerBadgeAnimation?.RefreshPresentationLayout();
            ApplyLegacyPrimaryTransform();
        }
    }
}

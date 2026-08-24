using System;
using System.Threading.Tasks;
using KillConfirmGameBar.Services;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace KillConfirmGameBar
{
    public sealed partial class KillConfirmWidgetPage
    {
        private async Task ApplyCustomPackOverlaySupportAsync(string iconPack)
        {
            if (!PackCatalogService.IsImportedIconPackKey(iconPack))
            {
                // Built-in pack 鈥?FX handled by built-in logic, no override needed
                Controls.KillConfirmAnimation.ConfigureCustomPackOverlayCapabilities(false, false, false);
                LoadKillFxSetting();
                return;
            }

            IconPackItem item = await PackCatalogService.RefreshImportedIconPackCapabilitiesAsync(iconPack);
            bool hasKillFx = item?.HasKillFxOverlay == true;
            bool hasEliteOverlay = item?.HasEliteOverlay == true;
            bool hasWeaponBadgeOverlay = item?.HasWeaponBadgeOverlay == true;
            Controls.KillConfirmAnimation.ConfigureCustomPackOverlayCapabilities(
                hasKillFx,
                hasEliteOverlay,
                hasWeaponBadgeOverlay);

            // Custom packs default to off when optional overlay assets are missing, but users can still choose Original.
            int currentElite = GetSelectedEliteEffectLevel();
            if (!hasEliteOverlay && currentElite >= 1 && currentElite <= 3)
            {
                SelectEliteEffectLevel(0);
                ApplicationData.Current.LocalSettings.Values[EliteEffectSettingKey] = 0;
                Controls.KillConfirmAnimation.ConfigureEliteEffectLevel(0);
            }
            else if (hasEliteOverlay && currentElite == 0)
            {
                // Default to level 1 when elite assets are present and were previously off.
                SelectEliteEffectLevel(1);
                ApplicationData.Current.LocalSettings.Values[EliteEffectSettingKey] = 1;
                Controls.KillConfirmAnimation.ConfigureEliteEffectLevel(1);
            }

            int currentWeaponBadgeMode = GetSelectedWeaponBadgeMode();
            if (currentWeaponBadgeMode == 0)
            {
                SelectWeaponBadgeMode(1);
                ApplicationData.Current.LocalSettings.Values[WeaponBadgeSettingKey] = 1;
                Controls.KillConfirmAnimation.ConfigureWeaponBadgeMode(1);
            }

            LoadKillFxSetting();
            UpdateEliteEffectSelectorState();
            UpdateKillFxSelectorState();
            UpdateWeaponBadgeSelectorState();
            if (_isPageActive)
            {
                _ = WarmStartupAnimationCacheAsync(0);
            }
        }

        private void OnEliteEffectSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressEliteEffectEvents)
            {
                return;
            }

            int eliteLevel = GetSelectedEliteEffectLevel();
            ApplicationData.Current.LocalSettings.Values[EliteEffectSettingKey] = eliteLevel;
            Controls.KillConfirmAnimation.ConfigureEliteEffectLevel(eliteLevel);
        }

        private void OnKillFxSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressKillFxEvents)
            {
                return;
            }

            int mode = GetSelectedKillFxMode();
            ApplicationData.Current.LocalSettings.Values[KillFxSettingKey] = mode;
            Controls.KillConfirmAnimation.ConfigureKillFxMode(mode);
        }

        private void OnWeaponBadgeSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressWeaponBadgeEvents)
            {
                return;
            }

            int mode = GetSelectedWeaponBadgeMode();
            ApplicationData.Current.LocalSettings.Values[WeaponBadgeSettingKey] = mode;
            Controls.KillConfirmAnimation.ConfigureWeaponBadgeMode(mode);
        }

        private void OnMainAnimationStyleSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressMainAnimationStyleEvents)
            {
                return;
            }

            int style = GetSelectedMainAnimationStyle();
            ApplicationData.Current.LocalSettings.Values[MainAnimationStyleSettingKey] = style;
            PrimaryKillAnimation?.ReleaseAnimationResourcesForPackChange();
            BadgeKillAnimation?.ReleaseAnimationResourcesForPackChange();
            OverwatchCardAnimation?.ReleaseAnimationResourcesForPackChange();
            ModernWarfare2019UpperAnimation?.ReleaseAnimationResourcesForPackChange();
            Controls.KillConfirmAnimation.ConfigureMainAnimationStyle(style);
            WarmStartupAnimationCacheIfActive();
        }

        private void LoadEliteEffectSetting()
        {
            object stored = ApplicationData.Current.LocalSettings.Values[EliteEffectSettingKey];
            int eliteLevel = 0;
            if (stored is int intValue)
            {
                eliteLevel = intValue;
            }
            else if (stored is string text && int.TryParse(text, out int parsed))
            {
                eliteLevel = parsed;
            }

            eliteLevel = NormalizeEliteEffectMode(eliteLevel);
            SelectEliteEffectLevel(eliteLevel);
            Controls.KillConfirmAnimation.ConfigureEliteEffectLevel(eliteLevel);
            UpdateEliteEffectSelectorState();
        }

        private void LoadKillFxSetting()
        {
            int mode = GetDefaultKillFxModeForSelectedPack();
            object stored = ApplicationData.Current.LocalSettings.Values[KillFxSettingKey];
            if (stored is int intValue)
            {
                mode = NormalizeKillFxMode(intValue);
            }
            else if (stored is bool boolValue)
            {
                mode = boolValue ? 1 : 0;
            }
            else if (stored is string text)
            {
                if (int.TryParse(text, out int parsedMode))
                {
                    mode = NormalizeKillFxMode(parsedMode);
                }
                else if (bool.TryParse(text, out bool parsedBool))
                {
                    mode = parsedBool ? 1 : 0;
                }
            }

            SelectKillFxMode(mode);
            Controls.KillConfirmAnimation.ConfigureKillFxMode(mode);
            UpdateKillFxSelectorState();
        }

        private void LoadWeaponBadgeSetting()
        {
            object stored = ApplicationData.Current.LocalSettings.Values[WeaponBadgeSettingKey];
            int mode = GetDefaultWeaponBadgeModeForSelectedPack();
            if (stored is bool boolValue)
            {
                mode = boolValue ? 1 : 0;
            }
            else if (stored is int intValue)
            {
                mode = NormalizeWeaponBadgeMode(intValue);
            }
            else if (stored is string text)
            {
                if (int.TryParse(text, out int parsedMode))
                {
                    mode = NormalizeWeaponBadgeMode(parsedMode);
                }
                else if (bool.TryParse(text, out bool parsedBool))
                {
                    mode = parsedBool ? 1 : 0;
                }
            }

            SelectWeaponBadgeMode(mode);
            Controls.KillConfirmAnimation.ConfigureWeaponBadgeMode(mode);
            UpdateWeaponBadgeSelectorState();
        }

        private void LoadMainAnimationStyleSetting()
        {
            object stored = ApplicationData.Current.LocalSettings.Values[MainAnimationStyleSettingKey];
            int style = 1;
            if (stored is int intValue)
            {
                style = intValue;
            }
            else if (stored is string text && int.TryParse(text, out int parsed))
            {
                style = parsed;
            }

            style = Math.Max(1, Math.Min(2, style));
            SelectMainAnimationStyle(style);
            Controls.KillConfirmAnimation.ConfigureMainAnimationStyle(style);
        }

        private bool SupportsEliteOverlayForSelectedIconPack()
        {
            string iconPack = GetSelectedIconPack();
            if (SupportsBuiltInCodeIconPack(iconPack))
            {
                return true;
            }

            if (PackCatalogService.IsImportedIconPackKey(iconPack))
            {
                return true;
            }

            return false;
        }

        private bool SupportsKillFxForSelectedIconPack()
        {
            string iconPack = GetSelectedIconPack();
            if (SupportsBuiltInCodeIconPack(iconPack))
            {
                return true;
            }

            return PackCatalogService.IsImportedIconPackKey(iconPack);
        }

        private bool SupportsWeaponBadgeForSelectedIconPack()
        {
            string iconPack = GetSelectedIconPack();
            if (SupportsBuiltInCodeIconPack(iconPack))
            {
                return true;
            }

            if (PackCatalogService.IsImportedIconPackKey(iconPack))
            {
                return true;
            }

            return false;
        }

        private int GetSelectedEliteEffectLevel()
        {
            if (EliteEffectSelector == null)
            {
                return 0;
            }

            if (EliteEffectSelector.SelectedItem is ComboBoxItem item
                && item.Tag is string tag
                && int.TryParse(tag, out int level))
            {
                return NormalizeEliteEffectMode(level);
            }

            return 0;
        }

        private int GetSelectedWeaponBadgeMode()
        {
            if (WeaponBadgeSelector == null)
            {
                return 0;
            }

            if (WeaponBadgeSelector.SelectedItem is ComboBoxItem item
                && item.Tag is string tag
                && int.TryParse(tag, out int mode))
            {
                return NormalizeWeaponBadgeMode(mode);
            }

            return 0;
        }

        private int GetSelectedKillFxMode()
        {
            if (KillFxSelector == null) return 1;
            if (KillFxSelector.SelectedItem is ComboBoxItem item
                && item.Tag is string tag
                && int.TryParse(tag, out int mode))
            {
                return NormalizeKillFxMode(mode);
            }

            return 1;
        }

        private int GetSelectedMainAnimationStyle()
        {
            if (MainAnimationStyleSelector == null)
            {
                return 1;
            }

            if (MainAnimationStyleSelector.SelectedItem is ComboBoxItem item
                && item.Tag is string tag
                && int.TryParse(tag, out int style))
            {
                return Math.Max(1, Math.Min(2, style));
            }

            return 1;
        }

        private void SelectEliteEffectLevel(int eliteLevel)
        {
            if (EliteEffectSelector == null)
            {
                return;
            }

            _suppressEliteEffectEvents = true;
            try
            {
                string target = NormalizeEliteEffectMode(eliteLevel).ToString();
                foreach (object option in EliteEffectSelector.Items)
                {
                    if (option is ComboBoxItem item
                        && item.Tag is string tag
                        && string.Equals(tag, target, StringComparison.OrdinalIgnoreCase))
                    {
                        EliteEffectSelector.SelectedItem = item;
                        return;
                    }
                }

                EliteEffectSelector.SelectedIndex = 0;
            }
            finally
            {
                _suppressEliteEffectEvents = false;
            }
        }

        private void SelectKillFxMode(int mode)
        {
            if (KillFxSelector == null) return;
            _suppressKillFxEvents = true;
            try
            {
                string target = NormalizeKillFxMode(mode).ToString();
                foreach (object option in KillFxSelector.Items)
                {
                    if (option is ComboBoxItem item
                        && item.Tag is string tag
                        && string.Equals(tag, target, StringComparison.OrdinalIgnoreCase))
                    {
                        KillFxSelector.SelectedItem = item;
                        return;
                    }
                }
                KillFxSelector.SelectedIndex = 0;
            }
            finally
            {
                _suppressKillFxEvents = false;
            }
        }

        private void SelectWeaponBadgeMode(int mode)
        {
            if (WeaponBadgeSelector == null)
            {
                return;
            }

            _suppressWeaponBadgeEvents = true;
            try
            {
                string target = NormalizeWeaponBadgeMode(mode).ToString();
                foreach (object option in WeaponBadgeSelector.Items)
                {
                    if (option is ComboBoxItem item
                        && item.Tag is string tag
                        && string.Equals(tag, target, StringComparison.OrdinalIgnoreCase))
                    {
                        WeaponBadgeSelector.SelectedItem = item;
                        return;
                    }
                }

                WeaponBadgeSelector.SelectedIndex = 0;
            }
            finally
            {
                _suppressWeaponBadgeEvents = false;
            }
        }

        private void SelectMainAnimationStyle(int style)
        {
            if (MainAnimationStyleSelector == null)
            {
                return;
            }

            _suppressMainAnimationStyleEvents = true;
            try
            {
                string target = Math.Max(1, Math.Min(2, style)).ToString();
                foreach (object option in MainAnimationStyleSelector.Items)
                {
                    if (option is ComboBoxItem item
                        && item.Tag is string tag
                        && string.Equals(tag, target, StringComparison.OrdinalIgnoreCase))
                    {
                        MainAnimationStyleSelector.SelectedItem = item;
                        return;
                    }
                }

                MainAnimationStyleSelector.SelectedIndex = 0;
            }
            finally
            {
                _suppressMainAnimationStyleEvents = false;
            }
        }

        private void UpdateEliteEffectSelectorState()
        {
            if (EliteEffectSelector == null) return;
            bool supportsEliteOverlay = SupportsEliteOverlayForSelectedIconPack();
            bool showOriginalOptions = PackCatalogService.IsImportedIconPackKey(GetSelectedIconPack());
            EliteOriginal1Item.Visibility = showOriginalOptions ? Visibility.Visible : Visibility.Collapsed;
            EliteOriginal2Item.Visibility = showOriginalOptions ? Visibility.Visible : Visibility.Collapsed;
            EliteOriginal3Item.Visibility = showOriginalOptions ? Visibility.Visible : Visibility.Collapsed;

            int currentElite = GetSelectedEliteEffectLevel();
            if (!showOriginalOptions && currentElite >= 11 && currentElite <= 13)
            {
                SelectEliteEffectLevel(currentElite - 10);
            }

            EliteEffectSelector.IsEnabled = supportsEliteOverlay;
            EliteEffectSelector.Opacity = supportsEliteOverlay ? 1.0 : 0.55;
        }

        private void UpdateWeaponBadgeSelectorState()
        {
            if (WeaponBadgeSelector == null) return;
            bool supportsWeaponBadge = SupportsWeaponBadgeForSelectedIconPack();
            WeaponBadgeSelector.IsEnabled = supportsWeaponBadge;
            WeaponBadgeSelector.Opacity = supportsWeaponBadge ? 1.0 : 0.55;
        }

        private void UpdateKillFxSelectorState()
        {
            // Kill FX selector is always enabled 鈥?all packs can opt in or out
            if (KillFxSelector == null) return;
            bool supportsKillFx = SupportsKillFxForSelectedIconPack();
            KillFxSelector.IsEnabled = supportsKillFx;
            KillFxSelector.Opacity = supportsKillFx ? 1.0 : 0.55;
        }
    }
}

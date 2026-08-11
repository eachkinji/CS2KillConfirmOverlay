using System;
using KillConfirmGameBar.Services;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace KillConfirmGameBar.Controls.GameStyles
{
    public sealed partial class CrossfireStylePanel : UserControl
    {
        private const string KillFxSettingKey = "KillFxEnabled";
        private const string EliteEffectSettingKey = "KillEliteEffect";
        private const string WeaponBadgeSettingKey = "KillWeaponBadge";
        private const string MainAnimationStyleSettingKey = "MainAnimationStyle";
        private bool _standaloneSettingsEnabled;
        private bool _suppressStandaloneEvents;

        public CrossfireStylePanel()
        {
            InitializeComponent();
        }

        public PathIcon KillFxIconControl => KillFxIcon;
        public PathIcon EliteOverlayIconControl => EliteOverlayIcon;
        public PathIcon WeaponBadgeIconControl => WeaponBadgeIcon;
        public PathIcon MainAnimationIconControl => MainAnimationIcon;
        public ComboBox KillFxSelectorControl => KillFxSelector;
        public ComboBox EliteEffectSelectorControl => EliteEffectSelector;
        public ComboBox WeaponBadgeSelectorControl => WeaponBadgeSelector;
        public ComboBox MainAnimationStyleSelectorControl => MainAnimationStyleSelector;
        public ComboBoxItem KillFxPackItemControl => KillFxPackItem;
        public ComboBoxItem KillFxOffItemControl => KillFxOffItem;
        public ComboBoxItem KillFxOriginalItemControl => KillFxOriginalItem;
        public ComboBoxItem EliteLevelOffItemControl => EliteLevelOffItem;
        public ComboBoxItem EliteLevel1ItemControl => EliteLevel1Item;
        public ComboBoxItem EliteLevel2ItemControl => EliteLevel2Item;
        public ComboBoxItem EliteLevel3ItemControl => EliteLevel3Item;
        public ComboBoxItem EliteOriginal1ItemControl => EliteOriginal1Item;
        public ComboBoxItem EliteOriginal2ItemControl => EliteOriginal2Item;
        public ComboBoxItem EliteOriginal3ItemControl => EliteOriginal3Item;
        public ComboBoxItem WeaponBadgeOnItemControl => WeaponBadgeOnItem;
        public ComboBoxItem WeaponBadgeOffItemControl => WeaponBadgeOffItem;
        public ComboBoxItem WeaponBadgeOriginalItemControl => WeaponBadgeOriginalItem;
        public ComboBoxItem AnimationStyle1ItemControl => AnimationStyle1Item;
        public ComboBoxItem AnimationStyle2ItemControl => AnimationStyle2Item;

        internal void EnableStandaloneSettings()
        {
            if (_standaloneSettingsEnabled)
            {
                return;
            }

            _standaloneSettingsEnabled = true;
            KillFxSelector.SelectionChanged += OnStandaloneKillFxChanged;
            EliteEffectSelector.SelectionChanged += OnStandaloneEliteEffectChanged;
            WeaponBadgeSelector.SelectionChanged += OnStandaloneWeaponBadgeChanged;
            MainAnimationStyleSelector.SelectionChanged += OnStandaloneAnimationStyleChanged;
            RefreshStandaloneSettings();
        }

        internal void RefreshStandaloneSettings()
        {
            if (!_standaloneSettingsEnabled)
            {
                return;
            }

            _suppressStandaloneEvents = true;
            try
            {
                SelectTaggedItem(KillFxSelector, ReadIntSetting(KillFxSettingKey, 1, 0, 2));
                SelectTaggedItem(EliteEffectSelector, NormalizeEliteMode(ReadIntSetting(EliteEffectSettingKey, 0, 0, 13)));
                SelectTaggedItem(WeaponBadgeSelector, ReadIntSetting(WeaponBadgeSettingKey, 0, 0, 2));
                SelectTaggedItem(MainAnimationStyleSelector, ReadIntSetting(MainAnimationStyleSettingKey, 1, 1, 2));
            }
            finally
            {
                _suppressStandaloneEvents = false;
            }
        }

        internal void ApplyLanguage(bool isChinese)
        {
            KillFxLabelText.Text = Services.LocalizationManager.Text("KillFxLabel");
            EliteOverlayLabelText.Text = Services.LocalizationManager.Text("EliteOverlayLabel");
            WeaponBadgeLabelText.Text = Services.LocalizationManager.Text("WeaponBadgeLabel");
            MainAnimationLabelText.Text = Services.LocalizationManager.Text("MainAnimationLabel");

            KillFxPackItem.Content = isChinese ? "自动" : "AUTO";
            KillFxOffItem.Content = isChinese ? "关闭" : "OFF";
            KillFxOriginalItem.Content = isChinese ? "原版" : "ORIG";
            EliteLevelOffItem.Content = isChinese ? "关闭" : "OFF";
            WeaponBadgeOnItem.Content = isChinese ? "自动" : "AUTO";
            WeaponBadgeOffItem.Content = isChinese ? "关闭" : "OFF";
            WeaponBadgeOriginalItem.Content = isChinese ? "原版" : "ORIG";
            AnimationStyle1Item.Content = isChinese ? "样式 1" : "Style 1";
            AnimationStyle2Item.Content = isChinese ? "样式 2" : "Style 2";
        }

        internal void ApplyTheme(GameThemePalette theme)
        {
            AdvancedEffectsPanelSupport.ApplySoftenedTree(this, theme);

            // These icons sit inside the accent-colored circle. Keep their
            // foreground consistent with the other compact setting cards.
            var iconBrush = new SolidColorBrush(Colors.White);
            KillFxIcon.Foreground = iconBrush;
            EliteOverlayIcon.Foreground = iconBrush;
            WeaponBadgeIcon.Foreground = iconBrush;
            MainAnimationIcon.Foreground = iconBrush;
        }

        private void OnStandaloneKillFxChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressStandaloneEvents) return;
            int value = ReadTaggedItem(KillFxSelector, 1);
            ApplicationData.Current.LocalSettings.Values[KillFxSettingKey] = value;
            KillConfirmAnimation.ConfigureKillFxMode(value);
        }

        private void OnStandaloneEliteEffectChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressStandaloneEvents) return;
            int value = NormalizeEliteMode(ReadTaggedItem(EliteEffectSelector, 0));
            ApplicationData.Current.LocalSettings.Values[EliteEffectSettingKey] = value;
            KillConfirmAnimation.ConfigureEliteEffectLevel(value);
        }

        private void OnStandaloneWeaponBadgeChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressStandaloneEvents) return;
            int value = ReadTaggedItem(WeaponBadgeSelector, 0);
            ApplicationData.Current.LocalSettings.Values[WeaponBadgeSettingKey] = value;
            KillConfirmAnimation.ConfigureWeaponBadgeMode(value);
        }

        private void OnStandaloneAnimationStyleChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressStandaloneEvents) return;
            int value = ReadTaggedItem(MainAnimationStyleSelector, 1);
            ApplicationData.Current.LocalSettings.Values[MainAnimationStyleSettingKey] = value;
            KillConfirmAnimation.ConfigureMainAnimationStyle(value);
        }

        private static int ReadIntSetting(string key, int fallback, int minimum, int maximum)
        {
            object stored = ApplicationData.Current.LocalSettings.Values[key];
            int value = fallback;
            if (stored is int number)
            {
                value = number;
            }
            else if (stored is bool boolean)
            {
                value = boolean ? 1 : 0;
            }
            else if (stored is string text && int.TryParse(text, out int parsed))
            {
                value = parsed;
            }

            return Math.Max(minimum, Math.Min(maximum, value));
        }

        private static int NormalizeEliteMode(int value)
        {
            return value == 0 || (value >= 1 && value <= 3) || (value >= 11 && value <= 13)
                ? value
                : 0;
        }

        private static int ReadTaggedItem(ComboBox selector, int fallback)
        {
            if (selector.SelectedItem is ComboBoxItem item
                && int.TryParse(item.Tag as string, out int value))
            {
                return value;
            }

            return fallback;
        }

        private static void SelectTaggedItem(ComboBox selector, int value)
        {
            string target = value.ToString();
            foreach (object entry in selector.Items)
            {
                if (entry is ComboBoxItem item
                    && string.Equals(item.Tag as string, target, StringComparison.Ordinal))
                {
                    selector.SelectedItem = item;
                    return;
                }
            }

            selector.SelectedIndex = 0;
        }
    }
}

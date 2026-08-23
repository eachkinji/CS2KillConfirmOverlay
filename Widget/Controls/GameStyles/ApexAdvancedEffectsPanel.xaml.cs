using KillConfirmGameBar.Services;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace KillConfirmGameBar.Controls.GameStyles
{
    public sealed partial class ApexAdvancedEffectsPanel : UserControl
    {
        private bool _suppressVisualEffectChanges;

        public ApexAdvancedEffectsPanel()
        {
            InitializeComponent();
            RefreshVisualEffectSettings();
        }

        public event SelectionChangedEventHandler MoneyRewardModeSelectionChanged;
        public event SelectionChangedEventHandler StreakModeSelectionChanged;

        public ComboBox MoneyRewardModeSelectorControl => MoneyRewardModeSelector;
        public ComboBoxItem MoneyRewardDeltaItemControl => MoneyRewardDeltaItem;
        public ComboBoxItem MoneyRewardRulesItemControl => MoneyRewardRulesItem;
        public TextBlock MoneyRewardModeLabelControl => MoneyRewardModeLabel;
        public ComboBox StreakModeSelectorControl => StreakEditor.SelectorControl;

        internal void ApplyTheme(GameThemePalette theme)
        {
            TitleText.Foreground = theme.Brush(theme.Text);
            AdvancedEffectsPanelSupport.ApplyResetButton(ResetButton, theme);
            AdvancedEffectsPanelSupport.ApplyMoneyRow(MoneyRewardModeLabel, MoneyRewardModeSelector, theme);
            StreakEditor.ApplyTheme(theme);
            VisualEffectsCard.Background = new Windows.UI.Xaml.Media.SolidColorBrush(theme.Card);
            VisualEffectsCard.BorderBrush = new Windows.UI.Xaml.Media.SolidColorBrush(theme.SoftBorder);
            VisualEffectsTitle.Foreground = new Windows.UI.Xaml.Media.SolidColorBrush(theme.Text);
            CrosshairEffectLabel.Foreground = new Windows.UI.Xaml.Media.SolidColorBrush(theme.Text);
            LowerEffectLabel.Foreground = new Windows.UI.Xaml.Media.SolidColorBrush(theme.Text);
            CrosshairEffectToggle.Foreground = new Windows.UI.Xaml.Media.SolidColorBrush(theme.Text);
            LowerEffectToggle.Foreground = new Windows.UI.Xaml.Media.SolidColorBrush(theme.Text);
        }

        public void ApplyLanguage(bool isChinese)
        {
            TitleText.Text = isChinese ? "Apex 击杀提示设置" : "Apex Kill Feedback";
            ResetButtonText.Text = isChinese ? "恢复默认" : "Reset";
            ToolTipService.SetToolTip(ResetButton, isChinese ? "恢复 Apex 默认设置" : "Restore Apex defaults");
            MoneyRewardModeLabel.Text = isChinese ? "金钱计算方式" : "Money calculation";
            MoneyRewardDeltaItem.Content = isChinese ? "按实际金钱变化（推荐）" : "Actual money change (recommended)";
            MoneyRewardRulesItem.Content = isChinese ? "按武器击杀奖励" : "Weapon kill reward";
            VisualEffectsTitle.Text = isChinese ? "显示哪些击杀提示" : "Visible kill feedback";
            CrosshairEffectLabel.Text = isChinese ? "中央准心提示" : "Center crosshair feedback";
            LowerEffectLabel.Text = isChinese ? "下方击杀瀑布流" : "Lower kill feed";
            ApplyToggleLanguage(CrosshairEffectToggle, isChinese);
            ApplyToggleLanguage(LowerEffectToggle, isChinese);
            StreakEditor.ApplyLanguage(isChinese);
        }

        public string GetSelectedMoneyRewardMode(string fallback)
        {
            return ReadTaggedItem(MoneyRewardModeSelector, fallback);
        }

        public void SelectMoneyRewardMode(string value, string fallback)
        {
            SelectTaggedItem(MoneyRewardModeSelector, value, fallback);
        }

        public string GetSelectedStreakMode(string fallback)
        {
            return StreakEditor.GetValue(fallback);
        }

        public void SelectStreakMode(string value)
        {
            StreakEditor.SelectValue(value);
        }

        public void RefreshVisualEffectSettings()
        {
            KillFeedbackVisibilitySettingsValues settings =
                KillFeedbackVisibilitySettingsStore.Load(GameStyleMode.Apex);
            _suppressVisualEffectChanges = true;
            try
            {
                CrosshairEffectToggle.IsOn = settings.CrosshairEnabled;
                LowerEffectToggle.IsOn = settings.LowerEnabled;
            }
            finally
            {
                _suppressVisualEffectChanges = false;
            }
        }

        private void OnMoneyRewardModeSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            MoneyRewardModeSelectionChanged?.Invoke(this, e);
        }

        private void OnStreakModeSelectionChanged(object sender, RoutedEventArgs e)
        {
            StreakModeSelectionChanged?.Invoke(this, null);
        }

        private void OnVisualEffectToggled(object sender, RoutedEventArgs e)
        {
            if (_suppressVisualEffectChanges)
            {
                return;
            }

            KillFeedbackVisibilitySettingsStore.Save(
                GameStyleMode.Apex,
                new KillFeedbackVisibilitySettingsValues
                {
                    CrosshairEnabled = CrosshairEffectToggle.IsOn,
                    LowerEnabled = LowerEffectToggle.IsOn
                });
        }

        private void OnResetButtonClick(object sender, RoutedEventArgs e)
        {
            SelectTaggedItem(MoneyRewardModeSelector, "delta", "delta");
            StreakEditor.SelectValue(SharedStreakSettingsStore.LifeMode);
            _suppressVisualEffectChanges = true;
            CrosshairEffectToggle.IsOn = true;
            LowerEffectToggle.IsOn = true;
            _suppressVisualEffectChanges = false;
            OnVisualEffectToggled(this, null);
            MoneyRewardModeSelectionChanged?.Invoke(MoneyRewardModeSelector, null);
            StreakModeSelectionChanged?.Invoke(StreakEditor.SelectorControl, null);
        }

        private static string ReadTaggedItem(ComboBox selector, string fallback)
        {
            if (selector?.SelectedItem is ComboBoxItem item
                && item.Tag is string tag
                && !string.IsNullOrWhiteSpace(tag))
            {
                return tag;
            }
            return fallback;
        }

        private static void SelectTaggedItem(ComboBox selector, string value, string fallback)
        {
            if (selector == null) return;
            string target = string.IsNullOrWhiteSpace(value) ? fallback : value;
            foreach (object option in selector.Items)
            {
                if (option is ComboBoxItem item
                    && item.Tag is string tag
                    && string.Equals(tag, target, System.StringComparison.OrdinalIgnoreCase))
                {
                    selector.SelectedItem = item;
                    return;
                }
            }
            selector.SelectedIndex = 0;
        }

        private static void ApplyToggleLanguage(ToggleSwitch toggle, bool isChinese)
        {
            toggle.OnContent = isChinese ? "开" : "On";
            toggle.OffContent = isChinese ? "关" : "Off";
        }
    }
}

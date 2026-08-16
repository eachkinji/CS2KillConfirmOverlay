using System;
using KillConfirmGameBar.Services;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace KillConfirmGameBar.Controls.GameStyles
{
    public sealed partial class Battlefield2042AdvancedEffectsPanel : UserControl
    {
        public Battlefield2042AdvancedEffectsPanel()
        {
            InitializeComponent();
            EventSoundPanel.Configure(GameStyleMode.Battlefield2042);
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
            AdvancedEffectsPanelSupport.ApplyHeader(TitleText, HintText, theme);
            AdvancedEffectsPanelSupport.ApplyResetButton(ResetButton, theme);
            AdvancedEffectsPanelSupport.ApplyMoneyRow(MoneyRewardModeLabel, MoneyRewardModeSelector, theme);
            StreakEditor.ApplyTheme(theme);
            EventSoundPanel.ApplyTheme(theme);
            AdvancedEffectsPanelSupport.ApplyNotice(ImportLockedNotice, ImportLockedText, theme);
            StylePanel.ApplyTheme(theme);
        }

        public void ApplyLanguage(bool isChinese)
        {
            TitleText.Text = isChinese ? "2042 高级特效" : "2042 Effects";
            ResetButtonText.Text = isChinese ? "恢复默认" : "Reset";
            ToolTipService.SetToolTip(ResetButton, isChinese ? "恢复 2042 默认设置" : "Restore 2042 defaults");
            HintText.Text = isChinese
                ? "Battlefield 2042 击杀展示、钱瀑布和 2042 声音包在这里单独设置。"
                : "Battlefield 2042 kill display, money waterfall, and 2042 sound pack are isolated here.";
            ImportLockedText.Text = isChinese
                ? "Battlefield 2042 视觉资源保持内置，事件声音可在上方单独自定义。"
                : "Battlefield 2042 visuals stay built in; event sounds can be customized above.";
            MoneyRewardModeLabel.Text = isChinese ? "奖励算法" : "Money";
            MoneyRewardDeltaItem.Content = isChinese ? "GSI 差值（默认）" : "GSI delta (default)";
            MoneyRewardRulesItem.Content = isChinese ? "击杀奖励规则" : "Kill reward rules";
            StreakEditor.ApplyLanguage(isChinese);
            EventSoundPanel.ApplyLanguage(isChinese);
            StylePanel.ApplyLanguage(isChinese);
        }

        public string GetSelectedMoneyRewardMode(string fallback)
        {
            return ReadTaggedComboBoxItem(MoneyRewardModeSelector, fallback);
        }

        public void SelectMoneyRewardMode(string value, string fallback)
        {
            SelectTaggedComboBoxItem(MoneyRewardModeSelector, value, fallback);
        }

        public string GetSelectedStreakMode(string fallback)
        {
            return StreakEditor.GetValue(fallback);
        }

        public void SelectStreakMode(string value)
        {
            StreakEditor.SelectValue(value);
        }

        public void ReloadEventSoundSettings()
        {
            EventSoundPanel.Reload();
        }

        private void OnMoneyRewardModeSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            MoneyRewardModeSelectionChanged?.Invoke(this, e);
        }

        private void OnStreakModeSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            StreakModeSelectionChanged?.Invoke(this, e);
        }

        private async void OnResetButtonClick(object sender, RoutedEventArgs e)
        {
            SelectTaggedComboBoxItem(MoneyRewardModeSelector, "delta", "delta");
            StreakEditor.SelectValue(SharedStreakSettingsStore.LifeMode);
            MoneyRewardModeSelectionChanged?.Invoke(MoneyRewardModeSelector, null);
            StreakModeSelectionChanged?.Invoke(StreakEditor.SelectorControl, null);
            await EventSoundPanel.ResetToDefaultsAsync();
        }

        private static string ReadTaggedComboBoxItem(ComboBox selector, string fallback)
        {
            if (selector?.SelectedItem is ComboBoxItem item
                && item.Tag is string tag
                && !string.IsNullOrWhiteSpace(tag))
            {
                return tag;
            }

            return fallback;
        }

        private static void SelectTaggedComboBoxItem(ComboBox selector, string value, string fallback)
        {
            if (selector == null)
            {
                return;
            }

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
    }
}

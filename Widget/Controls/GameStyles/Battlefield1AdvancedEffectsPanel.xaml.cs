using System;
using KillConfirmGameBar.Services;
using Windows.UI.Xaml.Controls;

namespace KillConfirmGameBar.Controls.GameStyles
{
    public sealed partial class Battlefield1AdvancedEffectsPanel : UserControl
    {
        public Battlefield1AdvancedEffectsPanel()
        {
            InitializeComponent();
        }

        public event SelectionChangedEventHandler MoneyRewardModeSelectionChanged;
        public event SelectionChangedEventHandler StreakModeSelectionChanged;

        public ComboBox MoneyRewardModeSelectorControl => MoneyRewardModeSelector;
        public ComboBoxItem MoneyRewardDeltaItemControl => MoneyRewardDeltaItem;
        public ComboBoxItem MoneyRewardRulesItemControl => MoneyRewardRulesItem;
        public TextBlock MoneyRewardModeLabelControl => MoneyRewardModeLabel;
        public ComboBox StreakModeSelectorControl => StreakModeSelector;

        internal void ApplyTheme(GameThemePalette theme)
        {
            AdvancedEffectsPanelSupport.ApplyHeader(TitleText, HintText, theme);
            AdvancedEffectsPanelSupport.ApplyMoneyRow(MoneyRewardModeLabel, MoneyRewardModeSelector, theme);
            AdvancedEffectsPanelSupport.ApplyMoneyRow(StreakModeLabel, StreakModeSelector, theme);
            AdvancedEffectsPanelSupport.ApplyNotice(ImportLockedNotice, ImportLockedText, theme);
            StylePanel.ApplyTheme(theme);
        }

        public void ApplyLanguage(bool isChinese)
        {
            TitleText.Text = isChinese ? "BF1 \u9ad8\u7ea7\u7279\u6548" : "BF1 Effects";
            HintText.Text = isChinese
                ? "BF1 \u7684\u6587\u5b57\u3001\u5361\u7247\u3001\u5956\u91d1\u7b97\u6cd5\u548c\u6218\u5730\u4e00\u8d44\u6e90\u90fd\u5728\u8fd9\u91cc\u5355\u72ec\u8bbe\u7f6e\u3002"
                : "BF1 text, card, money reward calculation, and Battlefield 1 assets are isolated here.";
            MoneyRewardModeLabel.Text = isChinese ? "\u5956\u52b1\u7b97\u6cd5" : "Money";
            MoneyRewardDeltaItem.Content = isChinese ? "GSI \u5dee\u503c\u6821\u9a8c\uff08\u5b9e\u9a8c\uff09" : "GSI delta validation (experimental)";
            MoneyRewardRulesItem.Content = isChinese ? "\u51fb\u6740\u5956\u52b1\u89c4\u5219\uff08\u63a8\u8350\uff09" : "Kill reward rules (recommended)";
            SharedStreakSettingsStore.ApplyLanguage(
                StreakModeLabel,
                StreakLifeItem,
                StreakTimed5Item,
                StreakTimed10Item,
                StreakTimed15Item,
                isChinese);
            ImportLockedText.Text = isChinese
                ? "\u4ec5\u4f7f\u7528\u5185\u7f6e Battlefield 1 \u8d44\u6e90\u3002\u6b64\u9875\u4e0d\u5141\u8bb8\u5bfc\u5165\u6587\u4ef6\u3002"
                : "Built-in Battlefield 1 resources only. File import is disabled for this page.";
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
            return SharedStreakSettingsStore.Read(StreakModeSelector, fallback);
        }

        public void SelectStreakMode(string value)
        {
            SharedStreakSettingsStore.Select(StreakModeSelector, value);
        }

        private void OnMoneyRewardModeSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            MoneyRewardModeSelectionChanged?.Invoke(this, e);
        }

        private void OnStreakModeSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            StreakModeSelectionChanged?.Invoke(this, e);
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
                    && string.Equals(tag, target, StringComparison.OrdinalIgnoreCase))
                {
                    selector.SelectedItem = item;
                    return;
                }
            }

            selector.SelectedIndex = 0;
        }
    }
}

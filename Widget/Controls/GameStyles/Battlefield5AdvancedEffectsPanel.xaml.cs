using System;
using KillConfirmGameBar.Services;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Controls;

namespace KillConfirmGameBar.Controls.GameStyles
{
    public sealed partial class Battlefield5AdvancedEffectsPanel : UserControl
    {
        public Battlefield5AdvancedEffectsPanel()
        {
            InitializeComponent();
            EventSoundPanel.Configure(GameStyleMode.Battlefield5);
        }

        public event SelectionChangedEventHandler MoneyRewardModeSelectionChanged;
        public event SelectionChangedEventHandler StreakModeSelectionChanged;

        public ComboBox MoneyRewardModeSelectorControl => MoneyRewardModeSelector;
        public ComboBox StreakModeSelectorControl => StreakEditor.SelectorControl;
        public ComboBoxItem MoneyRewardDeltaItemControl => MoneyRewardDeltaItem;
        public ComboBoxItem MoneyRewardRulesItemControl => MoneyRewardRulesItem;
        public TextBlock MoneyRewardModeLabelControl => MoneyRewardModeLabel;

        internal void ApplyTheme(GameThemePalette theme)
        {
            AdvancedEffectsPanelSupport.ApplyHeader(TitleText, HintText, theme);
            AdvancedEffectsPanelSupport.ApplyMoneyRow(MoneyRewardModeLabel, MoneyRewardModeSelector, theme);
            StreakEditor.ApplyTheme(theme);
            EventSoundPanel.ApplyTheme(theme);
            AdvancedEffectsPanelSupport.ApplyNotice(ImportLockedNotice, ImportLockedText, theme);
            StylePanel.ApplyTheme(theme);
        }

        public void ApplyLanguage(bool isChinese)
        {
            TitleText.Text = isChinese ? "BF5 \u9ad8\u7ea7\u7279\u6548" : "BF5 Effects";
            HintText.Text = isChinese
                ? "BF5 \u7684\u56fe\u6807\u961f\u5217\u3001\u6587\u5b57\u7011\u5e03\u3001\u5956\u91d1\u7b97\u6cd5\u548c\u6218\u5730\u4e94\u8d44\u6e90\u90fd\u5728\u8fd9\u91cc\u5355\u72ec\u8bbe\u7f6e\u3002"
                : "BF5 queue, text waterfall, money reward calculation, and Battlefield 5 assets are isolated here.";
            MoneyRewardModeLabel.Text = isChinese ? "\u5956\u52b1\u7b97\u6cd5" : "Money";
            MoneyRewardDeltaItem.Content = isChinese ? "GSI \u5dee\u503c\uff08\u9ed8\u8ba4\uff09" : "GSI delta (default)";
            MoneyRewardRulesItem.Content = isChinese ? "\u51fb\u6740\u5956\u52b1\u89c4\u5219" : "Kill reward rules";
            StreakEditor.ApplyLanguage(isChinese);
            EventSoundPanel.ApplyLanguage(isChinese);
            ImportLockedText.Text = isChinese
                ? "Battlefield 5 \u89c6\u89c9\u8d44\u6e90\u4fdd\u6301\u5185\u7f6e\uff0c\u4e8b\u4ef6\u58f0\u97f3\u53ef\u5728\u4e0a\u65b9\u5355\u72ec\u81ea\u5b9a\u4e49\u3002"
                : "Battlefield 5 visuals stay built in; event sounds can be customized above.";
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

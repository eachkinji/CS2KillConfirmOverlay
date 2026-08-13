using KillConfirmGameBar.Services;
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
            AdvancedEffectsPanelSupport.ApplyMoneyRow(MoneyRewardModeLabel, MoneyRewardModeSelector, theme);
            StreakEditor.ApplyTheme(theme);
            EventSoundPanel.ApplyTheme(theme);
            AdvancedEffectsPanelSupport.ApplyNotice(ImportLockedNotice, ImportLockedText, theme);
            StylePanel.ApplyTheme(theme);
        }

        public void ApplyLanguage(bool isChinese)
        {
            TitleText.Text = isChinese ? "2042 \u9ad8\u7ea7\u7279\u6548" : "2042 Effects";
            HintText.Text = isChinese
                ? "Battlefield 2042 \u51fb\u6740\u5c55\u793a\u3001\u94b1\u7011\u5e03\u548c 2042 \u58f0\u97f3\u5305\u5728\u8fd9\u91cc\u5355\u72ec\u8bbe\u7f6e\u3002"
                : "Battlefield 2042 kill display, money waterfall, and 2042 sound pack are isolated here.";
            ImportLockedText.Text = isChinese
                ? "Battlefield 2042 \u89c6\u89c9\u8d44\u6e90\u4fdd\u6301\u5185\u7f6e\uff0c\u4e8b\u4ef6\u58f0\u97f3\u53ef\u5728\u4e0a\u65b9\u5355\u72ec\u81ea\u5b9a\u4e49\u3002"
                : "Battlefield 2042 visuals stay built in; event sounds can be customized above.";
            MoneyRewardModeLabel.Text = isChinese ? "\u5956\u52b1\u7b97\u6cd5" : "Money";
            MoneyRewardDeltaItem.Content = isChinese ? "GSI \u5dee\u503c\uff08\u9ed8\u8ba4\uff09" : "GSI delta (default)";
            MoneyRewardRulesItem.Content = isChinese ? "\u51fb\u6740\u5956\u52b1\u89c4\u5219" : "Kill reward rules";
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

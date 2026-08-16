using System;
using KillConfirmGameBar.Services;
using Windows.UI.Xaml;
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
            AdvancedEffectsPanelSupport.ApplyResetButton(ResetButton, theme);
            AdvancedEffectsPanelSupport.ApplyMoneyRow(MoneyRewardModeLabel, MoneyRewardModeSelector, theme);
            StreakEditor.ApplyTheme(theme);
            EventSoundPanel.ApplyTheme(theme);
            AdvancedEffectsPanelSupport.ApplyNotice(ImportLockedNotice, ImportLockedText, theme);
            StylePanel.ApplyTheme(theme);
        }

        public void ApplyLanguage(bool isChinese)
        {
            TitleText.Text = isChinese ? "BF5 高级特效" : "BF5 Effects";
            ResetButtonText.Text = isChinese ? "恢复默认" : "Reset";
            ToolTipService.SetToolTip(ResetButton, isChinese ? "恢复 BF5 默认设置" : "Restore BF5 defaults");
            HintText.Text = isChinese
                ? "BF5 的图标队列、文字瀑布、奖金算法和战地五资源都在这里单独设置。"
                : "BF5 queue, text waterfall, money reward calculation, and Battlefield 5 assets are isolated here.";
            MoneyRewardModeLabel.Text = isChinese ? "奖励算法" : "Money";
            MoneyRewardDeltaItem.Content = isChinese ? "GSI 差值（默认）" : "GSI delta (default)";
            MoneyRewardRulesItem.Content = isChinese ? "击杀奖励规则" : "Kill reward rules";
            StreakEditor.ApplyLanguage(isChinese);
            EventSoundPanel.ApplyLanguage(isChinese);
            ImportLockedText.Text = isChinese
                ? "Battlefield 5 视觉资源保持内置，事件声音可在上方单独自定义。"
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

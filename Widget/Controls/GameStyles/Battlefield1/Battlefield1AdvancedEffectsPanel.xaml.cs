using System;
using KillConfirmGameBar.Services;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace KillConfirmGameBar.Controls.GameStyles
{
    public sealed partial class Battlefield1AdvancedEffectsPanel : UserControl
    {
        private bool _suppressKillMarkChanges;

        public Battlefield1AdvancedEffectsPanel()
        {
            InitializeComponent();
            RefreshKillMarkSetting();
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
            AdvancedEffectsPanelSupport.ApplyNotice(ImportLockedNotice, ImportLockedText, theme);
            AdvancedEffectsPanelSupport.ApplyKillMarkCard(VisualEffectsCard, VisualEffectsTitle, KillMarkEffectLabel, KillMarkEffectToggle, theme);
            StylePanel.ApplyTheme(theme);
        }

        public void ApplyLanguage(bool isChinese)
        {
            TitleText.Text = isChinese ? "BF1 高级特效" : "BF1 Effects";
            ResetButtonText.Text = isChinese ? "恢复默认" : "Reset";
            ToolTipService.SetToolTip(ResetButton, isChinese ? "恢复 BF1 默认设置" : "Restore BF1 defaults");
            HintText.Text = isChinese
                ? "BF1 的文字、卡片、奖金算法和战地一资源都在这里单独设置。"
                : "BF1 text, card, money reward calculation, and Battlefield 1 assets are isolated here.";
            MoneyRewardModeLabel.Text = isChinese ? "奖励算法" : "Money";
            MoneyRewardDeltaItem.Content = isChinese ? "按实际金钱变化（推荐）" : "Actual money change (recommended)";
            MoneyRewardRulesItem.Content = isChinese ? "击杀奖励规则" : "Kill reward rules";
            StreakEditor.ApplyLanguage(isChinese);
            AdvancedEffectsPanelSupport.ApplyKillMarkLanguage(VisualEffectsTitle, KillMarkEffectLabel, KillMarkEffectToggle, isChinese);
            ImportLockedText.Text = isChinese
                ? "Battlefield 1 视觉资源保持内置，事件声音可在语音标签页中自定义。"
                : "Battlefield 1 visuals stay built in; event sounds can be customized in the Voice tab.";
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

        private void OnMoneyRewardModeSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            MoneyRewardModeSelectionChanged?.Invoke(this, e);
        }

        private void OnStreakModeSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            StreakModeSelectionChanged?.Invoke(this, e);
        }

        private void OnResetButtonClick(object sender, RoutedEventArgs e)
        {
            SelectTaggedComboBoxItem(MoneyRewardModeSelector, "delta", "delta");
            StreakEditor.SelectValue(SharedStreakSettingsStore.LifeMode);
            SetKillMarkEnabled(true);
            MoneyRewardModeSelectionChanged?.Invoke(MoneyRewardModeSelector, null);
            StreakModeSelectionChanged?.Invoke(StreakEditor.SelectorControl, null);
        }

        private void RefreshKillMarkSetting()
        {
            SetKillMarkEnabled(KillFeedbackVisibilitySettingsStore.Load(GameStyleMode.Battlefield1).CrosshairEnabled, false);
        }

        private void SetKillMarkEnabled(bool enabled, bool save = true)
        {
            _suppressKillMarkChanges = true;
            KillMarkEffectToggle.IsOn = enabled;
            _suppressKillMarkChanges = false;
            if (save)
            {
                SaveKillMarkSetting();
            }
        }

        private void OnKillMarkEffectToggled(object sender, RoutedEventArgs e)
        {
            if (!_suppressKillMarkChanges)
            {
                SaveKillMarkSetting();
            }
        }

        private void SaveKillMarkSetting()
        {
            KillFeedbackVisibilitySettingsStore.Save(GameStyleMode.Battlefield1, new KillFeedbackVisibilitySettingsValues { CrosshairEnabled = KillMarkEffectToggle.IsOn });
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

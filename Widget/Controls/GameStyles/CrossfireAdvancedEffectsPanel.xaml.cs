using System;
using KillConfirmGameBar.Services;
using Windows.UI.Xaml.Controls;

namespace KillConfirmGameBar.Controls.GameStyles
{
    public sealed partial class CrossfireAdvancedEffectsPanel : UserControl
    {
        public CrossfireAdvancedEffectsPanel()
        {
            InitializeComponent();
        }

        public event SelectionChangedEventHandler StreakModeSelectionChanged;
        public event SelectionChangedEventHandler FirstKillAudioSelectionChanged;
        public event SelectionChangedEventHandler LastKillAudioSelectionChanged;

        public ComboBox StreakModeSelectorControl => StreakModeSelector;
        public ComboBox FirstKillAudioSelectorControl => FirstKillAudioSelector;
        public ComboBox LastKillAudioSelectorControl => LastKillAudioSelector;

        public void SetStylePanel(CrossfireStylePanel panel)
        {
            StylePanelHost.Content = panel;
        }

        internal void ApplyTheme(GameThemePalette theme)
        {
            AdvancedEffectsPanelSupport.ApplyHeader(TitleText, HintText, theme);
            AdvancedEffectsPanelSupport.ApplyMoneyRow(StreakModeLabel, StreakModeSelector, theme);
            AdvancedEffectsPanelSupport.ApplyMoneyRow(FirstKillAudioLabel, FirstKillAudioSelector, theme);
            AdvancedEffectsPanelSupport.ApplyMoneyRow(LastKillAudioLabel, LastKillAudioSelector, theme);
        }

        public void ApplyLanguage(bool isChinese)
        {
            TitleText.Text = isChinese ? "\u7a7f\u8d8a\u706b\u7ebf\u9ad8\u7ea7\u7279\u6548" : "CrossFire Effects";
            HintText.Text = isChinese
                ? "CF \u7684\u8fde\u6740\u8ba1\u6570\u3001\u9996\u5c3e\u6740\u8bed\u97f3\u548c\u51fb\u6740\u7279\u6548\u90fd\u5728\u8fd9\u91cc\u5355\u72ec\u8bbe\u7f6e\u3002"
                : "CrossFire kill streaks, first/last-kill audio, and kill effects are configured here.";
            StreakModeLabel.Text = isChinese ? "\u8fde\u6740\u8ba1\u7b97" : "Kill streak";
            StreakLifeItem.Content = isChinese ? "\u6b7b\u4ea1\u524d\u6301\u7eed\u7d2f\u8ba1" : "Until death";
            StreakTimed5Item.Content = isChinese ? "5 \u79d2\u8fde\u6740\u7a97\u53e3" : "5-second window";
            StreakTimed10Item.Content = isChinese ? "10 \u79d2\u8fde\u6740\u7a97\u53e3" : "10-second window";
            StreakTimed15Item.Content = isChinese ? "15 \u79d2\u8fde\u6740\u7a97\u53e3" : "15-second window";
            FirstKillAudioLabel.Text = isChinese ? "\u9996\u6740\u8bed\u97f3" : "First-kill audio";
            LastKillAudioLabel.Text = isChinese ? "\u5c3e\u6740\u8bed\u97f3" : "Last-kill audio";
            FirstKillSpecialItem.Content = isChinese ? "\u7279\u6b8a\u97f3\u6548" : "Special audio";
            LastKillSpecialItem.Content = FirstKillSpecialItem.Content;
            FirstKillOriginalItem.Content = isChinese ? "\u539f\u51fb\u6740\u97f3\u6548" : "Original kill audio";
            LastKillOriginalItem.Content = FirstKillOriginalItem.Content;
        }

        public string GetSelectedStreakMode(string fallback)
        {
            return ReadTaggedItem(StreakModeSelector, fallback);
        }

        public bool GetFirstKillSpecialAudio(bool fallback)
        {
            return ReadTaggedItem(FirstKillAudioSelector, fallback ? "special" : "original") == "special";
        }

        public bool GetLastKillSpecialAudio(bool fallback)
        {
            return ReadTaggedItem(LastKillAudioSelector, fallback ? "special" : "original") == "special";
        }

        public void SelectSettings(string streakMode, bool firstSpecial, bool lastSpecial)
        {
            SelectTaggedItem(StreakModeSelector, streakMode, "life");
            SelectTaggedItem(FirstKillAudioSelector, firstSpecial ? "special" : "original", "special");
            SelectTaggedItem(LastKillAudioSelector, lastSpecial ? "special" : "original", "special");
        }

        private void OnStreakModeSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            StreakModeSelectionChanged?.Invoke(this, e);
        }

        private void OnFirstKillAudioSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            FirstKillAudioSelectionChanged?.Invoke(this, e);
        }

        private void OnLastKillAudioSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            LastKillAudioSelectionChanged?.Invoke(this, e);
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

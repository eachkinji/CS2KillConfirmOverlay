using System;
using KillConfirmGameBar.Services;
using Windows.UI.Xaml;
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
        public event SelectionChangedEventHandler HeadshotAudioPrioritySelectionChanged;
        public event SelectionChangedEventHandler KnifeAudioPrioritySelectionChanged;
        public event SelectionChangedEventHandler HeadshotIconPrioritySelectionChanged;
        public event SelectionChangedEventHandler KnifeIconPrioritySelectionChanged;
        public event SelectionChangedEventHandler FirstKillAudioSelectionChanged;
        public event SelectionChangedEventHandler LastKillAudioSelectionChanged;
        public event RoutedEventHandler FirstKillEffectToggled;
        public event RoutedEventHandler LastKillEffectToggled;
        public event RoutedEventHandler AssistAudioToggled;

        public ComboBox StreakModeSelectorControl => StreakEditor.SelectorControl;
        public ComboBox HeadshotAudioPrioritySelectorControl => HeadshotAudioPrioritySelector;
        public ComboBox KnifeAudioPrioritySelectorControl => KnifeAudioPrioritySelector;
        public ComboBox HeadshotIconPrioritySelectorControl => HeadshotIconPrioritySelector;
        public ComboBox KnifeIconPrioritySelectorControl => KnifeIconPrioritySelector;
        public ComboBox FirstKillAudioSelectorControl => FirstKillAudioSelector;
        public ComboBox LastKillAudioSelectorControl => LastKillAudioSelector;

        public void SetStylePanel(CrossfireStylePanel panel)
        {
            StylePanelHost.Content = panel;
        }

        internal void ApplyTheme(GameThemePalette theme)
        {
            AdvancedEffectsPanelSupport.ApplyHeader(TitleText, HintText, theme);
            StreakEditor.ApplyTheme(theme);
            AdvancedEffectsPanelSupport.ApplyMoneyRow(HeadshotAudioPriorityLabel, HeadshotAudioPrioritySelector, theme);
            AdvancedEffectsPanelSupport.ApplyMoneyRow(KnifeAudioPriorityLabel, KnifeAudioPrioritySelector, theme);
            AdvancedEffectsPanelSupport.ApplyMoneyRow(HeadshotIconPriorityLabel, HeadshotIconPrioritySelector, theme);
            AdvancedEffectsPanelSupport.ApplyMoneyRow(KnifeIconPriorityLabel, KnifeIconPrioritySelector, theme);
            AdvancedEffectsPanelSupport.ApplyMoneyRow(FirstKillAudioLabel, FirstKillAudioSelector, theme);
            AdvancedEffectsPanelSupport.ApplyMoneyRow(LastKillAudioLabel, LastKillAudioSelector, theme);
            AdvancedEffectsPanelSupport.ApplyToggleRow(FirstKillEffectLabel, FirstKillEffectToggle, theme);
            AdvancedEffectsPanelSupport.ApplyToggleRow(LastKillEffectLabel, LastKillEffectToggle, theme);
            AdvancedEffectsPanelSupport.ApplyToggleRow(AssistAudioLabel, AssistAudioToggle, theme);
            if (StylePanelHost.Content is CrossfireStylePanel stylePanel)
            {
                stylePanel.ApplyTheme(theme);
            }
        }

        public void ApplyLanguage(bool isChinese)
        {
            TitleText.Text = isChinese ? "\u7a7f\u8d8a\u706b\u7ebf\u9ad8\u7ea7\u7279\u6548" : "CrossFire Effects";
            HintText.Text = isChinese
                ? "CF \u7684\u8fde\u6740\u8ba1\u6570\u3001\u7206\u5934/\u5200\u6740\u4e0e\u8fde\u6740\u97f3\u6548\u4f18\u5148\u7ea7\u3001\u9996\u5c3e\u6740\u8bed\u97f3\u548c\u51fb\u6740\u7279\u6548\u90fd\u5728\u8fd9\u91cc\u5355\u72ec\u8bbe\u7f6e\u3002"
                : "CrossFire streaks, headshot/knife audio priorities, first/last-kill audio, and kill effects are configured here.";
            StreakEditor.ApplyLanguage(isChinese);
            HeadshotAudioPriorityLabel.Text = isChinese ? "\u7206\u5934\u97f3\u6548" : "Headshot audio";
            KnifeAudioPriorityLabel.Text = isChinese ? "\u5200\u6740\u97f3\u6548" : "Knife-kill audio";
            HeadshotSpecialPriorityItem.Content = isChinese ? "\u7206\u5934\u4f18\u5148" : "Headshot priority";
            KnifeSpecialPriorityItem.Content = isChinese ? "\u5200\u6740\u4f18\u5148" : "Knife-kill priority";
            HeadshotStreakPriorityItem.Content = isChinese ? "\u8fde\u6740\u4f18\u5148" : "Kill-streak priority";
            KnifeStreakPriorityItem.Content = HeadshotStreakPriorityItem.Content;
            HeadshotIconPriorityLabel.Text = isChinese ? "\u7206\u5934\u56fe\u6807" : "Headshot icon";
            KnifeIconPriorityLabel.Text = isChinese ? "\u5200\u6740\u56fe\u6807" : "Knife-kill icon";
            HeadshotIconSpecialPriorityItem.Content = HeadshotSpecialPriorityItem.Content;
            KnifeIconSpecialPriorityItem.Content = KnifeSpecialPriorityItem.Content;
            HeadshotIconStreakPriorityItem.Content = HeadshotStreakPriorityItem.Content;
            KnifeIconStreakPriorityItem.Content = HeadshotStreakPriorityItem.Content;
            FirstKillAudioLabel.Text = isChinese ? "\u9996\u6740\u8bed\u97f3" : "First-kill audio";
            LastKillAudioLabel.Text = isChinese ? "\u5c3e\u6740\u8bed\u97f3" : "Last-kill audio";
            FirstKillSpecialItem.Content = isChinese ? "\u7279\u6b8a\u97f3\u6548" : "Special audio";
            LastKillSpecialItem.Content = FirstKillSpecialItem.Content;
            FirstKillOriginalItem.Content = isChinese ? "\u539f\u51fb\u6740\u97f3\u6548" : "Original kill audio";
            LastKillOriginalItem.Content = FirstKillOriginalItem.Content;
            FirstKillEffectLabel.Text = isChinese ? "\u9996\u6740\u7279\u6548" : "First-kill effect";
            LastKillEffectLabel.Text = isChinese ? "\u5c3e\u6740\u7279\u6548" : "Last-kill effect";
            FirstKillEffectToggle.OnContent = isChinese ? "\u5f00\u542f\uff08\u9ed8\u8ba4\uff09" : "On (default)";
            LastKillEffectToggle.OnContent = FirstKillEffectToggle.OnContent;
            FirstKillEffectToggle.OffContent = isChinese ? "\u5173\u95ed" : "Off";
            LastKillEffectToggle.OffContent = FirstKillEffectToggle.OffContent;
            AssistAudioLabel.Text = isChinese ? "\u52a9\u653b\u97f3\u6548" : "Assist audio";
            AssistAudioToggle.OnContent = isChinese ? "\u6709\u58f0\u97f3\uff08common\uff09" : "Sound (common)";
            AssistAudioToggle.OffContent = isChinese ? "\u65e0\u58f0\u97f3\uff08\u9ed8\u8ba4\uff09" : "Muted (default)";
            if (StylePanelHost.Content is CrossfireStylePanel stylePanel)
            {
                stylePanel.ApplyLanguage(isChinese);
            }
        }

        public string GetSelectedStreakMode(string fallback)
        {
            return StreakEditor.GetValue(fallback);
        }

        public bool GetHeadshotSpecialAudioPriority(bool fallback)
        {
            return ReadTaggedItem(HeadshotAudioPrioritySelector, fallback ? "special" : "streak") == "special";
        }

        public bool GetKnifeSpecialAudioPriority(bool fallback)
        {
            return ReadTaggedItem(KnifeAudioPrioritySelector, fallback ? "special" : "streak") == "special";
        }

        public bool GetHeadshotSpecialIconPriority(bool fallback)
        {
            return ReadTaggedItem(HeadshotIconPrioritySelector, fallback ? "special" : "streak") == "special";
        }

        public bool GetKnifeSpecialIconPriority(bool fallback)
        {
            return ReadTaggedItem(KnifeIconPrioritySelector, fallback ? "special" : "streak") == "special";
        }

        public bool GetFirstKillSpecialAudio(bool fallback)
        {
            return ReadTaggedItem(FirstKillAudioSelector, fallback ? "special" : "original") == "special";
        }

        public bool GetLastKillSpecialAudio(bool fallback)
        {
            return ReadTaggedItem(LastKillAudioSelector, fallback ? "special" : "original") == "special";
        }

        public bool GetFirstKillEffectEnabled(bool fallback)
        {
            return FirstKillEffectToggle?.IsOn ?? fallback;
        }

        public bool GetLastKillEffectEnabled(bool fallback)
        {
            return LastKillEffectToggle?.IsOn ?? fallback;
        }

        public bool GetAssistAudioEnabled(bool fallback)
        {
            return AssistAudioToggle?.IsOn ?? fallback;
        }

        public void SelectSettings(
            string streakMode,
            bool headshotSpecialPriority,
            bool knifeSpecialPriority,
            bool headshotIconSpecialPriority,
            bool knifeIconSpecialPriority,
            bool firstSpecial,
            bool lastSpecial,
            bool firstKillEffectEnabled,
            bool lastKillEffectEnabled,
            bool assistAudioEnabled)
        {
            StreakEditor.SelectValue(streakMode);
            SelectTaggedItem(HeadshotAudioPrioritySelector, headshotSpecialPriority ? "special" : "streak", "special");
            SelectTaggedItem(KnifeAudioPrioritySelector, knifeSpecialPriority ? "special" : "streak", "special");
            SelectTaggedItem(HeadshotIconPrioritySelector, headshotIconSpecialPriority ? "special" : "streak", "streak");
            SelectTaggedItem(KnifeIconPrioritySelector, knifeIconSpecialPriority ? "special" : "streak", "special");
            SelectTaggedItem(FirstKillAudioSelector, firstSpecial ? "special" : "original", "original");
            SelectTaggedItem(LastKillAudioSelector, lastSpecial ? "special" : "original", "original");
            FirstKillEffectToggle.IsOn = firstKillEffectEnabled;
            LastKillEffectToggle.IsOn = lastKillEffectEnabled;
            AssistAudioToggle.IsOn = assistAudioEnabled;
        }

        private void OnStreakModeSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            StreakModeSelectionChanged?.Invoke(this, e);
        }

        private void OnHeadshotAudioPrioritySelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            HeadshotAudioPrioritySelectionChanged?.Invoke(this, e);
        }

        private void OnKnifeAudioPrioritySelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            KnifeAudioPrioritySelectionChanged?.Invoke(this, e);
        }

        private void OnHeadshotIconPrioritySelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            HeadshotIconPrioritySelectionChanged?.Invoke(this, e);
        }

        private void OnKnifeIconPrioritySelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            KnifeIconPrioritySelectionChanged?.Invoke(this, e);
        }

        private void OnFirstKillAudioSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            FirstKillAudioSelectionChanged?.Invoke(this, e);
        }

        private void OnLastKillAudioSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            LastKillAudioSelectionChanged?.Invoke(this, e);
        }

        private void OnFirstKillEffectToggled(object sender, RoutedEventArgs e)
        {
            FirstKillEffectToggled?.Invoke(this, e);
        }

        private void OnLastKillEffectToggled(object sender, RoutedEventArgs e)
        {
            LastKillEffectToggled?.Invoke(this, e);
        }

        private void OnAssistAudioToggled(object sender, RoutedEventArgs e)
        {
            AssistAudioToggled?.Invoke(this, e);
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

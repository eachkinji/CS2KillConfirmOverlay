using System;
using KillConfirmGameBar.Services;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace KillConfirmGameBar.Controls.GameStyles
{
    public sealed partial class CrossfireAdvancedEffectsPanel : UserControl
    {
        private bool _suppressKillMarkChanges;

        public CrossfireAdvancedEffectsPanel()
        {
            InitializeComponent();
            RefreshKillMarkSetting();
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
            AdvancedEffectsPanelSupport.ApplyResetButton(ResetButton, theme);
            if (VisualEffectsGroupLabel != null) VisualEffectsGroupLabel.Foreground = new SolidColorBrush(theme.Text);
            if (StreakTriggerGroupLabel != null) StreakTriggerGroupLabel.Foreground = new SolidColorBrush(theme.Text);
            if (PrioritiesGroupLabel != null) PrioritiesGroupLabel.Foreground = new SolidColorBrush(theme.Text);
            if (SpecialKillsGroupLabel != null) SpecialKillsGroupLabel.Foreground = new SolidColorBrush(theme.Text);

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
            AdvancedEffectsPanelSupport.ApplyKillMarkCard(VisualEffectsCard, VisualEffectsTitle, KillMarkEffectLabel, KillMarkEffectToggle, theme);
            if (StylePanelHost.Content is CrossfireStylePanel stylePanel)
            {
                stylePanel.ApplyTheme(theme);
            }
        }

        public void ApplyLanguage(bool isChinese)
        {
            TitleText.Text = isChinese ? "CF（穿越火线）战斗与视效" : "CrossFire Combat & Effects";
            HintText.Text = isChinese
                ? "集中设置 CF 的连杀规则、爆头与刀杀提示、首杀与最终击杀，以及画面特效和武器徽章。"
                : "Centralized control for CrossFire streak triggers, priority resolutions, first/last kills, and visual FX.";
            ResetButtonText.Text = isChinese ? "恢复默认" : "Reset";
            ToolTipService.SetToolTip(ResetButton, isChinese ? "恢复 CF 默认设置" : "Restore CF defaults");

            if (VisualEffectsGroupLabel != null)
                VisualEffectsGroupLabel.Text = isChinese ? "画面特效与武器徽章" : "Visual FX & Badges";
            if (StreakTriggerGroupLabel != null)
                StreakTriggerGroupLabel.Text = isChinese ? "连杀与助攻设置" : "Streak & Assist Triggers";
            if (PrioritiesGroupLabel != null)
                PrioritiesGroupLabel.Text = isChinese ? "爆头与刀杀提示优先级" : "Priority Policies (Audio & Icon)";
            if (SpecialKillsGroupLabel != null)
                SpecialKillsGroupLabel.Text = isChinese ? "首杀与最终击杀" : "Special & First/Last Kills";

            StreakEditor.ApplyLanguage(isChinese);
            HeadshotAudioPriorityLabel.Text = isChinese ? "爆头声音" : "Headshot audio";
            KnifeAudioPriorityLabel.Text = isChinese ? "刀杀声音" : "Knife-kill audio";
            HeadshotSpecialPriorityItem.Content = isChinese ? "爆头优先" : "Headshot priority";
            KnifeSpecialPriorityItem.Content = isChinese ? "刀杀优先" : "Knife-kill priority";
            HeadshotStreakPriorityItem.Content = isChinese ? "连杀优先" : "Kill-streak priority";
            KnifeStreakPriorityItem.Content = HeadshotStreakPriorityItem.Content;
            HeadshotIconPriorityLabel.Text = isChinese ? "爆头图标" : "Headshot icon";
            KnifeIconPriorityLabel.Text = isChinese ? "刀杀图标" : "Knife-kill icon";
            HeadshotIconSpecialPriorityItem.Content = HeadshotSpecialPriorityItem.Content;
            KnifeIconSpecialPriorityItem.Content = KnifeSpecialPriorityItem.Content;
            HeadshotIconStreakPriorityItem.Content = HeadshotStreakPriorityItem.Content;
            KnifeIconStreakPriorityItem.Content = HeadshotStreakPriorityItem.Content;
            FirstKillAudioLabel.Text = isChinese ? "首杀语音" : "First-kill audio";
            LastKillAudioLabel.Text = isChinese ? "最终击杀语音" : "Last-kill audio";
            FirstKillSpecialItem.Content = isChinese ? "特殊音效（手雷）" : "Special audio (grenade)";
            LastKillSpecialItem.Content = FirstKillSpecialItem.Content;
            FirstKillOriginalItem.Content = isChinese ? "原击杀音效" : "Original kill audio";
            LastKillOriginalItem.Content = FirstKillOriginalItem.Content;
            FirstKillEffectLabel.Text = isChinese ? "首杀特效" : "First-kill effect";
            LastKillEffectLabel.Text = isChinese ? "最终击杀特效" : "Last-kill effect";
            FirstKillEffectToggle.OnContent = isChinese ? "开启（默认）" : "On (default)";
            LastKillEffectToggle.OnContent = FirstKillEffectToggle.OnContent;
            FirstKillEffectToggle.OffContent = isChinese ? "关闭" : "Off";
            LastKillEffectToggle.OffContent = FirstKillEffectToggle.OffContent;
            AssistAudioLabel.Text = isChinese ? "助攻音效" : "Assist audio";
            AssistAudioToggle.OnContent = isChinese ? "有声音（common）" : "Sound (common)";
            AssistAudioToggle.OffContent = isChinese ? "无声音（默认）" : "Muted (default)";
            AdvancedEffectsPanelSupport.ApplyKillMarkLanguage(VisualEffectsTitle, KillMarkEffectLabel, KillMarkEffectToggle, isChinese);
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

        private void OnResetButtonClick(object sender, RoutedEventArgs e)
        {
            StreakEditor.SelectValue(SharedStreakSettingsStore.LifeMode);
            SelectTaggedItem(HeadshotAudioPrioritySelector, "special", "special");
            SelectTaggedItem(KnifeAudioPrioritySelector, "special", "special");
            SelectTaggedItem(HeadshotIconPrioritySelector, "special", "special");
            SelectTaggedItem(KnifeIconPrioritySelector, "special", "special");
            SelectTaggedItem(FirstKillAudioSelector, "special", "special");
            SelectTaggedItem(LastKillAudioSelector, "special", "special");
            FirstKillEffectToggle.IsOn = true;
            LastKillEffectToggle.IsOn = true;
            AssistAudioToggle.IsOn = false;
            SetKillMarkEnabled(false);

            StreakModeSelectionChanged?.Invoke(StreakEditor.SelectorControl, null);
            HeadshotAudioPrioritySelectionChanged?.Invoke(HeadshotAudioPrioritySelector, null);
            KnifeAudioPrioritySelectionChanged?.Invoke(KnifeAudioPrioritySelector, null);
            HeadshotIconPrioritySelectionChanged?.Invoke(HeadshotIconPrioritySelector, null);
            KnifeIconPrioritySelectionChanged?.Invoke(KnifeIconPrioritySelector, null);
            FirstKillAudioSelectionChanged?.Invoke(FirstKillAudioSelector, null);
            LastKillAudioSelectionChanged?.Invoke(LastKillAudioSelector, null);
            FirstKillEffectToggled?.Invoke(FirstKillEffectToggle, null);
            LastKillEffectToggled?.Invoke(LastKillEffectToggle, null);
            AssistAudioToggled?.Invoke(AssistAudioToggle, null);
        }

        private void RefreshKillMarkSetting()
        {
            SetKillMarkEnabled(KillFeedbackVisibilitySettingsStore.Load(GameStyleMode.Crossfire).CrosshairEnabled, false);
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
            KillFeedbackVisibilitySettingsStore.Save(
                GameStyleMode.Crossfire,
                new KillFeedbackVisibilitySettingsValues { CrosshairEnabled = KillMarkEffectToggle.IsOn });
        }
    }
}

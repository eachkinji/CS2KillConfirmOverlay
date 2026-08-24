using System;
using System.Collections.Generic;
using KillConfirmGameBar.Services;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace KillConfirmGameBar.Controls.GameStyles
{
    public sealed partial class CsolAdvancedEffectsPanel : UserControl
    {
        private bool _suppressSelectionChanged;
        private bool _suppressKillMarkChanges;

        public CsolAdvancedEffectsPanel()
        {
            InitializeComponent();
            RefreshKillMarkSetting();
        }

        public event SelectionChangedEventHandler VoiceSettingChanged;

        public ComboBox StreakModeSelectorControl => StreakEditor.SelectorControl;

        internal void ApplyTheme(GameThemePalette theme)
        {
            AdvancedEffectsPanelSupport.ApplyHeader(TitleText, HintText, theme);
            TitleAccent.Background = new SolidColorBrush(theme.Accent);
            CoverageNotice.Background = new SolidColorBrush(theme.AccentSoft);
            CoverageNotice.BorderBrush = new SolidColorBrush(theme.SoftBorder);
            CoverageIcon.Foreground = new SolidColorBrush(theme.AccentText);
            CoverageText.Foreground = new SolidColorBrush(theme.AccentText);
            ResetButton.Background = new SolidColorBrush(theme.Accent);
            ResetButton.BorderBrush = new SolidColorBrush(theme.Accent);
            ResetButton.Foreground = new SolidColorBrush(Windows.UI.Colors.White);
            StreakEditor.ApplyTheme(theme);
            AdvancedEffectsPanelSupport.ApplyMoneyRow(PriorityLabel, PrioritySelector, theme);
            AdvancedEffectsPanelSupport.ApplyMoneyRow(FirstKillIconLabel, FirstKillIconSelector, theme);
            AdvancedEffectsPanelSupport.ApplyMoneyRow(LastKillIconLabel, LastKillIconSelector, theme);
            AdvancedEffectsPanelSupport.ApplyMoneyRow(LastKillAudioLabel, LastKillAudioSelector, theme);
            AdvancedEffectsPanelSupport.ApplyKillMarkCard(VisualEffectsCard, VisualEffectsTitle, KillMarkEffectLabel, KillMarkEffectToggle, theme);
        }

        public void ApplyLanguage(bool isChinese)
        {
            TitleText.Text = isChinese ? "CSOL 高级特效" : "CSOL Effects";
            HintText.Text = isChinese
                ? "集中设置连杀时间、特殊击杀优先级，以及独立的首杀和尾杀效果。"
                : "Configure kill-streak timing, special-event priority, and separate first/last-kill effects.";
            CoverageText.Text = isChinese
                ? "CSOL 语音包可以分别设置 1～10 杀语音。"
                : "The CSOL pack now plays distinct streak voices from 1 through 10 kills.";
            ResetButtonText.Text = isChinese ? "恢复默认" : "Reset";
            ToolTipService.SetToolTip(ResetButton, isChinese ? "恢复 CSOL 默认设置" : "Restore CSOL defaults");
            StreakEditor.ApplyLanguage(isChinese);
            PriorityLabel.Text = isChinese ? "语音优先级" : "Voice priority";
            PrioritySpecialItem.Content = isChinese ? "特殊优先" : "Special first";
            PriorityStreakItem.Content = isChinese ? "连杀优先" : "Kill-streak first";
            FirstKillIconLabel.Text = isChinese ? "首杀图标" : "First-kill icon";
            LastKillIconLabel.Text = isChinese ? "尾杀图标" : "Last-kill icon";
            FirstKillIconRevengeItem.Content = isChinese ? "复仇" : "Revenge";
            FirstKillIconFirstKillItem.Content = isChinese ? "首杀" : "First kill";
            LastKillIconRevengeItem.Content = isChinese ? "复仇" : "Revenge";
            LastKillIconFirstKillItem.Content = isChinese ? "首杀" : "First kill";
            LastKillAudioLabel.Text = isChinese ? "尾杀语音" : "Last-kill audio";
            LastKillSpecialAudioItem.Content = isChinese ? "尾杀音效" : "Last-kill sound";
            LastKillNormalAudioItem.Content = isChinese ? "正常击杀音效" : "Normal kill sound";
            AdvancedEffectsPanelSupport.ApplyKillMarkLanguage(VisualEffectsTitle, KillMarkEffectLabel, KillMarkEffectToggle, isChinese);
        }

        public string GetSelectedStreakMode(string fallback)
        {
            return StreakEditor.GetValue(fallback);
        }

        public void SelectStreakMode(string value)
        {
            StreakEditor.SelectValue(value);
        }

        public bool GetSpecialVoicePriority(bool fallback)
        {
            return ReadTaggedItem(PrioritySelector, fallback ? "special" : "streak") == "special";
        }

        public string GetFirstKillIcon(string fallback)
        {
            return NormalizeIcon(ReadTaggedItem(FirstKillIconSelector, fallback), fallback);
        }

        public string GetLastKillIcon(string fallback)
        {
            return NormalizeIcon(ReadTaggedItem(LastKillIconSelector, fallback), fallback);
        }

        public bool GetLastKillSpecialAudio(bool fallback)
        {
            return ReadTaggedItem(LastKillAudioSelector, fallback ? "special" : "normal") == "special";
        }

        private static string NormalizeIcon(string value, string fallback)
        {
            if (string.Equals(value, CsolVoiceSettingsStore.FirstKillIcon, StringComparison.OrdinalIgnoreCase))
            {
                return CsolVoiceSettingsStore.FirstKillIcon;
            }

            if (string.Equals(value, CsolVoiceSettingsStore.RevengeIcon, StringComparison.OrdinalIgnoreCase))
            {
                return CsolVoiceSettingsStore.RevengeIcon;
            }

            return fallback;
        }

        public void SelectSettings(
            string streakMode,
            bool specialVoicePriority,
            bool lastKillSpecialAudio,
            string firstKillIcon,
            string lastKillIcon)
        {
            _suppressSelectionChanged = true;
            try
            {
                StreakEditor.SelectValue(streakMode);
                SelectTaggedItem(PrioritySelector, specialVoicePriority ? "special" : "streak", "streak");
                SelectTaggedItem(LastKillAudioSelector, lastKillSpecialAudio ? "special" : "normal", "special");
                SelectTaggedItem(
                    FirstKillIconSelector,
                    NormalizeIcon(firstKillIcon, CsolVoiceSettingsStore.FirstKillIcon),
                    CsolVoiceSettingsStore.FirstKillIcon);
                SelectTaggedItem(
                    LastKillIconSelector,
                    NormalizeIcon(lastKillIcon, CsolVoiceSettingsStore.RevengeIcon),
                    CsolVoiceSettingsStore.RevengeIcon);
            }
            finally
            {
                _suppressSelectionChanged = false;
            }
        }

        private void OnVoiceSettingChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressSelectionChanged)
            {
                return;
            }

            VoiceSettingChanged?.Invoke(this, e);
        }

        private void OnResetButtonClick(object sender, RoutedEventArgs e)
        {
            _suppressSelectionChanged = true;
            try
            {
                StreakEditor.SelectValue(SharedStreakSettingsStore.LifeMode);
                SelectTaggedItem(PrioritySelector, "streak", "streak");
                SelectTaggedItem(FirstKillIconSelector, CsolVoiceSettingsStore.FirstKillIcon, CsolVoiceSettingsStore.FirstKillIcon);
                SelectTaggedItem(LastKillIconSelector, CsolVoiceSettingsStore.RevengeIcon, CsolVoiceSettingsStore.RevengeIcon);
                SelectTaggedItem(LastKillAudioSelector, "special", "special");
                SetKillMarkEnabled(false);
            }
            finally
            {
                _suppressSelectionChanged = false;
            }

            VoiceSettingChanged?.Invoke(this, null);
        }

        private void RefreshKillMarkSetting()
        {
            SetKillMarkEnabled(KillFeedbackVisibilitySettingsStore.Load(GameStyleMode.Csol).CrosshairEnabled, false);
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
                GameStyleMode.Csol,
                new KillFeedbackVisibilitySettingsValues { CrosshairEnabled = KillMarkEffectToggle.IsOn });
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

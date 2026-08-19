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

        public CsolAdvancedEffectsPanel()
        {
            InitializeComponent();
        }

        public event SelectionChangedEventHandler VoiceSettingChanged;
        public event EventHandler ImportVoiceRequested;

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

            if (ImportVoiceButton != null)
            {
                ImportVoiceButton.Background = new SolidColorBrush(theme.AccentSoft);
                ImportVoiceButton.BorderBrush = new SolidColorBrush(theme.SoftBorder);
                ImportVoiceButton.Foreground = new SolidColorBrush(theme.AccentText);
            }
        }

        public void ApplyLanguage(bool isChinese)
        {
            TitleText.Text = isChinese ? "CSOL 高级特效" : "CSOL Effects";
            HintText.Text = isChinese
                ? "集中设置连杀时间、特殊击杀优先级，以及独立的首杀和尾杀效果。"
                : "Configure kill-streak timing, special-event priority, and separate first/last-kill effects.";
            CoverageText.Text = isChinese
                ? "CSOL 语音包现已完整覆盖 1～10 杀连杀语音。"
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

            VoiceManagerTitleText.Text = isChinese ? "CSOL 语音包事件音效与随机管理" : "CSOL Voice Events & Randomizer";
            VoiceManagerHintText.Text = isChinese
                ? "为多音效事件（如 1杀、4杀、刀杀等）指定具体播放音效或设为随机播放。"
                : "Choose specific voice files or enable random voice picks per kill event.";
            ImportVoiceButtonText.Text = isChinese ? "导入语音" : "Import Voice";

            Kill1VoiceLabel.Text = isChinese ? "1杀音效" : "1-Kill Voice";
            Kill4VoiceLabel.Text = isChinese ? "4杀音效" : "4-Kill Voice";
            KnifeVoiceLabel.Text = isChinese ? "小刀击杀音效" : "Knife Voice";
            FirstKillVoiceLabel.Text = isChinese ? "首杀专属音效" : "First-Kill Voice";
            LastKillVoiceLabel.Text = isChinese ? "尾杀/复仇音效" : "Last-Kill Voice";
            AssistVoiceLabel.Text = isChinese ? "助攻音效" : "Assist Voice";

            Kill1RandomItem.Content = isChinese ? "🎲 随机语音" : "🎲 Random Voice";
            Kill4RandomItem.Content = isChinese ? "🎲 随机语音" : "🎲 Random Voice";
            KnifeRandomItem.Content = isChinese ? "🎲 随机语音" : "🎲 Random Voice";
            FirstKillRandomItem.Content = isChinese ? "🎲 随机语音" : "🎲 Random Voice";
            LastKillRandomItem.Content = isChinese ? "🎲 随机语音" : "🎲 Random Voice";
            AssistRandomItem.Content = isChinese ? "🎲 随机语音" : "🎲 Random Voice";
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

        public Dictionary<string, string> GetVoicePicks()
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["1"] = ReadTaggedItem(Kill1VoiceSelector, CsolVoiceSettingsStore.RandomPick),
                ["4"] = ReadTaggedItem(Kill4VoiceSelector, CsolVoiceSettingsStore.RandomPick),
                ["knife"] = ReadTaggedItem(KnifeVoiceSelector, CsolVoiceSettingsStore.RandomPick),
                ["first"] = ReadTaggedItem(FirstKillVoiceSelector, CsolVoiceSettingsStore.RandomPick),
                ["last"] = ReadTaggedItem(LastKillVoiceSelector, CsolVoiceSettingsStore.RandomPick),
                ["assist"] = ReadTaggedItem(AssistVoiceSelector, CsolVoiceSettingsStore.RandomPick)
            };
        }

        public void SelectSettings(
            string streakMode,
            bool specialVoicePriority,
            string firstKillIcon,
            string lastKillIcon,
            IReadOnlyDictionary<string, string> voicePicks = null)
        {
            _suppressSelectionChanged = true;
            try
            {
                StreakEditor.SelectValue(streakMode);
                SelectTaggedItem(PrioritySelector, specialVoicePriority ? "special" : "streak", "streak");
                SelectTaggedItem(
                    FirstKillIconSelector,
                    NormalizeIcon(firstKillIcon, CsolVoiceSettingsStore.FirstKillIcon),
                    CsolVoiceSettingsStore.FirstKillIcon);
                SelectTaggedItem(
                    LastKillIconSelector,
                    NormalizeIcon(lastKillIcon, CsolVoiceSettingsStore.RevengeIcon),
                    CsolVoiceSettingsStore.RevengeIcon);

                string pick1 = voicePicks != null && voicePicks.TryGetValue("1", out string v1) ? v1 : CsolVoiceSettingsStore.RandomPick;
                string pick4 = voicePicks != null && voicePicks.TryGetValue("4", out string v4) ? v4 : CsolVoiceSettingsStore.RandomPick;
                string pickKnife = voicePicks != null && voicePicks.TryGetValue("knife", out string vk) ? vk : CsolVoiceSettingsStore.RandomPick;
                string pickFirst = voicePicks != null && voicePicks.TryGetValue("first", out string vf) ? vf : CsolVoiceSettingsStore.RandomPick;
                string pickLast = voicePicks != null && voicePicks.TryGetValue("last", out string vl) ? vl : CsolVoiceSettingsStore.RandomPick;
                string pickAssist = voicePicks != null && voicePicks.TryGetValue("assist", out string va) ? va : CsolVoiceSettingsStore.RandomPick;

                SelectTaggedItem(Kill1VoiceSelector, pick1, CsolVoiceSettingsStore.RandomPick);
                SelectTaggedItem(Kill4VoiceSelector, pick4, CsolVoiceSettingsStore.RandomPick);
                SelectTaggedItem(KnifeVoiceSelector, pickKnife, CsolVoiceSettingsStore.RandomPick);
                SelectTaggedItem(FirstKillVoiceSelector, pickFirst, CsolVoiceSettingsStore.RandomPick);
                SelectTaggedItem(LastKillVoiceSelector, pickLast, CsolVoiceSettingsStore.RandomPick);
                SelectTaggedItem(AssistVoiceSelector, pickAssist, CsolVoiceSettingsStore.RandomPick);
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

        private void OnImportVoiceButtonClick(object sender, RoutedEventArgs e)
        {
            ImportVoiceRequested?.Invoke(this, EventArgs.Empty);
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
                SelectTaggedItem(Kill1VoiceSelector, CsolVoiceSettingsStore.RandomPick, CsolVoiceSettingsStore.RandomPick);
                SelectTaggedItem(Kill4VoiceSelector, CsolVoiceSettingsStore.RandomPick, CsolVoiceSettingsStore.RandomPick);
                SelectTaggedItem(KnifeVoiceSelector, CsolVoiceSettingsStore.RandomPick, CsolVoiceSettingsStore.RandomPick);
                SelectTaggedItem(FirstKillVoiceSelector, CsolVoiceSettingsStore.RandomPick, CsolVoiceSettingsStore.RandomPick);
                SelectTaggedItem(LastKillVoiceSelector, CsolVoiceSettingsStore.RandomPick, CsolVoiceSettingsStore.RandomPick);
                SelectTaggedItem(AssistVoiceSelector, CsolVoiceSettingsStore.RandomPick, CsolVoiceSettingsStore.RandomPick);
            }
            finally
            {
                _suppressSelectionChanged = false;
            }

            VoiceSettingChanged?.Invoke(this, null);
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

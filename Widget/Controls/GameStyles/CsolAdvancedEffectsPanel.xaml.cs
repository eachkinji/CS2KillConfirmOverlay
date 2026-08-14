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
            PopulateVariantSelectors();
        }

        public event SelectionChangedEventHandler VoiceSettingChanged;

        public ComboBox StreakModeSelectorControl => StreakEditor.SelectorControl;

        private static (ComboBox Selector, string KillType)[] GetVoiceSelectors(CsolAdvancedEffectsPanel panel)
        {
            return new[]
            {
                (panel.OneKillVoiceSelector, "1"),
                (panel.FourKillVoiceSelector, "4"),
                (panel.KnifeVoiceSelector, "knife"),
                (panel.FirstKillVoiceSelector, "first"),
                (panel.LastKillVoiceSelector, "last")
            };
        }

        private void PopulateVariantSelectors()
        {
            foreach ((ComboBox selector, string killType) in GetVoiceSelectors(this))
            {
                if (CsolVoiceSettingsStore.VoiceVariants.TryGetValue(killType, out string[] variants))
                {
                    foreach (string variant in variants)
                    {
                        selector.Items.Add(CreateVariantItem(variant));
                    }
                }
            }
        }

        private static ComboBoxItem CreateVariantItem(string fileName)
        {
            string display = fileName;
            int dot = display.LastIndexOf('.');
            if (dot > 0)
            {
                display = display.Substring(0, dot);
            }

            return new ComboBoxItem
            {
                Tag = fileName,
                Content = display
            };
        }

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
            AdvancedEffectsPanelSupport.ApplyMoneyRow(OneKillLabel, OneKillVoiceSelector, theme);
            AdvancedEffectsPanelSupport.ApplyMoneyRow(FourKillLabel, FourKillVoiceSelector, theme);
            AdvancedEffectsPanelSupport.ApplyMoneyRow(KnifeLabel, KnifeVoiceSelector, theme);
            AdvancedEffectsPanelSupport.ApplyMoneyRow(FirstKillVoiceLabel, FirstKillVoiceSelector, theme);
            AdvancedEffectsPanelSupport.ApplyMoneyRow(LastKillVoiceLabel, LastKillVoiceSelector, theme);
            AdvancedEffectsPanelSupport.ApplyMoneyRow(FirstKillIconLabel, FirstKillIconSelector, theme);
            AdvancedEffectsPanelSupport.ApplyMoneyRow(LastKillIconLabel, LastKillIconSelector, theme);
        }

        public void ApplyLanguage(bool isChinese)
        {
            TitleText.Text = isChinese ? "CSOL 高级特效" : "CSOL Effects";
            HintText.Text = isChinese
                ? "集中设置连杀时间、语音变体、特殊击杀优先级，以及独立的首杀和尾杀效果。"
                : "Configure kill-streak timing, voice variants, special-event priority and separate first/last-kill effects.";
            CoverageText.Text = isChinese
                ? "CSOL 语音包现已完整覆盖 1～10 杀连杀语音。"
                : "The CSOL pack now plays distinct streak voices from 1 through 10 kills.";
            ResetButtonText.Text = isChinese ? "恢复默认" : "Reset";
            ToolTipService.SetToolTip(ResetButton, isChinese ? "恢复 CSOL 默认设置" : "Restore CSOL defaults");
            StreakEditor.ApplyLanguage(isChinese);
            PriorityLabel.Text = isChinese ? "语音优先级" : "Voice priority";
            PrioritySpecialItem.Content = isChinese ? "特殊优先" : "Special first";
            PriorityStreakItem.Content = isChinese ? "连杀优先" : "Kill-streak first";
            OneKillLabel.Text = isChinese ? "1杀语音" : "1-kill voice";
            FourKillLabel.Text = isChinese ? "4杀语音" : "4-kill voice";
            KnifeLabel.Text = isChinese ? "刀杀语音" : "Knife-kill voice";
            FirstKillVoiceLabel.Text = isChinese ? "首杀语音" : "First-kill voice";
            LastKillVoiceLabel.Text = isChinese ? "尾杀语音" : "Last-kill voice";
            FirstKillIconLabel.Text = isChinese ? "首杀图标" : "First-kill icon";
            LastKillIconLabel.Text = isChinese ? "尾杀图标" : "Last-kill icon";
            FirstKillIconRevengeItem.Content = isChinese ? "复仇" : "Revenge";
            FirstKillIconFirstKillItem.Content = isChinese ? "首杀" : "First kill";
            LastKillIconRevengeItem.Content = isChinese ? "复仇" : "Revenge";
            LastKillIconFirstKillItem.Content = isChinese ? "首杀" : "First kill";

            foreach ((ComboBox selector, string _) in GetVoiceSelectors(this))
            {
                foreach (object option in selector.Items)
                {
                    if (option is ComboBoxItem item
                        && item.Tag is string tag
                        && string.Equals(tag, CsolVoiceSettingsStore.RandomPick, StringComparison.OrdinalIgnoreCase))
                    {
                        item.Content = isChinese ? "随机" : "Random";
                    }
                }
            }
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
            var picks = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach ((ComboBox selector, string killType) in GetVoiceSelectors(this))
            {
                string value = ReadTaggedItem(selector, CsolVoiceSettingsStore.RandomPick);
                picks[killType] = CsolVoiceSettingsStore.RandomPick.Equals(value, StringComparison.OrdinalIgnoreCase)
                    ? CsolVoiceSettingsStore.RandomPick
                    : value;
            }

            return picks;
        }

        public void SelectSettings(
            string streakMode,
            bool specialVoicePriority,
            string firstKillIcon,
            string lastKillIcon,
            IReadOnlyDictionary<string, string> voicePicks)
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

                foreach ((ComboBox selector, string killType) in GetVoiceSelectors(this))
                {
                    string pick = voicePicks != null && voicePicks.TryGetValue(killType, out string stored)
                        ? stored
                        : CsolVoiceSettingsStore.RandomPick;
                    SelectTaggedItem(selector, pick, CsolVoiceSettingsStore.RandomPick);
                }
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
                foreach ((ComboBox selector, string _) in GetVoiceSelectors(this))
                {
                    SelectTaggedItem(selector, CsolVoiceSettingsStore.RandomPick, CsolVoiceSettingsStore.RandomPick);
                }
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

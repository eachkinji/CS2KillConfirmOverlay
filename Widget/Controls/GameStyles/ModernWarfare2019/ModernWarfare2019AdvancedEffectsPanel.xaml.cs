using System;
using System.Globalization;
using KillConfirmGameBar.Services;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace KillConfirmGameBar.Controls.GameStyles
{
    public sealed partial class ModernWarfare2019AdvancedEffectsPanel : UserControl
    {
        private bool _suppressVisualEffectChanges;

        public ModernWarfare2019AdvancedEffectsPanel()
        {
            InitializeComponent();
            RefreshVisualEffectSettings();
            SelectAssistAudio(AssistAudioSettingsStore.Load(GameStyleMode.ModernWarfare2019));
            SelectRightFeedOffset(ModernWarfare2019FeedOffsetSettingsStore.Load());
        }

        public event SelectionChangedEventHandler MoneyRewardModeSelectionChanged;
        public event SelectionChangedEventHandler StreakModeSelectionChanged;
        public event RoutedEventHandler AssistAudioToggled;

        public ComboBox MoneyRewardModeSelectorControl => MoneyRewardModeSelector;
        public ComboBoxItem MoneyRewardDeltaItemControl => MoneyRewardDeltaItem;
        public ComboBoxItem MoneyRewardRulesItemControl => MoneyRewardRulesItem;
        public TextBlock MoneyRewardModeLabelControl => MoneyRewardModeLabel;

        internal void ApplyTheme(GameThemePalette theme)
        {
            TitleText.Foreground = Brush(theme.Text);
            VisualEffectsCard.Background = Brush(theme.Card);
            VisualEffectsCard.BorderBrush = Brush(theme.SoftBorder);
            RightFeedOffsetCard.Background = Brush(theme.Card);
            RightFeedOffsetCard.BorderBrush = Brush(theme.SoftBorder);
            AdvancedEffectsPanelSupport.ApplyResetButton(ResetButton, theme);
            AdvancedEffectsPanelSupport.ApplyMoneyRow(MoneyRewardModeLabel, MoneyRewardModeSelector, theme);
            StreakEditor.ApplyTheme(theme);
            VisualEffectsTitle.Foreground = Brush(theme.Text);
            UpperEffectLabel.Foreground = Brush(theme.Text);
            CrosshairEffectLabel.Foreground = Brush(theme.Text);
            LowerEffectLabel.Foreground = Brush(theme.Text);
            UpperEffectToggle.Foreground = Brush(theme.Text);
            CrosshairEffectToggle.Foreground = Brush(theme.Text);
            LowerEffectToggle.Foreground = Brush(theme.Text);
            AssistAudioLabel.Foreground = Brush(theme.Text);
            AssistAudioToggle.Foreground = Brush(theme.Text);
            RightFeedOffsetLabel.Foreground = Brush(theme.Text);
            RightFeedOffsetValue.Foreground = Brush(theme.Accent);
            RightFeedOffsetHint.Foreground = Brush(theme.MutedText);
        }

        public void ApplyLanguage(bool isChinese)
        {
            TitleText.Text = isChinese ? "MW2019 击杀提示设置" : "MW2019 Kill Feedback";
            ResetButtonText.Text = isChinese ? "恢复默认" : "Reset";
            ToolTipService.SetToolTip(ResetButton, isChinese ? "恢复 MW2019 默认设置" : "Restore MW2019 defaults");
            MoneyRewardModeLabel.Text = isChinese ? "金钱计算方式" : "Money calculation";
            MoneyRewardDeltaItem.Content = isChinese ? "按实际金钱变化（推荐）" : "Actual money change (recommended)";
            MoneyRewardRulesItem.Content = isChinese ? "按武器击杀奖励" : "Weapon kill reward";
            StreakEditor.ApplyLanguage(isChinese);
            VisualEffectsTitle.Text = isChinese ? "显示哪些击杀提示" : "Visible kill feedback";
            UpperEffectLabel.Text = isChinese ? "上方连杀提示" : "Upper streak notice";
            CrosshairEffectLabel.Text = isChinese ? "中央准心提示" : "Center crosshair feedback";
            LowerEffectLabel.Text = isChinese ? "下方第 N 杀提示" : "Lower kill-count banner";
            AssistAudioLabel.Text = isChinese ? "助攻时播放语音" : "Play voice on assist";
            RightFeedOffsetLabel.Text = isChinese
                ? "右侧金钱与文字离准心距离"
                : "Money and text distance from crosshair";
            RightFeedOffsetHint.Text = isChinese
                ? "数值越大，右侧金钱和文字瀑布越向右移动；准心本身不会移动。"
                : "Higher values move the money and text waterfall farther right without moving the crosshair.";
            ApplyToggleLanguage(UpperEffectToggle, isChinese);
            ApplyToggleLanguage(CrosshairEffectToggle, isChinese);
            ApplyToggleLanguage(LowerEffectToggle, isChinese);
            ApplyToggleLanguage(AssistAudioToggle, isChinese);
        }

        public string GetSelectedMoneyRewardMode(string fallback) =>
            ReadTaggedItem(MoneyRewardModeSelector, fallback);

        public void SelectMoneyRewardMode(string value, string fallback) =>
            SelectTaggedItem(MoneyRewardModeSelector, value, fallback);

        public string GetSelectedStreakMode(string fallback) => StreakEditor.GetValue(fallback);

        public void SelectStreakMode(string value) => StreakEditor.SelectValue(value);

        public bool GetAssistAudioEnabled(bool fallback) =>
            AssistAudioToggle == null ? fallback : AssistAudioToggle.IsOn;

        public void SelectAssistAudio(bool enabled)
        {
            _suppressVisualEffectChanges = true;
            try
            {
                AssistAudioToggle.IsOn = enabled;
            }
            finally
            {
                _suppressVisualEffectChanges = false;
            }
        }

        private void SelectRightFeedOffset(double value)
        {
            _suppressVisualEffectChanges = true;
            try
            {
                RightFeedOffsetSlider.Minimum = ModernWarfare2019FeedOffsetSettingsStore.MinimumOffset;
                RightFeedOffsetSlider.Maximum = ModernWarfare2019FeedOffsetSettingsStore.MaximumOffset;
                RightFeedOffsetSlider.StepFrequency = ModernWarfare2019FeedOffsetSettingsStore.OffsetStep;
                RightFeedOffsetSlider.Value = value;
                UpdateRightFeedOffsetValue();
            }
            finally
            {
                _suppressVisualEffectChanges = false;
            }
        }

        private void OnRightFeedOffsetValueChanged(
            object sender,
            Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            UpdateRightFeedOffsetValue();
            if (!_suppressVisualEffectChanges)
            {
                ModernWarfare2019FeedOffsetSettingsStore.Save(e.NewValue);
            }
        }

        private void UpdateRightFeedOffsetValue()
        {
            if (RightFeedOffsetValue != null && RightFeedOffsetSlider != null)
            {
                RightFeedOffsetValue.Text = "+"
                    + Math.Round(RightFeedOffsetSlider.Value)
                        .ToString(CultureInfo.InvariantCulture);
            }
        }

        public void RefreshVisualEffectSettings()
        {
            KillFeedbackVisibilitySettingsValues settings =
                KillFeedbackVisibilitySettingsStore.Load(GameStyleMode.ModernWarfare2019);
            _suppressVisualEffectChanges = true;
            try
            {
                UpperEffectToggle.IsOn = settings.UpperEnabled;
                CrosshairEffectToggle.IsOn = settings.CrosshairEnabled;
                LowerEffectToggle.IsOn = settings.LowerEnabled;
            }
            finally
            {
                _suppressVisualEffectChanges = false;
            }
        }

        private void OnMoneyRewardModeSelectionChanged(object sender, SelectionChangedEventArgs e) =>
            MoneyRewardModeSelectionChanged?.Invoke(this, e);

        private void OnStreakModeSelectionChanged(object sender, RoutedEventArgs e) =>
            StreakModeSelectionChanged?.Invoke(this, null);

        private void OnVisualEffectToggled(object sender, RoutedEventArgs e)
        {
            if (_suppressVisualEffectChanges)
            {
                return;
            }

            KillFeedbackVisibilitySettingsStore.Save(
                GameStyleMode.ModernWarfare2019,
                new KillFeedbackVisibilitySettingsValues
                {
                    UpperEnabled = UpperEffectToggle.IsOn,
                    CrosshairEnabled = CrosshairEffectToggle.IsOn,
                    LowerEnabled = LowerEffectToggle.IsOn
                });
        }

        private void OnResetButtonClick(object sender, RoutedEventArgs e)
        {
            SelectTaggedItem(MoneyRewardModeSelector, "delta", "delta");
            StreakEditor.SelectValue(SharedStreakSettingsStore.LifeMode);
            _suppressVisualEffectChanges = true;
            UpperEffectToggle.IsOn = true;
            CrosshairEffectToggle.IsOn = true;
            LowerEffectToggle.IsOn = true;
            AssistAudioToggle.IsOn = false;
            RightFeedOffsetSlider.Value = ModernWarfare2019FeedOffsetSettingsStore.DefaultOffset;
            _suppressVisualEffectChanges = false;
            ModernWarfare2019FeedOffsetSettingsStore.Save(
                ModernWarfare2019FeedOffsetSettingsStore.DefaultOffset);
            OnVisualEffectToggled(this, null);
            OnAssistAudioToggled(this, null);
            MoneyRewardModeSelectionChanged?.Invoke(this, null);
            StreakModeSelectionChanged?.Invoke(this, null);
        }

        private static string ReadTaggedItem(ComboBox selector, string fallback)
        {
            return selector?.SelectedItem is ComboBoxItem item
                && item.Tag is string tag
                && !string.IsNullOrWhiteSpace(tag)
                    ? tag
                    : fallback;
        }

        private void OnAssistAudioToggled(object sender, RoutedEventArgs e)
        {
            if (!_suppressVisualEffectChanges)
            {
                AssistAudioToggled?.Invoke(this, e);
            }
        }

        private static void SelectTaggedItem(ComboBox selector, string value, string fallback)
        {
            if (selector == null) return;
            string target = string.IsNullOrWhiteSpace(value) ? fallback : value;
            foreach (object option in selector.Items)
            {
                if (option is ComboBoxItem item
                    && string.Equals(item.Tag?.ToString(), target, System.StringComparison.OrdinalIgnoreCase))
                {
                    selector.SelectedItem = item;
                    return;
                }
            }
            selector.SelectedIndex = 0;
        }

        private static SolidColorBrush Brush(Color color)
        {
            return new SolidColorBrush(color);
        }

        private static void ApplyToggleLanguage(ToggleSwitch toggle, bool isChinese)
        {
            toggle.OnContent = isChinese ? "开" : "On";
            toggle.OffContent = isChinese ? "关" : "Off";
        }
    }
}

using System;
using KillConfirmGameBar.Services;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace KillConfirmGameBar.Controls.GameStyles
{
    public sealed partial class Titanfall2AdvancedEffectsPanel : UserControl
    {
        private bool _suppressEvents;

        public Titanfall2AdvancedEffectsPanel()
        {
            InitializeComponent();
            AdvancedEffectsPanelSupport.TrackCombatHeader(this, CombatGameLogo, TitleText);
            StreakEditor.SettingsChanged += (s, e) => StreakModeSelectionChanged?.Invoke(this, e);
            LoadTitanfall2Settings();
        }

        public event SelectionChangedEventHandler MoneyRewardModeSelectionChanged;

        public event SelectionChangedEventHandler StreakModeSelectionChanged;

        public event RoutedEventHandler AssistAudioToggled;

        public event RoutedEventHandler SettingsReset;

        public ComboBox MoneyRewardModeSelectorControl => MoneyRewardModeSelector;

        public TextBlock MoneyRewardModeLabelControl => MoneyRewardModeLabel;

        public ComboBoxItem MoneyRewardDeltaItemControl => MoneyDeltaModeItem;

        public ComboBoxItem MoneyRewardRulesItemControl => MoneyRuleModeItem;

        public void RefreshVisualEffectSettings() => LoadTitanfall2Settings();

        public void LoadTitanfall2Settings()
        {
            KillFeedbackVisibilitySettingsValues visibility =
                KillFeedbackVisibilitySettingsStore.Load(GameStyleMode.Titanfall2);
            _suppressEvents = true;
            try
            {
                SelectTaggedItem(
                    HudLanguageSelector,
                    Titanfall2SettingsStore.LoadHudLanguage(),
                    Titanfall2SettingsStore.LanguageFollowUi);
                SelectTaggedItem(
                    ReticleStyleSelector,
                    Titanfall2SettingsStore.LoadReticleStyle(),
                    Titanfall2SettingsStore.ReticleNone);
                HitIndicatorToggle.IsChecked = Titanfall2SettingsStore.LoadHitIndicatorEnabled();
                CrosshairMedalToggle.IsChecked = Titanfall2SettingsStore.LoadCrosshairMedalEnabled();
                MeterBonusToggle.IsChecked = Titanfall2SettingsStore.LoadMeterBonusEnabled();
                UpperToggle.IsChecked = visibility.UpperEnabled;
                CrosshairToggle.IsChecked = visibility.CrosshairEnabled;
                LowerToggle.IsChecked = visibility.LowerEnabled;
            }
            finally
            {
                _suppressEvents = false;
            }
        }

        public void ApplyLanguage(bool isChinese)
        {
            AdvancedEffectsPanelSupport.ApplyCombatHeader(CombatGameLogo, TitleText, isChinese);
            ResetButton.Content = isChinese ? "恢复默认" : "Reset";
            ToolTipService.SetToolTip(
                ResetButton,
                isChinese ? "恢复泰坦陨落 2 默认设置" : "Restore Titanfall 2 defaults");
            MoneyRewardModeLabel.Text = isChinese ? "奖金计算" : "Money mode";
            MoneyDeltaModeItem.Content = isChinese ? "金钱差" : "Money delta";
            MoneyRuleModeItem.Content = isChinese ? "枪械规则" : "Weapon rules";
            StreakEditor.ApplyLanguage(isChinese);

            HudOptionsTitleText.Text = isChinese ? "铁驭 RUI 反馈细节" : "Pilot RUI HUD options";
            HudLanguageLabel.Text = isChinese ? "HUD 文本语言" : "HUD language";
            HudLangAutoItem.Content = isChinese ? "跟随界面" : "Match app UI";
            HudLangZhTwItem.Content = isChinese ? "原版繁体中文" : "Traditional Chinese";
            HudLangZhCnItem.Content = isChinese ? "简体中文" : "Simplified Chinese";
            HudLangEnItem.Content = isChinese ? "原版英文" : "English (Retail)";

            ReticleStyleLabel.Text = isChinese ? "铁驭准星样式" : "Pilot reticle";
            ReticleNoneItem.Content = isChinese ? "关闭（游戏准星）" : "Off (in-game)";
            ReticleTriItem.Content = isChinese ? "铁驭三点准星" : "Pilot tri-dot";
            ReticleWingItem.Content = isChinese ? "R-201 三翼准星" : "R-201 tri-wing";

            HitIndicatorToggle.Content = isChinese ? "准星 X 命中 / 弱点反馈" : "X hit & crit indicator";
            CrosshairMedalToggle.Content = isChinese ? "右上角连杀勋章堆栈" : "Score medal stack";
            MeterBonusToggle.Content = isChinese ? "泰坦核心槽 +20% 跳字" : "+20% Titan meter popup";
            AssistAudioToggle.Content = isChinese ? "助攻时播放音效" : "Play audio on assist";

            VisualEffectsTitleText.Text = isChinese ? "画面提示层级" : "Visual HUD layers";
            UpperToggle.Content = isChinese ? "右上角勋章堆栈" : "Top-right medal stack";
            CrosshairToggle.Content = isChinese ? "准星命中与弱点反馈" : "Crosshair hit & crit";
            LowerToggle.Content = isChinese ? "底部击杀与核心槽" : "Bottom callout & meter";
            LoadTitanfall2Settings();
        }

        internal void ApplyTheme(GameThemePalette theme)
        {
            TitleText.Foreground = Brush(theme.Text);
            AdvancedEffectsPanelSupport.ApplyResetButton(ResetButton, theme);
            MoneyRewardCard.Background = Brush(theme.Card);
            MoneyRewardCard.BorderBrush = Brush(theme.SoftBorder);
            Titanfall2HudOptionsCard.Background = Brush(theme.Card);
            Titanfall2HudOptionsCard.BorderBrush = Brush(theme.SoftBorder);
            VisualEffectsCard.Background = Brush(theme.Card);
            VisualEffectsCard.BorderBrush = Brush(theme.SoftBorder);
            HudOptionsTitleText.Foreground = Brush(theme.Text);
            VisualEffectsTitleText.Foreground = Brush(theme.Text);
            AdvancedEffectsPanelSupport.ApplyMoneyRow(MoneyRewardModeLabel, MoneyRewardModeSelector, theme);
            AdvancedEffectsPanelSupport.ApplyMoneyRow(HudLanguageLabel, HudLanguageSelector, theme);
            AdvancedEffectsPanelSupport.ApplyMoneyRow(ReticleStyleLabel, ReticleStyleSelector, theme);
            HitIndicatorToggle.Foreground = Brush(theme.Text);
            CrosshairMedalToggle.Foreground = Brush(theme.Text);
            MeterBonusToggle.Foreground = Brush(theme.Text);
            AssistAudioToggle.Foreground = Brush(theme.Text);
            UpperToggle.Foreground = Brush(theme.Text);
            CrosshairToggle.Foreground = Brush(theme.Text);
            LowerToggle.Foreground = Brush(theme.Text);
            StreakEditor.ApplyTheme(theme);
        }

        public void SelectMoneyRewardMode(string mode, string fallback)
        {
            _suppressEvents = true;
            try
            {
                SelectTaggedItem(MoneyRewardModeSelector, mode, fallback);
            }
            finally
            {
                _suppressEvents = false;
            }
        }

        public string GetSelectedMoneyRewardMode(string fallback)
        {
            return ReadTaggedItem(MoneyRewardModeSelector, fallback);
        }

        public void SelectStreakMode(string mode)
        {
            StreakEditor.SelectValue(mode);
        }

        public string GetSelectedStreakMode(string fallback)
        {
            return StreakEditor.GetValue(fallback);
        }

        public void SelectAssistAudio(bool enabled) => SetAssistAudioEnabled(enabled);

        public void SetAssistAudioEnabled(bool enabled)
        {
            _suppressEvents = true;
            try
            {
                AssistAudioToggle.IsChecked = enabled;
            }
            finally
            {
                _suppressEvents = false;
            }
        }

        public bool GetAssistAudioEnabled(bool fallback)
        {
            return AssistAudioToggle == null ? fallback : (AssistAudioToggle.IsChecked ?? fallback);
        }

        private void OnMoneyRewardModeSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_suppressEvents)
            {
                MoneyRewardModeSelectionChanged?.Invoke(this, e);
            }
        }

        private void OnHudLanguageSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressEvents) return;
            Titanfall2SettingsStore.SaveHudLanguage(
                ReadTaggedItem(HudLanguageSelector, Titanfall2SettingsStore.LanguageFollowUi));
        }

        private void OnReticleStyleSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressEvents) return;
            Titanfall2SettingsStore.SaveReticleStyle(
                ReadTaggedItem(ReticleStyleSelector, Titanfall2SettingsStore.ReticleNone));
        }

        private void OnHudOptionToggled(object sender, RoutedEventArgs e)
        {
            if (_suppressEvents) return;
            Titanfall2SettingsStore.SaveHitIndicatorEnabled(HitIndicatorToggle.IsChecked != false);
            Titanfall2SettingsStore.SaveCrosshairMedalEnabled(CrosshairMedalToggle.IsChecked != false);
            Titanfall2SettingsStore.SaveMeterBonusEnabled(MeterBonusToggle.IsChecked != false);
        }

        private void OnAssistAudioChanged(object sender, RoutedEventArgs e)
        {
            if (!_suppressEvents)
            {
                AssistAudioToggled?.Invoke(this, e);
            }
        }

        private void OnLayerVisibilityChanged(object sender, RoutedEventArgs e)
        {
            if (_suppressEvents) return;
            KillFeedbackVisibilitySettingsStore.Save(
                GameStyleMode.Titanfall2,
                new KillFeedbackVisibilitySettingsValues
                {
                    UpperEnabled = UpperToggle.IsChecked != false,
                    CrosshairEnabled = CrosshairToggle.IsChecked != false,
                    LowerEnabled = LowerToggle.IsChecked != false
                });
        }

        private void OnResetClick(object sender, RoutedEventArgs e)
        {
            Titanfall2SettingsStore.Reset();
            SelectMoneyRewardMode("delta", "delta");
            StreakEditor.SelectValue(SharedStreakSettingsStore.LifeMode);
            SetAssistAudioEnabled(false);
            KillFeedbackVisibilitySettingsStore.Save(
                GameStyleMode.Titanfall2,
                new KillFeedbackVisibilitySettingsValues
                {
                    UpperEnabled = true,
                    CrosshairEnabled = true,
                    LowerEnabled = true
                });
            LoadTitanfall2Settings();
            OnAssistAudioChanged(this, null);
            MoneyRewardModeSelectionChanged?.Invoke(this, null);
            StreakModeSelectionChanged?.Invoke(this, null);
            SettingsReset?.Invoke(this, e);
        }

        private static string ReadTaggedItem(ComboBox selector, string fallback)
        {
            return selector != null
                && selector.SelectedItem is ComboBoxItem item
                && item.Tag is string tag
                && !string.IsNullOrWhiteSpace(tag)
                    ? tag
                    : fallback;
        }

        private static void SelectTaggedItem(ComboBox selector, string value, string fallback)
        {
            if (selector == null) return;
            string target = string.IsNullOrWhiteSpace(value) ? fallback : value;
            foreach (object option in selector.Items)
            {
                if (option is ComboBoxItem item
                    && string.Equals(item.Tag != null ? item.Tag.ToString() : null, target, StringComparison.OrdinalIgnoreCase))
                {
                    selector.SelectedItem = item;
                    return;
                }
            }

            if (selector.Items.Count > 0)
            {
                selector.SelectedIndex = 0;
            }
        }

        private static SolidColorBrush Brush(Color color)
        {
            return new SolidColorBrush(color);
        }
    }
}

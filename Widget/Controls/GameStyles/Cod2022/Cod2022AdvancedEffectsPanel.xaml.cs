using System;
using KillConfirmGameBar.Services;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace KillConfirmGameBar.Controls.GameStyles
{
    public sealed partial class Cod2022AdvancedEffectsPanel : UserControl
    {
        private bool _suppressEvents;

        public Cod2022AdvancedEffectsPanel()
        {
            InitializeComponent();
            AdvancedEffectsPanelSupport.TrackCombatHeader(this, CombatGameLogo, TitleText);
            StreakEditor.SettingsChanged += (s, e) => StreakModeSelectionChanged?.Invoke(this, e);
            LoadCod2022Settings();
        }

        public event SelectionChangedEventHandler MoneyRewardModeSelectionChanged;

        public event SelectionChangedEventHandler StreakModeSelectionChanged;

        public event RoutedEventHandler AssistAudioToggled;

        public event RoutedEventHandler SettingsReset;

        public ComboBox MoneyRewardModeSelectorControl => MoneyRewardModeSelector;

        public TextBlock MoneyRewardModeLabelControl => MoneyRewardModeLabel;

        public ComboBoxItem MoneyRewardDeltaItemControl => MoneyDeltaModeItem;

        public ComboBoxItem MoneyRewardRulesItemControl => MoneyRuleModeItem;

        public void RefreshVisualEffectSettings() => LoadCod2022Settings();

        public void LoadCod2022Settings()
        {
            KillFeedbackVisibilitySettingsValues visibility =
                KillFeedbackVisibilitySettingsStore.Load(GameStyleMode.Cod2022);
            _suppressEvents = true;
            try
            {
                SelectTaggedItem(
                    HudLanguageSelector,
                    Cod2022SettingsStore.LoadHudLanguage(),
                    Cod2022SettingsStore.LanguageFollowUi);
                SelectTaggedItem(
                    HitmarkerSizeSelector,
                    Cod2022SettingsStore.LoadHitmarkerSize(),
                    Cod2022SettingsStore.HitmarkerMedium);
                SelectTaggedItem(
                    PointsModeSelector,
                    Cod2022SettingsStore.LoadPointsDisplayMode(),
                    Cod2022SettingsStore.PointsXp);
                SelectTaggedItem(
                    LowerStyleSelector,
                    Cod2022SettingsStore.LoadLowerStyle(),
                    Cod2022SettingsStore.LowerNameplate);

                DecodeScrambleToggle.IsChecked = Cod2022SettingsStore.LoadDecodeScrambleEnabled();
                SplashIconToggle.IsChecked = Cod2022SettingsStore.LoadSplashIconEnabled();
                SplashAllKillsToggle.IsChecked = Cod2022SettingsStore.LoadSplashMode() == Cod2022SettingsStore.SplashAllKills;

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
                isChinese ? "恢复现代战争 II 默认设置" : "Restore Modern Warfare II defaults");
            MoneyRewardModeLabel.Text = isChinese ? "奖金计算" : "Money mode";
            MoneyDeltaModeItem.Content = isChinese ? "金钱差" : "Money delta";
            MoneyRuleModeItem.Content = isChinese ? "枪械规则" : "Weapon rules";
            StreakEditor.ApplyLanguage(isChinese);

            HudOptionsTitleText.Text = isChinese ? "现代战争 II (IW9) 战术 HUD 细节" : "MWII (IW9) LUI HUD options";
            HudLanguageLabel.Text = isChinese ? "HUD 词条语言" : "HUD language";
            HudLangAutoItem.Content = isChinese ? "跟随界面" : "Match app UI";
            HudLangZhCnItem.Content = isChinese ? "官方简体中文" : "Simplified Chinese";
            HudLangEnItem.Content = isChinese ? "原版英文 (IW9)" : "English (IW9)";

            HitmarkerSizeLabel.Text = isChinese ? "准星命中尺寸" : "Hitmarker size";
            HitmarkerMediumItem.Content = isChinese ? "标准 (270ms)" : "Medium (270ms)";
            HitmarkerLargeItem.Content = isChinese ? "重击放大 (600ms)" : "Large (600ms)";
            HitmarkerSmallItem.Content = isChinese ? "紧凑硬核" : "Small (Compact)";

            PointsModeLabel.Text = isChinese ? "+XP 跳字模式" : "Points popup";
            PointsXpItem.Content = isChinese ? "原版 +100 XP" : "Native +100 XP";
            PointsRewardItem.Content = isChinese ? "实战赏金数值" : "Match money reward";
            PointsOffItem.Content = isChinese ? "关闭跳字" : "Off";

            LowerStyleLabel.Text = isChinese ? "下方确认样式" : "Lower banner style";
            LowerNameplateItem.Content = isChinese ? "红底铭牌 + 骷髅标" : "Red nameplate + skull";
            LowerKilledMsgItem.Content = isChinese ? "KILLED 渐变横幅" : "KILLED gradient bar";

            DecodeScrambleToggle.Content = isChinese ? "IW9 乱码解码特效" : "IW9 decode scramble";
            SplashIconToggle.Content = isChinese ? "勋章准星内嵌图标" : "Splash reticle icon";
            SplashAllKillsToggle.Content = isChinese ? "普通单杀也弹上方横幅" : "Splash on plain kills";
            AssistAudioToggle.Content = isChinese ? "助攻时播放音效" : "Play audio on assist";

            VisualEffectsTitleText.Text = isChinese ? "画面提示层级" : "Visual HUD layers";
            UpperToggle.Content = isChinese ? "上方切角勋章横幅" : "Upper splash banner";
            CrosshairToggle.Content = isChinese ? "准星命中与+XP词条" : "Crosshair & +XP feed";
            LowerToggle.Content = isChinese ? "下方淘汰确认铭牌" : "Lower kill nameplate";
            LoadCod2022Settings();
        }

        internal void ApplyTheme(GameThemePalette theme)
        {
            TitleText.Foreground = Brush(theme.Text);
            AdvancedEffectsPanelSupport.ApplyResetButton(ResetButton, theme);
            MoneyRewardCard.Background = Brush(theme.Card);
            MoneyRewardCard.BorderBrush = Brush(theme.SoftBorder);
            Cod2022HudOptionsCard.Background = Brush(theme.Card);
            Cod2022HudOptionsCard.BorderBrush = Brush(theme.SoftBorder);
            VisualEffectsCard.Background = Brush(theme.Card);
            VisualEffectsCard.BorderBrush = Brush(theme.SoftBorder);
            HudOptionsTitleText.Foreground = Brush(theme.Text);
            VisualEffectsTitleText.Foreground = Brush(theme.Text);
            AdvancedEffectsPanelSupport.ApplyMoneyRow(MoneyRewardModeLabel, MoneyRewardModeSelector, theme);
            AdvancedEffectsPanelSupport.ApplyMoneyRow(HudLanguageLabel, HudLanguageSelector, theme);
            AdvancedEffectsPanelSupport.ApplyMoneyRow(HitmarkerSizeLabel, HitmarkerSizeSelector, theme);
            AdvancedEffectsPanelSupport.ApplyMoneyRow(PointsModeLabel, PointsModeSelector, theme);
            AdvancedEffectsPanelSupport.ApplyMoneyRow(LowerStyleLabel, LowerStyleSelector, theme);
            DecodeScrambleToggle.Foreground = Brush(theme.Text);
            SplashIconToggle.Foreground = Brush(theme.Text);
            SplashAllKillsToggle.Foreground = Brush(theme.Text);
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

        private void OnHudComboSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressEvents) return;
            Cod2022SettingsStore.SaveHudLanguage(
                ReadTaggedItem(HudLanguageSelector, Cod2022SettingsStore.LanguageFollowUi));
            Cod2022SettingsStore.SaveHitmarkerSize(
                ReadTaggedItem(HitmarkerSizeSelector, Cod2022SettingsStore.HitmarkerMedium));
            Cod2022SettingsStore.SavePointsDisplayMode(
                ReadTaggedItem(PointsModeSelector, Cod2022SettingsStore.PointsXp));
            Cod2022SettingsStore.SaveLowerStyle(
                ReadTaggedItem(LowerStyleSelector, Cod2022SettingsStore.LowerNameplate));
        }

        private void OnHudOptionToggled(object sender, RoutedEventArgs e)
        {
            if (_suppressEvents) return;
            Cod2022SettingsStore.SaveDecodeScrambleEnabled(DecodeScrambleToggle.IsChecked != false);
            Cod2022SettingsStore.SaveSplashIconEnabled(SplashIconToggle.IsChecked != false);
            Cod2022SettingsStore.SaveSplashMode(
                SplashAllKillsToggle.IsChecked == true
                    ? Cod2022SettingsStore.SplashAllKills
                    : Cod2022SettingsStore.SplashSpecialOnly);
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
                GameStyleMode.Cod2022,
                new KillFeedbackVisibilitySettingsValues
                {
                    UpperEnabled = UpperToggle.IsChecked != false,
                    CrosshairEnabled = CrosshairToggle.IsChecked != false,
                    LowerEnabled = LowerToggle.IsChecked != false
                });
        }

        private void OnResetClick(object sender, RoutedEventArgs e)
        {
            Cod2022SettingsStore.Reset();
            SelectMoneyRewardMode("delta", "delta");
            StreakEditor.SelectValue(SharedStreakSettingsStore.LifeMode);
            SetAssistAudioEnabled(false);
            KillFeedbackVisibilitySettingsStore.Save(
                GameStyleMode.Cod2022,
                new KillFeedbackVisibilitySettingsValues
                {
                    UpperEnabled = true,
                    CrosshairEnabled = true,
                    LowerEnabled = true
                });
            LoadCod2022Settings();
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

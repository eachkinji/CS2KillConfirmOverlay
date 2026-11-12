using System;
using KillConfirmGameBar.Services;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace KillConfirmGameBar.Features.CompatibilityDisplay.Controls.GameStyles
{
    public sealed partial class Rdr2AdvancedEffectsPanel : UserControl
    {
        private bool _suppressEvents;

        public Rdr2AdvancedEffectsPanel()
        {
            InitializeComponent();
            AdvancedEffectsPanelSupport.TrackCombatHeader(this, CombatGameLogo, TitleText);
            StreakEditor.SettingsChanged += (s, e) => StreakModeSelectionChanged?.Invoke(this, e);
            LoadRdr2Settings();
        }

        public event SelectionChangedEventHandler MoneyRewardModeSelectionChanged;

        public event SelectionChangedEventHandler StreakModeSelectionChanged;

        public event RoutedEventHandler AssistAudioToggled;

        public event RoutedEventHandler SettingsReset;

        public ComboBox MoneyRewardModeSelectorControl => MoneyRewardModeSelector;

        public TextBlock MoneyRewardModeLabelControl => MoneyRewardModeLabel;

        public ComboBoxItem MoneyRewardDeltaItemControl => MoneyDeltaModeItem;

        public ComboBoxItem MoneyRewardRulesItemControl => MoneyRuleModeItem;

        public void RefreshVisualEffectSettings() => LoadRdr2Settings();

        public void LoadRdr2Settings()
        {
            KillFeedbackVisibilitySettingsValues visibility =
                CompatibilityAppearanceStore.Load(GameStyleMode.Rdr2);
            _suppressEvents = true;
            try
            {
                SelectTaggedItem(
                    HudLanguageSelector,
                    Rdr2SettingsStore.LoadHudLanguage(),
                    Rdr2SettingsStore.LanguageFollowUi);
                SelectTaggedItem(
                    HonorModeSelector,
                    Rdr2SettingsStore.LoadHonorMode(),
                    Rdr2SettingsStore.HonorDynamic);
                SelectTaggedItem(
                    ReticleStyleSelector,
                    Rdr2SettingsStore.LoadReticleStyle(),
                    Rdr2SettingsStore.ReticleScaleform);
                SelectTaggedItem(
                    BountyModeSelector,
                    Rdr2SettingsStore.LoadBountyDisplayMode(),
                    Rdr2SettingsStore.BountyXp);

                AccuracyCircleToggle.IsChecked = Rdr2SettingsStore.LoadAccuracyCircleEnabled();
                HonorPopupToggle.IsChecked = Rdr2SettingsStore.LoadHonorPopupEnabled();
                LowerCaptionToggle.IsChecked = Rdr2SettingsStore.LoadLowerCaptionEnabled();

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
                isChinese ? "恢复荒野大镖客 2 默认设置" : "Restore Red Dead Redemption 2 defaults");
            MoneyRewardModeLabel.Text = isChinese ? "奖金计算" : "Money mode";
            MoneyDeltaModeItem.Content = isChinese ? "金钱差" : "Money delta";
            MoneyRuleModeItem.Content = isChinese ? "枪械规则" : "Weapon rules";
            StreakEditor.ApplyLanguage(isChinese);

            HudOptionsTitleText.Text = isChinese ? "荒野大镖客 2 · 善恶荣誉与死神之眼细节" : "RDR2 Honor & Dead Eye HUD options";
            HudLanguageLabel.Text = isChinese ? "HUD 词条语言" : "HUD language";
            HudLangAutoItem.Content = isChinese ? "跟随界面" : "Match app UI";
            HudLangZhCnItem.Content = isChinese ? "西部中文" : "Simplified Chinese";
            HudLangEnItem.Content = isChinese ? "原版英文 (RDR2)" : "English (RDR2)";

            HonorModeLabel.Text = isChinese ? "善恶荣誉推演" : "Honor morality";
            HonorDynamicItem.Content = isChinese ? "动态善恶推演" : "Dynamic morality";
            HonorHighItem.Content = isChinese ? "义警高荣誉 (+牛仔)" : "High Honor (+Cowboy)";
            HonorLowItem.Content = isChinese ? "亡命徒低荣誉 (-骷髅)" : "Low Honor (-Outlaw)";

            ReticleStyleLabel.Text = isChinese ? "准星命中表现" : "Reticle hitmarker";
            ReticleScaleformItem.Content = isChinese ? "原版 + 死神之眼红叉" : "Scaleform + Dead Eye X";
            ReticleDeadEyeItem.Content = isChinese ? "常驻死神之眼手绘红叉" : "Always Dead Eye red X";
            ReticleMinimalItem.Content = isChinese ? "精简四臂准星" : "Minimal 4-arm X";

            BountyModeLabel.Text = isChinese ? "右侧赏金模式" : "Right bounty display";
            BountyXpItem.Content = isChinese ? "线上 +XP 经验词条" : "RDO +XP feed";
            BountyCashItem.Content = isChinese ? "西部 +$ 美金悬赏" : "Western +$ cash";
            BountyOffItem.Content = isChinese ? "仅显示西部词条" : "Action tags only";

            AccuracyCircleToggle.Content = isChinese ? "准星精度扩散白环" : "Accuracy bloom circle";
            HonorPopupToggle.Content = isChinese ? "右侧善恶牛仔/骷髅徽章" : "Right Honor badge";
            LowerCaptionToggle.Content = isChinese ? "荣誉条上方衬线播报" : "Lower serif caption";
            AssistAudioToggle.Content = isChinese ? "助攻时播放音效" : "Play audio on assist";

            VisualEffectsTitleText.Text = isChinese ? "画面提示层级" : "Visual HUD layers";
            UpperToggle.Content = isChinese ? "右侧荣誉与赏金栈" : "Right Honor & XP";
            CrosshairToggle.Content = isChinese ? "准星与死神之眼" : "Crosshair & Dead Eye";
            LowerToggle.Content = isChinese ? "底部16档荣誉条" : "Bottom 16-state Honor";
            LoadRdr2Settings();
        }

        internal void ApplyTheme(GameThemePalette theme)
        {
            TitleText.Foreground = Brush(theme.Text);
            AdvancedEffectsPanelSupport.ApplyResetButton(ResetButton, theme);
            MoneyRewardCard.Background = Brush(theme.Card);
            MoneyRewardCard.BorderBrush = Brush(theme.SoftBorder);
            Rdr2HudOptionsCard.Background = Brush(theme.Card);
            Rdr2HudOptionsCard.BorderBrush = Brush(theme.SoftBorder);
            VisualEffectsCard.Background = Brush(theme.Card);
            VisualEffectsCard.BorderBrush = Brush(theme.SoftBorder);
            HudOptionsTitleText.Foreground = Brush(theme.Text);
            VisualEffectsTitleText.Foreground = Brush(theme.Text);
            AdvancedEffectsPanelSupport.ApplyMoneyRow(MoneyRewardModeLabel, MoneyRewardModeSelector, theme);
            AdvancedEffectsPanelSupport.ApplyMoneyRow(HudLanguageLabel, HudLanguageSelector, theme);
            AdvancedEffectsPanelSupport.ApplyMoneyRow(HonorModeLabel, HonorModeSelector, theme);
            AdvancedEffectsPanelSupport.ApplyMoneyRow(ReticleStyleLabel, ReticleStyleSelector, theme);
            AdvancedEffectsPanelSupport.ApplyMoneyRow(BountyModeLabel, BountyModeSelector, theme);
            AccuracyCircleToggle.Foreground = Brush(theme.Text);
            HonorPopupToggle.Foreground = Brush(theme.Text);
            LowerCaptionToggle.Foreground = Brush(theme.Text);
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
            Rdr2SettingsStore.SaveHudLanguage(
                ReadTaggedItem(HudLanguageSelector, Rdr2SettingsStore.LanguageFollowUi));
            Rdr2SettingsStore.SaveHonorMode(
                ReadTaggedItem(HonorModeSelector, Rdr2SettingsStore.HonorDynamic));
            Rdr2SettingsStore.SaveReticleStyle(
                ReadTaggedItem(ReticleStyleSelector, Rdr2SettingsStore.ReticleScaleform));
            Rdr2SettingsStore.SaveBountyDisplayMode(
                ReadTaggedItem(BountyModeSelector, Rdr2SettingsStore.BountyXp));
        }

        private void OnHudOptionToggled(object sender, RoutedEventArgs e)
        {
            if (_suppressEvents) return;
            Rdr2SettingsStore.SaveAccuracyCircleEnabled(AccuracyCircleToggle.IsChecked != false);
            Rdr2SettingsStore.SaveHonorPopupEnabled(HonorPopupToggle.IsChecked != false);
            Rdr2SettingsStore.SaveLowerCaptionEnabled(LowerCaptionToggle.IsChecked != false);
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
            CompatibilityAppearanceStore.Save(
                GameStyleMode.Rdr2,
                new KillFeedbackVisibilitySettingsValues
                {
                    UpperEnabled = UpperToggle.IsChecked != false,
                    CrosshairEnabled = CrosshairToggle.IsChecked != false,
                    LowerEnabled = LowerToggle.IsChecked != false
                });
        }

        private void OnResetClick(object sender, RoutedEventArgs e)
        {
            Rdr2SettingsStore.Reset();
            SelectMoneyRewardMode("delta", "delta");
            StreakEditor.SelectValue(SharedStreakSettingsStore.LifeMode);
            SetAssistAudioEnabled(false);
            CompatibilityAppearanceStore.Save(
                GameStyleMode.Rdr2,
                new KillFeedbackVisibilitySettingsValues
                {
                    UpperEnabled = true,
                    CrosshairEnabled = true,
                    LowerEnabled = true
                });
            LoadRdr2Settings();
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

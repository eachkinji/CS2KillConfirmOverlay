using System;
using KillConfirmGameBar.Services;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace KillConfirmGameBar.Controls.GameStyles
{
    public sealed partial class WarThunderAdvancedEffectsPanel : UserControl
    {
        private bool _suppressEvents;

        public WarThunderAdvancedEffectsPanel()
        {
            InitializeComponent();
            AdvancedEffectsPanelSupport.TrackCombatHeader(this, CombatGameLogo, TitleText);
            StreakEditor.SettingsChanged += (s, e) => StreakModeSelectionChanged?.Invoke(this, e);
            LoadWarThunderSettings();
        }

        public event SelectionChangedEventHandler MoneyRewardModeSelectionChanged;

        public event SelectionChangedEventHandler StreakModeSelectionChanged;

        public event RoutedEventHandler AssistAudioToggled;

        public event RoutedEventHandler SettingsReset;

        public ComboBox MoneyRewardModeSelectorControl => MoneyRewardModeSelector;

        public TextBlock MoneyRewardModeLabelControl => MoneyRewardModeLabel;

        public ComboBoxItem MoneyRewardDeltaItemControl => MoneyDeltaModeItem;

        public ComboBoxItem MoneyRewardRulesItemControl => MoneyRuleModeItem;

        public void RefreshVisualEffectSettings() => LoadWarThunderSettings();

        public void LoadWarThunderSettings()
        {
            KillFeedbackVisibilitySettingsValues visibility =
                KillFeedbackVisibilitySettingsStore.Load(GameStyleMode.WarThunder);
            _suppressEvents = true;
            try
            {
                SelectTaggedItem(
                    BattleModeSelector,
                    WarThunderSettingsStore.LoadBattleMode(),
                    WarThunderSettingsStore.ModeTank);
                SelectTaggedItem(
                    HudLanguageSelector,
                    WarThunderSettingsStore.LoadHudLanguage(),
                    WarThunderSettingsStore.LanguageFollowUi);
                SelectTaggedItem(
                    LowerDisplayModeSelector,
                    WarThunderSettingsStore.LoadLowerDisplayMode(),
                    WarThunderSettingsStore.LowerXRayAndLog);
                SelectTaggedItem(
                    CrosshairStyleSelector,
                    WarThunderSettingsStore.LoadCrosshairStyle(),
                    WarThunderSettingsStore.CrosshairMinimal);

                DamagedPartsToggle.IsChecked = WarThunderSettingsStore.LoadDamagedPartsEnabled();
                WorldKillMarkToggle.IsChecked = WarThunderSettingsStore.LoadWorldKillMarkEnabled();
                StreakAllKillsToggle.IsChecked = WarThunderSettingsStore.LoadStreakAllKillsEnabled();

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
                isChinese ? "恢复战争雷霆默认设置" : "Restore War Thunder defaults");
            MoneyRewardModeLabel.Text = isChinese ? "奖励核算" : "Economy mode";
            MoneyDeltaModeItem.Content = isChinese ? "金钱差" : "Money delta";
            MoneyRuleModeItem.Content = isChinese ? "枪械规则" : "Weapon rules";
            StreakEditor.ApplyLanguage(isChinese);

            HudOptionsTitleText.Text = isChinese ? "战争雷霆 · 兵种载具与 X-Ray 毁伤细节" : "War Thunder Vehicle & X-Ray HitCamera options";
            BattleModeLabel.Text = isChinese ? "兵种战斗模式" : "Combat branch";
            BattleModeTankItem.Content = isChinese ? "陆战历史 (主战坦克)" : "Ground RB (Tank)";
            BattleModeAirItem.Content = isChinese ? "空战历史 (喷气战机)" : "Air RB (Fighter)";
            BattleModeShipItem.Content = isChinese ? "海战历史 (战列巨舰)" : "Naval RB (Warship)";
            BattleModeInfantryItem.Content = isChinese ? "步兵突击 (战术小队)" : "Infantry Squad";

            HudLanguageLabel.Text = isChinese ? "HUD 词条语言" : "HUD language";
            HudLangAutoItem.Content = isChinese ? "跟随界面" : "Match app UI";
            HudLangZhCnItem.Content = isChinese ? "官方简中 (摧毁单位)" : "Simplified Chinese";
            HudLangEnItem.Content = isChinese ? "原版英文 (Target destroyed)" : "English (Original)";

            LowerDisplayModeLabel.Text = isChinese ? "战术副图层内容" : "Tactical lower content";
            LowerXRayAndLogItem.Content = isChinese ? "X-Ray 毁伤相机 + 战报" : "X-Ray HitCam + Kill Log";
            LowerXRayOnlyItem.Content = isChinese ? "仅 X-Ray 穿甲毁伤相机" : "X-Ray HitCamera only";
            LowerLogOnlyItem.Content = isChinese ? "仅阵营色击杀战报" : "Team Kill Log only";

            CrosshairStyleLabel.Text = isChinese ? "准星分划样式" : "Sight reticle style";
            CrosshairMinimalItem.Content = isChinese ? "精简 45° 四翼命中标" : "Minimal 45° hitmarks";
            CrosshairReticleItem.Content = isChinese ? "坦克测距分划环 + 四翼" : "Tank rangefinder + wings";

            DamagedPartsToggle.Content = isChinese ? "显示受损内构部位列表" : "Show damaged internal parts";
            WorldKillMarkToggle.Content = isChinese ? "准星 3D 击杀标上浮" : "Floating 3D world killmark";
            StreakAllKillsToggle.Content = isChinese ? "单杀也弹出兵种战功章" : "Show medal on single kills";
            AssistAudioToggle.Content = isChinese ? "助攻时播放音效" : "Play audio on assist";

            VisualEffectsTitleText.Text = isChinese ? "画面提示层级" : "Visual HUD layers";
            UpperToggle.Content = isChinese ? "上方勋章与红光奖励" : "Top medals & reward";
            CrosshairToggle.Content = isChinese ? "准星四翼与击杀标" : "Crosshair & killmark";
            LowerToggle.Content = isChinese ? "X-Ray 毁伤与战报" : "X-Ray cam & kill log";
            LoadWarThunderSettings();
        }

        internal void ApplyTheme(GameThemePalette theme)
        {
            TitleText.Foreground = Brush(theme.Text);
            AdvancedEffectsPanelSupport.ApplyResetButton(ResetButton, theme);
            MoneyRewardCard.Background = Brush(theme.Card);
            MoneyRewardCard.BorderBrush = Brush(theme.SoftBorder);
            WarThunderHudOptionsCard.Background = Brush(theme.Card);
            WarThunderHudOptionsCard.BorderBrush = Brush(theme.SoftBorder);
            VisualEffectsCard.Background = Brush(theme.Card);
            VisualEffectsCard.BorderBrush = Brush(theme.SoftBorder);
            HudOptionsTitleText.Foreground = Brush(theme.Text);
            VisualEffectsTitleText.Foreground = Brush(theme.Text);
            AdvancedEffectsPanelSupport.ApplyMoneyRow(MoneyRewardModeLabel, MoneyRewardModeSelector, theme);
            AdvancedEffectsPanelSupport.ApplyMoneyRow(BattleModeLabel, BattleModeSelector, theme);
            AdvancedEffectsPanelSupport.ApplyMoneyRow(HudLanguageLabel, HudLanguageSelector, theme);
            AdvancedEffectsPanelSupport.ApplyMoneyRow(LowerDisplayModeLabel, LowerDisplayModeSelector, theme);
            AdvancedEffectsPanelSupport.ApplyMoneyRow(CrosshairStyleLabel, CrosshairStyleSelector, theme);
            DamagedPartsToggle.Foreground = Brush(theme.Text);
            WorldKillMarkToggle.Foreground = Brush(theme.Text);
            StreakAllKillsToggle.Foreground = Brush(theme.Text);
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
            WarThunderSettingsStore.SaveBattleMode(
                ReadTaggedItem(BattleModeSelector, WarThunderSettingsStore.ModeTank));
            WarThunderSettingsStore.SaveHudLanguage(
                ReadTaggedItem(HudLanguageSelector, WarThunderSettingsStore.LanguageFollowUi));
            WarThunderSettingsStore.SaveLowerDisplayMode(
                ReadTaggedItem(LowerDisplayModeSelector, WarThunderSettingsStore.LowerXRayAndLog));
            WarThunderSettingsStore.SaveCrosshairStyle(
                ReadTaggedItem(CrosshairStyleSelector, WarThunderSettingsStore.CrosshairMinimal));
        }

        private void OnHudOptionToggled(object sender, RoutedEventArgs e)
        {
            if (_suppressEvents) return;
            WarThunderSettingsStore.SaveDamagedPartsEnabled(DamagedPartsToggle.IsChecked != false);
            WarThunderSettingsStore.SaveWorldKillMarkEnabled(WorldKillMarkToggle.IsChecked != false);
            WarThunderSettingsStore.SaveStreakAllKillsEnabled(StreakAllKillsToggle.IsChecked == true);
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
                GameStyleMode.WarThunder,
                new KillFeedbackVisibilitySettingsValues
                {
                    UpperEnabled = UpperToggle.IsChecked != false,
                    CrosshairEnabled = CrosshairToggle.IsChecked != false,
                    LowerEnabled = LowerToggle.IsChecked != false
                });
        }

        private void OnResetClick(object sender, RoutedEventArgs e)
        {
            WarThunderSettingsStore.Reset();
            SelectMoneyRewardMode("delta", "delta");
            StreakEditor.SelectValue(SharedStreakSettingsStore.LifeMode);
            SetAssistAudioEnabled(true);
            KillFeedbackVisibilitySettingsStore.Save(
                GameStyleMode.WarThunder,
                new KillFeedbackVisibilitySettingsValues
                {
                    UpperEnabled = true,
                    CrosshairEnabled = true,
                    LowerEnabled = true
                });
            LoadWarThunderSettings();
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

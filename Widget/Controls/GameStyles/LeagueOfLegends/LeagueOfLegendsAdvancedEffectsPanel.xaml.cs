using System;
using KillConfirmGameBar.Services;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace KillConfirmGameBar.Controls.GameStyles
{
    public sealed partial class LeagueOfLegendsAdvancedEffectsPanel : UserControl
    {
        private bool _suppressEvents;

        public LeagueOfLegendsAdvancedEffectsPanel()
        {
            InitializeComponent();
            AdvancedEffectsPanelSupport.TrackCombatHeader(this, CombatGameLogo, TitleText);
            StreakEditor.SettingsChanged += (s, e) => StreakModeSelectionChanged?.Invoke(this, e);
            LoadLeagueOfLegendsSettings();
        }

        public event SelectionChangedEventHandler MoneyRewardModeSelectionChanged;

        public event SelectionChangedEventHandler StreakModeSelectionChanged;

        public event RoutedEventHandler AssistAudioToggled;

        public event RoutedEventHandler SettingsReset;

        public ComboBox MoneyRewardModeSelectorControl => MoneyRewardModeSelector;

        public TextBlock MoneyRewardModeLabelControl => MoneyRewardModeLabel;

        public ComboBoxItem MoneyRewardDeltaItemControl => MoneyDeltaModeItem;

        public ComboBoxItem MoneyRewardRulesItemControl => MoneyRuleModeItem;

        public void RefreshVisualEffectSettings() => LoadLeagueOfLegendsSettings();

        public void LoadLeagueOfLegendsSettings()
        {
            KillFeedbackVisibilitySettingsValues visibility =
                KillFeedbackVisibilitySettingsStore.Load(GameStyleMode.LeagueOfLegends);
            _suppressEvents = true;
            try
            {
                SelectTaggedItem(
                    LanguageSelector,
                    LeagueOfLegendsSettingsStore.LoadHudLanguage(),
                    LeagueOfLegendsSettingsStore.LanguageFollowUi);
                SelectTaggedItem(
                    TeamThemeSelector,
                    LeagueOfLegendsSettingsStore.LoadTeamTheme(),
                    LeagueOfLegendsSettingsStore.TeamBlue);
                SelectTaggedItem(
                    ActorTextSelector,
                    LeagueOfLegendsSettingsStore.LoadActorTextMode(),
                    LeagueOfLegendsSettingsStore.ActorYou);
                SelectTaggedItem(
                    DefaultChampionSelector,
                    LeagueOfLegendsSettingsStore.LoadDefaultChampion(),
                    LeagueOfLegendsSettingsStore.BuiltInChampions[0]);
                SelectTaggedItem(
                    BannerModeSelector,
                    LeagueOfLegendsSettingsStore.LoadBannerMode(),
                    LeagueOfLegendsSettingsStore.BannerAllKills);
                SelectTaggedItem(
                    CsStreakModeSelector,
                    LeagueOfLegendsSettingsStore.LoadCsStreakMode(),
                    LeagueOfLegendsSettingsStore.StreakCombined);

                TextFlankingToggle.IsChecked = LeagueOfLegendsSettingsStore.LoadTextFlankingEnabled();
                PulseVfxToggle.IsChecked = LeagueOfLegendsSettingsStore.LoadPulseVfxEnabled();
                EnemyEventsToggle.IsChecked = LeagueOfLegendsSettingsStore.LoadEnemyEventsEnabled();

                UpperToggle.IsChecked = visibility.UpperEnabled;
                LowerToggle.IsChecked = visibility.LowerEnabled;
                CrosshairToggle.IsChecked = visibility.CrosshairEnabled;
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
                isChinese ? "恢复英雄联盟默认设置" : "Restore League of Legends defaults");
            MoneyRewardModeLabel.Text = isChinese ? "赏金计算" : "Gold bounty";
            MoneyDeltaModeItem.Content = isChinese ? "金钱差 / 真实赏金" : "Live gold delta";
            MoneyRuleModeItem.Content = isChinese ? "+300g 标准规则" : "+300g standard";
            StreakEditor.ApplyLanguage(isChinese);

            HudOptionsTitleText.Text = isChinese
                ? "英雄联盟海克斯横幅与右侧击杀通告细节"
                : "League of Legends Hextech Banner & Callout";
            LanguageLabel.Text = isChinese ? "横幅播报语言" : "Banner language";
            LangAutoItem.Content = isChinese ? "跟随界面" : "Match app UI";
            LangZhItem.Content = isChinese ? "国服简体中文" : "Simplified Chinese";
            LangEnItem.Content = isChinese ? "美服英文原版" : "English (Original)";

            TeamThemeLabel.Text = isChinese ? "阵营配色主题" : "Team palette";
            ThemeBlueItem.Content = isChinese ? "秩序蓝方 (友蓝/敌红)" : "Order Blue";
            ThemeRedItem.Content = isChinese ? "混沌红方 (猩红竞技)" : "Chaos Red";
            ThemeGoldItem.Content = isChinese ? "海克斯鎏金 (至臻)" : "Prestige Gold";

            ActorTextLabel.Text = isChinese ? "横幅副标题" : "Subtitle actor";
            ActorYouItem.Content = isChinese ? "第一人称 (你 击杀了…)" : "First-person (You…)";
            ActorChampionItem.Content = isChinese ? "英雄名 (亚索 击杀了…)" : "Champion name";

            DefaultChampionLabel.Text = isChinese ? "CS 默认英雄头像" : "Default CS champion";
            ChampYasuoItem.Content = isChinese ? "疾风剑豪 · 亚索" : "Yasuo";
            ChampYoneItem.Content = isChinese ? "封魔剑魂 · 永恩" : "Yone";
            ChampZedItem.Content = isChinese ? "影流之主 · 劫" : "Zed";
            ChampAhriItem.Content = isChinese ? "九尾妖狐 · 阿狸" : "Ahri";
            ChampJinxItem.Content = isChinese ? "暴走萝莉 · 金克丝" : "Jinx";
            ChampLeeSinItem.Content = isChinese ? "盲僧 · 李青" : "Lee Sin";
            ChampDariusItem.Content = isChinese ? "诺克萨斯之手 · 德莱厄斯" : "Darius";
            ChampVayneItem.Content = isChinese ? "暗夜猎手 · 薇恩" : "Vayne";
            ChampEzrealItem.Content = isChinese ? "探险家 · 伊泽瑞尔" : "Ezreal";
            ChampLuxItem.Content = isChinese ? "光辉女郎 · 拉克丝" : "Lux";

            BannerModeLabel.Text = isChinese ? "上方横幅范围" : "Upper banner scope";
            BannerAllItem.Content = isChinese ? "全部击杀与史诗目标" : "All kills & objectives";
            BannerHighlightsItem.Content = isChinese ? "仅一血/多杀/终结/团灭" : "Highlights only";

            CsStreakModeLabel.Text = isChinese ? "CS 连杀晋升" : "CS streak mode";
            CsStreakCombinedItem.Content = isChinese ? "智能双轨 (多杀+称号)" : "Dual-track (Multi+Spree)";
            CsStreakMultikillItem.Content = isChinese ? "仅多杀纹章 (双杀~五杀)" : "Multi-kills only";
            CsStreakSpreeItem.Content = isChinese ? "仅连杀称号 (大杀~超神)" : "Killing sprees only";

            TextFlankingToggle.Content = isChinese ? "双头像动态夹持文字" : "Dynamic two-icon flanking";
            PulseVfxToggle.Content = isChinese ? "右侧海克斯双环脉冲" : "Callout Hextech PulseVFX";
            EnemyEventsToggle.Content = isChinese ? "显示阵亡与处决通告" : "Show death & executed";
            AssistAudioToggle.Content = isChinese ? "助攻时播放音效" : "Play audio on assist";

            VisualEffectsTitleText.Text = isChinese ? "画面提示层级" : "Visual HUD layers";
            UpperToggle.Content = isChinese ? "上方海克斯播报横幅" : "Upper Hextech banner";
            LowerToggle.Content = isChinese ? "右侧四槽英雄通告栏" : "Right 4-slot callout";
            CrosshairToggle.Content = isChinese ? "准星命中与+300g跳字" : "Crosshair & +300g";
            LoadLeagueOfLegendsSettings();
        }

        internal void ApplyTheme(GameThemePalette theme)
        {
            TitleText.Foreground = Brush(theme.Text);
            AdvancedEffectsPanelSupport.ApplyResetButton(ResetButton, theme);
            MoneyRewardCard.Background = Brush(theme.Card);
            MoneyRewardCard.BorderBrush = Brush(theme.SoftBorder);
            LolHudOptionsCard.Background = Brush(theme.Card);
            LolHudOptionsCard.BorderBrush = Brush(theme.SoftBorder);
            VisualEffectsCard.Background = Brush(theme.Card);
            VisualEffectsCard.BorderBrush = Brush(theme.SoftBorder);
            HudOptionsTitleText.Foreground = Brush(theme.Text);
            VisualEffectsTitleText.Foreground = Brush(theme.Text);
            AdvancedEffectsPanelSupport.ApplyMoneyRow(MoneyRewardModeLabel, MoneyRewardModeSelector, theme);
            AdvancedEffectsPanelSupport.ApplyMoneyRow(LanguageLabel, LanguageSelector, theme);
            AdvancedEffectsPanelSupport.ApplyMoneyRow(TeamThemeLabel, TeamThemeSelector, theme);
            AdvancedEffectsPanelSupport.ApplyMoneyRow(ActorTextLabel, ActorTextSelector, theme);
            AdvancedEffectsPanelSupport.ApplyMoneyRow(DefaultChampionLabel, DefaultChampionSelector, theme);
            AdvancedEffectsPanelSupport.ApplyMoneyRow(BannerModeLabel, BannerModeSelector, theme);
            AdvancedEffectsPanelSupport.ApplyMoneyRow(CsStreakModeLabel, CsStreakModeSelector, theme);
            TextFlankingToggle.Foreground = Brush(theme.Text);
            PulseVfxToggle.Foreground = Brush(theme.Text);
            EnemyEventsToggle.Foreground = Brush(theme.Text);
            AssistAudioToggle.Foreground = Brush(theme.Text);
            UpperToggle.Foreground = Brush(theme.Text);
            LowerToggle.Foreground = Brush(theme.Text);
            CrosshairToggle.Foreground = Brush(theme.Text);
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
            LeagueOfLegendsSettingsStore.SaveHudLanguage(
                ReadTaggedItem(LanguageSelector, LeagueOfLegendsSettingsStore.LanguageFollowUi));
            LeagueOfLegendsSettingsStore.SaveTeamTheme(
                ReadTaggedItem(TeamThemeSelector, LeagueOfLegendsSettingsStore.TeamBlue));
            LeagueOfLegendsSettingsStore.SaveActorTextMode(
                ReadTaggedItem(ActorTextSelector, LeagueOfLegendsSettingsStore.ActorYou));
            LeagueOfLegendsSettingsStore.SaveDefaultChampion(
                ReadTaggedItem(DefaultChampionSelector, LeagueOfLegendsSettingsStore.BuiltInChampions[0]));
            LeagueOfLegendsSettingsStore.SaveBannerMode(
                ReadTaggedItem(BannerModeSelector, LeagueOfLegendsSettingsStore.BannerAllKills));
            LeagueOfLegendsSettingsStore.SaveCsStreakMode(
                ReadTaggedItem(CsStreakModeSelector, LeagueOfLegendsSettingsStore.StreakCombined));
        }

        private void OnHudOptionToggled(object sender, RoutedEventArgs e)
        {
            if (_suppressEvents) return;
            LeagueOfLegendsSettingsStore.SaveTextFlankingEnabled(TextFlankingToggle.IsChecked != false);
            LeagueOfLegendsSettingsStore.SavePulseVfxEnabled(PulseVfxToggle.IsChecked != false);
            LeagueOfLegendsSettingsStore.SaveEnemyEventsEnabled(EnemyEventsToggle.IsChecked != false);
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
                GameStyleMode.LeagueOfLegends,
                new KillFeedbackVisibilitySettingsValues
                {
                    UpperEnabled = UpperToggle.IsChecked != false,
                    LowerEnabled = LowerToggle.IsChecked != false,
                    CrosshairEnabled = CrosshairToggle.IsChecked == true
                });
        }

        private void OnResetClick(object sender, RoutedEventArgs e)
        {
            LeagueOfLegendsSettingsStore.Reset();
            SelectMoneyRewardMode("delta", "delta");
            StreakEditor.SelectValue(SharedStreakSettingsStore.LifeMode);
            SetAssistAudioEnabled(false);
            KillFeedbackVisibilitySettingsStore.Save(
                GameStyleMode.LeagueOfLegends,
                new KillFeedbackVisibilitySettingsValues
                {
                    UpperEnabled = true,
                    LowerEnabled = true,
                    CrosshairEnabled = false
                });
            LoadLeagueOfLegendsSettings();
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

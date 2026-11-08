using System;
using KillConfirmGameBar.Services;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace KillConfirmGameBar.Features.CompatibilityDisplay.Controls.GameStyles
{
    public sealed partial class SanguoshaAdvancedEffectsPanel : UserControl
    {
        private bool _suppressPriorityEvents;

        public SanguoshaAdvancedEffectsPanel()
        {
            InitializeComponent();
            AdvancedEffectsPanelSupport.TrackCombatHeader(this, CombatGameLogo, TitleText);
            Loaded += OnLoaded;
            Unloaded += (s, e) => GameSourceSettingsStore.Changed -= OnGameSourceChanged;
        }
        private void OnGameSourceChanged(object sender, EventArgs e)
        {
            _ = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () => ApplyLanguage(LocalizationManager.Current == UiLanguage.SimplifiedChinese));
        }

        public event SelectionChangedEventHandler StreakModeSelectionChanged;
        public event SelectionChangedEventHandler PrioritySettingsChanged;

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
        }

        internal void RefreshSettings()
        {
            SelectStreakMode(SharedStreakSettingsStore.Load(GameStyleMode.Sanguosha));
            SanguoshaGameplaySettingsValues settings = SanguoshaGameplaySettingsStore.Load();
            SelectPrioritySettings(settings);
        }

        public void SelectPrioritySettings(SanguoshaGameplaySettingsValues settings)
        {
            if (settings == null) return;
            _suppressPriorityEvents = true;
            try
            {
                SelectTaggedItem(HeadshotAudioPrioritySelector, settings.HeadshotSpecialAudioPriority ? "special" : "streak", "special");
                SelectTaggedItem(KnifeAudioPrioritySelector, settings.KnifeSpecialAudioPriority ? "special" : "streak", "special");
                SelectTaggedItem(GrenadeAudioPrioritySelector, settings.GrenadeSpecialAudioPriority ? "special" : "streak", "special");
                SelectTaggedItem(HeadshotIconPrioritySelector, settings.HeadshotSpecialIconPriority ? "special" : "streak", "special");
                SelectTaggedItem(KnifeIconPrioritySelector, settings.KnifeSpecialIconPriority ? "special" : "streak", "special");
                SelectTaggedItem(GrenadeIconPrioritySelector, settings.GrenadeSpecialIconPriority ? "special" : "streak", "special");
            }
            finally
            {
                _suppressPriorityEvents = false;
            }
        }

        public SanguoshaGameplaySettingsValues GetPrioritySettings(SanguoshaGameplaySettingsValues fallback = null)
        {
            SanguoshaGameplaySettingsValues fb = fallback ?? SanguoshaGameplaySettingsStore.Load();
            return new SanguoshaGameplaySettingsValues
            {
                HeadshotSpecialAudioPriority = ReadTaggedItem(HeadshotAudioPrioritySelector, fb.HeadshotSpecialAudioPriority ? "special" : "streak") == "special",
                KnifeSpecialAudioPriority = ReadTaggedItem(KnifeAudioPrioritySelector, fb.KnifeSpecialAudioPriority ? "special" : "streak") == "special",
                GrenadeSpecialAudioPriority = ReadTaggedItem(GrenadeAudioPrioritySelector, fb.GrenadeSpecialAudioPriority ? "special" : "streak") == "special",
                HeadshotSpecialIconPriority = ReadTaggedItem(HeadshotIconPrioritySelector, fb.HeadshotSpecialIconPriority ? "special" : "streak") == "special",
                KnifeSpecialIconPriority = ReadTaggedItem(KnifeIconPrioritySelector, fb.KnifeSpecialIconPriority ? "special" : "streak") == "special",
                GrenadeSpecialIconPriority = ReadTaggedItem(GrenadeIconPrioritySelector, fb.GrenadeSpecialIconPriority ? "special" : "streak") == "special"
            };
        }

        internal void ApplyTheme(GameThemePalette theme)
        {
            AdvancedEffectsPanelSupport.ApplyHeader(TitleText, HintText, theme);
            AdvancedEffectsPanelSupport.ApplyResetButton(ResetButton, theme);
            if (PrioritiesGroupLabel != null) PrioritiesGroupLabel.Foreground = new SolidColorBrush(theme.Text);
            if (PrioritiesHintText != null) PrioritiesHintText.Foreground = new SolidColorBrush(theme.MutedText);
            StreakEditor.ApplyTheme(theme);
            AdvancedEffectsPanelSupport.ApplyMoneyRow(HeadshotAudioPriorityLabel, HeadshotAudioPrioritySelector, theme);
            AdvancedEffectsPanelSupport.ApplyMoneyRow(KnifeAudioPriorityLabel, KnifeAudioPrioritySelector, theme);
            AdvancedEffectsPanelSupport.ApplyMoneyRow(GrenadeAudioPriorityLabel, GrenadeAudioPrioritySelector, theme);
            AdvancedEffectsPanelSupport.ApplyMoneyRow(HeadshotIconPriorityLabel, HeadshotIconPrioritySelector, theme);
            AdvancedEffectsPanelSupport.ApplyMoneyRow(KnifeIconPriorityLabel, KnifeIconPrioritySelector, theme);
            AdvancedEffectsPanelSupport.ApplyMoneyRow(GrenadeIconPriorityLabel, GrenadeIconPrioritySelector, theme);
        }

        public void ApplyLanguage(bool isChinese)
        {
            AdvancedEffectsPanelSupport.ApplyCombatHeader(CombatGameLogo, TitleText, isChinese);

            HintText.Text = isChinese
                ? "原版 Spine 骨骼动画与原版语音：首破至七破、8 杀以上（神威震乾坤）、爆头（神医妙手）、刀杀（青囊济世）、手雷（破敌）。"
                : "Original Spine animations and voices: First Strike through Seven Kills, 8+ kills, Headshot, Knife, and Grenade.";
            ResetButtonText.Text = isChinese ? "恢复默认" : "Reset";
            ToolTipService.SetToolTip(ResetButton, isChinese ? "恢复三国杀默认设置" : "Restore Sanguosha defaults");
            StreakEditor.ApplyLanguage(isChinese);

            if (PrioritiesGroupLabel != null)
                PrioritiesGroupLabel.Text = isChinese ? "爆头、刀杀与雷杀提示优先级" : "Priority Policies (Audio & Icon)";
            if (PrioritiesHintText != null)
                PrioritiesHintText.Text = isChinese
                    ? "2 连杀起比较优先级；单杀仍显示、播放对应击杀提示。声音与图标独立设置。"
                    : "Priorities apply from 2 kills; single kills keep their event cue. Audio and icons are independent.";

            HeadshotAudioPriorityLabel.Text = isChinese ? "爆头声音" : "Headshot audio";
            KnifeAudioPriorityLabel.Text = isChinese ? "刀杀声音" : "Knife-kill audio";
            GrenadeAudioPriorityLabel.Text = isChinese ? "雷杀声音" : "Grenade-kill audio";
            HeadshotSpecialPriorityItem.Content = isChinese ? "爆头优先" : "Headshot priority";
            KnifeSpecialPriorityItem.Content = isChinese ? "刀杀优先" : "Knife-kill priority";
            GrenadeSpecialPriorityItem.Content = isChinese ? "雷杀优先" : "Grenade-kill priority";
            HeadshotStreakPriorityItem.Content = isChinese ? "连杀优先" : "Kill-streak priority";
            KnifeStreakPriorityItem.Content = HeadshotStreakPriorityItem.Content;
            GrenadeStreakPriorityItem.Content = HeadshotStreakPriorityItem.Content;
            HeadshotIconPriorityLabel.Text = isChinese ? "爆头图标" : "Headshot icon";
            KnifeIconPriorityLabel.Text = isChinese ? "刀杀图标" : "Knife-kill icon";
            GrenadeIconPriorityLabel.Text = isChinese ? "雷杀图标" : "Grenade-kill icon";
            HeadshotIconSpecialPriorityItem.Content = HeadshotSpecialPriorityItem.Content;
            KnifeIconSpecialPriorityItem.Content = KnifeSpecialPriorityItem.Content;
            GrenadeIconSpecialPriorityItem.Content = GrenadeSpecialPriorityItem.Content;
            HeadshotIconStreakPriorityItem.Content = HeadshotStreakPriorityItem.Content;
            KnifeIconStreakPriorityItem.Content = HeadshotStreakPriorityItem.Content;
            KnifeIconStreakPriorityItem.Content = HeadshotStreakPriorityItem.Content;
            GrenadeIconStreakPriorityItem.Content = HeadshotStreakPriorityItem.Content;
            if (GameSourceSettingsStore.IsLol)
            {

                HintText.Text = isChinese ? "英雄联盟模式：1–7 杀对应首破至七破，8 杀及以上重复神威震乾坤；一血/终结/团灭/复仇播放万夫莫敌；助攻播放神医妙手；控龙/抢龙播放青囊济世；推塔/一血塔/破水晶播放破敌。" : "LOL Mode: 1-7 kills play First Strike..Seven Kills, 8+ kills repeat Tier 8; First Blood/Shutdown/Ace/Revenge play Tier 9; Assist plays 神医妙手; Objective/Steal plays 青囊济世; Turret/Inhibitor plays 破敌.";
                if (PrioritiesGroupLabel != null) PrioritiesGroupLabel.Text = isChinese ? "助攻、控龙与推塔提示优先级" : "Assist, Objective & Turret Priority";
                HeadshotAudioPriorityLabel.Text = isChinese ? "助攻（神医妙手）声音" : "Assist audio";
                KnifeAudioPriorityLabel.Text = isChinese ? "控龙/抢龙（青囊济世）声音" : "Objective/Steal audio";
                GrenadeAudioPriorityLabel.Text = isChinese ? "推塔/破水晶（破敌）声音" : "Turret/Inhibitor audio";
                HeadshotSpecialPriorityItem.Content = isChinese ? "神医妙手优先" : "Assist priority";
                KnifeSpecialPriorityItem.Content = isChinese ? "青囊济世优先" : "Objective priority";
                GrenadeSpecialPriorityItem.Content = isChinese ? "破敌优先" : "Turret priority";
                HeadshotIconPriorityLabel.Text = isChinese ? "助攻（神医妙手）动画" : "Assist animation";
                KnifeIconPriorityLabel.Text = isChinese ? "控龙/抢龙（青囊济世）动画" : "Objective/Steal animation";
                GrenadeIconPriorityLabel.Text = isChinese ? "推塔/破水晶（破敌）动画" : "Turret/Inhibitor animation";
                HeadshotIconSpecialPriorityItem.Content = HeadshotSpecialPriorityItem.Content;
                KnifeIconSpecialPriorityItem.Content = KnifeSpecialPriorityItem.Content;
                GrenadeIconSpecialPriorityItem.Content = GrenadeSpecialPriorityItem.Content;
            }

        }

        public string GetSelectedStreakMode(string fallback) => StreakEditor.GetValue(fallback);
        public void SelectStreakMode(string value) => StreakEditor.SelectValue(value);

        private void OnStreakModeSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            StreakModeSelectionChanged?.Invoke(this, e);
        }

        private void OnPrioritySelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressPriorityEvents) return;
            PrioritySettingsChanged?.Invoke(this, e);
        }

        private void OnResetClick(object sender, RoutedEventArgs e)
        {
            SelectStreakMode(SharedStreakSettingsStore.LifeMode);
            var defaults = new SanguoshaGameplaySettingsValues
            {
                HeadshotSpecialAudioPriority = true,
                KnifeSpecialAudioPriority = true,
                GrenadeSpecialAudioPriority = true,
                HeadshotSpecialIconPriority = true,
                KnifeSpecialIconPriority = true,
                GrenadeSpecialIconPriority = true
            };
            SelectPrioritySettings(defaults);
            StreakModeSelectionChanged?.Invoke(this, null);
            PrioritySettingsChanged?.Invoke(this, null);
        }

        private static string ReadTaggedItem(ComboBox selector, string fallback)
        {
            if (selector?.SelectedItem is ComboBoxItem item && item.Tag is string tag && !string.IsNullOrWhiteSpace(tag))
            {
                return tag;
            }
            return fallback;
        }

        private static void SelectTaggedItem(ComboBox selector, string targetTag, string defaultTag)
        {
            if (selector == null) return;
            string desired = string.IsNullOrWhiteSpace(targetTag) ? defaultTag : targetTag;
            foreach (object obj in selector.Items)
            {
                if (obj is ComboBoxItem item && item.Tag is string tag && string.Equals(tag, desired, StringComparison.OrdinalIgnoreCase))
                {
                    selector.SelectedItem = item;
                    return;
                }
            }
        }
    }
}

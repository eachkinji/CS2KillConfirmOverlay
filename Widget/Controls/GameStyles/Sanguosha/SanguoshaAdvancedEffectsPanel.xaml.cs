using System;
using KillConfirmGameBar.Services;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace KillConfirmGameBar.Controls.GameStyles
{
    public sealed partial class SanguoshaAdvancedEffectsPanel : UserControl
    {
        private bool _suppressPriorityEvents;

        public SanguoshaAdvancedEffectsPanel()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        public event SelectionChangedEventHandler StreakModeSelectionChanged;
        public event SelectionChangedEventHandler PrioritySettingsChanged;

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            RefreshSettings();
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
            TitleText.Text = isChinese ? "三国杀连破播报与优先级" : "Sanguosha Kill Broadcast & Priorities";
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
            GrenadeIconStreakPriorityItem.Content = HeadshotStreakPriorityItem.Content;

            RefreshSettings();
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

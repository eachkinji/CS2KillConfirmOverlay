using KillConfirmGameBar.Services;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace KillConfirmGameBar.Features.CompatibilityDisplay.Controls.GameStyles
{
    // Sanguosha replays the game's own broadcast animation, so the only tunable is how
    // Kill Confirm counts kills into the 首破..七破 tiers (shared streak window).
    public sealed partial class SanguoshaAdvancedEffectsPanel : UserControl
    {
        private GameThemePalette _theme;

        public SanguoshaAdvancedEffectsPanel()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        public event SelectionChangedEventHandler StreakModeSelectionChanged;

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            RefreshSettings();
        }

        internal void RefreshSettings()
        {
            SelectStreakMode(SharedStreakSettingsStore.Load(GameStyleMode.Sanguosha));
        }

        internal void ApplyTheme(GameThemePalette theme)
        {
            _theme = theme;
            TitleText.Foreground = theme.Brush(theme.Text);
            HintText.Foreground = theme.Brush(theme.MutedText);
            StreakEditor.ApplyTheme(theme);
            AdvancedEffectsPanelSupport.ApplyResetButton(ResetButton, theme);
        }

        public void ApplyLanguage(bool isChinese)
        {
            TitleText.Text = isChinese ? "三国杀连破播报" : "Sanguosha Kill Broadcast";
            HintText.Text = isChinese
                ? "原版 Spine 骨骼动画与原版语音：首破至七破、8 杀以上（神威震乾坤）、爆头（万夫莫敌）、刀杀（破敌）、手雷（秒杀）。"
                : "Original Spine animations and voices: First Strike through Seven Kills, 8+ kills (Godlike Might), Headshot (Unstoppable), Knife (Break the Enemy), and Grenade (Instant Kill).";
            ResetButtonText.Text = isChinese ? "恢复默认" : "Reset";
            ToolTipService.SetToolTip(ResetButton, isChinese ? "恢复三国杀默认设置" : "Restore Sanguosha defaults");
            StreakEditor.ApplyLanguage(isChinese);
            RefreshSettings();
        }

        public string GetSelectedStreakMode(string fallback) => StreakEditor.GetValue(fallback);
        public void SelectStreakMode(string value) => StreakEditor.SelectValue(value);

        private void OnStreakModeSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            StreakModeSelectionChanged?.Invoke(this, e);
        }

        private void OnResetClick(object sender, RoutedEventArgs e)
        {
            SelectStreakMode(SharedStreakSettingsStore.LifeMode);
            StreakModeSelectionChanged?.Invoke(this, null);
        }
    }
}

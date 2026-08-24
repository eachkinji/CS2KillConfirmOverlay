using System;
using KillConfirmGameBar.Services;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace KillConfirmGameBar.Controls.GameStyles
{
    public sealed partial class DoubaoAdvancedEffectsPanel : UserControl
    {
        private bool _suppressKillMarkChanges;
        private bool _isChinese = true;
        private GameThemePalette _theme;

        public DoubaoAdvancedEffectsPanel()
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
            SelectStreakMode(SharedStreakSettingsStore.Load(GameStyleMode.Doubao));
            SetKillMarkEnabled(
                KillFeedbackVisibilitySettingsStore.Load(GameStyleMode.Doubao).CrosshairEnabled,
                false);
        }

        internal void ApplyTheme(GameThemePalette theme)
        {
            _theme = theme;
            TitleText.Foreground = theme.Brush(theme.Text);
            StreakEditor.ApplyTheme(theme);
            AdvancedEffectsPanelSupport.ApplyKillMarkCard(
                VisualEffectsCard,
                VisualEffectsTitle,
                KillMarkEffectLabel,
                KillMarkEffectToggle,
                theme);
            AdvancedEffectsPanelSupport.ApplyResetButton(ResetButton, theme);
        }

        public void ApplyLanguage(bool isChinese)
        {
            _isChinese = isChinese;
            TitleText.Text = isChinese ? "豆包高级特效" : "Doubao Effects";
            ResetButtonText.Text = isChinese ? "恢复默认" : "Reset";
            ToolTipService.SetToolTip(ResetButton, isChinese ? "恢复豆包默认设置" : "Restore Doubao defaults");

            StreakEditor.ApplyLanguage(isChinese);
            AdvancedEffectsPanelSupport.ApplyKillMarkLanguage(
                VisualEffectsTitle,
                KillMarkEffectLabel,
                KillMarkEffectToggle,
                isChinese);
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
            SetKillMarkEnabled(true);
            StreakModeSelectionChanged?.Invoke(this, null);
        }

        private void SetKillMarkEnabled(bool enabled, bool save = true)
        {
            _suppressKillMarkChanges = true;
            KillMarkEffectToggle.IsOn = enabled;
            _suppressKillMarkChanges = false;
            if (save)
            {
                SaveKillMarkSetting();
            }
        }

        private void OnKillMarkEffectToggled(object sender, RoutedEventArgs e)
        {
            if (!_suppressKillMarkChanges)
            {
                SaveKillMarkSetting();
            }
        }

        private void SaveKillMarkSetting()
        {
            KillFeedbackVisibilitySettingsStore.Save(
                GameStyleMode.Doubao,
                new KillFeedbackVisibilitySettingsValues
                {
                    CrosshairEnabled = KillMarkEffectToggle.IsOn
                });
        }
    }
}

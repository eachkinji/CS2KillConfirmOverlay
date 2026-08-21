using System;
using KillConfirmGameBar.Services;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace KillConfirmGameBar.Controls.GameStyles
{
    public sealed partial class DoubaoAdvancedEffectsPanel : UserControl
    {
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
        }

        internal void ApplyTheme(GameThemePalette theme)
        {
            _theme = theme;
            AdvancedEffectsPanelSupport.ApplyHeader(TitleText, HintText, theme);
            StreakEditor.ApplyTheme(theme);
            AdvancedEffectsPanelSupport.ApplyResetButton(ResetButton, theme);
        }

        public void ApplyLanguage(bool isChinese)
        {
            _isChinese = isChinese;
            TitleText.Text = isChinese ? "豆包高级特效" : "Doubao Effects";
            HintText.Text = isChinese
                ? "设置连杀判定模式。逐杀图标与语音已整合至【语音包库】与【图标包库】管理。"
                : "Configure streak mode. Icons and voices are managed in the Voice / Icon Pack library tabs.";
            ResetButtonText.Text = isChinese ? "恢复默认" : "Reset";
            ToolTipService.SetToolTip(ResetButton, isChinese ? "恢复豆包默认设置" : "Restore Doubao defaults");

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

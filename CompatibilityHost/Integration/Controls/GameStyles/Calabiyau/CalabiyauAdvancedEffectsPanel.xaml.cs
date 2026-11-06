using KillConfirmGameBar.Services;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace KillConfirmGameBar.Features.CompatibilityDisplay.Controls.GameStyles
{
    public sealed partial class CalabiyauAdvancedEffectsPanel : UserControl
    {
        public CalabiyauAdvancedEffectsPanel()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        public event SelectionChangedEventHandler StreakModeSelectionChanged;

        public ComboBox StreakModeSelectorControl => StreakEditor.SelectorControl;

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            RefreshSettings();
        }

        internal void RefreshSettings()
        {
            SelectStreakMode(SharedStreakSettingsStore.Load(GameStyleMode.Calabiyau));
        }

        internal void ApplyTheme(GameThemePalette theme)
        {
            AdvancedEffectsPanelSupport.ApplyHeader(TitleText, HintText, theme);
            AdvancedEffectsPanelSupport.ApplyResetButton(ResetButton, theme);
            StreakEditor.ApplyTheme(theme);
        }

        public void ApplyLanguage(bool isChinese)
        {
            TitleText.Text = isChinese ? "卡拉彼丘战斗与特效" : "Strinova Combat & Effects";
            HintText.Text = isChinese
                ? "默认效果内置；其他主题需先导入图标包，随击杀数显示对应动画和星标。准心使用卡拉彼丘击杀反馈，下方与准心可以独立开关。"
                : "Choose each theme variant separately. Animations and stars follow the kill count. Native Calabiyau kill crosshair and lower feedback have independent switches.";
            ResetButtonText.Text = isChinese ? "恢复默认" : "Reset";
            ToolTipService.SetToolTip(ResetButton, isChinese ? "恢复卡拉彼丘默认设置" : "Restore Strinova defaults");
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

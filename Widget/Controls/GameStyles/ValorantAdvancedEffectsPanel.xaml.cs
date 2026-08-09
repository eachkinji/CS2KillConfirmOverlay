using KillConfirmGameBar.Services;
using Windows.UI.Xaml.Controls;

namespace KillConfirmGameBar.Controls.GameStyles
{
    public sealed partial class ValorantAdvancedEffectsPanel : UserControl
    {
        public ValorantAdvancedEffectsPanel()
        {
            InitializeComponent();
        }

        public event SelectionChangedEventHandler StreakModeSelectionChanged;

        public ComboBox StreakModeSelectorControl => StreakModeSelector;

        public void SetStylePanel(ValorantStylePanel panel)
        {
            StylePanelHost.Content = panel;
        }

        internal void ApplyTheme(GameThemePalette theme)
        {
            AdvancedEffectsPanelSupport.ApplyHeader(TitleText, HintText, theme);
            AdvancedEffectsPanelSupport.ApplyMoneyRow(StreakModeLabel, StreakModeSelector, theme);
        }

        public void ApplyLanguage(bool isChinese)
        {
            TitleText.Text = isChinese ? "VAL \u9ad8\u7ea7\u7279\u6548" : "VAL Effects";
            HintText.Text = string.Empty;
            HintText.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
            SharedStreakSettingsStore.ApplyLanguage(
                StreakModeLabel,
                StreakLifeItem,
                StreakTimed5Item,
                StreakTimed10Item,
                StreakTimed15Item,
                isChinese);
        }

        public string GetSelectedStreakMode(string fallback)
        {
            return SharedStreakSettingsStore.Read(StreakModeSelector, fallback);
        }

        public void SelectStreakMode(string value)
        {
            SharedStreakSettingsStore.Select(StreakModeSelector, value);
        }

        private void OnStreakModeSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            StreakModeSelectionChanged?.Invoke(this, e);
        }
    }
}

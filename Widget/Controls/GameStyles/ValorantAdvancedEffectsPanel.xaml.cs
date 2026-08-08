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
            HintText.Text = isChinese
                ? "VAL \u53ea\u4f7f\u7528 Valorant \u8d44\u6e90\u5305\u548c\u52a8\u753b\u6587\u4ef6\uff0c\u4e0d\u663e\u793a CS \u5956\u91d1\u7b97\u6cd5\u8bbe\u7f6e\u3002"
                : "VAL uses Valorant packs and animation files only. CS money reward logic is not shown here.";
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

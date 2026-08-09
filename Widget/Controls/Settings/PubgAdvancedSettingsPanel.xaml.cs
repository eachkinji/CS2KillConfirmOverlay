using KillConfirmGameBar.Services;
using Windows.UI.Xaml.Controls;

namespace KillConfirmGameBar.Controls.Settings
{
    public sealed partial class PubgAdvancedSettingsPanel : UserControl
    {
        private bool _suppressStreakEvents;

        public PubgAdvancedSettingsPanel()
        {
            InitializeComponent();
            _suppressStreakEvents = true;
            SharedStreakSettingsPanelSupport.Load(GameStyleMode.Pubg, StreakModeSelector);
            _suppressStreakEvents = false;
        }

        internal void ApplyTheme(GameThemePalette theme)
        {
            SettingsPanelSupport.ApplyPanel(Card, TitleText, BodyText, theme);
            SettingsPanelSupport.ApplySettingRow(StreakModeLabel, StreakModeSelector, theme);
        }

        public void ApplyLanguage(bool isChinese)
        {
            TitleText.Text = isChinese ? "PUBG 高级设置" : "PUBG advanced settings";
            SharedStreakSettingsStore.ApplyLanguage(
                StreakModeLabel,
                StreakLifeItem,
                StreakTimed5Item,
                StreakTimed10Item,
                StreakTimed15Item,
                isChinese);
        }

        private async void OnStreakModeSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_suppressStreakEvents)
            {
                await SharedStreakSettingsPanelSupport.SaveAndSyncAsync(
                    GameStyleMode.Pubg,
                    StreakModeSelector);
            }
        }
    }
}

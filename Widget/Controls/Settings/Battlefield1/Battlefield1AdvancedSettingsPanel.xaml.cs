using KillConfirmGameBar.Services;
using Windows.UI.Xaml.Controls;

namespace KillConfirmGameBar.Controls.Settings
{
    public sealed partial class Battlefield1AdvancedSettingsPanel : UserControl
    {
        private bool _suppressStreakEvents;

        public Battlefield1AdvancedSettingsPanel()
        {
            InitializeComponent();
            _suppressStreakEvents = true;
            SharedStreakSettingsPanelSupport.Load(GameStyleMode.Battlefield1, StreakModeSelector);
            _suppressStreakEvents = false;
        }

        internal void ApplyTheme(GameThemePalette theme)
        {
            SettingsPanelSupport.ApplyPanel(Card, TitleText, BodyText, theme);
            SettingsPanelSupport.ApplySettingRow(StreakModeLabel, StreakModeSelector, theme);
        }

        public void ApplyLanguage(bool isChinese)
        {
            TitleText.Text = isChinese ? "Battlefield 1 高级设置" : "Battlefield 1 advanced settings";
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
                    GameStyleMode.Battlefield1,
                    StreakModeSelector);
            }
        }
    }
}

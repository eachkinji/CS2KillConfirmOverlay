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
            SettingsPanelSupport.ApplyTag(AssetTag, AssetTagText, theme);
            SettingsPanelSupport.ApplyTag(HudTag, HudTagText, theme);
            SettingsPanelSupport.ApplyTag(ImportLockTag, ImportLockTagText, theme);
            SettingsPanelSupport.ApplySettingRow(StreakModeLabel, StreakModeSelector, theme);
        }

        public void ApplyLanguage(bool isChinese)
        {
            TitleText.Text = isChinese ? "PUBG \u9ad8\u7ea7\u8bbe\u7f6e" : "PUBG advanced settings";
            BodyText.Text = isChinese
                ? "PUBG \u4f7f\u7528\u72ec\u7acb\u7684 pubg \u58f0\u97f3\u5305\u3001\u6dd8\u6c70\u5b57\u5e55\u6e32\u67d3\u5668\u548c PUBG \u4e13\u5c5e\u8d44\u6e90\u76ee\u5f55\u3002"
                : "PUBG uses a separate pubg sound pack, elimination text renderer, and pubg-only resource folder.";
            AssetTagText.Text = isChinese ? "pubg \u8d44\u6e90" : "pubg assets";
            HudTagText.Text = isChinese ? "\u6dd8\u6c70\u5b57\u5e55" : "Elimination text";
            ImportLockTagText.Text = isChinese ? "\u4e0d\u5141\u8bb8\u5bfc\u5165\u6587\u4ef6" : "No file import";
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

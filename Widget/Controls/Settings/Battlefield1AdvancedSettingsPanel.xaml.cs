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
            SettingsPanelSupport.ApplyTag(AssetTag, AssetTagText, theme);
            SettingsPanelSupport.ApplyTag(MoneyTag, MoneyTagText, theme);
            SettingsPanelSupport.ApplyTag(ImportLockTag, ImportLockTagText, theme);
            SettingsPanelSupport.ApplySettingRow(StreakModeLabel, StreakModeSelector, theme);
        }

        public void ApplyLanguage(bool isChinese)
        {
            TitleText.Text = isChinese ? "Battlefield 1 \u9ad8\u7ea7\u8bbe\u7f6e" : "Battlefield 1 advanced settings";
            BodyText.Text = isChinese
                ? "BF1 \u4f7f\u7528\u6218\u5730\u4e00\u8d44\u6e90\u76ee\u5f55\u3001BF1 \u5361\u7247\u52a8\u753b\u6587\u4ef6\uff0c\u4ee5\u53ca\u53ea\u5c5e\u4e8e BF1/BF5 \u7684\u5956\u91d1\u6a21\u5f0f\u3002\u6b64\u9875\u4e0d\u5141\u8bb8\u5bfc\u5165\u6587\u4ef6\uff0cCF \u548c VAL \u7684\u8bbe\u7f6e\u4e0d\u6df7\u8fdb\u6765\u3002"
                : "BF1 uses the Battlefield 1 resource folder, BF1 card animation files, and BF1/BF5-only money reward modes. File import is disabled here; CF and VAL settings stay out of this module.";
            AssetTagText.Text = isChinese ? "\u6218\u5730\u4e00\u8d44\u6e90" : "battlefield1 assets";
            MoneyTagText.Text = isChinese ? "\u5956\u91d1\u6a21\u5f0f" : "Money mode";
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
                    GameStyleMode.Battlefield1,
                    StreakModeSelector);
            }
        }
    }
}

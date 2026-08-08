using KillConfirmGameBar.Services;
using Windows.UI.Xaml.Controls;

namespace KillConfirmGameBar.Controls.Settings
{
    public sealed partial class ValorantAdvancedSettingsPanel : UserControl
    {
        private bool _suppressStreakEvents;

        public ValorantAdvancedSettingsPanel()
        {
            InitializeComponent();
            _suppressStreakEvents = true;
            SharedStreakSettingsPanelSupport.Load(GameStyleMode.Valorant, StreakModeSelector);
            _suppressStreakEvents = false;
        }

        internal void ApplyTheme(GameThemePalette theme)
        {
            SettingsPanelSupport.ApplyPanel(Card, TitleText, BodyText, theme);
            SettingsPanelSupport.ApplyTag(PackTag, PackTagText, theme);
            SettingsPanelSupport.ApplyTag(NoMoneyTag, NoMoneyTagText, theme);
            SettingsPanelSupport.ApplySettingRow(StreakModeLabel, StreakModeSelector, theme);
        }

        public void ApplyLanguage(bool isChinese)
        {
            TitleText.Text = isChinese ? "VAL \u9ad8\u7ea7\u8bbe\u7f6e" : "VAL advanced settings";
            BodyText.Text = isChinese
                ? "VAL \u53ea\u4f7f\u7528 Valorant \u8d44\u6e90\u5305\u548c Valorant \u52a8\u753b\u6587\u4ef6\u3002CF \u7cbe\u82f1/\u6b66\u5668\u8bbe\u7f6e\u548c\u6218\u5730\u5956\u91d1\u6a21\u5f0f\u90fd\u4e0d\u5728\u8fd9\u91cc\u3002"
                : "VAL uses Valorant resource packs and Valorant animation files only. CF elite/weapon settings and Battlefield money reward modes are excluded.";
            PackTagText.Text = isChinese ? "VAL \u8d44\u6e90" : "VAL packs";
            NoMoneyTagText.Text = isChinese ? "\u65e0\u5956\u91d1\u6a21\u5f0f" : "No money mode";
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
                    GameStyleMode.Valorant,
                    StreakModeSelector);
            }
        }
    }
}

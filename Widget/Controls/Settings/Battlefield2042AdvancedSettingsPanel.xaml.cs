using KillConfirmGameBar.Services;
using Windows.UI.Xaml.Controls;

namespace KillConfirmGameBar.Controls.Settings
{
    public sealed partial class Battlefield2042AdvancedSettingsPanel : UserControl
    {
        public Battlefield2042AdvancedSettingsPanel()
        {
            InitializeComponent();
        }

        internal void ApplyTheme(GameThemePalette theme)
        {
            SettingsPanelSupport.ApplyPanel(Card, TitleText, BodyText, theme);
            SettingsPanelSupport.ApplyTag(AssetTag, AssetTagText, theme);
            SettingsPanelSupport.ApplyTag(MoneyTag, MoneyTagText, theme);
            SettingsPanelSupport.ApplyTag(ImportLockTag, ImportLockTagText, theme);
        }

        public void ApplyLanguage(bool isChinese)
        {
            TitleText.Text = isChinese ? "Battlefield 2042 \u9ad8\u7ea7\u8bbe\u7f6e" : "Battlefield 2042 advanced settings";
            BodyText.Text = isChinese
                ? "2042 \u4f7f\u7528\u72ec\u7acb HUD \u6587\u4ef6\u3001battlefield2042 \u58f0\u97f3\u5305\u548c 2042 \u4e13\u5c5e\u754c\u9762\u3002\u8fd9\u4e2a\u5185\u7f6e\u6a21\u5757\u4e0d\u5141\u8bb8\u5bfc\u5165\u6587\u4ef6\u3002"
                : "2042 uses a separate HUD file set, battlefield2042 sound pack, and 2042-only UI. Imported files are disabled for this built-in module.";
            AssetTagText.Text = isChinese ? "2042 \u8d44\u6e90" : "2042 assets";
            MoneyTagText.Text = isChinese ? "\u94b1\u7011\u5e03" : "Money waterfall";
            ImportLockTagText.Text = isChinese ? "\u4e0d\u5141\u8bb8\u5bfc\u5165\u6587\u4ef6" : "No file import";
        }
    }
}

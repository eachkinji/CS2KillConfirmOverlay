using KillConfirmGameBar.Services;
using Windows.UI.Xaml.Controls;

namespace KillConfirmGameBar.Controls.Settings
{
    public sealed partial class Battlefield4AdvancedSettingsPanel : UserControl
    {
        public Battlefield4AdvancedSettingsPanel()
        {
            InitializeComponent();
        }

        internal void ApplyTheme(GameThemePalette theme)
        {
            SettingsPanelSupport.ApplyPanel(Card, TitleText, BodyText, theme);
            SettingsPanelSupport.ApplyTag(AssetTag, AssetTagText, theme);
            SettingsPanelSupport.ApplyTag(HudTag, HudTagText, theme);
            SettingsPanelSupport.ApplyTag(ImportLockTag, ImportLockTagText, theme);
        }

        public void ApplyLanguage(bool isChinese)
        {
            TitleText.Text = isChinese ? "Battlefield 4 \u9ad8\u7ea7\u8bbe\u7f6e" : "Battlefield 4 advanced settings";
            BodyText.Text = isChinese
                ? "BF4 \u4f7f\u7528\u72ec\u7acb\u7684 Battlefield 4 HUD \u6587\u4ef6\u3001bf4 \u58f0\u97f3\u5305\u548c BF4 \u4e13\u5c5e\u754c\u9762\u3002\u5176\u4ed6\u6e38\u620f\u8bbe\u7f6e\u4e0d\u6df7\u8fdb\u8fd9\u4e2a\u6a21\u5757\u3002"
                : "BF4 uses a separate Battlefield 4 HUD file set, bf4 sound pack, and BF4-only UI. Other game settings stay out of this module.";
            AssetTagText.Text = isChinese ? "bf4 \u8d44\u6e90" : "bf4 assets";
            HudTagText.Text = isChinese ? "\u6587\u5b57 HUD" : "Text HUD";
            ImportLockTagText.Text = isChinese ? "\u4e0d\u5141\u8bb8\u5bfc\u5165\u6587\u4ef6" : "No file import";
        }
    }
}

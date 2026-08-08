using KillConfirmGameBar.Services;
using Windows.UI.Xaml.Controls;

namespace KillConfirmGameBar.Controls.Settings
{
    public sealed partial class DeltaForceAdvancedSettingsPanel : UserControl
    {
        public DeltaForceAdvancedSettingsPanel()
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
            TitleText.Text = isChinese ? "\u4e09\u89d2\u6d32\u884c\u52a8 \u9ad8\u7ea7\u8bbe\u7f6e" : "Delta Force advanced settings";
            BodyText.Text = isChinese
                ? "\u4e09\u89d2\u6d32\u884c\u52a8\u4f7f\u7528\u72ec\u7acb\u7684 deltaforce \u58f0\u97f3\u5305\u3001\u6eda\u52a8\u56fe\u6807\u6e32\u67d3\u5668\u548c\u4e09\u89d2\u6d32\u4e13\u5c5e\u8d44\u6e90\u76ee\u5f55\u3002"
                : "Delta Force uses a separate deltaforce sound pack, scrolling icon renderer, and deltaforce-only resource folder.";
            AssetTagText.Text = isChinese ? "deltaforce \u8d44\u6e90" : "deltaforce assets";
            HudTagText.Text = isChinese ? "\u6eda\u52a8\u56fe\u6807" : "Scrolling icons";
            ImportLockTagText.Text = isChinese ? "\u4e0d\u5141\u8bb8\u5bfc\u5165\u6587\u4ef6" : "No file import";
        }
    }
}

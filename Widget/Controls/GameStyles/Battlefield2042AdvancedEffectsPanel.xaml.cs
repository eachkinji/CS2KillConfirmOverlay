using KillConfirmGameBar.Services;
using Windows.UI.Xaml.Controls;

namespace KillConfirmGameBar.Controls.GameStyles
{
    public sealed partial class Battlefield2042AdvancedEffectsPanel : UserControl
    {
        public Battlefield2042AdvancedEffectsPanel()
        {
            InitializeComponent();
        }

        internal void ApplyTheme(GameThemePalette theme)
        {
            AdvancedEffectsPanelSupport.ApplyHeader(TitleText, HintText, theme);
            AdvancedEffectsPanelSupport.ApplyNotice(ImportLockedNotice, ImportLockedText, theme);
            StylePanel.ApplyTheme(theme);
        }

        public void ApplyLanguage(bool isChinese)
        {
            TitleText.Text = isChinese ? "2042 \u9ad8\u7ea7\u7279\u6548" : "2042 Effects";
            HintText.Text = isChinese
                ? "Battlefield 2042 \u51fb\u6740\u5c55\u793a\u3001\u94b1\u7011\u5e03\u548c 2042 \u58f0\u97f3\u5305\u5728\u8fd9\u91cc\u5355\u72ec\u8bbe\u7f6e\u3002"
                : "Battlefield 2042 kill display, money waterfall, and 2042 sound pack are isolated here.";
            ImportLockedText.Text = isChinese
                ? "\u4ec5\u4f7f\u7528\u5185\u7f6e Battlefield 2042 \u8d44\u6e90\u3002\u6b64\u9875\u4e0d\u5141\u8bb8\u5bfc\u5165\u6587\u4ef6\u3002"
                : "Built-in Battlefield 2042 resources only. File import is disabled for this page.";
            StylePanel.ApplyLanguage(isChinese);
        }
    }
}

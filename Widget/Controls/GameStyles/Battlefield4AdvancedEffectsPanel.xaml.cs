using KillConfirmGameBar.Services;
using Windows.UI.Xaml.Controls;

namespace KillConfirmGameBar.Controls.GameStyles
{
    public sealed partial class Battlefield4AdvancedEffectsPanel : UserControl
    {
        public Battlefield4AdvancedEffectsPanel()
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
            TitleText.Text = isChinese ? "BF4 \u9ad8\u7ea7\u7279\u6548" : "BF4 Effects";
            HintText.Text = isChinese
                ? "BF4 \u7684\u6587\u5b57 HUD\u3001\u5206\u6570\u7d2f\u8ba1\u548c Battlefield 4 \u8d44\u6e90\u5728\u8fd9\u91cc\u5355\u72ec\u8bbe\u7f6e\u3002"
                : "BF4 text HUD, score accumulation, and Battlefield 4 assets are isolated here.";
            ImportLockedText.Text = isChinese
                ? "\u4ec5\u4f7f\u7528\u5185\u7f6e Battlefield 4 \u8d44\u6e90\u3002\u6b64\u9875\u4e0d\u5141\u8bb8\u5bfc\u5165\u6587\u4ef6\u3002"
                : "Built-in Battlefield 4 resources only. File import is disabled for this page.";
            StylePanel.ApplyLanguage(isChinese);
        }
    }
}

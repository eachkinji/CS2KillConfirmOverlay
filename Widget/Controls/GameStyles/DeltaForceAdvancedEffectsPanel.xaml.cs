using KillConfirmGameBar.Services;
using Windows.UI.Xaml.Controls;

namespace KillConfirmGameBar.Controls.GameStyles
{
    public sealed partial class DeltaForceAdvancedEffectsPanel : UserControl
    {
        public DeltaForceAdvancedEffectsPanel()
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
            TitleText.Text = isChinese ? "\u4e09\u89d2\u6d32\u884c\u52a8 \u9ad8\u7ea7\u7279\u6548" : "Delta Force Effects";
            HintText.Text = isChinese
                ? "\u4e09\u89d2\u6d32\u884c\u52a8\u7684\u6eda\u52a8\u56fe\u6807\u3001\u5206\u6570\u7011\u5e03\u548c Delta Force \u8d44\u6e90\u5728\u8fd9\u91cc\u5355\u72ec\u8bbe\u7f6e\u3002"
                : "Delta Force scrolling icons, score waterfall, and Delta Force assets are isolated here.";
            ImportLockedText.Text = isChinese
                ? "\u4ec5\u4f7f\u7528\u5185\u7f6e\u4e09\u89d2\u6d32\u884c\u52a8\u8d44\u6e90\u3002\u6b64\u9875\u4e0d\u5141\u8bb8\u5bfc\u5165\u6587\u4ef6\u3002"
                : "Built-in Delta Force resources only. File import is disabled for this page.";
            StylePanel.ApplyLanguage(isChinese);
        }
    }
}

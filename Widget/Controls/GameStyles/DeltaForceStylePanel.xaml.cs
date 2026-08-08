using KillConfirmGameBar.Services;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace KillConfirmGameBar.Controls.GameStyles
{
    public sealed partial class DeltaForceStylePanel : UserControl
    {
        public DeltaForceStylePanel()
        {
            InitializeComponent();
        }

        internal void ApplyTheme(GameThemePalette theme)
        {
            Card.Background = new SolidColorBrush(theme.Card);
            Card.BorderBrush = new SolidColorBrush(theme.Border);
            TitleText.Foreground = new SolidColorBrush(theme.Text);
            BodyText.Foreground = new SolidColorBrush(theme.MutedText);
        }

        public void ApplyLanguage(bool isChinese)
        {
            TitleText.Text = isChinese ? "\u4e09\u89d2\u6d32\u884c\u52a8 \u72ec\u7acb\u9762\u677f" : "Delta Force Panel";
            BodyText.Text = isChinese
                ? "\u4e09\u89d2\u6d32\u884c\u52a8\u4f7f\u7528\u72ec\u7acb\u7684\u6eda\u52a8\u56fe\u6807\u3001\u5206\u6570\u7011\u5e03\u548c deltaforce \u8d44\u6e90\u76ee\u5f55\u3002"
                : "Delta Force uses its own scrolling icons, score waterfall, and deltaforce resource folder.";
        }
    }
}

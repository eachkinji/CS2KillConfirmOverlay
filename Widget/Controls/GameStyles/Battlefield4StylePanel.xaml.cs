using KillConfirmGameBar.Services;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace KillConfirmGameBar.Controls.GameStyles
{
    public sealed partial class Battlefield4StylePanel : UserControl
    {
        public Battlefield4StylePanel()
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
            TitleText.Text = isChinese ? "Battlefield 4 \u72ec\u7acb\u9762\u677f" : "Battlefield 4 Panel";
            BodyText.Text = isChinese
                ? "BF4 \u4f7f\u7528\u72ec\u7acb\u7684 Battlefield 4 \u6587\u5b57 HUD\u3001\u5206\u6570\u5956\u52b1\u548c bf4 \u8d44\u6e90\u76ee\u5f55\u3002"
                : "BF4 uses its own Battlefield 4 text HUD, score rewards, and bf4 resource folder.";
        }
    }
}

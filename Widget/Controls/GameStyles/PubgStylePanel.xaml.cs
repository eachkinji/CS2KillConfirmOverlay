using KillConfirmGameBar.Services;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace KillConfirmGameBar.Controls.GameStyles
{
    public sealed partial class PubgStylePanel : UserControl
    {
        public PubgStylePanel()
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
            TitleText.Text = isChinese ? "PUBG \u72ec\u7acb\u9762\u677f" : "PUBG Panel";
            BodyText.Text = isChinese
                ? "PUBG \u4f7f\u7528\u72ec\u7acb\u7684\u6dd8\u6c70\u5b57\u5e55\u3001\u8fde\u6740\u5b57\u5e55\u548c pubg \u8d44\u6e90\u76ee\u5f55\u3002"
                : "PUBG uses its own elimination captions, streak text, and pubg resource folder.";
        }
    }
}

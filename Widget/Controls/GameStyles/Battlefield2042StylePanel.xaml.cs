using KillConfirmGameBar.Services;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace KillConfirmGameBar.Controls.GameStyles
{
    public sealed partial class Battlefield2042StylePanel : UserControl
    {
        public Battlefield2042StylePanel()
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
            TitleText.Text = isChinese ? "Battlefield 2042 \u72ec\u7acb\u9762\u677f" : "Battlefield 2042 Panel";
            BodyText.Text = isChinese
                ? "2042 \u4f7f\u7528\u72ec\u7acb\u9ab7\u9ac5\u56fe\u6807\u3001\u51fb\u6740\u4fe1\u606f\u6837\u5f0f\u3001\u94b1\u5956\u52b1\u548c\u4e13\u5c5e\u58f0\u97f3\u5305\u3002"
                : "2042 uses isolated skull assets, kill-feed styling, money rewards, and its own sound pack.";
        }
    }
}

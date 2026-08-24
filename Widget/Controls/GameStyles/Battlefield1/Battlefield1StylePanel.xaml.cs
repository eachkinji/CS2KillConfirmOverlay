using KillConfirmGameBar.Services;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace KillConfirmGameBar.Controls.GameStyles
{
    public sealed partial class Battlefield1StylePanel : UserControl
    {
        public Battlefield1StylePanel()
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
    }
}

using Windows.UI;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace KillConfirmGameBar.Pages.Main.Appearance
{
    public sealed partial class MainPageBackgroundView : UserControl
    {
        public MainPageBackgroundView()
        {
            InitializeComponent();
        }

        internal void ApplyPalette(Brush heroBrush, Color highlight, Color frame, Color accent)
        {
            HeroSlash.Fill = heroBrush;
            HeroSlashLight.Fill = new SolidColorBrush(highlight);

            var frameBrush = new SolidColorBrush(frame);
            FrameStripeOne.Stroke = frameBrush;
            FrameStripeTwo.Stroke = frameBrush;

            var accentBrush = new SolidColorBrush(accent);
            AccentLineOne.Fill = accentBrush;
            AccentLineTwo.Fill = accentBrush;
            AccentLineThree.Fill = accentBrush;
        }
    }
}

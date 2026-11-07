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

        internal void ApplyPalette(Brush heroBrush, Color highlight, Color frame, Color accent, double heroOpacity = 0.20)
        {
            HeroSlash.Fill = heroBrush;
            HeroSlash.Opacity = heroOpacity;
            HeroSlashLight.Fill = new SolidColorBrush(highlight);

            var frameBrush = new SolidColorBrush(frame);
            FrameStripeOne.Stroke = frameBrush;
            FrameStripeTwo.Stroke = frameBrush;

        }

        // CS2 amber, CSGO steel blue, LOL Hextech gold. Kept low-alpha so the
        // per-style tint and glass panels above stay readable.
        internal void ApplySource(string source)
        {
            Color color = source == "lol" ? Color.FromArgb(255, 200, 155, 60)
                : source == "csgo_legacy" ? Color.FromArgb(255, 59, 111, 216)
                : Color.FromArgb(255, 255, 140, 26);
            SourceWashInner.Color = Color.FromArgb(110, color.R, color.G, color.B);
            SourceWashOuter.Color = Color.FromArgb(0, color.R, color.G, color.B);
            SourceWash.Opacity = 1;

            SourceEdgeTop.Color = Color.FromArgb(0, color.R, color.G, color.B);
            SourceEdgeMid.Color = Color.FromArgb(200, color.R, color.G, color.B);
            SourceEdgeBottom.Color = Color.FromArgb(0, color.R, color.G, color.B);
            SourceEdge.Opacity = 1;

            SourceWatermark.Text = source == "lol" ? "LOL" : source == "csgo_legacy" ? "CSGO" : "CS2";
            SourceWatermark.Foreground = new SolidColorBrush(color);
            SourceWatermark.Opacity = 0.08;
        }
    }
}

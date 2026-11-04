using Windows.UI;

namespace KillConfirmGameBar.Services
{
    internal sealed partial class GameThemePalette
    {
        public static readonly GameThemePalette Battlefield6 = new GameThemePalette
        {
            Shell = Color.FromArgb(255, 247, 247, 250),
            Panel = Color.FromArgb(255, 252, 252, 254),
            Card = Color.FromArgb(184, 255, 255, 255),
            Field = Color.FromArgb(153, 255, 255, 255),
            SubtleField = Color.FromArgb(112, 255, 255, 255),
            Border = Color.FromArgb(255, 229, 229, 234),
            SoftBorder = Color.FromArgb(255, 239, 239, 244),
            Text = Color.FromArgb(255, 28, 28, 30),
            MutedText = Color.FromArgb(255, 110, 110, 115),
            SubtleText = Color.FromArgb(255, 142, 142, 147),
            Accent = Color.FromArgb(255, 212, 96, 37),
            AccentSoft = Color.FromArgb(255, 255, 237, 222),
            AccentText = Color.FromArgb(255, 149, 63, 20),
            Secondary = Color.FromArgb(255, 87, 108, 88),
            WarningField = Color.FromArgb(255, 254, 242, 242),
            WarningBorder = Color.FromArgb(255, 252, 165, 165),
            WarningText = Color.FromArgb(255, 153, 27, 27)
        };
    }
}

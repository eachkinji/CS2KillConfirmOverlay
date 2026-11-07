using Windows.UI;

namespace KillConfirmGameBar.Services
{
    internal sealed partial class GameThemePalette
    {
        // Lacquer red and bronze of Sanguosha's 10th-anniversary client chrome.
        public static readonly GameThemePalette Sanguosha = new GameThemePalette
        {
            Shell = Color.FromArgb(255, 247, 247, 250),
            Panel = Color.FromArgb(255, 252, 252, 254),
            Card = Color.FromArgb(184, 255, 255, 255),
            Field = Color.FromArgb(153, 255, 255, 255),
            SubtleField = Color.FromArgb(112, 255, 255, 255),
            Border = Color.FromArgb(255, 229, 229, 234),
            SoftBorder = Color.FromArgb(255, 239, 239, 244),
            Text = Color.FromArgb(255, 28, 28, 30),
            MutedText = Color.FromArgb(255, 99, 99, 107),
            SubtleText = Color.FromArgb(255, 99, 99, 107),
            Accent = Color.FromArgb(255, 176, 38, 32),
            AccentSoft = Color.FromArgb(255, 252, 232, 222),
            AccentText = Color.FromArgb(255, 128, 28, 22),
            Secondary = Color.FromArgb(255, 157, 76, 28),
            WarningField = Color.FromArgb(255, 254, 242, 242),
            WarningBorder = Color.FromArgb(255, 252, 165, 165),
            WarningText = Color.FromArgb(255, 153, 27, 27)
        };
    }
}

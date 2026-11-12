using Windows.UI;

namespace KillConfirmGameBar.Services
{
    internal sealed partial class GameThemePalette
    {
        public static readonly GameThemePalette WarThunder = new GameThemePalette
        {
            Shell = Color.FromArgb(255, 245, 247, 250),
            Panel = Color.FromArgb(255, 251, 252, 254),
            Card = Color.FromArgb(255, 250, 251, 253),
            Field = Color.FromArgb(255, 243, 246, 250),
            SubtleField = Color.FromArgb(255, 234, 239, 245),
            Border = Color.FromArgb(255, 206, 214, 224),
            SoftBorder = Color.FromArgb(255, 220, 226, 235),
            Text = Color.FromArgb(255, 24, 30, 38),
            MutedText = Color.FromArgb(255, 92, 102, 115),
            SubtleText = Color.FromArgb(255, 92, 102, 115),
            Accent = Color.FromArgb(255, 220, 38, 24),
            AccentSoft = Color.FromArgb(255, 254, 228, 225),
            AccentText = Color.FromArgb(255, 162, 24, 14),
            Secondary = Color.FromArgb(255, 44, 62, 82),
            WarningField = Color.FromArgb(255, 254, 245, 220),
            WarningBorder = Color.FromArgb(255, 234, 192, 98),
            WarningText = Color.FromArgb(255, 138, 84, 12)
        };
    }
}

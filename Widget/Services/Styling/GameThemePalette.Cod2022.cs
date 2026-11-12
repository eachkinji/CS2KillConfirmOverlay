using Windows.UI;

namespace KillConfirmGameBar.Services
{
    internal sealed partial class GameThemePalette
    {
        public static readonly GameThemePalette Cod2022 = new GameThemePalette
        {
            Shell = Color.FromArgb(255, 246, 248, 245),
            Panel = Color.FromArgb(255, 251, 253, 250),
            Card = Color.FromArgb(255, 249, 252, 248),
            Field = Color.FromArgb(255, 244, 248, 242),
            SubtleField = Color.FromArgb(255, 233, 241, 227),
            Border = Color.FromArgb(255, 203, 220, 189),
            SoftBorder = Color.FromArgb(255, 217, 230, 206),
            Text = Color.FromArgb(255, 24, 31, 24),
            MutedText = Color.FromArgb(255, 92, 102, 92),
            SubtleText = Color.FromArgb(255, 92, 102, 92),
            Accent = Color.FromArgb(255, 110, 184, 20),
            AccentSoft = Color.FromArgb(255, 232, 247, 205),
            AccentText = Color.FromArgb(255, 58, 104, 8),
            Secondary = Color.FromArgb(255, 42, 54, 44),
            WarningField = Color.FromArgb(255, 247, 251, 232),
            WarningBorder = Color.FromArgb(255, 189, 216, 114),
            WarningText = Color.FromArgb(255, 77, 102, 14)
        };
    }
}

using Windows.UI;

namespace KillConfirmGameBar.Services
{
    internal sealed partial class GameThemePalette
    {
        public static readonly GameThemePalette Rdr2 = new GameThemePalette
        {
            Shell = Color.FromArgb(255, 249, 244, 242),
            Panel = Color.FromArgb(255, 253, 250, 248),
            Card = Color.FromArgb(255, 252, 248, 246),
            Field = Color.FromArgb(255, 247, 241, 238),
            SubtleField = Color.FromArgb(255, 241, 230, 227),
            Border = Color.FromArgb(255, 226, 198, 194),
            SoftBorder = Color.FromArgb(255, 235, 214, 210),
            Text = Color.FromArgb(255, 34, 22, 20),
            MutedText = Color.FromArgb(255, 110, 88, 84),
            SubtleText = Color.FromArgb(255, 110, 88, 84),
            Accent = Color.FromArgb(255, 200, 29, 37),
            AccentSoft = Color.FromArgb(255, 252, 226, 228),
            AccentText = Color.FromArgb(255, 152, 16, 24),
            Secondary = Color.FromArgb(255, 58, 36, 32),
            WarningField = Color.FromArgb(255, 254, 245, 224),
            WarningBorder = Color.FromArgb(255, 228, 190, 108),
            WarningText = Color.FromArgb(255, 134, 82, 12)
        };
    }
}

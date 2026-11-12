using Windows.UI;

namespace KillConfirmGameBar.Services
{
    internal sealed partial class GameThemePalette
    {
        public static readonly GameThemePalette LeagueOfLegends = new GameThemePalette
        {
            Shell = Color.FromArgb(255, 244, 247, 250),
            Panel = Color.FromArgb(255, 249, 252, 254),
            Card = Color.FromArgb(255, 247, 250, 253),
            Field = Color.FromArgb(255, 240, 246, 251),
            SubtleField = Color.FromArgb(255, 228, 239, 248),
            Border = Color.FromArgb(255, 186, 211, 229),
            SoftBorder = Color.FromArgb(255, 206, 223, 236),
            Text = Color.FromArgb(255, 20, 30, 40),
            MutedText = Color.FromArgb(255, 86, 102, 118),
            SubtleText = Color.FromArgb(255, 86, 102, 118),
            Accent = Color.FromArgb(255, 11, 120, 168),
            AccentSoft = Color.FromArgb(255, 221, 240, 250),
            AccentText = Color.FromArgb(255, 8, 92, 132),
            Secondary = Color.FromArgb(255, 178, 132, 42),
            WarningField = Color.FromArgb(255, 252, 247, 232),
            WarningBorder = Color.FromArgb(255, 214, 182, 106),
            WarningText = Color.FromArgb(255, 126, 88, 18)
        };
    }
}

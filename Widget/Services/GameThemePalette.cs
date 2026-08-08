using Windows.UI;
using Windows.UI.Xaml.Media;

namespace KillConfirmGameBar.Services
{
    internal sealed class GameThemePalette
    {
        public Color Shell { get; set; }
        public Color Panel { get; set; }
        public Color Card { get; set; }
        public Color Field { get; set; }
        public Color SubtleField { get; set; }
        public Color Border { get; set; }
        public Color SoftBorder { get; set; }
        public Color Text { get; set; }
        public Color MutedText { get; set; }
        public Color SubtleText { get; set; }
        public Color Accent { get; set; }
        public Color AccentSoft { get; set; }
        public Color AccentText { get; set; }
        public Color Secondary { get; set; }
        public Color WarningField { get; set; }
        public Color WarningBorder { get; set; }
        public Color WarningText { get; set; }

        public SolidColorBrush Brush(Color color) => new SolidColorBrush(color);

        public static GameThemePalette Current => ForMode(GameStyleService.Current);

        public static GameThemePalette ForMode(GameStyleMode mode)
        {
            switch (mode)
            {
                case GameStyleMode.Valorant:
                    return Valorant;
                case GameStyleMode.Battlefield1:
                    return Battlefield1;
                case GameStyleMode.Battlefield5:
                    return Battlefield5;
                case GameStyleMode.Battlefield4:
                    return Battlefield4;
                case GameStyleMode.Battlefield2042:
                    return Battlefield2042;
                case GameStyleMode.Pubg:
                    return Pubg;
                case GameStyleMode.DeltaForce:
                    return DeltaForce;
                case GameStyleMode.Crossfire:
                default:
                    return Crossfire;
            }
        }

        public static readonly GameThemePalette Crossfire = new GameThemePalette
        {
            Shell = Color.FromArgb(255, 242, 243, 242),
            Panel = Color.FromArgb(244, 250, 250, 247),
            Card = Color.FromArgb(255, 250, 250, 247),
            Field = Color.FromArgb(255, 255, 253, 252),
            SubtleField = Color.FromArgb(255, 243, 248, 251),
            Border = Color.FromArgb(255, 226, 221, 211),
            SoftBorder = Color.FromArgb(255, 213, 208, 196),
            Text = Color.FromArgb(255, 27, 31, 49),
            MutedText = Color.FromArgb(255, 75, 85, 99),
            SubtleText = Color.FromArgb(255, 113, 115, 122),
            Accent = Color.FromArgb(255, 245, 158, 11),
            AccentSoft = Color.FromArgb(255, 255, 247, 234),
            AccentText = Color.FromArgb(255, 138, 75, 0),
            Secondary = Color.FromArgb(255, 46, 136, 184),
            WarningField = Color.FromArgb(255, 255, 247, 234),
            WarningBorder = Color.FromArgb(255, 247, 190, 106),
            WarningText = Color.FromArgb(255, 138, 75, 0)
        };

        public static readonly GameThemePalette Valorant = new GameThemePalette
        {
            Shell = Color.FromArgb(255, 10, 14, 22),
            Panel = Color.FromArgb(246, 13, 19, 29),
            Card = Color.FromArgb(255, 19, 27, 40),
            Field = Color.FromArgb(255, 10, 15, 24),
            SubtleField = Color.FromArgb(255, 24, 34, 50),
            Border = Color.FromArgb(255, 255, 70, 85),
            SoftBorder = Color.FromArgb(255, 65, 78, 96),
            Text = Color.FromArgb(255, 236, 232, 225),
            MutedText = Color.FromArgb(255, 177, 185, 196),
            SubtleText = Color.FromArgb(255, 134, 146, 160),
            Accent = Color.FromArgb(255, 255, 70, 85),
            AccentSoft = Color.FromArgb(255, 51, 26, 36),
            AccentText = Color.FromArgb(255, 255, 202, 207),
            Secondary = Color.FromArgb(255, 0, 216, 190),
            WarningField = Color.FromArgb(255, 43, 35, 28),
            WarningBorder = Color.FromArgb(255, 212, 137, 64),
            WarningText = Color.FromArgb(255, 255, 202, 136)
        };

        public static readonly GameThemePalette Battlefield1 = new GameThemePalette
        {
            Shell = Color.FromArgb(255, 12, 21, 28),
            Panel = Color.FromArgb(246, 18, 31, 40),
            Card = Color.FromArgb(255, 30, 44, 54),
            Field = Color.FromArgb(255, 15, 27, 36),
            SubtleField = Color.FromArgb(255, 45, 60, 70),
            Border = Color.FromArgb(255, 242, 143, 50),
            SoftBorder = Color.FromArgb(255, 96, 118, 132),
            Text = Color.FromArgb(255, 250, 253, 250),
            MutedText = Color.FromArgb(255, 213, 225, 230),
            SubtleText = Color.FromArgb(255, 157, 174, 184),
            Accent = Color.FromArgb(255, 242, 126, 38),
            AccentSoft = Color.FromArgb(255, 58, 38, 28),
            AccentText = Color.FromArgb(255, 255, 218, 168),
            Secondary = Color.FromArgb(255, 157, 184, 202),
            WarningField = Color.FromArgb(255, 67, 42, 28),
            WarningBorder = Color.FromArgb(255, 255, 163, 74),
            WarningText = Color.FromArgb(255, 255, 218, 166)
        };

        public static readonly GameThemePalette Battlefield5 = new GameThemePalette
        {
            Shell = Color.FromArgb(255, 5, 21, 38),
            Panel = Color.FromArgb(246, 8, 31, 52),
            Card = Color.FromArgb(255, 12, 43, 66),
            Field = Color.FromArgb(255, 5, 26, 44),
            SubtleField = Color.FromArgb(255, 15, 58, 86),
            Border = Color.FromArgb(255, 84, 216, 237),
            SoftBorder = Color.FromArgb(255, 62, 121, 148),
            Text = Color.FromArgb(255, 235, 252, 255),
            MutedText = Color.FromArgb(255, 185, 229, 238),
            SubtleText = Color.FromArgb(255, 134, 187, 201),
            Accent = Color.FromArgb(255, 255, 90, 56),
            AccentSoft = Color.FromArgb(255, 60, 29, 45),
            AccentText = Color.FromArgb(255, 255, 208, 190),
            Secondary = Color.FromArgb(255, 0, 211, 255),
            WarningField = Color.FromArgb(255, 55, 32, 35),
            WarningBorder = Color.FromArgb(255, 255, 112, 72),
            WarningText = Color.FromArgb(255, 255, 205, 181)
        };

        public static readonly GameThemePalette Battlefield4 = new GameThemePalette
        {
            Shell = Color.FromArgb(255, 10, 19, 28),
            Panel = Color.FromArgb(246, 13, 28, 42),
            Card = Color.FromArgb(255, 20, 39, 58),
            Field = Color.FromArgb(255, 9, 24, 38),
            SubtleField = Color.FromArgb(255, 32, 57, 78),
            Border = Color.FromArgb(255, 77, 158, 214),
            SoftBorder = Color.FromArgb(255, 72, 104, 130),
            Text = Color.FromArgb(255, 234, 245, 252),
            MutedText = Color.FromArgb(255, 185, 211, 226),
            SubtleText = Color.FromArgb(255, 132, 162, 181),
            Accent = Color.FromArgb(255, 244, 137, 38),
            AccentSoft = Color.FromArgb(255, 54, 34, 22),
            AccentText = Color.FromArgb(255, 255, 218, 170),
            Secondary = Color.FromArgb(255, 66, 170, 226),
            WarningField = Color.FromArgb(255, 56, 37, 26),
            WarningBorder = Color.FromArgb(255, 246, 155, 65),
            WarningText = Color.FromArgb(255, 255, 216, 160)
        };

        public static readonly GameThemePalette Pubg = new GameThemePalette
        {
            Shell = Color.FromArgb(255, 20, 18, 12),
            Panel = Color.FromArgb(246, 31, 29, 21),
            Card = Color.FromArgb(255, 43, 39, 27),
            Field = Color.FromArgb(255, 25, 23, 16),
            SubtleField = Color.FromArgb(255, 67, 59, 37),
            Border = Color.FromArgb(255, 224, 161, 58),
            SoftBorder = Color.FromArgb(255, 116, 98, 62),
            Text = Color.FromArgb(255, 248, 244, 229),
            MutedText = Color.FromArgb(255, 219, 206, 171),
            SubtleText = Color.FromArgb(255, 159, 144, 105),
            Accent = Color.FromArgb(255, 242, 177, 53),
            AccentSoft = Color.FromArgb(255, 69, 50, 18),
            AccentText = Color.FromArgb(255, 255, 231, 176),
            Secondary = Color.FromArgb(255, 112, 168, 90),
            WarningField = Color.FromArgb(255, 67, 38, 24),
            WarningBorder = Color.FromArgb(255, 231, 113, 57),
            WarningText = Color.FromArgb(255, 255, 199, 148)
        };

        public static readonly GameThemePalette Battlefield2042 = new GameThemePalette
        {
            Shell = Color.FromArgb(255, 5, 15, 21),
            Panel = Color.FromArgb(246, 8, 25, 34),
            Card = Color.FromArgb(255, 11, 34, 45),
            Field = Color.FromArgb(255, 6, 20, 28),
            SubtleField = Color.FromArgb(255, 17, 58, 72),
            Border = Color.FromArgb(255, 30, 221, 221),
            SoftBorder = Color.FromArgb(255, 54, 116, 130),
            Text = Color.FromArgb(255, 236, 255, 255),
            MutedText = Color.FromArgb(255, 181, 226, 229),
            SubtleText = Color.FromArgb(255, 123, 174, 181),
            Accent = Color.FromArgb(255, 255, 48, 72),
            AccentSoft = Color.FromArgb(255, 58, 19, 28),
            AccentText = Color.FromArgb(255, 255, 205, 212),
            Secondary = Color.FromArgb(255, 34, 221, 221),
            WarningField = Color.FromArgb(255, 59, 37, 22),
            WarningBorder = Color.FromArgb(255, 255, 158, 64),
            WarningText = Color.FromArgb(255, 255, 217, 166)
        };

        public static readonly GameThemePalette DeltaForce = new GameThemePalette
        {
            Shell = Color.FromArgb(255, 10, 19, 17),
            Panel = Color.FromArgb(246, 14, 29, 25),
            Card = Color.FromArgb(255, 20, 42, 36),
            Field = Color.FromArgb(255, 9, 25, 21),
            SubtleField = Color.FromArgb(255, 31, 62, 52),
            Border = Color.FromArgb(255, 255, 174, 75),
            SoftBorder = Color.FromArgb(255, 84, 125, 102),
            Text = Color.FromArgb(255, 241, 255, 246),
            MutedText = Color.FromArgb(255, 197, 226, 205),
            SubtleText = Color.FromArgb(255, 141, 178, 153),
            Accent = Color.FromArgb(255, 255, 174, 75),
            AccentSoft = Color.FromArgb(255, 62, 44, 22),
            AccentText = Color.FromArgb(255, 255, 230, 180),
            Secondary = Color.FromArgb(255, 70, 220, 146),
            WarningField = Color.FromArgb(255, 62, 39, 24),
            WarningBorder = Color.FromArgb(255, 255, 126, 75),
            WarningText = Color.FromArgb(255, 255, 210, 177)
        };
    }
}

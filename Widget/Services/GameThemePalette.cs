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
                case GameStyleMode.Doubao:
                    return Doubao;
                case GameStyleMode.Dagoujiao:
                    return Dagoujiao;
                case GameStyleMode.Csol:
                    return Csol;
                case GameStyleMode.Crossfire:
                default:
                    return Crossfire;
            }
        }

        public static readonly GameThemePalette Home = new GameThemePalette
        {
            Shell = Color.FromArgb(255, 249, 249, 249),
            Panel = Color.FromArgb(255, 255, 255, 255),
            Card = Color.FromArgb(255, 255, 255, 255),
            Field = Color.FromArgb(255, 250, 250, 250),
            SubtleField = Color.FromArgb(255, 243, 243, 243),
            Border = Color.FromArgb(255, 229, 229, 229),
            SoftBorder = Color.FromArgb(255, 229, 229, 229),
            Text = Color.FromArgb(255, 27, 27, 27),
            MutedText = Color.FromArgb(255, 97, 97, 97),
            SubtleText = Color.FromArgb(255, 140, 140, 140),
            Accent = Color.FromArgb(255, 0, 103, 192),
            AccentSoft = Color.FromArgb(255, 235, 243, 252),
            AccentText = Color.FromArgb(255, 0, 103, 192),
            Secondary = Color.FromArgb(255, 14, 112, 144),
            WarningField = Color.FromArgb(255, 255, 248, 225),
            WarningBorder = Color.FromArgb(255, 255, 224, 130),
            WarningText = Color.FromArgb(255, 133, 77, 14)
        };

        public static readonly GameThemePalette Crossfire = new GameThemePalette
        {
            Shell = Color.FromArgb(255, 249, 249, 249),
            Panel = Color.FromArgb(255, 255, 255, 255),
            Card = Color.FromArgb(255, 255, 255, 255),
            Field = Color.FromArgb(255, 250, 250, 250),
            SubtleField = Color.FromArgb(255, 243, 243, 243),
            Border = Color.FromArgb(255, 229, 229, 229),
            SoftBorder = Color.FromArgb(255, 229, 229, 229),
            Text = Color.FromArgb(255, 27, 27, 27),
            MutedText = Color.FromArgb(255, 97, 97, 97),
            SubtleText = Color.FromArgb(255, 140, 140, 140),
            Accent = Color.FromArgb(255, 217, 119, 6),
            AccentSoft = Color.FromArgb(255, 254, 243, 199),
            AccentText = Color.FromArgb(255, 146, 64, 14),
            Secondary = Color.FromArgb(255, 46, 136, 184),
            WarningField = Color.FromArgb(255, 254, 243, 199),
            WarningBorder = Color.FromArgb(255, 251, 191, 36),
            WarningText = Color.FromArgb(255, 146, 64, 14)
        };

        public static readonly GameThemePalette Csol = new GameThemePalette
        {
            Shell = Color.FromArgb(255, 249, 249, 249),
            Panel = Color.FromArgb(255, 255, 255, 255),
            Card = Color.FromArgb(255, 255, 255, 255),
            Field = Color.FromArgb(255, 250, 250, 250),
            SubtleField = Color.FromArgb(255, 243, 243, 243),
            Border = Color.FromArgb(255, 229, 229, 229),
            SoftBorder = Color.FromArgb(255, 229, 229, 229),
            Text = Color.FromArgb(255, 27, 27, 27),
            MutedText = Color.FromArgb(255, 97, 97, 97),
            SubtleText = Color.FromArgb(255, 140, 140, 140),
            Accent = Color.FromArgb(255, 220, 38, 38),
            AccentSoft = Color.FromArgb(255, 254, 226, 226),
            AccentText = Color.FromArgb(255, 153, 27, 27),
            Secondary = Color.FromArgb(255, 234, 88, 12),
            WarningField = Color.FromArgb(255, 254, 242, 242),
            WarningBorder = Color.FromArgb(255, 252, 165, 165),
            WarningText = Color.FromArgb(255, 153, 27, 27)
        };

        public static readonly GameThemePalette Valorant = new GameThemePalette
        {
            Shell = Color.FromArgb(255, 249, 249, 249),
            Panel = Color.FromArgb(255, 255, 255, 255),
            Card = Color.FromArgb(255, 255, 255, 255),
            Field = Color.FromArgb(255, 250, 250, 250),
            SubtleField = Color.FromArgb(255, 243, 243, 243),
            Border = Color.FromArgb(255, 229, 229, 229),
            SoftBorder = Color.FromArgb(255, 229, 229, 229),
            Text = Color.FromArgb(255, 27, 27, 27),
            MutedText = Color.FromArgb(255, 97, 97, 97),
            SubtleText = Color.FromArgb(255, 140, 140, 140),
            Accent = Color.FromArgb(255, 255, 70, 85),
            AccentSoft = Color.FromArgb(255, 255, 228, 230),
            AccentText = Color.FromArgb(255, 159, 18, 57),
            Secondary = Color.FromArgb(255, 13, 148, 136),
            WarningField = Color.FromArgb(255, 255, 241, 242),
            WarningBorder = Color.FromArgb(255, 253, 164, 175),
            WarningText = Color.FromArgb(255, 159, 18, 57)
        };

        public static readonly GameThemePalette Battlefield1 = new GameThemePalette
        {
            Shell = Color.FromArgb(255, 249, 249, 249),
            Panel = Color.FromArgb(255, 255, 255, 255),
            Card = Color.FromArgb(255, 255, 255, 255),
            Field = Color.FromArgb(255, 250, 250, 250),
            SubtleField = Color.FromArgb(255, 243, 243, 243),
            Border = Color.FromArgb(255, 229, 229, 229),
            SoftBorder = Color.FromArgb(255, 229, 229, 229),
            Text = Color.FromArgb(255, 27, 27, 27),
            MutedText = Color.FromArgb(255, 97, 97, 97),
            SubtleText = Color.FromArgb(255, 140, 140, 140),
            Accent = Color.FromArgb(255, 217, 119, 6),
            AccentSoft = Color.FromArgb(255, 254, 243, 199),
            AccentText = Color.FromArgb(255, 146, 64, 14),
            Secondary = Color.FromArgb(255, 71, 85, 105),
            WarningField = Color.FromArgb(255, 254, 243, 199),
            WarningBorder = Color.FromArgb(255, 251, 191, 36),
            WarningText = Color.FromArgb(255, 146, 64, 14)
        };

        public static readonly GameThemePalette Battlefield5 = new GameThemePalette
        {
            Shell = Color.FromArgb(255, 249, 249, 249),
            Panel = Color.FromArgb(255, 255, 255, 255),
            Card = Color.FromArgb(255, 255, 255, 255),
            Field = Color.FromArgb(255, 250, 250, 250),
            SubtleField = Color.FromArgb(255, 243, 243, 243),
            Border = Color.FromArgb(255, 229, 229, 229),
            SoftBorder = Color.FromArgb(255, 229, 229, 229),
            Text = Color.FromArgb(255, 27, 27, 27),
            MutedText = Color.FromArgb(255, 97, 97, 97),
            SubtleText = Color.FromArgb(255, 140, 140, 140),
            Accent = Color.FromArgb(255, 8, 145, 178),
            AccentSoft = Color.FromArgb(255, 236, 254, 255),
            AccentText = Color.FromArgb(255, 21, 94, 117),
            Secondary = Color.FromArgb(255, 249, 115, 22),
            WarningField = Color.FromArgb(255, 255, 237, 213),
            WarningBorder = Color.FromArgb(255, 253, 186, 116),
            WarningText = Color.FromArgb(255, 154, 52, 18)
        };

        public static readonly GameThemePalette Battlefield4 = new GameThemePalette
        {
            Shell = Color.FromArgb(255, 249, 249, 249),
            Panel = Color.FromArgb(255, 255, 255, 255),
            Card = Color.FromArgb(255, 255, 255, 255),
            Field = Color.FromArgb(255, 250, 250, 250),
            SubtleField = Color.FromArgb(255, 243, 243, 243),
            Border = Color.FromArgb(255, 229, 229, 229),
            SoftBorder = Color.FromArgb(255, 229, 229, 229),
            Text = Color.FromArgb(255, 27, 27, 27),
            MutedText = Color.FromArgb(255, 97, 97, 97),
            SubtleText = Color.FromArgb(255, 140, 140, 140),
            Accent = Color.FromArgb(255, 234, 88, 12),
            AccentSoft = Color.FromArgb(255, 255, 237, 213),
            AccentText = Color.FromArgb(255, 154, 52, 18),
            Secondary = Color.FromArgb(255, 2, 132, 199),
            WarningField = Color.FromArgb(255, 255, 237, 213),
            WarningBorder = Color.FromArgb(255, 253, 186, 116),
            WarningText = Color.FromArgb(255, 154, 52, 18)
        };

        public static readonly GameThemePalette Pubg = new GameThemePalette
        {
            Shell = Color.FromArgb(255, 249, 249, 249),
            Panel = Color.FromArgb(255, 255, 255, 255),
            Card = Color.FromArgb(255, 255, 255, 255),
            Field = Color.FromArgb(255, 250, 250, 250),
            SubtleField = Color.FromArgb(255, 243, 243, 243),
            Border = Color.FromArgb(255, 229, 229, 229),
            SoftBorder = Color.FromArgb(255, 229, 229, 229),
            Text = Color.FromArgb(255, 27, 27, 27),
            MutedText = Color.FromArgb(255, 97, 97, 97),
            SubtleText = Color.FromArgb(255, 140, 140, 140),
            Accent = Color.FromArgb(255, 202, 138, 4),
            AccentSoft = Color.FromArgb(255, 254, 249, 195),
            AccentText = Color.FromArgb(255, 133, 77, 14),
            Secondary = Color.FromArgb(255, 22, 163, 74),
            WarningField = Color.FromArgb(255, 254, 249, 195),
            WarningBorder = Color.FromArgb(255, 250, 204, 21),
            WarningText = Color.FromArgb(255, 133, 77, 14)
        };

        public static readonly GameThemePalette Battlefield2042 = new GameThemePalette
        {
            Shell = Color.FromArgb(255, 249, 249, 249),
            Panel = Color.FromArgb(255, 255, 255, 255),
            Card = Color.FromArgb(255, 255, 255, 255),
            Field = Color.FromArgb(255, 250, 250, 250),
            SubtleField = Color.FromArgb(255, 243, 243, 243),
            Border = Color.FromArgb(255, 229, 229, 229),
            SoftBorder = Color.FromArgb(255, 229, 229, 229),
            Text = Color.FromArgb(255, 27, 27, 27),
            MutedText = Color.FromArgb(255, 97, 97, 97),
            SubtleText = Color.FromArgb(255, 140, 140, 140),
            Accent = Color.FromArgb(255, 6, 182, 212),
            AccentSoft = Color.FromArgb(255, 236, 254, 255),
            AccentText = Color.FromArgb(255, 21, 94, 117),
            Secondary = Color.FromArgb(255, 239, 68, 68),
            WarningField = Color.FromArgb(255, 254, 242, 242),
            WarningBorder = Color.FromArgb(255, 252, 165, 165),
            WarningText = Color.FromArgb(255, 153, 27, 27)
        };

        public static readonly GameThemePalette DeltaForce = new GameThemePalette
        {
            Shell = Color.FromArgb(255, 249, 249, 249),
            Panel = Color.FromArgb(255, 255, 255, 255),
            Card = Color.FromArgb(255, 255, 255, 255),
            Field = Color.FromArgb(255, 250, 250, 250),
            SubtleField = Color.FromArgb(255, 243, 243, 243),
            Border = Color.FromArgb(255, 229, 229, 229),
            SoftBorder = Color.FromArgb(255, 229, 229, 229),
            Text = Color.FromArgb(255, 27, 27, 27),
            MutedText = Color.FromArgb(255, 97, 97, 97),
            SubtleText = Color.FromArgb(255, 140, 140, 140),
            Accent = Color.FromArgb(255, 22, 163, 74),
            AccentSoft = Color.FromArgb(255, 220, 252, 231),
            AccentText = Color.FromArgb(255, 22, 101, 52),
            Secondary = Color.FromArgb(255, 217, 119, 6),
            WarningField = Color.FromArgb(255, 254, 243, 199),
            WarningBorder = Color.FromArgb(255, 251, 191, 36),
            WarningText = Color.FromArgb(255, 146, 64, 14)
        };

        public static readonly GameThemePalette Doubao = new GameThemePalette
        {
            Shell = Color.FromArgb(255, 249, 249, 249),
            Panel = Color.FromArgb(255, 255, 255, 255),
            Card = Color.FromArgb(255, 255, 255, 255),
            Field = Color.FromArgb(255, 250, 250, 250),
            SubtleField = Color.FromArgb(255, 243, 243, 243),
            Border = Color.FromArgb(255, 229, 229, 229),
            SoftBorder = Color.FromArgb(255, 229, 229, 229),
            Text = Color.FromArgb(255, 27, 27, 27),
            MutedText = Color.FromArgb(255, 97, 97, 97),
            SubtleText = Color.FromArgb(255, 140, 140, 140),
            Accent = Color.FromArgb(255, 217, 119, 6),
            AccentSoft = Color.FromArgb(255, 254, 243, 199),
            AccentText = Color.FromArgb(255, 146, 64, 14),
            Secondary = Color.FromArgb(255, 234, 88, 12),
            WarningField = Color.FromArgb(255, 254, 243, 199),
            WarningBorder = Color.FromArgb(255, 251, 191, 36),
            WarningText = Color.FromArgb(255, 146, 64, 14)
        };

        public static readonly GameThemePalette Dagoujiao = new GameThemePalette
        {
            Shell = Color.FromArgb(255, 249, 249, 249),
            Panel = Color.FromArgb(255, 255, 255, 255),
            Card = Color.FromArgb(255, 255, 255, 255),
            Field = Color.FromArgb(255, 250, 250, 250),
            SubtleField = Color.FromArgb(255, 243, 243, 243),
            Border = Color.FromArgb(255, 229, 229, 229),
            SoftBorder = Color.FromArgb(255, 229, 229, 229),
            Text = Color.FromArgb(255, 27, 27, 27),
            MutedText = Color.FromArgb(255, 97, 97, 97),
            SubtleText = Color.FromArgb(255, 140, 140, 140),
            Accent = Color.FromArgb(255, 101, 163, 13),
            AccentSoft = Color.FromArgb(255, 236, 252, 203),
            AccentText = Color.FromArgb(255, 63, 98, 18),
            Secondary = Color.FromArgb(255, 217, 119, 6),
            WarningField = Color.FromArgb(255, 254, 243, 199),
            WarningBorder = Color.FromArgb(255, 251, 191, 36),
            WarningText = Color.FromArgb(255, 146, 64, 14)
        };
    }
}

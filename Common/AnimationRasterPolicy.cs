using System;

namespace KillConfirmCompatibility.Contracts
{
    // The selected longest edge controls raster quality. Display zoom and monitor
    // DPI never enter this calculation; the original aspect ratio is retained.
    public static class AnimationRasterPolicy
    {
        public const string SettingKey = "AnimationRasterQuality";
        public const int DefaultEdge = 1920;
        public static int MaxEdge { get; private set; } = DefaultEdge;
        public static int Normalize(object value)
        {
            int edge;
            return int.TryParse(Convert.ToString(value), out edge) &&
                (edge == 1280 || edge == 1600 || edge == 1920 || edge == 2560 || edge == 3200) ? edge : DefaultEdge;
        }
        public static string GameSettingKey(string game) => SettingKey + "." + game.ToLowerInvariant();
        public static int DefaultForGame(string game)
        {
            switch ((game ?? "").ToLowerInvariant())
            {
                case "csol": case "valorant": case "overwatch": case "apex":
                case "calabiyau": case "pubg": case "deltaforce":
                case "battlefield1": case "battlefield4": case "battlefield5":
                case "battlefield6": case "battlefield2042": return 1920;
                default: return 1600;
            }
        }
        public static int ReadForGame(System.Collections.Generic.IDictionary<string, object> settings, string game)
        {
            object value;
            int edge;
            // A former global preference must not override the new game defaults.
            return settings.TryGetValue(GameSettingKey(game), out value)
                && int.TryParse(Convert.ToString(value), out edge)
                && (edge == 1280 || edge == 1600 || edge == 1920 || edge == 2560 || edge == 3200)
                ? edge : DefaultForGame(game);
        }
        public static void Configure(object value) { MaxEdge = Normalize(value); }

        public static double Scale(double logicalWidth, double logicalHeight)
        {
            if (double.IsNaN(logicalWidth) || double.IsInfinity(logicalWidth) || logicalWidth <= 0
                || double.IsNaN(logicalHeight) || double.IsInfinity(logicalHeight) || logicalHeight <= 0)
                throw new ArgumentOutOfRangeException(nameof(logicalWidth), "Canvas dimensions must be finite and positive.");
            return MaxEdge / Math.Max(logicalWidth, logicalHeight);
        }

        public static int Pixels(double logicalLength, double scale) =>
            Math.Max(1, (int)Math.Min(MaxEdge, Math.Ceiling(logicalLength * scale)));
    }
}

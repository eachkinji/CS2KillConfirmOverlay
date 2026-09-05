using System;

namespace KillConfirmGameBar.Controls
{
    // Parameters and layout transcribed from D:\icon\engine, not its stale README.
    // Rendering samples elapsed time directly rather than counting delivered frames.
    internal static class CrossfireStyle2Motion
    {
        public const double DurationMs = 1275;
        public const double Unit = 360.0 / 158;

        internal struct State
        {
            public double Scale, Alpha, X, Y;
        }

        internal struct Layout
        {
            public double Width, Height, X, Y;
            public Layout(double width, double height, double x = 0, double y = 0)
            { Width = width; Height = height; X = x; Y = y; }
        }

        public static State Sample(double elapsedMs, bool flame = false)
        {
            double limit = flame ? 2.0 : 1.4;
            double startMs = Math.Ceiling(limit / 0.33) * 15;
            double rumbleEnd = startMs + (flame ? 100 : 200);
            double elapsed = Math.Max(0, elapsedMs);
            if (elapsed < startMs)
                return new State { Scale = Math.Min(Math.Floor(elapsed / 15) * 0.33, limit) / limit, Alpha = 1 };
            if (elapsed < rumbleEnd)
            {
                const double sampleMs = 1000.0 / 60;
                double local = elapsed - startMs;
                int index = (int)Math.Floor(local / sampleMs);
                double fraction = (local % sampleMs) / sampleMs;
                uint seed = unchecked((uint)((index & 1) == 0 ? index : index - 1));
                uint random = unchecked(seed * 1103515245u + 12345u);
                double amount = (index & 1) == 0 ? fraction : 1 - fraction;
                return new State { Scale = 1, Alpha = 1,
                    X = ((int)(random % 5) - 5) * amount,
                    Y = ((int)((random >> 8) % 5) - 5) * amount };
            }
            return new State { Scale = 1, Alpha = Math.Max(0,
                1 - Math.Floor((elapsed - rumbleEnd) / 20) * (flame ? 0.15 : 0.02)) };
        }

        public static Layout MainLayout(string action)
        {
            switch (action)
            {
                case "knife": return new Layout(116, 170, 0, -7);
                case "grenade": return new Layout(140, 140, 0, 5);
                case "c4": case "bomb_plant": return new Layout(150, 150, 0, -15);
                case "c4defuse": case "bomb_defuse": return new Layout(130, 140, 0, -15);
                case "firstkill": case "lastkill": return new Layout(241, 33, 0, -100);
                case "revenge": return new Layout(185, 48, -0.5, -82);
                default: return new Layout(158, 158);
            }
        }
    }
}

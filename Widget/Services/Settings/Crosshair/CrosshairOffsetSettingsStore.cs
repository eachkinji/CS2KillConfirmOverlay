using Windows.Storage;

namespace KillConfirmGameBar.Services
{
    internal struct CrosshairOffset
    {
        public double X;
        public double Y;

        public CrosshairOffset(double x, double y)
        {
            X = x;
            Y = y;
        }
    }

    internal static class CrosshairOffsetSettingsStore
    {
        // Applied as an extra translation on the crosshair layer only.
        public const double DefaultOffsetX = 0.0;
        public const double DefaultOffsetY = 0.0;

        private const string DefaultXKey = "CrosshairOffsetX";
        private const string DefaultYKey = "CrosshairOffsetY";

        public static bool IsSupported(GameStyleMode style)
        {
            return GameStyleService.SupportsCrosshairAreaEffect(style);
        }

        public static CrosshairOffset Load(GameStyleMode style)
        {
            if (!IsSupported(style))
            {
                return new CrosshairOffset(0.0, 0.0);
            }

            return new CrosshairOffset(Read(style, true), Read(style, false));
        }

        public static void Save(GameStyleMode style, double x, double y)
        {
            if (!IsSupported(style))
            {
                return;
            }

            Write(style, true, x);
            Write(style, false, y);
        }

        private static double Read(GameStyleMode style, bool xAxis)
        {
            string key = GetKey(style, xAxis);
            if (key == null)
            {
                return 0.0;
            }

            object value = ApplicationData.Current.LocalSettings.Values[key];
            if (value is double number)
            {
                return number;
            }

            if (value is string text
                && double.TryParse(
                    text,
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out double parsed))
            {
                return parsed;
            }

            return xAxis ? DefaultOffsetX : DefaultOffsetY;
        }

        private static void Write(GameStyleMode style, bool xAxis, double value)
        {
            string key = GetKey(style, xAxis);
            if (key != null)
            {
                ApplicationData.Current.LocalSettings.Values[key] = value;
            }
        }

        private static string GetKey(GameStyleMode style, bool xAxis)
        {
            if (!IsSupported(style))
            {
                return null;
            }

            return GameStyleService.ToStorageValue(style)
                + "."
                + (xAxis ? DefaultXKey : DefaultYKey);
        }
    }
}

using System;
using System.Globalization;
using Windows.Storage;
using Windows.UI;

namespace KillConfirmGameBar.Services
{
    internal sealed class PanelColorSettingsValues
    {
        public bool Enabled { get; set; }
        public string BackgroundColorHex { get; set; } = PanelColorSettingsStore.DefaultBackgroundHex;
        public string BorderColorHex { get; set; } = PanelColorSettingsStore.DefaultBorderHex;
    }

    internal static class PanelColorSettingsStore
    {
        private const string EnabledKey = "PanelColor.CustomEnabled";
        private const string BackgroundHexKey = "PanelColor.BackgroundHex";
        private const string BorderHexKey = "PanelColor.BorderHex";

        public const string DefaultBackgroundHex = "#E61E222D";
        public const string DefaultBorderHex = "#33FFFFFF";

        public static event EventHandler Changed;

        public static PanelColorSettingsValues Load()
        {
            var values = ApplicationData.Current.LocalSettings.Values;
            return new PanelColorSettingsValues
            {
                Enabled = ReadBool(values[EnabledKey], false),
                BackgroundColorHex = ReadString(values[BackgroundHexKey], DefaultBackgroundHex),
                BorderColorHex = ReadString(values[BorderHexKey], DefaultBorderHex)
            };
        }

        public static void Save(bool enabled, string backgroundHex, string borderHex)
        {
            var values = ApplicationData.Current.LocalSettings.Values;
            values[EnabledKey] = enabled;
            values[BackgroundHexKey] = string.IsNullOrWhiteSpace(backgroundHex) ? DefaultBackgroundHex : backgroundHex.Trim();
            values[BorderHexKey] = string.IsNullOrWhiteSpace(borderHex) ? DefaultBorderHex : borderHex.Trim();
            Changed?.Invoke(null, EventArgs.Empty);
        }

        public static void Reset()
        {
            var values = ApplicationData.Current.LocalSettings.Values;
            values[EnabledKey] = false;
            values[BackgroundHexKey] = DefaultBackgroundHex;
            values[BorderHexKey] = DefaultBorderHex;
            Changed?.Invoke(null, EventArgs.Empty);
        }

        public static bool TryParseHexColor(string hex, out Color color)
        {
            color = Colors.Transparent;
            if (string.IsNullOrWhiteSpace(hex))
            {
                return false;
            }

            string clean = hex.Trim().TrimStart('#');
            if (clean.Length == 6)
            {
                if (uint.TryParse(clean, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint rgb))
                {
                    color = Color.FromArgb(
                        255,
                        (byte)((rgb >> 16) & 0xFF),
                        (byte)((rgb >> 8) & 0xFF),
                        (byte)(rgb & 0xFF));
                    return true;
                }
            }
            else if (clean.Length == 8)
            {
                if (uint.TryParse(clean, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint argb))
                {
                    color = Color.FromArgb(
                        (byte)((argb >> 24) & 0xFF),
                        (byte)((argb >> 16) & 0xFF),
                        (byte)((argb >> 8) & 0xFF),
                        (byte)(argb & 0xFF));
                    return true;
                }
            }

            return false;
        }

        public static string ColorToHex(Color color, bool includeAlpha = true)
        {
            if (includeAlpha)
            {
                return string.Format("#{0:X2}{1:X2}{2:X2}{3:X2}", color.A, color.R, color.G, color.B);
            }
            return string.Format("#{0:X2}{1:X2}{2:X2}", color.R, color.G, color.B);
        }

        private static bool ReadBool(object value, bool fallback)
        {
            if (value is bool b) return b;
            if (value is string s && bool.TryParse(s, out bool parsed)) return parsed;
            return fallback;
        }

        private static string ReadString(object value, string fallback)
        {
            return value as string ?? fallback;
        }
    }
}

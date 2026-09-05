using System;
using System.Globalization;
using Windows.Storage;

namespace KillConfirmGameBar.Services
{
    internal static class ModernWarfare2019FeedOffsetSettingsStore
    {
        public const double DefaultOffset = 0.0;
        public const double MinimumOffset = 0.0;
        public const double MaximumOffset = 300.0;
        public const double OffsetStep = 10.0;

        private const string SettingKey = "ModernWarfare2019.RightFeedOffset";

        public static double Load()
        {
            object value = ApplicationData.Current.LocalSettings.Values[SettingKey];
            if (value is double number)
            {
                return Clamp(number);
            }

            if (value is string text
                && double.TryParse(
                    text,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out double parsed))
            {
                return Clamp(parsed);
            }

            return DefaultOffset;
        }

        public static void Save(double value)
        {
            ApplicationData.Current.LocalSettings.Values[SettingKey] = Clamp(value);
        }

        private static double Clamp(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                return DefaultOffset;
            }

            return Math.Max(MinimumOffset, Math.Min(MaximumOffset, value));
        }
    }
}

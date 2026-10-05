using System;
using System.Globalization;
namespace KillConfirmCompatibility.Services
{
    internal static class SharedStreakSettingsStore
    {
        public static string Normalize(string value)
        {
            value = (value ?? "life").Trim().ToLowerInvariant();
            if (value == "none" || value == "timed_5" || value == "timed_10" || value == "timed_15") return value;
            if (value.StartsWith("loop:") && int.TryParse(value.Substring(5), out int kills)) return "loop:" + Math.Clamp(kills, 2, 50);
            if (value.StartsWith("custom:") && double.TryParse(value.Substring(7), NumberStyles.Float, CultureInfo.InvariantCulture, out double seconds) && double.IsFinite(seconds)) return "custom:" + Math.Clamp(seconds, 0.1, 300).ToString("0.###", CultureInfo.InvariantCulture);
            return "life";
        }
    }
}

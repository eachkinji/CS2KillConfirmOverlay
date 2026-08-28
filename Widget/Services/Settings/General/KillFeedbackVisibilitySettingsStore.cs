using System;
using Windows.Storage;

namespace KillConfirmGameBar.Services
{
    internal sealed class KillFeedbackVisibilitySettingsValues
    {
        public bool CrosshairEnabled { get; set; } = true;
        public bool LowerEnabled { get; set; } = true;
        public bool UpperEnabled { get; set; } = true;
        public double CrosshairBrightnessPercent { get; set; } = 100;
        public double CrosshairContrastPercent { get; set; } = 100;
        public double CrosshairOpacityPercent { get; set; } = 100;
        public double LowerBrightnessPercent { get; set; } = 100;
        public double LowerContrastPercent { get; set; } = 100;
        public double LowerOpacityPercent { get; set; } = 100;
        public double UpperBrightnessPercent { get; set; } = 100;
        public double UpperContrastPercent { get; set; } = 100;
        public double UpperOpacityPercent { get; set; } = 100;
    }

    internal static class KillFeedbackVisibilitySettingsStore
    {
        public static event Action<GameStyleMode> Changed;

        public static KillFeedbackVisibilitySettingsValues Load(GameStyleMode style)
        {
            string prefix = GetPrefix(style);
            if (prefix == null)
            {
                return new KillFeedbackVisibilitySettingsValues();
            }

            var values = ApplicationData.Current.LocalSettings.Values;
            return new KillFeedbackVisibilitySettingsValues
            {
                CrosshairEnabled = ReadBool(
                    values[prefix + "CrosshairEnabled"],
                    DefaultCrosshairEnabled(style)),
                LowerEnabled = ReadBool(values[prefix + "LowerEnabled"], true),
                UpperEnabled = style != GameStyleMode.ModernWarfare2019
                    || ReadBool(values[prefix + "UpperEnabled"], true),
                CrosshairBrightnessPercent = ReadPercent(values[prefix + "CrosshairBrightnessPercent"], 100, 50, 150),
                CrosshairContrastPercent = ReadPercent(values[prefix + "CrosshairContrastPercent"], 100, 50, 150),
                CrosshairOpacityPercent = ReadPercent(values[prefix + "CrosshairOpacityPercent"], 100, 10, 100),
                LowerBrightnessPercent = ReadPercent(values[prefix + "LowerBrightnessPercent"], 100, 50, 150),
                LowerContrastPercent = ReadPercent(values[prefix + "LowerContrastPercent"], 100, 50, 150),
                LowerOpacityPercent = ReadPercent(values[prefix + "LowerOpacityPercent"], 100, 10, 100),
                UpperBrightnessPercent = ReadPercent(values[prefix + "UpperBrightnessPercent"], 100, 50, 150),
                UpperContrastPercent = ReadPercent(values[prefix + "UpperContrastPercent"], 100, 50, 150),
                UpperOpacityPercent = ReadPercent(values[prefix + "UpperOpacityPercent"], 100, 10, 100)
            };
        }

        public static void Save(
            GameStyleMode style,
            KillFeedbackVisibilitySettingsValues settings)
        {
            string prefix = GetPrefix(style);
            if (prefix == null || settings == null)
            {
                return;
            }

            var values = ApplicationData.Current.LocalSettings.Values;
            values[prefix + "CrosshairEnabled"] = settings.CrosshairEnabled;
            values[prefix + "LowerEnabled"] = settings.LowerEnabled;
            if (style == GameStyleMode.ModernWarfare2019)
            {
                values[prefix + "UpperEnabled"] = settings.UpperEnabled;
            }
            values[prefix + "CrosshairBrightnessPercent"] = ClampPercent(settings.CrosshairBrightnessPercent, 50, 150);
            values[prefix + "CrosshairContrastPercent"] = ClampPercent(settings.CrosshairContrastPercent, 50, 150);
            values[prefix + "CrosshairOpacityPercent"] = ClampPercent(settings.CrosshairOpacityPercent, 10, 100);
            values[prefix + "LowerBrightnessPercent"] = ClampPercent(settings.LowerBrightnessPercent, 50, 150);
            values[prefix + "LowerContrastPercent"] = ClampPercent(settings.LowerContrastPercent, 50, 150);
            values[prefix + "LowerOpacityPercent"] = ClampPercent(settings.LowerOpacityPercent, 10, 100);
            values[prefix + "UpperBrightnessPercent"] = ClampPercent(settings.UpperBrightnessPercent, 50, 150);
            values[prefix + "UpperContrastPercent"] = ClampPercent(settings.UpperContrastPercent, 50, 150);
            values[prefix + "UpperOpacityPercent"] = ClampPercent(settings.UpperOpacityPercent, 10, 100);
            Changed?.Invoke(style);
        }

        public static void GetAppearance(
            KillFeedbackVisibilitySettingsValues settings,
            KillFeedbackLayer layer,
            out double brightnessPercent,
            out double contrastPercent,
            out double opacityPercent)
        {
            settings = settings ?? new KillFeedbackVisibilitySettingsValues();
            switch (layer)
            {
                case KillFeedbackLayer.Upper:
                    brightnessPercent = settings.UpperBrightnessPercent;
                    contrastPercent = settings.UpperContrastPercent;
                    opacityPercent = settings.UpperOpacityPercent;
                    break;
                case KillFeedbackLayer.Lower:
                    brightnessPercent = settings.LowerBrightnessPercent;
                    contrastPercent = settings.LowerContrastPercent;
                    opacityPercent = settings.LowerOpacityPercent;
                    break;
                case KillFeedbackLayer.Crosshair:
                default:
                    brightnessPercent = settings.CrosshairBrightnessPercent;
                    contrastPercent = settings.CrosshairContrastPercent;
                    opacityPercent = settings.CrosshairOpacityPercent;
                    break;
            }
        }

        private static string GetPrefix(GameStyleMode style)
        {
            switch (style)
            {
                case GameStyleMode.CustomModule:
                    return "CustomModuleKillFeedback";
                case GameStyleMode.ModernWarfare2019:
                    return "ModernWarfare2019KillFeedback";
                case GameStyleMode.Apex:
                    return "ApexKillFeedback";
                case GameStyleMode.Overwatch:
                    return "OverwatchKillFeedback";
                case GameStyleMode.Battlefield1:
                    return "Battlefield1KillFeedback";
                case GameStyleMode.Battlefield5:
                    return "Battlefield5KillFeedback";
                case GameStyleMode.Battlefield4:
                    return "Battlefield4KillFeedback";
                case GameStyleMode.Battlefield2042:
                    return "Battlefield2042KillFeedback";
                case GameStyleMode.DeltaForce:
                    return "DeltaForceKillFeedback";
                case GameStyleMode.Crossfire:
                    return "CrossfireKillFeedback";
                case GameStyleMode.Pubg:
                    return "PubgKillFeedback";
                case GameStyleMode.Csol:
                    return "CsolKillFeedback";
                case GameStyleMode.Valorant:
                    return "ValorantKillFeedback";
                case GameStyleMode.Doubao:
                    return "DoubaoKillFeedback";
                case GameStyleMode.Dagoujiao:
                    return "DagoujiaoKillFeedback";
                default:
                    return null;
            }
        }

        private static bool DefaultCrosshairEnabled(GameStyleMode style)
        {
            return style != GameStyleMode.Crossfire
                && style != GameStyleMode.CustomModule
                && style != GameStyleMode.Pubg
                && style != GameStyleMode.Csol
                && style != GameStyleMode.Valorant;
        }

        private static bool ReadBool(object value, bool fallback)
        {
            if (value is bool boolValue)
            {
                return boolValue;
            }

            return value is string text && bool.TryParse(text, out bool parsed)
                ? parsed
                : fallback;
        }

        private static double ReadPercent(object value, double fallback, double minimum, double maximum)
        {
            double parsed;
            switch (value)
            {
                case double doubleValue:
                    parsed = doubleValue;
                    break;
                case float floatValue:
                    parsed = floatValue;
                    break;
                case int intValue:
                    parsed = intValue;
                    break;
                case string text when double.TryParse(text, out double textValue):
                    parsed = textValue;
                    break;
                default:
                    parsed = fallback;
                    break;
            }

            return ClampPercent(parsed, minimum, maximum);
        }

        private static double ClampPercent(double value, double minimum, double maximum)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                return 100;
            }

            return System.Math.Max(minimum, System.Math.Min(maximum, value));
        }
    }
}

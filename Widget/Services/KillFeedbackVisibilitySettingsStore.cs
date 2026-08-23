using Windows.Storage;

namespace KillConfirmGameBar.Services
{
    internal sealed class KillFeedbackVisibilitySettingsValues
    {
        public bool CrosshairEnabled { get; set; } = true;
        public bool LowerEnabled { get; set; } = true;
        public bool UpperEnabled { get; set; } = true;
    }

    internal static class KillFeedbackVisibilitySettingsStore
    {
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
                    || ReadBool(values[prefix + "UpperEnabled"], true)
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
        }

        private static string GetPrefix(GameStyleMode style)
        {
            switch (style)
            {
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
    }
}

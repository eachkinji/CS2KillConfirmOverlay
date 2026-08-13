using KillConfirmGameBar.Services;
using Windows.Storage;

namespace KillConfirmGameBar
{
    public sealed partial class KillConfirmWidgetPage
    {
        private static string LoadPackSettingForStyle(
            string legacySettingKey,
            GameStyleMode style,
            string fallback)
        {
            ApplicationDataContainer settings = ApplicationData.Current.LocalSettings;
            string scopedSettingKey = GetPackSettingKey(legacySettingKey, style);
            string value = settings.Values[scopedSettingKey] as string;
            if (IsPackSettingValidForStyle(value, style))
            {
                return value;
            }

            // Migrate the old shared setting once when it belongs to this style.
            value = settings.Values[legacySettingKey] as string;
            if (IsPackSettingValidForStyle(value, style))
            {
                settings.Values[scopedSettingKey] = value;
                return value;
            }

            return fallback;
        }

        private static void SavePackSettingForStyle(
            string legacySettingKey,
            GameStyleMode style,
            string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            ApplicationDataContainer settings = ApplicationData.Current.LocalSettings;
            settings.Values[GetPackSettingKey(legacySettingKey, style)] = value;

            // Keep the original key as a compatibility mirror for older builds.
            settings.Values[legacySettingKey] = value;
        }

        private static string GetPackSettingKey(string legacySettingKey, GameStyleMode style)
        {
            return legacySettingKey + "." + GameStyleService.ToStorageValue(style);
        }

        private static bool IsPackSettingValidForStyle(string value, GameStyleMode style)
        {
            return !string.IsNullOrWhiteSpace(value)
                && GameStyleService.GetStyleForPackKey(value) == style;
        }
    }
}

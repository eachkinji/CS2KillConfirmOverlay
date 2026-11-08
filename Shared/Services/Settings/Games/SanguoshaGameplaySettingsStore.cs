// Shared by Widget (UWP) and CompatibilityHost; extracted from both former copies.
// Per-host lines stay inside #if WINDOWS_UWP blocks. See Tools/SharedSource/extract_shared.py.
using System;
using Windows.Storage;

#if WINDOWS_UWP
namespace KillConfirmGameBar.Services
#else
namespace KillConfirmCompatibility.Services
#endif
{
    public sealed class SanguoshaGameplaySettingsValues
    {
        public bool HeadshotSpecialAudioPriority { get; set; }
        public bool KnifeSpecialAudioPriority { get; set; }
        public bool GrenadeSpecialAudioPriority { get; set; }
        public bool HeadshotSpecialIconPriority { get; set; }
        public bool KnifeSpecialIconPriority { get; set; }
        public bool GrenadeSpecialIconPriority { get; set; }
    }

    internal static class SanguoshaGameplaySettingsStore
    {
        private const string HeadshotSpecialAudioPrioritySettingKey = "SanguoshaHeadshotSpecialAudioPriority";
        private const string KnifeSpecialAudioPrioritySettingKey = "SanguoshaKnifeSpecialAudioPriority";
        private const string GrenadeSpecialAudioPrioritySettingKey = "SanguoshaGrenadeSpecialAudioPriority";
        private const string HeadshotSpecialIconPrioritySettingKey = "SanguoshaHeadshotSpecialIconPriority";
        private const string KnifeSpecialIconPrioritySettingKey = "SanguoshaKnifeSpecialIconPriority";
        private const string GrenadeSpecialIconPrioritySettingKey = "SanguoshaGrenadeSpecialIconPriority";

        public static SanguoshaGameplaySettingsValues Load()
        {
            var values = ApplicationData.Current.LocalSettings.Values;
            return new SanguoshaGameplaySettingsValues
            {
                HeadshotSpecialAudioPriority = ReadBoolean(values[HeadshotSpecialAudioPrioritySettingKey], true),
                KnifeSpecialAudioPriority = ReadBoolean(values[KnifeSpecialAudioPrioritySettingKey], true),
                GrenadeSpecialAudioPriority = ReadBoolean(values[GrenadeSpecialAudioPrioritySettingKey], true),
                HeadshotSpecialIconPriority = ReadBoolean(values[HeadshotSpecialIconPrioritySettingKey], true),
                KnifeSpecialIconPriority = ReadBoolean(values[KnifeSpecialIconPrioritySettingKey], true),
                GrenadeSpecialIconPriority = ReadBoolean(values[GrenadeSpecialIconPrioritySettingKey], true)
            };
        }

        public static void Save(SanguoshaGameplaySettingsValues settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            var values = ApplicationData.Current.LocalSettings.Values;
            values[HeadshotSpecialAudioPrioritySettingKey] = settings.HeadshotSpecialAudioPriority;
            values[KnifeSpecialAudioPrioritySettingKey] = settings.KnifeSpecialAudioPriority;
            values[GrenadeSpecialAudioPrioritySettingKey] = settings.GrenadeSpecialAudioPriority;
            values[HeadshotSpecialIconPrioritySettingKey] = settings.HeadshotSpecialIconPriority;
            values[KnifeSpecialIconPrioritySettingKey] = settings.KnifeSpecialIconPriority;
            values[GrenadeSpecialIconPrioritySettingKey] = settings.GrenadeSpecialIconPriority;
        }

        private static bool ReadBoolean(object value, bool fallback)
        {
            if (value is bool boolValue)
            {
                return boolValue;
            }

            if (value is string text && bool.TryParse(text, out bool parsed))
            {
                return parsed;
            }

            return fallback;
        }
    }
}

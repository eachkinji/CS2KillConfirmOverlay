using System;
using Windows.Storage;

namespace KillConfirmGameBar.Services
{
    internal sealed class CrossfireGameplaySettingsValues
    {
        public string StreakMode { get; set; }
        public bool FirstKillSpecialAudio { get; set; }
        public bool LastKillSpecialAudio { get; set; }
    }

    internal static class CrossfireGameplaySettingsStore
    {
        public const string LifeStreakMode = "life";
        public const string Timed5StreakMode = "timed_5";
        public const string Timed10StreakMode = "timed_10";
        public const string Timed15StreakMode = "timed_15";

        private const string StreakModeSettingKey = "CrossfireStreakMode";
        private const string FirstKillSpecialAudioSettingKey = "CrossfireFirstKillSpecialAudio";
        private const string LastKillSpecialAudioSettingKey = "CrossfireLastKillSpecialAudio";

        public static CrossfireGameplaySettingsValues Load()
        {
            var values = ApplicationData.Current.LocalSettings.Values;
            return new CrossfireGameplaySettingsValues
            {
                StreakMode = NormalizeStreakMode(values[StreakModeSettingKey] as string),
                FirstKillSpecialAudio = ReadBoolean(values[FirstKillSpecialAudioSettingKey], true),
                LastKillSpecialAudio = ReadBoolean(values[LastKillSpecialAudioSettingKey], true)
            };
        }

        public static void Save(CrossfireGameplaySettingsValues settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            var values = ApplicationData.Current.LocalSettings.Values;
            values[StreakModeSettingKey] = NormalizeStreakMode(settings.StreakMode);
            values[FirstKillSpecialAudioSettingKey] = settings.FirstKillSpecialAudio;
            values[LastKillSpecialAudioSettingKey] = settings.LastKillSpecialAudio;
        }

        public static string NormalizeStreakMode(string value)
        {
            return SharedStreakSettingsStore.Normalize(value);
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

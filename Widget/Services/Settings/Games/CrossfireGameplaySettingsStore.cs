using System;
using Windows.Storage;

namespace KillConfirmGameBar.Services
{
    internal sealed class CrossfireGameplaySettingsValues
    {
        public string StreakMode { get; set; }
        public bool FirstKillSpecialAudio { get; set; }
        public bool LastKillSpecialAudio { get; set; }
        public bool HeadshotSpecialAudioPriority { get; set; }
        public bool KnifeSpecialAudioPriority { get; set; }
        public bool GrenadeSpecialAudioPriority { get; set; }
        public bool HeadshotSpecialIconPriority { get; set; }
        public bool KnifeSpecialIconPriority { get; set; }
        public bool GrenadeSpecialIconPriority { get; set; }
        public bool FirstKillEffectEnabled { get; set; }
        public bool LastKillEffectEnabled { get; set; }
        public bool AssistAudioEnabled { get; set; }
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
        private const string HeadshotSpecialAudioPrioritySettingKey = "CrossfireHeadshotSpecialAudioPriority";
        private const string KnifeSpecialAudioPrioritySettingKey = "CrossfireKnifeSpecialAudioPriority";
        private const string GrenadeSpecialAudioPrioritySettingKey = "CrossfireGrenadeSpecialAudioPriority";
        private const string HeadshotSpecialIconPrioritySettingKey = "CrossfireHeadshotSpecialIconPriority";
        private const string KnifeSpecialIconPrioritySettingKey = "CrossfireKnifeSpecialIconPriority";
        private const string GrenadeSpecialIconPrioritySettingKey = "CrossfireGrenadeSpecialIconPriority";
        private const string FirstKillEffectEnabledSettingKey = "CrossfireFirstKillEffectEnabled";
        private const string LastKillEffectEnabledSettingKey = "CrossfireLastKillEffectEnabled";

        public static CrossfireGameplaySettingsValues Load()
        {
            var values = ApplicationData.Current.LocalSettings.Values;
            return new CrossfireGameplaySettingsValues
            {
                StreakMode = NormalizeStreakMode(values[StreakModeSettingKey] as string),
                FirstKillSpecialAudio = ReadBoolean(values[FirstKillSpecialAudioSettingKey], false),
                LastKillSpecialAudio = ReadBoolean(values[LastKillSpecialAudioSettingKey], false),
                HeadshotSpecialAudioPriority = ReadBoolean(values[HeadshotSpecialAudioPrioritySettingKey], false),
                KnifeSpecialAudioPriority = ReadBoolean(values[KnifeSpecialAudioPrioritySettingKey], true),
                GrenadeSpecialAudioPriority = ReadBoolean(values[GrenadeSpecialAudioPrioritySettingKey], true),
                HeadshotSpecialIconPriority = ReadBoolean(values[HeadshotSpecialIconPrioritySettingKey], false),
                KnifeSpecialIconPriority = ReadBoolean(values[KnifeSpecialIconPrioritySettingKey], true),
                GrenadeSpecialIconPriority = ReadBoolean(values[GrenadeSpecialIconPrioritySettingKey], true),
                FirstKillEffectEnabled = ReadBoolean(values[FirstKillEffectEnabledSettingKey], true),
                LastKillEffectEnabled = ReadBoolean(values[LastKillEffectEnabledSettingKey], true),
                AssistAudioEnabled = AssistAudioSettingsStore.Load(GameStyleMode.Crossfire)
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
            values[HeadshotSpecialAudioPrioritySettingKey] = settings.HeadshotSpecialAudioPriority;
            values[KnifeSpecialAudioPrioritySettingKey] = settings.KnifeSpecialAudioPriority;
            values[GrenadeSpecialAudioPrioritySettingKey] = settings.GrenadeSpecialAudioPriority;
            values[HeadshotSpecialIconPrioritySettingKey] = settings.HeadshotSpecialIconPriority;
            values[KnifeSpecialIconPrioritySettingKey] = settings.KnifeSpecialIconPriority;
            values[GrenadeSpecialIconPrioritySettingKey] = settings.GrenadeSpecialIconPriority;
            values[FirstKillEffectEnabledSettingKey] = settings.FirstKillEffectEnabled;
            values[LastKillEffectEnabledSettingKey] = settings.LastKillEffectEnabled;
            AssistAudioSettingsStore.Save(GameStyleMode.Crossfire, settings.AssistAudioEnabled);
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

using System;
using Windows.Storage;

namespace KillConfirmGameBar.Services
{
    internal sealed class CsolVoiceSettingsValues
    {
        public string FirstKillIcon { get; set; } = "firstkill";

        public string LastKillIcon { get; set; } = "revenge";

        /// <summary>true = special voice (headshot/knife) beats the streak voice.</summary>
        public bool SpecialVoicePriority { get; set; }

        /// <summary>true = the final kill uses Revenge.wav; false = normal streak/kill audio.</summary>
        public bool LastKillSpecialAudio { get; set; } = true;
    }

    internal static class CsolVoiceSettingsStore
    {
        public const string RevengeIcon = "revenge";
        public const string FirstKillIcon = "firstkill";

        private const string FirstKillIconSettingKey = "CsolFirstKillIcon";
        private const string LastKillIconSettingKey = "CsolLastKillIcon";
        private const string LegacyFirstLastIconSettingKey = "CsolFirstLastIcon";
        private const string SpecialVoicePrioritySettingKey = "CsolSpecialVoicePriority";
        private const string LastKillSpecialAudioSettingKey = "CsolLastKillSpecialAudio";

        // CSOL voice randomization is now driven purely by the voice pack manifest:
        // a slot with multiple files forms the random pool (Rust pick_audio). There is
        // no longer a global per-event variant picker or voice_picks overlay.

        public static CsolVoiceSettingsValues Load()
        {
            var values = ApplicationData.Current.LocalSettings.Values;

            return new CsolVoiceSettingsValues
            {
                FirstKillIcon = values.ContainsKey(FirstKillIconSettingKey)
                    ? NormalizeIcon(values[FirstKillIconSettingKey] as string, CsolVoiceSettingsStore.FirstKillIcon)
                    : CsolVoiceSettingsStore.FirstKillIcon,
                LastKillIcon = values.ContainsKey(LastKillIconSettingKey)
                    ? NormalizeIcon(values[LastKillIconSettingKey] as string, RevengeIcon)
                    : NormalizeIcon(values[LegacyFirstLastIconSettingKey] as string, RevengeIcon),
                SpecialVoicePriority = ReadBoolean(values[SpecialVoicePrioritySettingKey], false),
                LastKillSpecialAudio = ReadBoolean(values[LastKillSpecialAudioSettingKey], true)
            };
        }

        public static void Save(CsolVoiceSettingsValues settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            var values = ApplicationData.Current.LocalSettings.Values;
            values[FirstKillIconSettingKey] = NormalizeIcon(settings.FirstKillIcon, FirstKillIcon);
            values[LastKillIconSettingKey] = NormalizeIcon(settings.LastKillIcon, RevengeIcon);
            values[SpecialVoicePrioritySettingKey] = settings.SpecialVoicePriority;
            values[LastKillSpecialAudioSettingKey] = settings.LastKillSpecialAudio;
        }

        private static string NormalizeIcon(string value, string fallback)
        {
            if (string.Equals(value, FirstKillIcon, StringComparison.OrdinalIgnoreCase))
            {
                return FirstKillIcon;
            }

            if (string.Equals(value, RevengeIcon, StringComparison.OrdinalIgnoreCase))
            {
                return RevengeIcon;
            }

            return fallback;
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

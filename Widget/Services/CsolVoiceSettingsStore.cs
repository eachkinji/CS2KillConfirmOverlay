using System;
using System.Collections.Generic;
using Windows.Storage;

namespace KillConfirmGameBar.Services
{
    internal sealed class CsolVoiceSettingsValues
    {
        /// <summary>Per kill-type voice pick: "random" or a specific file name.</summary>
        public Dictionary<string, string> VoicePicks { get; set; } = new Dictionary<string, string>();

        /// <summary>Icon shown for first/last kills: "revenge" or "firstkill".</summary>
        public string FirstLastIcon { get; set; } = "revenge";

        /// <summary>true = special voice (headshot/knife) beats the streak voice.</summary>
        public bool SpecialVoicePriority { get; set; } = true;
    }

    internal static class CsolVoiceSettingsStore
    {
        public const string RandomPick = "random";
        public const string RevengeIcon = "revenge";
        public const string FirstKillIcon = "firstkill";

        private const string FirstLastIconSettingKey = "CsolFirstLastIcon";
        private const string SpecialVoicePrioritySettingKey = "CsolSpecialVoicePriority";
        private const string VoicePickPrefix = "CsolVoicePick_";

        /// <summary>
        /// Known voice files per CSOL kill type. Order matters: index 0 is the default
        /// when "random" is selected and random selection is unavailable.
        /// </summary>
        public static readonly IReadOnlyDictionary<string, string[]> VoiceVariants =
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["1"] = new[] { "Cantbelive.wav", "Crazy.wav", "Excellent.wav", "Firstkill.wav", "Incredible.wav" },
                ["2"] = new[] { "Doublekill.wav" },
                ["3"] = new[] { "Triplekill.wav" },
                ["4"] = new[] { "Multikill.wav", "Multikill_ch.wav" },
                ["headshot"] = new[] { "Headshot.wav" },
                ["knife"] = new[] { "Humililation.wav", "Ohno.wav" },
                ["revenge"] = new[] { "Revenge.wav" },
                ["assist"] = new[] { "Assist.wav" }
            };

        public static CsolVoiceSettingsValues Load()
        {
            var values = ApplicationData.Current.LocalSettings.Values;
            var picks = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string killType in VoiceVariants.Keys)
            {
                string stored = values[VoicePickPrefix + killType] as string;
                picks[killType] = NormalizePick(stored, killType);
            }

            return new CsolVoiceSettingsValues
            {
                VoicePicks = picks,
                FirstLastIcon = ReadFirstLastIcon(values[FirstLastIconSettingKey]),
                SpecialVoicePriority = ReadBoolean(values[SpecialVoicePrioritySettingKey], true)
            };
        }

        public static void Save(CsolVoiceSettingsValues settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            var values = ApplicationData.Current.LocalSettings.Values;
            foreach (string killType in VoiceVariants.Keys)
            {
                string pick = settings.VoicePicks.TryGetValue(killType, out string stored)
                    ? stored
                    : RandomPick;
                values[VoicePickPrefix + killType] = NormalizePick(pick, killType);
            }

            values[FirstLastIconSettingKey] = NormalizeFirstLastIcon(settings.FirstLastIcon);
            values[SpecialVoicePrioritySettingKey] = settings.SpecialVoicePriority;
        }

        public static string ResolvePick(string killType, string fallback)
        {
            string stored = ApplicationData.Current.LocalSettings.Values[VoicePickPrefix + killType] as string;
            return NormalizePick(stored, killType) == RandomPick ? fallback : NormalizePick(stored, killType);
        }

        private static string NormalizePick(string value, string killType)
        {
            string trimmed = string.IsNullOrWhiteSpace(value) ? RandomPick : value.Trim();
            if (string.Equals(trimmed, RandomPick, StringComparison.OrdinalIgnoreCase))
            {
                return RandomPick;
            }

            if (VoiceVariants.TryGetValue(killType, out string[] variants))
            {
                foreach (string variant in variants)
                {
                    if (string.Equals(trimmed, variant, StringComparison.OrdinalIgnoreCase))
                    {
                        return variant;
                    }
                }
            }

            return RandomPick;
        }

        private static string ReadFirstLastIcon(object value)
        {
            if (value is string text
                && string.Equals(text, FirstKillIcon, StringComparison.OrdinalIgnoreCase))
            {
                return FirstKillIcon;
            }

            return RevengeIcon;
        }

        private static string NormalizeFirstLastIcon(string value)
        {
            return string.Equals(value, FirstKillIcon, StringComparison.OrdinalIgnoreCase)
                ? FirstKillIcon
                : RevengeIcon;
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

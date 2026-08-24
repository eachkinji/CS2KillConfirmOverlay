using System;
using System.Collections.Generic;
using Windows.Globalization;
using Windows.Storage;

namespace KillConfirmGameBar.Services
{
    public enum UiLanguage
    {
        English,
        SimplifiedChinese
    }

    public static partial class LocalizationManager
    {
        private const string SettingKey = "UiLanguage";
        private static UiLanguage _current = LoadLanguage();

        private static readonly Dictionary<string, string> English = MergeLocaleParts(
            CreateEnglishPartOne(),
            CreateEnglishPartTwo());
        private static readonly Dictionary<string, string> Chinese = MergeLocaleParts(
            CreateChinesePartOne(),
            CreateChinesePartTwo());

        private static Dictionary<string, string> MergeLocaleParts(
            Dictionary<string, string> first,
            Dictionary<string, string> second)
        {
            var merged = new Dictionary<string, string>(first, StringComparer.Ordinal);
            foreach (KeyValuePair<string, string> item in second)
            {
                merged[item.Key] = item.Value;
            }

            return merged;
        }

        public static UiLanguage Current => _current;

        public static void SetLanguage(UiLanguage language)
        {
            _current = language;
            ApplicationData.Current.LocalSettings.Values[SettingKey] = language == UiLanguage.SimplifiedChinese
                ? "zh-CN"
                : "en-US";
        }

        public static string Text(string key)
        {
            Dictionary<string, string> table = _current == UiLanguage.SimplifiedChinese ? Chinese : English;
            if (table.TryGetValue(key, out string value))
            {
                return value;
            }

            return English.TryGetValue(key, out value) ? value : key;
        }

        private static UiLanguage LoadLanguage()
        {
            string saved = ApplicationData.Current.LocalSettings.Values[SettingKey] as string;
            if (!string.IsNullOrWhiteSpace(saved))
            {
                return saved.StartsWith("zh", StringComparison.OrdinalIgnoreCase)
                    ? UiLanguage.SimplifiedChinese
                    : UiLanguage.English;
            }

            foreach (string language in ApplicationLanguages.Languages)
            {
                if (language.Equals("zh-CN", StringComparison.OrdinalIgnoreCase)
                    || language.Equals("zh-Hans", StringComparison.OrdinalIgnoreCase)
                    || language.StartsWith("zh-Hans-", StringComparison.OrdinalIgnoreCase))
                {
                    return UiLanguage.SimplifiedChinese;
                }

                if (!language.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }
            }

            return UiLanguage.English;
        }
    }
}

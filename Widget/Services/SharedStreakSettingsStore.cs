using System;
using Windows.Storage;
using Windows.UI.Xaml.Controls;

namespace KillConfirmGameBar.Services
{
    internal static class SharedStreakSettingsStore
    {
        public const string LifeMode = "life";
        public const string Timed5Mode = "timed_5";
        public const string Timed10Mode = "timed_10";
        public const string Timed15Mode = "timed_15";

        private const string SettingPrefix = "KillStreakMode_";

        public static bool IsSupported(GameStyleMode style)
        {
            return style == GameStyleMode.Battlefield1
                || style == GameStyleMode.Pubg
                || style == GameStyleMode.Valorant;
        }

        public static string Load(GameStyleMode style)
        {
            if (!IsSupported(style))
            {
                return LifeMode;
            }

            string value = ApplicationData.Current.LocalSettings.Values[SettingKey(style)] as string;
            return Normalize(value);
        }

        public static void Save(GameStyleMode style, string value)
        {
            if (!IsSupported(style))
            {
                return;
            }

            ApplicationData.Current.LocalSettings.Values[SettingKey(style)] = Normalize(value);
        }

        public static string Read(ComboBox selector, string fallback = LifeMode)
        {
            if (selector?.SelectedItem is ComboBoxItem item
                && item.Tag is string tag
                && !string.IsNullOrWhiteSpace(tag))
            {
                return Normalize(tag);
            }

            return Normalize(fallback);
        }

        public static void Select(ComboBox selector, string value)
        {
            if (selector == null)
            {
                return;
            }

            string target = Normalize(value);
            foreach (object option in selector.Items)
            {
                if (option is ComboBoxItem item
                    && item.Tag is string tag
                    && string.Equals(tag, target, StringComparison.OrdinalIgnoreCase))
                {
                    selector.SelectedItem = item;
                    return;
                }
            }

            selector.SelectedIndex = 0;
        }

        public static void ApplyLanguage(
            TextBlock label,
            ComboBoxItem life,
            ComboBoxItem timed5,
            ComboBoxItem timed10,
            ComboBoxItem timed15,
            bool isChinese)
        {
            label.Text = isChinese ? "\u8fde\u6740\u8ba1\u7b97" : "Kill streak";
            life.Content = isChinese ? "\u6b7b\u4ea1\u524d\u6301\u7eed\u7d2f\u8ba1" : "Until death";
            timed5.Content = isChinese ? "5 \u79d2\u8fde\u6740\u7a97\u53e3" : "5-second window";
            timed10.Content = isChinese ? "10 \u79d2\u8fde\u6740\u7a97\u53e3" : "10-second window";
            timed15.Content = isChinese ? "15 \u79d2\u8fde\u6740\u7a97\u53e3" : "15-second window";
        }

        public static string Normalize(string value)
        {
            if (string.Equals(value, Timed5Mode, StringComparison.OrdinalIgnoreCase))
            {
                return Timed5Mode;
            }

            if (string.Equals(value, Timed10Mode, StringComparison.OrdinalIgnoreCase))
            {
                return Timed10Mode;
            }

            if (string.Equals(value, Timed15Mode, StringComparison.OrdinalIgnoreCase))
            {
                return Timed15Mode;
            }

            return LifeMode;
        }

        private static string SettingKey(GameStyleMode style)
        {
            return SettingPrefix + GameStyleService.ToStorageValue(style);
        }
    }
}

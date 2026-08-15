using System;
using System.Globalization;
using System.Threading.Tasks;
using Windows.Data.Json;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.Web.Http;

namespace KillConfirmGameBar.Services
{
    internal static class SharedStreakSettingsStore
    {
        public const string NoneMode = "none";
        public const string LifeMode = "life";
        public const string CustomModeTag = "custom";
        public const string LoopModeTag = "loop";
        public const string Timed5Mode = "timed_5";
        public const string Timed10Mode = "timed_10";
        public const string Timed15Mode = "timed_15";

        public const double DefaultCustomSeconds = 1.0;
        public const double MinCustomSeconds = 0.1;
        public const double MaxCustomSeconds = 300.0;
        public const int DefaultLoopKillCount = 5;
        public const int MinLoopKillCount = 2;
        public const int MaxLoopKillCount = 50;

        private const string CustomModePrefix = "custom:";
        private const string LoopModePrefix = "loop:";
        private const string SettingPrefix = "KillStreakMode_";
        private const string LegacySharedSettingKey = "SharedStreakMode";
        private const string SpectatedKillEffectsSettingKey = "SpectatedKillEffectsEnabled";
        private static readonly Uri SpectatorSettingsUri =
            new Uri("http://127.0.0.1:10087/spectator/settings");

        public static bool IsSupported(GameStyleMode style)
        {
            return style != GameStyleMode.Crossfire;
        }

        public static bool LoadSpectatedKillEffects()
        {
            object value = ApplicationData.Current.LocalSettings.Values[SpectatedKillEffectsSettingKey];
            if (value is bool enabled)
            {
                return enabled;
            }

            if (value is string text && bool.TryParse(text, out bool parsed))
            {
                return parsed;
            }

            return false;
        }

        public static void SaveSpectatedKillEffects(bool enabled)
        {
            ApplicationData.Current.LocalSettings.Values[SpectatedKillEffectsSettingKey] = enabled;
        }

        public static async Task SyncSpectatedKillEffectsAsync()
        {
            var request = new JsonObject
            {
                ["enabled"] = JsonValue.CreateBooleanValue(LoadSpectatedKillEffects())
            };

            using (var client = await LocalServiceAuth.CreateHttpClientAsync())
            using (var content = new HttpStringContent(
                request.Stringify(),
                UnicodeEncoding.Utf8,
                "application/json"))
            using (HttpResponseMessage response = await client.PostAsync(SpectatorSettingsUri, content))
            {
                response.EnsureSuccessStatusCode();
            }
        }

        public static string Load(GameStyleMode style)
        {
            if (!IsSupported(style))
            {
                return LifeMode;
            }

            var settings = ApplicationData.Current.LocalSettings.Values;
            string value = settings[SettingKey(style)] as string;
            if (string.IsNullOrWhiteSpace(value))
            {
                value = settings[LegacySharedSettingKey] as string;
            }

            return Normalize(value);
        }

        public static void Save(GameStyleMode style, string value)
        {
            if (!IsSupported(style))
            {
                return;
            }

            string normalized = Normalize(value);
            var settings = ApplicationData.Current.LocalSettings.Values;
            settings[SettingKey(style)] = normalized;
            settings[LegacySharedSettingKey] = normalized;
        }

        public static string Read(ComboBox selector, string fallback = LifeMode)
        {
            return Read(selector, null, fallback);
        }

        public static string Read(
            ComboBox selector,
            TextBox customSecondsEditor,
            string fallback = LifeMode)
        {
            return Read(selector, customSecondsEditor, null, fallback);
        }

        public static string Read(
            ComboBox selector,
            TextBox customSecondsEditor,
            ComboBox loopKillCountSelector,
            string fallback = LifeMode)
        {
            if (selector?.SelectedItem is ComboBoxItem item
                && item.Tag is string tag
                && !string.IsNullOrWhiteSpace(tag))
            {
                if (string.Equals(tag, CustomModeTag, StringComparison.OrdinalIgnoreCase))
                {
                    return BuildCustomMode(ReadCustomSeconds(customSecondsEditor?.Text, fallback));
                }

                if (string.Equals(tag, LoopModeTag, StringComparison.OrdinalIgnoreCase))
                {
                    return BuildLoopMode(ReadLoopKillCount(loopKillCountSelector, fallback));
                }

                return Normalize(tag);
            }

            return Normalize(fallback);
        }

        public static void Select(ComboBox selector, string value)
        {
            Select(selector, null, value);
        }

        public static void Select(ComboBox selector, TextBox customSecondsEditor, string value)
        {
            Select(selector, customSecondsEditor, null, value);
        }

        public static void Select(
            ComboBox selector,
            TextBox customSecondsEditor,
            ComboBox loopKillCountSelector,
            string value)
        {
            if (selector == null)
            {
                return;
            }

            string normalized = Normalize(value);
            string target = IsCustomMode(normalized)
                ? CustomModeTag
                : IsLoopMode(normalized) ? LoopModeTag : normalized;
            if (customSecondsEditor != null)
            {
                customSecondsEditor.Text = FormatSeconds(ReadCustomSeconds(null, normalized));
            }
            SelectLoopKillCount(loopKillCountSelector, ReadLoopKillCount(null, normalized));

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

        public static void UpdateCustomEditorVisibility(
            ComboBox selector,
            FrameworkElement customEditor)
        {
            if (customEditor == null)
            {
                return;
            }

            bool isCustom = selector?.SelectedItem is ComboBoxItem item
                && string.Equals(
                    item.Tag as string,
                    CustomModeTag,
                    StringComparison.OrdinalIgnoreCase);
            customEditor.Visibility = isCustom ? Visibility.Visible : Visibility.Collapsed;
        }

        public static void UpdateLoopEditorVisibility(
            ComboBox selector,
            FrameworkElement loopEditor)
        {
            if (loopEditor == null)
            {
                return;
            }

            bool isLoop = selector?.SelectedItem is ComboBoxItem item
                && string.Equals(
                    item.Tag as string,
                    LoopModeTag,
                    StringComparison.OrdinalIgnoreCase);
            loopEditor.Visibility = isLoop ? Visibility.Visible : Visibility.Collapsed;
        }

        public static void NormalizeCustomSecondsEditor(TextBox editor, string fallback)
        {
            if (editor == null)
            {
                return;
            }

            editor.Text = FormatSeconds(ReadCustomSeconds(editor.Text, fallback));
        }

        public static void ApplyLanguage(
            TextBlock label,
            ComboBoxItem life,
            ComboBoxItem timed5,
            ComboBoxItem timed10,
            ComboBoxItem timed15,
            bool isChinese)
        {
            if (label != null)
            {
                label.Text = isChinese ? "\u8fde\u6740\u8ba1\u7b97" : "Kill streak";
            }
            if (life != null)
            {
                life.Content = isChinese ? "\u6b7b\u4ea1\u524d\u6301\u7eed\u7d2f\u8ba1" : "Until death";
            }
            if (timed5 != null)
            {
                timed5.Content = isChinese ? "5 \u79d2\u8fde\u6740\u7a97\u53e3" : "5-second window";
            }
            if (timed10 != null)
            {
                timed10.Content = isChinese ? "10 \u79d2\u8fde\u6740\u7a97\u53e3" : "10-second window";
            }
            if (timed15 != null)
            {
                timed15.Content = isChinese ? "15 \u79d2\u8fde\u6740\u7a97\u53e3" : "15-second window";
            }
        }

        public static string Normalize(string value)
        {
            if (string.Equals(value, NoneMode, StringComparison.OrdinalIgnoreCase))
            {
                return NoneMode;
            }

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

            if (string.Equals(value, CustomModeTag, StringComparison.OrdinalIgnoreCase)
                || IsCustomMode(value))
            {
                return BuildCustomMode(ReadCustomSeconds(null, value));
            }

            if (string.Equals(value, LoopModeTag, StringComparison.OrdinalIgnoreCase)
                || IsLoopMode(value))
            {
                return BuildLoopMode(ReadLoopKillCount(null, value));
            }

            return LifeMode;
        }

        public static bool IsCustomMode(string value)
        {
            return !string.IsNullOrWhiteSpace(value)
                && value.Trim().StartsWith(CustomModePrefix, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsLoopMode(string value)
        {
            return !string.IsNullOrWhiteSpace(value)
                && value.Trim().StartsWith(LoopModePrefix, StringComparison.OrdinalIgnoreCase);
        }

        public static int ReadLoopKillCount(ComboBox selector, int fallback)
        {
            if (selector?.SelectedItem is ComboBoxItem item)
            {
                if (item.Tag is int count)
                {
                    return ClampLoopKillCount(count);
                }

                if (int.TryParse(item.Tag?.ToString(), out count))
                {
                    return ClampLoopKillCount(count);
                }
            }

            return ClampLoopKillCount(fallback);
        }

        public static void SelectLoopKillCount(ComboBox selector, int count)
        {
            if (selector == null)
            {
                return;
            }

            int target = ClampLoopKillCount(count);
            foreach (object option in selector.Items)
            {
                if (option is ComboBoxItem item
                    && int.TryParse(item.Tag?.ToString(), out int itemCount)
                    && itemCount == target)
                {
                    selector.SelectedItem = item;
                    return;
                }
            }

            selector.SelectedIndex = 0;
        }

        private static string BuildCustomMode(double seconds)
        {
            return CustomModePrefix + FormatSeconds(seconds);
        }

        private static string BuildLoopMode(int kills)
        {
            return LoopModePrefix + ClampLoopKillCount(kills).ToString(CultureInfo.InvariantCulture);
        }

        private static int ReadLoopKillCount(ComboBox selector, string fallback)
        {
            int fallbackCount = DefaultLoopKillCount;
            if (IsLoopMode(fallback))
            {
                string countText = fallback.Trim().Substring(LoopModePrefix.Length);
                if (int.TryParse(countText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
                {
                    fallbackCount = parsed;
                }
            }

            return ReadLoopKillCount(selector, fallbackCount);
        }

        private static double ReadCustomSeconds(string text, string fallback)
        {
            if (TryParseSeconds(text, out double parsed))
            {
                return ClampSeconds(parsed);
            }

            if (IsCustomMode(fallback))
            {
                string fallbackText = fallback.Trim().Substring(CustomModePrefix.Length);
                if (TryParseSeconds(fallbackText, out parsed))
                {
                    return ClampSeconds(parsed);
                }
            }

            return DefaultCustomSeconds;
        }

        private static bool TryParseSeconds(string text, out double seconds)
        {
            if (!string.IsNullOrWhiteSpace(text)
                && (double.TryParse(
                        text.Trim(),
                        NumberStyles.Float,
                        CultureInfo.CurrentCulture,
                        out seconds)
                    || double.TryParse(
                        text.Trim().Replace(',', '.'),
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out seconds))
                && !double.IsNaN(seconds)
                && !double.IsInfinity(seconds))
            {
                return true;
            }

            seconds = 0;
            return false;
        }

        private static double ClampSeconds(double seconds)
        {
            return Math.Max(MinCustomSeconds, Math.Min(MaxCustomSeconds, seconds));
        }

        private static int ClampLoopKillCount(int kills)
        {
            return Math.Max(MinLoopKillCount, Math.Min(MaxLoopKillCount, kills));
        }

        private static string FormatSeconds(double seconds)
        {
            return ClampSeconds(seconds).ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static string SettingKey(GameStyleMode style)
        {
            return SettingPrefix + GameStyleService.ToStorageValue(style);
        }

    }
}

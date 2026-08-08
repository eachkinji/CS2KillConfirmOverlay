using System;
using System.Threading.Tasks;
using KillConfirmGameBar.Services;
using Windows.Data.Json;
using Windows.Storage.Streams;
using Windows.UI.Xaml.Controls;
using Windows.Web.Http;

namespace KillConfirmGameBar.Controls.Settings
{
    public sealed partial class CrossfireAdvancedSettingsPanel : UserControl
    {
        private bool _suppressSettingEvents;

        public CrossfireAdvancedSettingsPanel()
        {
            InitializeComponent();
            LoadGameplaySettings();
        }

        internal void ApplyTheme(GameThemePalette theme)
        {
            SettingsPanelSupport.ApplyPanel(Card, TitleText, BodyText, theme);
            SettingsPanelSupport.ApplyTag(VoiceTag, VoiceTagText, theme);
            SettingsPanelSupport.ApplyTag(IconTag, IconTagText, theme);
            SettingsPanelSupport.ApplySettingRow(StreakModeLabel, StreakModeSelector, theme);
            SettingsPanelSupport.ApplySettingRow(FirstKillAudioLabel, FirstKillAudioSelector, theme);
            SettingsPanelSupport.ApplySettingRow(LastKillAudioLabel, LastKillAudioSelector, theme);
        }

        public void ApplyLanguage(bool isChinese)
        {
            TitleText.Text = isChinese ? "\u7a7f\u8d8a\u706b\u7ebf\u9ad8\u7ea7\u8bbe\u7f6e" : "CrossFire advanced settings";
            BodyText.Text = isChinese
                ? "\u53ef\u9009\u62e9 CF \u8fde\u6740\u5728\u6b7b\u4ea1\u524d\u6301\u7eed\u7d2f\u8ba1\uff0c\u6216\u5728\u95f4\u9694 5\u300110\u300115 \u79d2\u540e\u4e2d\u65ad\uff1b\u4e5f\u53ef\u5206\u522b\u8bbe\u7f6e\u9996\u6740\u548c\u5c3e\u6740\u4f7f\u7528\u7279\u6b8a\u8fd8\u662f\u539f\u51fb\u6740\u97f3\u6548\u3002"
                : "Choose whether CF streaks last until death or expire after 5, 10, or 15 seconds, and whether first/last kills use special or original kill audio.";
            VoiceTagText.Text = isChinese ? "CF \u8bed\u97f3" : "CF voices";
            IconTagText.Text = isChinese ? "CF \u56fe\u6807" : "CF icons";
            StreakModeLabel.Text = isChinese ? "\u8fde\u6740\u8ba1\u7b97" : "Kill streak";
            StreakLifeItem.Content = isChinese ? "\u6b7b\u4ea1\u524d\u6301\u7eed\u7d2f\u8ba1" : "Until death";
            StreakTimed5Item.Content = isChinese ? "5 \u79d2\u8fde\u6740\u7a97\u53e3" : "5-second window";
            StreakTimed10Item.Content = isChinese ? "10 \u79d2\u8fde\u6740\u7a97\u53e3" : "10-second window";
            StreakTimed15Item.Content = isChinese ? "15 \u79d2\u8fde\u6740\u7a97\u53e3" : "15-second window";
            FirstKillAudioLabel.Text = isChinese ? "\u9996\u6740\u8bed\u97f3" : "First-kill audio";
            LastKillAudioLabel.Text = isChinese ? "\u5c3e\u6740\u8bed\u97f3" : "Last-kill audio";
            FirstKillSpecialItem.Content = isChinese ? "\u7279\u6b8a\u97f3\u6548" : "Special audio";
            LastKillSpecialItem.Content = FirstKillSpecialItem.Content;
            FirstKillOriginalItem.Content = isChinese ? "\u539f\u51fb\u6740\u97f3\u6548" : "Original kill audio";
            LastKillOriginalItem.Content = FirstKillOriginalItem.Content;
        }

        private void LoadGameplaySettings()
        {
            CrossfireGameplaySettingsValues settings = CrossfireGameplaySettingsStore.Load();
            _suppressSettingEvents = true;
            try
            {
                SelectTaggedItem(StreakModeSelector, settings.StreakMode, "life");
                SelectTaggedItem(FirstKillAudioSelector, settings.FirstKillSpecialAudio ? "special" : "original", "special");
                SelectTaggedItem(LastKillAudioSelector, settings.LastKillSpecialAudio ? "special" : "original", "special");
            }
            finally
            {
                _suppressSettingEvents = false;
            }

            CrossfireGameplaySettingsStore.Save(settings);
        }

        private async void OnGameplaySettingChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressSettingEvents)
            {
                return;
            }

            var settings = new CrossfireGameplaySettingsValues
            {
                StreakMode = ReadTaggedItem(StreakModeSelector, "life"),
                FirstKillSpecialAudio = ReadTaggedItem(FirstKillAudioSelector, "special") == "special",
                LastKillSpecialAudio = ReadTaggedItem(LastKillAudioSelector, "special") == "special"
            };
            CrossfireGameplaySettingsStore.Save(settings);
            await TrySyncRuntimeSettingsAsync(settings);
        }

        private static async Task TrySyncRuntimeSettingsAsync(CrossfireGameplaySettingsValues settings)
        {
            try
            {
                var request = new JsonObject
                {
                    ["active"] = JsonValue.CreateBooleanValue(true),
                    ["streak_mode"] = JsonValue.CreateStringValue(settings.StreakMode),
                    ["first_kill_special_audio"] = JsonValue.CreateBooleanValue(settings.FirstKillSpecialAudio),
                    ["last_kill_special_audio"] = JsonValue.CreateBooleanValue(settings.LastKillSpecialAudio)
                };

                using (var client = await LocalServiceAuth.CreateHttpClientAsync())
                using (var content = new HttpStringContent(
                    request.Stringify(),
                    UnicodeEncoding.Utf8,
                    "application/json"))
                {
                    await client.PostAsync(new Uri("http://127.0.0.1:3000/crossfire/settings"), content);
                }
            }
            catch (Exception ex)
            {
                App.Log("Sync CrossFire settings from advanced settings failed: " + ex.Message);
            }
        }

        private static string ReadTaggedItem(ComboBox selector, string fallback)
        {
            if (selector?.SelectedItem is ComboBoxItem item
                && item.Tag is string tag
                && !string.IsNullOrWhiteSpace(tag))
            {
                return tag;
            }

            return fallback;
        }

        private static void SelectTaggedItem(ComboBox selector, string value, string fallback)
        {
            string target = string.IsNullOrWhiteSpace(value) ? fallback : value;
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
    }
}

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
            StreakEditor.ApplyTheme(theme);
            SettingsPanelSupport.ApplySettingRow(FirstKillAudioLabel, FirstKillAudioSelector, theme);
            SettingsPanelSupport.ApplySettingRow(LastKillAudioLabel, LastKillAudioSelector, theme);
        }

        public void ApplyLanguage(bool isChinese)
        {
            TitleText.Text = isChinese ? "\u7a7f\u8d8a\u706b\u7ebf\u9ad8\u7ea7\u8bbe\u7f6e" : "CrossFire advanced settings";
            BodyText.Text = isChinese
                ? "CF \u8fde\u6740\u53ef\u8bbe\u4e3a\u65e0\u7a97\u53e3\u3001\u6b7b\u4ea1\u524d\u7d2f\u8ba1\u3001\u56fa\u5b9a\u79d2\u6570\u6216 0.1\u2013300 \u79d2\u81ea\u5b9a\u4e49\u7a97\u53e3\uff1b\u9996\u6740\u548c\u5c3e\u6740\u97f3\u6548\u4ecd\u53ef\u5206\u522b\u8bbe\u7f6e\u3002"
                : "Choose no streak window, until-death counting, a fixed window, or a custom 0.1?300 second window; first/last-kill audio remains configurable.";
            VoiceTagText.Text = isChinese ? "CF \u8bed\u97f3" : "CF voices";
            IconTagText.Text = isChinese ? "CF \u56fe\u6807" : "CF icons";
            StreakEditor.ApplyLanguage(isChinese);
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
                StreakEditor.SelectValue(settings.StreakMode);
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
                StreakMode = StreakEditor.GetValue(SharedStreakSettingsStore.LifeMode),
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

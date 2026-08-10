using System;
using System.Threading.Tasks;
using KillConfirmGameBar.Services;
using Windows.Data.Json;
using Windows.Storage.Streams;
using Windows.UI.Xaml;
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
            SettingsPanelSupport.ApplySettingRow(HeadshotAudioPriorityLabel, HeadshotAudioPrioritySelector, theme);
            SettingsPanelSupport.ApplySettingRow(KnifeAudioPriorityLabel, KnifeAudioPrioritySelector, theme);
            SettingsPanelSupport.ApplySettingRow(HeadshotIconPriorityLabel, HeadshotIconPrioritySelector, theme);
            SettingsPanelSupport.ApplySettingRow(KnifeIconPriorityLabel, KnifeIconPrioritySelector, theme);
            SettingsPanelSupport.ApplySettingRow(FirstKillAudioLabel, FirstKillAudioSelector, theme);
            SettingsPanelSupport.ApplySettingRow(LastKillAudioLabel, LastKillAudioSelector, theme);
            SettingsPanelSupport.ApplyToggleRow(FirstKillEffectLabel, FirstKillEffectToggle, theme);
            SettingsPanelSupport.ApplyToggleRow(LastKillEffectLabel, LastKillEffectToggle, theme);
            SettingsPanelSupport.ApplyToggleRow(AssistAudioLabel, AssistAudioToggle, theme);
        }

        public void ApplyLanguage(bool isChinese)
        {
            TitleText.Text = isChinese ? "\u7a7f\u8d8a\u706b\u7ebf\u9ad8\u7ea7\u8bbe\u7f6e" : "CrossFire advanced settings";
            BodyText.Text = isChinese
                ? "CF \u8fde\u6740\u53ef\u8bbe\u4e3a\u65e0\u7a97\u53e3\u3001\u6b7b\u4ea1\u524d\u7d2f\u8ba1\u3001\u56fa\u5b9a\u79d2\u6570\u6216 0.1\u2013300 \u79d2\u81ea\u5b9a\u4e49\u7a97\u53e3\uff1b\u7206\u5934\u548c\u5200\u6740\u97f3\u6548\u53ef\u5206\u522b\u9009\u62e9\u7279\u6b8a\u97f3\u6548\u6216\u8fde\u6740\u4f18\u5148\u3002"
                : "Configure the CF streak window and choose whether headshot or knife-kill audio takes priority over kill-streak audio.";
            VoiceTagText.Text = isChinese ? "CF \u8bed\u97f3" : "CF voices";
            IconTagText.Text = isChinese ? "CF \u56fe\u6807" : "CF icons";
            StreakEditor.ApplyLanguage(isChinese);
            HeadshotAudioPriorityLabel.Text = isChinese ? "\u7206\u5934\u97f3\u6548" : "Headshot audio";
            KnifeAudioPriorityLabel.Text = isChinese ? "\u5200\u6740\u97f3\u6548" : "Knife-kill audio";
            HeadshotSpecialPriorityItem.Content = isChinese ? "\u7206\u5934\u4f18\u5148" : "Headshot priority";
            KnifeSpecialPriorityItem.Content = isChinese ? "\u5200\u6740\u4f18\u5148" : "Knife-kill priority";
            HeadshotStreakPriorityItem.Content = isChinese ? "\u8fde\u6740\u4f18\u5148" : "Kill-streak priority";
            KnifeStreakPriorityItem.Content = HeadshotStreakPriorityItem.Content;
            HeadshotIconPriorityLabel.Text = isChinese ? "\u7206\u5934\u56fe\u6807" : "Headshot icon";
            KnifeIconPriorityLabel.Text = isChinese ? "\u5200\u6740\u56fe\u6807" : "Knife-kill icon";
            HeadshotIconSpecialPriorityItem.Content = HeadshotSpecialPriorityItem.Content;
            KnifeIconSpecialPriorityItem.Content = KnifeSpecialPriorityItem.Content;
            HeadshotIconStreakPriorityItem.Content = HeadshotStreakPriorityItem.Content;
            KnifeIconStreakPriorityItem.Content = HeadshotStreakPriorityItem.Content;
            FirstKillAudioLabel.Text = isChinese ? "\u9996\u6740\u8bed\u97f3" : "First-kill audio";
            LastKillAudioLabel.Text = isChinese ? "\u5c3e\u6740\u8bed\u97f3" : "Last-kill audio";
            FirstKillSpecialItem.Content = isChinese ? "\u7279\u6b8a\u97f3\u6548" : "Special audio";
            LastKillSpecialItem.Content = FirstKillSpecialItem.Content;
            FirstKillOriginalItem.Content = isChinese ? "\u539f\u51fb\u6740\u97f3\u6548" : "Original kill audio";
            LastKillOriginalItem.Content = FirstKillOriginalItem.Content;
            FirstKillEffectLabel.Text = isChinese ? "\u9996\u6740\u7279\u6548" : "First-kill effect";
            LastKillEffectLabel.Text = isChinese ? "\u5c3e\u6740\u7279\u6548" : "Last-kill effect";
            FirstKillEffectToggle.OnContent = isChinese ? "\u5f00\u542f\uff08\u9ed8\u8ba4\uff09" : "On (default)";
            LastKillEffectToggle.OnContent = FirstKillEffectToggle.OnContent;
            FirstKillEffectToggle.OffContent = isChinese ? "\u5173\u95ed" : "Off";
            LastKillEffectToggle.OffContent = FirstKillEffectToggle.OffContent;
            AssistAudioLabel.Text = isChinese ? "\u52a9\u653b\u97f3\u6548" : "Assist audio";
            AssistAudioToggle.OnContent = isChinese ? "\u6709\u58f0\u97f3\uff08common\uff09" : "Sound (common)";
            AssistAudioToggle.OffContent = isChinese ? "\u65e0\u58f0\u97f3\uff08\u9ed8\u8ba4\uff09" : "Muted (default)";
        }

        private void LoadGameplaySettings()
        {
            CrossfireGameplaySettingsValues settings = CrossfireGameplaySettingsStore.Load();
            _suppressSettingEvents = true;
            try
            {
                StreakEditor.SelectValue(settings.StreakMode);
                SelectTaggedItem(HeadshotAudioPrioritySelector, settings.HeadshotSpecialAudioPriority ? "special" : "streak", "streak");
                SelectTaggedItem(KnifeAudioPrioritySelector, settings.KnifeSpecialAudioPriority ? "special" : "streak", "special");
                SelectTaggedItem(HeadshotIconPrioritySelector, settings.HeadshotSpecialIconPriority ? "special" : "streak", "streak");
                SelectTaggedItem(KnifeIconPrioritySelector, settings.KnifeSpecialIconPriority ? "special" : "streak", "special");
                SelectTaggedItem(FirstKillAudioSelector, settings.FirstKillSpecialAudio ? "special" : "original", "original");
                SelectTaggedItem(LastKillAudioSelector, settings.LastKillSpecialAudio ? "special" : "original", "original");
                FirstKillEffectToggle.IsOn = settings.FirstKillEffectEnabled;
                LastKillEffectToggle.IsOn = settings.LastKillEffectEnabled;
                AssistAudioToggle.IsOn = settings.AssistAudioEnabled;
            }
            finally
            {
                _suppressSettingEvents = false;
            }

            CrossfireGameplaySettingsStore.Save(settings);
        }

        private async void OnGameplaySettingChanged(object sender, RoutedEventArgs e)
        {
            if (_suppressSettingEvents)
            {
                return;
            }

            var settings = new CrossfireGameplaySettingsValues
            {
                StreakMode = StreakEditor.GetValue(SharedStreakSettingsStore.LifeMode),
                HeadshotSpecialAudioPriority = ReadTaggedItem(HeadshotAudioPrioritySelector, "streak") == "special",
                KnifeSpecialAudioPriority = ReadTaggedItem(KnifeAudioPrioritySelector, "special") == "special",
                HeadshotSpecialIconPriority = ReadTaggedItem(HeadshotIconPrioritySelector, "streak") == "special",
                KnifeSpecialIconPriority = ReadTaggedItem(KnifeIconPrioritySelector, "special") == "special",
                FirstKillSpecialAudio = ReadTaggedItem(FirstKillAudioSelector, "original") == "special",
                LastKillSpecialAudio = ReadTaggedItem(LastKillAudioSelector, "original") == "special",
                FirstKillEffectEnabled = FirstKillEffectToggle.IsOn,
                LastKillEffectEnabled = LastKillEffectToggle.IsOn,
                AssistAudioEnabled = AssistAudioToggle.IsOn
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
                    ["last_kill_special_audio"] = JsonValue.CreateBooleanValue(settings.LastKillSpecialAudio),
                    ["headshot_special_audio_priority"] = JsonValue.CreateBooleanValue(settings.HeadshotSpecialAudioPriority),
                    ["knife_special_audio_priority"] = JsonValue.CreateBooleanValue(settings.KnifeSpecialAudioPriority),
                    ["assist_audio_enabled"] = JsonValue.CreateBooleanValue(settings.AssistAudioEnabled)
                };

                using (var client = await LocalServiceAuth.CreateHttpClientAsync())
                using (var content = new HttpStringContent(
                    request.Stringify(),
                    UnicodeEncoding.Utf8,
                    "application/json"))
                {
                    await client.PostAsync(new Uri("http://127.0.0.1:10087/crossfire/settings"), content);
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

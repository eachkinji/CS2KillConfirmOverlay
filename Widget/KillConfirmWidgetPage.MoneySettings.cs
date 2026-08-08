using System;
using System.Threading.Tasks;
using KillConfirmGameBar.Services;
using Windows.Data.Json;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Xaml.Controls;
using Windows.Web.Http;

namespace KillConfirmGameBar
{
    public sealed partial class KillConfirmWidgetPage
    {
        private async void OnMoneyRewardModeSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            await ApplyAndSaveMoneyRewardModeAsync();
        }

        private void LoadMoneyRewardModeSettings()
        {
            string mode = ApplicationData.Current.LocalSettings.Values[MoneyRewardModeSettingKey] as string;
            if (string.IsNullOrWhiteSpace(mode))
            {
                mode = DefaultMoneyRewardMode;
            }

            _suppressMoneyRewardModeEvents = true;
            if (MoneyRewardModeSelector != null)
            {
                SelectTaggedComboBoxItem(MoneyRewardModeSelector, mode, DefaultMoneyRewardMode);
            }
            _suppressMoneyRewardModeEvents = false;

            ApplicationData.Current.LocalSettings.Values[MoneyRewardModeSettingKey] = mode;
        }

        private async Task ApplyAndSaveMoneyRewardModeAsync()
        {
            if (_suppressMoneyRewardModeEvents)
            {
                return;
            }

            ApplicationData.Current.LocalSettings.Values[MoneyRewardModeSettingKey] = GetSelectedMoneyRewardMode();

            try
            {
                await EnsureServiceAvailableAsync();
            }
            catch (Exception ex)
            {
                App.Log("Set money reward mode failed: " + ex);
            }
        }

        private async Task SyncMoneyRewardModeAsync()
        {
            try
            {
                var request = new JsonObject
                {
                    ["mode"] = JsonValue.CreateStringValue(GetSelectedMoneyRewardMode())
                };

                using (var client = await LocalServiceAuth.CreateHttpClientAsync())
                using (var content = new HttpStringContent(request.Stringify(), UnicodeEncoding.Utf8, "application/json"))
                using (HttpResponseMessage response = await client.PostAsync(MoneyRewardModeUri, content))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        App.Log("Set money reward mode failed: status=" + response.StatusCode);
                    }
                }
            }
            catch (Exception ex)
            {
                App.Log("Set money reward mode failed: " + ex);
            }
        }

        private string GetSelectedMoneyRewardMode()
        {
            if (MoneyRewardModeSelector != null)
            {
                return ReadTaggedComboBoxItem(MoneyRewardModeSelector, DefaultMoneyRewardMode);
            }

            string mode = ApplicationData.Current.LocalSettings.Values[MoneyRewardModeSettingKey] as string;
            return string.IsNullOrWhiteSpace(mode) ? DefaultMoneyRewardMode : mode;
        }

        private static string ReadTaggedComboBoxItem(ComboBox selector, string fallback)
        {
            if (selector?.SelectedItem is ComboBoxItem item
                && item.Tag is string tag
                && !string.IsNullOrWhiteSpace(tag))
            {
                return tag;
            }

            return fallback;
        }

        private static void SelectTaggedComboBoxItem(ComboBox selector, string value, string fallback)
        {
            if (selector == null)
            {
                return;
            }

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

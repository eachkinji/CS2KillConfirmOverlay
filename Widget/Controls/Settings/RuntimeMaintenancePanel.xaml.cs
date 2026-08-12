using System;
using System.Threading.Tasks;
using KillConfirmGameBar.Controls.GameStyles;
using KillConfirmGameBar.Services;
using Windows.ApplicationModel.Core;
using Windows.Data.Json;
using Windows.Storage;
using Windows.Storage.AccessCache;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.Web.Http;

namespace KillConfirmGameBar.Controls.Settings
{
    public sealed partial class RuntimeMaintenancePanel : UserControl
    {
        private const string AudioDeviceSettingKey = "AudioOutputDevice";
        private static readonly Uri AudioDevicesUri = new Uri("http://127.0.0.1:10087/audio/devices");
        private static readonly Uri AudioDeviceUri = new Uri("http://127.0.0.1:10087/audio/device");
        private static readonly Uri ShutdownUri = new Uri("http://127.0.0.1:10087/shutdown");
        private bool _suppressAudioEvents;
        private bool _suppressDeveloperEvents;

        public RuntimeMaintenancePanel()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            ApplyLanguage();
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            ApplyTheme(GameThemePalette.Current);
            Services.DeveloperModeSettingsStore.Changed -= OnDeveloperModeChanged;
            Services.DeveloperModeSettingsStore.Changed += OnDeveloperModeChanged;
            SelectDeveloperMode(Services.DeveloperModeSettingsStore.IsEnabled);
            await LoadAudioDevicesAsync();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            Services.DeveloperModeSettingsStore.Changed -= OnDeveloperModeChanged;
        }

        internal void ApplyLanguage()
        {
            TitleText.Text = Services.LocalizationManager.Text("RuntimeTitle");
            DescriptionText.Text = Services.LocalizationManager.Text("RuntimeDescription");
            AudioDeviceLabel.Text = Services.LocalizationManager.Text("AudioOutputLabel");
            DeveloperModeLabel.Text = Services.LocalizationManager.Text("DeveloperModeLabel");
            DeveloperModeHint.Text = Services.LocalizationManager.Text("DeveloperModeHint");
            DeveloperModeToggle.OffContent = Services.LocalizationManager.Text("Off");
            DeveloperModeToggle.OnContent = Services.LocalizationManager.Text("On");
            ResetPluginButton.Content = Services.LocalizationManager.Text("ResetPluginData");
        }

        internal void ApplyTheme(GameThemePalette theme)
        {
            if (theme == null)
            {
                return;
            }

            Card.Background = new SolidColorBrush(theme.SubtleField);
            Card.BorderBrush = new SolidColorBrush(theme.SoftBorder);
            TitleText.Foreground = new SolidColorBrush(theme.Text);
            DescriptionText.Foreground = new SolidColorBrush(theme.MutedText);
            StatusText.Foreground = new SolidColorBrush(theme.MutedText);
            DeveloperModeHint.Foreground = new SolidColorBrush(theme.MutedText);
            ResetPluginButton.Background = new SolidColorBrush(theme.WarningField);
            ResetPluginButton.BorderBrush = new SolidColorBrush(theme.WarningBorder);
            ResetPluginButton.Foreground = new SolidColorBrush(theme.WarningText);
            ResetPluginButton.CornerRadius = new CornerRadius(14);
            AdvancedEffectsPanelSupport.ApplySoftenedTree(this, theme);
        }

        private void SelectDeveloperMode(bool enabled)
        {
            _suppressDeveloperEvents = true;
            DeveloperModeToggle.IsOn = enabled;
            _suppressDeveloperEvents = false;
        }

        private void OnDeveloperModeChanged(object sender, bool enabled)
        {
            // The Changed event can fire while the store is saved from a handler
            // on any thread, and writing IsOn re-enters the ToggleSwitch through
            // the XAML COM layer. Always marshal the UI update to this panel's
            // dispatcher so it runs on the UI thread after the current handler.
            _ = Dispatcher.RunAsync(
                CoreDispatcherPriority.Normal,
                () => SelectDeveloperMode(enabled));
        }

        private async void OnDeveloperModeToggled(object sender, RoutedEventArgs e)
        {
            if (_suppressDeveloperEvents)
            {
                return;
            }

            bool enabled = DeveloperModeToggle.IsOn;
            Services.DeveloperModeSettingsStore.Save(enabled);
            try
            {
                await Services.DeveloperModeSettingsStore.SyncToServiceAsync();
                StatusText.Text = Services.LocalizationManager.Text(
                    enabled ? "DeveloperModeEnabledStatus" : "DeveloperModeDisabledStatus");
            }
            catch (Exception ex)
            {
                App.Log("Failed to sync developer mode: " + ex);
                StatusText.Text = Services.LocalizationManager.Text("DeveloperModeSyncFailed");
            }
        }

        private async Task LoadAudioDevicesAsync()
        {
            _suppressAudioEvents = true;
            try
            {
                AudioDeviceSelector.Items.Clear();
                AudioDeviceSelector.Items.Add(new ComboBoxItem
                {
                    Content = Services.LocalizationManager.Text("SystemDefaultAudio"),
                    Tag = "default"
                });

                using (HttpClient client = await Services.LocalServiceAuth.CreateHttpClientAsync())
                using (HttpResponseMessage response = await client.GetAsync(AudioDevicesUri))
                {
                    response.EnsureSuccessStatusCode();
                    JsonObject json = JsonObject.Parse(await response.Content.ReadAsStringAsync());
                    JsonArray devices = json.GetNamedArray("devices", new JsonArray());
                    foreach (IJsonValue value in devices)
                    {
                        string name = value.GetString();
                        AudioDeviceSelector.Items.Add(new ComboBoxItem
                        {
                            Content = name,
                            Tag = name
                        });
                    }

                    string saved = ApplicationData.Current.LocalSettings.Values[AudioDeviceSettingKey] as string;
                    string selected = string.IsNullOrWhiteSpace(saved)
                        ? json.GetNamedString("selected", "default")
                        : saved;
                    SelectDevice(selected);
                    if (!string.IsNullOrWhiteSpace(saved)
                        && !string.Equals(saved, json.GetNamedString("selected", "default"), StringComparison.Ordinal))
                    {
                        await ApplyAudioDeviceAsync(saved, false);
                    }
                    StatusText.Text = FormatActiveDevice(json.GetNamedString("active", string.Empty));
                }
            }
            catch (Exception ex)
            {
                App.Log("Failed to load audio devices: " + ex);
                AudioDeviceSelector.SelectedIndex = 0;
                StatusText.Text = Services.LocalizationManager.Text("AudioDevicesUnavailable");
            }
            finally
            {
                _suppressAudioEvents = false;
            }
        }

        private void SelectDevice(string selected)
        {
            foreach (object entry in AudioDeviceSelector.Items)
            {
                if (entry is ComboBoxItem item
                    && string.Equals(item.Tag as string, selected, StringComparison.Ordinal))
                {
                    AudioDeviceSelector.SelectedItem = item;
                    return;
                }
            }
            AudioDeviceSelector.SelectedIndex = 0;
        }

        private async void OnAudioDeviceSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressAudioEvents || !(AudioDeviceSelector.SelectedItem is ComboBoxItem item))
            {
                return;
            }

            string device = item.Tag as string ?? "default";
            await ApplyAudioDeviceAsync(device, true);
        }

        private async Task ApplyAudioDeviceAsync(string device, bool save)
        {
            try
            {
                JsonObject request = new JsonObject
                {
                    ["device"] = JsonValue.CreateStringValue(device)
                };
                using (HttpClient client = await Services.LocalServiceAuth.CreateHttpClientAsync())
                using (var content = new HttpStringContent(
                    request.Stringify(),
                    Windows.Storage.Streams.UnicodeEncoding.Utf8,
                    "application/json"))
                using (HttpResponseMessage response = await client.PostAsync(AudioDeviceUri, content))
                {
                    response.EnsureSuccessStatusCode();
                    JsonObject json = JsonObject.Parse(await response.Content.ReadAsStringAsync());
                    if (save)
                    {
                        ApplicationData.Current.LocalSettings.Values[AudioDeviceSettingKey] = device;
                    }
                    StatusText.Text = FormatActiveDevice(json.GetNamedString("active", string.Empty));
                }
            }
            catch (Exception ex)
            {
                App.Log("Failed to select audio device: " + ex);
                StatusText.Text = Services.LocalizationManager.Text("AudioSwitchFailed");
            }
        }

        private static string FormatActiveDevice(string active)
        {
            if (string.IsNullOrWhiteSpace(active))
            {
                return string.Empty;
            }
            return Services.LocalizationManager.Text("ActiveOutputPrefix") + active;
        }

        private async void OnResetPluginClick(object sender, RoutedEventArgs e)
        {
            var dialog = new ContentDialog
            {
                Title = Services.LocalizationManager.Text("ResetPluginTitle"),
                Content = Services.LocalizationManager.Text("ResetPluginBody"),
                PrimaryButtonText = Services.LocalizationManager.Text("ResetAction"),
                CloseButtonText = Services.LocalizationManager.Text("Cancel"),
                DefaultButton = ContentDialogButton.Close
            };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            {
                return;
            }

            ResetPluginButton.IsEnabled = false;
            StatusText.Text = Services.LocalizationManager.Text("ResettingPlugin");
            try
            {
                try
                {
                    using (HttpClient client = await Services.LocalServiceAuth.CreateHttpClientAsync())
                    {
                        await client.PostAsync(ShutdownUri, null);
                    }
                }
                catch
                {
                }

                StorageApplicationPermissions.FutureAccessList.Clear();
                await ApplicationData.Current.ClearAsync(ApplicationDataLocality.Local);
                StatusText.Text = Services.LocalizationManager.Text("ResetCompleteRestarting");
                AppRestartFailureReason restartResult =
                    await CoreApplication.RequestRestartAsync("plugin-reset");
                if (restartResult != AppRestartFailureReason.RestartPending)
                {
                    StatusText.Text = Services.LocalizationManager.Text("ResetCompleteReopen");
                    ResetPluginButton.IsEnabled = true;
                }
            }
            catch (Exception ex)
            {
                App.Log("Failed to reset plugin state: " + ex);
                StatusText.Text = Services.LocalizationManager.Text("ResetFailed");
                ResetPluginButton.IsEnabled = true;
            }
        }
    }
}

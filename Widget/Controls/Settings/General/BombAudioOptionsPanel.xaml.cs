using System;
using System.Threading.Tasks;
using KillConfirmGameBar.Controls.GameStyles;
using KillConfirmGameBar.Services;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace KillConfirmGameBar.Controls.Settings
{
    public sealed partial class BombAudioOptionsPanel : UserControl
    {
        private bool _suppressBombAudioEvents = true;
        private readonly DispatcherTimer _bombAudioSyncTimer = new DispatcherTimer();
        private readonly DispatcherTimer _fullEffectPreviewTimer = new DispatcherTimer();
        private bool _fullEffectPreviewActive;

        public BombAudioOptionsPanel()
        {
            InitializeComponent();
            _bombAudioSyncTimer.Interval = TimeSpan.FromMilliseconds(250);
            _bombAudioSyncTimer.Tick += OnBombAudioSyncTimerTick;
            _fullEffectPreviewTimer.Interval = TimeSpan.FromSeconds(40);
            _fullEffectPreviewTimer.Tick += OnFullEffectPreviewTimerTick;
            ApplyLanguage();
            RefreshSettings();
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            RefreshSettings();
            ApplyTheme(GameThemePalette.Current);
        }

        internal void ApplyLanguage()
        {
            BombAudioLabelText.Text = LocalizationManager.Text("BombAudioLabel");
            BombAudioHintText.Text = LocalizationManager.Text("BombAudioHint");
            BombAudioVolumeLabelText.Text = LocalizationManager.Text("BombAudioVolumeLabel");
            BombAudioSpeedLabelText.Text = LocalizationManager.Text("BombAudioSpeedLabel");
            BombAudioInitialSpeedLabelText.Text = LocalizationManager.Text("BombAudioInitialSpeedLabel");
            BombAudioFinalSpeedLabelText.Text = LocalizationManager.Text("BombAudioFinalSpeedLabel");
            BombAudioToggle.OffContent = LocalizationManager.Text("Off");
            BombAudioToggle.OnContent = LocalizationManager.Text("On");
            BombAudioCustomLabelText.Text = LocalizationManager.Text("BombAudioCustomLabel");
            BombTimerAudioLabelText.Text = LocalizationManager.Text("BombTimerAudioLabel");
            BombExplodedAudioLabelText.Text = LocalizationManager.Text("BombExplodedAudioLabel");
            BombDefusedAudioLabelText.Text = LocalizationManager.Text("BombDefusedAudioLabel");
            BombTimerAudioImportButtonText.Text = LocalizationManager.Text("Import");
            BombExplodedAudioImportButtonText.Text = LocalizationManager.Text("Import");
            BombDefusedAudioImportButtonText.Text = LocalizationManager.Text("Import");
            ToolTipService.SetToolTip(BombTimerAudioPreviewButton, LocalizationManager.Text("BombAudioPreview"));
            ToolTipService.SetToolTip(BombExplodedAudioPreviewButton, LocalizationManager.Text("BombAudioPreview"));
            ToolTipService.SetToolTip(BombDefusedAudioPreviewButton, LocalizationManager.Text("BombAudioPreview"));
            UpdateFullEffectPreviewButtonText();
            UpdateBombAudioStatusTexts();
        }

        internal void ApplyTheme(GameThemePalette theme)
        {
            if (theme == null) return;
            BombAudioHintText.Foreground = new SolidColorBrush(theme.MutedText);
            AdvancedEffectsPanelSupport.ApplySoftenedTree(this, theme);
        }

        internal void RefreshSettings()
        {
            SelectBombAudioSettings();
            UpdateBombAudioStatusTexts();
        }

        private void SelectBombAudioSettings()
        {
            _suppressBombAudioEvents = true;
            try
            {
                BombAudioSettingsValues settings = BombAudioSettingsStore.Load();
                BombAudioToggle.IsOn = settings.Enabled;
                BombAudioVolumeSlider.Value = settings.VolumePercent;
                BombAudioInitialSpeedSlider.Value = settings.InitialSpeedPercent;
                BombAudioFinalSpeedSlider.Value = settings.FinalSpeedPercent;
                SetBombAudioControlsEnabled(settings.Enabled);
                UpdateBombAudioVolumeText(settings.VolumePercent);
                UpdateBombAudioSpeedTexts();
            }
            finally
            {
                _suppressBombAudioEvents = false;
            }
        }

        private async void OnBombAudioToggled(object sender, RoutedEventArgs e)
        {
            if (_suppressBombAudioEvents)
            {
                return;
            }

            SetBombAudioControlsEnabled(BombAudioToggle.IsOn);
            SaveBombAudioSettings();
            await SyncBombAudioSettingsAsync();
        }

        private void OnBombAudioVolumeChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            UpdateBombAudioVolumeText(e.NewValue);
            if (_suppressBombAudioEvents)
            {
                return;
            }

            SaveBombAudioSettings();
            _bombAudioSyncTimer.Stop();
            _bombAudioSyncTimer.Start();
        }

        private void OnBombAudioSpeedChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            // Setting Slider.Minimum during XAML construction can raise ValueChanged
            // before the sibling speed slider has been created.
            if (_suppressBombAudioEvents
                || BombAudioInitialSpeedSlider == null
                || BombAudioFinalSpeedSlider == null)
            {
                return;
            }

            if (BombAudioFinalSpeedSlider.Value < BombAudioInitialSpeedSlider.Value)
            {
                BombAudioFinalSpeedSlider.Value = BombAudioInitialSpeedSlider.Value;
            }

            UpdateBombAudioSpeedTexts();
            SaveBombAudioSettings();
            _bombAudioSyncTimer.Stop();
            _bombAudioSyncTimer.Start();
        }

        private async void OnBombAudioSyncTimerTick(object sender, object e)
        {
            _bombAudioSyncTimer.Stop();
            await SyncBombAudioSettingsAsync();
        }

        private void SaveBombAudioSettings()
        {
            BombAudioSettingsStore.Save(
                BombAudioToggle.IsOn,
                BombAudioVolumeSlider.Value,
                BombAudioInitialSpeedSlider.Value,
                BombAudioFinalSpeedSlider.Value);
        }

        private void UpdateBombAudioVolumeText(double value)
        {
            BombAudioVolumeValueText.Text = Math.Round(value) + "%";
        }

        private void UpdateBombAudioSpeedTexts()
        {
            BombAudioInitialSpeedValueText.Text = string.Format(
                "{0:0.00}×",
                BombAudioInitialSpeedSlider.Value / 100.0);
            BombAudioFinalSpeedValueText.Text = string.Format(
                "{0:0.00}×",
                BombAudioFinalSpeedSlider.Value / 100.0);
        }

        private async void OnBombAudioImportClick(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string kind)
            {
                var picker = new Windows.Storage.Pickers.FileOpenPicker();
                picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.MusicLibrary;
                picker.FileTypeFilter.Add(".wav");
                picker.FileTypeFilter.Add(".mp3");
                picker.FileTypeFilter.Add(".m4a");
                StorageFile file = await picker.PickSingleFileAsync();
                if (file != null)
                {
                    try
                    {
                        await BombAudioSettingsStore.ImportCustomAudioAsync(kind, file);
                        UpdateBombAudioStatusTexts();
                        await SyncBombAudioSettingsAsync();
                    }
                    catch (Exception ex)
                    {
                        App.Log("Import bomb audio failed: " + ex);
                    }
                }
            }
        }

        private async void OnBombAudioClearClick(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string kind)
            {
                BombAudioSettingsStore.ClearCustomAudio(kind);
                UpdateBombAudioStatusTexts();
                await SyncBombAudioSettingsAsync();
            }
        }

        private async void OnBombAudioPreviewClick(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button button) || !(button.Tag is string kind)) return;
            try
            {
                ResetFullEffectPreviewState();
                await SyncCurrentBombAudioSettingsAsync();
                await BombAudioSettingsStore.PreviewAsync(kind);
            }
            catch (Exception ex)
            {
                App.Log("Preview bomb audio material failed: " + ex);
            }
        }

        private async void OnBombFullEffectPreviewClick(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_fullEffectPreviewActive)
                {
                    await BombAudioSettingsStore.PreviewAsync("stop");
                    ResetFullEffectPreviewState();
                    return;
                }

                await SyncCurrentBombAudioSettingsAsync();
                await BombAudioSettingsStore.PreviewAsync("full");
                _fullEffectPreviewActive = true;
                _fullEffectPreviewTimer.Stop();
                _fullEffectPreviewTimer.Start();
                UpdateFullEffectPreviewButtonText();
            }
            catch (Exception ex)
            {
                ResetFullEffectPreviewState();
                App.Log("Preview full bomb audio effect failed: " + ex);
            }
        }

        private void OnFullEffectPreviewTimerTick(object sender, object e)
        {
            ResetFullEffectPreviewState();
        }

        private async Task SyncCurrentBombAudioSettingsAsync()
        {
            _bombAudioSyncTimer.Stop();
            SaveBombAudioSettings();
            await SyncBombAudioSettingsAsync();
        }

        private void ResetFullEffectPreviewState()
        {
            _fullEffectPreviewTimer.Stop();
            _fullEffectPreviewActive = false;
            UpdateFullEffectPreviewButtonText();
        }

        private void UpdateFullEffectPreviewButtonText()
        {
            if (BombFullEffectPreviewButtonText == null) return;
            BombFullEffectPreviewButtonText.Text = LocalizationManager.Text(
                _fullEffectPreviewActive ? "BombAudioStopPreview" : "BombAudioFullPreview");
        }

        private void UpdateBombAudioStatusTexts()
        {
            UpdateSlotStatus(BombAudioSettingsStore.TimerKind, BombTimerAudioStatusText, BombTimerAudioClearButton);
            UpdateSlotStatus(BombAudioSettingsStore.ExplodedKind, BombExplodedAudioStatusText, BombExplodedAudioClearButton);
            UpdateSlotStatus(BombAudioSettingsStore.DefusedKind, BombDefusedAudioStatusText, BombDefusedAudioClearButton);
        }

        private static void UpdateSlotStatus(string kind, TextBlock statusText, Button clearButton)
        {
            if (statusText == null) return;
            if (BombAudioSettingsStore.HasCustomAudio(kind))
            {
                string path = BombAudioSettingsStore.GetStoredAudioPath(kind);
                string fileName = System.IO.Path.GetFileName(path);
                statusText.Text = string.IsNullOrEmpty(fileName) ? LocalizationManager.Text("Custom") : fileName;
                statusText.Foreground = new SolidColorBrush(Windows.UI.Colors.CornflowerBlue);
                if (clearButton != null) clearButton.Visibility = Visibility.Visible;
            }
            else
            {
                statusText.Text = LocalizationManager.Text("BuiltIn");
                statusText.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 136, 136, 136));
                if (clearButton != null) clearButton.Visibility = Visibility.Collapsed;
            }
        }

        private void SetBombAudioControlsEnabled(bool enabled)
        {
            BombAudioVolumeSlider.IsEnabled = enabled;
            BombAudioInitialSpeedSlider.IsEnabled = enabled;
            BombAudioFinalSpeedSlider.IsEnabled = enabled;
        }

        private static async Task SyncBombAudioSettingsAsync()
        {
            try
            {
                await BombAudioSettingsStore.SyncAsync();
            }
            catch (Exception ex)
            {
                App.Log("Set bomb audio settings failed: " + ex);
            }
        }
    }
}

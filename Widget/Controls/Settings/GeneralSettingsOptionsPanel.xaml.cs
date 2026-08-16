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
    public sealed partial class GeneralSettingsOptionsPanel : UserControl
    {
        private bool _suppressCloseBehaviorEvents;
        private bool _suppressSpectatedKillEffectsEvents;
        private bool _suppressGsiGameVersionEvents;
        private bool _suppressBombAudioEvents = true;
        private bool _suppressAutoCloseOnGameExitEvents;
        private bool _suppressInterruptPreviousKillAudioEvents;
        private readonly DispatcherTimer _bombAudioSyncTimer = new DispatcherTimer();

        public GeneralSettingsOptionsPanel()
        {
            InitializeComponent();
            InitializeProcessPrioritySettings();
            _bombAudioSyncTimer.Interval = TimeSpan.FromMilliseconds(250);
            _bombAudioSyncTimer.Tick += OnBombAudioSyncTimerTick;
            ApplyLanguage();
            RefreshSettings();
            Loaded += OnLoaded;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            RefreshSettings();
            ApplyTheme(GameThemePalette.Current);
            await RefreshProcessPriorityStateAsync();
        }

        internal void ApplyLanguage()
        {
            RuntimePanel.ApplyLanguage();
            CloseBehaviorLabelText.Text = LocalizationManager.Text("CloseBehaviorLabel");
            CloseWindowTrayItem.Content = LocalizationManager.Text("CloseWindowTray");
            CloseWindowExitItem.Content = LocalizationManager.Text("CloseWindowExit");
            GsiGameVersionLabelText.Text = LocalizationManager.Text("GsiGameVersionLabel");
            GsiGameVersionHintText.Text = LocalizationManager.Text("GsiGameVersionHint");
            GsiGameVersionCs2Item.Content = LocalizationManager.Text("GsiGameVersionCs2");
            GsiGameVersionCsgoLegacyItem.Content =
                LocalizationManager.Text("GsiGameVersionCsgoLegacy");
            SpectatedKillEffectsLabelText.Text =
                LocalizationManager.Text("SpectatedKillEffectsLabel");
            SpectatedKillEffectsHintText.Text =
                LocalizationManager.Text("SpectatedKillEffectsHint");
            SpectatedKillEffectsToggle.OffContent = LocalizationManager.Text("Off");
            SpectatedKillEffectsToggle.OnContent = LocalizationManager.Text("On");
            BombAudioLabelText.Text = LocalizationManager.Text("BombAudioLabel");
            BombAudioHintText.Text = LocalizationManager.Text("BombAudioHint");
            BombAudioVolumeLabelText.Text = LocalizationManager.Text("BombAudioVolumeLabel");
            BombAudioSpeedLabelText.Text = LocalizationManager.Text("BombAudioSpeedLabel");
            BombAudioInitialSpeedLabelText.Text =
                LocalizationManager.Text("BombAudioInitialSpeedLabel");
            BombAudioFinalSpeedLabelText.Text =
                LocalizationManager.Text("BombAudioFinalSpeedLabel");
            BombAudioToggle.OffContent = LocalizationManager.Text("Off");
            BombAudioToggle.OnContent = LocalizationManager.Text("On");
            BombAudioCustomLabelText.Text = LocalizationManager.Text("BombAudioCustomLabel");
            BombTimerAudioLabelText.Text = LocalizationManager.Text("BombTimerAudioLabel");
            BombExplodedAudioLabelText.Text = LocalizationManager.Text("BombExplodedAudioLabel");
            BombDefusedAudioLabelText.Text = LocalizationManager.Text("BombDefusedAudioLabel");
            BombTimerAudioImportButtonText.Text = LocalizationManager.Text("Import");
            BombExplodedAudioImportButtonText.Text = LocalizationManager.Text("Import");
            BombDefusedAudioImportButtonText.Text = LocalizationManager.Text("Import");
            AutoCloseOnGameExitLabelText.Text =
                LocalizationManager.Text("AutoCloseOnGameExitLabel");
            AutoCloseOnGameExitHintText.Text =
                LocalizationManager.Text("AutoCloseOnGameExitHint");
            AutoCloseOnGameExitToggle.OffContent = LocalizationManager.Text("Off");
            AutoCloseOnGameExitToggle.OnContent = LocalizationManager.Text("On");
            InterruptPreviousKillAudioLabelText.Text =
                LocalizationManager.Text("InterruptPreviousKillAudioLabel");
            InterruptPreviousKillAudioHintText.Text =
                LocalizationManager.Text("InterruptPreviousKillAudioHint");
            InterruptPreviousKillAudioToggle.OffContent = LocalizationManager.Text("Off");
            InterruptPreviousKillAudioToggle.OnContent = LocalizationManager.Text("On");
            UpdateBombAudioStatusTexts();
            ApplyProcessPriorityLanguage();
        }

        internal void ApplyTheme(GameThemePalette theme)
        {
            if (theme == null)
            {
                return;
            }

            RuntimePanel.ApplyTheme(theme);
            GsiGameVersionHintText.Foreground = new SolidColorBrush(theme.MutedText);
            SpectatedKillEffectsHintText.Foreground = new SolidColorBrush(theme.MutedText);
            BombAudioHintText.Foreground = new SolidColorBrush(theme.MutedText);
            AutoCloseOnGameExitHintText.Foreground = new SolidColorBrush(theme.MutedText);
            InterruptPreviousKillAudioHintText.Foreground = new SolidColorBrush(theme.MutedText);
            ProcessPriorityHintText.Foreground = new SolidColorBrush(theme.MutedText);
            ProcessPriorityPersistenceHintText.Foreground = new SolidColorBrush(theme.MutedText);
            GameBarPriorityStatusText.Foreground = new SolidColorBrush(theme.MutedText);
            GameBarFtServerPriorityStatusText.Foreground = new SolidColorBrush(theme.MutedText);
            KillConfirmWidgetPriorityStatusText.Foreground = new SolidColorBrush(theme.MutedText);
            AdvancedEffectsPanelSupport.ApplySoftenedTree(this, theme);
        }

        internal void RefreshSettings()
        {
            SelectCloseBehavior();
            SelectGsiGameVersion();
            SelectSpectatedKillEffects();
            SelectBombAudioSettings();
            SelectProcessPrioritySettings();
            SelectAutoCloseOnGameExit();
            SelectInterruptPreviousKillAudio();
            UpdateBombAudioStatusTexts();
        }

        private void SelectGsiGameVersion()
        {
            _suppressGsiGameVersionEvents = true;
            try
            {
                SelectTaggedItem(GsiGameVersionSelector, GsiGameVersionSettingsStore.Load());
            }
            finally
            {
                _suppressGsiGameVersionEvents = false;
            }
        }

        private async void OnGsiGameVersionSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressGsiGameVersionEvents)
            {
                return;
            }

            if (GsiGameVersionSelector.SelectedItem is ComboBoxItem selected
                && selected.Tag is string version)
            {
                GsiGameVersionSettingsStore.Save(version);
                try
                {
                    await GsiGameVersionSettingsStore.SyncAsync();
                }
                catch (Exception ex)
                {
                    App.Log("Set GSI game version failed: " + ex);
                }
            }
        }

        private void SelectCloseBehavior()
        {
            _suppressCloseBehaviorEvents = true;
            try
            {
                SelectTaggedItem(CloseBehaviorSelector, CloseBehaviorSettingsStore.Load());
            }
            finally
            {
                _suppressCloseBehaviorEvents = false;
            }
        }

        private void OnCloseBehaviorSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressCloseBehaviorEvents)
            {
                return;
            }

            if (CloseBehaviorSelector.SelectedItem is ComboBoxItem selected
                && selected.Tag is string mode)
            {
                CloseBehaviorSettingsStore.Save(mode);
            }
        }

        private void SelectSpectatedKillEffects()
        {
            _suppressSpectatedKillEffectsEvents = true;
            try
            {
                SpectatedKillEffectsToggle.IsOn =
                    SharedStreakSettingsStore.LoadSpectatedKillEffects();
            }
            finally
            {
                _suppressSpectatedKillEffectsEvents = false;
            }
        }

        private async void OnSpectatedKillEffectsToggled(object sender, RoutedEventArgs e)
        {
            if (_suppressSpectatedKillEffectsEvents)
            {
                return;
            }

            SharedStreakSettingsStore.SaveSpectatedKillEffects(SpectatedKillEffectsToggle.IsOn);
            try
            {
                await SharedStreakSettingsStore.SyncSpectatedKillEffectsAsync();
            }
            catch (Exception ex)
            {
                // The local value is authoritative and will be synchronized at service startup.
                App.Log("Set spectated player kill effects failed: " + ex);
            }
        }

        private void SelectAutoCloseOnGameExit()
        {
            _suppressAutoCloseOnGameExitEvents = true;
            try
            {
                AutoCloseOnGameExitToggle.IsOn = AutoCloseOnGameExitSettingsStore.Load();
            }
            finally
            {
                _suppressAutoCloseOnGameExitEvents = false;
            }
        }

        private void OnAutoCloseOnGameExitToggled(object sender, RoutedEventArgs e)
        {
            if (_suppressAutoCloseOnGameExitEvents)
            {
                return;
            }

            AutoCloseOnGameExitSettingsStore.Save(AutoCloseOnGameExitToggle.IsOn);
        }

        private void SelectInterruptPreviousKillAudio()
        {
            _suppressInterruptPreviousKillAudioEvents = true;
            try
            {
                InterruptPreviousKillAudioToggle.IsOn = InterruptPreviousKillAudioSettingsStore.Load();
            }
            finally
            {
                _suppressInterruptPreviousKillAudioEvents = false;
            }
        }

        private async void OnInterruptPreviousKillAudioToggled(object sender, RoutedEventArgs e)
        {
            if (_suppressInterruptPreviousKillAudioEvents)
            {
                return;
            }

            InterruptPreviousKillAudioSettingsStore.Save(InterruptPreviousKillAudioToggle.IsOn);
            try
            {
                await InterruptPreviousKillAudioSettingsStore.SyncAsync();
            }
            catch (Exception ex)
            {
                // The local value is authoritative and will be synchronized at service startup.
                App.Log("Set interrupt previous kill audio failed: " + ex);
            }
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

        private static void SelectTaggedItem(ComboBox selector, string target)
        {
            foreach (object entry in selector.Items)
            {
                if (entry is ComboBoxItem item
                    && string.Equals(item.Tag as string, target, StringComparison.OrdinalIgnoreCase))
                {
                    selector.SelectedItem = item;
                    return;
                }
            }

            selector.SelectedIndex = 0;
        }
    }
}

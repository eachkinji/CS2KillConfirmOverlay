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
        private readonly DispatcherTimer _bombAudioSyncTimer = new DispatcherTimer();

        public GeneralSettingsOptionsPanel()
        {
            InitializeComponent();
            _bombAudioSyncTimer.Interval = TimeSpan.FromMilliseconds(250);
            _bombAudioSyncTimer.Tick += OnBombAudioSyncTimerTick;
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
            AdvancedEffectsPanelSupport.ApplySoftenedTree(this, theme);
        }

        internal void RefreshSettings()
        {
            SelectCloseBehavior();
            SelectGsiGameVersion();
            SelectSpectatedKillEffects();
            SelectBombAudioSettings();
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
            if (BombAudioFinalSpeedSlider.Value < BombAudioInitialSpeedSlider.Value)
            {
                BombAudioFinalSpeedSlider.Value = BombAudioInitialSpeedSlider.Value;
            }
            if (_suppressBombAudioEvents)
            {
                return;
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

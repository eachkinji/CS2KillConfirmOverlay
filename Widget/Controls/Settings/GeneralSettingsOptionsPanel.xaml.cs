using System;
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

        public GeneralSettingsOptionsPanel()
        {
            InitializeComponent();
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
            AdvancedEffectsPanelSupport.ApplySoftenedTree(this, theme);
        }

        internal void RefreshSettings()
        {
            SelectCloseBehavior();
            SelectGsiGameVersion();
            SelectSpectatedKillEffects();
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

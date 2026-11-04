using System;
using System.Threading.Tasks;
using KillConfirmGameBar.Controls.GameStyles;
using KillConfirmGameBar.Services;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace KillConfirmGameBar.Controls.Settings
{
    public sealed partial class AdvancedSystemOptionsPanel : UserControl
    {
        private bool _suppressGsiGameVersionEvents;

        public AdvancedSystemOptionsPanel()
        {
            InitializeComponent();
            ApplyLanguage();
            RefreshSettings();
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            RefreshSettings();
            ApplyTheme(GameThemePalette.Home);
        }

        internal void ApplyLanguage()
        {
            GsiGameVersionLabelText.Text = LocalizationManager.Text("GsiGameVersionLabel");
            GsiGameVersionHintText.Text = LocalizationManager.Text("GsiGameVersionHint");
            GsiGameVersionCs2Item.Content = LocalizationManager.Text("GsiGameVersionCs2");
            GsiGameVersionCsgoLegacyItem.Content =
                LocalizationManager.Text("GsiGameVersionCsgoLegacy");
        }

        internal void ApplyTheme(GameThemePalette theme)
        {
            if (theme == null)
            {
                return;
            }

            GsiGameVersionHintText.Foreground = new SolidColorBrush(theme.MutedText);
            AdvancedEffectsPanelSupport.ApplySoftenedTree(this, theme);
        }

        internal void RefreshSettings()
        {
            SelectGsiGameVersion();
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

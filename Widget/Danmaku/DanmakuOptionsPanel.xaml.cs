using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using KillConfirmGameBar.Services;

namespace KillConfirmGameBar.Danmaku
{
    public sealed partial class DanmakuOptionsPanel : UserControl
    {
        private bool _suppressEvents = false;

        public DanmakuOptionsPanel()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            SyncFromStore();
        }

        public void RefreshSettings()
        {
            SyncFromStore();
        }

        private void SyncFromStore()
        {
            _suppressEvents = true;
            try
            {
                // Scenarios
                TriggerOnKillToggle.IsOn = DanmakuSettingsStore.TriggerOnKill;
                TriggerOnDeathToggle.IsOn = DanmakuSettingsStore.TriggerOnDeath;

                // Count
                int count = DanmakuSettingsStore.Count;
                SelectComboItemByTag(CountSelector, count.ToString());

                // Duration
                int duration = (int)Math.Round(DanmakuSettingsStore.DurationSeconds);
                SelectComboItemByTag(DurationSelector, duration.ToString());

                // Area
                int area = (int)DanmakuSettingsStore.Area;
                SelectComboItemByTag(AreaSelector, area.ToString());

                // Speed
                int speed = (int)DanmakuSettingsStore.Speed;
                SelectComboItemByTag(SpeedSelector, speed.ToString());

                // FontSize
                int fontSize = DanmakuSettingsStore.FontSize;
                SelectComboItemByTag(FontSizeSelector, fontSize.ToString());

                // FontWeight
                int fontWeight = (int)DanmakuSettingsStore.FontWeight;
                SelectComboItemByTag(FontWeightSelector, fontWeight.ToString());

                // Background & Outline
                BackgroundToggle.IsOn = DanmakuSettingsStore.ShowBackground;
                OutlineToggle.IsOn = DanmakuSettingsStore.ShowOutline;
            }
            finally
            {
                _suppressEvents = false;
            }
        }

        private static void SelectComboItemByTag(ComboBox comboBox, string tag)
        {
            if (comboBox == null) return;
            for (int i = 0; i < comboBox.Items.Count; i++)
            {
                if (comboBox.Items[i] is ComboBoxItem item && string.Equals(item.Tag?.ToString(), tag, StringComparison.OrdinalIgnoreCase))
                {
                    comboBox.SelectedIndex = i;
                    return;
                }
            }
            if (comboBox.SelectedIndex < 0 && comboBox.Items.Count > 0)
            {
                comboBox.SelectedIndex = 0;
            }
        }

        private void OnTriggerOnKillToggled(object sender, RoutedEventArgs e)
        {
            if (_suppressEvents) return;
            DanmakuSettingsStore.TriggerOnKill = TriggerOnKillToggle.IsOn;
        }

        private void OnTriggerOnDeathToggled(object sender, RoutedEventArgs e)
        {
            if (_suppressEvents) return;
            DanmakuSettingsStore.TriggerOnDeath = TriggerOnDeathToggle.IsOn;
        }

        private void OnCountSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressEvents) return;
            if (CountSelector.SelectedItem is ComboBoxItem item && int.TryParse(item.Tag?.ToString(), out int val))
            {
                DanmakuSettingsStore.Count = val;
            }
        }

        private void OnDurationSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressEvents) return;
            if (DurationSelector.SelectedItem is ComboBoxItem item && double.TryParse(item.Tag?.ToString(), out double val))
            {
                DanmakuSettingsStore.DurationSeconds = val;
            }
        }

        private void OnAreaSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressEvents) return;
            if (AreaSelector.SelectedItem is ComboBoxItem item && int.TryParse(item.Tag?.ToString(), out int val))
            {
                DanmakuSettingsStore.Area = (DanmakuDisplayArea)val;
            }
        }

        private void OnSpeedSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressEvents) return;
            if (SpeedSelector.SelectedItem is ComboBoxItem item && int.TryParse(item.Tag?.ToString(), out int val))
            {
                DanmakuSettingsStore.Speed = (DanmakuSpeedMode)val;
            }
        }

        private void OnFontSizeSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressEvents) return;
            if (FontSizeSelector.SelectedItem is ComboBoxItem item && int.TryParse(item.Tag?.ToString(), out int val))
            {
                DanmakuSettingsStore.FontSize = val;
            }
        }

        private void OnFontWeightSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressEvents) return;
            if (FontWeightSelector.SelectedItem is ComboBoxItem item && int.TryParse(item.Tag?.ToString(), out int val))
            {
                DanmakuSettingsStore.FontWeight = (DanmakuFontWeightMode)val;
            }
        }

        private void OnBackgroundToggled(object sender, RoutedEventArgs e)
        {
            if (_suppressEvents) return;
            DanmakuSettingsStore.ShowBackground = BackgroundToggle.IsOn;
        }

        private void OnOutlineToggled(object sender, RoutedEventArgs e)
        {
            if (_suppressEvents) return;
            DanmakuSettingsStore.ShowOutline = OutlineToggle.IsOn;
        }

        private void OnKillTestClick(object sender, RoutedEventArgs e)
        {
            DanmakuSettingsStore.RequestKillTest();
        }

        private void OnDeathTestClick(object sender, RoutedEventArgs e)
        {
            DanmakuSettingsStore.RequestDeathTest();
        }

        internal void ApplyTheme(GameThemePalette theme)
        {
            if (theme == null) return;
            OptionsCard.Background = new SolidColorBrush(theme.Card);
            OptionsCard.BorderBrush = new SolidColorBrush(theme.SoftBorder);
            PanelTitle.Foreground = new SolidColorBrush(theme.Text);
            PanelSubtitle.Foreground = new SolidColorBrush(theme.MutedText);
            TriggerOnKillLabel.Foreground = new SolidColorBrush(theme.Text);
            TriggerOnDeathLabel.Foreground = new SolidColorBrush(theme.Text);
            CountLabel.Foreground = new SolidColorBrush(theme.Text);
            DurationLabel.Foreground = new SolidColorBrush(theme.Text);
            AreaLabel.Foreground = new SolidColorBrush(theme.Text);
            SpeedLabel.Foreground = new SolidColorBrush(theme.Text);
            FontSizeLabel.Foreground = new SolidColorBrush(theme.Text);
            FontWeightLabel.Foreground = new SolidColorBrush(theme.Text);
            OutlineLabel.Foreground = new SolidColorBrush(theme.Text);
            BackgroundLabel.Foreground = new SolidColorBrush(theme.Text);
        }
    }
}

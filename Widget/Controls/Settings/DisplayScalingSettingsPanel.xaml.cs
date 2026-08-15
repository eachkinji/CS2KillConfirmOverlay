using System;
using KillConfirmGameBar.Controls.GameStyles;
using KillConfirmGameBar.Services;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace KillConfirmGameBar.Controls.Settings
{
    public sealed partial class DisplayScalingSettingsPanel : UserControl
    {
        private bool _suppressSelectionEvents;

        public DisplayScalingSettingsPanel()
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
            bool isChinese = LocalizationManager.Current == UiLanguage.SimplifiedChinese;
            ScaleLabelText.Text = isChinese ? "Game Bar 控制面板缩放" : "Game Bar control panel scale";
            ScaleHintText.Text = isChinese
                ? "只放大控制面板、文字和点击区域，不改变击杀图标大小。所有显示环境默认均为 100%，请按需要手动选择。"
                : "Scales only the control panel, text, and hit targets. Kill icons keep their configured size. Every display defaults to 100%; choose the scale manually.";
        }

        internal void ApplyTheme(GameThemePalette theme)
        {
            if (theme == null)
            {
                return;
            }

            ScaleHintText.Foreground = new SolidColorBrush(theme.MutedText);
            AdvancedEffectsPanelSupport.ApplySoftenedTree(this, theme);
        }

        internal void RefreshSettings()
        {
            _suppressSelectionEvents = true;
            try
            {
                string selectedMode = ControlPanelScaleSettingsStore.Load();
                foreach (object entry in ScaleSelector.Items)
                {
                    if (entry is ComboBoxItem item
                        && string.Equals(item.Tag as string, selectedMode, StringComparison.OrdinalIgnoreCase))
                    {
                        ScaleSelector.SelectedItem = item;
                        break;
                    }
                }

                if (ScaleSelector.SelectedItem == null)
                {
                    ScaleSelector.SelectedIndex = 0;
                }
            }
            finally
            {
                _suppressSelectionEvents = false;
            }
        }

        private void OnScaleSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressSelectionEvents)
            {
                return;
            }

            if (ScaleSelector.SelectedItem is ComboBoxItem item && item.Tag is string mode)
            {
                ControlPanelScaleSettingsStore.Save(mode);
            }
        }
    }
}

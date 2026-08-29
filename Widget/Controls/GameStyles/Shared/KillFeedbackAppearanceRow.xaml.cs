using System;
using KillConfirmGameBar.Services;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace KillConfirmGameBar.Controls.GameStyles
{
    public sealed partial class KillFeedbackAppearanceRow : UserControl
    {
        private bool _suppressChanges;
        private CrosshairOffsetEditor _crosshairOffsetEditor;

        public KillFeedbackAppearanceRow()
        {
            InitializeComponent();
            BrightnessSlider.ValueChanged += OnSliderValueChanged;
            ContrastSlider.ValueChanged += OnSliderValueChanged;
            OpacitySlider.ValueChanged += OnSliderValueChanged;
        }

        public event EventHandler SettingsChanged;
        public event EventHandler CrosshairOffsetChanged;

        public bool IsLayerEnabled => EnabledToggle.IsOn;
        public double BrightnessPercent => BrightnessSlider.Value;
        public double ContrastPercent => ContrastSlider.Value;
        public double OpacityPercent => OpacitySlider.Value;

        internal void Configure(
            string layerName,
            bool enabled,
            double brightnessPercent,
            double contrastPercent,
            double opacityPercent,
            bool isChinese,
            GameThemePalette theme)
        {
            _suppressChanges = true;
            try
            {
                LayerNameText.Text = layerName;
                EnabledToggle.IsOn = enabled;
                EnabledToggle.OnContent = isChinese ? "显示" : "Visible";
                EnabledToggle.OffContent = isChinese ? "隐藏" : "Hidden";
                BrightnessLabel.Text = isChinese ? "亮度" : "Brightness";
                ContrastLabel.Text = isChinese ? "对比度" : "Contrast";
                OpacityLabel.Text = isChinese ? "透明度" : "Opacity";
                BrightnessSlider.Value = Clamp(brightnessPercent, 50, 150);
                ContrastSlider.Value = Clamp(contrastPercent, 50, 150);
                OpacitySlider.Value = Clamp(opacityPercent, 10, 100);
                UpdateValueLabels();
                UpdateAdjustmentAvailability();
                ApplyTheme(theme);
            }
            finally
            {
                _suppressChanges = false;
            }
        }

        internal void ConfigureCrosshairOffset(
            GameStyleMode style,
            bool isChinese,
            GameThemePalette theme)
        {
            if (_crosshairOffsetEditor == null)
            {
                _crosshairOffsetEditor = new CrosshairOffsetEditor();
                _crosshairOffsetEditor.SettingsChanged += OnCrosshairOffsetChanged;
                AdditionalSettingsHost.Content = _crosshairOffsetEditor;
            }

            _crosshairOffsetEditor.Initialize(style);
            _crosshairOffsetEditor.ApplyLanguage(isChinese);
            _crosshairOffsetEditor.ApplyTheme(theme);
            AdditionalSettingsHost.Visibility = Visibility.Visible;
            UpdateAdjustmentAvailability();
        }

        private void ApplyTheme(GameThemePalette theme)
        {
            if (theme == null)
            {
                return;
            }

            Card.Background = new SolidColorBrush(theme.Card);
            Card.BorderBrush = new SolidColorBrush(theme.SoftBorder);
            LayerNameText.Foreground = new SolidColorBrush(theme.Text);
            EnabledToggle.Foreground = new SolidColorBrush(theme.Text);
            BrightnessLabel.Foreground = new SolidColorBrush(theme.MutedText);
            ContrastLabel.Foreground = new SolidColorBrush(theme.MutedText);
            OpacityLabel.Foreground = new SolidColorBrush(theme.MutedText);
            BrightnessValueText.Foreground = new SolidColorBrush(theme.AccentText);
            ContrastValueText.Foreground = new SolidColorBrush(theme.AccentText);
            OpacityValueText.Foreground = new SolidColorBrush(theme.AccentText);
            BrightnessSlider.Foreground = new SolidColorBrush(theme.Accent);
            ContrastSlider.Foreground = new SolidColorBrush(theme.Accent);
            OpacitySlider.Foreground = new SolidColorBrush(theme.Accent);
        }

        private void OnSettingChanged(object sender, RoutedEventArgs e)
        {
            UpdateAdjustmentAvailability();
            RaiseSettingsChanged();
        }

        private void OnSliderValueChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            UpdateValueLabels();
            RaiseSettingsChanged();
        }

        private void OnCrosshairOffsetChanged(object sender, RoutedEventArgs e)
        {
            CrosshairOffsetChanged?.Invoke(this, EventArgs.Empty);
        }

        private void UpdateAdjustmentAvailability()
        {
            BrightnessSlider.IsEnabled = EnabledToggle.IsOn;
            ContrastSlider.IsEnabled = EnabledToggle.IsOn;
            OpacitySlider.IsEnabled = EnabledToggle.IsOn;
            AdjustmentGrid.Opacity = EnabledToggle.IsOn ? 1.0 : 0.5;
            AdditionalSettingsHost.IsEnabled = EnabledToggle.IsOn;
            AdditionalSettingsHost.Opacity = EnabledToggle.IsOn ? 1.0 : 0.5;
        }

        private void UpdateValueLabels()
        {
            BrightnessValueText.Text = Math.Round(BrightnessSlider.Value) + "%";
            ContrastValueText.Text = Math.Round(ContrastSlider.Value) + "%";
            OpacityValueText.Text = Math.Round(OpacitySlider.Value) + "%";
        }

        private void RaiseSettingsChanged()
        {
            if (!_suppressChanges)
            {
                SettingsChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private static double Clamp(double value, double minimum, double maximum)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                return 100;
            }

            return Math.Max(minimum, Math.Min(maximum, value));
        }
    }
}

using System;
using System.Globalization;
using KillConfirmGameBar.Services;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace KillConfirmGameBar.Controls.GameStyles
{
    public sealed partial class CrosshairOffsetEditor : UserControl
    {
        private bool _suppressEvents;
        private GameStyleMode _style;
        private GameThemePalette _theme;
        private double _currentX;
        private double _currentY;

        public CrosshairOffsetEditor()
        {
            InitializeComponent();
            SizeChanged += OnEditorSizeChanged;
        }

        public event RoutedEventHandler SettingsChanged;

        internal void Initialize(GameStyleMode style)
        {
            _style = style;
            CrosshairOffset offset = CrosshairOffsetSettingsStore.Load(style);
            SetEditorValues(offset.X, offset.Y);
        }

        public void ApplyLanguage(bool isChinese)
        {
            TitleLabel.Text = isChinese ? "准心中心微调" : "Crosshair center nudge";
            QuickPresetLabel.Text = isChinese ? "快速方案" : "Quick presets";
            ManualInputLabel.Text = isChinese ? "手动输入" : "Manual input";
            DefaultPresetButton.Content = isChinese ? "默认 0/0" : "Default 0/0";
            Preset1Button.Content = "① .25/.25";
            Preset2Button.Content = "② .25/.5";
            Preset3Button.Content = "③ .5/.25";
            Preset4Button.Content = "④ .5/.5";
            OffsetXLabel.Text = isChinese ? "X 右移 (px)" : "X right (px)";
            OffsetYLabel.Text = isChinese ? "Y 下移 (px)" : "Y down (px)";
            OffsetHint.Text = isChinese
                ? "正值向右/下，负值向左/上；可输入小数。"
                : "Positive moves right/down, negative left/up; decimals are supported.";
        }

        internal void ApplyTheme(GameThemePalette theme)
        {
            _theme = theme;
            Divider.Background = Brush(theme.SoftBorder);
            TitleLabel.Foreground = Brush(theme.Text);
            CurrentValueText.Foreground = Brush(theme.AccentText);
            QuickPresetLabel.Foreground = Brush(theme.MutedText);
            ManualInputLabel.Foreground = Brush(theme.MutedText);
            OffsetHint.Foreground = Brush(theme.MutedText);
            AdvancedEffectsPanelSupport.ApplyTextInput(OffsetXBox, OffsetXLabel, theme);
            AdvancedEffectsPanelSupport.ApplyTextInput(OffsetYBox, OffsetYLabel, theme);
            RefreshPresetButtons();
        }

        public void Reset()
        {
            SaveValues(
                CrosshairOffsetSettingsStore.DefaultOffsetX,
                CrosshairOffsetSettingsStore.DefaultOffsetY,
                true);
        }

        private void OnEditorSizeChanged(object sender, SizeChangedEventArgs e)
        {
            bool compact = e.NewSize.Width < 720.0;
            Grid.SetColumn(ManualPanel, compact ? 0 : 1);
            Grid.SetRow(ManualPanel, compact ? 1 : 0);
            ManualColumn.Width = compact
                ? new GridLength(0.0)
                : new GridLength(1.0, GridUnitType.Star);
        }

        private void OnPresetClick(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button button) || !(button.Tag is string tag))
            {
                return;
            }

            string[] values = tag.Split('|');
            if (values.Length == 2
                && TryParseOffset(values[0], out double x)
                && TryParseOffset(values[1], out double y))
            {
                SaveValues(x, y, true);
            }
        }

        private void OnOffsetXLostFocus(object sender, RoutedEventArgs e)
        {
            if (!_suppressEvents)
            {
                SaveManualValues();
            }
        }

        private void OnOffsetYLostFocus(object sender, RoutedEventArgs e)
        {
            if (!_suppressEvents)
            {
                SaveManualValues();
            }
        }

        private void OnOffsetKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter && !_suppressEvents)
            {
                SaveManualValues();
                e.Handled = true;
            }
        }

        private void SaveManualValues()
        {
            double x = ParseOffset(OffsetXBox.Text, _currentX);
            double y = ParseOffset(OffsetYBox.Text, _currentY);
            SaveValues(x, y, true);
        }

        private void SaveValues(double x, double y, bool notify)
        {
            CrosshairOffsetSettingsStore.Save(_style, x, y);
            SetEditorValues(x, y);
            if (notify)
            {
                SettingsChanged?.Invoke(this, null);
            }
        }

        private void SetEditorValues(double x, double y)
        {
            _currentX = x;
            _currentY = y;
            _suppressEvents = true;
            try
            {
                OffsetXBox.Text = FormatOffset(x);
                OffsetYBox.Text = FormatOffset(y);
                CurrentValueText.Text = "X " + FormatOffset(x) + " · Y " + FormatOffset(y);
            }
            finally
            {
                _suppressEvents = false;
            }

            RefreshPresetButtons();
        }

        private void RefreshPresetButtons()
        {
            if (_theme == null)
            {
                return;
            }

            ApplyPresetButton(DefaultPresetButton, Matches(0.0, 0.0));
            ApplyPresetButton(Preset1Button, Matches(0.25, 0.25));
            ApplyPresetButton(Preset2Button, Matches(0.25, 0.5));
            ApplyPresetButton(Preset3Button, Matches(0.5, 0.25));
            ApplyPresetButton(Preset4Button, Matches(0.5, 0.5));
        }

        private void ApplyPresetButton(Button button, bool selected)
        {
            button.Background = Brush(selected ? _theme.Accent : _theme.Field);
            button.BorderBrush = Brush(selected ? _theme.Accent : _theme.Border);
            button.Foreground = selected ? Brush(Colors.White) : Brush(_theme.Text);
        }

        private bool Matches(double x, double y)
        {
            return Math.Abs(_currentX - x) < 0.0001
                && Math.Abs(_currentY - y) < 0.0001;
        }

        private static double ParseOffset(string text, double fallback)
        {
            return TryParseOffset(text, out double value) ? value : fallback;
        }

        private static bool TryParseOffset(string text, out double value)
        {
            string normalized = (text ?? string.Empty).Trim();
            bool parsed = double.TryParse(normalized, NumberStyles.Float, CultureInfo.CurrentCulture, out value)
                || double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
            if (!parsed || double.IsNaN(value) || double.IsInfinity(value))
            {
                value = 0.0;
                return false;
            }

            return true;
        }

        private static string FormatOffset(double value)
        {
            return value.ToString("0.##", CultureInfo.InvariantCulture);
        }

        private static SolidColorBrush Brush(Color color)
        {
            return new SolidColorBrush(color);
        }
    }
}

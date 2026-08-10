using KillConfirmGameBar.Services;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace KillConfirmGameBar.Controls.GameStyles
{
    internal static class AdvancedEffectsPanelSupport
    {
        public static void ApplyHeader(TextBlock title, TextBlock hint, GameThemePalette theme)
        {
            if (title != null)
            {
                title.Foreground = Brush(theme.Text);
            }

            if (hint != null)
            {
                hint.Foreground = Brush(theme.MutedText);
            }
        }

        public static void ApplyMoneyRow(TextBlock label, ComboBox selector, GameThemePalette theme)
        {
            if (label != null)
            {
                label.Foreground = Brush(theme.Text);
            }

            ApplyCombo(selector, theme.Text, theme.Field, theme.Border);
        }

        public static void ApplyToggleRow(TextBlock label, ToggleSwitch toggle, GameThemePalette theme)
        {
            if (label != null)
            {
                label.Foreground = Brush(theme.Text);
            }

            if (toggle != null)
            {
                toggle.Foreground = Brush(theme.Text);
                toggle.RequestedTheme = IsDark(theme.Field) ? ElementTheme.Dark : ElementTheme.Light;
            }
        }

        public static void ApplyTextInput(TextBox input, TextBlock hint, GameThemePalette theme)
        {
            if (input != null)
            {
                input.Foreground = Brush(theme.Text);
                input.Background = Brush(theme.Field);
                input.BorderBrush = Brush(theme.Border);
                input.RequestedTheme = IsDark(theme.Field) ? ElementTheme.Dark : ElementTheme.Light;
            }

            if (hint != null)
            {
                hint.Foreground = Brush(theme.MutedText);
            }
        }

        public static void ApplyNotice(Border notice, TextBlock text, GameThemePalette theme)
        {
            if (notice != null)
            {
                notice.Background = Brush(theme.AccentSoft);
                notice.BorderBrush = Brush(theme.WarningBorder);
            }

            if (text != null)
            {
                text.Foreground = Brush(theme.WarningText);
            }
        }

        public static void ApplyCombo(ComboBox comboBox, Color text, Color field, Color border)
        {
            if (comboBox == null)
            {
                return;
            }

            comboBox.Foreground = Brush(text);
            comboBox.Background = Brush(field);
            comboBox.BorderBrush = Brush(border);
            comboBox.RequestedTheme = IsDark(field) ? ElementTheme.Dark : ElementTheme.Light;

            foreach (object item in comboBox.Items)
            {
                if (item is ComboBoxItem comboItem)
                {
                    comboItem.Foreground = Brush(text);
                    comboItem.Background = Brush(field);
                }
            }
        }

        private static SolidColorBrush Brush(Color color)
        {
            return new SolidColorBrush(color);
        }

        private static bool IsDark(Color color)
        {
            return (color.R * 0.299 + color.G * 0.587 + color.B * 0.114) < 128;
        }
    }
}

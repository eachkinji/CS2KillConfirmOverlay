using KillConfirmGameBar.Services;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace KillConfirmGameBar.Controls.Settings
{
    internal static class SettingsPanelSupport
    {
        public static void ApplyPanel(Border card, TextBlock title, TextBlock body, GameThemePalette theme)
        {
            if (card != null)
            {
                card.Background = new SolidColorBrush(theme.Card);
                card.BorderBrush = new SolidColorBrush(theme.SoftBorder);
            }

            ApplyText(title, theme.Text);
            ApplyText(body, theme.MutedText);
        }

        public static void ApplyTag(Border tag, TextBlock text, GameThemePalette theme)
        {
            if (tag != null)
            {
                tag.Background = new SolidColorBrush(theme.AccentSoft);
                tag.BorderBrush = new SolidColorBrush(theme.Border);
            }

            ApplyText(text, theme.Text);
        }

        public static void ApplySettingRow(TextBlock label, ComboBox selector, GameThemePalette theme)
        {
            ApplyText(label, theme.Text);
            if (selector == null)
            {
                return;
            }

            selector.Foreground = new SolidColorBrush(theme.Text);
            selector.Background = new SolidColorBrush(theme.Field);
            selector.BorderBrush = new SolidColorBrush(theme.Border);
            foreach (object option in selector.Items)
            {
                if (option is ComboBoxItem item)
                {
                    item.Foreground = new SolidColorBrush(theme.Text);
                    item.Background = new SolidColorBrush(theme.Field);
                }
            }
        }

        public static void ApplyToggleRow(TextBlock label, ToggleSwitch toggle, GameThemePalette theme)
        {
            ApplyText(label, theme.Text);
            if (toggle != null)
            {
                toggle.Foreground = new SolidColorBrush(theme.Text);
            }
        }

        private static void ApplyText(TextBlock textBlock, Color color)
        {
            if (textBlock != null)
            {
                textBlock.Foreground = new SolidColorBrush(color);
            }
        }
    }
}

using System;
using KillConfirmGameBar.Controls.GameStyles;
using KillConfirmGameBar.Services;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace KillConfirmGameBar
{
    public sealed partial class KillConfirmWidgetPage
    {
        private void LoadGameStyleSelector()
        {
            if (GameStyleSelector == null)
            {
                return;
            }

            _suppressGameStyleEvents = true;
            SelectGameStyleItem(GameStyleService.Current);
            _suppressGameStyleEvents = false;
        }

        private void SelectGameStyleItem(GameStyleMode mode)
        {
            string target = GameStyleService.ToStorageValue(mode);
            foreach (object option in GameStyleSelector.Items)
            {
                if (option is ComboBoxItem item
                    && item.Tag is string tag
                    && string.Equals(tag, target, StringComparison.OrdinalIgnoreCase))
                {
                    GameStyleSelector.SelectedItem = item;
                    return;
                }
            }

            GameStyleSelector.SelectedIndex = 0;
        }

        private GameStyleMode GetSelectedGameStyle()
        {
            if (GameStyleSelector?.SelectedItem is ComboBoxItem item
                && item.Tag is string tag)
            {
                return GameStyleService.FromKey(tag);
            }

            return GameStyleMode.Crossfire;
        }

        private void ApplyGameStyleUi()
        {
            if (ControlPanel == null
                || StatusHintBox == null
                || StatusLightsBox == null
                || GameStyleSelector == null
                || PackTestSection == null
                || VisualSection == null)
            {
                return;
            }

            bool crossfire = GameStyleService.Current == GameStyleMode.Crossfire;
            GameThemePalette theme = GameThemePalette.Current;
            bool darkStyle = IsDark(theme.Field);
            PackTestSection.Visibility = Visibility.Visible;

            MountAdvancedEffectsPanel();

            Color panel = theme.Panel;
            Color card = theme.Card;
            Color field = theme.Field;
            Color border = theme.Border;
            Color text = theme.Text;
            Color muted = theme.MutedText;
            Color accent = theme.Accent;
            Color secondary = theme.Secondary;

            ControlPanel.Background = Brush(panel);
            ControlPanel.BorderBrush = Brush(border);
            StatusHintBox.Background = Brush(field);
            StatusHintBox.BorderBrush = Brush(theme.SoftBorder);
            StatusHintProgressFill.Background = Brush(accent);
            PinHintText.Foreground = Brush(crossfire ? Color.FromArgb(255, 180, 90, 0) : theme.AccentText);
            StatusHintPagerText.Foreground = Brush(muted);
            StatusLightsBox.Background = Brush(field);
            StatusLightsBox.BorderBrush = Brush(theme.SoftBorder);

            LanguageToggleButton.Background = Brush(field);
            LanguageToggleButton.BorderBrush = Brush(theme.SoftBorder);
            OpenGuideButton.Background = Brush(theme.Accent);
            OpenGuideButton.BorderBrush = Brush(crossfire ? Color.FromArgb(255, 197, 106, 0) : theme.AccentText);
            SetButtonTheme(AdvancedEffectsButton, theme.Accent, crossfire ? Color.FromArgb(255, 197, 106, 0) : theme.AccentText, Color.FromArgb(255, 255, 255, 255));
            SetComboTheme(GameStyleSelector, text, field, border);

            ServiceBadgeText.Foreground = Brush(text);
            CfgBadgeText.Foreground = Brush(text);
            GsiBadgeText.Foreground = Brush(text);
            AnimationCacheBadgeText.Foreground = Brush(text);

            PackTestCard.Background = Brush(card);
            PackTestCard.BorderBrush = Brush(border);
            PackTestHeaderBorder.Background = Brush(accent);
            PackTestHeaderBorder.BorderBrush = Brush(crossfire ? Color.FromArgb(255, 216, 120, 0) : theme.AccentText);
            if (AdvancedEffectsFlyoutCard != null)
            {
                AdvancedEffectsFlyoutCard.Background = Brush(card);
                AdvancedEffectsFlyoutCard.BorderBrush = Brush(border);
            }

            ApplyAdvancedEffectsPanelTheme();
            VisualCard.Background = Brush(card);
            VisualCard.BorderBrush = Brush(theme.SoftBorder);
            VisualHeaderBorder.Background = Brush(crossfire ? Color.FromArgb(255, 46, 136, 184) : theme.Secondary);
            VisualHeaderBorder.BorderBrush = Brush(crossfire ? Color.FromArgb(255, 37, 111, 152) : theme.Border);

            CfgLabelText.Foreground = Brush(text);
            CfgStatusText.Foreground = Brush(text);
            CfgHintText.Foreground = Brush(muted);
            ServiceDiagnosticText.Foreground = Brush(crossfire ? Color.FromArgb(255, 255, 209, 102) : theme.WarningText);

            SetComboTheme(VoicePackSelector, text, field, border);
            SetComboTheme(IconPackSelector, text, field, border);
            SetComboTheme(KillFxSelector, text, field, border);
            SetComboTheme(EliteEffectSelector, text, field, border);
            SetComboTheme(WeaponBadgeSelector, text, field, border);
            SetComboTheme(MainAnimationStyleSelector, text, field, border);
            SetComboTheme(AudioVolumeSelector, text, field, border);
            SetComboTheme(TestPresetSelector, text, field, border);
            SetComboTheme(BrightnessSelector, text, field, secondary);
            SetComboTheme(ContrastSelector, text, field, secondary);
            SetComboTheme(PlaybackFpsSelector, text, field, secondary);

            VoicePackIcon.Foreground = Brush(text);
            IconPackIcon.Foreground = Brush(text);
            KillFxIcon.Foreground = Brush(text);
            EliteOverlayIcon.Foreground = Brush(text);
            WeaponBadgeIcon.Foreground = Brush(text);
            MainAnimationIcon.Foreground = Brush(text);
            VolumeIcon.Foreground = Brush(text);
            TestPresetIcon.Foreground = Brush(text);
            BrightnessIcon.Foreground = Brush(text);
            ContrastIcon.Foreground = Brush(text);
            PlaybackFpsLabel.Foreground = Brush(text);
            SetButtonTheme(SendTestButton, theme.Accent, crossfire ? Color.FromArgb(255, 197, 106, 0) : theme.AccentText, Color.FromArgb(255, 255, 255, 255));
            SetButtonTheme(ReloadAudioButton, field, theme.SoftBorder, text);

            Color visualButtonField = darkStyle ? theme.SubtleField : Color.FromArgb(255, 255, 253, 252);
            Color visualImportantField = darkStyle ? theme.SubtleField : Color.FromArgb(255, 232, 246, 254);
            Color visualButtonText = darkStyle ? text : Color.FromArgb(255, 27, 31, 49);
            Color visualImportantText = darkStyle ? text : Color.FromArgb(255, 27, 95, 130);
            SetButtonTheme(DefaultSizeButton, visualImportantField, secondary, visualImportantText);
            SetButtonTheme(CenterButton, visualImportantField, secondary, visualImportantText);
            SetButtonTheme(LowerThirdButton, visualImportantField, secondary, visualImportantText);
            SetButtonTheme(MoveUpButton, visualButtonField, darkStyle ? secondary : theme.SoftBorder, visualButtonText);
            SetButtonTheme(MoveDownButton, visualButtonField, darkStyle ? secondary : theme.SoftBorder, visualButtonText);
            SetButtonTheme(ScaleDownButton, visualButtonField, darkStyle ? secondary : theme.SoftBorder, visualButtonText);
            SetButtonTheme(ScaleUpButton, visualButtonField, darkStyle ? secondary : theme.SoftBorder, visualButtonText);
            SetButtonTheme(ResetVisualButton, visualButtonField, darkStyle ? secondary : theme.SoftBorder, visualButtonText);
            UpdateOverlay.ApplyTheme(theme);
        }

        private static void SetGameStyleItemContent(ComboBoxItem item, string text, string logoUri)
        {
            if (item == null)
            {
                return;
            }

            var image = new Image
            {
                Width = 22,
                Height = 18,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Stretch = Stretch.Uniform,
                Source = new BitmapImage(new Uri(logoUri))
            };

            item.Content = new Grid
            {
                Width = 30,
                Height = 18,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Children = { image }
            };
            ToolTipService.SetToolTip(item, text);
        }

        private static SolidColorBrush Brush(Color color)
        {
            return new SolidColorBrush(color);
        }

        private static void SetComboTheme(ComboBox comboBox, Color text, Color field, Color border)
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
                    comboItem.BorderBrush = Brush(border);
                    comboItem.RequestedTheme = comboBox.RequestedTheme;
                    SetComboItemContentForeground(comboItem, text);
                }
            }
        }

        private static bool IsDark(Color color)
        {
            double luminance = (0.2126 * color.R) + (0.7152 * color.G) + (0.0722 * color.B);
            return luminance < 128;
        }

        private static void SetButtonTheme(Button button, Color background, Color border, Color foreground)
        {
            if (button == null)
            {
                return;
            }

            button.Background = Brush(background);
            button.BorderBrush = Brush(border);
            button.Foreground = Brush(foreground);
            SetIconForeground(button, foreground);
        }

        private static void SetComboItemContentForeground(ComboBoxItem item, Color foreground)
        {
            if (item?.Content is DependencyObject content)
            {
                SetElementForeground(content, foreground, true);
            }
        }

        private static void SetIconForeground(DependencyObject root, Color foreground)
        {
            SetElementForeground(root, foreground, false);
        }

        private static void SetElementForeground(DependencyObject root, Color foreground, bool includeText)
        {
            if (root == null)
            {
                return;
            }

            if (includeText && root is TextBlock textBlock)
            {
                textBlock.Foreground = Brush(foreground);
            }
            else if (root is PathIcon pathIcon)
            {
                pathIcon.Foreground = Brush(foreground);
            }
            else if (root is FontIcon fontIcon)
            {
                fontIcon.Foreground = Brush(foreground);
            }

            if (root is Panel panel)
            {
                foreach (UIElement child in panel.Children)
                {
                    SetElementForeground(child, foreground, includeText);
                }
            }

            int childCount = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < childCount; i++)
            {
                SetElementForeground(VisualTreeHelper.GetChild(root, i), foreground, includeText);
            }
        }
    }
}

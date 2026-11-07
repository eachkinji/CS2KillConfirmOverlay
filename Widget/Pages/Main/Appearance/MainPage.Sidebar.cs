using System;
using System.Linq;
using KillConfirmGameBar.Services;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Automation;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.Foundation;

namespace KillConfirmGameBar
{
    public sealed partial class MainPage
    {
        // Both docks are permanently icon-only; names are exposed through tooltips and automation.
        private void InitializeSourceDock()
        {
            SidebarGameSources.SourceChanged += (s, source) => BackgroundDecoration.ApplySource(source);
            BackgroundDecoration.ApplySource(GameSourceSettingsStore.Current);
        }

        private void ApplySidebarGroups()
        {
            if (StyleSettingsSidebarItem == null) return;
            bool zh = LocalizationManager.Current == UiLanguage.SimplifiedChinese;
            foreach (var item in new[] { HomeSidebarItem, AdvancedSettingsSidebarItem, StyleSettingsSidebarItem })
            {
                string key = (string)item.Tag;
                string label = key == "home" ? (zh ? "主页" : "Home") : key == "advanced"
                    ? (zh ? "高级设置" : "Advanced settings") : (zh ? "游戏风格设置" : "Game styles");
                item.Content = label;
                AutomationProperties.SetName(item, label);
                ToolTipService.SetToolTip(item, label);
            }
            UpdateStylePicker();
        }

        private void UpdateStylePicker()
        {
            if (StylePicker == null || GameStyleSelector == null) return;
            if (StylePicker.Children.Count == 0)
            {
                foreach (var item in GameStyleSelector.Items.OfType<ComboBoxItem>())
                {
                    string key = (string)item.Tag;
                    var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
                    var tile = new Border { Width = 30, Height = 30, CornerRadius = new CornerRadius(15),
                        Background = new SolidColorBrush(key == "modernwarfare2019" || key == "battlefield5"
                            ? Color.FromArgb(255, 37, 43, 52) : Color.FromArgb(224, 255, 255, 255)) };
                    if (key == "custommodule") tile.Child = new FontIcon { Glyph = "\uE713", FontSize = 20 };
                    else tile.Child = new Image { Width = 26, Height = 26, Stretch = Stretch.Uniform,
                        Source = new BitmapImage(new Uri("ms-appx:///Assets/GameLogos/" + key + (key == "dagoujiao" ? ".jpg" : ".png"))) { DecodePixelWidth = 78 } };
                    content.Children.Add(tile);
                    content.Children.Add(new TextBlock { VerticalAlignment = VerticalAlignment.Center,
                        FontSize = 14, FontWeight = Windows.UI.Text.FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 140 });
                    var button = new Button { Tag = key, Content = content, Padding = new Thickness(6), Height = 46,
                        Margin = new Thickness(3), CornerRadius = new CornerRadius(23), BorderThickness = new Thickness(2),
                        HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left };
                    button.Click += OnStylePickerClick;
                    StylePicker.Children.Add(button);
                }
            }
            bool zh = LocalizationManager.Current == UiLanguage.SimplifiedChinese;
            var theme = GameThemePalette.ForMode(SettingsGameStyle);
            foreach (var button in StylePicker.Children.OfType<Button>())
            {
                var mode = GameStyleService.FromKey((string)button.Tag);
                bool selected = mode == SettingsGameStyle;
                string label = GetGamePageTitle(mode, zh);
                var text = (TextBlock)((StackPanel)button.Content).Children[1];
                text.Text = label;
                text.Visibility = selected ? Visibility.Visible : Visibility.Collapsed;
                button.BorderBrush = new SolidColorBrush(selected ? theme.Accent : Colors.Transparent);
                button.Background = new SolidColorBrush(selected ? Color.FromArgb(32, theme.Accent.R, theme.Accent.G, theme.Accent.B) : Color.FromArgb(120, 255, 255, 255));
                button.Foreground = new SolidColorBrush(theme.Text);
                AutomationProperties.SetName(button, label);
                AutomationProperties.SetHelpText(button, selected ? (zh ? "当前风格设置" : "Current style settings") : "");
                ToolTipService.SetToolTip(button, label);
            }
        }

        private void OnStylePickerClick(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string key)
            {
                SelectGameStyle(key);
                ApplyLanguage();
            }
        }
    }

    // Keep natural chip widths, including the expanded selected name, at every window size.
    public sealed class GameStyleWrapPanel : Panel
    {
        protected override Size MeasureOverride(Size availableSize)
        {
            foreach (var child in Children) child.Measure(new Size(availableSize.Width, double.PositiveInfinity));
            return LayoutChips(availableSize.Width, false);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            LayoutChips(finalSize.Width, true);
            return finalSize;
        }

        private Size LayoutChips(double width, bool arrange)
        {
            double x = 0, y = 0, rowHeight = 0, usedWidth = 0;
            foreach (var child in Children)
            {
                Size size = child.DesiredSize;
                if (x > 0 && x + size.Width > width)
                { y += rowHeight; x = 0; rowHeight = 0; }
                if (arrange) child.Arrange(new Rect(x, y, size.Width, size.Height));
                x += size.Width;
                usedWidth = Math.Max(usedWidth, x);
                rowHeight = Math.Max(rowHeight, size.Height);
            }
            return new Size(usedWidth, y + rowHeight);
        }
    }
}

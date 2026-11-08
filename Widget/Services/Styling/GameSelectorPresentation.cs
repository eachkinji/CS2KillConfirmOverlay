using System;
using System.Linq;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace KillConfirmGameBar.Services
{
    // Shared by the independently built Game Bar widget and the desktop panel.
    internal static class GameSelectorPresentation
    {
        internal const string GameCategoryHeaderPrefix = "category:";

        internal static FrameworkElement CreateMaterialGameSelectorContent(GameStyleMode style, string name, bool compact = false, string logoUri = null)
        {
            string key = GameStyleService.ToStorageValue(style);
            string extension = style == GameStyleMode.Dagoujiao ? "jpg" : "png";
            bool widgetLogo = logoUri != null;
            logoUri = logoUri ?? (style == GameStyleMode.CustomModule
                ? "ms-appx:///Assets/Square44x44Logo.scale-200.png"
                : $"ms-appx:///Assets/GameLogos/{key}.{extension}");
            var image = new Image
            {
                Width = compact ? 18 : 34,
                Height = compact ? 14 : 22,
                Stretch = Stretch.Uniform,
                Source = new BitmapImage(new Uri(logoUri))
            };
            bool darkBackdrop = !widgetLogo && style == GameStyleMode.ModernWarfare2019;
            var logo = new Border
            {
                // The widget's 72-DIP selector reserves 32 DIPs for its native arrow.
                Width = compact ? 24 : 38,
                Height = compact ? 20 : 26,
                Padding = compact ? new Thickness(3) : new Thickness(4, 2, 4, 2),
                CornerRadius = new CornerRadius(4),
                Background = new SolidColorBrush(darkBackdrop ? Color.FromArgb(217, 52, 56, 64) : Colors.Transparent),
                Child = image
            };
            var content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                HorizontalAlignment = compact ? HorizontalAlignment.Center : HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center
            };
            content.Children.Add(logo);
            if (!compact)
                content.Children.Add(new TextBlock
                {
                    Text = name,
                    MaxWidth = 148,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis
                });
            return content;
        }

        internal static void UpdateMaterialGameSelectorContent(FrameworkElement element, string name, bool compact)
        {
            if (!(element is StackPanel content)) return;
            var logo = content.Children.OfType<Border>().FirstOrDefault();
            if (logo?.Child is Image image)
            {
                image.Width = compact ? 18 : 34;
                image.Height = compact ? 14 : 22;
                logo.Width = compact ? 24 : 38;
                logo.Height = compact ? 20 : 26;
                logo.Padding = compact ? new Thickness(3) : new Thickness(4, 2, 4, 2);
            }
            content.HorizontalAlignment = compact ? HorizontalAlignment.Center : HorizontalAlignment.Left;
            content.Spacing = compact ? 0 : 8;
            foreach (var label in content.Children.OfType<TextBlock>())
            {
                label.Text = name;
                label.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
            }
        }

        internal static string WidgetGameLogoUri(GameStyleMode style)
        {
            string extension = style == GameStyleMode.Dagoujiao ? "jpg" : "png";
            return style == GameStyleMode.CustomModule
                ? "ms-appx:///Assets/Square44x44Logo.scale-200.png"
                : $"ms-appx:///Assets/GameLogos/{GameStyleService.ToStorageValue(style)}.{extension}";
        }

        internal static void PopulateGroupedGameSelector(ComboBox selector)
        {
            foreach (GameStyleMode style in Enum.GetValues(typeof(GameStyleMode)))
                selector.Items.Add(new ComboBoxItem
                {
                    Content = CreateMaterialGameSelectorContent(style, GameStyleService.ToDisplayName(style)),
                    Tag = GameStyleService.ToStorageValue(style)
                });
            AddGameCategoryHeaders(selector);
        }

        // Explicit disabled containers are skipped by native pointer/keyboard selection.
        // Their tags are not game keys, and selection handlers reject them as well.
        internal static void AddGameCategoryHeaders(ComboBox selector)
        {
            if (selector.Items.OfType<ComboBoxItem>().Any(IsGameCategoryHeader)) return;
            string previous = null;
            for (int i = 0; i < selector.Items.Count; i++)
            {
                if (!(selector.Items[i] is ComboBoxItem item) || !(item.Tag is string key)) continue;
                string category = GameStyleService.ToCategoryKey(GameStyleService.FromKey(key));
                if (category == previous) continue;
                var header = new ComboBoxItem
                {
                    Tag = GameCategoryHeaderPrefix + category,
                    IsEnabled = false,
                    IsTabStop = false,
                    IsHitTestVisible = false,
                    Height = 26,
                    MinHeight = 26,
                    Padding = new Thickness(10, 3, 10, 3),
                    FontSize = 11,
                    FontWeight = Windows.UI.Text.FontWeights.SemiBold
                };
                UpdateMaterialGameSelectorItemLanguage(header);
                selector.Items.Insert(i++, header);
                previous = category;
            }
        }

        internal static bool IsGameCategoryHeader(ComboBoxItem item)
            => (item?.Tag as string)?.StartsWith(GameCategoryHeaderPrefix, StringComparison.Ordinal) == true;

        internal static void UpdateMaterialGameSelectorItemLanguage(ComboBoxItem gameItem)
        {
            if (!(gameItem?.Tag is string key)) return;
            if (IsGameCategoryHeader(gameItem))
            {
                string category = key.Substring(GameCategoryHeaderPrefix.Length);
                gameItem.Content = new TextBlock
                {
                    Text = GameStyleService.ToCategoryDisplayName(category, LocalizationManager.Current == UiLanguage.SimplifiedChinese),
                    FontSize = 11,
                    Opacity = 0.85,
                    VerticalAlignment = VerticalAlignment.Center
                };
                return;
            }
            if (gameItem.Content is StackPanel content)
            {
                TextBlock name = content.Children.OfType<TextBlock>().FirstOrDefault();
                if (name != null) name.Text = GameStyleService.ToDisplayName(GameStyleService.FromKey(key));
            }
        }

    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KillConfirmGameBar.Services;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace KillConfirmGameBar
{
    public sealed partial class MainPage
    {

        internal static FrameworkElement CreateMaterialGameSelectorContent(GameStyleMode style, string name)
        {
            string key = GameStyleService.ToStorageValue(style);
            string extension = style == GameStyleMode.Dagoujiao ? "jpg" : "png";
            string logoUri = style == GameStyleMode.CustomModule
                ? "ms-appx:///Assets/Square44x44Logo.scale-200.png"
                : style == GameStyleMode.Battlefield6
                    ? "ms-appx:///Assets/GameStyles/battlefield6/killconfirm/textures/killicon_battlefield6_default.png"
                    : $"ms-appx:///Assets/GameLogos/{key}.{extension}";
            var image = new Image
            {
                Width = 34,
                Height = 22,
                Stretch = Stretch.Uniform,
                Source = new BitmapImage(new Uri(logoUri))
            };
            // White-on-transparent logos vanish on the light selector; only those get a dark chip.
            bool darkBackdrop = style == GameStyleMode.ModernWarfare2019 || style == GameStyleMode.Battlefield5
                || style == GameStyleMode.Battlefield6;
            var logoBackground = new Border
            {
                Width = 42,
                Height = 26,
                Padding = new Thickness(4, 2, 4, 2),
                CornerRadius = new CornerRadius(4),
                Background = new SolidColorBrush(darkBackdrop ? Color.FromArgb(217, 52, 56, 64) : Colors.Transparent),
                Child = image
            };
            var text = new TextBlock
            {
                Text = name,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            };

            bool zh = LocalizationManager.Current == UiLanguage.SimplifiedChinese;
            string categoryKey = GameStyleService.ToCategoryKey(style);
            var categoryIcon = new Image
            {
                Width = 13,
                Height = 13,
                Stretch = Stretch.Uniform,
                VerticalAlignment = VerticalAlignment.Center,
                Source = new BitmapImage(new Uri(GameStyleService.ToCategoryLogoUri(categoryKey)))
            };
            var categoryText = new TextBlock
            {
                Tag = "category",
                Text = GameStyleService.ToCategoryDisplayName(categoryKey, zh),
                FontSize = 11,
                Opacity = 0.78,
                VerticalAlignment = VerticalAlignment.Center
            };
            var categoryRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 4,
                VerticalAlignment = VerticalAlignment.Center
            };
            categoryRow.Children.Add(categoryIcon);
            categoryRow.Children.Add(categoryText);
            var categoryBadge = new Border
            {
                Tag = "categoryBadge",
                Margin = new Thickness(4, 0, 0, 0),
                Padding = new Thickness(6, 2, 6, 2),
                CornerRadius = new CornerRadius(6),
                Background = new SolidColorBrush(Color.FromArgb(24, 120, 130, 145)),
                VerticalAlignment = VerticalAlignment.Center,
                Child = categoryRow
            };

            var content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 9,
                VerticalAlignment = VerticalAlignment.Center
            };
            content.Children.Add(logoBackground);
            content.Children.Add(text);
            content.Children.Add(categoryBadge);
            return content;
        }

        internal static void UpdateMaterialGameSelectorItemLanguage(ComboBoxItem gameItem)
        {
            if (!(gameItem?.Content is StackPanel content) || !(gameItem.Tag is string key)) return;
            GameStyleMode style = GameStyleService.FromKey(key);
            TextBlock nameBlock = content.Children.OfType<TextBlock>().FirstOrDefault();
            if (nameBlock != null)
            {
                nameBlock.Text = GameStyleService.ToDisplayName(style);
            }

            bool zh = LocalizationManager.Current == UiLanguage.SimplifiedChinese;
            string categoryKey = GameStyleService.ToCategoryKey(style);
            Border badge = content.Children.OfType<Border>().FirstOrDefault(b => Equals(b.Tag, "categoryBadge"));
            if (badge?.Child is StackPanel categoryRow)
            {
                TextBlock categoryText = categoryRow.Children.OfType<TextBlock>().FirstOrDefault();
                if (categoryText != null)
                {
                    categoryText.Text = GameStyleService.ToCategoryDisplayName(categoryKey, zh);
                }
            }
        }

        private static bool IsPackInMaterialCategory(string gameKey, string packKey)
        {
            return GameStyleService.GetStyleForPackKey(packKey) == GameStyleService.FromKey(gameKey);
        }

        private static async Task<IReadOnlyList<StorageFile>> LoadMaterialFolderFilesAsync(MaterialFolderItem folder, bool isAudio)
        {
            if (folder?.Files != null) return folder.Files;
            if (folder?.Folder == null) return Array.Empty<StorageFile>();
            try
            {
                IReadOnlyList<StorageFile> files = await folder.Folder.GetFilesAsync();
                return files
                    .Where(file => !file.Name.StartsWith("pack_head.", StringComparison.OrdinalIgnoreCase))
                    .Where(file => IsSupportedMaterialFile(file, isAudio))
                    .ToList();
            }
            catch
            {
                return Array.Empty<StorageFile>();
            }
        }

        private static async Task<IReadOnlyList<StorageFile>> PickMaterialFilesAsync(bool isAudio, bool allowMultiple)
        {
            var picker = new FileOpenPicker();
            string[] filters = isAudio
                ? new[] { ".wav", ".mp3", ".m4a" }
                : new[] { ".png", ".jpg", ".jpeg", ".webp", ".tga" };
            foreach (string filter in filters) picker.FileTypeFilter.Add(filter);
            if (allowMultiple)
            {
                IReadOnlyList<StorageFile> files = await picker.PickMultipleFilesAsync();
                return files ?? Array.Empty<StorageFile>();
            }
            StorageFile file = await picker.PickSingleFileAsync();
            return file == null ? Array.Empty<StorageFile>() : new[] { file };
        }

        private static IEnumerable<StorageFile> DistinctMaterialFiles(IEnumerable<StorageFile> files)
        {
            return (files ?? Enumerable.Empty<StorageFile>())
                .Where(file => file != null)
                .GroupBy(MaterialFileIdentity, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First());
        }

        private static string MaterialFileIdentity(StorageFile file)
        {
            if (file == null) return string.Empty;
            return string.IsNullOrWhiteSpace(file.Path) ? file.Name : file.Path;
        }

        private static bool IsSupportedMaterialFile(StorageFile file, bool isAudio)
        {
            if (file == null) return false;
            string extension = System.IO.Path.GetExtension(file.Name)?.ToLowerInvariant() ?? string.Empty;
            return isAudio
                ? extension == ".wav" || extension == ".mp3" || extension == ".m4a"
                : extension == ".png" || extension == ".jpg" || extension == ".jpeg" || extension == ".webp" || extension == ".tga";
        }
    }
}

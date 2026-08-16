using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KillConfirmGameBar.Services;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace KillConfirmGameBar
{
    public sealed partial class MainPage
    {
        // CSOL icon pack creation dialog.
        // 13 slot rows driven by PackCatalogService.CsolIconSlotFileNames.
        // CSOL has no FX / Elite / WeaponBadge overlays, so the dialog stays
        // minimal: name + 13 image-picker rows.
        private async Task ShowCreateCsolIconPackDialogAsync(
            string initialDisplayName = null,
            IReadOnlyDictionary<string, StorageFile> initialFiles = null)
        {
            var slots = new (string FileName, string Label)[PackCatalogService.CsolIconSlotFileNames.Count];
            for (int i = 0; i < PackCatalogService.CsolIconSlotFileNames.Count; i++)
            {
                string fileName = PackCatalogService.CsolIconSlotFileNames[i];
                slots[i] = (fileName, LocalizationManager.Text(CsolIconLabelKeyFor(fileName)));
            }

            var selectedFiles = initialFiles != null
                ? new Dictionary<string, StorageFile>(initialFiles, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, StorageFile>(StringComparer.OrdinalIgnoreCase);

            var nameBox = new TextBox
            {
                PlaceholderText = LocalizationManager.Text("IconPackNamePlaceholder"),
                Text = initialDisplayName ?? string.Empty,
                Background = new SolidColorBrush(Color.FromArgb(255, 255, 255, 252)),
                Foreground = new SolidColorBrush(Color.FromArgb(255, 29, 34, 51)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(255, 213, 208, 196)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(14)
            };

            var layout = new StackPanel { Spacing = 12, Width = 420 };
            layout.Children.Add(new TextBlock
            {
                Text = LocalizationManager.Text("CreateIconPack"),
                FontSize = 24,
                FontWeight = Windows.UI.Text.FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 29, 34, 51))
            });
            layout.Children.Add(new TextBlock
            {
                Text = LocalizationManager.Text("IconPackCreationHint"),
                Foreground = new SolidColorBrush(Color.FromArgb(255, 85, 89, 102)),
                TextWrapping = TextWrapping.WrapWholeWords
            });
            layout.Children.Add(nameBox);

            var scroll = new ScrollViewer
            {
                MaxHeight = 460,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };
            var slotPanel = new StackPanel { Spacing = 8 };
            scroll.Content = new Border
            {
                Padding = new Thickness(0, 0, 0, 28),
                Child = slotPanel
            };

            foreach (var slot in slots)
            {
                selectedFiles.TryGetValue(slot.FileName, out StorageFile existingFile);
                var fileNameText = new TextBlock
                {
                    Text = existingFile?.Name ?? LocalizationManager.Text("NotSelected"),
                    FontSize = 12,
                    Foreground = new SolidColorBrush(Color.FromArgb(255, 106, 110, 122)),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    VerticalAlignment = VerticalAlignment.Center
                };

                Image previewImage = new Image
                {
                    Width = 30,
                    Height = 30,
                    Stretch = Stretch.Uniform,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Visibility = existingFile != null ? Visibility.Visible : Visibility.Collapsed
                };
                if (existingFile != null)
                {
                    await SetPreviewImageAsync(previewImage, existingFile);
                }

                var row = new Grid { ColumnSpacing = 5 };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(96) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(34) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(56) });

                row.Children.Add(new TextBlock
                {
                    Text = slot.Label,
                    VerticalAlignment = VerticalAlignment.Center,
                    FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromArgb(255, 29, 34, 51)),
                    FontSize = 12,
                    MaxLines = 2,
                    TextWrapping = TextWrapping.WrapWholeWords,
                    TextTrimming = TextTrimming.CharacterEllipsis
                });
                Grid.SetColumn(fileNameText, 1);
                row.Children.Add(fileNameText);
                Grid.SetColumn(previewImage, 2);
                row.Children.Add(previewImage);

                var browseButton = new Button
                {
                    Content = LocalizationManager.Text("ChooseFile"),
                    MinWidth = 54,
                    Padding = new Thickness(5, 4, 5, 4),
                    FontSize = 10,
                    Background = new SolidColorBrush(Color.FromArgb(255, 236, 247, 252)),
                    Foreground = new SolidColorBrush(Color.FromArgb(255, 46, 136, 184)),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(255, 185, 220, 236)),
                    CornerRadius = new CornerRadius(12)
                };
                browseButton.Click += async (_, __) =>
                {
                    StorageFile file = await PickSingleFileAsync(new[] { ".png", ".jpg", ".jpeg", ".webp", ".tga" });
                    if (file == null) return;
                    selectedFiles[slot.FileName] = file;
                    fileNameText.Text = file.Name;
                    await SetPreviewImageAsync(previewImage, file);
                };
                Grid.SetColumn(browseButton, 3);
                row.Children.Add(browseButton);

                slotPanel.Children.Add(new Border
                {
                    Padding = new Thickness(8, 6, 8, 6),
                    Background = new SolidColorBrush(Color.FromArgb(255, 255, 255, 252)),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(255, 226, 221, 211)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(14),
                    Child = row
                });
            }

            layout.Children.Add(scroll);

            var shell = CreatePackDialogShell(layout);

            var dialog = new ContentDialog
            {
                Content = shell,
                PrimaryButtonText = LocalizationManager.Text("Create"),
                CloseButtonText = LocalizationManager.Text("Cancel"),
                PrimaryButtonStyle = CreateDialogPrimaryButtonStyle(),
                CloseButtonStyle = CreateDialogCloseButtonStyle(),
                RequestedTheme = ElementTheme.Light,
                Background = new SolidColorBrush(Colors.Transparent),
                Foreground = new SolidColorBrush(Color.FromArgb(255, 29, 34, 51))
            };

            ContentDialogResult result = await dialog.ShowAsync();
            if (result != ContentDialogResult.Primary || selectedFiles.Count == 0)
            {
                return;
            }

            string displayName = string.IsNullOrWhiteSpace(nameBox.Text)
                ? LocalizationManager.Text("NewPack")
                : nameBox.Text.Trim();

            await PackCatalogService.CreateCsolIconPackAsync(displayName, selectedFiles);
        }

        // Maps a CSOL icon slot filename to its localization key.
        private static string CsolIconLabelKeyFor(string fileName)
        {
            switch (fileName)
            {
                case "badge_headshot.png": return "CsolIconHeadshot";
                case "badge_knife.png": return "CsolIconKnife";
                case "badge_firstkill.png": return "CsolIconFirstKill";
                case "badge_lastkill.png": return "CsolIconLastKill";
                case "multi2.png": return "CsolIconMulti2";
                case "multi3.png": return "CsolIconMulti3";
                case "multi4.png": return "CsolIconMulti4";
                case "multi5.png": return "CsolIconMulti5";
                case "multi6.png": return "CsolIconMulti6";
                case "multi7.png": return "CsolIconMulti7";
                case "multi8.png": return "CsolIconMulti8";
                case "multi9.png": return "CsolIconMulti9";
                case "multi10.png": return "CsolIconMulti10";
                default: return "CsolIconHeadshot";
            }
        }
    }
}

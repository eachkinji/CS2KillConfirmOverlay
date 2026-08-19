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
        private async Task ShowCreateIconPackDialogAsync(
            string initialDisplayName = null,
            IReadOnlyDictionary<string, StorageFile> initialFiles = null)
        {
            var slots = new[]
            {
                ("badge_multi1.png", LocalizationManager.Text("SingleKill")),
                ("badge_multi2.png", LocalizationManager.Text("DoubleKill")),
                ("badge_multi3.png", LocalizationManager.Text("TripleKill")),
                ("badge_multi4.png", LocalizationManager.Text("QuadraKill")),
                ("badge_multi5.png", LocalizationManager.Text("PentaKill")),
                ("badge_multi6.png", LocalizationManager.Text("HexaKill")),
                ("badge_headshot.png", LocalizationManager.Text("Headshot")),
                ("badge_headshot_gold.png", LocalizationManager.Text("FirstLastKill")),
                ("badge_knife.png", LocalizationManager.Text("KnifeKill")),
                ("FIRSTKILL.png", LocalizationManager.Text("FirstLastKill")),
                ("LASTKILL.png", LocalizationManager.Text("FirstLastKill")),
                ("KillMark_Upgrade1.png", LocalizationManager.Text("EliteLevel1")),
                ("KillMark_Upgrade2.png", LocalizationManager.Text("EliteLevel2")),
                ("KillMark_Upgrade3.png", LocalizationManager.Text("EliteLevel3")),
                ("multi2_fx.png", LocalizationManager.Text("DoubleKillFX")),
                ("multi3_fx.png", LocalizationManager.Text("TripleKillFX")),
                ("multi4_fx.png", LocalizationManager.Text("QuadraKillFX")),
                ("multi5_fx.png", LocalizationManager.Text("PentaKillFX")),
                ("multi6_fx.png", LocalizationManager.Text("HexaKillFX")),
                ("badge_knife_1.png", LocalizationManager.Text("EliteKnife1")),
                ("badge_knife_2.png", LocalizationManager.Text("EliteKnife2")),
                ("badge_knife_3.png", LocalizationManager.Text("EliteKnife3")),
                ("badge_assault1.png", LocalizationManager.Text("ClassAssault") + " 1"),
                ("badge_assault2.png", LocalizationManager.Text("ClassAssault") + " 2"),
                ("badge_assault3.png", LocalizationManager.Text("ClassAssault") + " 3"),
                ("badge_scout1.png", LocalizationManager.Text("ClassScout") + " 1"),
                ("badge_scout2.png", LocalizationManager.Text("ClassScout") + " 2"),
                ("badge_scout3.png", LocalizationManager.Text("ClassScout") + " 3"),
                ("badge_sniper1.png", LocalizationManager.Text("ClassSniper") + " 1"),
                ("badge_sniper2.png", LocalizationManager.Text("ClassSniper") + " 2"),
                ("badge_sniper3.png", LocalizationManager.Text("ClassSniper") + " 3"),
                ("badge_elite1.png", LocalizationManager.Text("ClassElite") + " 1"),
                ("badge_elite2.png", LocalizationManager.Text("ClassElite") + " 2"),
                ("badge_elite3.png", LocalizationManager.Text("ClassElite") + " 3"),
                ("badge_knife1.png", LocalizationManager.Text("ClassKnife") + " 1"),
                ("badge_knife2.png", LocalizationManager.Text("ClassKnife") + " 2"),
                ("badge_knife3.png", LocalizationManager.Text("ClassKnife") + " 3")
            };

            await ShowPackCreationDialogAsync(
                LocalizationManager.Text("CreateIconPack"),
                LocalizationManager.Text("IconPackCreationHint"),
                slots,
                new[] { ".png", ".jpg", ".jpeg", ".webp", ".tga" },
                PackCatalogService.CreateIconPackAsync,
                initialDisplayName,
                initialFiles);
        }

        private async Task ShowPackCreationDialogAsync(
            string title,
            string description,
            (string FileName, string Label)[] slots,
            string[] fileFilters,
            Func<string, IReadOnlyDictionary<string, StorageFile>, Task> createHandler,
            string initialDisplayName = null,
            IReadOnlyDictionary<string, StorageFile> initialFiles = null)
        {
            var selectedFiles = initialFiles != null
                ? new Dictionary<string, StorageFile>(initialFiles, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, StorageFile>(StringComparer.OrdinalIgnoreCase);
            bool supportsImagePreview = Array.Exists(fileFilters, filter =>
                string.Equals(filter, ".png", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(filter, ".jpg", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(filter, ".jpeg", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(filter, ".webp", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(filter, ".tga", StringComparison.OrdinalIgnoreCase));

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
                Text = title,
                FontSize = 24,
                FontWeight = Windows.UI.Text.FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 29, 34, 51))
            });
            layout.Children.Add(new TextBlock
            {
                Text = description,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 85, 89, 102)),
                TextWrapping = TextWrapping.WrapWholeWords
            });
            layout.Children.Add(nameBox);

            var scroll = new ScrollViewer
            {
                MaxHeight = 500,
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

                Image previewImage = null;
                if (supportsImagePreview)
                {
                    previewImage = new Image
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
                }

                var row = new Grid
                {
                    ColumnSpacing = 5
                };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(96) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                if (supportsImagePreview)
                {
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(34) });
                }
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

                if (previewImage != null)
                {
                    Grid.SetColumn(previewImage, 2);
                    row.Children.Add(previewImage);
                }

                var browseButton = new Button
                {
                    Content = LocalizationManager.Current == UiLanguage.SimplifiedChinese ? "閫夋嫨鏉愭枡" : "Select Material",
                    MinWidth = 64,
                    Padding = new Thickness(5, 4, 5, 4),
                    FontSize = 10,
                    Background = new SolidColorBrush(Color.FromArgb(255, 236, 247, 252)),
                    Foreground = new SolidColorBrush(Color.FromArgb(255, 46, 136, 184)),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(255, 185, 220, 236)),
                    CornerRadius = new CornerRadius(12)
                };
                browseButton.Click += async (_, __) =>
                {
                    StorageFile file = await PickSingleFileAsync(fileFilters);
                    if (file == null)
                    {
                        return;
                    }

                    selectedFiles[slot.FileName] = file;
                    fileNameText.Text = file.Name;
                    if (previewImage != null)
                    {
                        await SetPreviewImageAsync(previewImage, file);
                    }
                };
                Grid.SetColumn(browseButton, supportsImagePreview ? 3 : 2);
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
            await createHandler(displayName, selectedFiles);
        }
    }
}

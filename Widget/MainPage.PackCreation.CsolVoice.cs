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
        // CSOL voice pack creation dialog.
        // 15 slot rows (1.wav..10.wav, headshot.wav, knife.wav, first.wav,
        // last.wav, assist.wav). No common-overlay sub-card (CSOL has no
        // shared overlay audio). No kill-1 special hint. After Primary the
        // dialog calls PackCatalogService.CreateCsolVoicePackAsync.
        private async Task ShowCreateCsolVoicePackDialogAsync(
            string initialDisplayName = null,
            IReadOnlyDictionary<string, StorageFile> initialFiles = null,
            StorageFile initialHeadImageFile = null)
        {
            var slots = new[]
            {
                ("1.wav", LocalizationManager.Text("CsolSlot1")),
                ("2.wav", LocalizationManager.Text("CsolSlot2")),
                ("3.wav", LocalizationManager.Text("CsolSlot3")),
                ("4.wav", LocalizationManager.Text("CsolSlot4")),
                ("5.wav", LocalizationManager.Text("CsolSlot5")),
                ("6.wav", LocalizationManager.Text("CsolSlot6")),
                ("7.wav", LocalizationManager.Text("CsolSlot7")),
                ("8.wav", LocalizationManager.Text("CsolSlot8")),
                ("9.wav", LocalizationManager.Text("CsolSlot9")),
                ("10.wav", LocalizationManager.Text("CsolSlot10")),
                ("headshot.wav", LocalizationManager.Text("CsolSlotHeadshot")),
                ("knife.wav", LocalizationManager.Text("CsolSlotKnife")),
                ("first.wav", LocalizationManager.Text("CsolSlotFirst")),
                ("last.wav", LocalizationManager.Text("CsolSlotLast")),
                ("assist.wav", LocalizationManager.Text("CsolSlotAssist"))
            };

            var selectedFiles = initialFiles != null
                ? new Dictionary<string, StorageFile>(initialFiles, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, StorageFile>(StringComparer.OrdinalIgnoreCase);
            StorageFile headImageFile = initialHeadImageFile;

            var nameBox = new TextBox
            {
                PlaceholderText = LocalizationManager.Text("VoicePackNamePlaceholder"),
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
                Text = LocalizationManager.Text("CreateVoicePack"),
                FontSize = 24,
                FontWeight = Windows.UI.Text.FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 29, 34, 51))
            });
            layout.Children.Add(new TextBlock
            {
                Text = LocalizationManager.Text("VoicePackCreationHint"),
                Foreground = new SolidColorBrush(Color.FromArgb(255, 85, 89, 102)),
                TextWrapping = TextWrapping.WrapWholeWords
            });
            layout.Children.Add(nameBox);

            var headImageCard = new Border
            {
                Padding = new Thickness(8),
                Background = new SolidColorBrush(Color.FromArgb(255, 255, 255, 252)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(255, 226, 221, 211)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(18)
            };
            var headImageRow = new Grid { ColumnSpacing = 8 };
            headImageRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            headImageRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headImageRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var headPreview = CreatePackPreviewImage("ms-appx:///Assets/KillConfirmCode/Original/badge_headshot.PNG");
            headPreview.Width = 42;
            headPreview.Height = 42;
            if (headImageFile != null)
            {
                await SetPreviewImageAsync(headPreview, headImageFile);
            }
            headImageRow.Children.Add(headPreview);

            var headTextPanel = new StackPanel { Spacing = 0, VerticalAlignment = VerticalAlignment.Center };
            headTextPanel.Children.Add(new TextBlock
            {
                Text = LocalizationManager.Text("CustomHeadImage"),
                FontSize = 12,
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 29, 34, 51))
            });
            var headFileText = new TextBlock
            {
                Text = LocalizationManager.Text("CustomHeadImageHint"),
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 106, 110, 122)),
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            if (headImageFile != null)
            {
                headFileText.Text = headImageFile.Name;
            }
            headTextPanel.Children.Add(headFileText);
            Grid.SetColumn(headTextPanel, 1);
            headImageRow.Children.Add(headTextPanel);

            var headBrowseButton = new Button
            {
                Content = LocalizationManager.Text("ChooseImage"),
                Padding = new Thickness(8, 4, 8, 4),
                FontSize = 11,
                Background = new SolidColorBrush(Color.FromArgb(255, 236, 247, 252)),
                Foreground = new SolidColorBrush(Color.FromArgb(255, 46, 136, 184)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(255, 185, 220, 236)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(14)
            };
            headBrowseButton.Click += async (_, __) =>
            {
                StorageFile file = await PickSingleFileAsync(new[] { ".png", ".jpg", ".jpeg", ".webp", ".tga" });
                if (file == null) return;
                headImageFile = file;
                headFileText.Text = file.Name;
                await SetPreviewImageAsync(headPreview, file);
            };
            Grid.SetColumn(headBrowseButton, 2);
            headImageRow.Children.Add(headBrowseButton);
            headImageCard.Child = headImageRow;
            layout.Children.Add(headImageCard);

            var scroll = new ScrollViewer
            {
                MaxHeight = 380,
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
                selectedFiles.TryGetValue(slot.Item1, out StorageFile existingFile);

                var row = new Grid { ColumnSpacing = 5 };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(96) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(56) });

                row.Children.Add(new TextBlock
                {
                    Text = slot.Item2,
                    VerticalAlignment = VerticalAlignment.Center,
                    FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromArgb(255, 29, 34, 51)),
                    FontSize = 12,
                    TextTrimming = TextTrimming.CharacterEllipsis
                });

                var fileText = new TextBlock
                {
                    Text = existingFile?.Name ?? LocalizationManager.Text("NotSelected"),
                    FontSize = 12,
                    Foreground = new SolidColorBrush(Color.FromArgb(255, 106, 110, 122)),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(fileText, 1);
                row.Children.Add(fileText);

                var previewButton = new Button
                {
                    Content = "",
                    FontFamily = new FontFamily("Segoe MDL2 Assets"),
                    MinWidth = 30,
                    Padding = new Thickness(5, 4, 5, 4),
                    Background = new SolidColorBrush(Color.FromArgb(255, 236, 247, 252)),
                    Foreground = new SolidColorBrush(Color.FromArgb(255, 46, 136, 184)),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(255, 185, 220, 236)),
                    CornerRadius = new CornerRadius(12)
                };
                previewButton.Click += async (_, __) =>
                {
                    if (selectedFiles.TryGetValue(slot.Item1, out StorageFile previewFile) && previewFile != null)
                    {
                        await PlayPreviewAsync(previewFile);
                    }
                };
                Grid.SetColumn(previewButton, 2);
                row.Children.Add(previewButton);

                var browseButton = new Button
                {
                    Content = LocalizationManager.Current == UiLanguage.SimplifiedChinese ? "閫夋嫨鏉愭枡" : "Select Material",
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
                    StorageFile file = await ShowMaterialPickerDialogAsync(
                        isAudio: true,
                        currentGame: GameStyleMode.Csol,
                        stagedFiles: selectedFiles,
                        slotDisplayName: slot.Item2,
                        currentSelectedFile: existingFile);

                    if (file != null)
                    {
                        selectedFiles[slot.Item1] = file;
                        fileText.Text = file.Name;
                        existingFile = file;
                    }
                    else if (file == null && selectedFiles.ContainsKey(slot.Item1))
                    {
                        selectedFiles.Remove(slot.Item1);
                        fileText.Text = LocalizationManager.Text("NotSelected");
                        existingFile = null;
                    }
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
            if (result != ContentDialogResult.Primary)
            {
                return;
            }

            string displayName = string.IsNullOrWhiteSpace(nameBox.Text)
                ? LocalizationManager.Text("NewPack")
                : nameBox.Text.Trim();

            await PackCatalogService.CreateCsolVoicePackAsync(
                displayName,
                new VoicePackBuildOptions
                {
                    SelectedFiles = selectedFiles,
                    CommonOverlayEnabled = null,
                    UseBuiltInDefaultCommonOverlay = false,
                    CommonOverlayFile = null,
                    HeadImageFile = headImageFile
                });
        }
    }
}

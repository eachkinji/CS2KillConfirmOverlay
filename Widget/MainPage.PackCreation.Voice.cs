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
        private async Task ShowCreateVoicePackDialogAsync(
            string initialDisplayName = null,
            IReadOnlyDictionary<string, StorageFile> initialFiles = null,
            StorageFile initialCommonOverlayFile = null,
            StorageFile initialHeadImageFile = null)
        {
            var slots = new[]
            {
                ("common.wav", LocalizationManager.Text("SingleKill")),
                ("2.wav", LocalizationManager.Text("DoubleKill")),
                ("3.wav", LocalizationManager.Text("TripleKill")),
                ("4.wav", LocalizationManager.Text("QuadraKill")),
                ("5.wav", LocalizationManager.Text("PentaKill")),
                ("6.wav", LocalizationManager.Text("HexaKill")),
                ("7.wav", LocalizationManager.Text("HeptaKill")),
                ("8.wav", LocalizationManager.Text("OctaKill")),
                ("headshot.wav", LocalizationManager.Text("Headshot")),
                ("knife.wav", LocalizationManager.Text("KnifeKill")),
                ("firstandlast.wav", LocalizationManager.Text("FirstLastKill"))
            };

            var selectedFiles = initialFiles != null
                ? new Dictionary<string, StorageFile>(initialFiles, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, StorageFile>(StringComparer.OrdinalIgnoreCase);
            var overlayEnabled = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            var overlayCheckBoxes = new List<CheckBox>();
            StorageFile customCommonOverlayFile = initialCommonOverlayFile;
            bool useBuiltInCommonOverlay = initialCommonOverlayFile == null;
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
                if (file == null)
                {
                    return;
                }

                headImageFile = file;
                headFileText.Text = file.Name;
                await SetPreviewImageAsync(headPreview, file);
            };
            Grid.SetColumn(headBrowseButton, 2);
            headImageRow.Children.Add(headBrowseButton);
            headImageCard.Child = headImageRow;
            layout.Children.Add(headImageCard);

            var commonOverlayCard = new StackPanel { Spacing = 4 };

            var commonOverlayMode = new ComboBox
            {
                Width = 118,
                MinWidth = 118,
                FontSize = 11,
                Background = new SolidColorBrush(Color.FromArgb(255, 255, 255, 252)),
                Foreground = new SolidColorBrush(Color.FromArgb(255, 29, 34, 51)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(255, 213, 208, 196)),
                CornerRadius = new CornerRadius(14)
            };
            commonOverlayMode.Items.Add(new ComboBoxItem
            {
                Content = LocalizationManager.Text("UseBuiltInCommon"),
                Tag = "builtin"
            });
            commonOverlayMode.Items.Add(new ComboBoxItem
            {
                Content = LocalizationManager.Text("ChooseCustomAudio"),
                Tag = "custom"
            });
            commonOverlayMode.SelectedIndex = useBuiltInCommonOverlay ? 0 : 1;
            var commonOverlayRow = new Grid { ColumnSpacing = 5 };
            commonOverlayRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            commonOverlayRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            commonOverlayRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            commonOverlayRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            commonOverlayRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            commonOverlayRow.Children.Add(commonOverlayMode);

            var commonOverlayFileText = new TextBlock
            {
                Text = customCommonOverlayFile?.Name
                    ?? LocalizationManager.Text("UseBuiltInCommon"),
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 106, 110, 122)),
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center
            };

            var commonOverlayPreviewButton = new Button
            {
                Content = "\uE768",
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                MinWidth = 30,
                Padding = new Thickness(5, 4, 5, 4),
                Background = new SolidColorBrush(Color.FromArgb(255, 236, 247, 252)),
                Foreground = new SolidColorBrush(Color.FromArgb(255, 46, 136, 184)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(255, 185, 220, 236)),
                CornerRadius = new CornerRadius(12)
            };
            commonOverlayPreviewButton.Click += async (_, __) =>
            {
                StorageFile previewFile = useBuiltInCommonOverlay
                    ? await GetBuiltInCommonOverlayFileAsync()
                    : customCommonOverlayFile;
                if (previewFile != null)
                {
                    await PlayPreviewAsync(previewFile);
                }
            };
            Grid.SetColumn(commonOverlayPreviewButton, 1);
            commonOverlayRow.Children.Add(commonOverlayPreviewButton);

            var commonOverlayBrowseButton = new Button
            {
                Content = LocalizationManager.Text("ChooseFile"),
                MinWidth = 54,
                Padding = new Thickness(5, 4, 5, 4),
                FontSize = 11,
                IsEnabled = !useBuiltInCommonOverlay,
                Background = new SolidColorBrush(Color.FromArgb(255, 236, 247, 252)),
                Foreground = new SolidColorBrush(Color.FromArgb(255, 46, 136, 184)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(255, 185, 220, 236)),
                CornerRadius = new CornerRadius(12)
            };
            commonOverlayBrowseButton.Click += async (_, __) =>
            {
                StorageFile file = await PickSingleFileAsync(new[] { ".wav", ".mp3", ".m4a" });
                if (file == null)
                {
                    return;
                }

                customCommonOverlayFile = file;
                useBuiltInCommonOverlay = false;
                commonOverlayMode.SelectedIndex = 1;
                commonOverlayFileText.Text = file.Name;
            };
            Grid.SetColumn(commonOverlayBrowseButton, 2);
            commonOverlayRow.Children.Add(commonOverlayBrowseButton);

            commonOverlayMode.SelectionChanged += (_, __) =>
            {
                string mode = (commonOverlayMode.SelectedItem as ComboBoxItem)?.Tag as string;
                bool isCustom = string.Equals(mode, "custom", StringComparison.OrdinalIgnoreCase);
                commonOverlayBrowseButton.IsEnabled = isCustom;
                useBuiltInCommonOverlay = !isCustom;
                if (isCustom)
                {
                    commonOverlayFileText.Text = customCommonOverlayFile?.Name
                        ?? LocalizationManager.Text("NotSelected");
                }
                else
                {
                    commonOverlayFileText.Text = LocalizationManager.Text("UseBuiltInCommon");
                }
            };

            var overlayOnButton = new Button
            {
                Content = LocalizationManager.Text("EnableAllOverlay"),
                MinWidth = 62,
                Padding = new Thickness(6, 4, 6, 4),
                FontSize = 11,
                Background = new SolidColorBrush(Color.FromArgb(255, 46, 136, 184)),
                Foreground = new SolidColorBrush(Colors.White),
                BorderBrush = new SolidColorBrush(Color.FromArgb(255, 58, 156, 207)),
                CornerRadius = new CornerRadius(12)
            };
            overlayOnButton.Click += (_, __) =>
            {
                foreach (CheckBox checkBox in overlayCheckBoxes)
                {
                    checkBox.IsChecked = true;
                }
            };
            var overlayOffButton = new Button
            {
                Content = LocalizationManager.Text("DisableAllOverlay"),
                MinWidth = 62,
                Padding = new Thickness(6, 4, 6, 4),
                FontSize = 11,
                Background = new SolidColorBrush(Color.FromArgb(255, 255, 255, 252)),
                Foreground = new SolidColorBrush(Color.FromArgb(255, 29, 34, 51)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(255, 213, 208, 196)),
                CornerRadius = new CornerRadius(12)
            };
            overlayOffButton.Click += (_, __) =>
            {
                foreach (CheckBox checkBox in overlayCheckBoxes)
                {
                    checkBox.IsChecked = false;
                }
            };
            Grid.SetColumn(overlayOnButton, 3);
            commonOverlayRow.Children.Add(overlayOnButton);
            Grid.SetColumn(overlayOffButton, 4);
            commonOverlayRow.Children.Add(overlayOffButton);
            commonOverlayCard.Children.Add(commonOverlayRow);
            commonOverlayCard.Children.Add(commonOverlayFileText);
            layout.Children.Add(commonOverlayCard);

            var scroll = new ScrollViewer
            {
                MaxHeight = 330,
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
                overlayEnabled[slot.Item1] = true;
                selectedFiles.TryGetValue(slot.Item1, out StorageFile existingFile);

                var row = new Grid
                {
                    ColumnSpacing = 5
                };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(62) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(92) });
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
                var fileInfoPanel = new StackPanel
                {
                    Spacing = 1,
                    VerticalAlignment = VerticalAlignment.Center
                };
                fileInfoPanel.Children.Add(fileText);
                if (string.Equals(slot.Item1, "common.wav", StringComparison.OrdinalIgnoreCase))
                {
                    fileInfoPanel.Children.Add(new TextBlock
                    {
                        Text = LocalizationManager.Text("SingleKillVoiceSlotHint"),
                        FontSize = 9,
                        Foreground = new SolidColorBrush(Color.FromArgb(255, 106, 110, 122)),
                        LineHeight = 11,
                        MaxLines = 2,
                        TextWrapping = TextWrapping.WrapWholeWords
                    });
                }
                Grid.SetColumn(fileInfoPanel, 1);
                row.Children.Add(fileInfoPanel);

                var overlayCheckBox = new CheckBox
                {
                    IsChecked = true,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    MinWidth = 0,
                    Padding = new Thickness(0)
                };
                overlayCheckBox.Resources["CheckBoxCheckBackgroundFillChecked"] = new SolidColorBrush(Color.FromArgb(255, 46, 136, 184));
                overlayCheckBox.Resources["CheckBoxCheckBackgroundStrokeChecked"] = new SolidColorBrush(Color.FromArgb(255, 185, 220, 236));
                overlayCheckBox.Resources["CheckBoxCheckBackgroundFillUnchecked"] = new SolidColorBrush(Color.FromArgb(255, 255, 253, 252));
                overlayCheckBox.Resources["CheckBoxCheckBackgroundStrokeUnchecked"] = new SolidColorBrush(Color.FromArgb(255, 213, 208, 196));
                overlayCheckBox.Resources["CheckBoxCheckGlyphForegroundChecked"] = new SolidColorBrush(Colors.White);
                overlayCheckBox.Checked += (_, __) => overlayEnabled[slot.Item1] = true;
                overlayCheckBox.Unchecked += (_, __) => overlayEnabled[slot.Item1] = false;
                overlayCheckBoxes.Add(overlayCheckBox);

                var overlayToggle = new Grid
                {
                    ColumnSpacing = 6,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                overlayToggle.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                overlayToggle.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                overlayToggle.Children.Add(overlayCheckBox);

                var overlayLabel = new TextBlock
                {
                    Text = LocalizationManager.Text("LayerCommon"),
                    FontSize = 11,
                    FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromArgb(255, 29, 34, 51)),
                    VerticalAlignment = VerticalAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis
                };
                Grid.SetColumn(overlayLabel, 1);
                overlayToggle.Children.Add(overlayLabel);
                Grid.SetColumn(overlayToggle, 2);
                row.Children.Add(overlayToggle);

                var previewButton = new Button
                {
                    Content = "\uE768",
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
                Grid.SetColumn(previewButton, 3);
                row.Children.Add(previewButton);

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
                    StorageFile file = await ShowMaterialPickerDialogAsync(
                        isAudio: true,
                        currentGame: GameStyleService.Current,
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
                Grid.SetColumn(browseButton, 4);
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

            await PackCatalogService.CreateVoicePackAsync(
                displayName,
                new VoicePackBuildOptions
                {
                    SelectedFiles = selectedFiles,
                    CommonOverlayEnabled = overlayEnabled,
                    UseBuiltInDefaultCommonOverlay = useBuiltInCommonOverlay,
                    CommonOverlayFile = useBuiltInCommonOverlay ? null : customCommonOverlayFile,
                    HeadImageFile = headImageFile
                });
        }
    }
}

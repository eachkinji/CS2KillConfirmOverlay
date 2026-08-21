using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using KillConfirmGameBar.Services;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace KillConfirmGameBar
{
    public sealed partial class MainPage
    {
        private static readonly (string FileName, string Label, string BuiltInDefault)[] DagoujiaoVoiceSlots =
        {
            ("common.wav", "DagoujiaoSlotCommon", "common.wav"),
            ("headshot.wav", "DagoujiaoSlotHeadshot", "jiaojiaojiao.wav"),
            ("epic.wav", "DagoujiaoSlotEpic", "epic.wav"),
            ("jiaojiaojiao.wav", "DagoujiaoSlotJiaojiaojiao", "jiaojiaojiao.wav")
        };

        private static readonly (string FileName, string DisplayName)[] DagoujiaoBuiltInVoiceChoices =
        {
            ("common.wav", "普通连杀语音 (common.wav)"),
            ("epic.wav", "叫！！！ (epic.wav)"),
            ("jiaojiaojiao.wav", "叫叫叫 (jiaojiaojiao.wav)"),
            ("headshot.wav", "原爆头语音 (headshot.wav)")
        };

        private async Task ShowCreateDagoujiaoVoicePackDialogAsync(
            string initialDisplayName = null,
            IReadOnlyDictionary<string, StorageFile> initialFiles = null,
            StorageFile initialHeadImageFile = null)
        {
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

            var layout = new StackPanel { Spacing = 12, Width = 440 };
            layout.Children.Add(new TextBlock
            {
                Text = LocalizationManager.Text("CreateVoicePack"),
                FontSize = 22,
                FontWeight = Windows.UI.Text.FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 29, 34, 51))
            });
            layout.Children.Add(new TextBlock
            {
                Text = LocalizationManager.Text("DagoujiaoVoiceCollectionsHint"),
                Foreground = new SolidColorBrush(Color.FromArgb(255, 85, 89, 102)),
                TextWrapping = TextWrapping.WrapWholeWords,
                FontSize = 12
            });
            layout.Children.Add(nameBox);

            // Cover Image selector
            var headImageCard = new Border
            {
                Padding = new Thickness(8),
                Background = new SolidColorBrush(Color.FromArgb(255, 255, 255, 252)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(255, 226, 221, 211)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(16)
            };
            var headImageRow = new Grid { ColumnSpacing = 8 };
            headImageRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            headImageRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headImageRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var headPreview = CreatePackPreviewImage("ms-appx:///Assets/GameLogos/dagoujiao.jpg");
            headPreview.Width = 40;
            headPreview.Height = 40;
            if (headImageFile != null)
            {
                await SetPreviewImageAsync(headPreview, headImageFile);
            }
            headImageRow.Children.Add(headPreview);

            var headTextPanel = new StackPanel { Spacing = 0, VerticalAlignment = VerticalAlignment.Center };
            headTextPanel.Children.Add(new TextBlock
            {
                Text = LocalizationManager.Text("CustomHeadImage"),
                FontSize = 13,
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 29, 34, 51))
            });
            headTextPanel.Children.Add(new TextBlock
            {
                Text = LocalizationManager.Text("CustomHeadImageHint"),
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 106, 110, 122))
            });
            Grid.SetColumn(headTextPanel, 1);
            headImageRow.Children.Add(headTextPanel);

            var chooseHeadButton = new Button
            {
                Content = LocalizationManager.Text("ChooseImage"),
                Padding = new Thickness(10, 5, 10, 5),
                FontSize = 12,
                Background = new SolidColorBrush(Color.FromArgb(255, 243, 240, 233)),
                Foreground = new SolidColorBrush(Color.FromArgb(255, 29, 34, 51)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(255, 213, 208, 196)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(14),
                VerticalAlignment = VerticalAlignment.Center
            };
            chooseHeadButton.Click += async (_, __) =>
            {
                StorageFile picked = await PickSingleFileAsync(IconImageExtensions);
                if (picked != null)
                {
                    headImageFile = picked;
                    await SetPreviewImageAsync(headPreview, headImageFile);
                }
            };
            Grid.SetColumn(chooseHeadButton, 2);
            headImageRow.Children.Add(chooseHeadButton);
            headImageCard.Child = headImageRow;
            layout.Children.Add(headImageCard);

            // Slot items in a card
            var slotContainer = new StackPanel { Spacing = 8 };
            foreach (var slot in DagoujiaoVoiceSlots)
            {
                selectedFiles.TryGetValue(slot.FileName, out StorageFile existingFile);

                var slotCard = new Border
                {
                    Padding = new Thickness(10, 8, 10, 8),
                    Background = new SolidColorBrush(Color.FromArgb(255, 255, 255, 252)),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(255, 226, 221, 211)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(14)
                };

                var slotGrid = new Grid { ColumnSpacing = 8, RowSpacing = 4 };
                slotGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                slotGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                slotGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                slotGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                slotGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                var slotLabel = new TextBlock
                {
                    Text = LocalizationManager.Text(slot.Label),
                    FontSize = 13,
                    FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromArgb(255, 29, 34, 51)),
                    VerticalAlignment = VerticalAlignment.Center
                };
                slotGrid.Children.Add(slotLabel);

                var combo = new ComboBox
                {
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    FontSize = 12,
                    CornerRadius = new CornerRadius(10),
                    Background = new SolidColorBrush(Color.FromArgb(255, 250, 250, 247))
                };

                int defaultIndex = 0;
                for (int cIdx = 0; cIdx < DagoujiaoBuiltInVoiceChoices.Length; cIdx++)
                {
                    var choice = DagoujiaoBuiltInVoiceChoices[cIdx];
                    combo.Items.Add(new ComboBoxItem { Content = choice.DisplayName, Tag = choice.FileName });
                    if (string.Equals(choice.FileName, slot.BuiltInDefault, StringComparison.OrdinalIgnoreCase))
                    {
                        defaultIndex = cIdx;
                    }
                }

                ComboBoxItem customItem = new ComboBoxItem
                {
                    Content = existingFile != null ? existingFile.Name : LocalizationManager.Text("ChooseFile") + "...",
                    Tag = "custom"
                };
                combo.Items.Add(customItem);

                if (existingFile != null)
                {
                    combo.SelectedItem = customItem;
                }
                else
                {
                    combo.SelectedIndex = defaultIndex;
                }

                Grid.SetRow(combo, 1);
                slotGrid.Children.Add(combo);

                var browseBtn = new Button
                {
                    Content = LocalizationManager.Current == UiLanguage.SimplifiedChinese ? "选择材料" : "Select Material",
                    Padding = new Thickness(8, 4, 8, 4),
                    FontSize = 11,
                    Background = new SolidColorBrush(Color.FromArgb(255, 243, 240, 233)),
                    Foreground = new SolidColorBrush(Color.FromArgb(255, 29, 34, 51)),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(255, 213, 208, 196)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(10),
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetRow(browseBtn, 1);
                Grid.SetColumn(browseBtn, 1);
                slotGrid.Children.Add(browseBtn);

                var playBtn = new Button
                {
                    Content = "",
                    FontFamily = new FontFamily("Segoe MDL2 Assets"),
                    Padding = new Thickness(8, 4, 8, 4),
                    FontSize = 11,
                    Background = new SolidColorBrush(Color.FromArgb(255, 236, 247, 252)),
                    Foreground = new SolidColorBrush(Color.FromArgb(255, 46, 136, 184)),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(255, 185, 220, 236)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(10),
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetRow(playBtn, 1);
                Grid.SetColumn(playBtn, 2);
                slotGrid.Children.Add(playBtn);

                string currentSlotName = slot.FileName;
                browseBtn.Click += async (_, __) =>
                {
                    StorageFile picked = await ShowMaterialPickerDialogAsync(
                        isAudio: true,
                        currentGame: GameStyleMode.Dagoujiao,
                        stagedFiles: selectedFiles,
                        slotDisplayName: slot.Label,
                        currentSelectedFile: existingFile);
                    if (picked != null)
                    {
                        selectedFiles[currentSlotName] = picked;
                        customItem.Content = picked.Name;
                        combo.SelectedItem = customItem;
                    }
                };

                combo.SelectionChanged += async (_, __) =>
                {
                    if (combo.SelectedItem is ComboBoxItem item)
                    {
                        string tag = item.Tag as string;
                        if (!string.Equals(tag, "custom", StringComparison.OrdinalIgnoreCase))
                        {
                            try
                            {
                                StorageFile builtIn = await StorageFile.GetFileFromApplicationUriAsync(
                                    new Uri($"ms-appx:///KillConfirmService/sounds/dagoujiao/{tag}"));
                                selectedFiles[currentSlotName] = builtIn;
                            }
                            catch { }
                        }
                    }
                };

                playBtn.Click += async (_, __) =>
                {
                    if (selectedFiles.TryGetValue(currentSlotName, out StorageFile file) && file != null)
                    {
                        await PlayPreviewAsync(file);
                    }
                    else if (combo.SelectedItem is ComboBoxItem item && item.Tag is string tag && tag != "custom")
                    {
                        try
                        {
                            StorageFile builtIn = await StorageFile.GetFileFromApplicationUriAsync(
                                new Uri($"ms-appx:///KillConfirmService/sounds/dagoujiao/{tag}"));
                            await PlayPreviewAsync(builtIn);
                        }
                        catch { }
                    }
                };

                slotCard.Child = slotGrid;
                slotContainer.Children.Add(slotCard);
            }

            var scroll = new ScrollViewer
            {
                MaxHeight = 420,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = slotContainer
            };
            layout.Children.Add(scroll);

            var dialog = new ContentDialog
            {
                Title = null,
                Content = CreatePackDialogShell(layout),
                PrimaryButtonText = LocalizationManager.Text("Create"),
                CloseButtonText = LocalizationManager.Text("Cancel"),
                PrimaryButtonStyle = CreateDialogPrimaryButtonStyle(),
                CloseButtonStyle = CreateDialogCloseButtonStyle(),
                RequestedTheme = ElementTheme.Light,
                Background = new SolidColorBrush(Color.FromArgb(255, 250, 250, 247)),
                Foreground = new SolidColorBrush(Color.FromArgb(255, 29, 34, 51))
            };

            dialog.PrimaryButtonClick += async (_, args) =>
            {
                args.Cancel = true;
                string packName = string.IsNullOrWhiteSpace(nameBox.Text)
                    ? "大狗叫语音包"
                    : nameBox.Text.Trim();

                // Ensure all slots have audio files (populate defaults if not assigned)
                foreach (var slot in DagoujiaoVoiceSlots)
                {
                    if (!selectedFiles.ContainsKey(slot.FileName) || selectedFiles[slot.FileName] == null)
                    {
                        try
                        {
                            StorageFile builtIn = await StorageFile.GetFileFromApplicationUriAsync(
                                new Uri($"ms-appx:///KillConfirmService/sounds/dagoujiao/{slot.BuiltInDefault}"));
                            selectedFiles[slot.FileName] = builtIn;
                        }
                        catch { }
                    }
                }

                await PackCatalogService.CreateDagoujiaoVoicePackAsync(packName, new VoicePackBuildOptions
                {
                    SelectedFiles = selectedFiles,
                    HeadImageFile = headImageFile
                });

                dialog.Hide();
            };

            await dialog.ShowAsync();
        }
    }
}

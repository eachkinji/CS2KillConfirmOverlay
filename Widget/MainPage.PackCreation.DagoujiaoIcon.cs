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
        private static readonly (string FileName, string LabelKey, string BuiltInDefault)[] DagoujiaoIconSlots =
        {
            ("common.png", "DagoujiaoIconCommon", "common.png"),
            ("headshot.png", "DagoujiaoIconHeadshot", "headshot.png"),
            ("epic.jpg", "DagoujiaoIconEpic", "epic.jpg"),
            ("1kill.png", "DagoujiaoIconKill1", "common.png"),
            ("2kill.png", "DagoujiaoIconKill2", "common.png"),
            ("3kill.png", "DagoujiaoIconKill3", "common.png"),
            ("4kill.png", "DagoujiaoIconKill4", "common.png"),
            ("5kill.png", "DagoujiaoIconKill5", "common.png")
        };

        private static readonly (string FileName, string DisplayName)[] DagoujiaoBuiltInImageChoices =
        {
            ("common.png", "默认连杀图 (common.png)"),
            ("headshot.png", "默认爆头图 (headshot.png)"),
            ("epic.jpg", "Epic 叫叫叫 (epic.jpg)"),
            ("ice_dog.jpg", "冰狗 (ice_dog.jpg)"),
            ("electric_dog.jpg", "电狗 (electric_dog.jpg)"),
            ("red_dog.jpg", "红狗 (red_dog.jpg)"),
            ("fire_dog.jpg", "火狗 (fire_dog.jpg)"),
            ("sword_dog.jpg", "剑狗 (sword_dog.jpg)"),
            ("old_dog.jpg", "耄耋 (old_dog.jpg)"),
            ("old_dog_bark.jpg", "耄耋叫 (old_dog_bark.jpg)"),
            ("gun_dog.jpg", "枪狗 (gun_dog.jpg)"),
            ("earth_dog.jpg", "土狗 (earth_dog.jpg)"),
            ("scary_dog.jpg", "吓人狗 (scary_dog.jpg)"),
            ("dog_pack.jpg", "一群大狗 (dog_pack.jpg)"),
            ("no_bark.png", "不让叫 (no_bark.png)"),
            ("logo.jpg", "大狗叫 LOGO (logo.jpg)")
        };

        private async Task ShowCreateDagoujiaoIconPackDialogAsync(
            string initialDisplayName = null,
            IReadOnlyDictionary<string, StorageFile> initialFiles = null)
        {
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

            var layout = new StackPanel { Spacing = 12, Width = 450 };
            layout.Children.Add(new TextBlock
            {
                Text = LocalizationManager.Text("CreateIconPack"),
                FontSize = 22,
                FontWeight = Windows.UI.Text.FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 29, 34, 51))
            });
            layout.Children.Add(new TextBlock
            {
                Text = LocalizationManager.Text("DagoujiaoIconCollectionsHint"),
                Foreground = new SolidColorBrush(Color.FromArgb(255, 85, 89, 102)),
                TextWrapping = TextWrapping.WrapWholeWords,
                FontSize = 12
            });
            layout.Children.Add(nameBox);

            var slotContainer = new StackPanel { Spacing = 8 };
            foreach (var slot in DagoujiaoIconSlots)
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
                slotGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                slotGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                slotGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                slotGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                slotGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                var previewImg = new Image
                {
                    Width = 36,
                    Height = 36,
                    Stretch = Stretch.Uniform,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                Grid.SetRowSpan(previewImg, 2);
                slotGrid.Children.Add(previewImg);

                var slotLabel = new TextBlock
                {
                    Text = LocalizationManager.Text(slot.LabelKey),
                    FontSize = 13,
                    FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromArgb(255, 29, 34, 51)),
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(slotLabel, 1);
                slotGrid.Children.Add(slotLabel);

                var combo = new ComboBox
                {
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    FontSize = 12,
                    CornerRadius = new CornerRadius(10),
                    Background = new SolidColorBrush(Color.FromArgb(255, 250, 250, 247))
                };

                int defaultIndex = 0;
                for (int cIdx = 0; cIdx < DagoujiaoBuiltInImageChoices.Length; cIdx++)
                {
                    var choice = DagoujiaoBuiltInImageChoices[cIdx];
                    combo.Items.Add(new ComboBoxItem { Content = choice.DisplayName, Tag = choice.FileName });
                    if (string.Equals(choice.FileName, slot.BuiltInDefault, StringComparison.OrdinalIgnoreCase))
                    {
                        defaultIndex = cIdx;
                    }
                }

                ComboBoxItem customItem = new ComboBoxItem
                {
                    Content = existingFile != null ? existingFile.Name : LocalizationManager.Text("ChooseImage") + "...",
                    Tag = "custom"
                };
                combo.Items.Add(customItem);

                if (existingFile != null)
                {
                    combo.SelectedItem = customItem;
                    await SetPreviewImageAsync(previewImg, existingFile);
                }
                else
                {
                    combo.SelectedIndex = defaultIndex;
                    previewImg.Source = new BitmapImage(new Uri($"ms-appx:///Assets/GameStyles/dagoujiao/killconfirm/textures/{slot.BuiltInDefault}"));
                }

                Grid.SetRow(combo, 1);
                Grid.SetColumn(combo, 1);
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
                Grid.SetColumn(browseBtn, 2);
                slotGrid.Children.Add(browseBtn);

                string currentSlotName = slot.FileName;
                browseBtn.Click += async (_, __) =>
                {
                    StorageFile picked = await ShowMaterialPickerDialogAsync(
                        isAudio: false,
                        currentGame: GameStyleMode.Dagoujiao,
                        stagedFiles: selectedFiles,
                        slotDisplayName: LocalizationManager.Text(slot.LabelKey),
                        currentSelectedFile: existingFile);
                    if (picked != null)
                    {
                        selectedFiles[currentSlotName] = picked;
                        customItem.Content = picked.Name;
                        combo.SelectedItem = customItem;
                        await SetPreviewImageAsync(previewImg, picked);
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
                                previewImg.Source = new BitmapImage(new Uri($"ms-appx:///Assets/GameStyles/dagoujiao/killconfirm/textures/{tag}"));
                                StorageFile builtIn = await StorageFile.GetFileFromApplicationUriAsync(
                                    new Uri($"ms-appx:///Assets/GameStyles/dagoujiao/killconfirm/textures/{tag}"));
                                selectedFiles[currentSlotName] = builtIn;
                            }
                            catch { }
                        }
                    }
                };

                slotCard.Child = slotGrid;
                slotContainer.Children.Add(slotCard);
            }

            var scroll = new ScrollViewer
            {
                MaxHeight = 440,
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
                    ? "大狗叫图标包"
                    : nameBox.Text.Trim();

                // Ensure essential slots have image files
                foreach (var slot in DagoujiaoIconSlots)
                {
                    if (!selectedFiles.ContainsKey(slot.FileName) || selectedFiles[slot.FileName] == null)
                    {
                        try
                        {
                            StorageFile builtIn = await StorageFile.GetFileFromApplicationUriAsync(
                                new Uri($"ms-appx:///Assets/GameStyles/dagoujiao/killconfirm/textures/{slot.BuiltInDefault}"));
                            selectedFiles[slot.FileName] = builtIn;
                        }
                        catch { }
                    }
                }

                await PackCatalogService.CreateDagoujiaoIconPackAsync(packName, selectedFiles);
                dialog.Hide();
            };

            await dialog.ShowAsync();
        }
    }
}

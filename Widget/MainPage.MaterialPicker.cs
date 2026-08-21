using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using KillConfirmGameBar.Helpers;
using KillConfirmGameBar.Services;
using Windows.Media.Core;
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
        public sealed class MaterialItem
        {
            public string Name { get; set; }
            public string SourceCategory { get; set; }
            public StorageFile File { get; set; }
            public string AppxUri { get; set; }
            public bool IsAudio { get; set; }
            public string Key { get; set; }
        }

        private Task<StorageFile> ShowMaterialPickerDialogAsync(
            bool isAudio,
            GameStyleMode currentGame,
            IReadOnlyDictionary<string, StorageFile> stagedFiles,
            string slotDisplayName,
            StorageFile currentSelectedFile)
        {
            var tcs = new TaskCompletionSource<StorageFile>();

            bool isChinese = LocalizationManager.Current == UiLanguage.SimplifiedChinese;
            string materialTypeLabel = isAudio
                ? (isChinese ? "音频素材" : "Audio Material")
                : (isChinese ? "图标素材" : "Icon Material");

            var popup = new Popup
            {
                IsOpen = false,
                IsLightDismissEnabled = false
            };

            var rootOverlay = new Grid
            {
                Width = Window.Current.Bounds.Width,
                Height = Window.Current.Bounds.Height,
                Background = new SolidColorBrush(Color.FromArgb(180, 10, 15, 25))
            };

            // Keep the dialog inside the window: a fixed 490 DIP card overflows
            // narrow windows (common with display scaling above 100%), pushing
            // the per-row select buttons past the right screen edge.
            double CardWidthFor(double windowWidth) => Math.Min(490, Math.Max(300, windowWidth - 24));

            var dialogCard = new Border
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Width = CardWidthFor(Window.Current.Bounds.Width),
                MaxHeight = 620,
                Padding = new Thickness(18),
                Background = new SolidColorBrush(Color.FromArgb(255, 250, 250, 247)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(255, 213, 208, 196)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(18)
            };

            void OnWindowSizeChanged(object sender, Windows.UI.Core.WindowSizeChangedEventArgs e)
            {
                rootOverlay.Width = e.Size.Width;
                rootOverlay.Height = e.Size.Height;
                dialogCard.Width = CardWidthFor(e.Size.Width);
            }
            Window.Current.SizeChanged += OnWindowSizeChanged;

            var rootLayout = new StackPanel { Spacing = 10 };

            // Title Bar
            var titleBar = new Grid { ColumnSpacing = 8 };
            titleBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            titleBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var titlePanel = new StackPanel { Spacing = 2 };
            titlePanel.Children.Add(new TextBlock
            {
                Text = (isChinese ? "选择材料 - " : "Select Material - ") + (slotDisplayName ?? materialTypeLabel),
                FontSize = 16,
                FontWeight = Windows.UI.Text.FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 29, 34, 51))
            });
            titlePanel.Children.Add(new TextBlock
            {
                Text = isChinese
                    ? "优先展示当前游戏临时素材与已导入素材池，亦可切换分类自由选用。"
                    : "Prioritizes current game & staged pool; you can pick from any game.",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 106, 110, 122)),
                TextWrapping = TextWrapping.WrapWholeWords
            });
            Grid.SetColumn(titlePanel, 0);
            titleBar.Children.Add(titlePanel);

            var closeIconBtn = new Button
            {
                Content = "",
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 10,
                Width = 28,
                Height = 28,
                Padding = new Thickness(0),
                Background = new SolidColorBrush(Colors.Transparent),
                Foreground = new SolidColorBrush(Color.FromArgb(255, 120, 120, 120)),
                BorderThickness = new Thickness(0),
                CornerRadius = new CornerRadius(14)
            };
            Grid.SetColumn(closeIconBtn, 1);
            titleBar.Children.Add(closeIconBtn);
            rootLayout.Children.Add(titleBar);

            // Filter Bar (Category Selector + Search Box)
            var filterRow = new Grid { ColumnSpacing = 8 };
            filterRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(190) });
            filterRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var categorySelector = new ComboBox
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Background = new SolidColorBrush(Color.FromArgb(255, 255, 255, 252)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(255, 213, 208, 196)),
                CornerRadius = new CornerRadius(8)
            };

            categorySelector.Items.Add(new ComboBoxItem { Content = isChinese ? "本次已导入素材池" : "Staged Material Pool", Tag = "staged" });
            categorySelector.Items.Add(new ComboBoxItem { Content = isChinese ? "当前游戏素材" : "Current Game", Tag = "current" });
            categorySelector.Items.Add(new ComboBoxItem { Content = isChinese ? "穿越火线 (CF)" : "CrossFire", Tag = "crossfire" });
            categorySelector.Items.Add(new ComboBoxItem { Content = isChinese ? "CSOL (反恐精英Online)" : "CSOL", Tag = "csol" });
            categorySelector.Items.Add(new ComboBoxItem { Content = isChinese ? "无畏契约 (Valorant)" : "Valorant", Tag = "valorant" });
            categorySelector.Items.Add(new ComboBoxItem { Content = isChinese ? "战地系列 (BF)" : "Battlefield", Tag = "battlefield" });
            categorySelector.Items.Add(new ComboBoxItem { Content = isChinese ? "绝地求生 (PUBG)" : "PUBG", Tag = "pubg" });
            categorySelector.Items.Add(new ComboBoxItem { Content = isChinese ? "三角洲行动 (Delta Force)" : "Delta Force", Tag = "deltaforce" });
            categorySelector.Items.Add(new ComboBoxItem { Content = isChinese ? "豆包 (Doubao)" : "Doubao", Tag = "doubao" });
            categorySelector.Items.Add(new ComboBoxItem { Content = isChinese ? "大狗叫 (Dagoujiao)" : "Dagoujiao", Tag = "dagoujiao" });

            categorySelector.SelectedIndex = (stagedFiles != null && stagedFiles.Count > 0) ? 0 : 1;

            Grid.SetColumn(categorySelector, 0);
            filterRow.Children.Add(categorySelector);

            var searchBox = new TextBox
            {
                PlaceholderText = isChinese ? "搜索素材名称..." : "Search materials...",
                Background = new SolidColorBrush(Color.FromArgb(255, 255, 255, 252)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(255, 213, 208, 196)),
                CornerRadius = new CornerRadius(8)
            };
            Grid.SetColumn(searchBox, 1);
            filterRow.Children.Add(searchBox);
            rootLayout.Children.Add(filterRow);

            // Material List Container
            var listScroll = new ScrollViewer
            {
                Height = Math.Min(260, Math.Max(120, Window.Current.Bounds.Height - 340)),
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Background = new SolidColorBrush(Color.FromArgb(255, 248, 249, 251)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(255, 226, 221, 211)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(6)
            };

            var itemsPanel = new StackPanel { Spacing = 6 };
            listScroll.Content = itemsPanel;
            rootLayout.Children.Add(listScroll);

            // Action Toolbar (Browse local file / Clear selection)
            var actionToolbar = new Grid { ColumnSpacing = 8 };
            actionToolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            actionToolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var browseLocalButton = new Button
            {
                Content = isChinese ? "从本地电脑浏览新文件..." : "Browse Local File...",
                FontSize = 11,
                Padding = new Thickness(10, 5, 10, 5),
                Background = new SolidColorBrush(Color.FromArgb(255, 236, 247, 252)),
                Foreground = new SolidColorBrush(Color.FromArgb(255, 46, 136, 184)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(255, 185, 220, 236)),
                CornerRadius = new CornerRadius(8)
            };
            Grid.SetColumn(browseLocalButton, 0);
            actionToolbar.Children.Add(browseLocalButton);

            var clearSlotButton = new Button
            {
                Content = isChinese ? "清空此插槽" : "Clear Slot",
                FontSize = 11,
                Padding = new Thickness(10, 5, 10, 5),
                Background = new SolidColorBrush(Color.FromArgb(255, 254, 242, 242)),
                Foreground = new SolidColorBrush(Color.FromArgb(255, 220, 38, 38)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(255, 254, 202, 202)),
                CornerRadius = new CornerRadius(8)
            };
            Grid.SetColumn(clearSlotButton, 1);
            actionToolbar.Children.Add(clearSlotButton);
            rootLayout.Children.Add(actionToolbar);

            // Dialog Footer Buttons (Confirm / Cancel)
            var footerRow = new Grid { ColumnSpacing = 10, Margin = new Thickness(0, 4, 0, 0) };
            footerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            footerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var confirmBtn = new Button
            {
                Content = isChinese ? "确定选用" : "Select",
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Style = CreateDialogPrimaryButtonStyle()
            };
            Grid.SetColumn(confirmBtn, 0);
            footerRow.Children.Add(confirmBtn);

            var cancelBtn = new Button
            {
                Content = isChinese ? "取消" : "Cancel",
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Style = CreateDialogCloseButtonStyle()
            };
            Grid.SetColumn(cancelBtn, 1);
            footerRow.Children.Add(cancelBtn);
            rootLayout.Children.Add(footerRow);

            dialogCard.Child = rootLayout;
            rootOverlay.Children.Add(dialogCard);
            popup.Child = rootOverlay;

            StorageFile chosenFile = currentSelectedFile;

            void CloseDialog(StorageFile finalResult)
            {
                Window.Current.SizeChanged -= OnWindowSizeChanged;
                popup.IsOpen = false;
                tcs.TrySetResult(finalResult);
            }

            closeIconBtn.Click += (_, __) => CloseDialog(currentSelectedFile);
            cancelBtn.Click += (_, __) => CloseDialog(currentSelectedFile);
            clearSlotButton.Click += (_, __) => CloseDialog(null);
            confirmBtn.Click += (_, __) => CloseDialog(chosenFile);

            async Task RefreshMaterialsAsync()
            {
                itemsPanel.Children.Clear();
                string categoryTag = (categorySelector.SelectedItem as ComboBoxItem)?.Tag as string ?? "current";
                string filterText = (searchBox.Text ?? string.Empty).Trim().ToLowerInvariant();

                var materialList = new List<MaterialItem>();

                // 1. Session Staged files
                if (categoryTag == "staged" || categoryTag == "current")
                {
                    if (stagedFiles != null)
                    {
                        foreach (var pair in stagedFiles)
                        {
                            materialList.Add(new MaterialItem
                            {
                                Name = pair.Value.Name,
                                SourceCategory = isChinese ? "本次导入素材" : "Staged Pool",
                                File = pair.Value,
                                IsAudio = isAudio,
                                Key = pair.Key
                            });
                        }
                    }
                }

                // 2. Disk Staged files for category
                string targetGameKey = categoryTag == "current" ? GameStyleService.ToStorageValue(currentGame) : categoryTag;
                if (targetGameKey != "staged")
                {
                    var diskStaged = await PackCatalogService.GetStagedMaterialsAsync(targetGameKey, isAudio);
                    foreach (var df in diskStaged)
                    {
                        if (!materialList.Any(m => m.File == df || string.Equals(m.Name, df.Name, StringComparison.OrdinalIgnoreCase)))
                        {
                            materialList.Add(new MaterialItem
                            {
                                Name = df.Name,
                                SourceCategory = isChinese ? "临时素材池" : "Staging Pool",
                                File = df,
                                IsAudio = isAudio,
                                Key = df.Name
                            });
                        }
                    }
                }

                // 3. Built-in and Pack materials
                if (categoryTag != "staged")
                {
                    var builtIn = await DiscoverBuiltInMaterialsAsync(isAudio, targetGameKey);
                    materialList.AddRange(builtIn);
                }

                if (!string.IsNullOrWhiteSpace(filterText))
                {
                    materialList = materialList
                        .Where(m => m.Name.ToLowerInvariant().Contains(filterText) || m.SourceCategory.ToLowerInvariant().Contains(filterText))
                        .ToList();
                }

                if (materialList.Count == 0)
                {
                    itemsPanel.Children.Add(new TextBlock
                    {
                        Text = isChinese ? "无匹配素材文件。您可以点击下方“从本地电脑浏览新文件”导入。" : "No matching materials found. Click 'Browse Local File' below.",
                        FontSize = 11,
                        Foreground = new SolidColorBrush(Color.FromArgb(255, 140, 140, 140)),
                        TextWrapping = TextWrapping.WrapWholeWords,
                        Margin = new Thickness(10)
                    });
                    return;
                }

                foreach (var mat in materialList)
                {
                    bool isSelected = chosenFile != null && (chosenFile == mat.File || string.Equals(chosenFile.Name, mat.Name, StringComparison.OrdinalIgnoreCase));
                    var card = new Border
                    {
                        Padding = new Thickness(8, 6, 8, 6),
                        Background = new SolidColorBrush(isSelected
                            ? Color.FromArgb(255, 230, 244, 255)
                            : Color.FromArgb(255, 255, 255, 255)),
                        BorderBrush = new SolidColorBrush(isSelected
                            ? Color.FromArgb(255, 46, 136, 184)
                            : Color.FromArgb(255, 230, 230, 230)),
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(8)
                    };

                    var row = new Grid { ColumnSpacing = 8 };
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                    if (isAudio)
                    {
                        var playBtn = new Button
                        {
                            Content = "",
                            FontFamily = new FontFamily("Segoe MDL2 Assets"),
                            FontSize = 10,
                            MinWidth = 28,
                            Height = 28,
                            Padding = new Thickness(0),
                            Background = new SolidColorBrush(Color.FromArgb(255, 236, 247, 252)),
                            Foreground = new SolidColorBrush(Color.FromArgb(255, 46, 136, 184)),
                            BorderBrush = new SolidColorBrush(Color.FromArgb(255, 185, 220, 236)),
                            CornerRadius = new CornerRadius(14)
                        };
                        playBtn.Click += async (_, __) =>
                        {
                            if (mat.File != null)
                            {
                                await PlayPreviewAsync(mat.File);
                            }
                            else if (!string.IsNullOrWhiteSpace(mat.AppxUri))
                            {
                                try
                                {
                                    StorageFile sf = await StorageFile.GetFileFromApplicationUriAsync(new Uri(mat.AppxUri));
                                    await PlayPreviewAsync(sf);
                                }
                                catch {}
                            }
                        };
                        Grid.SetColumn(playBtn, 0);
                        row.Children.Add(playBtn);
                    }
                    else
                    {
                        var img = new Image
                        {
                            Width = 28,
                            Height = 28,
                            Stretch = Stretch.Uniform,
                            VerticalAlignment = VerticalAlignment.Center
                        };
                        if (mat.File != null)
                        {
                            _ = SetPreviewImageAsync(img, mat.File);
                        }
                        else if (!string.IsNullOrWhiteSpace(mat.AppxUri))
                        {
                            img.Source = new BitmapImage(new Uri(mat.AppxUri));
                        }
                        Grid.SetColumn(img, 0);
                        row.Children.Add(img);
                    }

                    var textStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Spacing = 1 };
                    textStack.Children.Add(new TextBlock
                    {
                        Text = mat.Name,
                        FontSize = 11,
                        FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                        Foreground = new SolidColorBrush(Color.FromArgb(255, 29, 34, 51)),
                        TextTrimming = TextTrimming.CharacterEllipsis
                    });
                    textStack.Children.Add(new TextBlock
                    {
                        Text = mat.SourceCategory,
                        FontSize = 9,
                        Foreground = new SolidColorBrush(Color.FromArgb(255, 120, 120, 120))
                    });
                    Grid.SetColumn(textStack, 1);
                    row.Children.Add(textStack);

                    var selectBtn = new Button
                    {
                        Content = isSelected ? (isChinese ? "已选" : "Selected") : (isChinese ? "选用" : "Use"),
                        FontSize = 10,
                        Padding = new Thickness(8, 3, 8, 3),
                        Background = new SolidColorBrush(isSelected
                            ? Color.FromArgb(255, 46, 136, 184)
                            : Color.FromArgb(255, 240, 240, 240)),
                        Foreground = new SolidColorBrush(isSelected ? Colors.White : Color.FromArgb(255, 29, 34, 51)),
                        CornerRadius = new CornerRadius(6)
                    };
                    selectBtn.Click += async (_, __) =>
                    {
                        if (mat.File != null)
                        {
                            chosenFile = mat.File;
                        }
                        else if (!string.IsNullOrWhiteSpace(mat.AppxUri))
                        {
                            try
                            {
                                chosenFile = await StorageFile.GetFileFromApplicationUriAsync(new Uri(mat.AppxUri));
                            }
                            catch {}
                        }
                        await RefreshMaterialsAsync();
                    };
                    Grid.SetColumn(selectBtn, 2);
                    row.Children.Add(selectBtn);

                    card.Child = row;
                    itemsPanel.Children.Add(card);
                }
            }

            categorySelector.SelectionChanged += async (_, __) => await RefreshMaterialsAsync();
            searchBox.TextChanged += async (_, __) => await RefreshMaterialsAsync();

            browseLocalButton.Click += async (_, __) =>
            {
                var filters = isAudio
                    ? new[] { ".wav", ".mp3", ".m4a", ".ogg", ".flac" }
                    : new[] { ".png", ".jpg", ".jpeg", ".webp", ".tga" };
                StorageFile picked = await PickSingleFileAsync(filters);
                if (picked != null)
                {
                    chosenFile = picked;
                    // Also copy into staging for persistent access
                    await PackCatalogService.ImportStagedMaterialsAsync(currentGame, isAudio, new[] { picked });
                    await RefreshMaterialsAsync();
                }
            };

            _ = RefreshMaterialsAsync();

            popup.IsOpen = true;
            return tcs.Task;
        }

        private static async Task<List<MaterialItem>> DiscoverBuiltInMaterialsAsync(bool isAudio, string gameKey)
        {
            var results = new List<MaterialItem>();
            string norm = (gameKey ?? string.Empty).Trim().ToLowerInvariant();

            try
            {
                StorageFolder installed = Windows.ApplicationModel.Package.Current.InstalledLocation;

                if (isAudio)
                {
                    StorageFolder soundsFolder = await installed.GetFolderAsync(@"KillConfirmService\sounds");
                    IReadOnlyList<StorageFolder> packFolders = await soundsFolder.GetFoldersAsync();

                    foreach (StorageFolder folder in packFolders)
                    {
                        string fname = folder.Name.ToLowerInvariant();
                        if (norm != "battlefield" && norm != "all")
                        {
                            if (norm == "crossfire" && !fname.StartsWith("crossfire_")) continue;
                            if (norm == "csol" && !fname.StartsWith("csol")) continue;
                            if (norm == "pubg" && !fname.StartsWith("pubg")) continue;
                            if (norm == "deltaforce" && !fname.StartsWith("deltaforce")) continue;
                            if (norm == "doubao" && !fname.StartsWith("doubao")) continue;
                            if (norm == "dagoujiao" && !fname.StartsWith("dagoujiao")) continue;
                            if (norm == "valorant" && !fname.StartsWith("valorant")) continue;
                            if (norm == "battlefield1" && !fname.StartsWith("bf1")) continue;
                            if (norm == "battlefield5" && !fname.StartsWith("bf5")) continue;
                            if (norm == "battlefield4" && !fname.StartsWith("bf4")) continue;
                            if (norm == "battlefield2042" && !fname.StartsWith("battlefield2042")) continue;
                        }

                        IReadOnlyList<StorageFile> files = await folder.GetFilesAsync();
                        foreach (StorageFile file in files)
                        {
                            if (file.FileType.Equals(".wav", StringComparison.OrdinalIgnoreCase)
                                || file.FileType.Equals(".mp3", StringComparison.OrdinalIgnoreCase)
                                || file.FileType.Equals(".m4a", StringComparison.OrdinalIgnoreCase))
                            {
                                results.Add(new MaterialItem
                                {
                                    Name = file.Name,
                                    SourceCategory = folder.DisplayName,
                                    File = file,
                                    IsAudio = true
                                });
                            }
                        }
                    }
                }
                else
                {
                    StorageFolder assetsFolder = await installed.GetFolderAsync(@"Assets");
                    try
                    {
                        StorageFolder kcFolder = await assetsFolder.GetFolderAsync(@"KillConfirmCode");
                        IReadOnlyList<StorageFolder> kcSubFolders = await kcFolder.GetFoldersAsync();
                        foreach (StorageFolder folder in kcSubFolders)
                        {
                            string fname = folder.Name.ToLowerInvariant();
                            if (norm == "csol" && !fname.StartsWith("csol")) continue;
                            if (norm == "crossfire" && fname.StartsWith("csol")) continue;

                            IReadOnlyList<StorageFile> files = await folder.GetFilesAsync();
                            foreach (StorageFile file in files)
                            {
                                if (file.FileType.Equals(".png", StringComparison.OrdinalIgnoreCase)
                                    || file.FileType.Equals(".tga", StringComparison.OrdinalIgnoreCase)
                                    || file.FileType.Equals(".jpg", StringComparison.OrdinalIgnoreCase))
                                {
                                    results.Add(new MaterialItem
                                    {
                                        Name = file.Name,
                                        SourceCategory = folder.DisplayName,
                                        File = file,
                                        IsAudio = false
                                    });
                                }
                            }
                        }
                    }
                    catch {}
                }
            }
            catch
            {
            }

            return results;
        }
    }
}

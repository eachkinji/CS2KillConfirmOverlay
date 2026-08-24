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
        private sealed class MaterialFolderItem
        {
            public string Key { get; set; }
            public string DisplayName { get; set; }
            public StorageFolder Folder { get; set; }
            public IReadOnlyList<StorageFile> Files { get; set; }
        }

        private async Task<StorageFile> ShowMaterialPickerDialogAsync(
            bool isAudio,
            GameStyleMode currentGame,
            IReadOnlyDictionary<string, StorageFile> stagedFiles,
            string slotDisplayName,
            StorageFile currentSelectedFile)
        {
            IReadOnlyList<StorageFile> files = await ShowMaterialPickerCoreAsync(
                isAudio,
                allowMultiple: false,
                currentGame,
                stagedFiles?.Values,
                slotDisplayName,
                currentSelectedFile == null ? null : new[] { currentSelectedFile });
            return files?.FirstOrDefault();
        }

        private Task<IReadOnlyList<StorageFile>> ShowAudioMaterialPickerDialogAsync(
            GameStyleMode currentGame,
            IReadOnlyDictionary<string, List<StorageFile>> stagedFiles,
            string slotDisplayName,
            IReadOnlyList<StorageFile> currentSelectedFiles)
        {
            IEnumerable<StorageFile> allStaged = stagedFiles == null
                ? Enumerable.Empty<StorageFile>()
                : stagedFiles.Values.Where(files => files != null).SelectMany(files => files);
            return ShowMaterialPickerCoreAsync(
                isAudio: true,
                allowMultiple: true,
                currentGame,
                allStaged,
                slotDisplayName,
                currentSelectedFiles);
        }

        private Task<IReadOnlyList<StorageFile>> ShowMaterialPickerCoreAsync(
            bool isAudio,
            bool allowMultiple,
            GameStyleMode currentGame,
            IEnumerable<StorageFile> stagedFiles,
            string slotDisplayName,
            IEnumerable<StorageFile> currentSelectedFiles)
        {
            var completion = new TaskCompletionSource<IReadOnlyList<StorageFile>>();
            bool zh = LocalizationManager.Current == UiLanguage.SimplifiedChinese;
            string typeName = isAudio ? (zh ? "语音素材" : "Audio") : (zh ? "图标素材" : "Icon");
            var original = DistinctMaterialFiles(currentSelectedFiles).ToList();
            var selection = original.ToDictionary(MaterialFileIdentity, file => file, StringComparer.OrdinalIgnoreCase);
            var folders = new List<MaterialFolderItem>();
            MaterialFolderItem activeFolder = null;
            IReadOnlyList<StorageFile> activeFiles = Array.Empty<StorageFile>();
            int loadVersion = 0;

            var popup = new Popup { IsLightDismissEnabled = false };
            var overlay = new Grid
            {
                Width = Window.Current.Bounds.Width,
                Height = Window.Current.Bounds.Height,
                Background = new SolidColorBrush(Color.FromArgb(180, 10, 15, 25))
            };
            double CardWidth(double width) => Math.Min(500, Math.Max(300, width - 24));
            var card = new Border
            {
                Width = CardWidth(Window.Current.Bounds.Width),
                MaxHeight = 640,
                Padding = new Thickness(18),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Background = new SolidColorBrush(Color.FromArgb(255, 250, 250, 247)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(255, 213, 208, 196)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(18)
            };
            void OnSizeChanged(object sender, Windows.UI.Core.WindowSizeChangedEventArgs e)
            {
                overlay.Width = e.Size.Width;
                overlay.Height = e.Size.Height;
                card.Width = CardWidth(e.Size.Width);
            }
            Window.Current.SizeChanged += OnSizeChanged;

            var layout = new StackPanel { Spacing = 10 };
            layout.Children.Add(CreateMaterialPickerTitle(
                zh, allowMultiple, slotDisplayName, typeName, out Button closeButton));
            layout.Children.Add(CreateMaterialPickerFilters(
                zh, currentGame, out ComboBox gameSelector, out TextBox searchBox));
            layout.Children.Add(CreateMaterialPickerNavigation(
                zh, out Button backButton, out TextBlock breadcrumb, out TextBlock selectionText));
            layout.Children.Add(CreateMaterialPickerItems(out StackPanel itemsPanel));
            layout.Children.Add(CreateMaterialPickerToolbar(
                zh, allowMultiple, out Button browseButton, out Button clearButton));
            layout.Children.Add(CreateMaterialPickerFooter(
                zh, out Button confirmButton, out Button cancelButton));
            card.Child = layout;
            overlay.Children.Add(card);
            popup.Child = overlay;

            void UpdateSelection()
            {
                selectionText.Text = allowMultiple
                    ? string.Format(zh ? "已选 {0} 个" : "{0} selected", selection.Count)
                    : (selection.Count == 0 ? string.Empty : (zh ? "已选择" : "Selected"));
                confirmButton.Content = allowMultiple
                    ? string.Format(zh ? "确定（{0}）" : "Select ({0})", selection.Count)
                    : (zh ? "确定选用" : "Select");
            }

            void Close(IReadOnlyList<StorageFile> result)
            {
                Window.Current.SizeChanged -= OnSizeChanged;
                popup.IsOpen = false;
                completion.TrySetResult(result);
            }

            void ShowEmpty(string text)
            {
                itemsPanel.Children.Clear();
                itemsPanel.Children.Add(new TextBlock
                {
                    Text = text,
                    Margin = new Thickness(10),
                    FontSize = 11,
                    Foreground = new SolidColorBrush(Color.FromArgb(255, 130, 130, 130)),
                    TextWrapping = TextWrapping.WrapWholeWords
                });
            }

            void RenderFolders()
            {
                activeFolder = null;
                activeFiles = Array.Empty<StorageFile>();
                backButton.Visibility = Visibility.Collapsed;
                breadcrumb.Text = zh ? "素材包文件夹" : "Material-pack folders";
                itemsPanel.Children.Clear();
                string query = (searchBox.Text ?? string.Empty).Trim();
                var visible = folders
                    .Where(folder => string.IsNullOrWhiteSpace(query)
                        || (folder.DisplayName ?? string.Empty).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                    .OrderBy(folder => folder.DisplayName, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                if (visible.Count == 0)
                {
                    ShowEmpty(zh ? "这个游戏下没有匹配的素材包文件夹。" : "No matching pack folders for this game.");
                    return;
                }
                foreach (MaterialFolderItem folder in visible)
                {
                    var row = new Grid { ColumnSpacing = 10 };
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    row.Children.Add(new TextBlock
                    {
                        Text = "",
                        FontFamily = new FontFamily("Segoe MDL2 Assets"),
                        FontSize = 22,
                        Foreground = new SolidColorBrush(Color.FromArgb(255, 224, 151, 34)),
                        VerticalAlignment = VerticalAlignment.Center
                    });
                    var names = new StackPanel { Spacing = 1, VerticalAlignment = VerticalAlignment.Center };
                    names.Children.Add(new TextBlock
                    {
                        Text = folder.DisplayName,
                        FontSize = 12,
                        FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                        TextTrimming = TextTrimming.CharacterEllipsis
                    });
                    names.Children.Add(new TextBlock
                    {
                        Text = zh ? "打开后读取文件" : "Files load when opened",
                        FontSize = 9,
                        Foreground = new SolidColorBrush(Color.FromArgb(255, 112, 116, 126))
                    });
                    Grid.SetColumn(names, 1);
                    row.Children.Add(names);
                    var arrow = new TextBlock
                    {
                        Text = "",
                        FontFamily = new FontFamily("Segoe MDL2 Assets"),
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    Grid.SetColumn(arrow, 2);
                    row.Children.Add(arrow);
                    var button = new Button
                    {
                        Content = row,
                        HorizontalContentAlignment = HorizontalAlignment.Stretch,
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        Padding = new Thickness(10, 8, 10, 8),
                        Background = new SolidColorBrush(Colors.White),
                        BorderBrush = new SolidColorBrush(Color.FromArgb(255, 224, 221, 213)),
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(9)
                    };
                    button.Click += async (_, __) =>
                    {
                        activeFolder = folder;
                        searchBox.Text = string.Empty;
                        activeFiles = await LoadMaterialFolderFilesAsync(folder, isAudio);
                        RenderFiles();
                    };
                    itemsPanel.Children.Add(button);
                }
            }

            void RenderFiles()
            {
                if (activeFolder == null)
                {
                    RenderFolders();
                    return;
                }
                backButton.Visibility = Visibility.Visible;
                breadcrumb.Text = activeFolder.DisplayName;
                itemsPanel.Children.Clear();
                string query = (searchBox.Text ?? string.Empty).Trim();
                var visible = activeFiles
                    .Where(file => IsSupportedMaterialFile(file, isAudio))
                    .Where(file => string.IsNullOrWhiteSpace(query)
                        || file.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                    .OrderBy(file => file.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                if (visible.Count == 0)
                {
                    ShowEmpty(zh ? "此文件夹没有匹配的素材文件。" : "No matching material files in this folder.");
                    return;
                }
                foreach (StorageFile file in visible)
                {
                    string id = MaterialFileIdentity(file);
                    bool selected = selection.ContainsKey(id);
                    var row = new Grid { ColumnSpacing = 8 };
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    if (isAudio)
                    {
                        var play = new Button
                        {
                            Content = "",
                            FontFamily = new FontFamily("Segoe MDL2 Assets"),
                            Width = 28,
                            Height = 28,
                            Padding = new Thickness(0),
                            Background = new SolidColorBrush(Color.FromArgb(255, 236, 247, 252)),
                            Foreground = new SolidColorBrush(Color.FromArgb(255, 46, 136, 184)),
                            BorderBrush = new SolidColorBrush(Color.FromArgb(255, 185, 220, 236)),
                            CornerRadius = new CornerRadius(14)
                        };
                        play.Click += async (_, __) => await PlayPreviewAsync(file);
                        row.Children.Add(play);
                    }
                    else
                    {
                        var preview = new Image { Width = 28, Height = 28, Stretch = Stretch.Uniform };
                        _ = SetPreviewImageAsync(preview, file);
                        row.Children.Add(preview);
                    }
                    var name = new TextBlock
                    {
                        Text = file.Name,
                        VerticalAlignment = VerticalAlignment.Center,
                        FontSize = 11,
                        FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                        TextTrimming = TextTrimming.CharacterEllipsis
                    };
                    Grid.SetColumn(name, 1);
                    row.Children.Add(name);
                    var fileCard = new Border
                    {
                        Padding = new Thickness(8, 6, 8, 6),
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(8),
                        Child = row
                    };
                    var select = new Button
                    {
                        Content = selected
                            ? (zh ? "取消选择" : "Remove")
                            : (allowMultiple ? (zh ? "加入" : "Add") : (zh ? "选用" : "Use")),
                        FontSize = 10,
                        Padding = new Thickness(8, 3, 8, 3),
                        Background = new SolidColorBrush(selected
                            ? Color.FromArgb(255, 46, 136, 184)
                            : Color.FromArgb(255, 240, 240, 240)),
                        Foreground = new SolidColorBrush(selected ? Colors.White : Color.FromArgb(255, 29, 34, 51)),
                        CornerRadius = new CornerRadius(6)
                    };
                    select.Click += (_, __) =>
                    {
                        if (allowMultiple)
                        {
                            if (selection.ContainsKey(id)) selection.Remove(id);
                            else selection[id] = file;
                            bool nowSelected = selection.ContainsKey(id);
                            select.Content = nowSelected ? (zh ? "取消选择" : "Remove") : (zh ? "加入" : "Add");
                            select.Background = new SolidColorBrush(nowSelected
                                ? Color.FromArgb(255, 46, 136, 184)
                                : Color.FromArgb(255, 240, 240, 240));
                            select.Foreground = new SolidColorBrush(nowSelected
                                ? Colors.White
                                : Color.FromArgb(255, 29, 34, 51));
                            fileCard.Background = new SolidColorBrush(nowSelected
                                ? Color.FromArgb(255, 230, 244, 255)
                                : Colors.White);
                            fileCard.BorderBrush = new SolidColorBrush(nowSelected
                                ? Color.FromArgb(255, 46, 136, 184)
                                : Color.FromArgb(255, 230, 230, 230));
                        }
                        else
                        {
                            selection.Clear();
                            selection[id] = file;
                            RenderFiles();
                        }
                        UpdateSelection();
                    };
                    Grid.SetColumn(select, 2);
                    row.Children.Add(select);
                    fileCard.Background = new SolidColorBrush(selected
                        ? Color.FromArgb(255, 230, 244, 255)
                        : Colors.White);
                    fileCard.BorderBrush = new SolidColorBrush(selected
                        ? Color.FromArgb(255, 46, 136, 184)
                        : Color.FromArgb(255, 230, 230, 230));
                    itemsPanel.Children.Add(fileCard);
                }
            }

            async Task LoadGameFoldersAsync()
            {
                int version = ++loadVersion;
                activeFolder = null;
                ShowEmpty(zh ? "正在读取素材包文件夹..." : "Loading pack folders...");
                string gameKey = (gameSelector.SelectedItem as ComboBoxItem)?.Tag as string
                    ?? GameStyleService.ToStorageValue(currentGame);
                List<MaterialFolderItem> result = await DiscoverMaterialFoldersAsync(isAudio, gameKey);
                if (version != loadVersion) return;
                IReadOnlyList<StorageFile> diskStaged = await PackCatalogService.GetStagedMaterialsAsync(gameKey, isAudio);
                if (version != loadVersion) return;
                if (diskStaged.Count > 0)
                {
                    result.Insert(0, new MaterialFolderItem
                    {
                        Key = "staged:" + gameKey,
                        DisplayName = zh ? "临时素材" : "Staged materials",
                        Files = diskStaged
                    });
                }
                folders = result;
                searchBox.Text = string.Empty;
                RenderFolders();
            }

            backButton.Click += (_, __) =>
            {
                searchBox.Text = string.Empty;
                RenderFolders();
            };
            searchBox.TextChanged += (_, __) =>
            {
                if (activeFolder == null) RenderFolders();
                else RenderFiles();
            };
            gameSelector.SelectionChanged += async (_, __) => await LoadGameFoldersAsync();
            browseButton.Click += async (_, __) =>
            {
                IReadOnlyList<StorageFile> picked = await PickMaterialFilesAsync(isAudio, allowMultiple);
                if (picked.Count == 0) return;
                await PackCatalogService.ImportStagedMaterialsAsync(currentGame, isAudio, picked);
                if (!allowMultiple) selection.Clear();
                foreach (StorageFile file in picked) selection[MaterialFileIdentity(file)] = file;
                activeFolder = new MaterialFolderItem
                {
                    Key = "session",
                    DisplayName = zh ? "本次导入" : "Imported now",
                    Files = picked
                };
                activeFiles = picked;
                searchBox.Text = string.Empty;
                UpdateSelection();
                RenderFiles();
            };
            clearButton.Click += (_, __) => Close(Array.Empty<StorageFile>());
            confirmButton.Click += (_, __) => Close(selection.Values.ToList());
            closeButton.Click += (_, __) => Close(original);
            cancelButton.Click += (_, __) => Close(original);

            UpdateSelection();
            popup.IsOpen = true;
            _ = LoadGameFoldersAsync();
            return completion.Task;
        }

        private static async Task<List<MaterialFolderItem>> DiscoverMaterialFoldersAsync(bool isAudio, string gameKey)
        {
            var result = new List<MaterialFolderItem>();
            if (isAudio)
            {
                IReadOnlyList<VoicePackItem> packs = await PackCatalogService.GetAllVoicePacksAsync();
                MaterialFolderItem[] resolved = await Task.WhenAll(
                    packs
                        .Where(pack => IsPackInMaterialCategory(gameKey, pack.Key))
                        .Select(async pack =>
                        {
                            StorageFolder folder = await GetVoicePackFolderAsync(pack);
                            return folder == null ? null : new MaterialFolderItem
                            {
                                Key = "voice:" + pack.Key,
                                DisplayName = PackCatalogService.GetVoicePackDisplayName(pack),
                                Folder = folder
                            };
                        }));
                foreach (MaterialFolderItem folder in resolved.Where(folder => folder != null))
                {
                    result.Add(folder);
                }
            }
            else
            {
                IReadOnlyList<IconPackItem> packs = await PackCatalogService.GetAllIconPacksAsync();
                MaterialFolderItem[] resolved = await Task.WhenAll(
                    packs
                        .Where(pack => IsPackInMaterialCategory(gameKey, pack.Key))
                        .Select(async pack =>
                        {
                            StorageFolder folder = await GetIconPackFolderAsync(pack);
                            return folder == null ? null : new MaterialFolderItem
                            {
                                Key = "icon:" + pack.Key,
                                DisplayName = PackCatalogService.GetIconPackDisplayName(pack),
                                Folder = folder
                            };
                        }));
                foreach (MaterialFolderItem folder in resolved.Where(folder => folder != null))
                {
                    result.Add(folder);
                }
            }
            return result;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KillConfirmGameBar.Helpers;
using KillConfirmGameBar.Services;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace KillConfirmGameBar
{
    public sealed partial class MainPage
    {
        private static Border CreatePackDialogShell(UIElement content)
        {
            return new Border
            {
                Padding = new Thickness(14),
                Background = new SolidColorBrush(Color.FromArgb(255, 250, 250, 247)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(255, 226, 221, 211)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(24),
                Child = content
            };
        }

        private static Style CreateDialogPrimaryButtonStyle()
        {
            var style = new Style(typeof(Button));
            style.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush(Color.FromArgb(255, 46, 136, 184))));
            style.Setters.Add(new Setter(Control.ForegroundProperty, new SolidColorBrush(Colors.White)));
            style.Setters.Add(new Setter(Control.BorderBrushProperty, new SolidColorBrush(Color.FromArgb(255, 58, 156, 207))));
            style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));
            style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(18, 8, 18, 8)));
            style.Setters.Add(new Setter(Control.FontWeightProperty, Windows.UI.Text.FontWeights.SemiBold));
            style.Setters.Add(new Setter(Control.CornerRadiusProperty, new CornerRadius(16)));
            return style;
        }

        private static Style CreateDialogCloseButtonStyle()
        {
            var style = new Style(typeof(Button));
            style.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush(Color.FromArgb(255, 255, 255, 252))));
            style.Setters.Add(new Setter(Control.ForegroundProperty, new SolidColorBrush(Color.FromArgb(255, 29, 34, 51))));
            style.Setters.Add(new Setter(Control.BorderBrushProperty, new SolidColorBrush(Color.FromArgb(255, 213, 208, 196))));
            style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));
            style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(18, 8, 18, 8)));
            style.Setters.Add(new Setter(Control.FontWeightProperty, Windows.UI.Text.FontWeights.SemiBold));
            style.Setters.Add(new Setter(Control.CornerRadiusProperty, new CornerRadius(16)));
            return style;
        }

        private static async Task SetPreviewImageAsync(Image image, StorageFile file)
        {
            try
            {
                if (file.FileType.Equals(".tga", StringComparison.OrdinalIgnoreCase))
                {
                    var softwareBitmap = await TgaDecoder.GetSoftwareBitmapAsync(file);
                    if (softwareBitmap != null)
                    {
                        var source = new SoftwareBitmapSource();
                        await source.SetBitmapAsync(softwareBitmap);
                        image.Source = source;
                        image.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        image.Source = null;
                        image.Visibility = Visibility.Collapsed;
                    }
                }
                else
                {
                    var bitmap = new BitmapImage();
                    using (var stream = await file.OpenReadAsync())
                    {
                        await bitmap.SetSourceAsync(stream);
                    }
                    image.Source = bitmap;
                    image.Visibility = Visibility.Visible;
                }
            }
            catch
            {
                image.Source = null;
                image.Visibility = Visibility.Collapsed;
            }
        }

        // 统一弹窗 layout：Width=440、标题 22、描述、姓名框。返回 layout 并通过 out 暴露 nameBox。
        private static StackPanel CreatePackDialogLayout(
            string titleText,
            string descriptionText,
            string nameBoxPlaceholder,
            string initialDisplayName,
            out TextBox nameBox)
        {
            nameBox = new TextBox
            {
                PlaceholderText = nameBoxPlaceholder,
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
                Text = titleText,
                FontSize = 22,
                FontWeight = Windows.UI.Text.FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 29, 34, 51))
            });
            layout.Children.Add(new TextBlock
            {
                Text = descriptionText,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 85, 89, 102)),
                TextWrapping = TextWrapping.WrapWholeWords,
                FontSize = 12
            });
            layout.Children.Add(nameBox);
            return layout;
        }

        // 统一头图卡片：42×42 预览 + 选择按钮 + 清除按钮。onImageChanged/onImageCleared 让调用方同步其 headImageFile 变量。
        private async Task<Border> CreateHeadImageCardAsync(
            string defaultPreviewUri,
            StorageFile initialHeadImageFile,
            Action<StorageFile> onImageChanged,
            Action onImageCleared)
        {
            var card = new Border
            {
                Padding = new Thickness(8),
                Background = new SolidColorBrush(Color.FromArgb(255, 255, 255, 252)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(255, 226, 221, 211)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(18)
            };
            var row = new Grid { ColumnSpacing = 8 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var headPreview = CreatePackPreviewImage(defaultPreviewUri);
            headPreview.Width = 42;
            headPreview.Height = 42;
            if (initialHeadImageFile != null)
            {
                await SetPreviewImageAsync(headPreview, initialHeadImageFile);
            }
            row.Children.Add(headPreview);

            var textPanel = new StackPanel { Spacing = 0, VerticalAlignment = VerticalAlignment.Center };
            textPanel.Children.Add(new TextBlock
            {
                Text = LocalizationManager.Text("CustomHeadImage"),
                FontSize = 12,
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 29, 34, 51))
            });
            var headFileText = new TextBlock
            {
                Text = initialHeadImageFile != null ? initialHeadImageFile.Name : LocalizationManager.Text("CustomHeadImageHint"),
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 106, 110, 122)),
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            textPanel.Children.Add(headFileText);
            Grid.SetColumn(textPanel, 1);
            row.Children.Add(textPanel);

            var chooseButton = new Button
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
            Grid.SetColumn(chooseButton, 2);
            row.Children.Add(chooseButton);

            var clearButton = new Button
            {
                Content = LocalizationManager.Current == UiLanguage.SimplifiedChinese ? "清除" : "Clear",
                Padding = new Thickness(8, 4, 8, 4),
                FontSize = 11,
                Background = new SolidColorBrush(Color.FromArgb(255, 254, 242, 242)),
                Foreground = new SolidColorBrush(Color.FromArgb(255, 220, 38, 38)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(255, 254, 202, 202)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(14)
            };
            Grid.SetColumn(clearButton, 3);
            row.Children.Add(clearButton);

            chooseButton.Click += async (_, __) =>
            {
                StorageFile file = await PickSingleFileAsync(new[] { ".png", ".jpg", ".jpeg", ".webp", ".tga" });
                if (file == null) return;
                headFileText.Text = file.Name;
                await SetPreviewImageAsync(headPreview, file);
                onImageChanged?.Invoke(file);
            };

            clearButton.Click += (_, __) =>
            {
                headFileText.Text = LocalizationManager.Text("CustomHeadImageHint");
                headPreview.Source = new BitmapImage(new Uri(defaultPreviewUri));
                onImageCleared?.Invoke();
            };

            card.Child = row;
            return card;
        }

        // 统一槽位行：[缩略图/播放][插槽名+文件名(+hint)][选择材料][重置]。
        // isAudio 决定第 1 列是播放按钮还是 30×30 缩略图。selectedFiles 由构建器直接读写。
        // attachExtraColumn 供 CF 注入 overlay CheckBox 列。
        private async Task<Border> CreateSlotRowAsync(
            string slotFileName,
            string slotDisplayName,
            bool isAudio,
            GameStyleMode currentGame,
            Dictionary<string, StorageFile> selectedFiles,
            StorageFile existingFile,
            string defaultPreviewUri = null,
            string hint = null,
            Action<Grid> attachExtraColumn = null)
        {
            var row = new Grid { ColumnSpacing = 8 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(34) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(56) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(56) });

            Image previewImage = null;
            if (isAudio)
            {
                var playButton = new Button
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
                playButton.Click += async (_, __) =>
                {
                    if (selectedFiles.TryGetValue(slotFileName, out StorageFile f) && f != null)
                    {
                        await PlayPreviewAsync(f);
                    }
                };
                row.Children.Add(playButton);
            }
            else
            {
                previewImage = new Image
                {
                    Width = 30,
                    Height = 30,
                    Stretch = Stretch.Uniform,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Visibility = (existingFile != null || !string.IsNullOrEmpty(defaultPreviewUri))
                        ? Visibility.Visible : Visibility.Collapsed
                };
                if (existingFile != null)
                {
                    await SetPreviewImageAsync(previewImage, existingFile);
                }
                else if (!string.IsNullOrEmpty(defaultPreviewUri))
                {
                    previewImage.Source = new BitmapImage(new Uri(defaultPreviewUri));
                }
                row.Children.Add(previewImage);
            }

            var infoPanel = new StackPanel { Spacing = 1, VerticalAlignment = VerticalAlignment.Center };
            infoPanel.Children.Add(new TextBlock
            {
                Text = slotDisplayName,
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
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            infoPanel.Children.Add(fileText);
            if (!string.IsNullOrEmpty(hint))
            {
                infoPanel.Children.Add(new TextBlock
                {
                    Text = hint,
                    FontSize = 9,
                    Foreground = new SolidColorBrush(Color.FromArgb(255, 106, 110, 122)),
                    LineHeight = 11,
                    MaxLines = 2,
                    TextWrapping = TextWrapping.WrapWholeWords
                });
            }
            Grid.SetColumn(infoPanel, 1);
            row.Children.Add(infoPanel);

            var selectButton = new Button
            {
                Content = LocalizationManager.Current == UiLanguage.SimplifiedChinese ? "选择材料" : "Select Material",
                MinWidth = 56,
                Padding = new Thickness(5, 4, 5, 4),
                FontSize = 10,
                Background = new SolidColorBrush(Color.FromArgb(255, 236, 247, 252)),
                Foreground = new SolidColorBrush(Color.FromArgb(255, 46, 136, 184)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(255, 185, 220, 236)),
                CornerRadius = new CornerRadius(12)
            };
            Grid.SetColumn(selectButton, 2);
            row.Children.Add(selectButton);

            var resetButton = new Button
            {
                Content = LocalizationManager.Current == UiLanguage.SimplifiedChinese ? "重置" : "Reset",
                MinWidth = 56,
                Padding = new Thickness(5, 4, 5, 4),
                FontSize = 10,
                Background = new SolidColorBrush(Color.FromArgb(255, 254, 242, 242)),
                Foreground = new SolidColorBrush(Color.FromArgb(255, 220, 38, 38)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(255, 254, 202, 202)),
                CornerRadius = new CornerRadius(12)
            };
            Grid.SetColumn(resetButton, 3);
            row.Children.Add(resetButton);

            attachExtraColumn?.Invoke(row);

            async Task ApplySelectedAsync(StorageFile file)
            {
                if (file != null)
                {
                    selectedFiles[slotFileName] = file;
                    fileText.Text = file.Name;
                    if (previewImage != null)
                    {
                        await SetPreviewImageAsync(previewImage, file);
                        previewImage.Visibility = Visibility.Visible;
                    }
                }
                else
                {
                    selectedFiles.Remove(slotFileName);
                    fileText.Text = LocalizationManager.Text("NotSelected");
                    if (previewImage != null)
                    {
                        if (!string.IsNullOrEmpty(defaultPreviewUri))
                        {
                            previewImage.Source = new BitmapImage(new Uri(defaultPreviewUri));
                            previewImage.Visibility = Visibility.Visible;
                        }
                        else
                        {
                            previewImage.Source = null;
                            previewImage.Visibility = Visibility.Collapsed;
                        }
                    }
                }
            }

            selectButton.Click += async (_, __) =>
            {
                StorageFile current = selectedFiles.TryGetValue(slotFileName, out StorageFile f) ? f : null;
                StorageFile picked = await ShowMaterialPickerDialogAsync(
                    isAudio: isAudio,
                    currentGame: currentGame,
                    stagedFiles: selectedFiles,
                    slotDisplayName: slotDisplayName,
                    currentSelectedFile: current);
                await ApplySelectedAsync(picked);
            };

            resetButton.Click += async (_, __) => await ApplySelectedAsync(null);

            return new Border
            {
                Padding = new Thickness(8, 6, 8, 6),
                Background = new SolidColorBrush(Color.FromArgb(255, 255, 255, 252)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(255, 226, 221, 211)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(14),
                Child = row
            };
        }

        // 统一确认流程：外壳 + 按钮 Style + ShowAsync。
        private static async Task<ContentDialogResult> ShowPackDialogAsync(
            UIElement content,
            string primaryText,
            string closeText)
        {
            var dialog = new ContentDialog
            {
                Content = CreatePackDialogShell(content),
                PrimaryButtonText = primaryText,
                CloseButtonText = closeText,
                PrimaryButtonStyle = CreateDialogPrimaryButtonStyle(),
                CloseButtonStyle = CreateDialogCloseButtonStyle(),
                RequestedTheme = ElementTheme.Light,
                Background = new SolidColorBrush(Colors.Transparent),
                Foreground = new SolidColorBrush(Color.FromArgb(255, 29, 34, 51))
            };
            return await dialog.ShowAsync();
        }

        // 给豆包/大狗叫用：创建前对未选槽位填内置默认。
        private static async Task FillBuiltInDefaultsAsync(
            Dictionary<string, StorageFile> selectedFiles,
            IEnumerable<(string FileName, string Label, string BuiltInDefault)> slots,
            string baseUri)
        {
            foreach (var slot in slots)
            {
                if (!selectedFiles.ContainsKey(slot.FileName) || selectedFiles[slot.FileName] == null)
                {
                    try
                    {
                        StorageFile builtIn = await StorageFile.GetFileFromApplicationUriAsync(
                            new Uri(baseUri + slot.BuiltInDefault));
                        selectedFiles[slot.FileName] = builtIn;
                    }
                    catch { }
                }
            }
        }
    }
}

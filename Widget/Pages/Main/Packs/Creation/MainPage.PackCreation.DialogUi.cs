using System;
using System.Collections.Generic;
using System.Linq;
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
        // Detached dialogs and virtualized pack cards use the same design resources as XAML pages.
        // This dictionary is loaded on the UI thread and never carries per-game mutable state.
        private static ResourceDictionary _panelDesignResources;

        private static T PanelResource<T>(string key)
        {
            if (_panelDesignResources == null)
            {
                _panelDesignResources = new ResourceDictionary
                {
                    Source = new Uri("ms-appx:///Controls/Settings/PanelDesign.xaml")
                };
            }
            return (T)_panelDesignResources[key];
        }

        private ContentDialog CreatePackStatusDialog(string title, UIElement content, string closeText = null)
        {
            var theme = SettingsTheme;
            var layout = new StackPanel { Spacing = 18, MinWidth = 340, MaxWidth = 500, Margin = new Thickness(10) };
            layout.Children.Add(new TextBlock
            {
                Style = PanelResource<Style>("PanelPageTitleTextStyle"),
                Text = title,
                Foreground = new SolidColorBrush(theme.Text), TextWrapping = TextWrapping.Wrap
            });
            layout.Children.Add(content);
            var shell = CreatePackDialogShell(layout);
            shell.Background = new SolidColorBrush(theme.Panel);
            shell.BorderBrush = new SolidColorBrush(theme.Border);
            var dialog = new ContentDialog
            {
                Content = shell, CloseButtonText = closeText ?? string.Empty,
                CloseButtonStyle = CreateDialogCloseButtonStyle(),
                RequestedTheme = ElementTheme.Light, Background = new SolidColorBrush(Colors.Transparent),
                Foreground = new SolidColorBrush(theme.Text)
            };
            ControlTheme.Apply(dialog, theme);
            return dialog;
        }

        private static Border CreatePackDialogShell(UIElement content)
        {
            return new Border
            {
                Padding = PanelResource<Thickness>("PanelCardPadding"),
                Background = PanelResource<Brush>("PanelFieldBrush"),
                BorderBrush = PanelResource<Brush>("PanelBorderBrush"),
                BorderThickness = new Thickness(1),
                CornerRadius = PanelResource<CornerRadius>("PanelCardRadius"),
                Child = content
            };
        }

        private Style CreateDialogPrimaryButtonStyle()
        {
            var style = new Style(typeof(Button)) { BasedOn = PanelResource<Style>("PanelPrimaryButtonStyle") };
            style.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush(SettingsTheme.Accent)));
            style.Setters.Add(new Setter(Control.BorderBrushProperty, new SolidColorBrush(SettingsTheme.Accent)));
            return style;
        }

        private static Style CreateDialogCloseButtonStyle()
        {
            return PanelResource<Style>("PanelButtonStyle");
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
                    var bitmap = new BitmapImage { DecodePixelWidth = 128 };
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

        // 统一弹窗 layout：Width=440、共享页面标题、描述、名称框。返回 layout 并通过 out 暴露 nameBox。
        private static StackPanel CreatePackDialogLayout(
            string titleText,
            string descriptionText,
            string nameBoxPlaceholder,
            string initialDisplayName,
            out TextBox nameBox)
        {
            nameBox = new TextBox
            {
                Style = PanelResource<Style>("PanelTextBoxStyle"),
                PlaceholderText = nameBoxPlaceholder,
                Text = initialDisplayName ?? string.Empty,
                Background = PanelResource<Brush>("PanelFieldBrush"),
                Foreground = PanelResource<Brush>("PanelTextPrimaryBrush"),
                BorderBrush = PanelResource<Brush>("PanelBorderBrush"),
                BorderThickness = new Thickness(1)
            };

            var layout = new StackPanel { Spacing = 12, Width = 440 };
            layout.Children.Add(new TextBlock
            {
                Style = PanelResource<Style>("PanelPageTitleTextStyle"),
                Text = titleText,
                Foreground = PanelResource<Brush>("PanelTextPrimaryBrush")
            });
            layout.Children.Add(new TextBlock
            {
                Style = PanelResource<Style>("PanelCaptionTextStyle"),
                Text = descriptionText,
                Foreground = PanelResource<Brush>("PanelTextSecondaryBrush"),
                TextWrapping = TextWrapping.WrapWholeWords
            });
            layout.Children.Add(nameBox);
            return layout;
        }

        // 统一头图卡片：42×42 预览 + 选择按钮 + 清除按钮。onImageChanged/onImageCleared 让调用方同步其 headImageFile 变量。
        private async Task<Border> CreateHeadImageCardAsync(
            string defaultPreviewUri,
            StorageFile initialHeadImageFile,
            Action<StorageFile> onImageChanged,
            Action onImageCleared,
            bool allowTga = true)
        {
            var card = new Border
            {
                Padding = new Thickness(8),
                Background = PanelResource<Brush>("PanelFieldBrush"),
                BorderBrush = PanelResource<Brush>("PanelBorderBrush"),
                BorderThickness = new Thickness(1),
                CornerRadius = PanelResource<CornerRadius>("PanelSectionRadius")
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
                Style = PanelResource<Style>("PanelTitleTextStyle"),
                Text = LocalizationManager.Text("CustomHeadImage"),
                Foreground = PanelResource<Brush>("PanelTextPrimaryBrush")
            });
            var headFileText = new TextBlock
            {
                Style = PanelResource<Style>("PanelCaptionTextStyle"),
                Text = initialHeadImageFile != null ? initialHeadImageFile.Name : LocalizationManager.Text("CustomHeadImageHint"),
                Foreground = PanelResource<Brush>("PanelTextMutedBrush"),
                TextWrapping = TextWrapping.WrapWholeWords, MaxLines = 2, TextTrimming = TextTrimming.CharacterEllipsis
            };
            textPanel.Children.Add(headFileText);
            Grid.SetColumn(textPanel, 1);
            row.Children.Add(textPanel);

            var chooseButton = new Button
            {
                Style = PanelResource<Style>("PanelCompactButtonStyle"),
                Content = LocalizationManager.Text("ChooseImage"),
                Background = PanelResource<Brush>("PanelAccentSoftBrush"),
                Foreground = new SolidColorBrush(SettingsTheme.Accent),
                BorderBrush = PanelResource<Brush>("PanelAccentBorderBrush"),
                BorderThickness = new Thickness(1)
            };
            Grid.SetColumn(chooseButton, 2);
            row.Children.Add(chooseButton);

            var clearButton = new Button
            {
                Style = PanelResource<Style>("PanelDangerCompactButtonStyle"),
                Content = LocalizationManager.Current == UiLanguage.SimplifiedChinese ? "清除" : "Clear",
                Background = new SolidColorBrush(Color.FromArgb(26, 255, 59, 48)),
                Foreground = PanelResource<Brush>("PanelDangerBrush"),
                BorderBrush = new SolidColorBrush(Color.FromArgb(61, 255, 59, 48)),
                BorderThickness = new Thickness(1)
            };
            Grid.SetColumn(clearButton, 3);
            row.Children.Add(clearButton);

            chooseButton.Click += async (_, __) =>
            {
                StorageFile file = await PickSingleFileAsync(allowTga
                    ? new[] { ".png", ".jpg", ".jpeg", ".webp", ".tga" }
                    : new[] { ".png", ".jpg", ".jpeg", ".webp" });
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
        // attachExtraColumn 供 CF 在独立第二行注入 overlay CheckBox，不挤占文件名宽度。
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
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(PanelResource<double>("PanelControlHeight")) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            Image previewImage = null;
            if (isAudio)
            {
                var playButton = new Button
                {
                    Style = PanelResource<Style>("PanelIconButtonStyle"),
                    Content = "",
                    FontFamily = new FontFamily("Segoe MDL2 Assets"),
                    Background = PanelResource<Brush>("PanelAccentSoftBrush"),
                    Foreground = new SolidColorBrush(SettingsTheme.Accent),
                    BorderBrush = PanelResource<Brush>("PanelAccentBorderBrush")
                };
                Windows.UI.Xaml.Automation.AutomationProperties.SetName(playButton, LocalizationManager.Current == UiLanguage.SimplifiedChinese ? "试听" : "Preview audio");
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
                Style = PanelResource<Style>("PanelTitleTextStyle"),
                Text = slotDisplayName,
                Foreground = PanelResource<Brush>("PanelTextPrimaryBrush"),
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            var fileText = new TextBlock
            {
                Style = PanelResource<Style>("PanelCaptionTextStyle"),
                Text = existingFile?.Name ?? LocalizationManager.Text("NotSelected"),
                Foreground = PanelResource<Brush>("PanelTextMutedBrush"),
                TextWrapping = TextWrapping.NoWrap, TextTrimming = TextTrimming.CharacterEllipsis
            };
            infoPanel.Children.Add(fileText);
            if (!string.IsNullOrEmpty(hint))
            {
                infoPanel.Children.Add(new TextBlock
                {
                    Style = PanelResource<Style>("PanelCaptionTextStyle"),
                    Text = hint,
                    Foreground = PanelResource<Brush>("PanelTextMutedBrush"),
                    LineHeight = 18,
                    MaxLines = 2,
                    TextWrapping = TextWrapping.WrapWholeWords
                });
            }
            Grid.SetColumn(infoPanel, 1);
            row.Children.Add(infoPanel);

            var selectButton = new Button
            {
                Style = PanelResource<Style>("PanelCompactButtonStyle"),
                Content = LocalizationManager.Current == UiLanguage.SimplifiedChinese ? "选择材料" : "Select Material",
                MinWidth = 56,
                Background = PanelResource<Brush>("PanelAccentSoftBrush"),
                Foreground = new SolidColorBrush(SettingsTheme.Accent),
                BorderBrush = PanelResource<Brush>("PanelAccentBorderBrush")
            };
            Grid.SetColumn(selectButton, 2);
            row.Children.Add(selectButton);

            var resetButton = new Button
            {
                Style = PanelResource<Style>("PanelDangerCompactButtonStyle"),
                Content = LocalizationManager.Current == UiLanguage.SimplifiedChinese ? "重置" : "Reset",
                MinWidth = 56,
                Background = new SolidColorBrush(Color.FromArgb(26, 255, 59, 48)),
                Foreground = PanelResource<Brush>("PanelDangerBrush"),
                BorderBrush = new SolidColorBrush(Color.FromArgb(61, 255, 59, 48))
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
                Background = PanelResource<Brush>("PanelFieldBrush"),
                BorderBrush = PanelResource<Brush>("PanelBorderBrush"),
                BorderThickness = new Thickness(1),
                CornerRadius = PanelResource<CornerRadius>("PanelSectionRadius"),
                Child = row
            };
        }
    }
}

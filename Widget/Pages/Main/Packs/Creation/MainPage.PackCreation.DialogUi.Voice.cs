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

        // Voice slots use the same material browser as icons, but keep a list per
        // slot. The generated manifest turns lists into random-play arrays.
        private Task<Border> CreateVoiceSlotRowAsync(
            string slotFileName,
            string slotDisplayName,
            GameStyleMode currentGame,
            Dictionary<string, List<StorageFile>> selectedFiles,
            IReadOnlyList<StorageFile> existingFiles,
            string hint = null,
            Action<Grid> attachExtraColumn = null)
        {
            var row = new Grid { ColumnSpacing = 8 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(34) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(56) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(56) });

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
                if (selectedFiles.TryGetValue(slotFileName, out List<StorageFile> files)
                    && files != null && files.Count > 0)
                {
                    int index = files.Count == 1 ? 0 : new Random().Next(files.Count);
                    await PlayPreviewAsync(files[index]);
                }
            };
            row.Children.Add(playButton);

            var info = new StackPanel { Spacing = 1, VerticalAlignment = VerticalAlignment.Center };
            info.Children.Add(new TextBlock
            {
                Text = slotDisplayName,
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 29, 34, 51)),
                FontSize = 12,
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            var fileText = new TextBlock
            {
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 106, 110, 122)),
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            info.Children.Add(fileText);
            if (!string.IsNullOrEmpty(hint))
            {
                info.Children.Add(new TextBlock
                {
                    Text = hint,
                    FontSize = 9,
                    Foreground = new SolidColorBrush(Color.FromArgb(255, 106, 110, 122)),
                    LineHeight = 11,
                    MaxLines = 2,
                    TextWrapping = TextWrapping.WrapWholeWords
                });
            }
            Grid.SetColumn(info, 1);
            row.Children.Add(info);

            var selectButton = new Button
            {
                Content = LocalizationManager.Current == UiLanguage.SimplifiedChinese ? "选择素材" : "Select",
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

            void ApplySelection(IEnumerable<StorageFile> files)
            {
                List<StorageFile> list = (files ?? Enumerable.Empty<StorageFile>())
                    .Where(file => file != null)
                    .GroupBy(file => string.IsNullOrWhiteSpace(file.Path) ? file.Name : file.Path, StringComparer.OrdinalIgnoreCase)
                    .Select(group => group.First())
                    .ToList();
                if (list.Count == 0)
                {
                    selectedFiles.Remove(slotFileName);
                    fileText.Text = LocalizationManager.Text("NotSelected");
                }
                else
                {
                    selectedFiles[slotFileName] = list;
                    fileText.Text = list.Count == 1
                        ? list[0].Name
                        : string.Format(
                            LocalizationManager.Current == UiLanguage.SimplifiedChinese ? "已选 {0} 个（随机播放）" : "{0} selected (random)",
                            list.Count);
                }
            }

            ApplySelection(existingFiles);
            selectButton.Click += async (_, __) =>
            {
                selectedFiles.TryGetValue(slotFileName, out List<StorageFile> current);
                IReadOnlyList<StorageFile> picked = await ShowAudioMaterialPickerDialogAsync(
                    currentGame,
                    selectedFiles,
                    slotDisplayName,
                    current);
                ApplySelection(picked);
            };
            resetButton.Click += (_, __) => ApplySelection(null);

            return Task.FromResult(new Border
            {
                Padding = new Thickness(8, 6, 8, 6),
                Background = new SolidColorBrush(Color.FromArgb(255, 255, 255, 252)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(255, 226, 221, 211)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(14),
                Child = row
            });
        }

        private static Dictionary<string, List<StorageFile>> CreateVoiceSelectionMap(
            IReadOnlyDictionary<string, IReadOnlyList<StorageFile>> initialFiles)
        {
            var result = new Dictionary<string, List<StorageFile>>(StringComparer.OrdinalIgnoreCase);
            if (initialFiles == null) return result;
            foreach (var pair in initialFiles)
            {
                result[pair.Key] = (pair.Value ?? Array.Empty<StorageFile>())
                    .Where(file => file != null)
                    .ToList();
            }
            return result;
        }

        private static IReadOnlyDictionary<string, IReadOnlyList<StorageFile>> AsReadOnlyVoiceSelection(
            Dictionary<string, List<StorageFile>> selectedFiles)
        {
            return selectedFiles.ToDictionary(
                pair => pair.Key,
                pair => (IReadOnlyList<StorageFile>)pair.Value,
                StringComparer.OrdinalIgnoreCase);
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

        private static async Task FillBuiltInVoiceDefaultsAsync(
            Dictionary<string, List<StorageFile>> selectedFiles,
            IEnumerable<(string FileName, string Label, string BuiltInDefault)> slots,
            string baseUri)
        {
            foreach (var slot in slots)
            {
                if (!selectedFiles.TryGetValue(slot.FileName, out List<StorageFile> files)
                    || files == null || files.Count == 0)
                {
                    if (string.IsNullOrWhiteSpace(slot.BuiltInDefault)) continue;
                    try
                    {
                        StorageFile builtIn = await StorageFile.GetFileFromApplicationUriAsync(
                            new Uri(baseUri + slot.BuiltInDefault));
                        selectedFiles[slot.FileName] = new List<StorageFile> { builtIn };
                    }
                    catch { }
                }
            }
        }
    }
}

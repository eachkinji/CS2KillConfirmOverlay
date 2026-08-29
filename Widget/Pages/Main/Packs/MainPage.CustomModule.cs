using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
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

namespace KillConfirmGameBar
{
    public sealed partial class MainPage
    {
        private sealed class CustomSequenceRow
        {
            public string Slot;
            public CustomSequenceInput Input;
            public TextBox Fps, Hold, Start, End;
            public int Mode;
        }

        private static ToggleButton CreateCustomSourceModeButton(string glyph, string label)
        {
            var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, HorizontalAlignment = HorizontalAlignment.Center };
            content.Children.Add(new FontIcon { Glyph = glyph, FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 13 });
            content.Children.Add(new TextBlock { Text = label, FontSize = 11, VerticalAlignment = VerticalAlignment.Center });
            return new ToggleButton
            {
                Content = content, MinHeight = 38, HorizontalAlignment = HorizontalAlignment.Stretch,
                Background = new SolidColorBrush(Color.FromArgb(255, 255, 255, 252)),
                Foreground = new SolidColorBrush(Color.FromArgb(255, 58, 63, 76)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(255, 213, 208, 196)),
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12)
            };
        }

        private static void ApplyCustomSourceModeVisual(IReadOnlyList<ToggleButton> buttons, int selected)
        {
            for (int index = 0; index < buttons.Count; index++)
            {
                bool active = index == selected;
                buttons[index].IsChecked = active;
                buttons[index].Background = new SolidColorBrush(active
                    ? Color.FromArgb(255, 226, 244, 251) : Color.FromArgb(255, 255, 255, 252));
                buttons[index].Foreground = new SolidColorBrush(active
                    ? Color.FromArgb(255, 24, 116, 158) : Color.FromArgb(255, 58, 63, 76));
                buttons[index].BorderBrush = new SolidColorBrush(active
                    ? Color.FromArgb(255, 81, 170, 207) : Color.FromArgb(255, 213, 208, 196));
            }
        }

        private async Task ImportCustomModuleAsync(bool zip)
        {
            bool chinese = LocalizationManager.Current == UiLanguage.SimplifiedChinese;
            string title = chinese ? "导入自定义图标包" : "Import custom icon pack";
            try
            {
                Func<IProgress<string>, ICollection<string>, Task<IconPackItem>> import;
                if (zip)
                {
                    var picker = new FileOpenPicker(); picker.FileTypeFilter.Add(".zip");
                    StorageFile file = await picker.PickSingleFileAsync();
                    if (file == null) return;
                    import = (progress, warnings) => CustomSequencePackService.ImportZipAsync(file, progress, warnings);
                }
                else
                {
                    var picker = new FolderPicker(); picker.FileTypeFilter.Add("*");
                    StorageFolder folder = await picker.PickSingleFolderAsync();
                    if (folder == null) return;
                    import = (progress, warnings) => CustomSequencePackService.ImportFolderAsync(folder, progress, warnings: warnings);
                }
                var progressText = new TextBlock { Text = chinese ? "正在解析图标包…" : "Reading icon pack…", TextWrapping = TextWrapping.Wrap };
                var dialog = new ContentDialog { Title = title, Content = progressText };
                bool running = true;
                dialog.Closing += (s, e) => e.Cancel = running;
                var showing = dialog.ShowAsync();
                var notes = new List<string>();
                IconPackItem imported;
                try { imported = await import(new Progress<string>(text => progressText.Text = text), notes); }
                finally { running = false; dialog.Hide(); await showing; }
                await ShowMessageAsync(title, imported.DisplayName
                    + (chinese ? "\n已加入图标包库，可在库中编辑；效果测试使用现有测试功能。" : "\nAdded to the icon library. Edit it there and use the existing tests.")
                    + (notes.Count == 0 ? "" : "\n\n" + string.Join("\n", notes.Distinct())));
            }
            catch (Exception ex) { await ShowMessageAsync(title, ex.Message); }
        }

        private async Task ExportCustomModuleAsync(IconPackItem pack)
        {
            try
            {
                var picker = new FileSavePicker { SuggestedFileName = pack.DisplayName };
                picker.FileTypeChoices.Add("CS2 Customizer ZIP", new[] { ".zip" });
                var file = await picker.PickSaveFileAsync();
                if (file != null) await CustomSequencePackService.ExportAsync(pack.Key, file);
            }
            catch (Exception ex) { await ShowMessageAsync("Export ZIP / 导出 ZIP", ex.Message); }
        }

        // The library's create/edit action edits assets, never the active selection
        // or the Game Bar playback/display settings. Cancel leaves the pack untouched.
        private async Task ShowCustomModuleEditorAsync(string key)
        {
            bool chinese = LocalizationManager.Current == UiLanguage.SimplifiedChinese;
            string title = chinese ? "自定义逐帧图标包" : "Custom frame icon pack";
            try
            {
                IconPackItem existing = key == null ? null : await PackCatalogService.GetIconPackAsync(key);
                StorageFolder original = key == null ? null : await PackCatalogService.GetImportedIconFolderAsync(key);
                if (key != null && (existing == null || original == null))
                    throw new InvalidDataException(chinese ? "素材包不存在，请刷新图标包库。" : "The pack no longer exists. Refresh the library.");
                var notes = new List<string>();
                var initial = original == null ? new List<CustomSequenceInput>() : await CustomSequencePackService.ReadInputsAsync(original, notes);
                var layout = CreatePackDialogLayout(title,
                    chinese ? "这里按击杀等级单独添加素材：先选择导入方式，再选择对应文件。单槽不会扫描或猜测目录结构；整套目录/ZIP 的自动解析请使用图标包库上方的“导入整包”。"
                        : "Add assets to each kill level: choose an input mode first, then its files. A slot never scans or guesses a folder layout; use Import full pack above for automatic folder/ZIP parsing.",
                    LocalizationManager.Text("IconPackNamePlaceholder"), existing?.DisplayName, out TextBox name);
                var status = new TextBlock { Text = string.Join("\n", notes), TextWrapping = TextWrapping.Wrap, FontSize = 12 };
                var rows = new List<CustomSequenceRow>();
                var slots = new StackPanel { Spacing = 12 };
                var slotHost = new ContentControl { Content = slots, HorizontalContentAlignment = HorizontalAlignment.Stretch };
                var headshotSlots = new StackPanel { Spacing = 12 };
                var showHeadshots = new CheckBox
                {
                    Content = chinese ? "配置爆头变体（可选）" : "Configure headshot variants (optional)",
                    IsChecked = initial.Any(i => i.Slot.EndsWith("hs", StringComparison.Ordinal))
                };
                bool busy = false;
                var dialog = new ContentDialog
                {
                    Content = CreatePackDialogShell(layout),
                    PrimaryButtonText = chinese ? "保存" : "Save",
                    CloseButtonText = LocalizationManager.Text("Cancel"),
                    PrimaryButtonStyle = CreateDialogPrimaryButtonStyle(),
                    CloseButtonStyle = CreateDialogCloseButtonStyle(),
                    RequestedTheme = ElementTheme.Light
                };
                void SetBusy(bool value)
                {
                    busy = value; slotHost.IsEnabled = !value; showHeadshots.IsEnabled = !value; name.IsEnabled = !value;
                    dialog.IsPrimaryButtonEnabled = !value; dialog.IsSecondaryButtonEnabled = !value;
                }
                for (int level = 1; level <= 5; level++)
                    foreach (string suffix in new[] { "", "hs" })
                    {
                        string slot = level + suffix;
                        var row = new CustomSequenceRow { Slot = slot, Input = initial.FirstOrDefault(i => i.Slot == slot) };
                        rows.Add(row);
                        row.Mode = row.Input?.Sheet != null ? 2 : row.Input?.Frames?.Count == 1 ? 0 : 1;
                        var card = new StackPanel { Spacing = 9 };
                        var header = new Grid();
                        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                        header.Children.Add(new TextBlock { Text = chinese ? level + " 杀" + (suffix == "" ? "" : " · 爆头") : "Kill " + level + (suffix == "" ? "" : " · Headshot"), FontWeight = Windows.UI.Text.FontWeights.SemiBold, FontSize = 14 });
                        var clear = new Button { Content = "×", Padding = new Thickness(8, 2, 8, 2), FontSize = 14, Background = new SolidColorBrush(Colors.Transparent), BorderThickness = new Thickness(0) };
                        ToolTipService.SetToolTip(clear, chinese ? "清除这个击杀等级的素材" : "Clear this kill level");
                        Grid.SetColumn(clear, 1); header.Children.Add(clear); card.Children.Add(header);
                        var modeGrid = new Grid { ColumnSpacing = 6, RowSpacing = 6 };
                        for (int column = 0; column < 2; column++) modeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                        for (int gridRow = 0; gridRow < 2; gridRow++) modeGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                        var modeButtons = new[] {
                            CreateCustomSourceModeButton("\uEB9F", chinese ? "单张" : "Image"),
                            CreateCustomSourceModeButton("\uE8B7", chinese ? "散帧" : "Frames"),
                            CreateCustomSourceModeButton("\uE91B", chinese ? "图集" : "Atlas"),
                            CreateCustomSourceModeButton("\uE714", chinese ? "视频" : "Video")
                        };
                        for (int mode = 0; mode < modeButtons.Length; mode++)
                        {
                            Grid.SetColumn(modeButtons[mode], mode % 2);
                            Grid.SetRow(modeButtons[mode], mode / 2);
                            modeGrid.Children.Add(modeButtons[mode]);
                        }
                        card.Children.Add(modeGrid);
                        var description = new TextBlock { TextWrapping = TextWrapping.Wrap, FontSize = 12 };
                        card.Children.Add(description);
                        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
                        var choose = new Button { Padding = new Thickness(12, 6, 12, 6), FontSize = 11, CornerRadius = new CornerRadius(12), Background = new SolidColorBrush(Color.FromArgb(255, 226, 244, 251)), Foreground = new SolidColorBrush(Color.FromArgb(255, 24, 116, 158)) };
                        var folder = new Button { Padding = new Thickness(12, 6, 12, 6), FontSize = 11, CornerRadius = new CornerRadius(12) };
                        actions.Children.Add(choose); actions.Children.Add(folder); card.Children.Add(actions);
                        row.Fps = new TextBox { Header = "FPS (1–60)", Width = 130 };
                        row.Hold = new TextBox { Header = chinese ? "末帧停留（秒）" : "Last-frame hold (s)", Width = 165 };
                        var timing = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
                        timing.Children.Add(row.Fps); timing.Children.Add(row.Hold); card.Children.Add(timing);
                        row.Start = new TextBox { Header = chinese ? "起点（秒）" : "Start (s)", Width = 105, Text = "0" };
                        row.End = new TextBox { Header = chinese ? "终点（秒，最长 20）" : "End (s, max 20)", Width = 145, Text = "5" };
                        var videoRange = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
                        videoRange.Children.Add(row.Start); videoRange.Children.Add(row.End); card.Children.Add(videoRange);
                        var borderedCard = new Border { Padding = new Thickness(12), CornerRadius = new CornerRadius(16), Background = new SolidColorBrush(Color.FromArgb(255, 255, 255, 252)), BorderBrush = new SolidColorBrush(Color.FromArgb(255, 226, 221, 211)), BorderThickness = new Thickness(1), Child = card };
                        bool switchingMode = false;
                        void RefreshMode()
                        {
                            switchingMode = true;
                            ApplyCustomSourceModeVisual(modeButtons, row.Mode);
                            switchingMode = false;
                            choose.Content = row.Mode == 0 ? (chinese ? "选择一张图片" : "Choose image")
                                : row.Mode == 1 ? (chinese ? "选择多张帧" : "Choose frames")
                                : row.Mode == 2 ? (chinese ? "同时选择 PNG + JSON" : "Select PNG + JSON together")
                                : (chinese ? "选择视频" : "Choose video");
                            folder.Content = chinese ? "选择散帧目录" : "Choose frame folder";
                            folder.Visibility = row.Mode == 1 ? Visibility.Visible : Visibility.Collapsed;
                            videoRange.Visibility = row.Mode == 3 ? Visibility.Visible : Visibility.Collapsed;
                        }
                        string EmptyModeDescription()
                        {
                            if (row.Mode == 0) return chinese ? "单个静态图片；不会查找同目录文件。" : "One static image; sibling files are not inspected.";
                            if (row.Mode == 1) return chinese ? "多张图片或一个散帧目录；只读取顶层图片，并按文件名数字排序。" : "Multiple images or one frame folder; reads top-level images in numeric filename order.";
                            if (row.Mode == 2) return chinese ? "严格选择一对同名 PNG + JSON；不提供图集目录扫描。" : "Exactly one matching PNG + JSON pair; atlas folders are not scanned.";
                            return chinese ? "一个视频；保存时按下方时间范围和 FPS 转为图集。" : "One video; converted to an atlas at save time using the range and FPS below.";
                        }
                        async Task RefreshRowAsync()
                        {
                            var json = row.Input?.Metadata == null ? new Windows.Data.Json.JsonObject() : await CustomSequencePackService.ReadJsonFileAsync(row.Input.Metadata);
                            int fps = row.Input?.Fps ?? CustomSequenceFormat.ClampFps(CustomSequencePackService.Number(json, "fps", 30));
                            double hold = row.Input?.Hold ?? CustomSequenceFormat.ClampHold(CustomSequencePackService.Number(json, "hold_seconds", row.Input?.Frames?.Count == 1 ? 1 : 0));
                            row.Fps.Text = fps.ToString(CultureInfo.InvariantCulture); row.Hold.Text = hold.ToString("0.###", CultureInfo.InvariantCulture);
                            description.Text = row.Input?.Description ?? EmptyModeDescription();
                            timing.Visibility = row.Input == null ? Visibility.Collapsed : Visibility.Visible;
                            RefreshMode();
                        }
                        async Task SelectAsync(Func<Task<CustomSequenceInput>> pick)
                        {
                            if (busy) return;
                            SetBusy(true);
                            var previous = row.Input;
                            try
                            {
                                var input = await pick();
                                if (input != null) { row.Input = input; await RefreshRowAsync(); status.Text = ""; }
                            }
                            catch (Exception ex) { row.Input = previous; status.Text = ex.Message; }
                            finally { SetBusy(false); }
                        }
                        for (int mode = 0; mode < modeButtons.Length; mode++)
                        {
                            int selectedMode = mode;
                            modeButtons[mode].Click += async (s, e) =>
                            {
                                if (switchingMode || busy || row.Mode == selectedMode) { RefreshMode(); return; }
                                row.Mode = selectedMode; row.Input = null; await RefreshRowAsync();
                            };
                        }
                        choose.Click += async (s, e) => await SelectAsync(async () =>
                        {
                            var picker = new FileOpenPicker();
                            if (row.Mode == 3)
                            {
                                foreach (string extension in CustomSequencePackService.VideoExtensions) picker.FileTypeFilter.Add(extension);
                                var video = await picker.PickSingleFileAsync();
                                return video == null ? null : new CustomSequenceInput { Slot = slot, Video = video, Fps = 30, Hold = 0, VideoStart = 0, VideoEnd = 5, Description = video.Name + (chinese ? " · 视频将在保存时解析" : " · decoded when saved") };
                            }
                            if (row.Mode == 2)
                            {
                                picker.FileTypeFilter.Add(".png");
                                picker.FileTypeFilter.Add(".json");
                            }
                            else foreach (string extension in CustomSequencePackService.ImageExtensions) picker.FileTypeFilter.Add(extension);
                            if (row.Mode == 0)
                            {
                                var file = await picker.PickSingleFileAsync();
                                return file == null ? null : new CustomSequenceInput { Slot = slot, Frames = new[] { file }, Description = file.Name + (chinese ? " · 单张图片" : " · single image") };
                            }
                            var files = await picker.PickMultipleFilesAsync();
                            if (files.Count == 0) return null;
                            if (row.Mode == 1) return CustomSequencePackService.CreateLooseFramesInput(slot, files);
                            return await CustomSequencePackService.CreateAtlasInputAsync(slot, files);
                        });
                        folder.Click += async (s, e) => await SelectAsync(async () =>
                        {
                            var picker = new FolderPicker(); picker.FileTypeFilter.Add("*");
                            var selected = await picker.PickSingleFolderAsync();
                            if (selected == null) return null;
                            return CustomSequencePackService.CreateLooseFramesInput(slot, await selected.GetFilesAsync(), true);
                        });
                        clear.Click += async (s, e) => { if (!busy) { row.Input = null; await RefreshRowAsync(); } };
                        await RefreshRowAsync();
                        (suffix == "" ? slots : headshotSlots).Children.Add(borderedCard);
                    }
                slots.Children.Add(showHeadshots); slots.Children.Add(headshotSlots);
                void UpdateHeadshots() => headshotSlots.Visibility = showHeadshots.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
                showHeadshots.Checked += (s, e) => UpdateHeadshots(); showHeadshots.Unchecked += (s, e) => UpdateHeadshots(); UpdateHeadshots();
                layout.Children.Add(new ScrollViewer { Content = slotHost, MaxHeight = 470, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
                layout.Children.Add(status);
                dialog.Closing += (s, e) => { if (busy) e.Cancel = true; };
                dialog.PrimaryButtonClick += async (s, e) =>
                {
                    var deferral = e.GetDeferral();
                    SetBusy(true);
                    try
                    {
                        if (string.IsNullOrWhiteSpace(name.Text)) throw new InvalidDataException(chinese ? "请填写素材包名称。" : "Enter a pack name.");
                        foreach (var row in rows.Where(r => r.Input != null))
                        {
                            if (!int.TryParse(row.Fps.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int fps) || fps < 1 || fps > 60
                                || !double.TryParse(row.Hold.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double hold) || double.IsNaN(hold) || double.IsInfinity(hold) || hold < 0 || hold > 10)
                                throw new InvalidDataException(row.Slot + (chinese ? "：FPS 须为 1～60 整数，停留须为 0～10 秒（小数点用 .）。" : ": FPS must be 1–60; hold must be 0–10 seconds (use a decimal point)."));
                            var json = row.Input.Metadata == null ? new Windows.Data.Json.JsonObject() : await CustomSequencePackService.ReadJsonFileAsync(row.Input.Metadata);
                            row.Input.Fps = row.Input.Sheet != null && fps == CustomSequenceFormat.ClampFps(CustomSequencePackService.Number(json, "fps", 30)) ? (int?)null : fps;
                            row.Input.Hold = row.Input.Sheet != null && hold == CustomSequenceFormat.ClampHold(CustomSequencePackService.Number(json, "hold_seconds", 0)) ? (double?)null : hold;
                            if (row.Input.Video != null)
                            {
                                if (!double.TryParse(row.Start.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double start)
                                    || !double.TryParse(row.End.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double end)
                                    || double.IsNaN(start) || double.IsInfinity(start) || double.IsNaN(end) || double.IsInfinity(end)
                                    || start < 0 || end <= start || end - start > 20 || (end - start) * fps > 600)
                                    throw new InvalidDataException(row.Slot + (chinese ? "：视频截取须在 20 秒、600 帧以内。请缩短区间或降低 FPS。" : ": video extraction must stay within 20 seconds and 600 frames. Shorten the range or lower FPS."));
                                row.Input.VideoStart = start; row.Input.VideoEnd = end;
                            }
                        }
                        notes.Clear();
                        await CustomSequencePackService.SavePackAsync(name.Text, rows.Where(r => r.Input != null).Select(r => r.Input),
                            original, key, new Progress<string>(text => status.Text = text), notes);
                    }
                    catch (Exception ex) { e.Cancel = true; status.Text = ex.Message; }
                    finally { SetBusy(false); deferral.Complete(); }
                };
                var result = await dialog.ShowAsync();
                if (result == ContentDialogResult.Primary && notes.Count > 0) await ShowMessageAsync(title, string.Join("\n", notes.Distinct()));
            }
            catch (Exception ex) { await ShowMessageAsync(title, ex.Message); }
        }
    }
}

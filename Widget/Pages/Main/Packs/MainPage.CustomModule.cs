using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using KillConfirmGameBar.Services;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.Web.Http;

namespace KillConfirmGameBar
{
    public sealed partial class MainPage
    {
        private sealed class CustomSequenceRow
        {
            public string Slot;
            public CustomSequenceInput Input;
            public TextBox Fps, Hold;
            public int Mode;
        }

        private sealed class CustomVideoPreviewInfo
        {
            public StorageFolder StagingFolder;
            public StorageFile PreviewFile;
            public double DurationSeconds;
            public double SourceFps;
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

        private static async Task<StorageFile> PickCustomModuleVideoAsync(bool chinese)
        {
            var picker = new FileOpenPicker
            {
                ViewMode = PickerViewMode.Thumbnail,
                SuggestedStartLocation = PickerLocationId.VideosLibrary,
                SettingsIdentifier = "CustomModuleVideo"
            };
            foreach (string extension in CustomSequencePackService.VideoExtensions)
            {
                picker.FileTypeFilter.Add(extension);
            }

            StorageFile video;
            try
            {
                video = await picker.PickSingleFileAsync();
            }
            catch (Exception ex)
            {
                App.Log("Custom module video picker failed (0x"
                    + ex.HResult.ToString("X8", CultureInfo.InvariantCulture)
                    + "): " + ex);
                throw new InvalidOperationException(
                    (chinese ? "Windows 视频文件选择器启动失败" : "Windows video file picker failed")
                    + " (0x" + ex.HResult.ToString("X8", CultureInfo.InvariantCulture) + ").",
                    ex);
            }

            if (video == null)
            {
                return null;
            }

            if (!CustomSequencePackService.VideoExtensions.Contains(
                    video.FileType,
                    StringComparer.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    (chinese ? "不支持此视频格式：" : "Unsupported video format: ")
                    + video.FileType
                    + (chinese
                        ? "。支持 MP4、MOV、M4V、WEBM、MKV、AVI、WMV 和 GIF。"
                        : ". Supported: MP4, MOV, M4V, WEBM, MKV, AVI, WMV, and GIF."));
            }

            ulong size = (await video.GetBasicPropertiesAsync()).Size;
            if (size == 0 || size > 512UL * 1024UL * 1024UL)
            {
                throw new InvalidDataException(chinese
                    ? "视频必须有效且不超过 512 MB。"
                    : "The video must be valid and no larger than 512 MB.");
            }

            return video;
        }

        private static async Task<CustomVideoPreviewInfo> PrepareCustomVideoPreviewAsync(StorageFile video)
        {
            StorageFolder root = await ApplicationData.Current.LocalFolder.CreateFolderAsync(
                "CustomVideoImport", CreationCollisionOption.OpenIfExists);
            StorageFolder staging = await root.CreateFolderAsync(
                "Preview_" + Guid.NewGuid().ToString("N"), CreationCollisionOption.FailIfExists);
            try
            {
                StorageFile source = await video.CopyAsync(staging, "source" + video.FileType);
                string previewPath = Path.Combine(staging.Path, "preview.mp4");
                var request = new Windows.Data.Json.JsonObject
                {
                    ["source_path"] = Windows.Data.Json.JsonValue.CreateStringValue(source.Path),
                    ["preview_path"] = Windows.Data.Json.JsonValue.CreateStringValue(previewPath)
                };
                using (HttpClient client = await LocalServiceAuth.CreateHttpClientAsync())
                using (var content = new HttpStringContent(
                    request.Stringify(), UnicodeEncoding.Utf8, "application/json"))
                using (HttpResponseMessage response = await client.PostAsync(
                    LocalServiceEndpoints.Build("/video/preview"), content))
                {
                    string body = await response.Content.ReadAsStringAsync();
                    if (!response.IsSuccessStatusCode
                        || !Windows.Data.Json.JsonObject.TryParse(body, out Windows.Data.Json.JsonObject result))
                    {
                        throw new InvalidDataException(string.IsNullOrWhiteSpace(body)
                            ? "Video preview failed / 视频预览生成失败。"
                            : body);
                    }
                    return new CustomVideoPreviewInfo
                    {
                        StagingFolder = staging,
                        PreviewFile = await staging.GetFileAsync("preview.mp4"),
                        DurationSeconds = result.GetNamedNumber("duration_seconds", 0),
                        SourceFps = result.GetNamedNumber("source_fps", 30)
                    };
                }
            }
            catch
            {
                try { await staging.DeleteAsync(StorageDeleteOption.PermanentDelete); } catch { }
                throw;
            }
        }

        private async Task<CustomSequenceInput> ShowCustomVideoClipEditorAsync(
            string slot, StorageFile video, bool chinese)
        {
            CustomVideoPreviewInfo info = await PrepareCustomVideoPreviewAsync(video);
            IRandomAccessStream previewStream = null;
            var completion = new TaskCompletionSource<CustomSequenceInput>();
            var media = new MediaElement
            {
                Height = 300,
                Stretch = Stretch.Uniform,
                AutoPlay = false,
                AreTransportControlsEnabled = true,
                PosterSource = new BitmapImage()
            };
            double duration = Math.Max(0.01, info.DurationSeconds);
            int sourceFps = CustomSequenceFormat.ClampFps(Math.Round(info.SourceFps));
            double initialEnd = Math.Min(duration, 20.0);
            var seek = new Slider { Minimum = 0, Maximum = duration, StepFrequency = 0.01 };
            var startSlider = new Slider { Minimum = 0, Maximum = duration, Value = 0, StepFrequency = 0.01 };
            var endSlider = new Slider { Minimum = 0, Maximum = duration, Value = initialEnd, StepFrequency = 0.01 };
            var startBox = new TextBox { Header = chinese ? "起点（秒）" : "Start (seconds)", Text = "0", Width = 150 };
            var endBox = new TextBox { Header = chinese ? "终点（秒）" : "End (seconds)", Text = initialEnd.ToString("0.###", CultureInfo.InvariantCulture), Width = 150 };
            var fpsBox = new TextBox { Header = "FPS (1–120)", Text = sourceFps.ToString(CultureInfo.InvariantCulture), Width = 130 };
            var rangeText = new TextBlock { FontSize = 12, Foreground = new SolidColorBrush(Color.FromArgb(255, 86, 91, 104)) };
            var warning = new TextBlock { FontSize = 12, Foreground = new SolidColorBrush(Color.FromArgb(255, 194, 93, 0)), TextWrapping = TextWrapping.Wrap };
            var loop = new CheckBox { Content = chinese ? "循环预览所选区间" : "Loop selected range", IsChecked = true };
            bool internalSeek = false;
            bool previewingRange = false;

            bool TryReadValues(out double start, out double end, out int fps)
            {
                start = 0;
                end = 0;
                fps = 0;
                bool parsedStart = double.TryParse(startBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out start);
                bool parsedEnd = double.TryParse(endBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out end);
                bool parsedFps = int.TryParse(fpsBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out fps);
                bool valid = parsedStart && parsedEnd && parsedFps
                    && start >= 0 && end > start && end <= duration + 0.01
                    && end - start <= 20.0 && fps >= 1 && fps <= 120
                    && (end - start) * fps <= CustomSequenceFormat.MaxFrames;
                return valid;
            }

            void UpdateSummary()
            {
                if (!double.TryParse(startBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double start)
                    || !double.TryParse(endBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double end)
                    || !int.TryParse(fpsBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int fps))
                {
                    rangeText.Text = chinese ? "请输入有效数字。" : "Enter valid numeric values.";
                    return;
                }
                int frames = end > start && fps > 0 ? (int)Math.Ceiling((end - start) * fps) : 0;
                rangeText.Text = chinese
                    ? $"源视频：{duration:0.###} 秒 / {info.SourceFps:0.##} FPS；所选约 {frames} 帧"
                    : $"Source: {duration:0.###} s / {info.SourceFps:0.##} FPS; selection about {frames} frames";
                if (fps > 60)
                {
                    warning.Text = chinese
                        ? "高帧率会增加图集体积、内存与转换耗时；运行时跟不上时会跳帧，但总时长不会变慢。"
                        : "High FPS increases atlas size, memory use, and conversion time. Runtime playback may skip frames while preserving duration.";
                }
                else if (frames > CustomSequenceFormat.MaxFrames)
                {
                    warning.Text = chinese ? "超过 600 帧，请缩短区间或降低 FPS。" : "Over 600 frames; shorten the range or lower FPS.";
                }
                else
                {
                    warning.Text = string.Empty;
                }
            }

            void SyncBoxesFromSliders()
            {
                if (startSlider.Value >= endSlider.Value)
                {
                    endSlider.Value = Math.Min(duration, startSlider.Value + 0.01);
                    if (startSlider.Value >= endSlider.Value)
                        startSlider.Value = Math.Max(0, endSlider.Value - 0.01);
                }
                startBox.Text = startSlider.Value.ToString("0.###", CultureInfo.InvariantCulture);
                endBox.Text = endSlider.Value.ToString("0.###", CultureInfo.InvariantCulture);
                UpdateSummary();
            }

            startSlider.ValueChanged += (_, __) => SyncBoxesFromSliders();
            endSlider.ValueChanged += (_, __) => SyncBoxesFromSliders();
            startBox.LostFocus += (_, __) =>
            {
                if (double.TryParse(startBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
                    startSlider.Value = Math.Max(0, Math.Min(duration, value));
                SyncBoxesFromSliders();
            };
            endBox.LostFocus += (_, __) =>
            {
                if (double.TryParse(endBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
                    endSlider.Value = Math.Max(0, Math.Min(duration, value));
                SyncBoxesFromSliders();
            };
            fpsBox.TextChanged += (_, __) => UpdateSummary();
            seek.ValueChanged += (_, __) =>
            {
                if (!internalSeek) media.Position = TimeSpan.FromSeconds(seek.Value);
            };

            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
            timer.Tick += (_, __) =>
            {
                internalSeek = true;
                seek.Value = Math.Max(0, Math.Min(duration, media.Position.TotalSeconds));
                internalSeek = false;
                if (previewingRange
                    && double.TryParse(endBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double end)
                    && media.Position.TotalSeconds >= end - 0.02)
                {
                    if (loop.IsChecked == true
                        && double.TryParse(startBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double start))
                    {
                        media.Position = TimeSpan.FromSeconds(start);
                        media.Play();
                    }
                    else
                    {
                        media.Pause();
                        previewingRange = false;
                    }
                }
            };

            var previewButton = new Button
            {
                Content = chinese ? "预览所选区间" : "Preview selection",
                Padding = new Thickness(14, 7, 14, 7),
                CornerRadius = new CornerRadius(14)
            };
            previewButton.Click += (_, __) =>
            {
                if (!TryReadValues(out double start, out double ignoredEnd, out int ignoredFps)) { UpdateSummary(); return; }
                previewingRange = true;
                media.Position = TimeSpan.FromSeconds(start);
                media.Play();
            };
            var presets = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5, VerticalAlignment = VerticalAlignment.Bottom };
            foreach (int fps in new[] { 24, 30, 60, 90, 120 })
            {
                var button = new Button { Content = fps.ToString(CultureInfo.InvariantCulture), MinWidth = 44, Padding = new Thickness(7, 5, 7, 5), CornerRadius = new CornerRadius(12) };
                button.Click += (_, __) => fpsBox.Text = fps.ToString(CultureInfo.InvariantCulture);
                presets.Children.Add(button);
            }

            var fields = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            fields.Children.Add(startBox); fields.Children.Add(endBox); fields.Children.Add(fpsBox); fields.Children.Add(presets);
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
            var cancel = new Button { Content = LocalizationManager.Text("Cancel"), Padding = new Thickness(16, 7, 16, 7), CornerRadius = new CornerRadius(14) };
            var confirm = new Button { Content = chinese ? "使用此片段" : "Use clip", Padding = new Thickness(16, 7, 16, 7), CornerRadius = new CornerRadius(14), Background = new SolidColorBrush(GameThemePalette.Current.Accent), Foreground = new SolidColorBrush(Colors.White) };
            buttons.Children.Add(cancel); buttons.Children.Add(confirm);

            var layout = new StackPanel { Spacing = 10 };
            layout.Children.Add(new TextBlock { Text = chinese ? "视频 / GIF 片段编辑" : "Video / GIF clip editor", FontSize = 19, FontWeight = Windows.UI.Text.FontWeights.SemiBold });
            layout.Children.Add(new TextBlock { Text = video.Name + (chinese ? " · 可拖动播放器查看整段素材" : " · use the player to inspect the full source"), FontSize = 12, TextWrapping = TextWrapping.Wrap });
            layout.Children.Add(media);
            layout.Children.Add(seek);
            layout.Children.Add(new TextBlock { Text = chinese ? "起点" : "Start", FontSize = 11 });
            layout.Children.Add(startSlider);
            layout.Children.Add(new TextBlock { Text = chinese ? "终点" : "End", FontSize = 11 });
            layout.Children.Add(endSlider);
            layout.Children.Add(fields);
            layout.Children.Add(rangeText);
            layout.Children.Add(warning);
            var previewRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
            previewRow.Children.Add(previewButton); previewRow.Children.Add(loop);
            layout.Children.Add(previewRow);
            layout.Children.Add(buttons);

            double width = Math.Min(780, Math.Max(620, Window.Current.Bounds.Width - 80));
            var popup = new Popup { IsLightDismissEnabled = false };
            popup.Child = new Border
            {
                Width = width,
                MaxHeight = Math.Max(420, Window.Current.Bounds.Height - 50),
                Padding = new Thickness(18),
                Background = new SolidColorBrush(Color.FromArgb(255, 250, 250, 247)),
                BorderBrush = new SolidColorBrush(GameThemePalette.Current.AccentSoft),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(20),
                Child = new ScrollViewer { Content = layout, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }
            };
            popup.HorizontalOffset = Math.Max(20, (Window.Current.Bounds.Width - width) / 2);
            popup.VerticalOffset = 20;

            cancel.Click += (_, __) => { popup.IsOpen = false; completion.TrySetResult(null); };
            confirm.Click += (_, __) =>
            {
                if (!TryReadValues(out double start, out double end, out int fps))
                {
                    warning.Text = chinese
                        ? "请确保起止点有效、片段不超过 20 秒、FPS 为 1～120，并且总帧数不超过 600。"
                        : "Use a valid range up to 20 seconds, FPS 1–120, and no more than 600 frames.";
                    return;
                }
                popup.IsOpen = false;
                completion.TrySetResult(new CustomSequenceInput
                {
                    Slot = slot,
                    Video = video,
                    Fps = fps,
                    Hold = 0,
                    VideoStart = start,
                    VideoEnd = end,
                    Description = video.Name + $" · {start:0.###}–{end:0.###}s · {fps} FPS"
                });
            };

            try
            {
                previewStream = await info.PreviewFile.OpenAsync(FileAccessMode.Read);
                media.SetSource(previewStream, "video/mp4");
                timer.Start();
                UpdateSummary();
                popup.IsOpen = true;
                return await completion.Task;
            }
            finally
            {
                timer.Stop();
                media.Stop();
                media.Source = null;
                previewStream?.Dispose();
                try { await info.StagingFolder.DeleteAsync(StorageDeleteOption.PermanentDelete); } catch { }
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
                    StorageFile file = _providedPackZipFile;
                    if (file == null)
                    {
                        var picker = new FileOpenPicker(); picker.FileTypeFilter.Add(".zip");
                        file = await picker.PickSingleFileAsync();
                    }
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
                StorageFile headImageFile = original == null
                    ? null
                    : await TryGetCustomPackHeadImageAsync(original.Path);
                layout.Children.Add(await CreateHeadImageCardAsync(
                    "ms-appx:///Assets/GameStyles/custommodule/iconpacks/custommodule/pack_head.webp",
                    headImageFile,
                    file => headImageFile = file,
                    () => headImageFile = null,
                    allowTga: false));
                var status = new TextBlock
                {
                    Text = string.Join("\n", notes),
                    TextWrapping = TextWrapping.Wrap,
                    FontSize = 12,
                    Foreground = new SolidColorBrush(Color.FromArgb(255, 86, 91, 104))
                };
                var statusCard = new Border
                {
                    Padding = new Thickness(10, 8, 10, 8),
                    CornerRadius = new CornerRadius(10),
                    Background = new SolidColorBrush(Color.FromArgb(255, 245, 247, 250)),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(255, 220, 224, 232)),
                    BorderThickness = new Thickness(1),
                    Child = status,
                    Visibility = string.IsNullOrWhiteSpace(status.Text) ? Visibility.Collapsed : Visibility.Visible
                };
                void SetStatus(string message, bool isError = false)
                {
                    status.Text = message ?? string.Empty;
                    status.Foreground = new SolidColorBrush(isError
                        ? Color.FromArgb(255, 157, 38, 28)
                        : Color.FromArgb(255, 86, 91, 104));
                    statusCard.Background = new SolidColorBrush(isError
                        ? Color.FromArgb(255, 255, 238, 235)
                        : Color.FromArgb(255, 245, 247, 250));
                    statusCard.BorderBrush = new SolidColorBrush(isError
                        ? Color.FromArgb(255, 224, 128, 118)
                        : Color.FromArgb(255, 220, 224, 232));
                    statusCard.Visibility = string.IsNullOrWhiteSpace(status.Text)
                        ? Visibility.Collapsed
                        : Visibility.Visible;
                }
                layout.Children.Add(statusCard);
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
                        row.Fps = new TextBox { Header = "FPS (1–120)", Width = 130 };
                        row.Hold = new TextBox { Header = chinese ? "末帧停留（秒）" : "Last-frame hold (s)", Width = 165 };
                        var timing = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
                        timing.Children.Add(row.Fps); timing.Children.Add(row.Hold); card.Children.Add(timing);
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
                        }
                        string EmptyModeDescription()
                        {
                            if (row.Mode == 0) return chinese ? "单个静态图片；不会查找同目录文件。" : "One static image; sibling files are not inspected.";
                            if (row.Mode == 1) return chinese ? "多张图片或一个散帧目录；只读取顶层图片，并按文件名数字排序。" : "Multiple images or one frame folder; reads top-level images in numeric filename order.";
                            if (row.Mode == 2) return chinese ? "严格选择一对同名 PNG + JSON；不提供图集目录扫描。" : "Exactly one matching PNG + JSON pair; atlas folders are not scanned.";
                            return chinese
                                ? "支持 MP4/MOV/M4V/WEBM/MKV/AVI/WMV/GIF；选择后会打开片段编辑窗口。"
                                : "MP4/MOV/M4V/WEBM/MKV/AVI/WMV/GIF; choosing one opens the clip editor.";
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
                                if (input != null) { row.Input = input; await RefreshRowAsync(); SetStatus(""); }
                            }
                            catch (Exception ex)
                            {
                                row.Input = previous;
                                App.Log("Custom module material selection failed for slot " + slot
                                    + ", mode " + row.Mode
                                    + " (0x" + ex.HResult.ToString("X8", CultureInfo.InvariantCulture)
                                    + "): " + ex);
                                string message = string.IsNullOrWhiteSpace(ex.Message)
                                    ? (chinese ? "选择素材失败" : "Material selection failed")
                                    : ex.Message;
                                if (message.IndexOf("0x", StringComparison.OrdinalIgnoreCase) < 0)
                                {
                                    message += " (0x"
                                        + ex.HResult.ToString("X8", CultureInfo.InvariantCulture) + ")";
                                }
                                SetStatus(message, true);
                            }
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
                            if (row.Mode == 3)
                            {
                                var video = await PickCustomModuleVideoAsync(chinese);
                                if (video == null) return null;
                                SetStatus(chinese ? "正在读取视频并生成预览…" : "Reading video and preparing preview…");
                                return await ShowCustomVideoClipEditorAsync(slot, video, chinese);
                            }
                            var picker = new FileOpenPicker();
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
                            if (!int.TryParse(row.Fps.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int fps) || fps < 1 || fps > 120
                                || !double.TryParse(row.Hold.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double hold) || double.IsNaN(hold) || double.IsInfinity(hold) || hold < 0 || hold > 10)
                                throw new InvalidDataException(row.Slot + (chinese ? "：FPS 须为 1～120 整数，停留须为 0～10 秒（小数点用 .）。" : ": FPS must be 1–120; hold must be 0–10 seconds (use a decimal point)."));
                            var json = row.Input.Metadata == null ? new Windows.Data.Json.JsonObject() : await CustomSequencePackService.ReadJsonFileAsync(row.Input.Metadata);
                            row.Input.Fps = row.Input.Sheet != null && fps == CustomSequenceFormat.ClampFps(CustomSequencePackService.Number(json, "fps", 30)) ? (int?)null : fps;
                            row.Input.Hold = row.Input.Sheet != null && hold == CustomSequenceFormat.ClampHold(CustomSequencePackService.Number(json, "hold_seconds", 0)) ? (double?)null : hold;
                            if (row.Input.Video != null)
                            {
                                double start = row.Input.VideoStart;
                                double end = row.Input.VideoEnd;
                                if (double.IsNaN(start) || double.IsInfinity(start) || double.IsNaN(end) || double.IsInfinity(end)
                                    || start < 0 || end <= start || end - start > 20 || (end - start) * fps > 600)
                                    throw new InvalidDataException(row.Slot + (chinese ? "：视频截取须在 20 秒、600 帧以内。请缩短区间或降低 FPS。" : ": video extraction must stay within 20 seconds and 600 frames. Shorten the range or lower FPS."));
                            }
                        }
                        notes.Clear();
                        await CustomSequencePackService.SavePackAsync(name.Text, rows.Where(r => r.Input != null).Select(r => r.Input),
                            original, existing != null && !existing.IsBuiltIn ? key : null,
                            new Progress<string>(text => SetStatus(text)), notes,
                            headImageFile: headImageFile,
                            preserveOriginalHeadImage: false);
                    }
                    catch (Exception ex)
                    {
                        e.Cancel = true;
                        App.Log("Custom module pack save failed: " + ex);
                        SetStatus(ex.Message, true);
                    }
                    finally { SetBusy(false); deferral.Complete(); }
                };
                var result = await dialog.ShowAsync();
                if (result == ContentDialogResult.Primary && notes.Count > 0) await ShowMessageAsync(title, string.Join("\n", notes.Distinct()));
            }
            catch (Exception ex) { await ShowMessageAsync(title, ex.Message); }
        }
    }
}

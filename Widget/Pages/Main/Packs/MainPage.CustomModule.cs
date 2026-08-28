using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using KillConfirmGameBar.Services;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace KillConfirmGameBar
{
    public sealed partial class MainPage
    {
        private sealed class CustomSequenceRow
        {
            public string Slot;
            public CustomSequenceInput Input;
            public TextBox Fps, Hold;
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
                    chinese ? "选择素材或目录，自动识别散帧与同名 PNG/JSON 图集。每个击杀等级一组，无需改名。这里只编辑素材，测试和位置设置沿用现有入口。"
                        : "Choose files or a folder; matching PNG/JSON atlases and frame sequences are detected automatically. One source per kill level. Tests and positioning stay in their existing locations.",
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
                        var card = new StackPanel { Spacing = 6 };
                        card.Children.Add(new TextBlock { Text = chinese ? level + " 杀" + (suffix == "" ? "" : " · 爆头") : "Kill " + level + (suffix == "" ? "" : " · Headshot"), FontWeight = Windows.UI.Text.FontWeights.SemiBold });
                        var description = new TextBlock { TextWrapping = TextWrapping.Wrap, FontSize = 12 };
                        card.Children.Add(description);
                        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
                        var images = new Button { Content = chinese ? "选择素材" : "Choose source" };
                        var folder = new Button { Content = chinese ? "素材目录" : "Source folder" };
                        var clear = new Button { Content = chinese ? "清除" : "Clear" };
                        foreach (var button in new[] { images, folder, clear })
                        { button.Padding = new Thickness(8, 4, 8, 4); button.FontSize = 12; buttons.Children.Add(button); }
                        card.Children.Add(buttons);
                        row.Fps = new TextBox { Header = "FPS (1–60)", Width = 130 };
                        row.Hold = new TextBox { Header = chinese ? "末帧停留（秒）" : "Last-frame hold (s)", Width = 165 };
                        var timing = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
                        timing.Children.Add(row.Fps); timing.Children.Add(row.Hold); card.Children.Add(timing);
                        async Task RefreshRowAsync()
                        {
                            var json = row.Input?.Metadata == null ? new Windows.Data.Json.JsonObject() : await CustomSequencePackService.ReadJsonFileAsync(row.Input.Metadata);
                            int fps = row.Input?.Fps ?? CustomSequenceFormat.ClampFps(CustomSequencePackService.Number(json, "fps", 30));
                            double hold = row.Input?.Hold ?? CustomSequenceFormat.ClampHold(CustomSequencePackService.Number(json, "hold_seconds", row.Input?.Frames?.Count == 1 ? 1 : 0));
                            row.Fps.Text = fps.ToString(CultureInfo.InvariantCulture); row.Hold.Text = hold.ToString("0.###", CultureInfo.InvariantCulture);
                            description.Text = row.Input?.Description ?? (chinese ? "未设置" : "Not assigned");
                            timing.Visibility = row.Input == null ? Visibility.Collapsed : Visibility.Visible;
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
                        images.Click += async (s, e) => await SelectAsync(async () =>
                        {
                            var picker = new FileOpenPicker();
                            foreach (string extension in CustomSequencePackService.ImageExtensions) picker.FileTypeFilter.Add(extension);
                            picker.FileTypeFilter.Add(".json");
                            var files = await picker.PickMultipleFilesAsync();
                            return files.Count == 0 ? null : await CustomSequencePackService.ProbeInputAsync(slot, files);
                        });
                        folder.Click += async (s, e) => await SelectAsync(async () =>
                        {
                            var picker = new FolderPicker(); picker.FileTypeFilter.Add("*");
                            var selected = await picker.PickSingleFolderAsync();
                            if (selected == null) return null;
                            return await CustomSequencePackService.ProbeInputAsync(slot, await selected.GetFilesAsync(), selected);
                        });
                        clear.Click += async (s, e) => { if (!busy) { row.Input = null; await RefreshRowAsync(); } };
                        await RefreshRowAsync();
                        (suffix == "" ? slots : headshotSlots).Children.Add(card);
                    }
                slots.Children.Add(showHeadshots); slots.Children.Add(headshotSlots);
                void UpdateHeadshots() => headshotSlots.Visibility = showHeadshots.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
                showHeadshots.Checked += (s, e) => UpdateHeadshots(); showHeadshots.Unchecked += (s, e) => UpdateHeadshots(); UpdateHeadshots();
                layout.Children.Add(new ScrollViewer { Content = slotHost, MaxHeight = 370, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
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

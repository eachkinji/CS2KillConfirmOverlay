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
using Windows.UI.Xaml.Media;

namespace KillConfirmGameBar.Controls.GameStyles
{
    public sealed class CustomModulePanel : UserControl
    {
        private readonly StackPanel _root = new StackPanel { Spacing = 10 };
        private readonly TextBlock _title = new TextBlock { FontSize = 15, FontWeight = Windows.UI.Text.FontWeights.SemiBold };
        private readonly TextBlock _help = new TextBlock { TextWrapping = TextWrapping.Wrap, FontSize = 12 };
        private readonly TextBlock _status = new TextBlock { TextWrapping = TextWrapping.Wrap, FontSize = 12 };
        private readonly ComboBox _packs = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };
        private readonly ComboBox _level = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };
        private readonly CheckBox _previewHeadshot = new CheckBox();
        private readonly Slider _fps = new Slider { Minimum = 0, Maximum = 60, StepFrequency = 1 };
        private readonly Slider _hold = new Slider { Minimum = -1, Maximum = 10, StepFrequency = 0.1 };
        private readonly Slider _scale = new Slider { Minimum = 0.1, Maximum = 4, StepFrequency = 0.01 };
        private readonly Slider _x = new Slider { Minimum = -2000, Maximum = 2000, StepFrequency = 1 };
        private readonly Slider _y = new Slider { Minimum = -2000, Maximum = 2000, StepFrequency = 1 };
        private readonly ComboBox _placement = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };
        private readonly ToggleSwitch _fade = new ToggleSwitch();
        private readonly ToggleSwitch _headshots = new ToggleSwitch();
        private readonly Button _zip = new Button(), _folder = new Button(), _export = new Button();
        private readonly Button _delete = new Button(), _play = new Button(), _stop = new Button(), _reset = new Button();
        private readonly KillConfirmAnimation _preview = new KillConfirmAnimation();
        private readonly StreakWindowEditor _streak = new StreakWindowEditor();
        private bool _chinese = true, _suppress, _busy, _loaded;
        private int _refreshToken;
        public bool AllowDelete { set { _delete.Visibility = value ? Visibility.Visible : Visibility.Collapsed; } }
        public event SelectionChangedEventHandler StreakModeSelectionChanged;
        public event EventHandler PlacementChanged;
        public string GetSelectedStreakMode(string fallback) => _streak.GetValue(fallback);
        public void SelectStreakMode(string value) { _suppress = true; _streak.SelectValue(value); _suppress = false; }
        private string SelectedKey => (_packs.SelectedItem as IconPackItem)?.Key;

        public CustomModulePanel()
        {
            Content = _root;
            _root.Children.Add(_title); _root.Children.Add(_help); _root.Children.Add(_packs);
            AddButtons(_zip, _folder); AddButtons(_export, _delete);
            var previewBorder = new Border
            {
                Height = 150, CornerRadius = new CornerRadius(8), Background = new SolidColorBrush(Color.FromArgb(255, 32, 36, 44)),
                Child = new Viewbox { Stretch = Stretch.Uniform, Child = _preview }
            };
            _root.Children.Add(previewBorder);
            for (int level = 1; level <= 5; level++) _level.Items.Add(level);
            _level.SelectedIndex = 0;
            _root.Children.Add(_level); _root.Children.Add(_previewHeadshot);
            AddButtons(_play, _stop);
            _root.Children.Add(_streak);
            _root.Children.Add(_fps); _root.Children.Add(_hold); _root.Children.Add(_fade); _root.Children.Add(_headshots);
            foreach (string placement in new[] { "Bottom", "Center", "Top", "Manual" }) _placement.Items.Add(new ComboBoxItem { Tag = placement, Content = placement });
            _root.Children.Add(_placement); _root.Children.Add(_scale); _root.Children.Add(_x); _root.Children.Add(_y);
            _root.Children.Add(_reset); _root.Children.Add(_status);
            _packs.DisplayMemberPath = "DisplayName";
            _packs.SelectionChanged += OnPackChanged;
            _fps.ValueChanged += (s, e) => SaveSettings();
            _hold.ValueChanged += (s, e) => SaveSettings();
            _fade.Toggled += (s, e) => SaveSettings();
            _headshots.Toggled += (s, e) => SaveSettings();
            _placement.SelectionChanged += (s, e) => SavePlacement();
            _scale.ValueChanged += (s, e) => SavePlacement();
            _x.ValueChanged += (s, e) => SavePlacement();
            _y.ValueChanged += (s, e) => SavePlacement();
            _preview.CustomSequenceStatusChanged += (s, text) => _status.Text = text;
            _streak.SettingsChanged += (s, e) =>
            {
                if (!_suppress)
                {
                    SharedStreakSettingsStore.Save(GameStyleMode.CustomModule, _streak.GetValue(SharedStreakSettingsStore.LifeMode));
                    StreakModeSelectionChanged?.Invoke(this, e);
                }
            };
            _zip.Click += async (s, e) => await RunAsync(async () =>
            {
                var picker = new FileOpenPicker(); picker.FileTypeFilter.Add(".zip");
                StorageFile file = await picker.PickSingleFileAsync();
                if (file != null) await SelectImportedAsync(await CustomSequencePackService.ImportZipAsync(file, Progress()));
            });
            _folder.Click += async (s, e) => await RunAsync(async () =>
            {
                var picker = new FolderPicker(); picker.FileTypeFilter.Add("*");
                StorageFolder folder = await picker.PickSingleFolderAsync();
                if (folder != null) await SelectImportedAsync(await CustomSequencePackService.ImportFolderAsync(folder, Progress()));
            });
            _export.Click += async (s, e) => await RunAsync(async () =>
            {
                string key = SelectedKey;
                if (key == null) return;
                var picker = new FileSavePicker { SuggestedFileName = (_packs.SelectedItem as IconPackItem).DisplayName };
                picker.FileTypeChoices.Add("CS2 Customizer ZIP", new[] { ".zip" });
                StorageFile output = await picker.PickSaveFileAsync();
                if (output != null) await CustomSequencePackService.ExportAsync(key, output);
            });
            _delete.Click += async (s, e) => await RunAsync(async () =>
            {
                string key = SelectedKey;
                if (key == null) return;
                var confirm = new ContentDialog
                {
                    Title = _chinese ? "删除自定义素材包？" : "Delete custom pack?",
                    Content = (_packs.SelectedItem as IconPackItem).DisplayName,
                    PrimaryButtonText = _chinese ? "删除" : "Delete",
                    CloseButtonText = _chinese ? "取消" : "Cancel", DefaultButton = ContentDialogButton.Close
                };
                if (await confirm.ShowAsync() != ContentDialogResult.Primary) return;
                _preview.StopCustomSequence();
                await PackCatalogService.RemoveCustomIconPackAsync(key);
                ApplicationData.Current.LocalSettings.Values["KillIconPack.custommodule"] = "custommodule";
                await RefreshPacksAsync();
            });
            _play.Click += (s, e) =>
            {
                if (SelectedKey == null) { _status.Text = EmptyMessage; return; }
                _preview.PlayCustomKill(_level.SelectedIndex + 1, _previewHeadshot.IsChecked == true, SelectedKey);
            };
            _stop.Click += (s, e) => _preview.StopCustomSequence();
            _reset.Click += (s, e) => { CustomModuleSettingsStore.Save(new CustomModuleSettings()); RefreshSettings(); };
            Loaded += async (s, e) =>
            {
                _loaded = true;
                PackCatalogService.CatalogChanged += OnCatalogChanged;
                CustomModuleSettingsStore.Changed += OnSettingsChanged;
                RefreshSettings(); await RefreshPacksAsync();
            };
            Unloaded += (s, e) =>
            {
                _loaded = false; _refreshToken++;
                PackCatalogService.CatalogChanged -= OnCatalogChanged;
                CustomModuleSettingsStore.Changed -= OnSettingsChanged;
                _preview.ReleaseCustomSequenceResources();
            };
            ApplyLanguage(true);
        }

        private void AddButtons(params Button[] buttons)
        {
            var row = new Grid { ColumnSpacing = 8 };
            for (int i = 0; i < buttons.Length; i++)
            {
                row.ColumnDefinitions.Add(new ColumnDefinition());
                buttons[i].HorizontalAlignment = HorizontalAlignment.Stretch;
                buttons[i].MinWidth = 0;
                Grid.SetColumn(buttons[i], i); row.Children.Add(buttons[i]);
            }
            _root.Children.Add(row);
        }

        private string EmptyMessage => _chinese ? "尚无素材：导入 CS2 Customizer ZIP 或 1～5 杀素材目录。" : "No materials. Import a CS2 Customizer ZIP or a 1–5 level folder.";
        private IProgress<string> Progress() => new Progress<string>(text => _status.Text = text);
        private async Task RunAsync(Func<Task> action)
        {
            if (_busy) return;
            _busy = true; SetButtonsEnabled(false);
            _status.Text = _chinese ? "处理中…" : "Working…";
            try { await action(); _status.Text = _chinese ? "完成" : "Done"; }
            catch (Exception ex) { _status.Text = ex.Message; App.Log("Custom module: " + ex); }
            finally { _busy = false; SetButtonsEnabled(true); }
        }

        private void SetButtonsEnabled(bool enabled)
        {
            foreach (Button button in new[] { _zip, _folder, _export, _delete }) button.IsEnabled = enabled;
        }

        private async Task SelectImportedAsync(IconPackItem pack)
        {
            ApplicationData.Current.LocalSettings.Values["KillIconPack.custommodule"] = pack.Key;
            PackCatalogService.NotifyCustomSequenceSelectionChanged();
            await RefreshPacksAsync();
        }

        private async void OnCatalogChanged(object sender, EventArgs e)
        {
            if (!_loaded) return;
            try
            {
                await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
                { if (_loaded) await RefreshPacksAsync(); });
            }
            catch (Exception ex) { App.Log("Custom module refresh: " + ex.Message); }
        }

        private async Task RefreshPacksAsync()
        {
            int token = ++_refreshToken;
            try
            {
                var packs = (await PackCatalogService.GetAllIconPacksAsync()).Where(p => p.Key.StartsWith("custom_module_icon_", StringComparison.OrdinalIgnoreCase)).ToList();
                if (token != _refreshToken) return;
                string saved = ApplicationData.Current.LocalSettings.Values["KillIconPack.custommodule"] as string;
                _suppress = true;
                _packs.ItemsSource = packs;
                _packs.SelectedItem = packs.FirstOrDefault(p => p.Key == saved);
                _suppress = false;
                if (packs.Count == 0) _status.Text = EmptyMessage;
                else if (_packs.SelectedItem == null) _packs.SelectedItem = packs[0];
                else await ShowPackInfoAsync();
            }
            catch (Exception ex) { _suppress = false; _status.Text = ex.Message; }
        }

        private async void OnPackChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppress || SelectedKey == null) return;
            _preview.StopCustomSequence();
            ApplicationData.Current.LocalSettings.Values["KillIconPack.custommodule"] = SelectedKey;
            PackCatalogService.NotifyCustomSequenceSelectionChanged();
            await ShowPackInfoAsync();
        }

        private async Task ShowPackInfoAsync()
        {
            string key = SelectedKey;
            if (key == null) return;
            try
            {
                StorageFolder folder = await PackCatalogService.GetImportedIconFolderAsync(key);
                if (folder == null) return;
                var lines = new List<string>();
                foreach (int level in Enumerable.Range(1, 5))
                    foreach (string suffix in new[] { "", "hs" })
                        if (await folder.TryGetItemAsync(level + suffix + ".png") != null)
                        {
                            var m = await CustomSequencePackService.ReadMetadataAsync(folder, level + suffix);
                            lines.Add($"{level}{suffix}: {m.Frames} frames · {m.Fps} FPS · {m.Frames / (double)m.Fps + m.HoldSeconds:0.00}s");
                        }
                if (SelectedKey == key && !_busy) _status.Text = string.Join("\n", lines);
            }
            catch (Exception ex) { _status.Text = ex.Message; }
        }

        private void OnSettingsChanged(object sender, EventArgs e) { if (!_suppress) RefreshSettings(); }
        private void RefreshSettings()
        {
            _suppress = true;
            var s = CustomModuleSettingsStore.Load();
            _fps.Value = s.Fps; _hold.Value = s.Hold; _fade.IsOn = s.Fade; _headshots.IsOn = s.Headshots;
            _streak.SelectValue(SharedStreakSettingsStore.Load(GameStyleMode.CustomModule));
            var values = ApplicationData.Current.LocalSettings.Values;
            _scale.Value = values["AnimationScale.CustomModule"] is double scale ? scale : 1;
            _x.Value = values["AnimationHorizontalOffset.CustomModule"] is double x ? x : 0;
            _y.Value = values["AnimationOffset.CustomModule"] is double y ? y : 0;
            string placement = values["AnimationPlacement.CustomModule"] as string ?? "Bottom";
            _placement.SelectedItem = _placement.Items.Cast<ComboBoxItem>().FirstOrDefault(i => (string)i.Tag == placement);
            _suppress = false; UpdateSliderLabels();
        }

        private void SavePlacement()
        {
            if (_suppress) return;
            var values = ApplicationData.Current.LocalSettings.Values;
            values["AnimationScale.CustomModule"] = _scale.Value;
            values["AnimationHorizontalOffset.CustomModule"] = _x.Value;
            values["AnimationOffset.CustomModule"] = _y.Value;
            values["AnimationPlacement.CustomModule"] = (_placement.SelectedItem as ComboBoxItem)?.Tag as string ?? "Bottom";
            UpdateSliderLabels();
            PlacementChanged?.Invoke(this, EventArgs.Empty);
        }
        private void SaveSettings()
        {
            if (_suppress) return;
            _suppress = true;
            CustomModuleSettingsStore.Save(new CustomModuleSettings
            { Fps = (int)_fps.Value, Hold = _hold.Value, Fade = _fade.IsOn, Headshots = _headshots.IsOn });
            _suppress = false; UpdateSliderLabels();
        }
        private void UpdateSliderLabels()
        {
            _fps.Header = (_chinese ? "帧率：" : "FPS: ") + (_fps.Value == 0 ? (_chinese ? "跟随素材" : "From material") : _fps.Value.ToString("0"));
            _hold.Header = (_chinese ? "末帧停留：" : "Last-frame hold: ") + (_hold.Value < 0 ? (_chinese ? "跟随素材" : "From material") : _hold.Value.ToString("0.0") + "s");
            _scale.Header = (_chinese ? "图标缩放：" : "Icon scale: ") + _scale.Value.ToString("0.00") + "×";
            _x.Header = (_chinese ? "水平偏移：" : "Horizontal offset: ") + _x.Value.ToString("0") + " px";
            _y.Header = (_chinese ? "垂直偏移：" : "Vertical offset: ") + _y.Value.ToString("0") + " px";
        }

        public void ApplyLanguage(bool chinese)
        {
            _chinese = chinese;
            _title.Text = chinese ? "自定义模块 · 序列帧" : "Custom Module · Frame sequences";
            _help.Text = chinese ? "兼容 CS2 Customizer 图集、JSON 和旧版逐帧目录。新击杀替换当前动画。预览自动适应画框；位置和缩放应用于游戏中的图标。" : "CS2 Customizer sheets, JSON and legacy frame folders. A new kill replaces playback. Preview fits its box; position and scale apply in game.";
            _packs.Header = chinese ? "素材包" : "Material pack";
            _zip.Content = chinese ? "导入 ZIP" : "Import ZIP";
            _folder.Content = chinese ? "导入目录" : "Import folder";
            _export.Content = chinese ? "导出兼容 ZIP" : "Export ZIP";
            _delete.Content = chinese ? "删除素材包" : "Delete pack";
            _play.Content = chinese ? "播放预览" : "Preview";
            _stop.Content = chinese ? "停止" : "Stop";
            _reset.Content = chinese ? "恢复播放默认设置" : "Reset playback";
            _level.Header = chinese ? "预览击杀等级" : "Preview kill level";
            _previewHeadshot.Content = chinese ? "预览爆头" : "Preview headshot";
            _fade.Header = chinese ? "淡入淡出（0.12 / 0.25 秒）" : "Fade in/out (0.12 / 0.25s)";
            _headshots.Header = chinese ? "优先使用爆头变体，缺失时用普通图标" : "Use headshot variant, fall back to normal";
            _placement.Header = chinese ? "图标位置" : "Icon position";
            string[] positions = chinese ? new[] { "底部", "居中", "顶部", "手动" } : new[] { "Bottom", "Center", "Top", "Manual" };
            for (int i = 0; i < positions.Length; i++) ((ComboBoxItem)_placement.Items[i]).Content = positions[i];
            _streak.ApplyLanguage(chinese); RefreshSettings();
        }

        internal void ApplyTheme(GameThemePalette theme)
        {
            _title.Foreground = theme.Brush(theme.Text);
            _help.Foreground = theme.Brush(theme.MutedText); _status.Foreground = theme.Brush(theme.MutedText);
            foreach (Control control in new Control[] { _packs, _level, _previewHeadshot, _fps, _hold, _scale, _x, _y, _placement, _fade, _headshots, _zip, _folder, _export, _delete, _play, _stop, _reset })
                control.Foreground = theme.Brush(theme.Text);
            _streak.ApplyTheme(theme);
        }
    }
}

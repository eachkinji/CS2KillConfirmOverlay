using KillConfirmCompatibility.Contracts;
using KillConfirmCompatibility.Danmaku;
using KillConfirmCompatibility.Desktop.Runtime;
using KillConfirmCompatibility.Services;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.Web.Http;

namespace KillConfirmCompatibility.Desktop.UI
{
    public partial class DesktopControlPanel : Window
    {
        private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
        private readonly ServiceSession _service = new();
        private HostController _host;
        private System.Threading.Mutex _hostGate;
        private GameBarAvailability _availability;
        private bool _loading = true, _switching, _closing, _polling;
        private string _page = "home", _lastStyle;
        private bool? _lastDisplayMode;
        private readonly TaskCompletionSource<bool> _ready = new();
        internal Task Ready => _ready.Task;
        internal string ConfigurationPath => Path.Combine(DesktopStorage.Current.LocalFolder.Path, DisplayFiles.FolderName, DisplayFiles.ConfigurationName);
        internal string StatusPath => Path.Combine(Path.GetDirectoryName(ConfigurationPath), DisplayFiles.StatusName);
        private DisplayConfiguration Configuration => DisplayFiles.Read<DisplayConfiguration>(ConfigurationPath) ?? new DisplayConfiguration();
        private IDictionary<string, object> Settings => DesktopStorage.Current.LocalSettings.Values;
        internal HostController ActiveHost => _host;
        internal DesktopControlPanel(GameBarAvailability availability = null)
        {
            _availability = availability;
            InitializeComponent();
            Loaded += async (s, e) => { try { await InitializeAsync(); _ready.TrySetResult(true); } catch (Exception error) { ShowError(error); _ready.TrySetException(error); } };
            Closing += OnClosing;
            _timer.Tick += async (s, e) => { if (_polling || _closing) return; _polling = true; try { await RefreshAsync(); } catch (Exception error) { ShowError(error); } finally { _polling = false; } };
        }
        internal async Task InitializeAsync()
        {
            _availability ??= await Task.Run(DesktopDeployment.CheckGameBar);
            if (!File.Exists(ConfigurationPath)) DisplayFiles.Update(ConfigurationPath, c => c.Enabled = !_availability.Available);
            if (Settings[GameStyleService.SettingKey] == null) GameStyleService.Current = GameStyleMode.Valorant;
            await PortSettingsStore.SavePortAsync(PortSettingsStore.CurrentPort);
            Navigation.Items.Add(new ListBoxItem { Content = "⌂  主页", Tag = "home", Padding = new Thickness(10) });
            Navigation.Items.Add(new ListBoxItem { Content = "⚙  高级设置", Tag = "advanced", Padding = new Thickness(10) });
            foreach (GameStyleMode game in Enum.GetValues(typeof(GameStyleMode))) Navigation.Items.Add(new ListBoxItem { Content = GameStyleService.ToDisplayName(game), Tag = GameStyleService.ToStorageValue(game), Padding = new Thickness(10) });
            VersionText.Text = "版本 " + typeof(DesktopControlPanel).Assembly.GetName().Version;
            Screens.ItemsSource = new[] { new ScreenChoice { Key = "", Label = "自动（主显示器）" } }.Concat(Windowing.NativeWindows.Screens().Select(s => new ScreenChoice { Key = s.Device, Label = s.Device })).ToArray();
            var config = Configuration; FollowGame.IsChecked = config.FollowGame; HideInactive.IsChecked = config.HideWhenInactive; FrameRate.SelectedIndex = config.FramesPerSecond == 30 ? 1 : 0; Screens.SelectedValue = config.ScreenName;
            CompatibilityMode.IsChecked = config.Enabled || !_availability.Available; GameBarMode.IsChecked = !CompatibilityMode.IsChecked;
            if (!_availability.Available && !config.Enabled) DisplayFiles.Update(ConfigurationPath, c => c.Enabled = true);
            GameBarMode.IsEnabled = _availability.Available; ModeDescription.Text = _availability.Reason;
            BuildAdvancedSettings(); Navigation.SelectedIndex = 0;
            _loading = false; await RefreshGameAsync(); ApplyPage();
            await SetModeAsync(CompatibilityMode.IsChecked == true); _timer.Start();
        }
        private async void OnNavigationChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Navigation.SelectedItem is not ListBoxItem item) return;
            _page = (string)item.Tag;
            if (_page != "home" && _page != "advanced") GameStyleService.Current = GameStyleService.FromKey(_page);
            if (!_loading) { try { await RefreshGameAsync(); ApplyPage(); } catch (Exception error) { ShowError(error); } }
        }
        private void ApplyPage()
        {
            bool advanced = _page == "advanced", home = _page == "home";
            PageTitle.Text = advanced ? "高级设置" : home ? "主页" : GameStyleService.ToDisplayName(GameStyleService.Current);
            HomeSection.Visibility = home ? Visibility.Visible : Visibility.Collapsed;
            GameBarStatusCard.Visibility = !Configuration.Enabled ? Visibility.Visible : Visibility.Collapsed;
            AdvancedSection.Visibility = advanced ? Visibility.Visible : Visibility.Collapsed;
            MaterialsSection.Visibility = !advanced && (!home || Configuration.Enabled) ? Visibility.Visible : Visibility.Collapsed;
            EditLayout.IsEnabled = Configuration.Enabled;
            ScreenLayoutTab.IsEnabled = Configuration.Enabled;
        }
        private async void OnModeChanged(object sender, RoutedEventArgs e)
        {
            if (_loading || _switching) return;
            try { await SetModeAsync(CompatibilityMode.IsChecked == true); } catch (Exception error) { ShowError(error); }
        }
        internal async Task SetModeAsync(bool compatibility)
        {
            if (!compatibility && !_availability.Available) throw new InvalidOperationException(_availability.Reason);
            _switching = true;
            try
            {
                if (_host?.IsClosed == true) { _host = null; _hostGate?.Dispose(); _hostGate = null; }
                Settings["CompatibilityDisplay.Enabled"] = true;
                DisplayFiles.Update(ConfigurationPath, c => { c.Enabled = compatibility; c.ModeRequest = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(); });
                if (compatibility)
                {
                    if (_host == null)
                    {
                        _hostGate = new System.Threading.Mutex(true, DesktopEnvironment.InstanceName("overlay"), out bool first);
                        if (first) _host = new HostController(false);
                        else { _hostGate.Dispose(); _hostGate = null; }
                    }
                }
                else
                {
                    if (_host != null) { await _host.CloseAsync(); _host = null; _hostGate?.Dispose(); _hostGate = null; }
                    var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
                    while (DateTimeOffset.UtcNow < deadline)
                    {
                        var status = DisplayFiles.Read<DisplayStatus>(StatusPath);
                        if (status == null || status.ProcessId == 0 || DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - status.Timestamp > 5000) break;
                        await Task.Delay(100);
                    }
                    var remaining = DisplayFiles.Read<DisplayStatus>(StatusPath);
                    if (remaining?.ProcessId > 0 && DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - remaining.Timestamp < 5000) throw new InvalidOperationException("兼容显示尚未关闭，请重试切换。");
                    Settings["CompatibilityDisplay.Enabled"] = false;
                }
                ApplyPage();
                _lastDisplayMode = compatibility;
            }
            finally { _switching = false; }
        }
        internal async Task RefreshGameAsync()
        {
            _loading = true;
            try
            {
                var style = GameStyleService.Current; string key = GameStyleService.ToStorageValue(style);
                await PackCatalogService.ReloadForCompatibilityAsync();
                IconPacks.ItemsSource = await PackCatalogService.GetVisibleIconPacksAsync();
                VoicePacks.ItemsSource = await PackCatalogService.GetVisibleVoicePacksAsync();
                IconPacks.SelectedValue = Settings["KillIconPack." + key] as string ?? GameStyleService.DefaultIconPackKey(style);
                VoicePacks.SelectedValue = Settings["VoicePack." + key] as string ?? GameStyleService.DefaultVoicePackKey(style);
                CurrentGameText.Text = "当前游戏风格：" + GameStyleService.ToDisplayName(style);
                ElementVisibility.Children.Clear();
                foreach (var element in new[] { ("Crosshair", "准星反馈"), ("Lower", "下方反馈"), ("Upper", "上方反馈"), ("Danmaku", "弹幕区域") })
                {
                    string name = element.Item1; var toggle = new CheckBox { Content = element.Item2, IsChecked = Configuration.GetLayout(key).GetElement(name).Visible, Margin = new Thickness(0, 8, 20, 0) };
                    toggle.Click += (s, e) => { try { DisplayFiles.Update(ConfigurationPath, c => c.GetLayout(key).GetElement(name).Visible = toggle.IsChecked == true); } catch (Exception error) { ShowError(error); } }; ElementVisibility.Children.Add(toggle);
                }
                await BuildGameSettingsAsync(); _lastStyle = key;
            }
            finally { _loading = false; }
        }
        private void OnPackChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_loading) return;
            var selector = (ComboBox)sender;
            if (selector.SelectedValue is string value)
                TryAction(() => { Settings[(selector == IconPacks ? "KillIconPack." : "VoicePack.") + GameStyleService.ToStorageValue(GameStyleService.Current)] = value; DisplayFiles.Update(ConfigurationPath, c => c.AssetRevision = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()); });
        }
        private async void OnImportZip(object sender, RoutedEventArgs e)
        {
            var picker = new OpenFileDialog { Filter = "资源包 (*.zip)|*.zip" };
            if (picker.ShowDialog(this) == true) await ImportAsync(picker.FileName, (string)((Button)sender).Tag == "voice");
        }
        private async void OnImportFolder(object sender, RoutedEventArgs e)
        {
            var picker = new OpenFolderDialog { Title = "选择完整的素材包文件夹" };
            if (picker.ShowDialog(this) == true) await ImportAsync(picker.FolderName, (string)((Button)sender).Tag == "voice");
        }
        private async Task ImportAsync(string path, bool voice)
        {
            try { ActionFeedback.Text = "正在导入素材…"; await DesktopPackImport.ImportAsync(path, GameStyleService.Current, voice); await RefreshGameAsync(); DisplayFiles.Update(ConfigurationPath, c => c.AssetRevision = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()); ActionFeedback.Text = "素材导入完成。"; }
            catch (Exception error) { ShowError(error); }
        }
        internal async Task PreviewAsync(string preset, bool audio)
        {
            PreviewFeedback.Text = "正在准备测试…";
            if (!Configuration.Enabled)
            {
                using var client = await LocalServiceAuth.CreateHttpClientAsync(); using var response = await client.GetAsync(PreviewEvents.UriFor(PreviewEvents.Create(preset), audio)); response.EnsureSuccessStatusCode();
                PreviewFeedback.Text = "已发送 Game Bar 测试，请打开并固定小组件。"; return;
            }
            long request = 0;
            DisplayFiles.Update(ConfigurationPath, c => { c.TestPreset = preset; c.TestAudio = audio; c.TestRequest = request = Math.Max(c.TestRequest + 1, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()); });
            await WaitForTestAsync(request, false); PreviewFeedback.Text = "测试画面已显示。";
        }
        private async void OnPreview(object sender, RoutedEventArgs e) { try { await PreviewAsync((string)((ComboBoxItem)TestPreset.SelectedItem).Tag, TestAudio.IsChecked == true); } catch (Exception error) { PreviewFeedback.Text = error.Message; } }
        internal async Task TestDanmakuAsync(string key)
        {
            if (!Configuration.Enabled) throw new InvalidOperationException("请在 Game Bar 小组件中测试弹幕，或切换到兼容显示模式。");
            if (!DanmakuSettingsStore.IsEnabled) throw new InvalidOperationException("请先开启游戏事件弹幕。");
            long request = 0; DisplayFiles.Update(ConfigurationPath, c => { c.DanmakuTestEvent = key; c.DanmakuTestRequest = request = Math.Max(c.DanmakuTestRequest + 1, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()); });
            await WaitForTestAsync(request, true); ActionFeedback.Text = "测试弹幕已显示。";
        }
        private async void OnDanmakuTest(object sender, RoutedEventArgs e) { try { await TestDanmakuAsync((string)((Button)sender).Tag); } catch (Exception error) { ShowError(error); } }
        private async Task WaitForTestAsync(long request, bool danmaku)
        {
            var deadline = DateTimeOffset.UtcNow.AddSeconds(35);
            while (Configuration.Enabled && DateTimeOffset.UtcNow < deadline)
            {
                var status = DisplayFiles.Read<DisplayStatus>(StatusPath);
                if (status != null && (danmaku ? status.LastDanmakuTestRequest : status.LastTestRequest) == request)
                { string error = danmaku ? status.DanmakuTestError : status.TestError; if (error != null) throw new InvalidOperationException(error); return; }
                await Task.Delay(100);
            }
            throw new InvalidOperationException("测试尚未完成，请检查显示模式、素材和元素显隐。");
        }
        private void OnEditLayout(object sender, RoutedEventArgs e) => TryAction(() => DisplayFiles.Update(ConfigurationPath, c => c.EditRequest = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));
        private void OnResetLayout(object sender, RoutedEventArgs e) => TryAction(() => DisplayFiles.Update(ConfigurationPath, c => c.Layouts.Remove(GameStyleService.ToStorageValue(GameStyleService.Current))));
        private void OnScreenChanged(object sender, RoutedEventArgs e)
        {
            if (!_loading) TryAction(() => DisplayFiles.Update(ConfigurationPath, c => { c.FollowGame = FollowGame.IsChecked == true; c.HideWhenInactive = HideInactive.IsChecked == true; c.ScreenName = Screens.SelectedValue as string ?? ""; c.FramesPerSecond = FrameRate.SelectedIndex == 1 ? 30 : 60; }));
        }
        private async Task RefreshAsync()
        {
            if (_switching) return;
            ProfileMirror.Synchronize();
            bool compatibility = Configuration.Enabled;
            if (_lastDisplayMode != compatibility || (_host?.IsClosed == true && compatibility))
            {
                _loading = true; CompatibilityMode.IsChecked = compatibility; GameBarMode.IsChecked = !compatibility; _loading = false;
                await SetModeAsync(compatibility);
            }
            if (_host == null) await _service.EnsureRegisteredAsync();
            var status = DisplayFiles.Read<DisplayStatus>(StatusPath);
            RuntimeStatus.Text = Configuration.Enabled
                ? "兼容显示：" + (status?.ProcessId > 0 ? "运行中" : "正在连接") + "\n游戏事件连接：" + (status?.Connected == true ? "已连接" : "连接中") + "\n显示器：" + (status?.Screen ?? "自动") + "   帧率：" + Configuration.FramesPerSecond + " FPS\n素材：" + (status?.Loading == true ? "加载中" : "已就绪") + (status?.Error == null ? "" : "\n" + status.Error)
                : "当前使用 Game Bar。请在小组件内测试画面，并将窗口固定。";
            var widget = DisplayFiles.Read<WidgetStatusSnapshot>(Path.Combine(DesktopEnvironment.WidgetDataRoot, DisplayFiles.FolderName, "widget-status.json"));
            bool fresh = widget?.Active == true && DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - widget.Timestamp < 5000;
            GameBarStatusText.Text = !fresh ? "小组件：未检测，请按 Win + G 打开。\n固定窗口：等待状态\n单击浏览：等待状态" : "小组件：运行中\n固定窗口：" + (widget.Pinned ? "已固定" : "请点击图钉") + "\n单击浏览：" + (widget.ClickThrough ? "已关闭" : "需要关闭");
            if (_lastStyle != GameStyleService.ToStorageValue(GameStyleService.Current)) await RefreshGameAsync();
        }
        private async void OnReconnect(object sender, RoutedEventArgs e) { try { await _service.EnsureRegisteredAsync(); await SetModeAsync(Configuration.Enabled); ActionFeedback.Text = "已重试连接。"; } catch (Exception error) { ShowError(error); } }
        private async void OnRepairGsi(object sender, RoutedEventArgs e)
        {
            try { using var client = await LocalServiceAuth.CreateHttpClientAsync(); using var content = new HttpStringContent("{}", UnicodeEncoding.Utf8, "application/json"); using var response = await client.PostAsync(LocalServiceEndpoints.Build("/counter-strike/cfg?version=" + GsiGameVersionSettingsStore.Load()), content); response.EnsureSuccessStatusCode(); ActionFeedback.Text = "游戏配置已安装，请进入游戏测试。"; }
            catch (Exception error) { ShowError(error); }
        }
        private void OnOpenLogs(object sender, RoutedEventArgs e) => TryAction(() => Process.Start(new ProcessStartInfo("explorer.exe", "\"" + DesktopStorage.Current.LocalFolder.Path + "\"") { UseShellExecute = true }));
        private void OnOpenGameBar(object sender, RoutedEventArgs e) => TryAction(DesktopDeployment.OpenGameBar);
        private void OnRetryGameBar(object sender, RoutedEventArgs e) => TryAction(DesktopDeployment.RetryGameBar);
        private async void OnRefreshAvailability(object sender, RoutedEventArgs e) { _availability = await Task.Run(DesktopDeployment.CheckGameBar); GameBarMode.IsEnabled = _availability.Available; ModeDescription.Text = _availability.Reason; }
        private async void OnSavePort(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(PortInput.Text, out int port) || port < 1024 || port > 65535) { ActionFeedback.Text = "请输入 1024–65535 的端口。"; return; }
            try { await PortSettingsStore.SavePortAsync(port); ActionFeedback.Text = "端口已保存，请安装 / 修复游戏配置。"; } catch (Exception error) { ShowError(error); }
        }
        private async void OnClosing(object sender, CancelEventArgs e)
        {
            if (_closing) return; e.Cancel = true; _closing = true; _timer.Stop();
            try { if (_host != null) await _host.CloseAsync(); await _service.ReleaseAsync(); }
            finally { _hostGate?.Dispose(); System.Windows.Application.Current.Shutdown(); }
        }
        private void TryAction(Action action) { try { action(); } catch (Exception error) { ShowError(error); } }
        private void ShowError(Exception error) { ActionFeedback.Text = error.GetBaseException().Message; App.Log("Desktop control panel: " + error); }
        private sealed class ScreenChoice { public string Key { get; set; } public string Label { get; set; } }
    }
}

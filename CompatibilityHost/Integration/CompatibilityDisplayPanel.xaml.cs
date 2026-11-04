using System;
using System.Linq;
using System.Threading.Tasks;
using KillConfirmGameBar.Services;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Shapes;

namespace KillConfirmGameBar.Features.CompatibilityDisplay
{
    public sealed partial class CompatibilityDisplayPanel : UserControl
    {
        private bool _loading = true, _switching;
        private bool _previewing => HomeView.IsPreviewing;
        private string _previewNotice;
        private DateTimeOffset _previewNoticeUntil;
        private string _screenSignature;
        private GameThemePalette _theme;
        internal Action OpenGameSettings { get; set; }
        internal Action OpenAboutVersion { get; set; }
        internal Action<GameStyleMode, bool> OpenPackLibrary { get; set; }
        private readonly DispatcherTimer _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        public CompatibilityDisplayPanel()
        {
            InitializeComponent();
            foreach (GameStyleMode style in Enum.GetValues(typeof(GameStyleMode)))
                MainGameSelector.Items.Add(new ComboBoxItem { Content = MainPage.CreateMaterialGameSelectorContent(style, GameStyleService.ToDisplayName(style)), Tag = GameStyleService.ToStorageValue(style) });
            HomeView.OpenGameSettings = () => OpenGameSettings?.Invoke();
            HomeView.OpenPackLibrary = (style, voice) => OpenPackLibrary?.Invoke(style, voice);
            HomeView.PreviewChanged += () => {
                _previewNotice = HomeView.PreviewFeedback;
                _previewNoticeUntil = DateTimeOffset.UtcNow.AddSeconds(8);
                RefreshStatus();
            };
            LayoutContentSource.Content = null;
            HomeView.SetLayoutContent(DisplayView);
            Loaded += OnLoaded; Unloaded += OnUnloaded;
            _statusTimer.Tick += async (s,e) => {
                RefreshStatus();
                if (!_switching && CompatibilityDisplayRuntime.Load().Enabled && !CompatibilityDisplayRuntime.IsRunning(CompatibilityDisplayRuntime.ReadStatus()))
                    await CompatibilityDisplayRuntime.EnsureStartedAsync();
            };
            _loading = false;
        }
        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            GameStyleService.Changed += OnPanelGameChanged;
            VersionUpdateService.Changed += OnUpdateStateChanged;
            OnUpdateStateChanged(null, EventArgs.Empty);
            _ = VersionUpdateService.CheckAsync();
            _loading = true;
            var c = CompatibilityDisplayRuntime.Load();
            CompatibilityMode.IsChecked = c.Enabled; GameBarMode.IsChecked = !c.Enabled;
            FollowToggle.IsOn = c.FollowGame; HideToggle.IsOn = c.HideWhenInactive;
            _loading = false; ApplyLanguage(); ApplyTheme(GameThemePalette.Home); RefreshStatus();
            if (!CompatibilityHomeView.IsUiValidation) _statusTimer.Start();
        }
        private void OnUnloaded(object sender, RoutedEventArgs e) { _statusTimer.Stop(); GameStyleService.Changed -= OnPanelGameChanged; VersionUpdateService.Changed -= OnUpdateStateChanged; }
        private void OnUpdateStatusClick(object sender, RoutedEventArgs e) => OpenAboutVersion?.Invoke();
        private void OnUpdateStateChanged(object sender, EventArgs e) => UpdateStatusText.Text = VersionUpdateService.StatusText(LocalizationManager.Current == UiLanguage.SimplifiedChinese);
        private void OnPanelGameChanged(object sender, GameStyleMode style) { ApplyLanguage(); RefreshStatus(); }
        private async void OnMainGameChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_loading || !(MainGameSelector.SelectedItem is ComboBoxItem item) || !(item.Tag is string key)) return;
            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () => GameStyleService.Current = GameStyleService.FromKey(key));
        }
        private async void OnModeChanged(object sender, RoutedEventArgs e)
        {
            if (_loading || _switching) return;
            _switching = true; CompatibilityMode.IsEnabled = GameBarMode.IsEnabled = false;
            try
            {
                bool compatibility = CompatibilityMode.IsChecked == true;
                bool changed = await CompatibilityDisplayRuntime.SetModeAsync(compatibility);
                RefreshStatus(true);
                if (!changed) StatusText.Text = LocalizationManager.Current == UiLanguage.SimplifiedChinese
                    ? (compatibility ? "无法启动兼容显示，请点击素材与测试中的重试服务。" : "兼容显示未确认关闭，Game Bar 仍暂停。请重试切换。")
                    : (compatibility ? "Desktop display could not start. Retry the service under Packs & testing." : "Desktop display has not confirmed shutdown. Game Bar remains paused. Retry switching modes.");
            }
            catch (Exception error) { StatusText.Text = error.Message; App.Log("Display mode switch: " + error); }
            finally { _switching = false; CompatibilityMode.IsEnabled = GameBarMode.IsEnabled = true; ApplyModeSelection(CompatibilityDisplayRuntime.Load().Enabled); }
        }
        private void OnGeneralChanged(object sender, RoutedEventArgs e)
        {
            if (!_loading) CompatibilityDisplayRuntime.Update(c => { c.FollowGame = FollowToggle.IsOn; c.HideWhenInactive = HideToggle.IsOn; });
        }
        private void OnScreenChanged(object sender, SelectionChangedEventArgs e) { if (!_loading) CompatibilityDisplayRuntime.Update(c => c.ScreenName = (ScreenSelector.SelectedItem as ComboBoxItem)?.Tag as string ?? ""); }
        private async void OnEditClick(object sender, RoutedEventArgs e) => await RequestAsync(true);
        private async void OnHomePreviewClick(object sender, RoutedEventArgs e) => await HomeView.PreviewAsync();
        private void OnGameBarDetailsClick(object sender, RoutedEventArgs e)
        {
            bool expanded = GameBarDetails.Visibility != Visibility.Visible;
            GameBarDetails.Visibility = expanded ? Visibility.Visible : Visibility.Collapsed;
            GameBarDetailsChevron.Glyph = expanded ? "\uE70E" : "\uE70D";
        }
        private async Task RequestAsync(bool edit)
        {
            if (!CompatibilityDisplayRuntime.Load().Enabled) return;
            CompatibilityDisplayRuntime.Update(c => { if (edit) c.EditRequest = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(); else { c.TestPreset = "three"; c.TestAudio = false; c.TestRequest = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(); } });
            if (!await CompatibilityDisplayRuntime.EnsureStartedAsync()) StatusText.Text = "无法启动兼容显示";
        }
        private void OnResetClick(object sender, RoutedEventArgs e) => CompatibilityDisplayRuntime.Update(c => c.Layouts.Remove(GameStyleService.ToStorageValue(GameStyleService.Current)));
        private void RefreshStatus(bool force = false)
        {
            if (_switching && !force) return;
            bool zh = LocalizationManager.Current == UiLanguage.SimplifiedChinese;
            var config = CompatibilityDisplayRuntime.Load(); var status = CompatibilityDisplayRuntime.ReadStatus();
            ApplyModeSelection(config.Enabled);
            if (!config.Enabled) RefreshGameBarStatus();
            string problem = config.Enabled && CompatibilityDisplayRuntime.IsRunning(status) ? status.Error : null;
            StatusText.Text = _previewing ? (zh ? "正在预览…" : "Previewing…") : DateTimeOffset.UtcNow < _previewNoticeUntil ? _previewNotice :
                !config.Enabled ? (zh ? "Game Bar 已选择" : "Game Bar selected") : !string.IsNullOrWhiteSpace(problem) ? (zh ? "需要处理" : "Needs attention") :
                status?.Editing == true ? (zh ? "编辑布局中" : "Editing layout") : !CompatibilityDisplayRuntime.IsRunning(status) ? (zh ? "正在启动…" : "Starting…") :
                status.Loading ? (zh ? "加载素材中…" : "Loading packs…") : (zh ? "已就绪" : "Ready");
            HomePreviewButton.IsEnabled = config.Enabled && !_switching && !_previewing && HomeView.CanPreview;
            EditButton.IsEnabled = ResetButton.IsEnabled = config.Enabled;
            bool runningNow = config.Enabled && CompatibilityDisplayRuntime.IsRunning(status);
            bool editing = runningNow && status?.Editing == true;
            bool visible = runningNow && status?.Visible == true;
            bool hasProblem = !string.IsNullOrWhiteSpace(problem);
            var gsi = GsiStatusMonitor.Instance.CurrentSnapshot;
            bool serviceOnline = gsi.ServiceReachable || (runningNow && status.Connected);
            UpdateStatusLights(config.Enabled, runningNow, hasProblem, serviceOnline, gsi.IsGreen, HomeView.PacksReady);
            ToolTipService.SetToolTip(StatusSvcLight, serviceOnline ? (zh ? "后台服务已连接" : "Service connected") : (zh ? "后台服务等待连接" : "Service connecting"));
            ToolTipService.SetToolTip(StatusCfgLight, !config.Enabled ? (zh ? "Game Bar 模式" : "Game Bar mode") : runningNow && !hasProblem ? (zh ? "显示端已就绪" : "Display ready") : hasProblem ? problem : (zh ? "显示端启动中" : "Display starting"));
            ToolTipService.SetToolTip(StatusGsiLight, gsi.IsGreen ? (zh ? "正在接收游戏数据" : "Receiving game data") : (zh ? "等待游戏数据" : "Waiting for game data"));
            ToolTipService.SetToolTip(StatusAniLight, HomeView.PacksReady ? (zh ? "素材已加载" : "Packs loaded") : (zh ? "素材加载中" : "Loading packs"));
            SetStatusLight(VisibilityLight, visible ? StatusGreen : StatusGray);
            VisibilityText.Text = zh ? (visible ? "显示中" : "待机") : (visible ? "Visible" : "Idle");
            if (config.Enabled) HomeView.RefreshStatusNow();
            var screens = status?.Screens ?? new string[0];
            string signature = string.Join("|", screens) + config.ScreenName;
            if (_screenSignature != signature)
            {
                _screenSignature = signature; _loading = true;
                ScreenSelector.Items.Clear(); ScreenSelector.Items.Add(new ComboBoxItem { Content = zh ? "自动（主显示器）" : "Automatic (primary display)", Tag = "" });
                foreach (var screen in screens) ScreenSelector.Items.Add(new ComboBoxItem { Content = screen, Tag = screen });
                ScreenSelector.SelectedItem = ScreenSelector.Items.OfType<ComboBoxItem>().FirstOrDefault(i => (string)i.Tag == config.ScreenName) ?? ScreenSelector.Items[0];
                _loading = false;
            }
        }
        public void ApplyLanguage()
        {
            OnUpdateStateChanged(null, EventArgs.Empty);
            bool zh = LocalizationManager.Current == UiLanguage.SimplifiedChinese;
            bool wasLoading = _loading; _loading = true;
            foreach (ComboBoxItem gameItem in MainGameSelector.Items)
                if (gameItem.Content is StackPanel content && gameItem.Tag is string key)
                    content.Children.OfType<TextBlock>().First().Text = GameStyleService.ToDisplayName(GameStyleService.FromKey(key));
            MainGameSelector.SelectedItem = MainGameSelector.Items.OfType<ComboBoxItem>().FirstOrDefault(item => (string)item.Tag == GameStyleService.ToStorageValue(GameStyleService.Current));
            _loading = wasLoading;
            ToolTipService.SetToolTip(MainGameSelector, zh ? "效果风格" : "Effect style");
            TitleText.Text = zh ? "显示模式" : "Display mode";
            GameBarModeTitle.Text = zh ? "Game Bar" : "Game Bar";
            GameBarModeHint.Text = zh ? "支持全屏独占使用" : "Supports exclusive fullscreen";
            CompatibilityModeTitle.Text = zh ? "兼容显示" : "Desktop display";
            CompatibilityModeHint.Text = zh ? "支持全屏窗口化使用" : "Supports borderless fullscreen";
            RecommendedText.Text = zh ? "推荐" : "Recommended";
            HomePreviewLabel.Text = zh ? "预览效果" : "Preview effects";
            GameBarDetailsLabel.Text = zh ? "状态详情" : "Status details";
            OpenGameBarLabel.Text = zh ? "打开 Game Bar" : "Open Game Bar";
            ToolTipService.SetToolTip(StatusRetryButton, zh ? "重试连接" : "Retry connection");
            ToolTipService.SetToolTip(StatusRepairButton, zh ? "修复游戏配置" : "Repair game config");
            ToolTipService.SetToolTip(StatusCopyButton, zh ? "复制诊断" : "Copy diagnostics");
            ToolTipService.SetToolTip(StatusLogsButton, zh ? "打开日志" : "Open logs");
            LayoutHint.Text = zh ? "当前风格：" + GameStyleService.ToDisplayName(GameStyleService.Current) : "Current style: " + GameStyleService.ToDisplayName(GameStyleService.Current);
            EditLabel.Text = zh ? "编辑屏幕" : "Edit screen";
            ToolTipService.SetToolTip(ResetButton, zh ? "恢复当前游戏布局" : "Reset game layout");
            ToolTipService.SetToolTip(EditButton, zh ? "编辑屏幕 · Ctrl+Alt+L 开始 / 结束 · Enter 或 Esc 保存" : "Edit screen · Ctrl+Alt+L start / finish · Enter or Esc saves");
            ScreenTitle.Text = zh ? "显示范围" : "Display area";
            FollowToggle.Header = zh ? "自动跟随游戏窗口" : "Follow game window";
            HideToggle.Header = zh ? "切出游戏时隐藏" : "Hide when game loses focus";
            ScreenSelector.Header = zh ? "显示器（预览与固定显示）" : "Monitor (preview and fixed display)";
            ApplyGameBarStatusLanguage();
            HomeView.ApplyLanguage(); ApplyModeSelection(CompatibilityDisplayRuntime.Load().Enabled);
        }
        private void ApplyModeSelection(bool compatibility)
        {
            bool wasLoading = _loading;
            _loading = true;
            CompatibilityMode.IsChecked = compatibility;
            GameBarMode.IsChecked = !compatibility;
            _loading = wasLoading;
            bool zh = LocalizationManager.Current == UiLanguage.SimplifiedChinese;
            if (_theme != null)
            {
                GameBarModeCard.Background = compatibility ? GameThemePalette.Glass(0.55) : _theme.Brush(_theme.AccentSoft);
                GameBarModeCard.BorderBrush = _theme.Brush(compatibility ? CardStrokeColor : AccentBorderColor(_theme));
                CompatibilityModeCard.Background = compatibility ? _theme.Brush(_theme.AccentSoft) : GameThemePalette.Glass(0.55);
                CompatibilityModeCard.BorderBrush = _theme.Brush(compatibility ? AccentBorderColor(_theme) : CardStrokeColor);
            }
            GameBarStatusSection.Visibility = compatibility ? Visibility.Collapsed : Visibility.Visible;
            HomeView.Visibility = compatibility ? Visibility.Visible : Visibility.Collapsed;
            StatusActions.Visibility = compatibility ? Visibility.Visible : Visibility.Collapsed;
            OpenGameBarButton.Visibility = compatibility ? Visibility.Collapsed : Visibility.Visible;
            HomePreviewButton.Visibility = compatibility ? Visibility.Visible : Visibility.Collapsed;
            ModeUsageHint.Text = compatibility
                ? (zh ? "请打开游戏使用。也可以先点击右侧按钮预览效果。" : "Open your game to use the overlay, or preview the effects first.")
                : (zh ? "请按 Win+G 打开 Game Bar，启用并固定 Kill Confirm Overlay 小组件，然后打开游戏使用。" : "Press Win+G to open Game Bar, open and pin the Kill Confirm Overlay widget, then launch your game.");
            OpenGameBarButton.IsEnabled = !_switching && !CompatibilityDisplayRuntime.IsEnabled;
        }
        private static Color CardStrokeColor => Color.FromArgb(166, 255, 255, 255);

        private static Color AccentBorderColor(GameThemePalette theme) =>
            Color.FromArgb(115, theme.Accent.R, theme.Accent.G, theme.Accent.B);

        internal void ApplyTheme(GameThemePalette theme)
        {
            // Home uses the same neutral workspace palette as Advanced settings.
            _theme = GameThemePalette.Home; theme = _theme;
            Foreground = theme.Brush(theme.Text);
            foreach (var card in new[] { ModeCard, StatusBar, ScreenCard })
            {
                card.Background = GameThemePalette.Glass(0.55);
                card.BorderBrush = theme.Brush(CardStrokeColor);
            }
            GameBarModeCard.CornerRadius = new CornerRadius(14);
            CompatibilityModeCard.CornerRadius = new CornerRadius(14);
            ModeStatusBadge.Background = theme.Brush(theme.AccentSoft);
            ModeStatusBadge.CornerRadius = new CornerRadius(14);
            StatusText.Foreground = theme.Brush(theme.Accent);
            ModeUsageHint.Foreground = theme.Brush(theme.Text);
            foreach (var text in new[] { GameBarModeHint, CompatibilityModeHint, LayoutHint }) text.Foreground = theme.Brush(theme.MutedText);
            MainGameSelector.Background = theme.Brush(theme.Field); MainGameSelector.Foreground = theme.Brush(theme.Text); MainGameSelector.BorderBrush = theme.Brush(Color.FromArgb(15, 0, 0, 0)); MainGameSelector.CornerRadius = new CornerRadius(12);
            ApplyGameBarStatusTheme(theme);
            HomeView.ApplyTheme(theme); ApplyModeSelection(CompatibilityDisplayRuntime.Load().Enabled);
        }
        private static readonly Color StatusGreen = Color.FromArgb(255, 52, 199, 89);
        private static readonly Color StatusAmber = Color.FromArgb(255, 255, 159, 10);
        private static readonly Color StatusRed = Color.FromArgb(255, 255, 59, 48);
        private static readonly Color StatusGray = Color.FromArgb(255, 142, 142, 147);
        private static void SetStatusLight(Ellipse light, Color color) { if (light != null) light.Fill = new Windows.UI.Xaml.Media.SolidColorBrush(color); }
        private static bool IsLight(Ellipse light, Color color) => light.Fill is Windows.UI.Xaml.Media.SolidColorBrush brush && brush.Color == color;
        private void UpdateStatusLights(bool enabled, bool running, bool problem, bool serviceOnline, bool gameData, bool packsReady)
        {
            SetStatusLight(StatusSvcLight, serviceOnline ? StatusGreen : StatusAmber);
            SetStatusLight(StatusCfgLight, !enabled ? StatusGray : running && !problem ? StatusGreen : problem ? StatusRed : StatusAmber);
            SetStatusLight(StatusGsiLight, gameData ? StatusGreen : StatusGray);
            SetStatusLight(StatusAniLight, packsReady ? StatusGreen : StatusAmber);
        }
        private void OnStatusRetryClick(object sender, RoutedEventArgs e) => HomeView.RequestRetry();
        private void OnStatusRepairClick(object sender, RoutedEventArgs e) => HomeView.RepairGameConfig();
        private void OnStatusCopyClick(object sender, RoutedEventArgs e) => HomeView.CopyDiagnostics();
        private void OnStatusLogsClick(object sender, RoutedEventArgs e) => HomeView.OpenRuntimeLogs();
    }
}

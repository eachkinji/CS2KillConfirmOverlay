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
        internal Action<GameStyleMode, AudioPackKind> OpenAudioPackLibrary { get; set; }
        private readonly DispatcherTimer _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        public CompatibilityDisplayPanel()
        {
            InitializeComponent();
            GameSelectorPresentation.PopulateGroupedGameSelector(MainGameSelector);
            HomeView.OpenGameSettings = () => OpenGameSettings?.Invoke();
            HomeView.OpenPackLibrary = (style, voice) => OpenPackLibrary?.Invoke(style, voice);
            HomeView.OpenAudioPackLibrary = (style, kind) => OpenAudioPackLibrary?.Invoke(style, kind);
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
            GameSourceSettingsStore.Changed += OnGameSourceChanged;
            GsiStatusMonitor.Instance.StatusUpdated += OnGsiStatusUpdated;
            VersionUpdateService.Changed += OnUpdateStateChanged;
            OnUpdateStateChanged(null, EventArgs.Empty);
            _ = VersionUpdateService.CheckAsync();
            _loading = true;
            var c = CompatibilityDisplayRuntime.Load();
            CompatibilityMode.IsChecked = c.Enabled; GameBarMode.IsChecked = !c.Enabled;
            FollowToggle.IsOn = c.FollowGame; HideToggle.IsOn = c.HideWhenInactive;
            _loading = false; ApplyLanguage(); ApplyTheme(GameThemePalette.Home); RefreshStatus();
            if (!CompatibilityHomeView.IsUiValidation)
            {
                GsiStatusMonitor.Instance.StartMonitoring();
                _ = GsiStatusMonitor.Instance.RefreshAsync();
                _statusTimer.Start();
            }
        }
        private bool? _statusStripCompact;
        private void OnStatusStripSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e.NewSize.Width <= 0) return;
            // Four status labels, four actions and a 220px selector must not
            // compete with the update label at compact desktop widths.
            bool compact = e.NewSize.Width < 900;
            if (_statusStripCompact == compact) return;
            _statusStripCompact = compact;
            StatusStripGrid.RowSpacing = compact ? 12 : 0;
            Grid.SetColumnSpan(UpdateStatusButton, compact ? 3 : 1);
            Grid.SetRow(StatusActions, compact ? 1 : 0);
            Grid.SetColumn(StatusActions, compact ? 0 : 2);
            Grid.SetRow(MainGameSelector, compact ? 1 : 0);
            Grid.SetColumn(MainGameSelector, compact ? 1 : 3);
            Grid.SetColumnSpan(MainGameSelector, compact ? 3 : 1);
            MainGameSelector.HorizontalAlignment = HorizontalAlignment.Right;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            _statusTimer.Stop();
            if (!CompatibilityHomeView.IsUiValidation) GsiStatusMonitor.Instance.StopMonitoring();
            GameStyleService.Changed -= OnPanelGameChanged;
            GameSourceSettingsStore.Changed -= OnGameSourceChanged;
            GsiStatusMonitor.Instance.StatusUpdated -= OnGsiStatusUpdated;
            VersionUpdateService.Changed -= OnUpdateStateChanged;
        }
        private void OnUpdateStatusClick(object sender, RoutedEventArgs e) => OpenAboutVersion?.Invoke();
        private void OnUpdateStateChanged(object sender, EventArgs e) => UpdateStatusText.Text = VersionUpdateService.StatusText(LocalizationManager.Current == UiLanguage.SimplifiedChinese);
        private void OnPanelGameChanged(object sender, GameStyleMode style) { ApplyLanguage(); RefreshStatus(); }
        private void OnGameSourceChanged(object sender, EventArgs e)
        {
            _ = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                ApplyLanguage();
                RefreshStatus();
            });
        }
        private void OnGsiStatusUpdated(GsiStatusSnapshot snapshot)
        {
            _ = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () => RefreshStatus());
        }
        private async void OnMainGameChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_loading || !(MainGameSelector.SelectedItem is ComboBoxItem item) || !item.IsEnabled || !(item.Tag is string key)) return;
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
            bool isLol = GameSourceSettingsStore.IsLol;
            StatusRepairButton.Visibility = isLol ? Visibility.Collapsed : Visibility.Visible;
            if (StatusCfgBadge != null) StatusCfgBadge.Visibility = isLol ? Visibility.Collapsed : Visibility.Visible;
            if (StatusCfgColumn != null) StatusCfgColumn.Width = isLol ? new GridLength(0) : new GridLength(50);
            if (StatusSvcColumn != null) StatusSvcColumn.Width = new GridLength(isLol ? 66 : 50);
            if (StatusGsiColumn != null) StatusGsiColumn.Width = new GridLength(isLol ? 66 : 50);
            if (StatusAniColumn != null) StatusAniColumn.Width = new GridLength(isLol ? 66 : 50);
            if (StatusGsiLabel != null) StatusGsiLabel.Text = isLol ? "LOL" : "GSI";
            var gsi = GsiStatusMonitor.Instance.CurrentSnapshot;
            bool serviceOnline = gsi.ServiceReachable || (runningNow && status.Connected);
            UpdateStatusLights(config.Enabled, runningNow, hasProblem, serviceOnline, gsi.IsGreen, HomeView.PacksReady);
            if (!gsi.IsGreen && gsi.ServiceReachable && gsi.Posts > 0)
            {
                SetStatusLight(StatusGsiLight, StatusAmber);
            }
            string svcTip = serviceOnline ? (zh ? "后台服务已连接" : "Service connected") : (zh ? "后台服务等待连接" : "Service connecting");
            string cfgTip = !config.Enabled ? (zh ? "Game Bar 模式" : "Game Bar mode") : runningNow && !hasProblem ? (zh ? "显示端已就绪" : "Display ready") : hasProblem ? problem : (zh ? "显示端启动中" : "Display starting");
            string gsiTip = isLol
                ? (gsi.IsGreen
                    ? (zh ? "LOL 对局已连接 · 正在以 50ms 实时采集英雄联盟对局数据" : "LOL match connected · polling Live Client Data at 50ms")
                    : (gsi.ServiceReachable && gsi.Posts > 0)
                        ? (zh ? "上一局 LOL 对局已结束，等待进入下一场对局" : "Previous LOL match ended, waiting for next match")
                        : (zh ? "等待进入 LOL 对局 · 进入英雄联盟对局或训练营后自动连接（免 CFG）" : "Waiting for LOL match · connects automatically in-game (no CFG needed)"))
                : (gsi.IsGreen
                    ? (zh ? "正在接收 CS 游戏数据 (GSI)" : "Receiving CS game data (GSI)")
                    : (zh ? "等待 CS 游戏数据 (GSI)" : "Waiting for CS game data (GSI)"));
            string aniTip = HomeView.PacksReady ? (zh ? "素材已加载" : "Packs loaded") : (zh ? "素材加载中" : "Loading packs");
            ToolTipService.SetToolTip(StatusSvcLight, svcTip);
            ToolTipService.SetToolTip(StatusSvcBadge, svcTip);
            ToolTipService.SetToolTip(StatusSvcLabel, svcTip);
            ToolTipService.SetToolTip(StatusCfgLight, cfgTip);
            ToolTipService.SetToolTip(StatusCfgBadge, cfgTip);
            ToolTipService.SetToolTip(StatusCfgLabel, cfgTip);
            ToolTipService.SetToolTip(StatusGsiLight, gsiTip);
            ToolTipService.SetToolTip(StatusGsiBadge, gsiTip);
            ToolTipService.SetToolTip(StatusGsiLabel, gsiTip);
            ToolTipService.SetToolTip(StatusAniLight, aniTip);
            ToolTipService.SetToolTip(StatusAniBadge, aniTip);
            ToolTipService.SetToolTip(StatusAniLabel, aniTip);
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
                GameSelectorPresentation.UpdateMaterialGameSelectorItemLanguage(gameItem);
            MainGameSelector.SelectedItem = MainGameSelector.Items.OfType<ComboBoxItem>().FirstOrDefault(item => (string)item.Tag == GameStyleService.ToStorageValue(GameStyleService.Current));
            _loading = wasLoading;
            ToolTipService.SetToolTip(MainGameSelector, zh ? "效果风格" : "Effect style");
            TitleText.Text = zh ? "显示模式" : "Display mode";
            GameBarModeTitle.Text = zh ? "Game Bar" : "Game Bar";
            GameBarModeHint.Text = zh ? "支持全屏独占使用" : "Supports exclusive fullscreen";
            CompatibilityModeTitle.Text = zh ? "兼容显示" : "Desktop display";
            CompatibilityModeHint.Text = zh ? "支持全屏窗口化使用" : "Supports borderless fullscreen";
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
                GameBarModeCard.Background = compatibility ? _theme.Brush(_theme.SubtleField) : _theme.Brush(_theme.AccentSoft);
                GameBarModeCard.BorderBrush = _theme.Brush(compatibility ? CardStrokeColor : AccentBorderColor(_theme));
                CompatibilityModeCard.Background = compatibility ? _theme.Brush(_theme.AccentSoft) : _theme.Brush(_theme.SubtleField);
                CompatibilityModeCard.BorderBrush = _theme.Brush(compatibility ? AccentBorderColor(_theme) : CardStrokeColor);
            }
            GameBarStatusSection.Visibility = compatibility ? Visibility.Collapsed : Visibility.Visible;
            HomeView.Visibility = compatibility ? Visibility.Visible : Visibility.Collapsed;
            StatusActions.Visibility = compatibility ? Visibility.Visible : Visibility.Collapsed;
            OpenGameBarButton.Visibility = compatibility ? Visibility.Collapsed : Visibility.Visible;
            HomePreviewButton.Visibility = compatibility ? Visibility.Visible : Visibility.Collapsed;
            bool isLol = GameSourceSettingsStore.IsLol;
            if (CurrentSourceBadgeText != null)
            {
                CurrentSourceBadgeText.Text = isLol
                    ? (zh ? "英雄联盟 LOL · Live Client 驱动" : "League of Legends · Live Client")
                    : (GameSourceSettingsStore.Current == GsiGameVersionSettingsStore.CsgoLegacy
                        ? (zh ? "CS:GO 模式 · GSI 驱动" : "CS:GO Mode · GSI")
                        : (zh ? "CS2 模式 · GSI 驱动" : "CS2 Mode · GSI"));
            }
            ModeUsageHint.Text = isLol
                ? (compatibility
                    ? (zh ? "当前为英雄联盟 (LOL) 模式，打开游戏进入对局后将自动连接并实时触发击杀反馈。" : "League of Legends (LOL) mode active. Launch the game and enter a match to trigger kill feedback automatically.")
                    : (zh ? "当前为英雄联盟 (LOL) 模式。请按 Win+G 打开并固定 Kill Confirm Overlay 小组件，打开游戏进入对局后自动触发反馈。" : "League of Legends (LOL) mode active. Press Win+G to pin Kill Confirm Overlay, then enter a LOL match."))
                : (compatibility
                    ? (GameSourceSettingsStore.Current == GsiGameVersionSettingsStore.CsgoLegacy ? (zh ? "当前为 CS:GO 模式，打开游戏进入对局后将自动连接并实时触发击杀反馈。" : "CS:GO mode active. Launch the game and enter a match to trigger kill feedback automatically.") : (zh ? "当前为 CS2 模式，打开游戏进入对局后将自动连接并实时触发击杀反馈。" : "CS2 mode active. Launch the game and enter a match to trigger kill feedback automatically."))
                    : (GameSourceSettingsStore.Current == GsiGameVersionSettingsStore.CsgoLegacy ? (zh ? "当前为 CS:GO 模式。请按 Win+G 打开并固定 Kill Confirm Overlay 小组件，打开游戏进入对局后自动触发反馈。" : "CS:GO mode active. Press Win+G to pin Kill Confirm Overlay, then launch your game.") : (zh ? "当前为 CS2 模式。请按 Win+G 打开并固定 Kill Confirm Overlay 小组件，打开游戏进入对局后自动触发反馈。" : "CS2 mode active. Press Win+G to pin Kill Confirm Overlay, then launch your game.")));
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
            // Geometry belongs to the shared panel styles; theme changes only colors.
            ModeStatusBadge.Background = theme.Brush(theme.AccentSoft);
            StatusText.Foreground = theme.Brush(theme.Accent);
            ModeUsageHint.Foreground = theme.Brush(theme.Text);
            foreach (var text in new[] { GameBarModeHint, CompatibilityModeHint, LayoutHint }) text.Foreground = theme.Brush(theme.MutedText);
            ApplyGameBarStatusTheme(theme);
            HomeView.ApplyTheme(theme); ApplyModeSelection(CompatibilityDisplayRuntime.Load().Enabled);
        }
        private Color StatusGreen => (Color)Resources["PanelSuccessColor"];
        private Color StatusAmber => (Color)Resources["PanelWarningColor"];
        private Color StatusRed => (Color)Resources["PanelDangerColor"];
        private Color StatusGray => (Color)Resources["PanelTextMutedColor"];
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

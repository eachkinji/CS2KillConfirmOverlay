using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KillConfirmGameBar.Services;
using KillConfirmCompatibility.Contracts;
using Windows.ApplicationModel.DataTransfer;
using Windows.Data.Json;
using Windows.Storage;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.Web.Http;

namespace KillConfirmGameBar.Features.CompatibilityDisplay
{
    public sealed partial class CompatibilityHomeView : UserControl
    {
        private bool _isPageActive, _suppressGameStyleEvents;
        private bool _shutdownRequested => !_isPageActive;
        private bool _suppressVoicePackEvents, _suppressIconPackEvents, _packSelectorsInitialized;
        private bool _suppressEliteEffectEvents, _suppressKillFxEvents, _suppressWeaponBadgeEvents, _suppressMainAnimationStyleEvents;
        private bool _suppressMoneyRewardModeEvents, _suppressCrossfireGameplaySettingEvents, _suppressSharedStreakModeEvents, _suppressVisualAdjustmentEvents;
        private readonly SemaphoreSlim _packSelectorInitializationLock = new SemaphoreSlim(1, 1);
        private readonly SemaphoreSlim _refreshGate = new SemaphoreSlim(1, 1);
        private readonly SemaphoreSlim _serviceGate = new SemaphoreSlim(1, 1);
        private readonly DispatcherTimer _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        private CancellationTokenSource _repeat;
        private bool _testInProgress;
        private int _revision;
        private string _lastRefreshError, _actionNotice;
        private DateTimeOffset _actionNoticeUntil;
        public event EventHandler EffectsRequested;
        public UIElement EffectsContent => PackTestSectionView.AdvancedEffectsFlyoutCard;
        internal UIElement ScreenLayoutContent => PackTestSectionView.LayoutContentHost.Content as UIElement;
        internal void SetLayoutContent(UIElement content) => PackTestSectionView.LayoutContentHost.Content = content;
        private const string IconPackSettingKey = "KillIconPack", VoicePackSettingKey = "VoicePack", EliteEffectSettingKey = "KillEliteEffect", KillFxSettingKey = "KillFxEnabled", WeaponBadgeSettingKey = "KillWeaponBadge", MainAnimationStyleSettingKey = "MainAnimationStyle";
        private const string MoneyRewardModeSettingKey = "MoneyRewardMode", DefaultMoneyRewardMode = "delta", AudioVolumeSettingKey = "AudioVolume";
        internal const string OpenRuntimeLogsParameterGroupId = "OpenRuntimeLogs";
        private static Uri SoundPackUri => LocalServiceEndpoints.Build("/soundpack");
        private static Uri MoneyRewardModeUri => LocalServiceEndpoints.Build("/money/mode");
        private static Uri CrossfireSettingsUri => LocalServiceEndpoints.Build("/crossfire/settings");
        private static Uri CsolSettingsUri => LocalServiceEndpoints.Build("/csol/settings");
        private static Uri SharedStreakSettingsUri => LocalServiceEndpoints.Build("/streak/settings");
        private static readonly string[] VoicePackHeadImageNames = { "pack_head.png", "pack_head.jpg", "pack_head.jpeg", "pack_head.webp" };
        private static readonly string[] IconPackHeadImageNames = { "pack_head.png", "pack_head.jpg", "pack_head.jpeg", "pack_head.webp", "badge_headshot.png", "badgeex\\badge_headshot.png" };

        public CompatibilityHomeView()
        {
            _suppressGameStyleEvents = true;
            InitializeComponent();
            foreach (GameStyleMode style in Enum.GetValues(typeof(GameStyleMode)))
                GameSelector.Items.Add(new ComboBoxItem { Content = GameStyleService.ToDisplayName(style), Tag = GameStyleService.ToStorageValue(style) });
            PackTestSectionView.AdvancedEffectsFlyout.Content = null;
            _suppressGameStyleEvents = false;
            Loaded += OnLoaded; Unloaded += OnUnloaded;
            _statusTimer.Tick += (s, e) => RefreshStatus();
        }
        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            _isPageActive = true;
            GameStyleService.Changed += OnStyleChanged;
            PackCatalogService.CatalogChanged += OnPackCatalogChanged;
            Danmaku.DanmakuSettingsStore.TestFeedbackChanged += OnDanmakuTestFeedback;
            _statusTimer.Start();
            if (!IsUiValidation) GsiStatusMonitor.Instance.StartMonitoring();
            await RefreshGameAsync(++_revision);
        }
        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            _isPageActive = false; ++_revision; StopRepeating(); _statusTimer.Stop();
            if (!IsUiValidation) GsiStatusMonitor.Instance.StopMonitoring();
            GameStyleService.Changed -= OnStyleChanged; PackCatalogService.CatalogChanged -= OnPackCatalogChanged;
            Danmaku.DanmakuSettingsStore.TestFeedbackChanged -= OnDanmakuTestFeedback;
        }
        private async void OnGameChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressGameStyleEvents || !(GameSelector.SelectedItem is ComboBoxItem item) || !(item.Tag is string key)) return;
            GameStyleMode style = GameStyleService.FromKey(key);
            // Leave the native SelectionChanged callback before changing the shared game preference.
            try { await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => { if (_isPageActive) GameStyleService.Current = style; }); }
            catch (Exception error) { App.Log("Compatibility selection dispatch: " + error); }
        }
        private async void OnStyleChanged(object sender, GameStyleMode style)
        {
            int revision = ++_revision; StopRepeating();
            PackTestSectionView.TestFeedbackText.Visibility = Visibility.Collapsed;
            try { await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () => await RefreshGameAsync(revision)); }
            catch (Exception error) { App.Log("Compatibility game dispatch: " + error); }
        }
        private async Task RefreshGameAsync(int revision)
        {
            await _refreshGate.WaitAsync();
            try
            {
                if (!_isPageActive || revision != _revision) return;
                _packSelectorsInitialized = false;
                _lastRefreshError = null;
                ApplyGameStyleUi();
                await InitializePackSelectorsAsync();
                if (!_isPageActive || revision != _revision) return;
                LoadVolume();
                await EnsureServiceAvailableAsync();
                if (_isPageActive && revision == _revision) RefreshStatus();
            }
            catch (Exception error) { _lastRefreshError = error.Message; App.Log("Compatibility game refresh: " + error); if (_isPageActive) StatusText.Text = error.Message; }
            finally { _refreshGate.Release(); }
        }
        private void ApplyGameStyleUi()
        {
            _suppressGameStyleEvents = true;
            GameSelector.SelectedItem = GameSelector.Items.OfType<ComboBoxItem>().FirstOrDefault(i => (string)i.Tag == GameStyleService.ToStorageValue(GameStyleService.Current));
            _suppressGameStyleEvents = false;
            MountAdvancedEffectsPanel();
            ApplyLanguage(); ApplyTheme(GameThemePalette.Current);
        }
        public void ApplyLanguage()
        {
            bool zh = LocalizationManager.Current == UiLanguage.SimplifiedChinese;
            GameTitle.Text = GameStyleService.ToDisplayName(GameStyleService.Current);
            GameHint.Text = zh ? "图标、语音与战斗规则使用当前游戏配置；屏幕布局为兼容显示独立保存。" : "Packs, voice and combat rules follow the selected game. Desktop layouts are saved separately.";
            EffectsLabel.Text = zh ? "战斗与视效" : "Combat & effects";
            StatusTitle.Text = zh ? "运行状态" : "Runtime status"; RetryLabel.Text = zh ? "重试连接" : "Retry connection";
            CfgLabel.Text = zh ? "修复游戏配置" : "Repair game config"; CopyLabel.Text = zh ? "复制诊断" : "Copy diagnostics"; LogsLabel.Text = zh ? "打开日志" : "Open logs";
            DisplayStateLabel.Text = zh ? "显示端" : "Display"; ServiceStateLabel.Text = zh ? "后台服务" : "Service"; GameDataLabel.Text = zh ? "游戏数据" : "Game data";
            PackTestSectionView.PackHintText.Text = zh ? "先选择素材和测试事件，再播放确认效果。屏幕位置与大小可以在本卡片下方编辑。" : "Choose packs and a test event, then play to check the effect. Edit screen position and size below.";
            PackTestSectionView.VoicePackLabel.Text = zh ? "语音包" : "Voice pack"; PackTestSectionView.IconPackLabel.Text = zh ? "图标包" : "Icon pack";
            PackTestSectionView.TestPresetLabel.Text = zh ? "测试事件" : "Test event"; PackTestSectionView.VolumeLabel.Text = zh ? "播放音量" : "Volume";
            PackTestSectionView.SendTestLabel.Text = zh ? "播放画面与音频" : "Play visuals & audio";
            PackTestSectionView.RepeatTestLabel.Text = zh ? "循环播放" : "Repeat test";
            PackTestSectionView.ReloadAudioLabel.Text = zh ? "重载音频" : "Reload audio"; PackTestSectionView.DanmakuTestLabel.Text = zh ? "测试弹幕" : "Test danmaku";
            PackTestSectionView.PackTestHeaderText.Text = zh ? "素材与测试" : "Packs & testing";
            var presetLabels = new Dictionary<string,string> { ["one"] = "1 杀", ["one_hs"] = "1 杀爆头", ["one_knife"] = "1 杀刀杀", ["one_grenade"] = "1 杀雷杀", ["one_first"] = "首杀", ["one_last"] = "尾杀", ["assist"] = "助攻", ["gold_first"] = "爆头首杀", ["gold_last"] = "爆头尾杀", ["two"] = "2 杀", ["three"] = "3 杀", ["four"] = "4 杀", ["five"] = "5 杀", ["six"] = "6 杀", ["seven"] = "7 杀", ["eight"] = "8 杀", ["nine"] = "9 杀", ["badge_first"] = "首杀徽章", ["badge_last"] = "尾杀徽章", ["bomb_plant"] = "C4 下包", ["bomb_defuse"] = "C4 拆包", ["hostage_interact"] = "人质接触", ["hostage_rescue"] = "人质救出", ["round_win"] = "回合胜利", ["round_loss"] = "回合失败" };
            foreach (var item in PackTestSectionView.TestPresetSelector.Items.OfType<ComboBoxItem>()) { string key = (string)item.Tag; item.Content = zh && presetLabels.ContainsKey(key) ? presetLabels[key] : key.Replace('_', ' '); }
            ToolTipService.SetToolTip(PackTestSectionView.VoicePackSelector, LocalizationManager.Text("VoicePackLabel"));
            ToolTipService.SetToolTip(PackTestSectionView.IconPackSelector, LocalizationManager.Text("IconPackLabel"));
            ToolTipService.SetToolTip(PackTestSectionView.SendTestButton, zh ? "播放画面与音频" : "Test visuals and audio");
            ApplyAdvancedEffectsPanelLanguage(); RefreshStatus();
        }
        internal void ApplyTheme(GameThemePalette theme)
        {
            theme = GameThemePalette.Home;
            Foreground = theme.Brush(theme.Text);
            foreach (var card in new[] { GameCard, StatusCard, PackTestSectionView.PackTestCard }) { card.Background = theme.Brush(theme.Card); card.BorderBrush = theme.Brush(theme.SoftBorder); }
            foreach (var text in new[] { GameHint, GsiText, DisplayStateHint, ServiceStateHint, ServiceEndpointText, PackTestSectionView.PackHintText, PackTestSectionView.TestFeedbackText }) text.Foreground = theme.Brush(theme.MutedText);
            PackTestSectionView.PackTestHeaderBorder.Background = theme.Brush(theme.Card);
            PackTestSectionView.PackTestHeaderBorder.BorderBrush = theme.Brush(theme.SoftBorder);
            PackTestSectionView.PackTestHeaderText.Foreground = theme.Brush(theme.Text);
            foreach (var selector in new[] { GameSelector, PackTestSectionView.VoicePackSelector, PackTestSectionView.IconPackSelector, PackTestSectionView.AudioVolumeSelector, PackTestSectionView.TestPresetSelector })
            { selector.Background = theme.Brush(theme.Field); selector.Foreground = theme.Brush(theme.Text); selector.BorderBrush = theme.Brush(theme.Border); selector.CornerRadius = new CornerRadius(4); }
            PackTestSectionView.IconPackSelector.IsEnabled = GameStyleService.Current != GameStyleMode.Overwatch && GameStyleService.Current != GameStyleMode.ModernWarfare2019 && GameStyleService.Current != GameStyleMode.Apex;
            foreach (var icon in new[] { PackTestSectionView.VoicePackIcon, PackTestSectionView.IconPackIcon, PackTestSectionView.VolumeIcon, PackTestSectionView.TestPresetIcon }) icon.Foreground = theme.Brush(theme.Accent);
            ApplyAdvancedEffectsPanelTheme(); RefreshStatus();
        }
        private void RefreshStatus()
        {
            UpdateStatusCards(CompatibilityDisplayRuntime.Load(), CompatibilityDisplayRuntime.ReadStatus(), GsiStatusMonitor.Instance.CurrentSnapshot);
            bool enabled = CompatibilityDisplayRuntime.Load().Enabled;
            PackTestSectionView.SendTestButton.IsEnabled = enabled && _packSelectorsInitialized && !_testInProgress;
            PackTestSectionView.RepeatTestButton.IsEnabled = enabled && _packSelectorsInitialized && (!_testInProgress || _repeat != null);
            bool zh = LocalizationManager.Current == UiLanguage.SimplifiedChinese;
            PackTestSectionView.RepeatTestLabel.Text = _repeat != null ? (zh ? "停止循环" : "Stop repeat") : (zh ? "循环播放" : "Repeat test");
        }
        private void UpdateStatusCards(DisplayConfiguration config, DisplayStatus status, GsiStatusSnapshot gsi)
        {
            bool zh = LocalizationManager.Current == UiLanguage.SimplifiedChinese;
            bool running = config.Enabled && CompatibilityDisplayRuntime.IsRunning(status);
            bool serviceOnline = gsi.ServiceReachable || (running && status.Connected);
            string problem = (running && status.Style == GameStyleService.ToStorageValue(GameStyleService.Current) ? status.Error : null) ?? _lastRefreshError;
            DisplayStateText.Text = !config.Enabled ? (zh ? "Game Bar 模式" : "Game Bar mode") : !running ? (zh ? "等待启动" : "Starting") : status.Loading ? (zh ? "加载素材中" : "Loading packs") : !string.IsNullOrWhiteSpace(problem) ? (zh ? "需要处理" : "Needs attention") : status.Editing ? (zh ? "编辑布局中" : "Editing layout") : (zh ? "已就绪" : "Ready");
            DisplayStateHint.Text = !config.Enabled ? (zh ? "打开并固定 Game Bar 组件" : "Open and pin the Game Bar widget") : !running ? (zh ? "点击重试连接以启动显示端" : "Retry the connection to start the display") : status.Editing ? (zh ? "Enter 或 Esc 保存并结束" : "Enter or Esc to save and finish") : status.Visible ? (zh ? "正在屏幕上显示" : "Visible on screen") : (zh ? "等待游戏或播放测试" : "Waiting for a game or preview");
            ServiceStateText.Text = serviceOnline ? (zh ? "已连接" : "Connected") : (zh ? "等待连接" : "Connecting");
            ServiceStateHint.Text = serviceOnline ? (zh ? "音频与事件服务可用" : "Audio and event service available") : (zh ? "可点击重试连接" : "Select Retry connection");
            GameDataText.Text = gsi.IsGreen ? (zh ? "已连接" : "Connected") : (zh ? "等待游戏数据" : "Waiting for game data");
            GsiText.Text = gsi.Posts > 0
                ? (zh ? "已接收 " + gsi.Posts.ToString("0") + " 次" : gsi.Posts.ToString("0") + " updates received") + (gsi.LastPostAgeMs.HasValue ? (zh ? " · 上次 " : " · Last ") + Math.Max(0, gsi.LastPostAgeMs.Value / 1000).ToString("0") + (zh ? " 秒前" : "s ago") : "")
                : (zh ? "启动 CS2 后自动接收；预览无需游戏数据" : "Start CS2 to receive data. Preview works without it.");
            ServiceEndpointText.Text = (zh ? "本地服务：" : "Local service: ") + LocalServiceEndpoints.BaseUri;
            PaintStatusBadge(DisplayStateBadge, DisplayStateText, running && string.IsNullOrWhiteSpace(problem), !config.Enabled || (running && status.Loading));
            PaintStatusBadge(ServiceStateBadge, ServiceStateText, serviceOnline, false);
            PaintStatusBadge(GameDataBadge, GameDataText, gsi.IsGreen, !gsi.IsGreen);
            var theme = GameThemePalette.Home;
            RuntimeNoticeBorder.Background = theme.Brush(string.IsNullOrWhiteSpace(problem) ? theme.SubtleField : theme.WarningField);
            StatusText.Foreground = theme.Brush(string.IsNullOrWhiteSpace(problem) ? theme.MutedText : theme.WarningText);
            StatusText.Text = !string.IsNullOrWhiteSpace(problem) ? problem : DateTimeOffset.UtcNow < _actionNoticeUntil ? _actionNotice : (zh ? "无需启动游戏也能测试画面。实际击杀效果需要接收游戏数据；没有数据时，请安装或修复游戏配置。" : "Preview works without a running game. Live kill effects need game data; repair the game configuration if data is missing.");
        }
        private static void PaintStatusBadge(Border badge, TextBlock value, bool healthy, bool waiting)
        {
            var theme = GameThemePalette.Home;
            badge.Background = theme.Brush(healthy ? theme.AccentSoft : waiting ? theme.SubtleField : theme.WarningField);
            value.Foreground = theme.Brush(healthy ? theme.Accent : waiting ? theme.MutedText : theme.WarningText);
        }
        private void ShowRuntimeNotice(string message)
        {
            _actionNotice = message; _actionNoticeUntil = DateTimeOffset.UtcNow.AddSeconds(10); RefreshStatus();
        }
        private async Task EnsureServiceAvailableAsync()
        {
            if (IsUiValidation) return;
            await _serviceGate.WaitAsync();
            try
            {
                bool healthy = false;
                try { using (var client = await LocalServiceAuth.CreateHttpClientAsync()) using (var response = await client.GetAsync(LocalServiceEndpoints.Build("/health"))) healthy = response.IsSuccessStatusCode; } catch { }
                if (!healthy && !await ServiceLauncher.LaunchAsync(LocalServiceEndpoints.Port)) throw new InvalidOperationException("无法启动后台服务");
                if (!_isPageActive) return;
                await SyncSelectedVoicePackAsync();
                await SyncCrossfireGameplaySettingsAsync(); await SyncCsolGameplaySettingsAsync(); await SyncSharedStreakSettingsAsync();
                await SyncMoneyRewardModeAsync(); await SyncSpectatedKillEffectsAsync(); await SyncDagoujiaoSettingsAsync(); await SyncDoubaoSettingsAsync();
            }
            finally { _serviceGate.Release(); }
        }
        private Task WarmStartupAnimationCacheAsync(int delayMs = 0) { CompatibilityRenderPreferences.Invalidate(); return Task.CompletedTask; }

        private void OnOpenEffectsClick(object sender, RoutedEventArgs e) => EffectsRequested?.Invoke(this, EventArgs.Empty);
        private async void OnTestEventClick(object sender, RoutedEventArgs e) { StopRepeating(); try { await PlayTestAsync(); } catch (Exception error) { ShowTestFeedback(error.Message); } }
        private void ShowTestFeedback(string message)
        {
            PackTestSectionView.TestFeedbackText.Text = message;
            PackTestSectionView.TestFeedbackText.Visibility = Visibility.Visible;
        }
        private void OnDanmakuTestFeedback(string message) => ShowTestFeedback(message);
        private async Task PlayTestAsync(bool audio = true)
        {
            bool zh = LocalizationManager.Current == UiLanguage.SimplifiedChinese;
            if (!CompatibilityDisplayRuntime.Load().Enabled) throw new InvalidOperationException(zh ? "请先在主页选择兼容显示模式。" : "Select desktop display mode on Home first.");
            if (!_packSelectorsInitialized) throw new InvalidOperationException(zh ? "素材正在加载，请稍后再测试。" : "Packs are loading. Try again shortly.");
            if (_testInProgress) return;
            _testInProgress = true;
            RefreshStatus();
            int revision = _revision;
            try
            {
                ShowTestFeedback(audio ? (zh ? "正在准备画面与音频测试…" : "Preparing the visual and audio test…") : (zh ? "正在准备画面预览…" : "Preparing the visual preview…"));
                await EnsureServiceAvailableAsync();
                if (!_isPageActive || revision != _revision || !CompatibilityDisplayRuntime.Load().Enabled) return;
                if (!await CompatibilityDisplayRuntime.EnsureStartedAsync()) throw new InvalidOperationException(zh ? "无法启动兼容显示，请点击重试服务。" : "Could not start desktop display. Retry the service.");
                string preset = (PackTestSectionView.TestPresetSelector.SelectedItem as ComboBoxItem)?.Tag as string ?? "one";
                long request = 0;
                CompatibilityDisplayRuntime.Update(c => { c.TestPreset = preset; c.TestAudio = audio; c.TestRequest = request = Math.Max(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), c.TestRequest + 1); });
                var deadline = DateTimeOffset.UtcNow.AddSeconds(40);
                while (_isPageActive && revision == _revision && CompatibilityDisplayRuntime.Load().Enabled && DateTimeOffset.UtcNow < deadline)
                {
                    var status = CompatibilityDisplayRuntime.ReadStatus();
                    if (CompatibilityDisplayRuntime.IsRunning(status) && status.LastTestRequest == request)
                    {
                        if (!string.IsNullOrWhiteSpace(status.TestError)) throw new InvalidOperationException(status.TestError);
                        ShowTestFeedback(audio ? (zh ? "画面已预览，音频测试已触发。无需启动游戏。" : "Visuals previewed and audio test triggered. No game is needed.") : (zh ? "画面已预览。无需启动游戏。" : "Visuals previewed. No game is needed."));
                        return;
                    }
                    await Task.Delay(100);
                }
                if (_isPageActive && revision == _revision && CompatibilityDisplayRuntime.Load().Enabled)
                    throw new TimeoutException(zh ? "显示端未确认播放，请查看运行状态并点击重试服务。" : "The display did not confirm playback. Check its runtime status and retry the service.");
            }
            finally { _testInProgress = false; if (_isPageActive) RefreshStatus(); }
        }
        internal async Task PreviewVisualAsync()
        {
            StopRepeating();
            try { await PlayTestAsync(false); } catch (Exception error) { ShowTestFeedback(error.Message); }
        }
        private async void OnRepeatTestClick(object sender, RoutedEventArgs e)
        {
            if (_repeat != null) { StopRepeating(); return; }
            var repeat = _repeat = new CancellationTokenSource();
            try { while (_isPageActive && CompatibilityDisplayRuntime.Load().Enabled && !repeat.IsCancellationRequested) { await PlayTestAsync(); await Task.Delay(2000, repeat.Token); } }
            catch (OperationCanceledException) { }
            catch (Exception error) { ShowTestFeedback(error.Message); }
            finally { if (_repeat == repeat) _repeat = null; repeat.Dispose(); if (_isPageActive) RefreshStatus(); }
        }
        private void StopRepeating() { _repeat?.Cancel(); _repeat = null; if (_isPageActive) RefreshStatus(); }
        private async void OnReloadAudioClick(object sender, RoutedEventArgs e)
        {
            try { await EnsureServiceAvailableAsync(); using (var client = await LocalServiceAuth.CreateHttpClientAsync()) using (var body = new HttpStringContent("")) using (var response = await client.PostAsync(LocalServiceEndpoints.Build("/audio/reload"), body)) response.EnsureSuccessStatusCode(); ShowTestFeedback(LocalizationManager.Current == UiLanguage.SimplifiedChinese ? "音频已重新加载。" : "Audio reloaded."); }
            catch (Exception error) { ShowRuntimeNotice(error.Message); }
        }
        private async void OnRetryClick(object sender, RoutedEventArgs e) { try { await EnsureServiceAvailableAsync(); await CompatibilityDisplayRuntime.EnsureStartedAsync(); ShowRuntimeNotice(LocalizationManager.Current == UiLanguage.SimplifiedChinese ? "已重试服务连接。" : "Service connection retried."); } catch (Exception error) { ShowRuntimeNotice(error.Message); } }
        private async void OnLogsClick(object sender, RoutedEventArgs e) { if (!await KillConfirmWidgetPage.TryLaunchFullTrustHelperAsync(OpenRuntimeLogsParameterGroupId)) ShowRuntimeNotice(LocalizationManager.Current == UiLanguage.SimplifiedChinese ? "无法打开日志，请重试。" : "Could not open logs. Try again."); }
        private void OnCopyClick(object sender, RoutedEventArgs e) { var data = new DataPackage(); data.SetText("Display=" + DisplayStateText.Text + "\nService=" + ServiceStateText.Text + "\n" + ServiceEndpointText.Text + "\n" + StatusText.Text + "\n" + GsiText.Text + "\nGame=" + GameStyleService.Current); Clipboard.SetContent(data); ShowRuntimeNotice(LocalizationManager.Current == UiLanguage.SimplifiedChinese ? "诊断信息已复制。" : "Diagnostics copied."); }
        private async void OnInstallCfgClick(object sender, RoutedEventArgs e)
        {
            try
            {
                await EnsureServiceAvailableAsync();
                using (var client = await LocalServiceAuth.CreateHttpClientAsync()) using (var body = new HttpStringContent("{}", Windows.Storage.Streams.UnicodeEncoding.Utf8, "application/json")) using (var response = await client.PostAsync(LocalServiceEndpoints.Build("/counter-strike/cfg?version=" + GsiGameVersionSettingsStore.Load()), body))
                { response.EnsureSuccessStatusCode(); ShowRuntimeNotice(LocalizationManager.Current == UiLanguage.SimplifiedChinese ? "游戏配置已安装 / 修复。请启动或重启 CS2。" : "Game configuration repaired. Start or restart CS2."); }
            }
            catch (Exception error) { ShowRuntimeNotice(error.Message); }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KillConfirmGameBar.Services;
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
        private int _revision;
        private string _lastRefreshError;
        public event EventHandler EffectsRequested;
        public UIElement EffectsContent => PackTestSectionView.AdvancedEffectsFlyoutCard;
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
            _statusTimer.Start();
            if (!IsUiValidation) GsiStatusMonitor.Instance.StartMonitoring();
            await RefreshGameAsync(++_revision);
        }
        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            _isPageActive = false; ++_revision; StopRepeating(); _statusTimer.Stop();
            if (!IsUiValidation) GsiStatusMonitor.Instance.StopMonitoring();
            GameStyleService.Changed -= OnStyleChanged; PackCatalogService.CatalogChanged -= OnPackCatalogChanged;
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
            catch (Exception error) { _lastRefreshError = error.ToString(); App.Log("Compatibility game refresh: " + error); if (_isPageActive) StatusText.Text = error.Message; }
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
            EditScreenButton.Content = zh ? "编辑屏幕" : "Edit screen"; EffectsButton.Content = zh ? "战斗与视效设置" : "Combat & effects";
            StatusTitle.Text = zh ? "运行状态" : "Runtime status"; RetryButton.Content = zh ? "重试服务" : "Retry service";
            CfgButton.Content = zh ? "安装 / 修复游戏配置" : "Install / repair game config"; CopyButton.Content = zh ? "复制诊断" : "Copy diagnostics"; LogsButton.Content = zh ? "打开日志" : "Open logs";
            PackTestSectionView.PackTestHeaderText.Text = zh ? "资源与测试" : "PACKS & TEST";
            var presetLabels = new Dictionary<string,string> { ["one"] = "1 杀", ["one_hs"] = "1 杀爆头", ["one_knife"] = "1 杀刀杀", ["one_grenade"] = "1 杀雷杀", ["one_first"] = "首杀", ["one_last"] = "尾杀", ["assist"] = "助攻", ["gold_first"] = "爆头首杀", ["gold_last"] = "爆头尾杀", ["two"] = "2 杀", ["three"] = "3 杀", ["four"] = "4 杀", ["five"] = "5 杀", ["six"] = "6 杀", ["seven"] = "7 杀", ["eight"] = "8 杀", ["nine"] = "9 杀", ["badge_first"] = "首杀徽章", ["badge_last"] = "尾杀徽章", ["bomb_plant"] = "C4 下包", ["bomb_defuse"] = "C4 拆包", ["hostage_interact"] = "人质接触", ["hostage_rescue"] = "人质救出", ["round_win"] = "回合胜利", ["round_loss"] = "回合失败" };
            foreach (var item in PackTestSectionView.TestPresetSelector.Items.OfType<ComboBoxItem>()) { string key = (string)item.Tag; item.Content = zh && presetLabels.ContainsKey(key) ? presetLabels[key] : key.Replace('_', ' '); }
            ToolTipService.SetToolTip(PackTestSectionView.VoicePackSelector, LocalizationManager.Text("VoicePackLabel"));
            ToolTipService.SetToolTip(PackTestSectionView.IconPackSelector, LocalizationManager.Text("IconPackLabel"));
            ToolTipService.SetToolTip(PackTestSectionView.SendTestButton, zh ? "播放画面与音频" : "Test visuals and audio");
            ApplyAdvancedEffectsPanelLanguage();
        }
        internal void ApplyTheme(GameThemePalette theme)
        {
            Foreground = theme.Brush(theme.Text);
            foreach (var card in new[] { GameCard, StatusCard, PackTestSectionView.PackTestCard }) { card.Background = theme.Brush(theme.Card); card.BorderBrush = theme.Brush(theme.SoftBorder); }
            GameHint.Foreground = StatusText.Foreground = GsiText.Foreground = theme.Brush(theme.MutedText);
            EditScreenButton.Background = theme.Brush(theme.Accent); EditScreenButton.Foreground = theme.Brush(theme.AccentText);
            PackTestSectionView.PackTestHeaderBorder.Background = theme.Brush(theme.Accent);
            PackTestSectionView.PackTestHeaderBorder.BorderBrush = theme.Brush(theme.Border);
            PackTestSectionView.PackTestHeaderText.Foreground = theme.Brush(theme.AccentText);
            foreach (var selector in new[] { GameSelector, PackTestSectionView.VoicePackSelector, PackTestSectionView.IconPackSelector, PackTestSectionView.AudioVolumeSelector, PackTestSectionView.TestPresetSelector })
            { selector.Background = theme.Brush(theme.Field); selector.Foreground = theme.Brush(theme.Text); selector.BorderBrush = theme.Brush(theme.Border); }
            PackTestSectionView.IconPackSelector.IsEnabled = GameStyleService.Current != GameStyleMode.Overwatch && GameStyleService.Current != GameStyleMode.ModernWarfare2019 && GameStyleService.Current != GameStyleMode.Apex;
            foreach (var icon in new[] { PackTestSectionView.VoicePackIcon, PackTestSectionView.IconPackIcon, PackTestSectionView.VolumeIcon, PackTestSectionView.TestPresetIcon }) icon.Foreground = theme.Brush(theme.Text);
            foreach (var button in new[] { EffectsButton, RetryButton, CfgButton, CopyButton, LogsButton, PackTestSectionView.ReloadAudioButton, PackTestSectionView.RepeatTestButton, PackTestSectionView.AdvancedEffectsButton, PackTestSectionView.SendTestButton, PackTestSectionView.DanmakuTestButton })
            { button.Background = theme.Brush(theme.Field); button.Foreground = theme.Brush(theme.Text); button.BorderBrush = theme.Brush(theme.SoftBorder); button.CornerRadius = new CornerRadius(10); }
            PackTestSectionView.SendTestButton.Background = theme.Brush(theme.Accent); PackTestSectionView.SendTestButton.Foreground = theme.Brush(theme.AccentText);
            ApplyAdvancedEffectsPanelTheme();
        }
        private void RefreshStatus()
        {
            var status = CompatibilityDisplayRuntime.ReadStatus(); bool zh = LocalizationManager.Current == UiLanguage.SimplifiedChinese;
            string runtime = !CompatibilityDisplayRuntime.Load().Enabled ? (zh ? "Game Bar 模式 · 兼容显示已停用" : "Game Bar mode · Desktop display disabled")
                : !CompatibilityDisplayRuntime.IsRunning(status) ? (zh ? "兼容显示尚未运行" : "Desktop display stopped")
                : status.Loading ? (zh ? "资源正在加载" : "Loading resources") : status.Connected ? (zh ? "事件已连接 · 资源已配置" : "Events connected · Resources configured") : (zh ? "正在连接后台" : "Connecting to service");
            StatusText.Text = runtime + (string.IsNullOrWhiteSpace(status?.Error) ? "" : "\n" + status.Error);
            if (_lastRefreshError != null) StatusText.Text += "\n" + _lastRefreshError;
            var gsi = GsiStatusMonitor.Instance.CurrentSnapshot;
            GsiText.Text = $"SVC {(gsi.ServiceReachable ? "●" : "○")}   GSI {(gsi.IsGreen ? "●" : "○")}   {LocalServiceEndpoints.BaseUri}   ·   GSI {gsi.Posts:0}";
            EditScreenButton.IsEnabled = PackTestSectionView.SendTestButton.IsEnabled = PackTestSectionView.RepeatTestButton.IsEnabled = CompatibilityDisplayRuntime.Load().Enabled;
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
        private async void OnEditScreenClick(object sender, RoutedEventArgs e)
        {
            if (!CompatibilityDisplayRuntime.Load().Enabled) return;
            CompatibilityDisplayRuntime.Update(c => c.EditRequest = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            await CompatibilityDisplayRuntime.EnsureStartedAsync();
        }
        private async void OnTestEventClick(object sender, RoutedEventArgs e) { StopRepeating(); try { await PlayTestAsync(); } catch (Exception error) { StatusText.Text = error.Message; } }
        private async Task PlayTestAsync()
        {
            if (!CompatibilityDisplayRuntime.Load().Enabled || !_packSelectorsInitialized) return;
            int revision = _revision;
            await EnsureServiceAvailableAsync();
            if (!_isPageActive || revision != _revision || !CompatibilityDisplayRuntime.Load().Enabled) return;
            string preset = (PackTestSectionView.TestPresetSelector.SelectedItem as ComboBoxItem)?.Tag as string ?? "one";
            CompatibilityDisplayRuntime.Update(c => { c.TestPreset = preset; c.TestAudio = true; c.TestRequest = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(); });
            await CompatibilityDisplayRuntime.EnsureStartedAsync();
        }
        private async void OnRepeatTestClick(object sender, RoutedEventArgs e)
        {
            if (_repeat != null) { StopRepeating(); return; }
            var repeat = _repeat = new CancellationTokenSource();
            try { while (_isPageActive && CompatibilityDisplayRuntime.Load().Enabled && !repeat.IsCancellationRequested) { await PlayTestAsync(); await Task.Delay(2000, repeat.Token); } }
            catch (OperationCanceledException) { }
            catch (Exception error) { StatusText.Text = error.Message; }
            finally { if (_repeat == repeat) _repeat = null; repeat.Dispose(); }
        }
        private void StopRepeating() { _repeat?.Cancel(); _repeat = null; }
        private async void OnReloadAudioClick(object sender, RoutedEventArgs e)
        {
            try { await EnsureServiceAvailableAsync(); using (var client = await LocalServiceAuth.CreateHttpClientAsync()) using (var body = new HttpStringContent("")) using (var response = await client.PostAsync(LocalServiceEndpoints.Build("/audio/reload"), body)) response.EnsureSuccessStatusCode(); }
            catch (Exception error) { StatusText.Text = error.Message; }
        }
        private async void OnRetryClick(object sender, RoutedEventArgs e) { try { await EnsureServiceAvailableAsync(); await CompatibilityDisplayRuntime.EnsureStartedAsync(); RefreshStatus(); } catch (Exception error) { StatusText.Text = error.Message; } }
        private async void OnLogsClick(object sender, RoutedEventArgs e) => await KillConfirmWidgetPage.TryLaunchFullTrustHelperAsync(OpenRuntimeLogsParameterGroupId);
        private void OnCopyClick(object sender, RoutedEventArgs e) { var data = new DataPackage(); data.SetText(StatusText.Text + "\n" + GsiText.Text + "\nGame=" + GameStyleService.Current); Clipboard.SetContent(data); }
        private async void OnInstallCfgClick(object sender, RoutedEventArgs e)
        {
            try
            {
                await EnsureServiceAvailableAsync();
                using (var client = await LocalServiceAuth.CreateHttpClientAsync()) using (var body = new HttpStringContent("{}", Windows.Storage.Streams.UnicodeEncoding.Utf8, "application/json")) using (var response = await client.PostAsync(LocalServiceEndpoints.Build("/counter-strike/cfg?version=" + GsiGameVersionSettingsStore.Load()), body))
                { response.EnsureSuccessStatusCode(); StatusText.Text = await response.Content.ReadAsStringAsync(); }
            }
            catch (Exception error) { StatusText.Text = error.Message; }
        }
    }
}

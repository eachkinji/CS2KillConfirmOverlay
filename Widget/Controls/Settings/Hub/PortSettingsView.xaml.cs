using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using KillConfirmGameBar.Services;
using Windows.Data.Json;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.Web.Http;

namespace KillConfirmGameBar.Controls.Settings
{
    public sealed partial class PortSettingsView : UserControl
    {
        private readonly ObservableCollection<PortButtonBinding> _portButtons =
            new ObservableCollection<PortButtonBinding>();

        private bool _suppressLolPollEvents;
        public PortSettingsView()
        {
            InitializeComponent();
            HubPortBackupList.ItemsSource = _portButtons;
            Loaded += (s, e) => { GameSourceSettingsStore.Changed -= OnGameSourceChanged; GameSourceSettingsStore.Changed += OnGameSourceChanged; };
            Unloaded += (s, e) => { GameSourceSettingsStore.Changed -= OnGameSourceChanged; };
        }
        private void OnGameSourceChanged(object sender, EventArgs e)
        {
            _ = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () => { ApplyLanguage(); _ = RefreshPortStateAsync(); });
        }

        internal async Task InitializeAsync()
        {
            ApplyLanguage();
            ApplyTheme(GameThemePalette.Current);
            HubPortAutoSearchToggle.IsOn = PortSettingsStore.AutoSearchEnabled;
            await RefreshPortStateAsync();
        }

        internal void ApplyLanguage()
        {
            bool isChinese = LocalizationManager.Current == UiLanguage.SimplifiedChinese;
            bool isLol = GameSourceSettingsStore.IsLol;
            string src = GameSourceSettingsStore.Current;
            string fullName = GameSourceSettingsStore.FullDisplayName(src, isChinese);
            if (HubPortGameLogoBrush != null) HubPortGameLogoBrush.ImageSource = new Windows.UI.Xaml.Media.Imaging.BitmapImage(new Uri(GameSourceSettingsStore.LogoUri(src)));
            HubPortTitleText.Text = isChinese ? $"{fullName} 本地服务与数据源配置" : $"{fullName} Service & Data Source";
            if (CsCfgSectionCard != null) CsCfgSectionCard.Visibility = isLol ? Visibility.Collapsed : Visibility.Visible;
            if (LolPortSectionCard != null) LolPortSectionCard.Visibility = isLol ? Visibility.Visible : Visibility.Collapsed;
            if (CsRootTitleText != null) CsRootTitleText.Text = isChinese ? $"{fullName} 游戏根目录与 CFG 配置" : $"{fullName} Root & CFG Status";
            if (CsSelectRootButton != null) CsSelectRootButton.Content = isChinese ? "选择根目录" : "Select root";
            if (CsInstallCfgButton != null) CsInstallCfgButton.Content = isChinese ? "一键写入/修复 CFG" : "Install / Repair CFG";
            if (LolRootTitleText != null) LolRootTitleText.Text = isChinese ? "英雄联盟游戏根目录与检测端口" : "League of Legends Root & Live Client Endpoint";
            if (LolSelectRootButton != null) LolSelectRootButton.Content = isChinese ? "选择根目录" : "Select root";
            if (LolPollIntervalLabelText != null) LolPollIntervalLabelText.Text = isChinese ? "端口抓取速度调控 (Polling Interval)" : "Live Client Polling Interval";
            if (LolPollIntervalHintText != null) LolPollIntervalHintText.Text = isChinese ? "默认 50ms 极速采集（用于金币跳动反算与瞬时连杀）；支持 20–1000ms 自调控。" : "Default 50ms ultra-fast polling; adjustable between 20–1000ms.";
            if (LolPollCustomApplyButton != null) LolPollCustomApplyButton.Content = isChinese ? "应用" : "Apply";
            SelectLolPollIntervalUi();
            HubPortDescriptionText.Text = isChinese
                ? "默认端口 10087 与部分用户电脑上的软件冲突，下面提供 5 个备用端口和自定义入口。"
                : "The default port 10087 conflicts with some user-installed software. Below are 5 backup ports and a custom-port input.";
            HubPortCurrentLabel.Text = isChinese ? "当前使用" : "In use";
            HubPortRefreshButton.Content = isChinese ? "重新检测" : "Refresh";
            HubPortBackupLabel.Text = isChinese ? "备用端口（点击切换）" : "Backup ports (tap to switch)";
            HubPortBackupHint.Text = isChinese
                ? "若默认端口冲突，点击任意一个备用端口即可立即切换。"
                : "If the default port is busy, tap any backup to switch immediately.";
            HubPortCustomLabel.Text = isChinese ? "自定义端口" : "Custom port";
            HubPortCustomHint.Text = isChinese
                ? "支持 1024-65535，应用后会自动重启服务并更新 cfg 文件。"
                : "Accepts 1024-65535. Applying will restart the service and refresh the cfg file.";
            HubPortCustomApplyButton.Content = isChinese ? "应用" : "Apply";
            HubPortAutoSearchLabel.Text = isChinese ? "端口冲突时自动切换" : "Auto-search free port";
            HubPortAutoSearchHint.Text = isChinese
                ? "默认 10087 被占用时，自动顺延到下一个空闲端口（最多 +100）。"
                : "If 10087 is held by another program, the service will scan forward to the next free port (up to +100).";
            HubPortStatusTitle.Text = isChinese ? "连接状态" : "CONNECTION STATUS";
            HubPortStatusBody.Text = isChinese
                ? "下面是最近一次连接检查结果。"
                : "Latest connection check result.";
            RefreshPortButtons();
            UpdatePortStatusText();
        }

        internal void ApplyTheme(GameThemePalette theme)
        {
            HubPortTitleText.Foreground = new SolidColorBrush(theme.Text);
            HubPortDescriptionText.Foreground = new SolidColorBrush(theme.MutedText);
            HubPortCurrentLabel.Foreground = new SolidColorBrush(theme.Text);
            HubPortCurrentValue.Foreground = new SolidColorBrush(theme.Secondary);
            HubPortBackupLabel.Foreground = new SolidColorBrush(theme.Text);
            HubPortBackupHint.Foreground = new SolidColorBrush(theme.SubtleText);
            HubPortCustomLabel.Foreground = new SolidColorBrush(theme.Text);
            HubPortCustomHint.Foreground = new SolidColorBrush(theme.SubtleText);
            HubPortAutoSearchLabel.Foreground = new SolidColorBrush(theme.Text);
            HubPortAutoSearchHint.Foreground = new SolidColorBrush(theme.SubtleText);
            HubPortStatusTitle.Foreground = new SolidColorBrush(theme.Text);
            HubPortStatusBody.Foreground = new SolidColorBrush(theme.SubtleText);
        }

        private async void OnHubPortAutoSearchToggled(object sender, RoutedEventArgs e)
        {
            if (HubPortAutoSearchToggle == null)
            {
                return;
            }

            bool enabled = HubPortAutoSearchToggle.IsOn;
            await PortSettingsStore.SetAutoSearchAsync(enabled);

            bool isChinese = LocalizationManager.Current == UiLanguage.SimplifiedChinese;
            HubPortStatusMessage.Text = isChinese
                ? (enabled
                    ? "已开启自动切换端口，正在重启服务…"
                    : "已关闭自动切换端口，正在重启服务…")
                : (enabled
                    ? "Auto-search enabled. Restarting service…"
                    : "Auto-search disabled. Restarting service…");

            bool launched = await ServiceLauncher.LaunchAsync(PortSettingsStore.CurrentPort);
            if (!launched)
            {
                HubPortStatusMessage.Text = isChinese
                    ? "无法重启服务，请稍后重试。"
                    : "Failed to restart the service. Please retry.";
                return;
            }

            for (int attempt = 0; attempt < 8; attempt++)
            {
                await Task.Delay(250);
                int? reported = await TryGetServicePortAsync();
                if (reported.HasValue)
                {
                    break;
                }
            }

            await RefreshPortStateAsync();
            await TryRefreshCounterStrikeCfgAsync();
            HubPortStatusMessage.Text = isChinese
                ? (enabled
                    ? "自动切换已开启。服务运行后，端口将自动顺延。"
                    : "自动切换已关闭。")
                : (enabled
                    ? "Auto-search on. The service will pick the next free port on bind."
                    : "Auto-search off.");
        }

        private async void OnHubPortRefreshClick(object sender, RoutedEventArgs e)
        {
            await RefreshPortStateAsync();
        }

        private async void OnHubPortCustomApplyClick(object sender, RoutedEventArgs e)
        {
            string text = HubPortCustomInput?.Text ?? string.Empty;
            if (!PortSettingsStore.TryParsePort(text, out int port))
            {
                bool isChinese = LocalizationManager.Current == UiLanguage.SimplifiedChinese;
                HubPortStatusMessage.Text = isChinese
                    ? $"无效端口：{text}（需要 1024-65535）"
                    : $"Invalid port: {text} (expected 1024-65535)";
                return;
            }

            await ApplyPortAsync(port);
        }

        internal async Task RefreshPortStateAsync()
        {
            int current = PortSettingsStore.CurrentPort;
            HubPortCurrentValue.Text = current.ToString();
            await RefreshGameSourceDetailsAsync();
            UpdatePortButtons();
            UpdatePortStatusText();
            int? reported = await TryGetServicePortAsync();
            bool isChinese = LocalizationManager.Current == UiLanguage.SimplifiedChinese;
            if (reported.HasValue)
            {
                HubPortServiceReportedValue.Text = isChinese
                    ? $"服务报告端口：{reported.Value}"
                    : $"Service reports: {reported.Value}";
                if (reported.Value != current)
                {
                    HubPortServiceReportedValue.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0xFF, 0xC0, 0x66));
                }
                else
                {
                    HubPortServiceReportedValue.Foreground = (SolidColorBrush)Resources["HubBodyText"];
                }
            }
            else
            {
                HubPortServiceReportedValue.Text = isChinese
                    ? "服务未在运行"
                    : "Service not running";
                HubPortServiceReportedValue.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0xFF, 0x9F, 0x9F));
            }
        }

        private async Task<int?> TryGetServicePortAsync()
        {
            try
            {
                using (var client = await LocalServiceAuth.CreateHttpClientAsync())
                {
                    var response = await client.GetAsync(LocalServiceEndpoints.Build("/port"));
                    if (!response.IsSuccessStatusCode)
                    {
                        return null;
                    }

                    string body = await response.Content.ReadAsStringAsync();
                    var json = JsonObject.Parse(body);
                    if (json.ContainsKey("port") && json["port"].ValueType == JsonValueType.Number)
                    {
                        return (int)json["port"].GetNumber();
                    }
                }
            }
            catch
            {
            }

            return null;
        }

        private void RefreshPortButtons()
        {
            UpdatePortButtons();
        }

        private void UpdatePortButtons()
        {
            _portButtons.Clear();
            int current = PortSettingsStore.CurrentPort;
            HubPortCurrentBadgeText.Text = string.Format(PortBadgeFormat(), current);

            foreach (int port in PortSettingsStore.BackupPorts)
            {
                _portButtons.Add(new PortButtonBinding
                {
                    Port = port,
                    Label = port.ToString(),
                    IsCurrent = port == current
                });
            }
        }

        private string PortBadgeFormat()
        {
            bool isChinese = LocalizationManager.Current == UiLanguage.SimplifiedChinese;
            return isChinese ? "当前：{0}" : "Active: {0}";
        }

        private void UpdatePortStatusText()
        {
            bool isChinese = LocalizationManager.Current == UiLanguage.SimplifiedChinese;
            int current = PortSettingsStore.CurrentPort;
            HubPortStatusMessage.Text = isChinese
                ? $"当前端口 {current} — 状态将在点击“重新检测”后更新。"
                : $"Active port {current} - status updates after tapping Refresh.";
        }

        private async void OnPortChipClick(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is PortButtonBinding binding)
            {
                await ApplyPortAsync(binding.Port);
            }
        }

        private async Task ApplyPortAsync(int port)
        {
            bool isChinese = LocalizationManager.Current == UiLanguage.SimplifiedChinese;
            HubPortStatusMessage.Text = isChinese
                ? $"正在切换到 {port}..."
                : $"Switching to {port}...";

            bool launched = await ServiceLauncher.LaunchAsync(port);
            if (!launched)
            {
                HubPortStatusMessage.Text = isChinese
                    ? $"无法启动服务（端口 {port}）。请稍后重试。"
                    : $"Failed to launch service on port {port}. Please retry.";
                return;
            }

            // Give the service a moment to bind and expose /port.
            for (int attempt = 0; attempt < 8; attempt++)
            {
                await Task.Delay(250);
                int? reported = await TryGetServicePortAsync();
                if (reported.HasValue && reported.Value == port)
                {
                    break;
                }
            }

            await RefreshPortStateAsync();
            UpdatePortButtons();
            await TryRefreshCounterStrikeCfgAsync();
            HubPortStatusMessage.Text = isChinese
                ? $"端口 {port} 已生效，cfg 文件已同步更新。"
                : $"Port {port} is now active; the cfg file was refreshed.";
        }

        private void SelectLolPollIntervalUi()
        {
            if (LolPollIntervalSelector == null) return;
            _suppressLolPollEvents = true;
            try
            {
                int ms = GameSourceSettingsStore.LolPollIntervalMs;
                string tag = (ms == 50 || ms == 100 || ms == 250 || ms == 500) ? ms.ToString() : "custom";
                foreach (object obj in LolPollIntervalSelector.Items)
                {
                    if (obj is ComboBoxItem item && string.Equals(item.Tag as string, tag, StringComparison.OrdinalIgnoreCase))
                    {
                        LolPollIntervalSelector.SelectedItem = item;
                        break;
                    }
                }
                bool isCustom = tag == "custom";
                if (LolPollCustomInput != null) { LolPollCustomInput.Visibility = isCustom ? Visibility.Visible : Visibility.Collapsed; LolPollCustomInput.Text = ms.ToString(); }
                if (LolPollCustomApplyButton != null) LolPollCustomApplyButton.Visibility = isCustom ? Visibility.Visible : Visibility.Collapsed;
            }
            finally { _suppressLolPollEvents = false; }
        }
        private async void OnLolPollIntervalSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressLolPollEvents || !(LolPollIntervalSelector?.SelectedItem is ComboBoxItem item) || !(item.Tag is string tag)) return;
            bool isCustom = string.Equals(tag, "custom", StringComparison.OrdinalIgnoreCase);
            if (LolPollCustomInput != null) LolPollCustomInput.Visibility = isCustom ? Visibility.Visible : Visibility.Collapsed;
            if (LolPollCustomApplyButton != null) LolPollCustomApplyButton.Visibility = isCustom ? Visibility.Visible : Visibility.Collapsed;
            if (!isCustom && int.TryParse(tag, out int ms))
            {
                GameSourceSettingsStore.LolPollIntervalMs = ms;
                try { await GameSourceSettingsStore.SyncLolSettingsAsync(); } catch { }
                await RefreshGameSourceDetailsAsync();
            }
        }
        private async void OnLolPollCustomApplyClick(object sender, RoutedEventArgs e)
        {
            if (LolPollCustomInput == null || !int.TryParse(LolPollCustomInput.Text?.Trim(), out int ms)) return;
            GameSourceSettingsStore.LolPollIntervalMs = Math.Max(20, Math.Min(1000, ms));
            SelectLolPollIntervalUi();
            try { await GameSourceSettingsStore.SyncLolSettingsAsync(); } catch { }
            await RefreshGameSourceDetailsAsync();
        }
        private async void OnLolSelectRootClick(object sender, RoutedEventArgs e)
        {
            var picker = new Windows.Storage.Pickers.FolderPicker { SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.ComputerFolder, ViewMode = Windows.Storage.Pickers.PickerViewMode.List };
            picker.FileTypeFilter.Add("*");
            Windows.Storage.StorageFolder folder = await picker.PickSingleFolderAsync();
            if (folder == null) return;
            GameSourceSettingsStore.LolInstallRootPath = folder.Path;
            await RefreshGameSourceDetailsAsync();
        }
        private async void OnCsSelectRootClick(object sender, RoutedEventArgs e)
        {
            var picker = new Windows.Storage.Pickers.FolderPicker { SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.ComputerFolder, ViewMode = Windows.Storage.Pickers.PickerViewMode.List };
            picker.FileTypeFilter.Add("*");
            Windows.Storage.StorageFolder folder = await picker.PickSingleFolderAsync();
            if (folder == null) return;
            bool isLegacy = GameSourceSettingsStore.Current == "csgo_legacy";
            ApplicationData.Current.LocalSettings.Values[isLegacy ? "CsgoLegacyInstallFolderPath" : "CsInstallFolderPath"] = folder.Path;
            await RefreshGameSourceDetailsAsync();
        }
        private async void OnCsInstallCfgClick(object sender, RoutedEventArgs e)
        {
            await TryRefreshCounterStrikeCfgAsync();
            await RefreshGameSourceDetailsAsync();
        }
        private async Task RefreshGameSourceDetailsAsync()
        {
            bool zh = LocalizationManager.Current == UiLanguage.SimplifiedChinese;
            if (GameSourceSettingsStore.IsLol)
            {
                SelectLolPollIntervalUi();
                string lolRoot = GameSourceSettingsStore.LolInstallRootPath;
                if (LolRootPathText != null) LolRootPathText.Text = string.IsNullOrWhiteSpace(lolRoot) ? (zh ? "游戏根目录：未手动指定（LOL 对局通过本地 2999 端口自动连接，无需依赖目录）" : "Game root: Not specified (auto-connects via local port 2999)") : (zh ? $"游戏根目录：{lolRoot}" : $"Game root: {lolRoot}");
                bool active = false;
                try
                {
                    using (var client = await LocalServiceAuth.CreateHttpClientAsync())
                    using (var resp = await client.GetAsync(LocalServiceEndpoints.Build("/game-source/status")))
                    {
                        if (resp.IsSuccessStatusCode)
                        {
                            var j = JsonObject.Parse(await resp.Content.ReadAsStringAsync());
                            active = j.GetNamedNumber("posts", 0) > 0 && j.ContainsKey("last_post_age_ms") && j["last_post_age_ms"].ValueType == JsonValueType.Number && j["last_post_age_ms"].GetNumber() <= 120000;
                        }
                    }
                }
                catch { }
                int pollMs = GameSourceSettingsStore.LolPollIntervalMs;
                if (LolEndpointStatusText != null) LolEndpointStatusText.Text = zh ? $"检测端口：https://127.0.0.1:2999/liveclientdata/allgamedata · 抓取间隔 {pollMs}ms · {(active ? "对局已连接" : "等待进入对局")}" : $"Endpoint: https://127.0.0.1:2999/liveclientdata/allgamedata · {pollMs}ms · {(active ? "Connected" : "Waiting for match")}";
                return;
            }
            bool isLegacy = GameSourceSettingsStore.Current == "csgo_legacy";
            string versionParam = isLegacy ? "csgo_legacy" : "cs2";
            string savedRoot = ApplicationData.Current.LocalSettings.Values[isLegacy ? "CsgoLegacyInstallFolderPath" : "CsInstallFolderPath"] as string ?? string.Empty;
            string cfgStatus = "unknown";
            try
            {
                using (var client = await LocalServiceAuth.CreateHttpClientAsync())
                using (var resp = await client.GetAsync(LocalServiceEndpoints.Build("/counter-strike/root?version=" + versionParam)))
                {
                    if (resp.IsSuccessStatusCode)
                    {
                        var j = JsonObject.Parse(await resp.Content.ReadAsStringAsync());
                        string detected = j.GetNamedString("path", string.Empty);
                        cfgStatus = j.GetNamedString("cfg_status", "unknown");
                        if (!string.IsNullOrWhiteSpace(detected)) savedRoot = detected;
                    }
                }
            }
            catch { }
            string cfgSub = isLegacy ? "csgo\\cfg" : "game\\csgo\\cfg";
            string cfgDir = string.IsNullOrWhiteSpace(savedRoot) ? (isLegacy ? "<CS:GO>\\csgo\\cfg" : "<CS2>\\game\\csgo\\cfg") : System.IO.Path.Combine(savedRoot, cfgSub);
            if (CsRootPathText != null) CsRootPathText.Text = string.IsNullOrWhiteSpace(savedRoot) ? (zh ? "游戏根目录：未检测到，请点击右侧“选择根目录”" : "Game root: Not detected, click Select root") : (zh ? $"游戏根目录：{savedRoot}" : $"Game root: {savedRoot}");
            if (CsCfgDirPathText != null) CsCfgDirPathText.Text = zh ? $"CFG 目录位置：{cfgDir}" : $"CFG directory: {cfgDir}";
            string statusDesc = cfgStatus == "ready" ? (zh ? "已存放且为最新 (gamestate_integration_killconfirm.cfg)" : "Installed and up to date (gamestate_integration_killconfirm.cfg)") : cfgStatus == "outdated" ? (zh ? "文件已存在但端口/版本需更新，请点击“一键写入/修复 CFG”" : "Outdated, click Install / Repair CFG") : (zh ? "未检测到 gamestate_integration_killconfirm.cfg，请点击“一键写入/修复 CFG”" : "Missing gamestate_integration_killconfirm.cfg");
            if (CsCfgStatusText != null) CsCfgStatusText.Text = zh ? $"CFG 文件存放情况：{statusDesc}" : $"CFG file status: {statusDesc}";
        }
        private static async Task TryRefreshCounterStrikeCfgAsync()
        {
            try
            {
                using (var client = await LocalServiceAuth.CreateHttpClientAsync())
                {
                    var uri = LocalServiceEndpoints.Build("/counter-strike/cfg?version=cs2");
                    await client.PostAsync(uri, new HttpStringContent(string.Empty));
                }
            }
            catch (Exception ex)
            {
                App.Log("Failed to refresh cfg after port change: " + ex.Message);
            }
        }

        private sealed class PortButtonBinding
        {
            public int Port { get; set; }
            public string Label { get; set; }
            public bool IsCurrent { get; set; }
        }
    }
}

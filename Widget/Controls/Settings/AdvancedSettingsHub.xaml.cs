using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using KillConfirmGameBar.Controls.GameStyles;
using KillConfirmGameBar.Services;
using Windows.Data.Json;
using Windows.Storage.Streams;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.Web.Http;

namespace KillConfirmGameBar.Controls.Settings
{
    public sealed partial class AdvancedSettingsHub : UserControl
    {
        private const string TabGeneral = "general";
        private const string TabPort = "port";
        private const string TabDisplay = "display";
        private const string TabAbout = "about";

        private string _activeTab = TabGeneral;
        private readonly ObservableCollection<PortButtonBinding> _portButtons = new ObservableCollection<PortButtonBinding>();

        public AdvancedSettingsHub()
        {
            InitializeComponent();
            HubPortBackupList.ItemsSource = _portButtons;
            Loaded += OnLoaded;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            ApplyLanguage();
            ApplyTheme(GameThemePalette.Current);
            if (HubPortAutoSearchToggle != null)
            {
                HubPortAutoSearchToggle.IsOn = PortSettingsStore.AutoSearchEnabled;
            }
            await RefreshPortStateAsync();
        }

        internal void ApplyLanguage()
        {
            bool isChinese = LocalizationManager.Current == UiLanguage.SimplifiedChinese;

            if (HubHeaderBadgeText != null) HubHeaderBadgeText.Text = isChinese ? "高级设置中心" : "ADVANCED CONTROL HUB";
            if (HubHeaderTitleText != null) HubHeaderTitleText.Text = isChinese ? "高级设置" : "Advanced Settings";
            if (HubHeaderSubtitleText != null) HubHeaderSubtitleText.Text = isChinese
                ? "在一个页面集中调整运行时、网络端口与显示偏好。"
                : "Tune runtime behaviour, network endpoints, and presentation in one place.";
            if (HubStatusBadgeText != null) HubStatusBadgeText.Text = isChinese ? "服务就绪" : "SERVICE READY";

            if (HubTabGeneralButton != null) HubTabGeneralButton.Content = isChinese ? "通用" : "General";
            if (HubTabPortButton != null) HubTabPortButton.Content = isChinese ? "端口" : "Port";
            if (HubTabDisplayButton != null) HubTabDisplayButton.Content = isChinese ? "显示" : "Display";
            if (HubTabAboutButton != null) HubTabAboutButton.Content = isChinese ? "关于" : "About";

            HubGeneralCardTitle.Text = isChinese ? "运行时与维护" : "RUNTIME & MAINTENANCE";
            HubGeneralCardDescription.Text = isChinese
                ? "更新 GSI 配置文件、重启服务、关闭窗口行为等。"
                : "Update the GSI config, restart the service, and configure window-close behaviour.";
            HubGeneralCardSecondaryTitle.Text = isChinese ? "游戏状态集成" : "GAME-STATE INTEGRATION";
            HubGeneralCardSecondaryDescription.Text = isChinese
                ? "选择 CF2 或 CS:GO legacy 模式，调整广播、轰炸音效等选项。"
                : "Switch between CS2 and CS:GO legacy mode, plus broadcast and bomb-audio options.";

            HubPortTitleText.Text = isChinese ? "本地服务端口" : "LOCAL SERVICE PORT";
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
                ? "下面是最近一次端口探测结果。"
                : "Latest port probe result.";

            HubDisplayCardTitle.Text = isChinese ? "显示与缩放" : "DISPLAY & SCALING";
            HubDisplayCardDescription.Text = isChinese
                ? "高分辨率显示器下放大控制面板，方便点击。"
                : "Scale up the control panel on high-resolution displays for easier clicking.";

            HubAboutTitle.Text = isChinese ? "关于本版本" : "ABOUT THIS BUILD";
            HubAboutBody.Text = isChinese
                ? "KillConfirm FIX 高级设置中心 — 集中管理端口、显示与维护选项。"
                : "KillConfirm FIX Advanced Hub — centralised port, display, and maintenance controls.";
            HubAboutVersion.Text = isChinese
                ? "Widget 通过 LocalService 端点与后台服务通信，所有端口变更都会同步到 gamestate_integration 配置。"
                : "The widget talks to the companion service through LocalService endpoints. Every port change syncs into the gamestate_integration config.";

            RefreshPortButtons();
            UpdatePortStatusText();
        }

        internal void ApplyTheme(GameThemePalette theme)
        {
            if (HubGeneralCardTitle != null) HubGeneralCardTitle.Foreground = new SolidColorBrush(theme.Text);
            if (HubGeneralCardDescription != null) HubGeneralCardDescription.Foreground = new SolidColorBrush(theme.MutedText);
            if (HubGeneralCardSecondaryTitle != null) HubGeneralCardSecondaryTitle.Foreground = new SolidColorBrush(theme.Text);
            if (HubGeneralCardSecondaryDescription != null) HubGeneralCardSecondaryDescription.Foreground = new SolidColorBrush(theme.MutedText);

            if (HubPortTitleText != null) HubPortTitleText.Foreground = new SolidColorBrush(theme.Text);
            if (HubPortDescriptionText != null) HubPortDescriptionText.Foreground = new SolidColorBrush(theme.MutedText);
            if (HubPortCurrentLabel != null) HubPortCurrentLabel.Foreground = new SolidColorBrush(theme.Text);
            if (HubPortCurrentValue != null) HubPortCurrentValue.Foreground = new SolidColorBrush(theme.Secondary);
            if (HubPortBackupLabel != null) HubPortBackupLabel.Foreground = new SolidColorBrush(theme.Text);
            if (HubPortBackupHint != null) HubPortBackupHint.Foreground = new SolidColorBrush(theme.SubtleText);
            if (HubPortCustomLabel != null) HubPortCustomLabel.Foreground = new SolidColorBrush(theme.Text);
            if (HubPortCustomHint != null) HubPortCustomHint.Foreground = new SolidColorBrush(theme.SubtleText);
            if (HubPortAutoSearchLabel != null) HubPortAutoSearchLabel.Foreground = new SolidColorBrush(theme.Text);
            if (HubPortAutoSearchHint != null) HubPortAutoSearchHint.Foreground = new SolidColorBrush(theme.SubtleText);
            if (HubPortStatusTitle != null) HubPortStatusTitle.Foreground = new SolidColorBrush(theme.Text);
            if (HubPortStatusBody != null) HubPortStatusBody.Foreground = new SolidColorBrush(theme.SubtleText);

            if (HubDisplayCardTitle != null) HubDisplayCardTitle.Foreground = new SolidColorBrush(theme.Text);
            if (HubDisplayCardDescription != null) HubDisplayCardDescription.Foreground = new SolidColorBrush(theme.MutedText);

            if (HubAboutTitle != null) HubAboutTitle.Foreground = new SolidColorBrush(theme.Text);
            if (HubAboutBody != null) HubAboutBody.Foreground = new SolidColorBrush(theme.MutedText);
            if (HubAboutVersion != null) HubAboutVersion.Foreground = new SolidColorBrush(theme.SubtleText);

            if (HubRuntimePanel != null)
            {
                HubRuntimePanel.ApplyTheme(theme);
            }
            if (HubGeneralOptionsPanel != null)
            {
                HubGeneralOptionsPanel.ApplyTheme(theme);
            }
            if (HubDisplayScalingPanel != null)
            {
                HubDisplayScalingPanel.ApplyTheme(theme);
            }
        }

        private void OnHubTabGeneralClick(object sender, RoutedEventArgs e) => SwitchTab(TabGeneral);
        private void OnHubTabPortClick(object sender, RoutedEventArgs e) => SwitchTab(TabPort);
        private void OnHubTabDisplayClick(object sender, RoutedEventArgs e) => SwitchTab(TabDisplay);
        private void OnHubTabAboutClick(object sender, RoutedEventArgs e) => SwitchTab(TabAbout);

        public void SwitchTab(string tab)
        {
            _activeTab = tab;
            bool isChinese = LocalizationManager.Current == UiLanguage.SimplifiedChinese;

            if (HubTabGeneralButton != null) HubTabGeneralButton.Style = (Style)Resources[tab == TabGeneral ? "HubTabActiveButtonStyle" : "HubTabButtonStyle"];
            if (HubTabPortButton != null) HubTabPortButton.Style = (Style)Resources[tab == TabPort ? "HubTabActiveButtonStyle" : "HubTabButtonStyle"];
            if (HubTabDisplayButton != null) HubTabDisplayButton.Style = (Style)Resources[tab == TabDisplay ? "HubTabActiveButtonStyle" : "HubTabButtonStyle"];
            if (HubTabAboutButton != null) HubTabAboutButton.Style = (Style)Resources[tab == TabAbout ? "HubTabActiveButtonStyle" : "HubTabButtonStyle"];

            if (HubGeneralView != null) HubGeneralView.Visibility = tab == TabGeneral ? Visibility.Visible : Visibility.Collapsed;
            if (HubPortView != null) HubPortView.Visibility = tab == TabPort ? Visibility.Visible : Visibility.Collapsed;
            if (HubDisplayView != null) HubDisplayView.Visibility = tab == TabDisplay ? Visibility.Visible : Visibility.Collapsed;
            if (HubAboutView != null) HubAboutView.Visibility = tab == TabAbout ? Visibility.Visible : Visibility.Collapsed;

            if (tab == TabPort)
            {
                _ = RefreshPortStateAsync();
            }

            // Suppress unused warning - the tooltip uses isChinese in case we expand it later.
            _ = isChinese;
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

        private async Task RefreshPortStateAsync()
        {
            int current = PortSettingsStore.CurrentPort;
            HubPortCurrentValue.Text = current.ToString();
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

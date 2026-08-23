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
                ? "在一个页面集中调整游戏体验、网络端口与进阶系统设置。"
                : "Tune game experience, network endpoints, and advanced system options in one place.";
            if (HubStatusBadgeText != null) HubStatusBadgeText.Text = isChinese ? "服务就绪" : "SERVICE READY";

            if (HubTabGeneralButton != null) HubTabGeneralButton.Content = isChinese ? "体验" : "Experience";
            if (HubTabPortButton != null) HubTabPortButton.Content = isChinese ? "端口" : "Port";
            if (HubTabDisplayButton != null) HubTabDisplayButton.Content = isChinese ? "进阶" : "Advanced";
            if (HubTabAboutButton != null) HubTabAboutButton.Content = isChinese ? "关于" : "About";

            HubExperienceCardTitle.Text = isChinese ? "游戏体验增强" : "GAME EXPERIENCE";
            HubExperienceCardDescription.Text = isChinese
                ? "观战击杀特效、游戏退出自动关闭、击杀语音打断与 C4 炸弹音效等对局体验选项。"
                : "In-match experience options: spectated kill effects, auto-close on game exit, kill-voice interrupt, and C4 bomb audio.";

            HubGeneralCardTitle.Text = isChinese ? "软件与维护" : "APP & MAINTENANCE";
            HubGeneralCardDescription.Text = isChinese
                ? "更新游戏数据配置、重启本地服务或查看日志。"
                : "Update the game-data configuration, restart the local service, or view logs.";
            HubGeneralCardSecondaryTitle.Text = isChinese
                ? "游戏与系统选项"
                : "GAME & SYSTEM";
            HubGeneralCardSecondaryDescription.Text = isChinese
                ? "设置 Counter-Strike 版本、主窗口关闭方式和程序运行优先级。"
                : "Choose the Counter-Strike version, main-window close behavior, and program priority.";

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
                ? "下面是最近一次连接检查结果。"
                : "Latest connection check result.";

            HubDisplayCardTitle.Text = isChinese ? "进阶设置" : "ADVANCED SETTINGS";
            HubDisplayCardDescription.Text = isChinese
                ? "可在这里调整 Counter-Strike 版本和软件维护选项。"
                : "Adjust the Counter-Strike version and app maintenance options here.";

            HubAboutTitle.Text = isChinese ? "关于本版本" : "ABOUT THIS BUILD";
            HubAboutBody.Text = isChinese
                ? "Kill Confirm Overlay 是 Xbox Game Bar 的击杀提示工具，可根据比赛中的击杀信息显示动画并播放语音。程序不会修改游戏文件或游戏进程。"
                : "Kill Confirm Overlay is a kill-feedback tool for Xbox Game Bar. It shows animations and plays audio from match information without modifying game files or the game process.";
            var packageVersion = Windows.ApplicationModel.Package.Current.Id.Version;
            string versionText = $"{packageVersion.Major}.{packageVersion.Minor}.{packageVersion.Build}.{packageVersion.Revision}";
            HubAboutVersion.Text = isChinese
                ? $"当前版本 {versionText}"
                : $"Current version {versionText}";
            HubExitAllButton.Content = isChinese ? "完全退出本程序" : "Exit completely";
            HubUninstallButton.Content = isChinese ? "卸载" : "Uninstall";

            HubAboutUpdateTitle.Text = isChinese ? "更新信息" : "WHAT'S NEW";
            HubAboutUpdateBody.Text = isChinese
                ? "· 新增：全新高级设置页面与端口快速切换\n"
                    + "· 新增：C4 炸弹倒计时与音效自定义、面板配色自定义、CSOL 资源包导入\n"
                    + "· 优化：设置页签重组为「游戏体验增强」与「进阶设置」，常用体验选项一键直达"
                : "• New: redesigned advanced settings page with quick port switching\n"
                    + "• New: C4 bomb timer audio customization, panel color customizer, CSOL pack import\n"
                    + "• Improved: settings tabs reorganized into Game Experience and Advanced";

            HubAboutCreditsTitle.Text = isChinese ? "作者与致谢" : "AUTHOR & CREDITS";
            HubAuthorNameText.Text = "Zac · eachkinji";
            HubAuthorDescriptionText.Text = isChinese
                ? "Kill Confirm Overlay 作者与维护者"
                : "Author and maintainer of Kill Confirm Overlay";
            HubAuthorGitHubButton.Content = "GitHub · eachkinji";
            HubAuthorBilibiliButton.Content = isChinese ? "B站 · Zac不想烤肉" : "Bilibili · Zac";
            HubProjectButton.Content = isChinese ? "项目主页" : "Project home";
            HubDownloadButton.Content = isChinese ? "下载与更新 · 7Twv" : "Download & update · 7Twv";
            HubAboutCreditsBody.Text = isChinese
                ? "感谢 st0nie 提供 cskillconfirm 的开发思路与基础代码，并感谢 gufan0000 的 CS2 Customizer 与本项目持续联动。本工具为非官方社区项目，仅供学习交流；游戏资源归各自版权方所有。"
                : "Thanks to st0nie for the ideas and foundation from cskillconfirm, and to gufan0000 for the ongoing CS2 Customizer collaboration. This is an unofficial community project for learning and personal use; game assets belong to their respective owners.";
            HubCreditsCommunityPanel?.ApplyLanguage();

            HubGeneralOptionsPanel?.ApplyLanguage();
            HubAdvancedSystemOptionsPanel?.ApplyLanguage();
            HubRuntimePanel?.ApplyLanguage();

            RefreshPortButtons();
            UpdatePortStatusText();
        }

        internal void ApplyTheme(GameThemePalette theme)
        {
            if (HubExperienceCardTitle != null) HubExperienceCardTitle.Foreground = new SolidColorBrush(theme.Text);
            if (HubExperienceCardDescription != null) HubExperienceCardDescription.Foreground = new SolidColorBrush(theme.MutedText);
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
            if (HubUninstallButton != null)
            {
                HubUninstallButton.Background = theme.Brush(theme.Card);
                HubUninstallButton.BorderBrush = theme.Brush(theme.SoftBorder);
                HubUninstallButton.Foreground = theme.Brush(theme.Text);
            }
            if (HubAppActionsStatusText != null) HubAppActionsStatusText.Foreground = theme.Brush(theme.MutedText);
            if (HubAboutUpdateTitle != null) HubAboutUpdateTitle.Foreground = new SolidColorBrush(theme.Text);
            if (HubAboutUpdateBody != null) HubAboutUpdateBody.Foreground = new SolidColorBrush(theme.MutedText);
            if (HubAboutCreditsTitle != null) HubAboutCreditsTitle.Foreground = new SolidColorBrush(theme.Text);
            if (HubAboutCreditsBody != null) HubAboutCreditsBody.Foreground = new SolidColorBrush(theme.MutedText);
            if (HubAuthorProfileCard != null)
            {
                HubAuthorProfileCard.Background = theme.Brush(theme.SubtleField);
                HubAuthorProfileCard.BorderBrush = theme.Brush(theme.SoftBorder);
            }
            if (HubAuthorAvatarFrame != null)
            {
                HubAuthorAvatarFrame.Background = theme.Brush(theme.Card);
                HubAuthorAvatarFrame.BorderBrush = theme.Brush(theme.SoftBorder);
            }
            if (HubAuthorNameText != null) HubAuthorNameText.Foreground = theme.Brush(theme.Text);
            if (HubAuthorDescriptionText != null) HubAuthorDescriptionText.Foreground = theme.Brush(theme.MutedText);
            if (HubCreditsCommunityPanel != null) HubCreditsCommunityPanel.ApplyTheme(theme);

            if (HubRuntimePanel != null)
            {
                HubRuntimePanel.ApplyTheme(theme);
            }
            if (HubGeneralOptionsPanel != null)
            {
                HubGeneralOptionsPanel.ApplyTheme(theme);
            }
            if (HubAdvancedSystemOptionsPanel != null)
            {
                HubAdvancedSystemOptionsPanel.ApplyTheme(theme);
            }
        }

        private void OnHubTabGeneralClick(object sender, RoutedEventArgs e) => SwitchTab(TabGeneral);
        private void OnHubTabPortClick(object sender, RoutedEventArgs e) => SwitchTab(TabPort);
        private void OnHubTabDisplayClick(object sender, RoutedEventArgs e) => SwitchTab(TabDisplay);
        private void OnHubTabAboutClick(object sender, RoutedEventArgs e) => SwitchTab(TabAbout);

        private async void OnHubExitAllClick(object sender, RoutedEventArgs e)
        {
            bool isChinese = LocalizationManager.Current == UiLanguage.SimplifiedChinese;
            var confirmation = new ContentDialog
            {
                Title = isChinese ? "完全退出本程序？" : "Exit Kill Confirm Overlay?",
                Content = isChinese
                    ? "这会关闭所有前台窗口和后台服务。"
                    : "This closes every Kill Confirm Overlay window and background service.",
                PrimaryButtonText = isChinese ? "完全退出" : "Exit",
                CloseButtonText = isChinese ? "取消" : "Cancel"
            };

            if (await confirmation.ShowAsync() != ContentDialogResult.Primary)
            {
                return;
            }

            HubExitAllButton.IsEnabled = false;
            HubUninstallButton.IsEnabled = false;
            bool launched = await App.RequestFullExitAsync();
            if (launched)
            {
                Application.Current.Exit();
                return;
            }

            HubExitAllButton.IsEnabled = true;
            HubUninstallButton.IsEnabled = true;
            ShowAppActionStatus(isChinese
                ? "无法启动退出程序，请稍后重试。"
                : "Could not start the exit helper. Please retry.");
        }

        private async void OnHubUninstallClick(object sender, RoutedEventArgs e)
        {
            bool isChinese = LocalizationManager.Current == UiLanguage.SimplifiedChinese;
            HubUninstallButton.IsEnabled = false;
            bool launched = await KillConfirmWidgetPage.TryLaunchFullTrustHelperAsync(
                KillConfirmWidgetPage.OpenUninstallerParameterGroupId);
            HubUninstallButton.IsEnabled = true;
            ShowAppActionStatus(launched
                ? (isChinese ? "已打开卸载程序。" : "The uninstaller was opened.")
                : (isChinese ? "无法打开卸载程序。" : "Could not open the uninstaller."));
        }

        private void ShowAppActionStatus(string message)
        {
            HubAppActionsStatusText.Text = message;
            HubAppActionsStatusText.Visibility = Visibility.Visible;
        }

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

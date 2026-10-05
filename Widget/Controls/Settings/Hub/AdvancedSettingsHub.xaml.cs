using System;
using KillConfirmGameBar.Services;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace KillConfirmGameBar.Controls.Settings
{
    public sealed partial class AdvancedSettingsHub : UserControl
    {
        private const string TabGeneral = "general";
        private const string TabPort = "port";
        private const string TabDisplay = "display";
        private const string TabAbout = "about";

        private string _activeTab = TabGeneral;
        private readonly DispatcherTimer _gameBarStatusTimer;
        private string _gameBarActionMessage;
        private DateTimeOffset _gameBarActionMessageExpiresAt;

        public AdvancedSettingsHub()
        {
            InitializeComponent();
            _gameBarStatusTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _gameBarStatusTimer.Tick += OnGameBarStatusTimerTick;
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }
        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            ApplyLanguage();
            ApplyTheme(GameThemePalette.Current);
            RefreshGameBarStatus();
            _gameBarStatusTimer.Start();
            await HubPortSettingsView.InitializeAsync();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            _gameBarStatusTimer.Stop();
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

            HubGameBarStatusTitle.Text = isChinese ? "GAME BAR 使用状态" : "GAME BAR STATUS";
            HubGameBarStatusDescription.Text = isChinese
                ? "打开 Win+G 后，这里会实时显示固定与单击浏览状态。"
                : "Open Win+G to see the live pin and click-through state.";
            HubGameBarCardTitle.Text = isChinese
                ? "Kill Confirm Overlay 小组件"
                : "Kill Confirm Overlay widget";
            HubWidgetStatusTitle.Text = isChinese ? "小组件状态" : "Widget status";
            HubPinStatusTitle.Text = isChinese ? "固定窗口" : "Pin widget";
            HubClickThroughStatusTitle.Text = isChinese ? "单击浏览" : "Click-through";
            HubOpenGameBarButton.Content = isChinese ? "打开 Game Bar" : "Open Game Bar";
            RefreshGameBarStatus();

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

            HubPortSettingsView?.ApplyLanguage();

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

        }
        internal void ApplyTheme(GameThemePalette theme)
        {
            if (HubExperienceCardTitle != null) HubExperienceCardTitle.Foreground = new SolidColorBrush(theme.Text);
            if (HubExperienceCardDescription != null) HubExperienceCardDescription.Foreground = new SolidColorBrush(theme.MutedText);
            if (HubGameBarStatusTitle != null) HubGameBarStatusTitle.Foreground = theme.Brush(theme.Text);
            if (HubGameBarStatusDescription != null) HubGameBarStatusDescription.Foreground = theme.Brush(theme.MutedText);
            if (HubGameBarStatusCard != null)
            {
                HubGameBarStatusCard.Background = theme.Brush(theme.Card);
                HubGameBarStatusCard.BorderBrush = theme.Brush(theme.SoftBorder);
            }
            if (HubGameBarCardTitle != null) HubGameBarCardTitle.Foreground = theme.Brush(theme.Text);
            if (HubGameBarCardSummary != null) HubGameBarCardSummary.Foreground = theme.Brush(theme.MutedText);
            if (HubWidgetStatusTitle != null) HubWidgetStatusTitle.Foreground = theme.Brush(theme.Text);
            if (HubWidgetStatusDetail != null) HubWidgetStatusDetail.Foreground = theme.Brush(theme.MutedText);
            if (HubPinStatusTitle != null) HubPinStatusTitle.Foreground = theme.Brush(theme.Text);
            if (HubPinStatusDetail != null) HubPinStatusDetail.Foreground = theme.Brush(theme.MutedText);
            if (HubClickThroughStatusTitle != null) HubClickThroughStatusTitle.Foreground = theme.Brush(theme.Text);
            if (HubClickThroughStatusDetail != null) HubClickThroughStatusDetail.Foreground = theme.Brush(theme.MutedText);
            if (HubOpenGameBarButton != null)
            {
                HubOpenGameBarButton.Background = theme.Brush(theme.Card);
                HubOpenGameBarButton.BorderBrush = theme.Brush(theme.SoftBorder);
                HubOpenGameBarButton.Foreground = theme.Brush(theme.Text);
            }
            if (HubGeneralCardTitle != null) HubGeneralCardTitle.Foreground = new SolidColorBrush(theme.Text);
            if (HubGeneralCardDescription != null) HubGeneralCardDescription.Foreground = new SolidColorBrush(theme.MutedText);
            if (HubGeneralCardSecondaryTitle != null) HubGeneralCardSecondaryTitle.Foreground = new SolidColorBrush(theme.Text);
            if (HubGeneralCardSecondaryDescription != null) HubGeneralCardSecondaryDescription.Foreground = new SolidColorBrush(theme.MutedText);

            HubPortSettingsView?.ApplyTheme(theme);

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

        private void OnGameBarStatusTimerTick(object sender, object e)
        {
            RefreshGameBarStatus();
        }

        private void RefreshGameBarStatus()
        {
            if (HubGameBarCardSummary == null)
            {
                return;
            }

            bool isChinese = LocalizationManager.Current == UiLanguage.SimplifiedChinese;
            GameBarRuntimeStatus status = GameBarRuntimeStatusStore.Read();
            if (!status.IsAvailable)
            {
                SetGameBarCardSummary(isChinese
                    ? "等待小组件上报状态"
                    : "Waiting for the widget to report its state");
                SetGameBarStatusRow(
                    HubWidgetStatusGlyph,
                    HubWidgetStatusDetail,
                    HubWidgetStatusBadge,
                    null,
                    isChinese ? "请按 Win+G 打开 Kill Confirm Overlay" : "Press Win+G and open Kill Confirm Overlay",
                    isChinese ? "未检测" : "Not detected");
                SetGameBarStatusRow(
                    HubPinStatusGlyph,
                    HubPinStatusDetail,
                    HubPinStatusBadge,
                    null,
                    isChinese ? "等待小组件状态" : "Waiting for widget state",
                    isChinese ? "未知" : "Unknown");
                SetGameBarStatusRow(
                    HubClickThroughStatusGlyph,
                    HubClickThroughStatusDetail,
                    HubClickThroughStatusBadge,
                    null,
                    isChinese ? "等待小组件状态" : "Waiting for widget state",
                    isChinese ? "未知" : "Unknown");
                return;
            }

            bool ready = status.IsPinned && status.IsClickThroughEnabled;
            SetGameBarCardSummary(ready
                ? (isChinese ? "Game Bar 配置正确" : "Game Bar is configured correctly")
                : (isChinese ? "还有项目需要处理" : "Some setup steps still need attention"));
            SetGameBarStatusRow(
                HubWidgetStatusGlyph,
                HubWidgetStatusDetail,
                HubWidgetStatusBadge,
                true,
                isChinese ? "正在接收实时状态" : "Receiving live state",
                isChinese ? "运行中" : "Running");
            SetGameBarStatusRow(
                HubPinStatusGlyph,
                HubPinStatusDetail,
                HubPinStatusBadge,
                status.IsPinned,
                status.IsPinned
                    ? (isChinese ? "窗口会保留在游戏画面上" : "The widget stays visible over the game")
                    : (isChinese ? "点击小组件右上角的图钉" : "Click the pin in the widget's top-right corner"),
                status.IsPinned
                    ? (isChinese ? "已固定" : "Pinned")
                    : (isChinese ? "未固定" : "Not pinned"));
            SetGameBarStatusRow(
                HubClickThroughStatusGlyph,
                HubClickThroughStatusDetail,
                HubClickThroughStatusBadge,
                status.IsClickThroughEnabled,
                status.IsClickThroughEnabled
                    ? (isChinese ? "单击浏览已关闭" : "Click-through is configured correctly")
                    : (isChinese ? "请在顶部工具栏关闭“单击浏览”" : "Configure click-through in the top toolbar"),
                status.IsClickThroughEnabled
                    ? (isChinese ? "已关闭" : "Ready")
                    : (isChinese ? "需要关闭" : "Action needed"));
        }

        private static void SetGameBarStatusRow(
            TextBlock glyph,
            TextBlock detail,
            TextBlock badge,
            bool? success,
            string detailText,
            string badgeText)
        {
            Color color = !success.HasValue
                ? Color.FromArgb(255, 110, 110, 110)
                : success.Value
                    ? Color.FromArgb(255, 16, 124, 16)
                    : Color.FromArgb(255, 196, 43, 28);
            glyph.Text = !success.HasValue ? "?" : success.Value ? "✓" : "×";
            glyph.Foreground = new SolidColorBrush(color);
            detail.Text = detailText;
            badge.Text = badgeText;
            badge.Foreground = new SolidColorBrush(color);
        }

        private async void OnOpenGameBarClick(object sender, RoutedEventArgs e)
        {
            bool isChinese = LocalizationManager.Current == UiLanguage.SimplifiedChinese;
            HubOpenGameBarButton.IsEnabled = false;
            HubOpenGameBarButton.Content = isChinese ? "正在打开…" : "Opening…";

            bool launched = false;
            try
            {
                // Launching ms-gamebar directly from a UWP control panel can
                // report success without displaying Game Bar on some Windows
                // builds. Prefer the packaged desktop helper and retain the
                // system launcher for installations where full-trust launch is
                // unavailable.
                launched = await KillConfirmWidgetPage.TryLaunchFullTrustHelperAsync(
                    KillConfirmWidgetPage.OpenGameBarParameterGroupId);
                if (!launched)
                {
                    launched = await Windows.System.Launcher.LaunchUriAsync(new Uri("ms-gamebar:"));
                }
            }
            catch (Exception ex)
            {
                App.Log("Failed to open Xbox Game Bar: " + ex);
            }
            finally
            {
                HubOpenGameBarButton.IsEnabled = true;
                HubOpenGameBarButton.Content = isChinese ? "打开 Game Bar" : "Open Game Bar";
            }

            _gameBarActionMessage = launched
                ? (isChinese ? "已发送 Game Bar 打开请求" : "Game Bar open request sent")
                : (isChinese ? "无法打开 Game Bar，请尝试按 Win+G" : "Could not open Game Bar. Try pressing Win+G.");
            _gameBarActionMessageExpiresAt = DateTimeOffset.UtcNow.AddSeconds(5);
            HubGameBarCardSummary.Text = _gameBarActionMessage;
        }

        private void SetGameBarCardSummary(string statusMessage)
        {
            HubGameBarCardSummary.Text = DateTimeOffset.UtcNow < _gameBarActionMessageExpiresAt
                ? _gameBarActionMessage
                : statusMessage;
        }

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
            if (HubPortSettingsView != null) HubPortSettingsView.Visibility = tab == TabPort ? Visibility.Visible : Visibility.Collapsed;
            if (HubDisplayView != null) HubDisplayView.Visibility = tab == TabDisplay ? Visibility.Visible : Visibility.Collapsed;
            if (HubAboutView != null) HubAboutView.Visibility = tab == TabAbout ? Visibility.Visible : Visibility.Collapsed;

            if (tab == TabPort)
            {
                _ = HubPortSettingsView.RefreshPortStateAsync();
            }

            // Suppress unused warning - the tooltip uses isChinese in case we expand it later.
            _ = isChinese;
        }

    }
}

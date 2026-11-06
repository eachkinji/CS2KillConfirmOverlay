using System;
using KillConfirmGameBar.Services;
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
        public AdvancedSettingsHub()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            Unloaded += (sender, args) => VersionUpdateService.Changed -= OnVersionStateChanged;
        }
        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            ApplyLanguage();
            RefreshMaterialLibrary();
            ApplyTheme(GameThemePalette.Home);
            VersionUpdateService.Changed += OnVersionStateChanged;
            OnVersionStateChanged(null, EventArgs.Empty);
            _ = VersionUpdateService.CheckAsync();
            await HubPortSettingsView.InitializeAsync();
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
                ? "选择要监听的 Counter-Strike 版本。"
                : "Choose the Counter-Strike version to monitor.";

            HubPortSettingsView?.ApplyLanguage();

            HubDisplayCardTitle.Text = isChinese ? "进阶设置" : "ADVANCED SETTINGS";

            HubAboutTitle.Text = isChinese ? "关于本版本" : "ABOUT THIS BUILD";
            var packageVersion = Windows.ApplicationModel.Package.Current.Id.Version;
            string versionText = $"{packageVersion.Major}.{packageVersion.Minor}.{packageVersion.Build}.{packageVersion.Revision}";
            HubAboutVersion.Text = isChinese
                ? $"当前版本 {versionText}"
                : $"Current version {versionText}";
            HubExitAllButton.Content = isChinese ? "完全退出本程序" : "Exit completely";
            HubUninstallButton.Content = isChinese ? "卸载" : "Uninstall";

            HubAboutUpdateTitle.Text = isChinese ? "公开版本更新说明" : "Public release notes";
            HubUpdateStepsTitle.Text = isChinese ? "如何更新" : "How to update";
            HubUpdateSteps.Text = isChinese
                ? "1  打开下载页面，下载最新版本的 EXE 安装包。\n2  运行安装包；安装器会关闭正在运行的本程序。\n3  按安装向导覆盖安装，无需先卸载；个人配置会保留。\n4  重新打开控制面板。使用 Game Bar 时，按 Win + G 打开小组件。"
                : "1  Open the download page and get the latest EXE installer.\n2  Run the installer; it closes running KillConfirm processes.\n3  Install over the existing version; no uninstall is needed and settings are retained.\n4  Reopen the panel. For Game Bar, press Win + G and open the widget.";
            HubCheckUpdateButton.Content = isChinese ? "检查更新" : "Check again";
            HubCopyUpdateLinkButton.Content = isChinese ? "复制网盘链接与提取码" : "Copy download link and code";
            OnVersionStateChanged(null, EventArgs.Empty);

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
            if (HubGeneralCardTitle != null) HubGeneralCardTitle.Foreground = new SolidColorBrush(theme.Text);
            if (HubGeneralCardDescription != null) HubGeneralCardDescription.Foreground = new SolidColorBrush(theme.MutedText);
            if (HubGeneralCardSecondaryTitle != null) HubGeneralCardSecondaryTitle.Foreground = new SolidColorBrush(theme.Text);
            if (HubGeneralCardSecondaryDescription != null) HubGeneralCardSecondaryDescription.Foreground = new SolidColorBrush(theme.MutedText);

            HubPortSettingsView?.ApplyTheme(theme);

            if (HubDisplayCardTitle != null) HubDisplayCardTitle.Foreground = new SolidColorBrush(theme.Text);

            if (HubAboutTitle != null) HubAboutTitle.Foreground = new SolidColorBrush(theme.Text);
            if (HubAboutVersion != null) HubAboutVersion.Foreground = new SolidColorBrush(theme.SubtleText);
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
        private void OnAboutHeaderSizeChanged(object sender, SizeChangedEventArgs e)
        {
            bool narrow = e.NewSize.Width < 520;
            Grid.SetColumn(HubVersionActions, narrow ? 0 : 1);
            Grid.SetRow(HubVersionActions, narrow ? 1 : 0);
            Grid.SetColumnSpan(HubVersionActions, narrow ? 2 : 1);
        }

        private async void OnCheckUpdateClick(object sender, RoutedEventArgs e) => await VersionUpdateService.CheckAsync(true);
        private void OnCopyUpdateLinkClick(object sender, RoutedEventArgs e)
        {
            var data = new Windows.ApplicationModel.DataTransfer.DataPackage();
            data.SetText(VersionUpdateService.DownloadUrl + "\n提取码 / Code: " + VersionUpdateService.DownloadCode);
            Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(data);
            HubUpdateLinkFeedback.Text = LocalizationManager.Current == UiLanguage.SimplifiedChinese ? "下载链接与提取码已复制。" : "Download link and code copied.";
            HubUpdateLinkFeedback.Visibility = Visibility.Visible;
        }
        private void OnVersionStateChanged(object sender, EventArgs e)
        {
            bool zh = LocalizationManager.Current == UiLanguage.SimplifiedChinese;
            HubVersionStatus.Text = VersionUpdateService.StatusText(zh);
            HubCheckUpdateButton.IsEnabled = VersionUpdateService.State != VersionUpdateState.Checking;
            string latest = string.IsNullOrEmpty(VersionUpdateService.LatestVersion) ? (zh ? "尚未获取" : "Not available") : VersionUpdateService.LatestVersion;
            HubVersionDetail.Text = (zh ? "当前版本 " : "Installed ") + VersionUpdateService.CurrentVersion + (zh ? "  ·  GitHub 公开版本 " : "  ·  Public release ") + latest;
            if (VersionUpdateService.State == VersionUpdateState.Ahead) HubVersionDetail.Text += zh ? "\n当前版本高于 GitHub 公开版，暂不需要更新。" : "\nYour installed version is newer than the public release; no update is needed.";
            if (VersionUpdateService.State == VersionUpdateState.Unavailable) HubVersionDetail.Text += zh ? "\n网络连接失败，可重试检查或直接打开下载页面。" : "\nConnection failed. Retry or open the download page.";
            if (VersionUpdateService.CheckedAt.HasValue) HubVersionDetail.Text += (zh ? "\n上次检查 " : "\nLast checked ") + VersionUpdateService.CheckedAt.Value.ToLocalTime().ToString("MM-dd HH:mm");
            HubAboutUpdateBody.Text = string.IsNullOrWhiteSpace(VersionUpdateService.ReleaseNotes) ? (zh ? "检查更新后显示公开版本的更新说明。" : "Check for updates to load public release notes.") : VersionUpdateService.ReleaseNotes;
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
            if (HubPortSettingsView != null) HubPortSettingsView.Visibility = tab == TabPort ? Visibility.Visible : Visibility.Collapsed;
            if (HubDisplayView != null) HubDisplayView.Visibility = tab == TabDisplay ? Visibility.Visible : Visibility.Collapsed;
            if (HubAboutView != null) HubAboutView.Visibility = tab == TabAbout ? Visibility.Visible : Visibility.Collapsed;

            if (tab == TabGeneral)
            {
                HubGeneralOptionsPanel.RefreshSettings();
            }

            if (tab == TabPort)
            {
                _ = HubPortSettingsView.RefreshPortStateAsync();
            }

            // Suppress unused warning - the tooltip uses isChinese in case we expand it later.
            _ = isChinese;
        }

    }
}

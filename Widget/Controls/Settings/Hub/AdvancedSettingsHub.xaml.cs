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
        }
        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            ApplyLanguage();
            ApplyTheme(GameThemePalette.Current);
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

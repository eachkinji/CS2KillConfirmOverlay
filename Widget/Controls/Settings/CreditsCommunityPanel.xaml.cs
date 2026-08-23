using KillConfirmGameBar.Services;
using Windows.UI.Xaml.Controls;

namespace KillConfirmGameBar.Controls.Settings
{
    public sealed partial class CreditsCommunityPanel : UserControl
    {
        public CreditsCommunityPanel()
        {
            InitializeComponent();
            ApplyLanguage();
        }

        public void ApplyLanguage()
        {
            bool isChinese = LocalizationManager.Current == UiLanguage.SimplifiedChinese;
            CommunityTitleText.Text = isChinese
                ? "社区感谢"
                : "Community thanks";
            St0nieCommunityRoleText.Text = isChinese
                ? "cskillconfirm 原作者"
                : "Original cskillconfirm author";
            GufanCommunityRoleText.Text = isChinese
                ? "CS2 Customizer 作者"
                : "CS2 Customizer author";
            ContributorsTitleText.Text = isChinese ? "代码贡献者" : "Code contributors";
            St0nieContributorRoleText.Text = isChinese
                ? "安装包与发布支持"
                : "Installer and release support";
            System32ContributorRoleText.Text = isChinese
                ? "死斗模式、游戏文件检测与稳定性改进"
                : "Deathmatch, game-file detection, and reliability improvements";
        }

        internal void ApplyTheme(GameThemePalette theme)
        {
            if (theme == null)
            {
                return;
            }

            CommunityTitleText.Foreground = theme.Brush(theme.Text);
            ContributorsTitleText.Foreground = theme.Brush(theme.Text);
            SetCardTheme(St0nieCommunityCard, theme);
            SetCardTheme(GufanCommunityCard, theme);
            SetCardTheme(St0nieContributorCard, theme);
            SetCardTheme(System32ContributorCard, theme);
            SetPersonTheme(St0nieCommunityNameText, St0nieCommunityRoleText, theme);
            SetPersonTheme(GufanCommunityNameText, GufanCommunityRoleText, theme);
            SetPersonTheme(St0nieContributorNameText, St0nieContributorRoleText, theme);
            SetPersonTheme(System32ContributorNameText, System32ContributorRoleText, theme);
        }

        private static void SetCardTheme(Border card, GameThemePalette theme)
        {
            card.Background = theme.Brush(theme.SubtleField);
            card.BorderBrush = theme.Brush(theme.SoftBorder);
        }

        private static void SetPersonTheme(
            TextBlock name,
            TextBlock role,
            GameThemePalette theme)
        {
            name.Foreground = theme.Brush(theme.Text);
            role.Foreground = theme.Brush(theme.MutedText);
        }
    }
}

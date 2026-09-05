using KillConfirmGameBar.Controls.GameStyles;
using KillConfirmGameBar.Services;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Media;

namespace KillConfirmGameBar
{
    public sealed partial class KillConfirmWidgetPage
    {

        private void ApplyAdvancedEffectsPanelTheme()
        {
            GameThemePalette theme = GameThemePalette.Current;
            _customModulePanel?.ApplyTheme(theme);
            PackTestSectionView.AdvancedEffectsFlyoutCard.Background = new SolidColorBrush(theme.Shell);
            PackTestSectionView.AdvancedEffectsFlyoutCard.BorderBrush = new SolidColorBrush(theme.SoftBorder);
            PackTestSectionView.AdvancedEffectsGameCard.Background = new SolidColorBrush(theme.Panel);
            PackTestSectionView.AdvancedEffectsGameCard.BorderBrush = new SolidColorBrush(theme.Border);
            PackTestSectionView.AdvancedEffectsGameTitleText.Foreground = new SolidColorBrush(theme.Text);
            PackTestSectionView.AdvancedEffectsExperienceCard.Background = new SolidColorBrush(theme.Panel);
            PackTestSectionView.AdvancedEffectsExperienceCard.BorderBrush = new SolidColorBrush(theme.Border);
            PackTestSectionView.AdvancedEffectsExperienceTitleText.Foreground = new SolidColorBrush(theme.Text);
            PackTestSectionView.AdvancedEffectsRuntimeCard.Background = new SolidColorBrush(theme.Panel);
            PackTestSectionView.AdvancedEffectsRuntimeCard.BorderBrush = new SolidColorBrush(theme.Border);
            PackTestSectionView.AdvancedEffectsRuntimeTitleText.Foreground = new SolidColorBrush(theme.Text);
            PackTestSectionView.AdvancedEffectsCreditsCard.Background = new SolidColorBrush(theme.Panel);
            PackTestSectionView.AdvancedEffectsCreditsCard.BorderBrush = new SolidColorBrush(theme.Border);
            PackTestSectionView.AdvancedEffectsCreditsTitleText.Foreground = new SolidColorBrush(theme.Text);
            PackTestSectionView.AdvancedEffectsCreditsBodyText.Foreground = new SolidColorBrush(theme.MutedText);
            PackTestSectionView.AdvancedEffectsAuthorCard.Background = theme.Brush(theme.SubtleField);
            PackTestSectionView.AdvancedEffectsAuthorCard.BorderBrush = theme.Brush(theme.SoftBorder);
            PackTestSectionView.AdvancedEffectsAuthorAvatarFrame.Background = theme.Brush(theme.Card);
            PackTestSectionView.AdvancedEffectsAuthorAvatarFrame.BorderBrush = theme.Brush(theme.SoftBorder);
            PackTestSectionView.AdvancedEffectsAuthorNameText.Foreground = theme.Brush(theme.Text);
            PackTestSectionView.AdvancedEffectsAuthorDescriptionText.Foreground = theme.Brush(theme.MutedText);
            PackTestSectionView.AdvancedEffectsCreditsCommunityPanel.ApplyTheme(theme);
            PackTestSectionView.AdvancedEffectsExperiencePanel.ApplyTheme(theme);
            PackTestSectionView.AdvancedEffectsRuntimePanel.ApplyTheme(theme);
            if (_crossfireAdvancedEffectsPanel != null)
            {
                _crossfireAdvancedEffectsPanel.ApplyTheme(theme);
            }

            if (_csolAdvancedEffectsPanel != null)
            {
                _csolAdvancedEffectsPanel.ApplyTheme(theme);
            }

            if (_valorantAdvancedEffectsPanel != null)
            {
                _valorantAdvancedEffectsPanel.ApplyTheme(theme);
            }

            if (_overwatchAdvancedEffectsPanel != null)
            {
                _overwatchAdvancedEffectsPanel.ApplyTheme(theme);
            }

            if (_modernWarfare2019AdvancedEffectsPanel != null)
            {
                _modernWarfare2019AdvancedEffectsPanel.ApplyTheme(theme);
            }

            if (_apexAdvancedEffectsPanel != null)
            {
                _apexAdvancedEffectsPanel.ApplyTheme(theme);
            }

            if (_battlefield1AdvancedEffectsPanel != null)
            {
                _battlefield1AdvancedEffectsPanel.ApplyTheme(theme);
            }

            if (_battlefield5AdvancedEffectsPanel != null)
            {
                _battlefield5AdvancedEffectsPanel.ApplyTheme(theme);
            }

            if (_battlefield4AdvancedEffectsPanel != null)
            {
                _battlefield4AdvancedEffectsPanel.ApplyTheme(theme);
            }

            if (_battlefield2042AdvancedEffectsPanel != null)
            {
                _battlefield2042AdvancedEffectsPanel.ApplyTheme(theme);
            }

            if (_pubgAdvancedEffectsPanel != null)
            {
                _pubgAdvancedEffectsPanel.ApplyTheme(theme);
            }

            if (_deltaForceAdvancedEffectsPanel != null)
            {
                _deltaForceAdvancedEffectsPanel.ApplyTheme(theme);
            }

            if (_doubaoAdvancedEffectsPanel != null)
            {
                _doubaoAdvancedEffectsPanel.ApplyTheme(theme);
            }

            if (_dagoujiaoAdvancedEffectsPanel != null)
            {
                _dagoujiaoAdvancedEffectsPanel.ApplyTheme(theme);
            }

            AdvancedEffectsPanelSupport.ApplySoftenedTree(PackTestSectionView.AdvancedEffectsPanelHost, theme);
            AdvancedEffectsPanelSupport.ApplySoftenedTree(
                PackTestSectionView.AdvancedEffectsPanelHost.Content as DependencyObject,
                theme);
        }

        private void ApplyAdvancedEffectsPanelLanguage()
        {
            bool isChinese = LocalizationManager.Current == UiLanguage.SimplifiedChinese;
            _customModulePanel?.ApplyLanguage(isChinese);
            PackTestSectionView.AdvancedEffectsGameTitleText.Text = LocalizationManager.Text("GameEffectsTitle");
            PackTestSectionView.AdvancedEffectsExperienceTitleText.Text = isChinese ? "游戏体验增强" : "Game experience";
            PackTestSectionView.AdvancedEffectsRuntimeTitleText.Text = isChinese ? "软件与维护" : "App & maintenance";
            PackTestSectionView.AdvancedEffectsCreditsTitleText.Text = isChinese ? "作者与致谢" : "Author & credits";
            PackTestSectionView.AdvancedEffectsAuthorNameText.Text = "Zac · eachkinji";
            PackTestSectionView.AdvancedEffectsAuthorDescriptionText.Text = isChinese
                ? "Kill Confirm Overlay 作者与维护者"
                : "Author and maintainer of Kill Confirm Overlay";
            PackTestSectionView.AdvancedEffectsAuthorGitHubButton.Content = "GitHub · eachkinji";
            PackTestSectionView.AdvancedEffectsAuthorBilibiliButton.Content = isChinese ? "B站 · Zac不想烤肉" : "Bilibili · Zac";
            PackTestSectionView.AdvancedEffectsProjectButton.Content = isChinese ? "项目主页" : "Project home";
            PackTestSectionView.AdvancedEffectsDownloadButton.Content = isChinese ? "下载与更新 · 7Twv" : "Download & update · 7Twv";
            PackTestSectionView.AdvancedEffectsCreditsBodyText.Text = isChinese
                ? "感谢 st0nie 提供 cskillconfirm 的开发思路与基础代码，并感谢 gufan0000 的 CS2 Customizer 与本项目持续联动。"
                : "Thanks to st0nie for the ideas and foundation from cskillconfirm, and to gufan0000 for the ongoing CS2 Customizer collaboration.";
            PackTestSectionView.AdvancedEffectsCreditsCommunityPanel.ApplyLanguage();
            PackTestSectionView.AdvancedEffectsExperiencePanel.ApplyLanguage();
            PackTestSectionView.AdvancedEffectsRuntimePanel.ApplyLanguage();
            if (_crossfireAdvancedEffectsPanel != null)
            {
                _crossfireAdvancedEffectsPanel.ApplyLanguage(isChinese);
            }

            if (_csolAdvancedEffectsPanel != null)
            {
                _csolAdvancedEffectsPanel.ApplyLanguage(isChinese);
            }

            if (_valorantAdvancedEffectsPanel != null)
            {
                _valorantAdvancedEffectsPanel.ApplyLanguage(isChinese);
            }

            if (_overwatchAdvancedEffectsPanel != null)
            {
                _overwatchAdvancedEffectsPanel.ApplyLanguage(isChinese);
            }

            if (_modernWarfare2019AdvancedEffectsPanel != null)
            {
                _modernWarfare2019AdvancedEffectsPanel.ApplyLanguage(isChinese);
            }

            if (_apexAdvancedEffectsPanel != null)
            {
                _apexAdvancedEffectsPanel.ApplyLanguage(isChinese);
            }

            if (_battlefield1AdvancedEffectsPanel != null)
            {
                _battlefield1AdvancedEffectsPanel.ApplyLanguage(isChinese);
            }

            if (_battlefield5AdvancedEffectsPanel != null)
            {
                _battlefield5AdvancedEffectsPanel.ApplyLanguage(isChinese);
            }

            if (_battlefield4AdvancedEffectsPanel != null)
            {
                _battlefield4AdvancedEffectsPanel.ApplyLanguage(isChinese);
            }

            if (_battlefield2042AdvancedEffectsPanel != null)
            {
                _battlefield2042AdvancedEffectsPanel.ApplyLanguage(isChinese);
            }

            if (_pubgAdvancedEffectsPanel != null)
            {
                _pubgAdvancedEffectsPanel.ApplyLanguage(isChinese);
            }

            if (_deltaForceAdvancedEffectsPanel != null)
            {
                _deltaForceAdvancedEffectsPanel.ApplyLanguage(isChinese);
            }

            if (_doubaoAdvancedEffectsPanel != null)
            {
                _doubaoAdvancedEffectsPanel.ApplyLanguage(isChinese);
            }

            if (_dagoujiaoAdvancedEffectsPanel != null)
            {
                _dagoujiaoAdvancedEffectsPanel.ApplyLanguage(isChinese);
            }
        }

        private void SelectCurrentBattlefieldMoneyRewardMode()
        {
            string mode = ApplicationData.Current.LocalSettings.Values[MoneyRewardModeSettingKey] as string;
            if (string.IsNullOrWhiteSpace(mode))
            {
                mode = DefaultMoneyRewardMode;
            }

            _suppressMoneyRewardModeEvents = true;
            if (PackTestSectionView.AdvancedEffectsPanelHost?.Content == _battlefield1AdvancedEffectsPanel)
            {
                _battlefield1AdvancedEffectsPanel.SelectMoneyRewardMode(mode, DefaultMoneyRewardMode);
            }
            else if (PackTestSectionView.AdvancedEffectsPanelHost?.Content == _battlefield5AdvancedEffectsPanel)
            {
                _battlefield5AdvancedEffectsPanel.SelectMoneyRewardMode(mode, DefaultMoneyRewardMode);
            }
            else if (PackTestSectionView.AdvancedEffectsPanelHost?.Content == _battlefield4AdvancedEffectsPanel)
            {
                _battlefield4AdvancedEffectsPanel.SelectMoneyRewardMode(mode, DefaultMoneyRewardMode);
            }
            else if (PackTestSectionView.AdvancedEffectsPanelHost?.Content == _battlefield2042AdvancedEffectsPanel)
            {
                _battlefield2042AdvancedEffectsPanel.SelectMoneyRewardMode(mode, DefaultMoneyRewardMode);
            }
            else if (PackTestSectionView.AdvancedEffectsPanelHost?.Content == _pubgAdvancedEffectsPanel)
            {
                _pubgAdvancedEffectsPanel.SelectMoneyRewardMode(mode, DefaultMoneyRewardMode);
            }
            else if (PackTestSectionView.AdvancedEffectsPanelHost?.Content == _apexAdvancedEffectsPanel)
            {
                _apexAdvancedEffectsPanel.SelectMoneyRewardMode(mode, DefaultMoneyRewardMode);
            }
            else if (PackTestSectionView.AdvancedEffectsPanelHost?.Content == _modernWarfare2019AdvancedEffectsPanel)
            {
                _modernWarfare2019AdvancedEffectsPanel.SelectMoneyRewardMode(mode, DefaultMoneyRewardMode);
            }
            else if (PackTestSectionView.AdvancedEffectsPanelHost?.Content == _deltaForceAdvancedEffectsPanel)
            {
                _deltaForceAdvancedEffectsPanel.SelectMoneyRewardMode(mode, DefaultMoneyRewardMode);
            }

            _suppressMoneyRewardModeEvents = false;
        }
    }
}

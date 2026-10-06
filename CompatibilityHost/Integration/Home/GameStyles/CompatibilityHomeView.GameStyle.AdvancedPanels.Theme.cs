using KillConfirmGameBar.Features.CompatibilityDisplay.Controls.GameStyles;
using KillConfirmGameBar.Services;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Media;

namespace KillConfirmGameBar.Features.CompatibilityDisplay
{
    public sealed partial class CompatibilityHomeView
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

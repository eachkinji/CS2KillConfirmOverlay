using KillConfirmGameBar.Features.CompatibilityDisplay;
using KillConfirmGameBar.Services;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace KillConfirmGameBar
{
    public sealed partial class MainPage
    {
        private void InitializeCompatibilityWorkspace()
        {
            ToolTipService.SetToolTip(CompatibilitySidebarItem, LocalizationManager.Current == UiLanguage.SimplifiedChinese ? "兼容显示" : "Compatibility display");
            InitializeCompatibilityUiValidation();
        }
        private bool _isCompatibilityPageSelected;
        private CompatibilityDisplayPanel _compatibilityWorkspace;
        private bool HandleCompatibilityGameStyleChanged(GameStyleMode mode)
        {
            if (!_isCompatibilityPageSelected) return false;
            _ = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () => {
                if (_isSettingsPageLoaded && _isCompatibilityPageSelected && GameStyleService.Current == mode) ApplyCompatibilityWorkspace();
            });
            return true;
        }
        private void SelectCompatibilityWorkspace()
        {
            _isCompatibilityPageSelected = true;
            _isHomePageSelected = true;
            BeginGameStyleTransition();
            ApplyCompatibilityWorkspace();
        }
        private void ApplyCompatibilityWorkspace()
        {
            SyncGameStyleSelector();
            UpdateSettingsPageVisibility();
            HomeWorkspaceTabBar.Visibility = GameWorkspaceTabBar.Visibility = Visibility.Collapsed;
            BackgroundDecoration.Visibility = Visibility.Collapsed;
            var theme = GameThemePalette.ForMode(GameStyleService.Current);
            SettingsRootGrid.Background = CreateSettingsBackground(GameStyleService.Current, true);
            ApplyGameStyleSidebarTheme(theme);
            ApplyPageTitleTheme(theme);
            TitleText.Text = LocalizationManager.Current == UiLanguage.SimplifiedChinese ? "兼容显示" : "Compatibility display";
            if (_compatibilityWorkspace == null) _compatibilityWorkspace = new CompatibilityDisplayPanel();
            if (CompatibilityPageContent.Content != _compatibilityWorkspace) CompatibilityPageContent.Content = _compatibilityWorkspace;
            _compatibilityWorkspace.ApplyLanguage();
            _compatibilityWorkspace.ApplyTheme(theme);
        }
    }
}

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
            InitializeCompatibilityUiValidation();
        }
        private bool _isHomePageSelected = true;
        private CompatibilityDisplayPanel _compatibilityWorkspace;
        private bool HandleCompatibilityGameStyleChanged(GameStyleMode mode)
        {
            if (!_isHomePageSelected) return false;
            _ = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () => {
                if (_isSettingsPageLoaded && _isHomePageSelected && GameStyleService.Current == mode) ApplyCompatibilityWorkspace();
            });
            return true;
        }
        private void SelectCompatibilityWorkspace()
        {
            _isHomePageSelected = true;
            _isSettingsWorkspaceSelected = true;
            BeginGameStyleTransition();
            ApplyCompatibilityWorkspace();
        }
        private void ApplyCompatibilityWorkspace()
        {
            SyncGameStyleSelector();
            UpdateSettingsPageVisibility();
            AdvancedSettingsTabBar.Visibility = GameWorkspaceTabBar.Visibility = Visibility.Collapsed;
            BackgroundDecoration.Visibility = Visibility.Collapsed;
            var theme = GameThemePalette.ForMode(GameStyleService.Current);
            SettingsRootGrid.Background = CreateSettingsBackground(GameStyleService.Current, true);
            ApplyGameStyleSidebarTheme(theme);
            ApplyPageTitleTheme(theme);
            TitleText.Text = LocalizationManager.Current == UiLanguage.SimplifiedChinese ? "主页" : "Home";
            if (_compatibilityWorkspace == null) _compatibilityWorkspace = new CompatibilityDisplayPanel();
            if (CompatibilityPageContent.Content != _compatibilityWorkspace) CompatibilityPageContent.Content = _compatibilityWorkspace;
            _compatibilityWorkspace.ApplyLanguage();
            _compatibilityWorkspace.ApplyTheme(theme);
        }
    }
}

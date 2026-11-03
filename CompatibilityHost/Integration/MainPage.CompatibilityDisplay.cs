using System;
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
            var theme = GameThemePalette.Home;
            SettingsRootGrid.Background = CreateSettingsBackground(GameStyleService.Current, true);
            UpdateBackgroundDecoration(GameStyleService.Current, false);
            ApplyGameStyleSidebarTheme(theme);
            ApplyPageTitleTheme(theme);
            if (_compatibilityWorkspace == null) _compatibilityWorkspace = new CompatibilityDisplayPanel();
            _compatibilityWorkspace.OpenGameSettings = OpenCurrentGameSettings;
            if (CompatibilityPageContent.Content != _compatibilityWorkspace) CompatibilityPageContent.Content = _compatibilityWorkspace;
            _compatibilityWorkspace.ApplyLanguage();
            _compatibilityWorkspace.ApplyTheme(theme);
        }
        internal void OpenCurrentGameSettings()
        {
            if (GameStyleSidebarSelector == null) return;
            string key = GameStyleService.ToStorageValue(GameStyleService.Current);
            foreach (object item in GameStyleSidebarSelector.MenuItems)
            {
                if (item is NavigationViewItem sidebarItem && sidebarItem.Tag is string tag && string.Equals(tag, key, StringComparison.OrdinalIgnoreCase))
                {
                    GameStyleSidebarSelector.SelectedItem = sidebarItem;
                    return;
                }
            }
        }
    }
}

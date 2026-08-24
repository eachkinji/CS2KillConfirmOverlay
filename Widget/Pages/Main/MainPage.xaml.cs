using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using KillConfirmGameBar.Services;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using KillConfirmGameBar.Helpers;

namespace KillConfirmGameBar
{
    public sealed partial class MainPage : Page
    {
        private readonly MediaPlayer _previewPlayer = new MediaPlayer();
        private bool _iconSpecExpanded;
        private bool _isSettingsPageLoaded;
        private string _activeHomeTab = "general";
        private string _activeGameTab = "combat";

        public MainPage()
        {
            InitializeComponent();
            ApplyLanguage();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnHomeTabGeneralClick(object sender, RoutedEventArgs e) => SelectHomeTab("general");
        private void OnHomeTabPortClick(object sender, RoutedEventArgs e) => SelectHomeTab("port");
        private void OnHomeTabDisplayClick(object sender, RoutedEventArgs e) => SelectHomeTab("display");
        private void OnHomeTabAboutClick(object sender, RoutedEventArgs e) => SelectHomeTab("about");

        private void OnGameTabCombatClick(object sender, RoutedEventArgs e) => SelectGameTab("combat");
        private void OnGameTabVoiceClick(object sender, RoutedEventArgs e) => SelectGameTab("voice");
        private void OnGameTabIconClick(object sender, RoutedEventArgs e) => SelectGameTab("icon");
        private void OnGameTabGuideClick(object sender, RoutedEventArgs e) => SelectGameTab("guide");

        public void SelectHomeTab(string tab)
        {
            _activeHomeTab = tab;
            ApplyHomeActiveTab();
        }

        public void SelectGameTab(string tab)
        {
            _activeGameTab = tab;
            ApplyGameActiveTab();
            _ = EnsureActivePackListLoadedAsync();
        }

        private void ApplyHomeActiveTab()
        {
            if (!_isHomePageSelected)
            {
                return;
            }

            if (AdvancedSettingsHubControl != null)
            {
                AdvancedSettingsHubControl.SwitchTab(_activeHomeTab);
            }

            UpdateHomeTabButtonsTheme();
        }

        private void ApplyGameActiveTab()
        {
            if (_isHomePageSelected)
            {
                return;
            }

            bool isCombat = _activeGameTab == "combat";
            bool isVoice = _activeGameTab == "voice";
            bool isIcon = _activeGameTab == "icon";
            bool isGuide = _activeGameTab == "guide";
            bool isSpecialGuide = GameStyleService.Current == GameStyleMode.Csol || GameStyleService.Current == GameStyleMode.Dagoujiao;

            if (GameEffectsCard != null) GameEffectsCard.Visibility = isCombat ? Visibility.Visible : Visibility.Collapsed;
            if (VoicePackCollectionsCard != null) VoicePackCollectionsCard.Visibility = isVoice ? Visibility.Visible : Visibility.Collapsed;
            if (IconPackCollectionsCard != null) IconPackCollectionsCard.Visibility = isIcon ? Visibility.Visible : Visibility.Collapsed;
            if (VoiceCollectionsCard != null) VoiceCollectionsCard.Visibility = (isGuide && !isSpecialGuide) ? Visibility.Visible : Visibility.Collapsed;
            if (CsolGuideCard != null) CsolGuideCard.Visibility = (isGuide && isSpecialGuide) ? Visibility.Visible : Visibility.Collapsed;

            UpdateGameTabButtonsTheme();
        }

        private void UpdateHomeTabButtonsTheme()
        {
            GameThemePalette theme = _isHomePageSelected ? GameThemePalette.Home : GameThemePalette.Current;
            UpdateTabBtn(HomeTabGeneralButton, _activeHomeTab == "general", theme);
            UpdateTabBtn(HomeTabPortButton, _activeHomeTab == "port", theme);
            UpdateTabBtn(HomeTabDisplayButton, _activeHomeTab == "display", theme);
            UpdateTabBtn(HomeTabAboutButton, _activeHomeTab == "about", theme);
        }

        private void UpdateGameTabButtonsTheme()
        {
            GameThemePalette theme = GameThemePalette.Current;
            UpdateTabBtn(GameTabCombatButton, _activeGameTab == "combat", theme);
            UpdateTabBtn(GameTabVoiceButton, _activeGameTab == "voice", theme);
            UpdateTabBtn(GameTabIconButton, _activeGameTab == "icon", theme);
            UpdateTabBtn(GameTabGuideButton, _activeGameTab == "guide", theme);
        }

        private static void UpdateTabBtn(Button btn, bool isActive, GameThemePalette theme)
        {
            if (btn == null) return;
            if (isActive)
            {
                btn.Background = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255));
                btn.Foreground = new SolidColorBrush(Color.FromArgb(255, 27, 27, 27));
                btn.BorderBrush = new SolidColorBrush(Color.FromArgb(255, 209, 209, 209));
                btn.BorderThickness = new Thickness(1);
            }
            else
            {
                btn.Background = new SolidColorBrush(Colors.Transparent);
                btn.Foreground = new SolidColorBrush(Color.FromArgb(255, 97, 97, 97));
                btn.BorderBrush = new SolidColorBrush(Colors.Transparent);
                btn.BorderThickness = new Thickness(0);
            }
        }

        private void OnGameStyleServiceChanged(object sender, GameStyleMode mode)
        {
            if (!_isSettingsPageLoaded)
            {
                return;
            }

            int navigationRevision = System.Threading.Volatile.Read(ref _gameStyleNavigationRevision);
            _ = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
            {
                if (!_isSettingsPageLoaded
                    || _isHomePageSelected
                    || navigationRevision != System.Threading.Volatile.Read(ref _gameStyleNavigationRevision)
                    || GameStyleService.Current != mode)
                {
                    return;
                }

                try
                {
                    // Game-specific tab titles, collection descriptions and guide
                    // text are localized from the active style. Refresh them with
                    // the visual theme so labels from the previous game cannot
                    // remain after navigation.
                    ApplyLanguage();
                    await EnsureActivePackListLoadedAsync();
                }
               catch (System.Exception ex)
               {
                    App.LogCrash("Game style switch failed in settings page: " + ex);
               }
            });
        }
    }
}

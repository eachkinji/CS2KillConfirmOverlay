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
        private string _activeCfTab = "combat";

        public MainPage()
        {
            InitializeComponent();
            ApplyLanguage();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnCfTabCombatClick(object sender, RoutedEventArgs e) => SelectCfTab("combat");
        private void OnCfTabVoiceClick(object sender, RoutedEventArgs e) => SelectCfTab("voice");
        private void OnCfTabIconClick(object sender, RoutedEventArgs e) => SelectCfTab("icon");
        private void OnCfTabGuideClick(object sender, RoutedEventArgs e) => SelectCfTab("guide");

        public void SelectCfTab(string tab)
        {
            _activeCfTab = tab;
            ApplyCfActiveTab();
        }

        private void ApplyCfActiveTab()
        {
            if (GameStyleService.Current != GameStyleMode.Crossfire || _isHomePageSelected)
            {
                return;
            }

            bool isCombat = _activeCfTab == "combat";
            bool isVoice = _activeCfTab == "voice";
            bool isIcon = _activeCfTab == "icon";
            bool isGuide = _activeCfTab == "guide";

            if (GameEffectsCard != null) GameEffectsCard.Visibility = isCombat ? Visibility.Visible : Visibility.Collapsed;
            if (VoicePackCollectionsCard != null) VoicePackCollectionsCard.Visibility = isVoice ? Visibility.Visible : Visibility.Collapsed;
            if (IconPackCollectionsCard != null) IconPackCollectionsCard.Visibility = isIcon ? Visibility.Visible : Visibility.Collapsed;
            if (VoiceCollectionsCard != null) VoiceCollectionsCard.Visibility = isGuide ? Visibility.Visible : Visibility.Collapsed;

            UpdateCfTabButtonsTheme();
        }

        private void UpdateCfTabButtonsTheme()
        {
            GameThemePalette theme = GameThemePalette.Current;
            UpdateTabBtn(CfTabCombatButton, _activeCfTab == "combat", theme);
            UpdateTabBtn(CfTabVoiceButton, _activeCfTab == "voice", theme);
            UpdateTabBtn(CfTabIconButton, _activeCfTab == "icon", theme);
            UpdateTabBtn(CfTabGuideButton, _activeCfTab == "guide", theme);
        }

        private static void UpdateTabBtn(Button btn, bool isActive, GameThemePalette theme)
        {
            if (btn == null) return;
            if (isActive)
            {
                btn.Background = new SolidColorBrush(theme.Accent);
                btn.Foreground = new SolidColorBrush(Colors.White);
                btn.BorderBrush = new SolidColorBrush(theme.Accent);
            }
            else
            {
                btn.Background = new SolidColorBrush(theme.SubtleField);
                btn.Foreground = new SolidColorBrush(theme.Text);
                btn.BorderBrush = new SolidColorBrush(theme.SoftBorder);
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
                    ApplyGameStyleUi();
                    await ReloadPackListsAsync(mode);
                    try
                    {
                        await CombatEventSoundSettingsStore.SyncAsync(mode);
                    }
                   catch (System.Exception ex)
                   {
                        App.LogCrash("Sync event sounds after style change failed: " + ex.Message);
                   }
               }
               catch (System.Exception ex)
               {
                    App.LogCrash("Game style switch failed in settings page: " + ex);
               }
            });
        }
    }
}

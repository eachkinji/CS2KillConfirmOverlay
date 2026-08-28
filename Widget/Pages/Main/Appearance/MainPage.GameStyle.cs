using System;
using KillConfirmGameBar.Services;
using KillConfirmGameBar.Controls.Settings;
using KillConfirmGameBar.Controls.GameStyles;
using System.Threading.Tasks;
using Windows.Data.Json;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.Web.Http;

namespace KillConfirmGameBar
{
    public sealed partial class MainPage
    {
        private CrossfireAdvancedEffectsPanel _crossfireAdvancedEffectsPanel;
        private CrossfireStylePanel _crossfireStylePanel;
        private CsolAdvancedEffectsPanel _csolAdvancedEffectsPanel;
        private ValorantAdvancedEffectsPanel _valorantAdvancedEffectsPanel;
        private OverwatchAdvancedEffectsPanel _overwatchAdvancedEffectsPanel;
        private ModernWarfare2019AdvancedEffectsPanel _modernWarfare2019AdvancedEffectsPanel;
        private ApexAdvancedEffectsPanel _apexAdvancedEffectsPanel;
        private Battlefield1AdvancedEffectsPanel _battlefield1AdvancedEffectsPanel;
        private Battlefield5AdvancedEffectsPanel _battlefield5AdvancedEffectsPanel;
        private Battlefield4AdvancedEffectsPanel _battlefield4AdvancedEffectsPanel;
        private Battlefield2042AdvancedEffectsPanel _battlefield2042AdvancedEffectsPanel;
        private PubgAdvancedEffectsPanel _pubgAdvancedEffectsPanel;
        private DeltaForceAdvancedEffectsPanel _deltaForceAdvancedEffectsPanel;
        private CustomModulePanel _customModulePanel;
        private DoubaoAdvancedEffectsPanel _doubaoAdvancedEffectsPanel;
        private DagoujiaoAdvancedEffectsPanel _dagoujiaoAdvancedEffectsPanel;

        private bool _suppressGameStyleEvents;
        private bool _suppressCrossfireSettingEvents;
        private bool _isHomePageSelected = true;
        private int _gameStyleNavigationRevision;

        private void ApplyGameStyleUi()
        {
            GameStyleMode mode = GameStyleService.Current;
            SyncGameStyleSelector();
            bool valorant = mode == GameStyleMode.Valorant;
            bool csol = mode == GameStyleMode.Csol;
            bool battlefield1 = mode == GameStyleMode.Battlefield1;
            bool battlefield5 = mode == GameStyleMode.Battlefield5;
            bool battlefield4 = mode == GameStyleMode.Battlefield4;
            bool battlefield2042 = mode == GameStyleMode.Battlefield2042;
            bool fixedPreset = GameStyleService.IsModPresetGameKey(GameStyleService.ToStorageValue(mode));
            bool isCrossfire = mode == GameStyleMode.Crossfire;
            bool isDagoujiao = mode == GameStyleMode.Dagoujiao;
            bool overwatch = mode == GameStyleMode.Overwatch;
            bool modernWarfare2019 = mode == GameStyleMode.ModernWarfare2019;
            bool apex = mode == GameStyleMode.Apex;
            GameThemePalette theme = _isHomePageSelected ? GameThemePalette.Home : GameThemePalette.ForMode(mode);

            Visibility iconCreationVisibility = overwatch || modernWarfare2019 || apex ? Visibility.Collapsed : Visibility.Visible;
            if (ImportIconMaterialButton != null) ImportIconMaterialButton.Visibility = iconCreationVisibility;
            if (ImportIconPackButton != null) ImportIconPackButton.Visibility = iconCreationVisibility;
            if (ImportIconZipButton != null) ImportIconZipButton.Visibility = iconCreationVisibility;
            if (CreateIconPackButton != null) CreateIconPackButton.Visibility = iconCreationVisibility;
            if (mode == GameStyleMode.CustomModule && ImportIconMaterialButton != null) ImportIconMaterialButton.Visibility = Visibility.Collapsed;
            VoicePackCollectionsCard.Visibility = mode == GameStyleMode.CustomModule ? Visibility.Collapsed : Visibility.Visible;
            VoiceCollectionsCard.Visibility = mode == GameStyleMode.CustomModule ? Visibility.Collapsed : Visibility.Visible;

            UpdateSettingsPageVisibility();
            if (_isHomePageSelected)
            {
                if (GameAdvancedSettingsPanelHost != null)
                {
                    GameAdvancedSettingsPanelHost.Content = null;
                }
            }
            else
            {
                MountGameAdvancedSettingsPanel();
            }

            if (HomeWorkspaceTabBar != null)
            {
                HomeWorkspaceTabBar.Visibility = _isHomePageSelected ? Visibility.Visible : Visibility.Collapsed;
            }

            if (GameWorkspaceTabBar != null)
            {
                GameWorkspaceTabBar.Visibility = !_isHomePageSelected ? Visibility.Visible : Visibility.Collapsed;
            }

            if (_isHomePageSelected)
            {
                ApplyHomeActiveTab();
                SettingsRootGrid.Background = CreateSettingsBackground(mode, true);
                BackgroundDecoration.Visibility = Visibility.Collapsed;
            }
            else
            {
                ApplyGameActiveTab();
                SettingsRootGrid.Background = CreateSettingsBackground(mode, false);
                BackgroundDecoration.Visibility = Visibility.Visible;
                BackgroundDecoration.ApplyPalette(
                    CreateHeroSlashBrush(mode),
                    battlefield5 ? Color.FromArgb(255, 119, 243, 255) :
                    battlefield2042 ? Color.FromArgb(255, 109, 255, 255) :
                    battlefield4 ? Color.FromArgb(255, 125, 211, 252) :
                    battlefield1 ? Color.FromArgb(255, 255, 218, 166) :
                    valorant ? Color.FromArgb(255, 255, 170, 178) :
                    csol ? Color.FromArgb(255, 255, 168, 150) :
                    isDagoujiao ? Color.FromArgb(255, 233, 213, 255) :
                    overwatch ? Color.FromArgb(255, 255, 205, 168) :
                    modernWarfare2019 ? Color.FromArgb(255, 177, 231, 244) :
                    Color.FromArgb(255, 255, 240, 213),
                    battlefield5 ? Color.FromArgb(255, 58, 137, 166) :
                    battlefield2042 ? Color.FromArgb(255, 60, 128, 146) :
                    battlefield4 ? Color.FromArgb(255, 56, 120, 160) :
                    battlefield1 ? Color.FromArgb(255, 88, 110, 126) :
                    valorant ? Color.FromArgb(255, 59, 78, 102) :
                    csol ? Color.FromArgb(255, 120, 37, 42) :
                    isDagoujiao ? Color.FromArgb(255, 107, 33, 168) :
                    overwatch ? Color.FromArgb(255, 67, 77, 88) :
                    modernWarfare2019 ? Color.FromArgb(255, 48, 93, 107) :
                    Color.FromArgb(255, 196, 196, 196),
                    fixedPreset || csol ? theme.Accent :
                    valorant ? theme.Secondary :
                    isDagoujiao ? theme.Accent :
                    Color.FromArgb(255, 207, 107, 0));
            }

            bool isChinese = LocalizationManager.Current == UiLanguage.SimplifiedChinese;
            string gameName = isChinese ? GameStyleService.ToDisplayName(mode) : mode.ToString();
            if (mode != GameStyleMode.Dagoujiao
                && mode != GameStyleMode.Csol
                && mode != GameStyleMode.Overwatch
                && mode != GameStyleMode.ModernWarfare2019
                && mode != GameStyleMode.Apex)
            {
                if (VoiceCollectionsTitleText != null) VoiceCollectionsTitleText.Text = gameName + " " + LocalizationManager.Text("VoiceCollectionsTitle");
                if (IconCollectionsTitleText != null) IconCollectionsTitleText.Text = gameName + " " + LocalizationManager.Text("IconCollectionsTitle");
            }
            if (GameEffectsTitleText != null)
            {
                GameEffectsTitleText.Text = mode == GameStyleMode.Overwatch
                    ? (isChinese ? "守望先锋击杀提示" : "Overwatch Kill Feedback")
                    : gameName + " " + (isChinese ? "战斗与特效设置" : "Combat & Effects Settings");
            }
            if (StructureTitleText != null) StructureTitleText.Text = gameName + " " + (isChinese ? "资源包制作指南" : "Resource Pack Guide");

            ApplyPageTitleTheme(theme);
            SetText(GameStyleLabelText, theme.Text);
            SetText(GameEffectsTitleText, Color.FromArgb(255, 27, 27, 27));
            SetText(VoiceCollectionsTitleText, Color.FromArgb(255, 27, 27, 27));
            SetText(VoiceCollectionsHintText, Color.FromArgb(255, 97, 97, 97));
            SetText(IconCollectionsTitleText, Color.FromArgb(255, 27, 27, 27));
            SetText(IconCollectionsHintText, Color.FromArgb(255, 97, 97, 97));
            SetText(VoiceVisibleCountText, Color.FromArgb(255, 0, 103, 192));
            SetText(IconVisibleCountText, Color.FromArgb(255, 0, 103, 192));
            SetText(StructureTitleText, Color.FromArgb(255, 27, 27, 27));
            SetText(StructureBodyText, Color.FromArgb(255, 97, 97, 97));
            SetText(StructureImportFolderTitleText, Color.FromArgb(255, 27, 27, 27));
            SetText(StructureImportFolderBodyText, Color.FromArgb(255, 97, 97, 97));
            SetText(StructureVoiceSpecTitleText, Color.FromArgb(255, 27, 27, 27));
            SetText(StructureVoiceSpecBodyText, Color.FromArgb(255, 66, 66, 66));
            SetText(StructureIconSpecTitleText, Color.FromArgb(255, 27, 27, 27));
            SetText(StructureIconSpecSummaryText, Color.FromArgb(255, 0, 103, 192));
            SetText(StructureIconSpecFullText, Color.FromArgb(255, 66, 66, 66));
            SetText(StructureImportZipTitleText, Color.FromArgb(255, 27, 27, 27));
            SetText(StructureImportZipBodyText, Color.FromArgb(255, 97, 97, 97));
            SetText(StructureCreatorTitleText, Color.FromArgb(255, 27, 27, 27));
            SetText(StructureCreatorBodyText, Color.FromArgb(255, 97, 97, 97));
            SetText(StructureFileHintText, Color.FromArgb(255, 66, 66, 66));
            SetText(TipsTitleText, Color.FromArgb(255, 27, 27, 27));
            SetText(TipsBodyText, Color.FromArgb(255, 97, 97, 97));

            ApplySectionTheme(GameEffectsCard, theme);
            ApplyCardTheme(VoicePackCollectionsCard, theme);
            ApplyCardTheme(IconPackCollectionsCard, theme);
            ApplyCardTheme(VoiceCollectionsCard, theme);
            ApplyCardTheme(IconCollectionsCard, theme);

            ApplyButtonTheme(ImportVoicePackButton, theme, false);
            ApplyButtonTheme(ImportVoiceZipButton, theme, false);
            ApplyButtonTheme(CreateVoicePackButton, theme, true);
            ApplyButtonTheme(ImportIconPackButton, theme, false);
            ApplyButtonTheme(ImportIconZipButton, theme, false);
            ApplyButtonTheme(CreateIconPackButton, theme, true);
            ApplyButtonTheme(IconSpecToggleButton, theme, false);
            ApplyPackCardTheme(VoicePackListPanel, theme);
            ApplyPackCardTheme(IconPackListPanel, theme);
            AdvancedEffectsPanelSupport.ApplyCombo(
                GameStyleSelector,
                theme.Text,
                theme.SubtleField,
                theme.SoftBorder);
            ApplyGameStyleSidebarTheme(theme);
            AdvancedSettingsHubControl.ApplyTheme(theme);
            ApplyGameAdvancedSettingsPanelTheme();
        }

        private static void ApplySectionTheme(Border card, GameThemePalette theme)
        {
            if (card != null)
            {
                card.Background = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255));
                card.BorderBrush = new SolidColorBrush(Color.FromArgb(255, 229, 229, 229));
            }
        }

        private void ApplyPageTitleTheme(GameThemePalette theme)
        {
            if (PageTitleSurface != null)
            {
                // Keep the title on an opaque theme-colored surface so the
                // decorative game background can never reduce its contrast.
                PageTitleSurface.Background = new SolidColorBrush(theme.AccentSoft);
                PageTitleSurface.BorderBrush = new SolidColorBrush(theme.Accent);
            }

            SetText(TitleText, theme.AccentText);
        }

        private static void ApplyCardTheme(Border card, GameThemePalette theme)
        {
            if (card != null)
            {
                card.Background = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255));
                card.BorderBrush = new SolidColorBrush(Color.FromArgb(255, 229, 229, 229));
            }
        }

        private void SyncGameStyleSelector()
        {
            if (GameStyleSelector == null && GameStyleSidebarSelector == null)
            {
                return;
            }

            _suppressGameStyleEvents = true;
            try
            {
                string key = GameStyleService.ToStorageValue(GameStyleService.Current);
                if (GameStyleSelector != null)
                {
                    foreach (object item in GameStyleSelector.Items)
                    {
                        if (item is ComboBoxItem comboItem && comboItem.Tag is string tag && string.Equals(tag, key, System.StringComparison.OrdinalIgnoreCase))
                        {
                            GameStyleSelector.SelectedItem = comboItem;
                            break;
                        }
                    }
                }

                if (GameStyleSidebarSelector != null)
                {
                    string sidebarKey = _isHomePageSelected ? "home" : key;
                    foreach (object item in GameStyleSidebarSelector.MenuItems)
                    {
                        if (item is NavigationViewItem sidebarItem && sidebarItem.Tag is string tag && string.Equals(tag, sidebarKey, System.StringComparison.OrdinalIgnoreCase))
                        {
                            GameStyleSidebarSelector.SelectedItem = sidebarItem;
                            break;
                        }
                    }
                }
            }
            finally
            {
                _suppressGameStyleEvents = false;
            }
        }

        private void OnGameStyleSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressGameStyleEvents)
            {
                return;
            }

            if (GameStyleSelector?.SelectedItem is ComboBoxItem selected && selected.Tag is string key)
            {
                SelectGameStyle(key);
            }
        }

        private void OnGameStyleSidebarSelectionChanged(
            NavigationView sender,
            NavigationViewSelectionChangedEventArgs e)
        {
            if (_suppressGameStyleEvents)
            {
                return;
            }

            if (e.SelectedItem is NavigationViewItem selected && selected.Tag is string key)
            {
                if (string.Equals(key, "home", StringComparison.OrdinalIgnoreCase))
                {
                    _isHomePageSelected = true;
                    BeginGameStyleTransition();
                    ApplyGameStyleUi();
                    return;
                }

                _isHomePageSelected = false;
                SelectGameStyle(key);
            }
        }

        private void UpdateSettingsPageVisibility()
        {
            if (HomePageContent != null)
            {
                HomePageContent.Visibility = _isHomePageSelected
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }

            if (GamePageContent != null)
            {
                GamePageContent.Visibility = _isHomePageSelected
                    ? Visibility.Collapsed
                    : Visibility.Visible;
            }
        }

        private void SelectGameStyle(string key)
        {
            GameStyleMode newMode = GameStyleService.FromKey(key);
            BeginGameStyleTransition();
            if (GameStyleService.Current != newMode)
            {
                GameStyleService.Current = newMode;
                return;
            }

            // Setting the same mode does not raise GameStyleService.Changed.
            // Route it through the same guarded refresh path as a real mode change.
            OnGameStyleServiceChanged(null, newMode);
        }

        private void BeginGameStyleTransition()
        {
            System.Threading.Interlocked.Increment(ref _gameStyleNavigationRevision);
            System.Threading.Interlocked.Increment(ref _packListReloadVersion);
            _loadedVoicePackStyle = null;
            _loadedIconPackStyle = null;
            UpdateSettingsPageVisibility();

            if (GameAdvancedSettingsPanelHost != null)
            {
                GameAdvancedSettingsPanelHost.Content = null;
            }

            VoicePackListPanel?.Children.Clear();
            IconPackListPanel?.Children.Clear();
            if (VoiceVisibleCountText != null)
            {
                VoiceVisibleCountText.Text = string.Empty;
            }
            if (IconVisibleCountText != null)
            {
                IconVisibleCountText.Text = string.Empty;
            }
        }

        private void ApplyGameStyleSidebarTheme(GameThemePalette theme)
        {
            if (GameModeSidebar != null)
            {
                GameModeSidebar.Background = new SolidColorBrush(theme.Panel);
                GameModeSidebar.BorderBrush = new SolidColorBrush(theme.Border);
            }

            if (GameStyleSidebarSelector == null)
            {
                return;
            }

            foreach (object entry in GameStyleSidebarSelector.MenuItems)
            {
                if (!(entry is NavigationViewItem item))
                {
                    continue;
                }

                bool selected = item.IsSelected;
                item.Foreground = new SolidColorBrush(selected ? theme.Accent : theme.MutedText);
                item.Opacity = selected ? 1.0 : 0.82;
            }
        }

        private void MountGameAdvancedSettingsPanel()
        {
            if (GameAdvancedSettingsPanelHost == null)
            {
                return;
            }

            object panel;
            switch (GameStyleService.Current)
            {
                case GameStyleMode.Overwatch:
                    panel = EnsureOverwatchAdvancedSettingsPanel();
                    break;
                case GameStyleMode.ModernWarfare2019:
                    panel = EnsureModernWarfare2019AdvancedSettingsPanel();
                    break;
                case GameStyleMode.Apex:
                    panel = EnsureApexAdvancedSettingsPanel();
                    break;
                case GameStyleMode.Valorant:
                    panel = EnsureValorantAdvancedSettingsPanel();
                    break;
                case GameStyleMode.Battlefield1:
                    panel = EnsureBattlefield1AdvancedSettingsPanel();
                    break;
                case GameStyleMode.Battlefield5:
                    panel = EnsureBattlefield5AdvancedSettingsPanel();
                    break;
                case GameStyleMode.Battlefield4:
                    panel = EnsureBattlefield4AdvancedSettingsPanel();
                    break;
                case GameStyleMode.Battlefield2042:
                    panel = EnsureBattlefield2042AdvancedSettingsPanel();
                    break;
                case GameStyleMode.Pubg:
                    panel = EnsurePubgAdvancedSettingsPanel();
                    break;
                case GameStyleMode.DeltaForce:
                    panel = EnsureDeltaForceAdvancedSettingsPanel();
                    break;
                case GameStyleMode.CustomModule:
                    if (_customModulePanel == null)
                    {
                        _customModulePanel = new CustomModulePanel();
                        _customModulePanel.StreakModeSelectionChanged += async (s, e) => await TrySyncSharedStreakSettingsAsync(GameStyleMode.CustomModule, SharedStreakSettingsStore.Load(GameStyleMode.CustomModule));
                    }
                    panel = _customModulePanel;
                    break;
                case GameStyleMode.Doubao:
                    panel = EnsureDoubaoAdvancedSettingsPanel();
                    break;
                case GameStyleMode.Dagoujiao:
                    panel = EnsureDagoujiaoAdvancedSettingsPanel();
                    break;
                case GameStyleMode.Csol:
                    panel = EnsureCsolAdvancedSettingsPanel();
                    break;
                case GameStyleMode.Crossfire:
                default:
                    panel = EnsureCrossfireAdvancedSettingsPanel();
                    break;
            }

            if (GameAdvancedSettingsPanelHost.Content != panel)
            {
                GameAdvancedSettingsPanelHost.Content = panel;
            }

            // All games now use the same per-layer appearance editor. Keep the
            // old one-toggle card hidden inside legacy panels while preserving
            // their unrelated gameplay settings and reset controls.
            if (panel is FrameworkElement panelElement
                && panelElement.FindName("VisualEffectsCard") is UIElement legacyVisualEffectsCard)
            {
                legacyVisualEffectsCard.Visibility = Visibility.Collapsed;
            }

            KillFeedbackAppearanceEditorControl?.Configure(
                GameStyleService.Current,
                LocalizationManager.Current == UiLanguage.SimplifiedChinese,
                GameThemePalette.ForMode(GameStyleService.Current));

            ApplyGameAdvancedSettingsPanelLanguage();
        }
    }
}

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
using Windows.UI.Xaml.Media.Imaging;
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

        private GameStyleMode SettingsGameStyle = GameStyleService.Current;
        private GameThemePalette SettingsTheme => _isHomePageSelected || _isSettingsWorkspaceSelected ? GameThemePalette.Home : GameThemePalette.ForMode(SettingsGameStyle);
        private bool _suppressGameStyleEvents;
        private bool _suppressCrossfireSettingEvents;
        private bool _isSettingsWorkspaceSelected = true;
        private int _gameStyleNavigationRevision;

        private void ApplyGameStyleUi()
        {
            if (_isHomePageSelected) { ApplyCompatibilityWorkspace(); return; }
            GameStyleMode mode = SettingsGameStyle;
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
            GameThemePalette theme = _isSettingsWorkspaceSelected ? GameThemePalette.Home : GameThemePalette.ForMode(mode);

            ControlTheme.Apply(this, theme);

            Visibility iconCreationVisibility = overwatch || modernWarfare2019 || apex ? Visibility.Collapsed : Visibility.Visible;
            if (ImportIconZipButton != null) ImportIconZipButton.Visibility = iconCreationVisibility;
            if (BatchImportIconZipButton != null) BatchImportIconZipButton.Visibility = iconCreationVisibility;
            if (CreateIconPackButton != null) CreateIconPackButton.Visibility = mode == GameStyleMode.Calabiyau ? Visibility.Collapsed : iconCreationVisibility;
            Visibility voiceCreationVisibility = Visibility.Visible;
            ImportVoiceZipButton.Visibility = voiceCreationVisibility;
            BatchImportVoiceZipButton.Visibility = voiceCreationVisibility;
            CreateVoicePackButton.Visibility = voiceCreationVisibility;
            VoicePackCollectionsCard.Visibility = Visibility.Visible;
            VoiceCollectionsCard.Visibility = Visibility.Visible;

            UpdateSettingsPageVisibility();
            if (_isSettingsWorkspaceSelected)
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

            if (AdvancedSettingsTabBar != null)
            {
                AdvancedSettingsTabBar.Visibility = _isSettingsWorkspaceSelected ? Visibility.Visible : Visibility.Collapsed;
            }

            if (GameWorkspaceTabBar != null)
            {
                GameWorkspaceTabBar.Visibility = !_isSettingsWorkspaceSelected ? Visibility.Visible : Visibility.Collapsed;
            }

            if (_isSettingsWorkspaceSelected)
            {
                ApplyHomeActiveTab();
                SettingsRootGrid.Background = CreateSettingsBackground(mode, true);
            }
            else
            {
                ApplyGameActiveTab();
                SettingsRootGrid.Background = CreateSettingsBackground(mode, false);
            }

            UpdateBackgroundDecoration(mode, !_isSettingsWorkspaceSelected);

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
            SetText(GameEffectsTitleText, Color.FromArgb(255, 28, 28, 30));
            SetText(VoiceCollectionsTitleText, Color.FromArgb(255, 28, 28, 30));
            SetText(VoiceCollectionsHintText, Color.FromArgb(255, 110, 110, 115));
            SetText(IconCollectionsTitleText, Color.FromArgb(255, 28, 28, 30));
            SetText(IconCollectionsHintText, Color.FromArgb(255, 110, 110, 115));
            SetText(VoiceVisibleCountText, theme.Accent);
            SetText(IconVisibleCountText, theme.Accent);
            SetText(StructureTitleText, Color.FromArgb(255, 28, 28, 30));
            SetText(StructureBodyText, Color.FromArgb(255, 110, 110, 115));
            SetText(StructureImportFolderTitleText, Color.FromArgb(255, 28, 28, 30));
            SetText(StructureImportFolderBodyText, Color.FromArgb(255, 110, 110, 115));
            SetText(StructureVoiceSpecTitleText, Color.FromArgb(255, 28, 28, 30));
            SetText(StructureVoiceSpecBodyText, Color.FromArgb(255, 58, 58, 60));
            SetText(StructureIconSpecTitleText, Color.FromArgb(255, 28, 28, 30));
            SetText(StructureIconSpecSummaryText, theme.Accent);
            SetText(StructureIconSpecFullText, Color.FromArgb(255, 58, 58, 60));
            SetText(StructureImportZipTitleText, Color.FromArgb(255, 28, 28, 30));
            SetText(StructureImportZipBodyText, Color.FromArgb(255, 110, 110, 115));
            SetText(StructureCreatorTitleText, Color.FromArgb(255, 28, 28, 30));
            SetText(StructureCreatorBodyText, Color.FromArgb(255, 110, 110, 115));
            SetText(StructureFileHintText, Color.FromArgb(255, 58, 58, 60));
            SetText(TipsTitleText, Color.FromArgb(255, 28, 28, 30));
            SetText(TipsBodyText, Color.FromArgb(255, 110, 110, 115));

            ApplySectionTheme(GameEffectsCard, theme);
            ApplyCardTheme(VoicePackCollectionsCard, theme);
            ApplyCardTheme(IconPackCollectionsCard, theme);
            ApplyCardTheme(VoiceCollectionsCard, theme);
            ApplyCardTheme(IconCollectionsCard, theme);

            ApplyButtonTheme(ImportVoiceZipButton, theme, false);
            ApplyButtonTheme(ImportIconZipButton, theme, false);
            ApplyButtonTheme(BatchImportIconZipButton, theme, false);
            ApplyButtonTheme(BatchImportVoiceZipButton, theme, false);
            ApplyButtonTheme(CreateVoicePackButton, theme, true);
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
            ApplyPackLibraryLanguage();
        }

        private static void ApplySectionTheme(Border card, GameThemePalette theme)
        {
            if (card != null)
            {
                // One layer of glass: translucent white over the page wash.
                card.Background = GameThemePalette.Glass(0.55);
                card.BorderBrush = new SolidColorBrush(Color.FromArgb(166, 255, 255, 255));
                card.CornerRadius = new CornerRadius(20);
            }
        }

        private void ApplyPageTitleTheme(GameThemePalette theme)
        {
            bool gamePage = !_isHomePageSelected && !_isSettingsWorkspaceSelected;
            bool isChinese = LocalizationManager.Current == UiLanguage.SimplifiedChinese;
            PageTitleAccent.Visibility = PageTitleLogoSurface.Visibility = gamePage ? Visibility.Visible : Visibility.Collapsed;
            PageTitleBody.Padding = gamePage ? new Thickness(20, 14, 24, 14) : new Thickness(4, 2, 4, 2);
            PageTitleSurface.BorderThickness = new Thickness(gamePage ? 1 : 0);
            PageTitleSurface.CornerRadius = new CornerRadius(gamePage ? 18 : 0);
            TitleText.FontSize = gamePage ? 24 : 22;
            if (gamePage)
            {
                GameStyleMode mode = SettingsGameStyle;
                TitleText.Text = GetGamePageTitle(mode, isChinese);
                string extension = mode == GameStyleMode.Dagoujiao ? "jpg" : "png";
                var logoUri = new Uri(mode == GameStyleMode.Battlefield6
                    ? "ms-appx:///Assets/GameStyles/battlefield6/killconfirm/textures/killicon_battlefield6_default.png"
                    : "ms-appx:///Assets/GameLogos/" + GameStyleService.ToStorageValue(mode) + "." + extension);
                if (!(PageTitleGameLogo.Source is BitmapImage currentLogo) || currentLogo.UriSource != logoUri)
                    PageTitleGameLogo.Source = new BitmapImage(logoUri) { DecodePixelWidth = 152 };
                // White-on-transparent logos need a dark tile on the light page.
                bool darkLogo = mode == GameStyleMode.ModernWarfare2019 || mode == GameStyleMode.Battlefield5
                    || mode == GameStyleMode.Battlefield6;
                PageTitleLogoSurface.Background = new SolidColorBrush(darkLogo
                    ? Color.FromArgb(255, 37, 43, 52) : Color.FromArgb(224, 255, 255, 255));
                PageTitleAccent.Background = theme.Brush(theme.Accent);
                PageTitleSurface.BorderBrush = new SolidColorBrush(Color.FromArgb(64, theme.Accent.R, theme.Accent.G, theme.Accent.B));
                var background = new LinearGradientBrush
                {
                    StartPoint = new Windows.Foundation.Point(0, 0),
                    EndPoint = new Windows.Foundation.Point(1, 1)
                };
                background.GradientStops.Add(new GradientStop { Offset = 0, Color = Color.FromArgb(245, 255, 255, 255) });
                background.GradientStops.Add(new GradientStop { Offset = 1, Color = Color.FromArgb(235,
                    (byte)(235 + theme.Accent.R * 20 / 255),
                    (byte)(235 + theme.Accent.G * 20 / 255),
                    (byte)(235 + theme.Accent.B * 20 / 255)) });
                PageTitleSurface.Background = background;
            }
            else
            {
                TitleText.Text = _isHomePageSelected ? (isChinese ? "主页" : "Home") : (isChinese ? "高级设置" : "Advanced settings");
                PageTitleGameLogo.Source = null;
                PageTitleSurface.Background = new SolidColorBrush(Colors.Transparent);
                PageTitleSurface.BorderBrush = new SolidColorBrush(Colors.Transparent);
            }
            SetText(TitleText, theme.Text);
        }

        private static string GetGamePageTitle(GameStyleMode mode, bool isChinese)
        {
            if (isChinese)
            {
                switch (mode)
                {
                    case GameStyleMode.Crossfire: return "CrossFire · 穿越火线";
                    case GameStyleMode.Csol: return "CSOL · 反恐精英 Online";
                    case GameStyleMode.Pubg: return "PUBG · 绝地求生";
                    case GameStyleMode.DeltaForce: return "三角洲行动";
                    default: return GameStyleService.ToDisplayName(mode);
                }
            }
            switch (mode)
            {
                case GameStyleMode.Crossfire: return "CrossFire";
                case GameStyleMode.Csol: return "Counter-Strike Online";
                case GameStyleMode.Valorant: return "VALORANT";
                case GameStyleMode.Overwatch: return "Overwatch";
                case GameStyleMode.ModernWarfare2019: return "Call of Duty: Modern Warfare 2019";
                case GameStyleMode.Apex: return "Apex Legends";
                case GameStyleMode.Battlefield1: return "Battlefield 1";
                case GameStyleMode.Battlefield5: return "Battlefield V";
                case GameStyleMode.Battlefield4: return "Battlefield 4";
                case GameStyleMode.Battlefield2042: return "Battlefield 2042";
                case GameStyleMode.Battlefield6: return "Battlefield 6";
                case GameStyleMode.Pubg: return "PUBG: Battlegrounds";
                case GameStyleMode.DeltaForce: return "Delta Force";
                case GameStyleMode.Doubao: return "Doubao";
                case GameStyleMode.Dagoujiao: return "Big Dog Bark";
                case GameStyleMode.Calabiyau: return "Strinova";
                case GameStyleMode.CustomModule: return "Custom";
                default: return GameStyleService.ToDisplayName(mode);
            }
        }

        private void UpdateBackgroundDecoration(GameStyleMode mode, bool gameTint = true)
        {
            var theme = GameThemePalette.ForMode(mode);
            bool valorant = mode == GameStyleMode.Valorant;
            bool csol = mode == GameStyleMode.Csol;
            bool battlefield1 = mode == GameStyleMode.Battlefield1;
            bool battlefield5 = mode == GameStyleMode.Battlefield5;
            bool battlefield4 = mode == GameStyleMode.Battlefield4;
            bool battlefield2042 = mode == GameStyleMode.Battlefield2042;
            bool fixedPreset = GameStyleService.IsModPresetGameKey(GameStyleService.ToStorageValue(mode));
            bool isDagoujiao = mode == GameStyleMode.Dagoujiao;
            bool overwatch = mode == GameStyleMode.Overwatch;
            bool modernWarfare2019 = mode == GameStyleMode.ModernWarfare2019;
            BackgroundDecoration.Visibility = Visibility.Visible;
            BackgroundDecoration.ApplyPalette(
                CreateHeroSlashBrush(mode),
                battlefield5 ? Color.FromArgb(255, 119, 243, 255) :
                battlefield2042 ? Color.FromArgb(255, 109, 255, 255) :
                mode == GameStyleMode.Battlefield6 ? Color.FromArgb(255, 238, 175, 126) :
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
                mode == GameStyleMode.Battlefield6 ? Color.FromArgb(255, 93, 120, 101) :
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
                Color.FromArgb(255, 207, 107, 0),
                gameTint ? 0.20 : 0.0);
        }

        private static void ApplyCardTheme(Border card, GameThemePalette theme)
        {
            if (card != null)
            {
                card.Background = GameThemePalette.Glass(0.55);
                card.BorderBrush = new SolidColorBrush(Color.FromArgb(166, 255, 255, 255));
                card.CornerRadius = new CornerRadius(20);
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
                string key = GameStyleService.ToStorageValue(SettingsGameStyle);
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
                    string sidebarKey = _isHomePageSelected ? "home" : _isSettingsWorkspaceSelected ? "advanced" : key;
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
                if (string.Equals(key, "home", StringComparison.OrdinalIgnoreCase)) { SelectCompatibilityWorkspace(); return; }
                _isHomePageSelected = false;
                CompatibilityPageContent.Content = null;
                if (string.Equals(key, "advanced", StringComparison.OrdinalIgnoreCase))
                {
                    _isSettingsWorkspaceSelected = true;
                    BeginGameStyleTransition();
                    ApplyGameStyleUi();
                    ApplyLanguage();
                    return;
                }

                _isSettingsWorkspaceSelected = false;
                SelectGameStyle(key);
            }
        }

        private void UpdateSettingsPageVisibility()
        {
            CompatibilityPageContent.Visibility = _isHomePageSelected ? Visibility.Visible : Visibility.Collapsed;
            if (AdvancedSettingsContent != null)
            {
                AdvancedSettingsContent.Visibility = _isSettingsWorkspaceSelected && !_isHomePageSelected
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }

            if (GamePageContent != null)
            {
                GamePageContent.Visibility = _isSettingsWorkspaceSelected || _isHomePageSelected
                    ? Visibility.Collapsed
                    : Visibility.Visible;
            }
        }

        private void SelectGameStyle(string key)
        {
            GameStyleMode newMode = GameStyleService.FromKey(key);
            BeginGameStyleTransition();
            SettingsGameStyle = newMode;
            RefreshSettingsGamePage(newMode);
        }

        private void BeginGameStyleTransition()
        {
            ResetNativePackDrag();
            System.Threading.Interlocked.Increment(ref _gameStyleNavigationRevision);
            System.Threading.Interlocked.Increment(ref _packListReloadVersion);
            _loadedVoicePackStyle = null;
            _loadedIconPackStyle = null;
            UpdateSettingsPageVisibility();

            if (GameAdvancedSettingsPanelHost != null)
            {
                GameAdvancedSettingsPanelHost.Content = null;
            }

            if (VoicePackListPanel != null) VoicePackListPanel.ItemsSource = null;
            if (IconPackListPanel != null) IconPackListPanel.ItemsSource = null;
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
            ControlTheme.Apply(GameStyleSidebarSelector, theme);
            if (GameModeSidebar != null)
            {
                GameModeSidebar.Background = GameThemePalette.Glass(0.66);
                GameModeSidebar.BorderBrush = new SolidColorBrush(Color.FromArgb(217, 255, 255, 255));
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
            switch (SettingsGameStyle)
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
                case GameStyleMode.Battlefield6:
                    panel = EnsureBattlefield6AdvancedSettingsPanel();
                    break;
                case GameStyleMode.Pubg:
                    panel = EnsurePubgAdvancedSettingsPanel();
                    break;
                case GameStyleMode.DeltaForce:
                    panel = EnsureDeltaForceAdvancedSettingsPanel();
                    break;
                case GameStyleMode.Calabiyau:
                    panel = new TextBlock { Text = LocalizationManager.Current == UiLanguage.SimplifiedChinese
                        ? "默认击杀动画 · 3.5 秒。当前版本不含音效、粒子和连杀星级。"
                        : "Default kill animation · 3.5 s. Audio, particles and streak stars are not included.", TextWrapping = TextWrapping.Wrap };
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
                SettingsGameStyle,
                LocalizationManager.Current == UiLanguage.SimplifiedChinese,
                GameThemePalette.ForMode(SettingsGameStyle));

            ApplyGameAdvancedSettingsPanelLanguage();
        }
    }
}

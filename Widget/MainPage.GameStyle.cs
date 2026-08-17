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
using Windows.UI.Xaml.Shapes;
using Windows.Web.Http;

namespace KillConfirmGameBar
{
    public sealed partial class MainPage
    {
        private CrossfireAdvancedEffectsPanel _crossfireAdvancedEffectsPanel;
        private CrossfireStylePanel _crossfireStylePanel;
        private CsolAdvancedEffectsPanel _csolAdvancedEffectsPanel;
        private ValorantAdvancedEffectsPanel _valorantAdvancedEffectsPanel;
        private Battlefield1AdvancedEffectsPanel _battlefield1AdvancedEffectsPanel;
        private Battlefield5AdvancedEffectsPanel _battlefield5AdvancedEffectsPanel;
        private Battlefield4AdvancedEffectsPanel _battlefield4AdvancedEffectsPanel;
        private Battlefield2042AdvancedEffectsPanel _battlefield2042AdvancedEffectsPanel;
        private PubgAdvancedEffectsPanel _pubgAdvancedEffectsPanel;
        private DeltaForceAdvancedEffectsPanel _deltaForceAdvancedEffectsPanel;
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
            bool battlefield2042 = mode == GameStyleMode.Battlefield2042;
            bool fixedPreset = GameStyleService.IsModPresetGameKey(GameStyleService.ToStorageValue(mode));
            bool isCrossfire = mode == GameStyleMode.Crossfire;
            bool isDagoujiao = mode == GameStyleMode.Dagoujiao;
            GameThemePalette theme = _isHomePageSelected ? GameThemePalette.Home : GameThemePalette.ForMode(mode);

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

            if (CfWorkspaceTabBar != null)
            {
                CfWorkspaceTabBar.Visibility = (isCrossfire && !_isHomePageSelected) ? Visibility.Visible : Visibility.Collapsed;
            }

            if (CsolWorkspaceTabBar != null)
            {
                CsolWorkspaceTabBar.Visibility = (csol && !_isHomePageSelected) ? Visibility.Visible : Visibility.Collapsed;
            }

            if (DagoujiaoWorkspaceTabBar != null)
            {
                DagoujiaoWorkspaceTabBar.Visibility = (isDagoujiao && !_isHomePageSelected) ? Visibility.Visible : Visibility.Collapsed;
            }

            if (_isHomePageSelected)
            {
                ApplyHomeActiveTab();
            }
            else if (isCrossfire)
            {
                ApplyCfActiveTab();
            }
            else if (csol)
            {
                ApplyCsolActiveTab();
            }
            else if (isDagoujiao)
            {
                ApplyDagoujiaoActiveTab();
            }
            else
            {
                if (GameEffectsCard != null) GameEffectsCard.Visibility = Visibility.Visible;
                if (VoicePackCollectionsCard != null) VoicePackCollectionsCard.Visibility = Visibility.Collapsed;
                if (IconPackCollectionsCard != null) IconPackCollectionsCard.Visibility = Visibility.Collapsed;
                if (VoiceCollectionsCard != null) VoiceCollectionsCard.Visibility = Visibility.Collapsed;
                if (IconCollectionsCard != null) IconCollectionsCard.Visibility = Visibility.Collapsed;
                if (CsolGuideCard != null) CsolGuideCard.Visibility = Visibility.Collapsed;
            }

            SettingsRootGrid.Background = CreateSettingsBackground(_isHomePageSelected ? GameStyleMode.Crossfire : mode);
            HeroSlash.Visibility = Visibility.Visible;
            HeroSlashLight.Visibility = Visibility.Visible;
            FrameStripeOne.Visibility = Visibility.Visible;
            FrameStripeTwo.Visibility = Visibility.Visible;
            AccentLineOne.Visibility = Visibility.Visible;
            AccentLineTwo.Visibility = Visibility.Visible;
            AccentLineThree.Visibility = Visibility.Visible;

            HeroSlash.Fill = CreateHeroSlashBrush(_isHomePageSelected ? GameStyleMode.Crossfire : mode);
            HeroSlashLight.Fill = new SolidColorBrush(battlefield5 ? Color.FromArgb(255, 119, 243, 255) : battlefield2042 ? Color.FromArgb(255, 109, 255, 255) : battlefield1 ? Color.FromArgb(255, 255, 218, 166) : valorant ? Color.FromArgb(255, 255, 170, 178) : csol ? Color.FromArgb(255, 255, 168, 150) : Color.FromArgb(255, 255, 240, 213));
            FrameStripeOne.Stroke = new SolidColorBrush(battlefield5 ? Color.FromArgb(255, 58, 137, 166) : battlefield2042 ? Color.FromArgb(255, 60, 128, 146) : battlefield1 ? Color.FromArgb(255, 88, 110, 126) : valorant ? Color.FromArgb(255, 59, 78, 102) : csol ? Color.FromArgb(255, 120, 37, 42) : Color.FromArgb(255, 196, 196, 196));
            FrameStripeTwo.Stroke = FrameStripeOne.Stroke;
            AccentLineOne.Fill = new SolidColorBrush(fixedPreset || csol ? theme.Accent : valorant ? theme.Secondary : Color.FromArgb(255, 207, 107, 0));
            AccentLineTwo.Fill = AccentLineOne.Fill;
            AccentLineThree.Fill = AccentLineOne.Fill;

            SetText(TitleText, theme.Text);
            SetText(GameStyleLabelText, theme.Text);
            SetText(GameStyleSidebarTitleText, theme.MutedText);
            SetText(GameEffectsTitleText, theme.Text);
            SetText(VoiceCollectionsTitleText, theme.Text);
            SetText(VoiceCollectionsHintText, theme.MutedText);
            SetText(IconCollectionsTitleText, theme.Text);
            SetText(IconCollectionsHintText, theme.MutedText);
            SetText(VoiceVisibleCountText, valorant || csol ? theme.Secondary : Color.FromArgb(255, 46, 136, 184));
            SetText(IconVisibleCountText, valorant || csol ? theme.Secondary : Color.FromArgb(255, 46, 136, 184));
            SetText(StructureTitleText, theme.Text);
            SetText(StructureBodyText, theme.MutedText);
            SetText(StructureImportFolderTitleText, theme.Text);
            SetText(StructureImportFolderBodyText, theme.MutedText);
            SetText(StructureVoiceSpecTitleText, theme.Text);
            SetText(StructureVoiceSpecBodyText, theme.MutedText);
            SetText(StructureIconSpecTitleText, theme.Text);
            SetText(StructureIconSpecSummaryText, theme.WarningText);
            SetText(StructureIconSpecFullText, theme.MutedText);
            SetText(StructureImportZipTitleText, theme.Text);
            SetText(StructureImportZipBodyText, theme.MutedText);
            SetText(StructureCreatorTitleText, theme.Text);
            SetText(StructureCreatorBodyText, theme.MutedText);
            SetText(StructureFileHintText, theme.WarningText);
            SetText(TipsTitleText, theme.Text);
            SetText(TipsBodyText, theme.MutedText);

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
                card.Background = new SolidColorBrush(theme.Panel);
                card.BorderBrush = new SolidColorBrush(theme.Border);
            }
        }

        private static void ApplyCardTheme(Border card, GameThemePalette theme)
        {
            if (card != null)
            {
                card.Background = new SolidColorBrush(theme.Card);
                card.BorderBrush = new SolidColorBrush(theme.SoftBorder);
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
                    foreach (object item in GameStyleSidebarSelector.Items)
                    {
                        if (item is ListViewItem sidebarItem && sidebarItem.Tag is string tag && string.Equals(tag, sidebarKey, System.StringComparison.OrdinalIgnoreCase))
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

        private void OnGameStyleSidebarSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressGameStyleEvents)
            {
                return;
            }

            if (GameStyleSidebarSelector?.SelectedItem is ListViewItem selected && selected.Tag is string key)
            {
                if (string.Equals(key, "home", StringComparison.OrdinalIgnoreCase))
                {
                    _isHomePageSelected = true;
                    BeginGameStyleTransition();
                    ApplyGameStyleSidebarTheme(GameThemePalette.Current);
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

            foreach (object entry in GameStyleSidebarSelector.Items)
            {
                if (!(entry is ListViewItem item) || !(item.Content is Border tile))
                {
                    continue;
                }

                bool selected = item.IsSelected;
                item.Background = new SolidColorBrush(Colors.Transparent);
                tile.Background = new SolidColorBrush(selected ? theme.AccentSoft : theme.SubtleField);
                tile.BorderBrush = new SolidColorBrush(selected ? theme.Accent : theme.SoftBorder);
                tile.BorderThickness = new Thickness(selected ? 2 : 1);
                tile.Opacity = selected ? 1.0 : 0.78;
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

            ApplyGameAdvancedSettingsPanelLanguage();
        }

        private CrossfireAdvancedEffectsPanel EnsureCrossfireAdvancedSettingsPanel()
        {
            if (_crossfireAdvancedEffectsPanel == null)
            {
                _crossfireAdvancedEffectsPanel = new CrossfireAdvancedEffectsPanel();
                _crossfireStylePanel = new CrossfireStylePanel();
                _crossfireStylePanel.EnableStandaloneSettings();
                _crossfireAdvancedEffectsPanel.SetStylePanel(_crossfireStylePanel);
                _crossfireAdvancedEffectsPanel.StreakModeSelectionChanged += OnCrossfireGameplaySettingChanged;
                _crossfireAdvancedEffectsPanel.HeadshotAudioPrioritySelectionChanged += OnCrossfireGameplaySettingChanged;
                _crossfireAdvancedEffectsPanel.KnifeAudioPrioritySelectionChanged += OnCrossfireGameplaySettingChanged;
                _crossfireAdvancedEffectsPanel.HeadshotIconPrioritySelectionChanged += OnCrossfireGameplaySettingChanged;
                _crossfireAdvancedEffectsPanel.KnifeIconPrioritySelectionChanged += OnCrossfireGameplaySettingChanged;
                _crossfireAdvancedEffectsPanel.FirstKillAudioSelectionChanged += OnCrossfireGameplaySettingChanged;
                _crossfireAdvancedEffectsPanel.LastKillAudioSelectionChanged += OnCrossfireGameplaySettingChanged;
                _crossfireAdvancedEffectsPanel.FirstKillEffectToggled += OnCrossfireGameplaySettingChanged;
                _crossfireAdvancedEffectsPanel.LastKillEffectToggled += OnCrossfireGameplaySettingChanged;
                _crossfireAdvancedEffectsPanel.AssistAudioToggled += OnCrossfireGameplaySettingChanged;
            }

            RefreshCrossfireAdvancedSettingsPanel();
            return _crossfireAdvancedEffectsPanel;
        }

        private void RefreshCrossfireAdvancedSettingsPanel()
        {
            if (_crossfireAdvancedEffectsPanel == null)
            {
                return;
            }

            CrossfireGameplaySettingsValues settings = CrossfireGameplaySettingsStore.Load();
            _suppressCrossfireSettingEvents = true;
            try
            {
                _crossfireAdvancedEffectsPanel.SelectSettings(
                    settings.StreakMode,
                    settings.HeadshotSpecialAudioPriority,
                    settings.KnifeSpecialAudioPriority,
                    settings.HeadshotSpecialIconPriority,
                    settings.KnifeSpecialIconPriority,
                    settings.FirstKillSpecialAudio,
                    settings.LastKillSpecialAudio,
                    settings.FirstKillEffectEnabled,
                    settings.LastKillEffectEnabled,
                    settings.AssistAudioEnabled);
            }
            finally
            {
                _suppressCrossfireSettingEvents = false;
            }
            _crossfireStylePanel?.RefreshStandaloneSettings();
        }

        private async void OnCrossfireGameplaySettingChanged(object sender, RoutedEventArgs e)
        {
            if (_suppressCrossfireSettingEvents || _crossfireAdvancedEffectsPanel == null)
            {
                return;
            }

            CrossfireGameplaySettingsValues fallback = CrossfireGameplaySettingsStore.Load();
            var settings = new CrossfireGameplaySettingsValues
            {
                StreakMode = _crossfireAdvancedEffectsPanel.GetSelectedStreakMode(fallback.StreakMode),
                HeadshotSpecialAudioPriority = _crossfireAdvancedEffectsPanel.GetHeadshotSpecialAudioPriority(fallback.HeadshotSpecialAudioPriority),
                KnifeSpecialAudioPriority = _crossfireAdvancedEffectsPanel.GetKnifeSpecialAudioPriority(fallback.KnifeSpecialAudioPriority),
                HeadshotSpecialIconPriority = _crossfireAdvancedEffectsPanel.GetHeadshotSpecialIconPriority(fallback.HeadshotSpecialIconPriority),
                KnifeSpecialIconPriority = _crossfireAdvancedEffectsPanel.GetKnifeSpecialIconPriority(fallback.KnifeSpecialIconPriority),
                FirstKillSpecialAudio = _crossfireAdvancedEffectsPanel.GetFirstKillSpecialAudio(fallback.FirstKillSpecialAudio),
                LastKillSpecialAudio = _crossfireAdvancedEffectsPanel.GetLastKillSpecialAudio(fallback.LastKillSpecialAudio),
                FirstKillEffectEnabled = _crossfireAdvancedEffectsPanel.GetFirstKillEffectEnabled(fallback.FirstKillEffectEnabled),
                LastKillEffectEnabled = _crossfireAdvancedEffectsPanel.GetLastKillEffectEnabled(fallback.LastKillEffectEnabled),
                AssistAudioEnabled = _crossfireAdvancedEffectsPanel.GetAssistAudioEnabled(fallback.AssistAudioEnabled)
            };
            CrossfireGameplaySettingsStore.Save(settings);
            await TrySyncCrossfireSettingsAsync(settings);
        }

        private static async Task TrySyncCrossfireSettingsAsync(CrossfireGameplaySettingsValues settings)
        {
            try
            {
                var request = new JsonObject
                {
                    ["active"] = JsonValue.CreateBooleanValue(true),
                    ["streak_mode"] = JsonValue.CreateStringValue(settings.StreakMode),
                    ["first_kill_special_audio"] = JsonValue.CreateBooleanValue(settings.FirstKillSpecialAudio),
                    ["last_kill_special_audio"] = JsonValue.CreateBooleanValue(settings.LastKillSpecialAudio),
                    ["headshot_special_audio_priority"] = JsonValue.CreateBooleanValue(settings.HeadshotSpecialAudioPriority),
                    ["knife_special_audio_priority"] = JsonValue.CreateBooleanValue(settings.KnifeSpecialAudioPriority),
                    ["assist_audio_enabled"] = JsonValue.CreateBooleanValue(settings.AssistAudioEnabled)
                };

                using (var client = await LocalServiceAuth.CreateHttpClientAsync())
                using (var content = new HttpStringContent(
                    request.Stringify(),
                    UnicodeEncoding.Utf8,
                    "application/json"))
                {
                    await client.PostAsync(LocalServiceEndpoints.Build("/crossfire/settings"), content);
                }
            }
            catch (Exception ex)
            {
                App.Log("Sync CrossFire settings from desktop failed: " + ex.Message);
            }
        }

        private CsolAdvancedEffectsPanel EnsureCsolAdvancedSettingsPanel()
        {
            if (_csolAdvancedEffectsPanel == null)
            {
                _csolAdvancedEffectsPanel = new CsolAdvancedEffectsPanel();
                _csolAdvancedEffectsPanel.VoiceSettingChanged += OnCsolGameplaySettingChanged;
            }

            RefreshCsolAdvancedSettingsPanel();
            return _csolAdvancedEffectsPanel;
        }

        private void RefreshCsolAdvancedSettingsPanel()
        {
            if (_csolAdvancedEffectsPanel == null)
            {
                return;
            }

            CsolVoiceSettingsValues settings = CsolVoiceSettingsStore.Load();
            string streakMode = SharedStreakSettingsStore.Load(GameStyleMode.Csol);
            _suppressCrossfireSettingEvents = true;
            try
            {
                _csolAdvancedEffectsPanel.SelectSettings(
                    streakMode,
                    settings.SpecialVoicePriority,
                    settings.FirstKillIcon,
                    settings.LastKillIcon,
                    settings.VoicePicks);
            }
            finally
            {
                _suppressCrossfireSettingEvents = false;
            }
            SharedStreakSettingsStore.Save(GameStyleMode.Csol, streakMode);
        }

        private async void OnCsolGameplaySettingChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressCrossfireSettingEvents || _csolAdvancedEffectsPanel == null)
            {
                return;
            }

            CsolVoiceSettingsValues fallback = CsolVoiceSettingsStore.Load();
            string streakMode = SharedStreakSettingsStore.Normalize(
                _csolAdvancedEffectsPanel.GetSelectedStreakMode(SharedStreakSettingsStore.LifeMode));
            SharedStreakSettingsStore.Save(GameStyleMode.Csol, streakMode);
            CsolVoiceSettingsStore.Save(new CsolVoiceSettingsValues
            {
                VoicePicks = _csolAdvancedEffectsPanel.GetVoicePicks(),
                FirstKillIcon = _csolAdvancedEffectsPanel.GetFirstKillIcon(fallback.FirstKillIcon),
                LastKillIcon = _csolAdvancedEffectsPanel.GetLastKillIcon(fallback.LastKillIcon),
                SpecialVoicePriority = _csolAdvancedEffectsPanel.GetSpecialVoicePriority(fallback.SpecialVoicePriority)
            });
            await TrySyncCsolSettingsAsync();
        }

        private async Task TrySyncCsolSettingsAsync()
        {
            try
            {
                CsolVoiceSettingsValues settings = CsolVoiceSettingsStore.Load();
                var picks = new JsonObject();
                foreach (var pair in settings.VoicePicks)
                {
                    picks[pair.Key] = JsonValue.CreateStringValue(pair.Value);
                }

                var request = new JsonObject
                {
                    ["voice_picks"] = picks,
                    ["special_voice_priority"] = JsonValue.CreateBooleanValue(settings.SpecialVoicePriority)
                };

                using (var client = await LocalServiceAuth.CreateHttpClientAsync())
                using (var content = new HttpStringContent(
                    request.Stringify(),
                    UnicodeEncoding.Utf8,
                    "application/json"))
                {
                    await client.PostAsync(LocalServiceEndpoints.Build("/csol/settings"), content);
                }
            }
            catch (Exception ex)
            {
                App.Log("Sync CSOL settings from desktop failed: " + ex.Message);
            }

            try
            {
                await TrySyncSharedStreakSettingsAsync(
                    GameStyleMode.Csol,
                    SharedStreakSettingsStore.Load(GameStyleMode.Csol));
            }
            catch (Exception ex)
            {
                App.Log("Sync CSOL streak from desktop failed: " + ex.Message);
            }
        }

        private ValorantAdvancedEffectsPanel EnsureValorantAdvancedSettingsPanel()
        {
            if (_valorantAdvancedEffectsPanel == null)
            {
                _valorantAdvancedEffectsPanel = new ValorantAdvancedEffectsPanel();
                _valorantAdvancedEffectsPanel.SelectAssistAudio(
                    AssistAudioSettingsStore.Load(GameStyleMode.Valorant));
                _valorantAdvancedEffectsPanel.StreakModeSelectionChanged += OnStreakModeSelectionChanged;
                _valorantAdvancedEffectsPanel.AssistAudioToggled += OnValorantAssistAudioToggled;
            }
            string streak = SharedStreakSettingsStore.Load(GameStyleMode.Valorant);
            _valorantAdvancedEffectsPanel.SelectStreakMode(streak);
            _valorantAdvancedEffectsPanel.SelectAssistAudio(
                AssistAudioSettingsStore.Load(GameStyleMode.Valorant));
            return _valorantAdvancedEffectsPanel;
        }

        private Battlefield1AdvancedEffectsPanel EnsureBattlefield1AdvancedSettingsPanel()
        {
            if (_battlefield1AdvancedEffectsPanel == null)
            {
                _battlefield1AdvancedEffectsPanel = new Battlefield1AdvancedEffectsPanel();
                _battlefield1AdvancedEffectsPanel.MoneyRewardModeSelectionChanged += OnMoneyRewardModeSelectionChanged;
                _battlefield1AdvancedEffectsPanel.StreakModeSelectionChanged += OnStreakModeSelectionChanged;
            }
            string money = ApplicationData.Current.LocalSettings.Values["MoneyRewardMode"] as string;
            string streak = SharedStreakSettingsStore.Load(GameStyleMode.Battlefield1);
            _battlefield1AdvancedEffectsPanel.SelectMoneyRewardMode(money, "delta");
            _battlefield1AdvancedEffectsPanel.SelectStreakMode(streak);
            _battlefield1AdvancedEffectsPanel.ReloadEventSoundSettings();
            return _battlefield1AdvancedEffectsPanel;
        }

        private Battlefield5AdvancedEffectsPanel EnsureBattlefield5AdvancedSettingsPanel()
        {
            if (_battlefield5AdvancedEffectsPanel == null)
            {
                _battlefield5AdvancedEffectsPanel = new Battlefield5AdvancedEffectsPanel();
                _battlefield5AdvancedEffectsPanel.MoneyRewardModeSelectionChanged += OnMoneyRewardModeSelectionChanged;
                _battlefield5AdvancedEffectsPanel.StreakModeSelectionChanged += OnStreakModeSelectionChanged;
            }
            string money = ApplicationData.Current.LocalSettings.Values["MoneyRewardMode"] as string;
            string streak = SharedStreakSettingsStore.Load(GameStyleMode.Battlefield5);
            _battlefield5AdvancedEffectsPanel.SelectMoneyRewardMode(money, "delta");
            _battlefield5AdvancedEffectsPanel.SelectStreakMode(streak);
            _battlefield5AdvancedEffectsPanel.ReloadEventSoundSettings();
            return _battlefield5AdvancedEffectsPanel;
        }

        private Battlefield4AdvancedEffectsPanel EnsureBattlefield4AdvancedSettingsPanel()
        {
            if (_battlefield4AdvancedEffectsPanel == null)
            {
                _battlefield4AdvancedEffectsPanel = new Battlefield4AdvancedEffectsPanel();
                _battlefield4AdvancedEffectsPanel.MoneyRewardModeSelectionChanged += OnMoneyRewardModeSelectionChanged;
                _battlefield4AdvancedEffectsPanel.StreakModeSelectionChanged += OnStreakModeSelectionChanged;
            }
            string money = ApplicationData.Current.LocalSettings.Values["MoneyRewardMode"] as string;
            string streak = SharedStreakSettingsStore.Load(GameStyleMode.Battlefield4);
            _battlefield4AdvancedEffectsPanel.SelectMoneyRewardMode(money, "delta");
            _battlefield4AdvancedEffectsPanel.SelectStreakMode(streak);
            _battlefield4AdvancedEffectsPanel.ReloadEventSoundSettings();
            return _battlefield4AdvancedEffectsPanel;
        }

        private Battlefield2042AdvancedEffectsPanel EnsureBattlefield2042AdvancedSettingsPanel()
        {
            if (_battlefield2042AdvancedEffectsPanel == null)
            {
                _battlefield2042AdvancedEffectsPanel = new Battlefield2042AdvancedEffectsPanel();
                _battlefield2042AdvancedEffectsPanel.MoneyRewardModeSelectionChanged += OnMoneyRewardModeSelectionChanged;
                _battlefield2042AdvancedEffectsPanel.StreakModeSelectionChanged += OnStreakModeSelectionChanged;
            }
            string money = ApplicationData.Current.LocalSettings.Values["MoneyRewardMode"] as string;
            string streak = SharedStreakSettingsStore.Load(GameStyleMode.Battlefield2042);
            _battlefield2042AdvancedEffectsPanel.SelectMoneyRewardMode(money, "delta");
            _battlefield2042AdvancedEffectsPanel.SelectStreakMode(streak);
            _battlefield2042AdvancedEffectsPanel.ReloadEventSoundSettings();
            return _battlefield2042AdvancedEffectsPanel;
        }

        private PubgAdvancedEffectsPanel EnsurePubgAdvancedSettingsPanel()
        {
            if (_pubgAdvancedEffectsPanel == null)
            {
                _pubgAdvancedEffectsPanel = new PubgAdvancedEffectsPanel();
                _pubgAdvancedEffectsPanel.MoneyRewardModeSelectionChanged += OnMoneyRewardModeSelectionChanged;
                _pubgAdvancedEffectsPanel.StreakModeSelectionChanged += OnStreakModeSelectionChanged;
            }
            string money = ApplicationData.Current.LocalSettings.Values["MoneyRewardMode"] as string;
            string streak = SharedStreakSettingsStore.Load(GameStyleMode.Pubg);
            _pubgAdvancedEffectsPanel.SelectMoneyRewardMode(money, "delta");
            _pubgAdvancedEffectsPanel.SelectStreakMode(streak);
            return _pubgAdvancedEffectsPanel;
        }

        private DeltaForceAdvancedEffectsPanel EnsureDeltaForceAdvancedSettingsPanel()
        {
            if (_deltaForceAdvancedEffectsPanel == null)
            {
                _deltaForceAdvancedEffectsPanel = new DeltaForceAdvancedEffectsPanel();
                _deltaForceAdvancedEffectsPanel.MoneyRewardModeSelectionChanged += OnMoneyRewardModeSelectionChanged;
                _deltaForceAdvancedEffectsPanel.StreakModeSelectionChanged += OnStreakModeSelectionChanged;
            }
            string money = ApplicationData.Current.LocalSettings.Values["MoneyRewardMode"] as string;
            string streak = SharedStreakSettingsStore.Load(GameStyleMode.DeltaForce);
            _deltaForceAdvancedEffectsPanel.SelectMoneyRewardMode(money, "delta");
            _deltaForceAdvancedEffectsPanel.SelectStreakMode(streak);
            _deltaForceAdvancedEffectsPanel.ReloadEventSoundSettings();
            return _deltaForceAdvancedEffectsPanel;
        }

        private DoubaoAdvancedEffectsPanel EnsureDoubaoAdvancedSettingsPanel()
        {
            if (_doubaoAdvancedEffectsPanel == null)
            {
                _doubaoAdvancedEffectsPanel = new DoubaoAdvancedEffectsPanel();
                _doubaoAdvancedEffectsPanel.StreakModeSelectionChanged += OnStreakModeSelectionChanged;
                _doubaoAdvancedEffectsPanel.DoubaoSettingsChanged += OnDoubaoSettingsChanged;
            }

            _doubaoAdvancedEffectsPanel.SelectStreakMode(
                SharedStreakSettingsStore.Load(GameStyleMode.Doubao));
            _doubaoAdvancedEffectsPanel.RefreshSettings();
            return _doubaoAdvancedEffectsPanel;
        }

        private DagoujiaoAdvancedEffectsPanel EnsureDagoujiaoAdvancedSettingsPanel()
        {
            if (_dagoujiaoAdvancedEffectsPanel == null)
            {
                _dagoujiaoAdvancedEffectsPanel = new DagoujiaoAdvancedEffectsPanel();
                _dagoujiaoAdvancedEffectsPanel.StreakModeSelectionChanged += OnStreakModeSelectionChanged;
                _dagoujiaoAdvancedEffectsPanel.DagoujiaoSettingsChanged += OnDagoujiaoSettingsChanged;
            }
            _dagoujiaoAdvancedEffectsPanel.SelectStreakMode(
                SharedStreakSettingsStore.Load(GameStyleMode.Dagoujiao));
            _ = _dagoujiaoAdvancedEffectsPanel.RefreshSettingsAsync();
            return _dagoujiaoAdvancedEffectsPanel;
        }

        private async void OnDagoujiaoSettingsChanged(object sender, EventArgs e)
        {
            try
            {
                await DagoujiaoSettingsStore.SyncServiceAsync();
            }
            catch (Exception ex)
            {
                App.Log("Sync Dagoujiao settings from desktop failed: " + ex.Message);
            }
        }

        private async void OnDoubaoSettingsChanged(object sender, EventArgs e)
        {
            try
            {
                await DoubaoSettingsStore.SyncAsync();
            }
            catch (Exception ex)
            {
                App.Log("Sync Doubao settings from desktop failed: " + ex.Message);
            }
        }

        private void OnMoneyRewardModeSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            string mode = "delta";
            if (sender is Battlefield1AdvancedEffectsPanel p1) mode = p1.GetSelectedMoneyRewardMode("delta");
            else if (sender is Battlefield5AdvancedEffectsPanel p5) mode = p5.GetSelectedMoneyRewardMode("delta");
            else if (sender is Battlefield4AdvancedEffectsPanel p4) mode = p4.GetSelectedMoneyRewardMode("delta");
            else if (sender is Battlefield2042AdvancedEffectsPanel p2042) mode = p2042.GetSelectedMoneyRewardMode("delta");
            else if (sender is DeltaForceAdvancedEffectsPanel pDF) mode = pDF.GetSelectedMoneyRewardMode("delta");
            else if (sender is PubgAdvancedEffectsPanel pPubg) mode = pPubg.GetSelectedMoneyRewardMode("delta");

            ApplicationData.Current.LocalSettings.Values["MoneyRewardMode"] = mode;
        }

        private async void OnValorantAssistAudioToggled(object sender, RoutedEventArgs e)
        {
            if (!(sender is ValorantAdvancedEffectsPanel panel))
            {
                return;
            }

            bool enabled = panel.GetAssistAudioEnabled(false);
            AssistAudioSettingsStore.Save(GameStyleMode.Valorant, enabled);
            await TrySyncSharedStreakSettingsAsync(
                GameStyleMode.Valorant,
                SharedStreakSettingsStore.Load(GameStyleMode.Valorant));
        }

        private async void OnStreakModeSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            GameStyleMode style = GameStyleService.Current;
            string mode = SharedStreakSettingsStore.LifeMode;
            if (sender is Battlefield1AdvancedEffectsPanel p1) mode = p1.GetSelectedStreakMode(mode);
            else if (sender is Battlefield5AdvancedEffectsPanel p5) mode = p5.GetSelectedStreakMode(mode);
            else if (sender is Battlefield4AdvancedEffectsPanel p4) mode = p4.GetSelectedStreakMode(mode);
            else if (sender is Battlefield2042AdvancedEffectsPanel p2042) mode = p2042.GetSelectedStreakMode(mode);
            else if (sender is DeltaForceAdvancedEffectsPanel pDF) mode = pDF.GetSelectedStreakMode(mode);
            else if (sender is PubgAdvancedEffectsPanel pPubg) mode = pPubg.GetSelectedStreakMode(mode);
            else if (sender is ValorantAdvancedEffectsPanel pVal) mode = pVal.GetSelectedStreakMode(mode);
            else if (sender is DoubaoAdvancedEffectsPanel pDoubao) mode = pDoubao.GetSelectedStreakMode(mode);
            else if (sender is DagoujiaoAdvancedEffectsPanel pDagoujiao) mode = pDagoujiao.GetSelectedStreakMode(mode);

            SharedStreakSettingsStore.Save(style, mode);
            await TrySyncSharedStreakSettingsAsync(style, mode);
        }

        private static async Task TrySyncSharedStreakSettingsAsync(GameStyleMode style, string mode)
        {
            try
            {
                var request = new JsonObject
                {
                    ["active"] = JsonValue.CreateBooleanValue(
                        SharedStreakSettingsStore.IsSupported(style)),
                    ["streak_mode"] = JsonValue.CreateStringValue(mode),
                    ["assist_audio_enabled"] = JsonValue.CreateBooleanValue(
                        style == GameStyleMode.Valorant
                        && AssistAudioSettingsStore.Load(style)),
                    ["assist_audio_setting_active"] = JsonValue.CreateBooleanValue(
                        style == GameStyleMode.Valorant)
                };

                using (var client = await LocalServiceAuth.CreateHttpClientAsync())
                using (var content = new HttpStringContent(
                    request.Stringify(),
                    UnicodeEncoding.Utf8,
                    "application/json"))
                {
                    await client.PostAsync(
                        LocalServiceEndpoints.Build("/streak/settings"),
                        content);
                }
            }
            catch (System.Exception ex)
            {
                App.Log("Sync shared streak settings from desktop failed: " + ex.Message);
            }
        }

        private void ApplyGameAdvancedSettingsPanelTheme()
        {
            GameThemePalette theme = GameThemePalette.Current;
            if (_crossfireAdvancedEffectsPanel != null) _crossfireAdvancedEffectsPanel.ApplyTheme(theme);
            if (_csolAdvancedEffectsPanel != null) _csolAdvancedEffectsPanel.ApplyTheme(theme);
            if (_valorantAdvancedEffectsPanel != null) _valorantAdvancedEffectsPanel.ApplyTheme(theme);
            if (_battlefield1AdvancedEffectsPanel != null) _battlefield1AdvancedEffectsPanel.ApplyTheme(theme);
            if (_battlefield5AdvancedEffectsPanel != null) _battlefield5AdvancedEffectsPanel.ApplyTheme(theme);
            if (_battlefield4AdvancedEffectsPanel != null) _battlefield4AdvancedEffectsPanel.ApplyTheme(theme);
            if (_battlefield2042AdvancedEffectsPanel != null) _battlefield2042AdvancedEffectsPanel.ApplyTheme(theme);
            if (_pubgAdvancedEffectsPanel != null) _pubgAdvancedEffectsPanel.ApplyTheme(theme);
            if (_deltaForceAdvancedEffectsPanel != null) _deltaForceAdvancedEffectsPanel.ApplyTheme(theme);
            if (_doubaoAdvancedEffectsPanel != null) _doubaoAdvancedEffectsPanel.ApplyTheme(theme);
            if (_dagoujiaoAdvancedEffectsPanel != null) _dagoujiaoAdvancedEffectsPanel.ApplyTheme(theme);
            AdvancedEffectsPanelSupport.ApplySoftenedTree(GameAdvancedSettingsPanelHost, theme);
            AdvancedEffectsPanelSupport.ApplySoftenedTree(
                GameAdvancedSettingsPanelHost.Content as DependencyObject,
                theme);
        }

        private static bool IsDarkColor(Color color)
        {
            return (color.R * 0.299 + color.G * 0.587 + color.B * 0.114) < 128;
        }

        private void ApplyGameAdvancedSettingsPanelLanguage()
        {
            bool isChinese = LocalizationManager.Current == UiLanguage.SimplifiedChinese;
            if (_crossfireAdvancedEffectsPanel != null) _crossfireAdvancedEffectsPanel.ApplyLanguage(isChinese);
            if (_csolAdvancedEffectsPanel != null) _csolAdvancedEffectsPanel.ApplyLanguage(isChinese);
            if (_valorantAdvancedEffectsPanel != null) _valorantAdvancedEffectsPanel.ApplyLanguage(isChinese);
            if (_battlefield1AdvancedEffectsPanel != null) _battlefield1AdvancedEffectsPanel.ApplyLanguage(isChinese);
            if (_battlefield5AdvancedEffectsPanel != null) _battlefield5AdvancedEffectsPanel.ApplyLanguage(isChinese);
            if (_battlefield4AdvancedEffectsPanel != null) _battlefield4AdvancedEffectsPanel.ApplyLanguage(isChinese);
            if (_battlefield2042AdvancedEffectsPanel != null) _battlefield2042AdvancedEffectsPanel.ApplyLanguage(isChinese);
            if (_pubgAdvancedEffectsPanel != null) _pubgAdvancedEffectsPanel.ApplyLanguage(isChinese);
            if (_deltaForceAdvancedEffectsPanel != null) _deltaForceAdvancedEffectsPanel.ApplyLanguage(isChinese);
            if (_doubaoAdvancedEffectsPanel != null) _doubaoAdvancedEffectsPanel.ApplyLanguage(isChinese);
            if (_dagoujiaoAdvancedEffectsPanel != null) _dagoujiaoAdvancedEffectsPanel.ApplyLanguage(isChinese);
        }

        private static Brush CreateSettingsBackground(GameStyleMode mode)
        {
            var brush = new LinearGradientBrush { StartPoint = new Windows.Foundation.Point(0, 0), EndPoint = new Windows.Foundation.Point(1, 1) };
            if (mode == GameStyleMode.Battlefield5)
            {
                brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 5, 21, 38), Offset = 0 });
                brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 16, 62, 92), Offset = 1 });
            }
            else if (mode == GameStyleMode.Valorant)
            {
                brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 10, 14, 22), Offset = 0 });
                brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 22, 28, 42), Offset = 1 });
            }
            else if (mode == GameStyleMode.Battlefield1)
            {
                brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 12, 21, 28), Offset = 0 });
                brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 44, 59, 69), Offset = 1 });
            }
            else if (mode == GameStyleMode.Battlefield4)
            {
                brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 9, 20, 31), Offset = 0 });
                brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 24, 55, 80), Offset = 1 });
            }
            else if (mode == GameStyleMode.Battlefield2042)
            {
                brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 4, 16, 24), Offset = 0 });
                brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 20, 68, 82), Offset = 0.58 });
                brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 61, 16, 35), Offset = 1 });
            }
            else if (mode == GameStyleMode.Pubg)
            {
                brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 26, 24, 17), Offset = 0 });
                brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 74, 61, 30), Offset = 1 });
            }
            else if (mode == GameStyleMode.DeltaForce)
            {
                brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 9, 21, 19), Offset = 0 });
                brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 37, 69, 47), Offset = 1 });
            }
            else if (mode == GameStyleMode.Doubao)
            {
                brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 24, 16, 8), Offset = 0 });
                brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 86, 49, 15), Offset = 0.68 });
                brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 45, 20, 8), Offset = 1 });
            }
            else if (mode == GameStyleMode.Dagoujiao)
            {
                brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 17, 23, 13), Offset = 0 });
                brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 55, 75, 30), Offset = 0.68 });
                brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 30, 38, 19), Offset = 1 });
            }
            else if (mode == GameStyleMode.Csol)
            {
                brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 247, 248, 249), Offset = 0 });
                brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 229, 232, 235), Offset = 0.72 });
                brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 248, 224, 225), Offset = 1 });
            }
            else
            {
                brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 242, 243, 242), Offset = 0 });
                brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 216, 217, 216), Offset = 1 });
            }

            return brush;
        }

        private static Brush CreateHeroSlashBrush(GameStyleMode mode)
        {
            var brush = new LinearGradientBrush { StartPoint = new Windows.Foundation.Point(0, 0), EndPoint = new Windows.Foundation.Point(1, 1) };
            if (mode == GameStyleMode.Battlefield5)
            {
                brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 0, 211, 255), Offset = 0 });
                brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 255, 90, 56), Offset = 0.58 });
                brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 174, 14, 57), Offset = 1 });
            }
            else if (mode == GameStyleMode.Valorant)
            {
                brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 255, 70, 85), Offset = 0 });
                brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 131, 38, 55), Offset = 0.58 });
                brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 63, 25, 38), Offset = 1 });
            }
            else if (mode == GameStyleMode.Battlefield1)
            {
                brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 255, 196, 100), Offset = 0 });
                brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 242, 126, 38), Offset = 0.55 });
                brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 62, 86, 102), Offset = 1 });
            }
            else if (mode == GameStyleMode.Battlefield4)
            {
                brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 71, 183, 255), Offset = 0 });
                brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 255, 138, 45), Offset = 0.58 });
                brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 33, 70, 104), Offset = 1 });
            }
            else if (mode == GameStyleMode.Battlefield2042)
            {
                brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 34, 221, 221), Offset = 0 });
                brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 255, 79, 82), Offset = 0.58 });
                brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 72, 23, 48), Offset = 1 });
            }
            else if (mode == GameStyleMode.Pubg)
            {
                brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 245, 182, 66), Offset = 0 });
                brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 130, 104, 40), Offset = 0.58 });
                brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 36, 55, 36), Offset = 1 });
            }
            else if (mode == GameStyleMode.DeltaForce)
            {
                brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 102, 214, 134), Offset = 0 });
                brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 255, 135, 40), Offset = 0.58 });
                brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 24, 58, 43), Offset = 1 });
            }
            else if (mode == GameStyleMode.Doubao)
            {
                brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 255, 232, 150), Offset = 0 });
                brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 255, 176, 43), Offset = 0.55 });
                brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 152, 56, 15), Offset = 1 });
            }
            else if (mode == GameStyleMode.Dagoujiao)
            {
                brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 223, 244, 160), Offset = 0 });
                brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 154, 205, 57), Offset = 0.55 });
                brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 67, 91, 34), Offset = 1 });
            }
            else if (mode == GameStyleMode.Csol)
            {
                brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 194, 32, 40), Offset = 0 });
                brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 229, 68, 38), Offset = 0.58 });
                brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 74, 80, 88), Offset = 1 });
            }
            else
            {
                brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 242, 154, 23), Offset = 0 });
                brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 234, 127, 5), Offset = 0.58 });
                brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 219, 105, 0), Offset = 1 });
            }

            return brush;
        }

        private static void SetText(TextBlock textBlock, Color color)
        {
            if (textBlock != null)
            {
                textBlock.Foreground = new SolidColorBrush(color);
            }
        }

        private static void ApplyButtonTheme(Button button, GameThemePalette theme, bool primary)
        {
            if (button == null)
            {
                return;
            }

            button.Background = new SolidColorBrush(primary ? theme.Accent : theme.Field);
            button.BorderBrush = new SolidColorBrush(primary ? theme.AccentText : theme.SoftBorder);
            button.Foreground = new SolidColorBrush(primary ? Colors.White : theme.Text);
        }

        private static void ApplyPackCardTheme(Panel panel, GameThemePalette theme)
        {
            if (panel == null)
            {
                return;
            }

            foreach (UIElement child in panel.Children)
            {
                ApplyThemeToElement(child, theme);
            }
        }

        private static void ApplyThemeToElement(DependencyObject element, GameThemePalette theme)
        {
            if (element is Border border)
            {
                border.Background = new SolidColorBrush(theme.Card);
                border.BorderBrush = new SolidColorBrush(theme.SoftBorder);
            }
            else if (element is TextBlock textBlock)
            {
                textBlock.Foreground = new SolidColorBrush(textBlock.FontSize <= 11 ? theme.MutedText : theme.Text);
            }
            else if (element is Button button)
            {
                ApplyButtonTheme(button, theme, false);
            }

            int count = VisualTreeHelper.GetChildrenCount(element);
            for (int i = 0; i < count; i++)
            {
                ApplyThemeToElement(VisualTreeHelper.GetChild(element, i), theme);
            }
        }
    }
}

using KillConfirmGameBar.Services;
using KillConfirmGameBar.Controls.Settings;
using KillConfirmGameBar.Controls.GameStyles;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Shapes;

namespace KillConfirmGameBar
{
    public sealed partial class MainPage
    {
        private CrossfireAdvancedSettingsPanel _crossfireAdvancedSettingsPanel;
        private ValorantAdvancedEffectsPanel _valorantAdvancedEffectsPanel;
        private Battlefield1AdvancedEffectsPanel _battlefield1AdvancedEffectsPanel;
        private Battlefield5AdvancedEffectsPanel _battlefield5AdvancedEffectsPanel;
        private Battlefield4AdvancedEffectsPanel _battlefield4AdvancedEffectsPanel;
        private Battlefield2042AdvancedEffectsPanel _battlefield2042AdvancedEffectsPanel;
        private PubgAdvancedEffectsPanel _pubgAdvancedEffectsPanel;
        private DeltaForceAdvancedEffectsPanel _deltaForceAdvancedEffectsPanel;

        private bool _suppressGameStyleEvents;

        private void ApplyGameStyleUi()
        {
            GameStyleMode mode = GameStyleService.Current;
            SyncGameStyleSelector();
            bool valorant = mode == GameStyleMode.Valorant;
            bool battlefield = mode == GameStyleMode.Battlefield1 || mode == GameStyleMode.Battlefield5;
            bool battlefield1 = mode == GameStyleMode.Battlefield1;
            bool battlefield5 = mode == GameStyleMode.Battlefield5;
            bool battlefield2042 = mode == GameStyleMode.Battlefield2042;
            bool fixedPreset = GameStyleService.IsModPresetGameKey(GameStyleService.ToStorageValue(mode));
            bool hideCfPacks = valorant || fixedPreset;
            GameThemePalette theme = GameThemePalette.Current;
            MountGameAdvancedSettingsPanel();
            VoicePackCollectionsCard.Visibility = hideCfPacks ? Visibility.Collapsed : Visibility.Visible;
            IconPackCollectionsCard.Visibility = hideCfPacks ? Visibility.Collapsed : Visibility.Visible;
            VoiceCollectionsCard.Visibility = hideCfPacks ? Visibility.Collapsed : Visibility.Visible;
            IconCollectionsCard.Visibility = hideCfPacks ? Visibility.Collapsed : Visibility.Visible;

            SettingsRootGrid.Background = CreateSettingsBackground(mode);
            HeroSlash.Fill = CreateHeroSlashBrush(mode);
            HeroSlashLight.Fill = new SolidColorBrush(battlefield5 ? Color.FromArgb(255, 119, 243, 255) : battlefield2042 ? Color.FromArgb(255, 109, 255, 255) : battlefield1 ? Color.FromArgb(255, 255, 218, 166) : valorant ? Color.FromArgb(255, 255, 170, 178) : Color.FromArgb(255, 255, 240, 213));
            FrameStripeOne.Stroke = new SolidColorBrush(battlefield5 ? Color.FromArgb(255, 58, 137, 166) : battlefield2042 ? Color.FromArgb(255, 60, 128, 146) : battlefield1 ? Color.FromArgb(255, 88, 110, 126) : valorant ? Color.FromArgb(255, 59, 78, 102) : Color.FromArgb(255, 196, 196, 196));
            FrameStripeTwo.Stroke = FrameStripeOne.Stroke;
            AccentLineOne.Fill = new SolidColorBrush(fixedPreset ? theme.Accent : valorant ? theme.Secondary : Color.FromArgb(255, 207, 107, 0));
            AccentLineTwo.Fill = AccentLineOne.Fill;
            AccentLineThree.Fill = AccentLineOne.Fill;

            SetText(TitleText, theme.Text);
            SetText(GameStyleLabelText, theme.Text);
            SetText(GeneralSettingsTitleText, theme.Text);
            SetText(CloseBehaviorLabelText, theme.MutedText);
            SetText(VoiceCollectionsTitleText, theme.Text);
            SetText(VoiceCollectionsHintText, theme.MutedText);
            SetText(IconCollectionsTitleText, theme.Text);
            SetText(IconCollectionsHintText, theme.MutedText);
            SetText(VoiceVisibleCountText, valorant ? theme.Secondary : Color.FromArgb(255, 46, 136, 184));
            SetText(IconVisibleCountText, valorant ? theme.Secondary : Color.FromArgb(255, 46, 136, 184));
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

            ApplyCardTheme(GeneralSettingsCard, theme);
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
            ApplyGameAdvancedSettingsPanelTheme();
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
            if (GameStyleSelector == null)
            {
                return;
            }

            _suppressGameStyleEvents = true;
            try
            {
                string key = GameStyleService.ToStorageValue(GameStyleService.Current);
                foreach (object item in GameStyleSelector.Items)
                {
                    if (item is ComboBoxItem comboItem && comboItem.Tag is string tag && string.Equals(tag, key, System.StringComparison.OrdinalIgnoreCase))
                    {
                        GameStyleSelector.SelectedItem = comboItem;
                        break;
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
                GameStyleMode newMode = GameStyleService.FromKey(key);
                if (GameStyleService.Current != newMode)
                {
                    GameStyleService.Current = newMode;
                    ApplyGameStyleUi();
                }
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

        private CrossfireAdvancedSettingsPanel EnsureCrossfireAdvancedSettingsPanel()
        {
            return _crossfireAdvancedSettingsPanel ?? (_crossfireAdvancedSettingsPanel = new CrossfireAdvancedSettingsPanel());
        }

        private ValorantAdvancedEffectsPanel EnsureValorantAdvancedSettingsPanel()
        {
            if (_valorantAdvancedEffectsPanel == null)
            {
                _valorantAdvancedEffectsPanel = new ValorantAdvancedEffectsPanel();
                _valorantAdvancedEffectsPanel.StreakModeSelectionChanged += OnStreakModeSelectionChanged;
            }
            string streak = ApplicationData.Current.LocalSettings.Values["SharedStreakMode"] as string;
            _valorantAdvancedEffectsPanel.SelectStreakMode(streak);
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
            string streak = ApplicationData.Current.LocalSettings.Values["SharedStreakMode"] as string;
            _battlefield1AdvancedEffectsPanel.SelectMoneyRewardMode(money, "rules");
            _battlefield1AdvancedEffectsPanel.SelectStreakMode(streak);
            return _battlefield1AdvancedEffectsPanel;
        }

        private Battlefield5AdvancedEffectsPanel EnsureBattlefield5AdvancedSettingsPanel()
        {
            if (_battlefield5AdvancedEffectsPanel == null)
            {
                _battlefield5AdvancedEffectsPanel = new Battlefield5AdvancedEffectsPanel();
                _battlefield5AdvancedEffectsPanel.MoneyRewardModeSelectionChanged += OnMoneyRewardModeSelectionChanged;
            }
            string money = ApplicationData.Current.LocalSettings.Values["MoneyRewardMode"] as string;
            _battlefield5AdvancedEffectsPanel.SelectMoneyRewardMode(money, "rules");
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
            string streak = ApplicationData.Current.LocalSettings.Values["SharedStreakMode"] as string;
            _battlefield4AdvancedEffectsPanel.SelectMoneyRewardMode(money, "rules");
            _battlefield4AdvancedEffectsPanel.SelectStreakMode(streak);
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
            string streak = ApplicationData.Current.LocalSettings.Values["SharedStreakMode"] as string;
            _battlefield2042AdvancedEffectsPanel.SelectMoneyRewardMode(money, "rules");
            _battlefield2042AdvancedEffectsPanel.SelectStreakMode(streak);
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
            string streak = ApplicationData.Current.LocalSettings.Values["SharedStreakMode"] as string;
            _pubgAdvancedEffectsPanel.SelectMoneyRewardMode(money, "rules");
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
            string streak = ApplicationData.Current.LocalSettings.Values["SharedStreakMode"] as string;
            _deltaForceAdvancedEffectsPanel.SelectMoneyRewardMode(money, "rules");
            _deltaForceAdvancedEffectsPanel.SelectStreakMode(streak);
            return _deltaForceAdvancedEffectsPanel;
        }

        private void OnMoneyRewardModeSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            string mode = "rules";
            if (sender is Battlefield1AdvancedEffectsPanel p1) mode = p1.GetSelectedMoneyRewardMode("rules");
            else if (sender is Battlefield5AdvancedEffectsPanel p5) mode = p5.GetSelectedMoneyRewardMode("rules");
            else if (sender is Battlefield4AdvancedEffectsPanel p4) mode = p4.GetSelectedMoneyRewardMode("rules");
            else if (sender is Battlefield2042AdvancedEffectsPanel p2042) mode = p2042.GetSelectedMoneyRewardMode("rules");
            else if (sender is DeltaForceAdvancedEffectsPanel pDF) mode = pDF.GetSelectedMoneyRewardMode("rules");
            else if (sender is PubgAdvancedEffectsPanel pPubg) mode = pPubg.GetSelectedMoneyRewardMode("rules");

            ApplicationData.Current.LocalSettings.Values["MoneyRewardMode"] = mode;
        }

        private void OnStreakModeSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            string mode = "life";
            if (sender is Battlefield1AdvancedEffectsPanel p1) mode = p1.GetSelectedStreakMode("life");
            else if (sender is Battlefield4AdvancedEffectsPanel p4) mode = p4.GetSelectedStreakMode("life");
            else if (sender is Battlefield2042AdvancedEffectsPanel p2042) mode = p2042.GetSelectedStreakMode("life");
            else if (sender is DeltaForceAdvancedEffectsPanel pDF) mode = pDF.GetSelectedStreakMode("life");
            else if (sender is PubgAdvancedEffectsPanel pPubg) mode = pPubg.GetSelectedStreakMode("life");
            else if (sender is ValorantAdvancedEffectsPanel pVal) mode = pVal.GetSelectedStreakMode("life");

            ApplicationData.Current.LocalSettings.Values["SharedStreakMode"] = mode;
        }

        private void ApplyGameAdvancedSettingsPanelTheme()
        {
            GameThemePalette theme = GameThemePalette.Current;
            if (_crossfireAdvancedSettingsPanel != null) _crossfireAdvancedSettingsPanel.ApplyTheme(theme);
            if (_valorantAdvancedEffectsPanel != null) _valorantAdvancedEffectsPanel.ApplyTheme(theme);
            if (_battlefield1AdvancedEffectsPanel != null) _battlefield1AdvancedEffectsPanel.ApplyTheme(theme);
            if (_battlefield5AdvancedEffectsPanel != null) _battlefield5AdvancedEffectsPanel.ApplyTheme(theme);
            if (_battlefield4AdvancedEffectsPanel != null) _battlefield4AdvancedEffectsPanel.ApplyTheme(theme);
            if (_battlefield2042AdvancedEffectsPanel != null) _battlefield2042AdvancedEffectsPanel.ApplyTheme(theme);
            if (_pubgAdvancedEffectsPanel != null) _pubgAdvancedEffectsPanel.ApplyTheme(theme);
            if (_deltaForceAdvancedEffectsPanel != null) _deltaForceAdvancedEffectsPanel.ApplyTheme(theme);
        }

        private void ApplyGameAdvancedSettingsPanelLanguage()
        {
            bool isChinese = LocalizationManager.Current == UiLanguage.SimplifiedChinese;
            if (_crossfireAdvancedSettingsPanel != null) _crossfireAdvancedSettingsPanel.ApplyLanguage(isChinese);
            if (_valorantAdvancedEffectsPanel != null) _valorantAdvancedEffectsPanel.ApplyLanguage(isChinese);
            if (_battlefield1AdvancedEffectsPanel != null) _battlefield1AdvancedEffectsPanel.ApplyLanguage(isChinese);
            if (_battlefield5AdvancedEffectsPanel != null) _battlefield5AdvancedEffectsPanel.ApplyLanguage(isChinese);
            if (_battlefield4AdvancedEffectsPanel != null) _battlefield4AdvancedEffectsPanel.ApplyLanguage(isChinese);
            if (_battlefield2042AdvancedEffectsPanel != null) _battlefield2042AdvancedEffectsPanel.ApplyLanguage(isChinese);
            if (_pubgAdvancedEffectsPanel != null) _pubgAdvancedEffectsPanel.ApplyLanguage(isChinese);
            if (_deltaForceAdvancedEffectsPanel != null) _deltaForceAdvancedEffectsPanel.ApplyLanguage(isChinese);
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

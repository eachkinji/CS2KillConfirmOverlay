using KillConfirmGameBar.Services;
using KillConfirmGameBar.Controls.Settings;
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
        private ValorantAdvancedSettingsPanel _valorantAdvancedSettingsPanel;
        private Battlefield1AdvancedSettingsPanel _battlefield1AdvancedSettingsPanel;
        private Battlefield5AdvancedSettingsPanel _battlefield5AdvancedSettingsPanel;
        private Battlefield4AdvancedSettingsPanel _battlefield4AdvancedSettingsPanel;
        private Battlefield2042AdvancedSettingsPanel _battlefield2042AdvancedSettingsPanel;
        private PubgAdvancedSettingsPanel _pubgAdvancedSettingsPanel;
        private DeltaForceAdvancedSettingsPanel _deltaForceAdvancedSettingsPanel;

        private void ApplyGameStyleUi()
        {
            GameStyleMode mode = GameStyleService.Current;
            bool valorant = mode == GameStyleMode.Valorant;
            bool battlefield = mode == GameStyleMode.Battlefield1 || mode == GameStyleMode.Battlefield5;
            bool battlefield1 = mode == GameStyleMode.Battlefield1;
            bool battlefield5 = mode == GameStyleMode.Battlefield5;
            bool battlefield2042 = mode == GameStyleMode.Battlefield2042;
            bool fixedPreset = GameStyleService.IsModPresetGameKey(GameStyleService.ToStorageValue(mode));
            GameThemePalette theme = GameThemePalette.Current;
            MountGameAdvancedSettingsPanel();
            VoicePackCollectionsCard.Visibility = fixedPreset ? Visibility.Collapsed : Visibility.Visible;
            IconPackCollectionsCard.Visibility = fixedPreset ? Visibility.Collapsed : Visibility.Visible;
            VoiceCollectionsCard.Visibility = (valorant || fixedPreset) ? Visibility.Collapsed : Visibility.Visible;
            IconCollectionsCard.Visibility = (valorant || fixedPreset) ? Visibility.Collapsed : Visibility.Visible;

            SettingsRootGrid.Background = CreateSettingsBackground(mode);
            HeroSlash.Fill = CreateHeroSlashBrush(mode);
            HeroSlashLight.Fill = new SolidColorBrush(battlefield5 ? Color.FromArgb(255, 119, 243, 255) : battlefield2042 ? Color.FromArgb(255, 109, 255, 255) : battlefield1 ? Color.FromArgb(255, 255, 218, 166) : valorant ? Color.FromArgb(255, 255, 170, 178) : Color.FromArgb(255, 255, 240, 213));
            FrameStripeOne.Stroke = new SolidColorBrush(battlefield5 ? Color.FromArgb(255, 58, 137, 166) : battlefield2042 ? Color.FromArgb(255, 60, 128, 146) : battlefield1 ? Color.FromArgb(255, 88, 110, 126) : valorant ? Color.FromArgb(255, 59, 78, 102) : Color.FromArgb(255, 196, 196, 196));
            FrameStripeTwo.Stroke = FrameStripeOne.Stroke;
            AccentLineOne.Fill = new SolidColorBrush(fixedPreset ? theme.Accent : valorant ? theme.Secondary : Color.FromArgb(255, 207, 107, 0));
            AccentLineTwo.Fill = AccentLineOne.Fill;
            AccentLineThree.Fill = AccentLineOne.Fill;

            SetText(TitleText, theme.Text);
            SetText(InstructionText, theme.MutedText);
            SetText(ShortcutText, theme.SubtleText);
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

        private ValorantAdvancedSettingsPanel EnsureValorantAdvancedSettingsPanel()
        {
            return _valorantAdvancedSettingsPanel ?? (_valorantAdvancedSettingsPanel = new ValorantAdvancedSettingsPanel());
        }

        private Battlefield1AdvancedSettingsPanel EnsureBattlefield1AdvancedSettingsPanel()
        {
            return _battlefield1AdvancedSettingsPanel ?? (_battlefield1AdvancedSettingsPanel = new Battlefield1AdvancedSettingsPanel());
        }

        private Battlefield5AdvancedSettingsPanel EnsureBattlefield5AdvancedSettingsPanel()
        {
            return _battlefield5AdvancedSettingsPanel ?? (_battlefield5AdvancedSettingsPanel = new Battlefield5AdvancedSettingsPanel());
        }

        private Battlefield4AdvancedSettingsPanel EnsureBattlefield4AdvancedSettingsPanel()
        {
            return _battlefield4AdvancedSettingsPanel ?? (_battlefield4AdvancedSettingsPanel = new Battlefield4AdvancedSettingsPanel());
        }

        private Battlefield2042AdvancedSettingsPanel EnsureBattlefield2042AdvancedSettingsPanel()
        {
            return _battlefield2042AdvancedSettingsPanel ?? (_battlefield2042AdvancedSettingsPanel = new Battlefield2042AdvancedSettingsPanel());
        }

        private PubgAdvancedSettingsPanel EnsurePubgAdvancedSettingsPanel()
        {
            return _pubgAdvancedSettingsPanel ?? (_pubgAdvancedSettingsPanel = new PubgAdvancedSettingsPanel());
        }

        private DeltaForceAdvancedSettingsPanel EnsureDeltaForceAdvancedSettingsPanel()
        {
            return _deltaForceAdvancedSettingsPanel ?? (_deltaForceAdvancedSettingsPanel = new DeltaForceAdvancedSettingsPanel());
        }

        private void ApplyGameAdvancedSettingsPanelTheme()
        {
            GameThemePalette theme = GameThemePalette.Current;
            if (_crossfireAdvancedSettingsPanel != null)
            {
                _crossfireAdvancedSettingsPanel.ApplyTheme(theme);
            }

            if (_valorantAdvancedSettingsPanel != null)
            {
                _valorantAdvancedSettingsPanel.ApplyTheme(theme);
            }

            if (_battlefield1AdvancedSettingsPanel != null)
            {
                _battlefield1AdvancedSettingsPanel.ApplyTheme(theme);
            }

            if (_battlefield5AdvancedSettingsPanel != null)
            {
                _battlefield5AdvancedSettingsPanel.ApplyTheme(theme);
            }

            if (_battlefield4AdvancedSettingsPanel != null)
            {
                _battlefield4AdvancedSettingsPanel.ApplyTheme(theme);
            }

            if (_battlefield2042AdvancedSettingsPanel != null)
            {
                _battlefield2042AdvancedSettingsPanel.ApplyTheme(theme);
            }

            if (_pubgAdvancedSettingsPanel != null)
            {
                _pubgAdvancedSettingsPanel.ApplyTheme(theme);
            }

            if (_deltaForceAdvancedSettingsPanel != null)
            {
                _deltaForceAdvancedSettingsPanel.ApplyTheme(theme);
            }
        }

        private void ApplyGameAdvancedSettingsPanelLanguage()
        {
            bool isChinese = LocalizationManager.Current == UiLanguage.SimplifiedChinese;
            if (_crossfireAdvancedSettingsPanel != null)
            {
                _crossfireAdvancedSettingsPanel.ApplyLanguage(isChinese);
            }

            if (_valorantAdvancedSettingsPanel != null)
            {
                _valorantAdvancedSettingsPanel.ApplyLanguage(isChinese);
            }

            if (_battlefield1AdvancedSettingsPanel != null)
            {
                _battlefield1AdvancedSettingsPanel.ApplyLanguage(isChinese);
            }

            if (_battlefield5AdvancedSettingsPanel != null)
            {
                _battlefield5AdvancedSettingsPanel.ApplyLanguage(isChinese);
            }

            if (_battlefield4AdvancedSettingsPanel != null)
            {
                _battlefield4AdvancedSettingsPanel.ApplyLanguage(isChinese);
            }

            if (_battlefield2042AdvancedSettingsPanel != null)
            {
                _battlefield2042AdvancedSettingsPanel.ApplyLanguage(isChinese);
            }

            if (_pubgAdvancedSettingsPanel != null)
            {
                _pubgAdvancedSettingsPanel.ApplyLanguage(isChinese);
            }

            if (_deltaForceAdvancedSettingsPanel != null)
            {
                _deltaForceAdvancedSettingsPanel.ApplyLanguage(isChinese);
            }
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

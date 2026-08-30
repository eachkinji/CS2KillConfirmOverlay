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

        private async void OnGameAssistAudioToggled(object sender, RoutedEventArgs e)
        {
            GameStyleMode style;
            bool enabled;
            if (sender is OverwatchAdvancedEffectsPanel overwatchPanel)
            {
                style = GameStyleMode.Overwatch;
                enabled = overwatchPanel.GetAssistAudioEnabled(false);
            }
            else if (sender is ModernWarfare2019AdvancedEffectsPanel modernWarfarePanel)
            {
                style = GameStyleMode.ModernWarfare2019;
                enabled = modernWarfarePanel.GetAssistAudioEnabled(false);
            }
            else
            {
                return;
            }

            AssistAudioSettingsStore.Save(style, enabled);
            await TrySyncSharedStreakSettingsAsync(
                style,
                SharedStreakSettingsStore.Load(style));
        }

        private void OnValorantPackSyncToggled(object sender, RoutedEventArgs e)
        {
            if (sender is ValorantAdvancedEffectsPanel panel)
            {
                ValorantPackSyncSettingsStore.Save(panel.GetPackSyncEnabled(true));
            }
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
            else if (sender is ApexAdvancedEffectsPanel pApex) mode = pApex.GetSelectedStreakMode(mode);
            else if (sender is ModernWarfare2019AdvancedEffectsPanel pMw) mode = pMw.GetSelectedStreakMode(mode);
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
                        AssistAudioSettingsStore.IsSupported(style)
                        && AssistAudioSettingsStore.Load(style)),
                    ["assist_audio_setting_active"] = JsonValue.CreateBooleanValue(
                        AssistAudioSettingsStore.IsSupported(style))
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
            _customModulePanel?.ApplyTheme(theme);
            if (_crossfireAdvancedEffectsPanel != null) _crossfireAdvancedEffectsPanel.ApplyTheme(theme);
            if (_csolAdvancedEffectsPanel != null) _csolAdvancedEffectsPanel.ApplyTheme(theme);
            if (_valorantAdvancedEffectsPanel != null) _valorantAdvancedEffectsPanel.ApplyTheme(theme);
            if (_overwatchAdvancedEffectsPanel != null) _overwatchAdvancedEffectsPanel.ApplyTheme(theme);
            if (_modernWarfare2019AdvancedEffectsPanel != null) _modernWarfare2019AdvancedEffectsPanel.ApplyTheme(theme);
            if (_apexAdvancedEffectsPanel != null) _apexAdvancedEffectsPanel.ApplyTheme(theme);
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
            _customModulePanel?.ApplyLanguage(isChinese);
            if (_crossfireAdvancedEffectsPanel != null) _crossfireAdvancedEffectsPanel.ApplyLanguage(isChinese);
            if (_csolAdvancedEffectsPanel != null) _csolAdvancedEffectsPanel.ApplyLanguage(isChinese);
            if (_valorantAdvancedEffectsPanel != null) _valorantAdvancedEffectsPanel.ApplyLanguage(isChinese);
            if (_overwatchAdvancedEffectsPanel != null) _overwatchAdvancedEffectsPanel.ApplyLanguage(isChinese);
            if (_modernWarfare2019AdvancedEffectsPanel != null) _modernWarfare2019AdvancedEffectsPanel.ApplyLanguage(isChinese);
            if (_apexAdvancedEffectsPanel != null) _apexAdvancedEffectsPanel.ApplyLanguage(isChinese);
            if (_battlefield1AdvancedEffectsPanel != null) _battlefield1AdvancedEffectsPanel.ApplyLanguage(isChinese);
            if (_battlefield5AdvancedEffectsPanel != null) _battlefield5AdvancedEffectsPanel.ApplyLanguage(isChinese);
            if (_battlefield4AdvancedEffectsPanel != null) _battlefield4AdvancedEffectsPanel.ApplyLanguage(isChinese);
            if (_battlefield2042AdvancedEffectsPanel != null) _battlefield2042AdvancedEffectsPanel.ApplyLanguage(isChinese);
            if (_pubgAdvancedEffectsPanel != null) _pubgAdvancedEffectsPanel.ApplyLanguage(isChinese);
            if (_deltaForceAdvancedEffectsPanel != null) _deltaForceAdvancedEffectsPanel.ApplyLanguage(isChinese);
            if (_doubaoAdvancedEffectsPanel != null) _doubaoAdvancedEffectsPanel.ApplyLanguage(isChinese);
            if (_dagoujiaoAdvancedEffectsPanel != null) _dagoujiaoAdvancedEffectsPanel.ApplyLanguage(isChinese);
        }

        private static Brush CreateSettingsBackground(GameStyleMode mode, bool isHomePage)
        {
            var brush = new LinearGradientBrush
            {
                StartPoint = new Windows.Foundation.Point(0, 0),
                EndPoint = new Windows.Foundation.Point(1, 1)
            };

            if (isHomePage)
            {
                brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 248, 250, 252), Offset = 0.0 });
                brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 238, 242, 246), Offset = 1.0 });
                return brush;
            }

            switch (mode)
            {
                case GameStyleMode.Battlefield5:
                    brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 5, 21, 38), Offset = 0 });
                    brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 16, 62, 92), Offset = 1 });
                    break;
                case GameStyleMode.Valorant:
                    brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 10, 14, 22), Offset = 0 });
                    brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 22, 28, 42), Offset = 1 });
                    break;
                case GameStyleMode.Battlefield1:
                    brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 12, 21, 28), Offset = 0 });
                    brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 44, 59, 69), Offset = 1 });
                    break;
                case GameStyleMode.Battlefield4:
                    brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 9, 20, 31), Offset = 0 });
                    brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 24, 55, 80), Offset = 1 });
                    break;
                case GameStyleMode.Battlefield2042:
                    brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 4, 16, 24), Offset = 0 });
                    brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 20, 68, 82), Offset = 0.58 });
                    brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 61, 16, 35), Offset = 1 });
                    break;
                case GameStyleMode.Pubg:
                    brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 26, 24, 17), Offset = 0 });
                    brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 74, 61, 30), Offset = 1 });
                    break;
                case GameStyleMode.Apex:
                    brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 28, 20, 20), Offset = 0 });
                    brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 76, 31, 30), Offset = 1 });
                    break;
                case GameStyleMode.Overwatch:
                    brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 38, 48, 58), Offset = 0 });
                    brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 79, 87, 96), Offset = 1 });
                    break;
                case GameStyleMode.ModernWarfare2019:
                    brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 16, 30, 35), Offset = 0 });
                    brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 39, 74, 84), Offset = 1 });
                    break;
                case GameStyleMode.DeltaForce:
                    brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 9, 21, 19), Offset = 0 });
                    brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 37, 69, 47), Offset = 1 });
                    break;
                case GameStyleMode.Doubao:
                    brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 10, 15, 30), Offset = 0 });
                    brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 25, 35, 65), Offset = 1 });
                    break;
                case GameStyleMode.Dagoujiao:
                    brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 20, 10, 30), Offset = 0 });
                    brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 45, 20, 65), Offset = 1 });
                    break;
                case GameStyleMode.Csol:
                    brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 25, 10, 12), Offset = 0 });
                    brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 50, 18, 22), Offset = 1 });
                    break;
                case GameStyleMode.Crossfire:
                default:
                    brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 242, 243, 242), Offset = 0 });
                    brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 216, 217, 216), Offset = 1 });
                    break;
            }

            return brush;
        }

        private static Brush CreateHeroSlashBrush(GameStyleMode mode)
        {
            var brush = new LinearGradientBrush
            {
                StartPoint = new Windows.Foundation.Point(0, 0),
                EndPoint = new Windows.Foundation.Point(1, 1)
            };

            switch (mode)
            {
                case GameStyleMode.Battlefield5:
                    brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 0, 211, 255), Offset = 0 });
                    brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 255, 90, 56), Offset = 0.58 });
                    brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 174, 14, 57), Offset = 1 });
                    break;
                case GameStyleMode.Valorant:
                    brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 255, 70, 85), Offset = 0 });
                    brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 131, 38, 55), Offset = 0.58 });
                    brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 63, 25, 38), Offset = 1 });
                    break;
                case GameStyleMode.Battlefield1:
                    brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 255, 196, 100), Offset = 0 });
                    brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 242, 126, 38), Offset = 0.55 });
                    brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 62, 86, 102), Offset = 1 });
                    break;
                case GameStyleMode.Battlefield4:
                    brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 71, 183, 255), Offset = 0 });
                    brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 255, 138, 45), Offset = 0.58 });
                    brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 33, 70, 104), Offset = 1 });
                    break;
                case GameStyleMode.Battlefield2042:
                    brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 34, 221, 221), Offset = 0 });
                    brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 255, 79, 82), Offset = 0.58 });
                    brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 72, 23, 48), Offset = 1 });
                    break;
                case GameStyleMode.Pubg:
                    brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 245, 182, 66), Offset = 0 });
                    brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 130, 104, 40), Offset = 0.58 });
                    brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 36, 55, 36), Offset = 1 });
                    break;
                case GameStyleMode.Apex:
                    brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 235, 57, 52), Offset = 0 });
                    brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 139, 37, 35), Offset = 0.58 });
                    brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 45, 28, 28), Offset = 1 });
                    break;
                case GameStyleMode.Overwatch:
                    brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 246, 101, 22), Offset = 0 });
                    brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 161, 72, 28), Offset = 0.58 });
                    brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 54, 67, 79), Offset = 1 });
                    break;
                case GameStyleMode.ModernWarfare2019:
                    brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 100, 210, 231), Offset = 0 });
                    brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 48, 132, 158), Offset = 0.58 });
                    brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 30, 48, 55), Offset = 1 });
                    break;
                case GameStyleMode.DeltaForce:
                    brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 102, 214, 134), Offset = 0 });
                    brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 255, 135, 40), Offset = 0.58 });
                    brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 24, 58, 43), Offset = 1 });
                    break;
                case GameStyleMode.Doubao:
                    brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 59, 130, 246), Offset = 0 });
                    brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 99, 102, 241), Offset = 0.58 });
                    brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 30, 27, 75), Offset = 1 });
                    break;
                case GameStyleMode.Dagoujiao:
                    brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 168, 85, 247), Offset = 0 });
                    brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 236, 72, 153), Offset = 0.58 });
                    brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 88, 28, 135), Offset = 1 });
                    break;
                case GameStyleMode.Csol:
                    brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 220, 38, 38), Offset = 0 });
                    brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 153, 27, 27), Offset = 0.58 });
                    brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 69, 10, 10), Offset = 1 });
                    break;
                case GameStyleMode.Crossfire:
                default:
                    brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 242, 154, 23), Offset = 0 });
                    brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 234, 127, 5), Offset = 0.58 });
                    brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 219, 105, 0), Offset = 1 });
                    break;
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
                string role = button.Tag as string;
                if (string.Equals(role, "PackDelete", StringComparison.Ordinal))
                {
                    button.Background = new SolidColorBrush(Color.FromArgb(255, 254, 242, 242));
                    button.Foreground = new SolidColorBrush(Color.FromArgb(255, 196, 43, 28));
                    button.BorderBrush = new SolidColorBrush(Color.FromArgb(255, 252, 209, 209));
                }
                else if (string.Equals(role, "PackEdit", StringComparison.Ordinal)
                    || string.Equals(role, "PackExport", StringComparison.Ordinal))
                {
                    button.Background = new SolidColorBrush(theme.Field);
                    button.Foreground = new SolidColorBrush(theme.Accent);
                    button.BorderBrush = new SolidColorBrush(theme.AccentSoft);
                }
                else
                {
                    ApplyButtonTheme(button, theme, false);
                }
            }

            int count = VisualTreeHelper.GetChildrenCount(element);
            for (int i = 0; i < count; i++)
            {
                ApplyThemeToElement(VisualTreeHelper.GetChild(element, i), theme);
            }
        }
    }
}

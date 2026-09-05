using KillConfirmGameBar.Services;
using Windows.System;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace KillConfirmGameBar.Controls.Settings
{
    public sealed partial class ValorantAdvancedSettingsPanel : UserControl
    {
        private bool _suppressStreakEvents;

        public ValorantAdvancedSettingsPanel()
        {
            InitializeComponent();
            _suppressStreakEvents = true;
            SharedStreakSettingsPanelSupport.Load(GameStyleMode.Valorant, StreakModeSelector);
            _suppressStreakEvents = false;
        }

        internal void ApplyTheme(GameThemePalette theme)
        {
            SettingsPanelSupport.ApplyPanel(Card, TitleText, BodyText, theme);
            SettingsPanelSupport.ApplySettingRow(StreakModeLabel, StreakModeSelector, theme);
            OpenExternalAssetsButton.Background = new SolidColorBrush(theme.SubtleField);
            OpenExternalAssetsButton.BorderBrush = new SolidColorBrush(theme.SoftBorder);
            OpenExternalAssetsButton.Foreground = new SolidColorBrush(theme.Text);
        }

        public void ApplyLanguage(bool isChinese)
        {
            TitleText.Text = isChinese ? "VAL 高级设置" : "VAL advanced settings";
            BodyText.Text = string.Empty;
            BodyText.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
            SharedStreakSettingsStore.ApplyLanguage(
                StreakModeLabel,
                StreakLifeItem,
                StreakTimed5Item,
                StreakTimed10Item,
                StreakTimed15Item,
                isChinese);
            ExternalAssetsLabel.Text = isChinese ? "外部原生素材" : "External native assets";
            ExternalAssetsHint.Text = isChinese
                ? "视觉素材和音频会优先从外部目录加载；增删素材包后重启应用刷新列表。"
                : "Visuals and audio load from the external folder first. Restart the app after adding or removing packs.";
            OpenExternalAssetsButtonText.Text = isChinese ? "打开目录" : "Open folder";
        }

        private async void OnStreakModeSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_suppressStreakEvents)
            {
                await SharedStreakSettingsPanelSupport.SaveAndSyncAsync(
                    GameStyleMode.Valorant,
                    StreakModeSelector);
            }
        }

        private async void OnOpenExternalAssetsClicked(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            try
            {
                var folder = await ValorantExternalAssetService.GetExternalAssetsFolderAsync();
                var launchOperation = Launcher.LaunchFolderAsync(folder);
            }
            catch
            {
            }
        }
    }
}

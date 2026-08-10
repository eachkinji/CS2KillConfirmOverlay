using KillConfirmGameBar.Services;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace KillConfirmGameBar.Controls.GameStyles
{
    public sealed partial class ValorantAdvancedEffectsPanel : UserControl
    {
        public ValorantAdvancedEffectsPanel()
        {
            InitializeComponent();
        }

        public event SelectionChangedEventHandler StreakModeSelectionChanged;
        public event RoutedEventHandler AssistAudioToggled;

        public ComboBox StreakModeSelectorControl => StreakEditor.SelectorControl;

        public void SetStylePanel(ValorantStylePanel panel)
        {
            StylePanelHost.Content = panel;
        }

        internal void ApplyTheme(GameThemePalette theme)
        {
            AdvancedEffectsPanelSupport.ApplyHeader(TitleText, HintText, theme);
            StreakEditor.ApplyTheme(theme);
            AdvancedEffectsPanelSupport.ApplyToggleRow(AssistAudioLabel, AssistAudioToggle, theme);
        }

        public void ApplyLanguage(bool isChinese)
        {
            TitleText.Text = isChinese ? "VAL \u9ad8\u7ea7\u7279\u6548" : "VAL Effects";
            HintText.Text = string.Empty;
            HintText.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
            StreakEditor.ApplyLanguage(isChinese);
            AssistAudioLabel.Text = isChinese ? "\u52a9\u653b\u97f3\u6548" : "Assist audio";
            AssistAudioToggle.OnContent = isChinese ? "\u6709\u58f0\u97f3\uff08common\uff09" : "Sound (common)";
            AssistAudioToggle.OffContent = isChinese ? "\u65e0\u58f0\u97f3\uff08\u9ed8\u8ba4\uff09" : "Muted (default)";
        }

        public string GetSelectedStreakMode(string fallback)
        {
            return StreakEditor.GetValue(fallback);
        }

        public void SelectStreakMode(string value)
        {
            StreakEditor.SelectValue(value);
        }

        public bool GetAssistAudioEnabled(bool fallback)
        {
            return AssistAudioToggle?.IsOn ?? fallback;
        }

        public void SelectAssistAudio(bool enabled)
        {
            AssistAudioToggle.IsOn = enabled;
        }

        private void OnStreakModeSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            StreakModeSelectionChanged?.Invoke(this, e);
        }

        private void OnAssistAudioToggled(object sender, RoutedEventArgs e)
        {
            AssistAudioToggled?.Invoke(this, e);
        }
    }
}

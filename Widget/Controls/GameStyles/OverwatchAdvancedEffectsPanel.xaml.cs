using KillConfirmGameBar.Services;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace KillConfirmGameBar.Controls.GameStyles
{
    public sealed partial class OverwatchAdvancedEffectsPanel : UserControl
    {
        private bool _suppressAssistAudioChanges;
        private bool _suppressVisualEffectChanges;

        public OverwatchAdvancedEffectsPanel()
        {
            InitializeComponent();
            RefreshVisualEffectSettings();
            SelectAssistAudio(AssistAudioSettingsStore.Load(GameStyleMode.Overwatch));
        }

        public event RoutedEventHandler AssistAudioToggled;

        internal void ApplyTheme(GameThemePalette theme)
        {
            TitleText.Foreground = Brush(theme.Text);
            VisualEffectsCard.Background = Brush(theme.Card);
            VisualEffectsCard.BorderBrush = Brush(theme.SoftBorder);
            VisualEffectsTitle.Foreground = Brush(theme.Text);
            CrosshairEffectLabel.Foreground = Brush(theme.Text);
            LowerEffectLabel.Foreground = Brush(theme.Text);
            CrosshairEffectToggle.Foreground = Brush(theme.Text);
            LowerEffectToggle.Foreground = Brush(theme.Text);
            CombatAudioCard.Background = Brush(theme.Card);
            CombatAudioCard.BorderBrush = Brush(theme.SoftBorder);
            AssistAudioLabel.Foreground = Brush(theme.Text);
            AssistAudioToggle.Foreground = Brush(theme.Text);
        }

        public void ApplyLanguage(bool isChinese)
        {
            TitleText.Text = isChinese ? "守望先锋击杀提示" : "Overwatch Kill Feedback";
            VisualEffectsTitle.Text = isChinese ? "显示哪些击杀提示" : "Visible kill feedback";
            CrosshairEffectLabel.Text = isChinese ? "中央准心提示" : "Center crosshair feedback";
            LowerEffectLabel.Text = isChinese ? "下方击杀卡片" : "Lower kill card";
            AssistAudioLabel.Text = isChinese ? "助攻时播放语音" : "Play voice on assist";
            ApplyToggleLanguage(CrosshairEffectToggle, isChinese);
            ApplyToggleLanguage(LowerEffectToggle, isChinese);
            AssistAudioToggle.OnContent = isChinese ? "开" : "On";
            AssistAudioToggle.OffContent = isChinese ? "关" : "Off";
        }

        public void RefreshVisualEffectSettings()
        {
            KillFeedbackVisibilitySettingsValues settings =
                KillFeedbackVisibilitySettingsStore.Load(GameStyleMode.Overwatch);
            _suppressVisualEffectChanges = true;
            try
            {
                CrosshairEffectToggle.IsOn = settings.CrosshairEnabled;
                LowerEffectToggle.IsOn = settings.LowerEnabled;
            }
            finally
            {
                _suppressVisualEffectChanges = false;
            }
        }

        public bool GetAssistAudioEnabled(bool fallback) =>
            AssistAudioToggle == null ? fallback : AssistAudioToggle.IsOn;

        public void SelectAssistAudio(bool enabled)
        {
            _suppressAssistAudioChanges = true;
            try
            {
                AssistAudioToggle.IsOn = enabled;
            }
            finally
            {
                _suppressAssistAudioChanges = false;
            }
        }

        private void OnAssistAudioToggled(object sender, RoutedEventArgs e)
        {
            if (!_suppressAssistAudioChanges)
            {
                AssistAudioToggled?.Invoke(this, e);
            }
        }

        private void OnVisualEffectToggled(object sender, RoutedEventArgs e)
        {
            if (_suppressVisualEffectChanges)
            {
                return;
            }

            KillFeedbackVisibilitySettingsStore.Save(
                GameStyleMode.Overwatch,
                new KillFeedbackVisibilitySettingsValues
                {
                    CrosshairEnabled = CrosshairEffectToggle.IsOn,
                    LowerEnabled = LowerEffectToggle.IsOn
                });
        }

        private static void ApplyToggleLanguage(ToggleSwitch toggle, bool isChinese)
        {
            toggle.OnContent = isChinese ? "开" : "On";
            toggle.OffContent = isChinese ? "关" : "Off";
        }

        private static SolidColorBrush Brush(Color color)
        {
            return new SolidColorBrush(color);
        }
    }
}

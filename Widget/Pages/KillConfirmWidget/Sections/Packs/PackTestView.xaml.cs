using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace KillConfirmGameBar
{
    public sealed partial class PackTestView : UserControl
    {
        public PackTestView()
        {
            InitializeComponent();
            KillConfirmGameBar.Services.PackSelectorItem.Configure(CharacterVoicePackSelector, true);
            KillConfirmGameBar.Services.PackSelectorItem.Configure(VoicePackSelector, true);
            KillConfirmGameBar.Services.PackSelectorItem.Configure(IconPackSelector, true);
        }

        public event SelectionChangedEventHandler CharacterVoicePackSelectionChanged;
        public event RoutedEventHandler AudioChannelSettingsChanged;
        private void OnCharacterVoicePackSelectionChanged(object sender, SelectionChangedEventArgs e)
            => CharacterVoicePackSelectionChanged?.Invoke(sender, e);
        private void OnAudioChannelVolumeSelectionChanged(object sender, SelectionChangedEventArgs e)
            => AudioChannelSettingsChanged?.Invoke(sender, new RoutedEventArgs());
        public void ApplyAudioLanguage(bool zh)
        {
            SoundEffectVolumeLabel.Text = zh ? "音效音量" : "Sound effects";
            CharacterVoiceVolumeLabel.Text = zh ? "语音音量" : "Character voice";
            AudioSettingsHeaderText.Text = zh ? "音频设置" : "Audio settings";
            MasterVolumeLabel.Text = zh ? "总音量" : "Master volume";
            PackTestHeaderText.Text = zh ? "图标包、音效包与语音包" : "Icons, sound effects & voices";
        }

        public UIElement DetachAudioSettings()
        {
            PackTestSection.Children.Remove(AudioSettingsCard);
            return AudioSettingsCard;
        }

        public event SelectionChangedEventHandler VoicePackSelectionChanged;
        public event SelectionChangedEventHandler IconPackSelectionChanged;
        public event RoutedEventHandler AdvancedEffectsRequested;
        public event SelectionChangedEventHandler AudioVolumeSelectionChanged;
        public event RoutedEventHandler TestEventRequested;
        public event RoutedEventHandler RepeatTestRequested;
        public event RoutedEventHandler ReloadAudioRequested;

        private void OnVoicePackSelectionChanged(object sender, SelectionChangedEventArgs e)
            => VoicePackSelectionChanged?.Invoke(sender, e);

        private void OnIconPackSelectionChanged(object sender, SelectionChangedEventArgs e)
            => IconPackSelectionChanged?.Invoke(sender, e);

        private void OnAdvancedEffectsButtonClick(object sender, RoutedEventArgs e)
            => AdvancedEffectsRequested?.Invoke(sender, e);

        private void OnAudioVolumeSelectionChanged(object sender, SelectionChangedEventArgs e)
            => AudioVolumeSelectionChanged?.Invoke(sender, e);

        private void OnTestEventClick(object sender, RoutedEventArgs e)
            => TestEventRequested?.Invoke(sender, e);

        private void OnRepeatTestClick(object sender, RoutedEventArgs e)
            => RepeatTestRequested?.Invoke(sender, e);

        private void OnDanmakuTestClick(object sender, RoutedEventArgs e)
        {
            KillConfirmGameBar.Danmaku.DanmakuSettingsStore.RequestTest();
        }

        private void OnReloadAudioClick(object sender, RoutedEventArgs e)
            => ReloadAudioRequested?.Invoke(sender, e);
    }
}

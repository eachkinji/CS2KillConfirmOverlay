using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace KillConfirmGameBar.Features.CompatibilityDisplay
{
    public sealed partial class PackTestView : UserControl
    {
        public PackTestView()
        {
            InitializeComponent();
            KillConfirmGameBar.Services.PackSelectorItem.Configure(CharacterVoicePackSelector, false);
            KillConfirmGameBar.Services.PackSelectorItem.Configure(VoicePackSelector, false);
            KillConfirmGameBar.Services.PackSelectorItem.Configure(IconPackSelector, false);
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
            VoicePackLabel.Text = zh ? "音效包" : "Sound effect pack";
            CharacterVoicePackLabel.Text = zh ? "语音包" : "Voice pack";
            VolumeLabel.Text = zh ? "总音量" : "Master volume";
        }

        public event SelectionChangedEventHandler VoicePackSelectionChanged;
        public event SelectionChangedEventHandler IconPackSelectionChanged;
        public event RoutedEventHandler AdvancedEffectsRequested;
        public event SelectionChangedEventHandler AudioVolumeSelectionChanged;
        public event RoutedEventHandler TestEventRequested;
        public event RoutedEventHandler RepeatTestRequested;
        public event RoutedEventHandler ReloadAudioRequested;
        public event RoutedEventHandler GameSettingsRequested;

        private void OnVoicePackSelectionChanged(object sender, SelectionChangedEventArgs e)
            => VoicePackSelectionChanged?.Invoke(sender, e);

        private void OnIconPackSelectionChanged(object sender, SelectionChangedEventArgs e)
            => IconPackSelectionChanged?.Invoke(sender, e);

        private void OnGameSettingsClick(object sender, RoutedEventArgs e)
            => GameSettingsRequested?.Invoke(sender, e);

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

using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace KillConfirmGameBar
{
    public sealed partial class PackTestView : UserControl
    {
        public PackTestView()
        {
            InitializeComponent();
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

        private void OnReloadAudioClick(object sender, RoutedEventArgs e)
            => ReloadAudioRequested?.Invoke(sender, e);
    }
}

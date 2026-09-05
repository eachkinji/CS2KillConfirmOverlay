using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace KillConfirmGameBar
{
    public sealed partial class VisualSettingsView : UserControl
    {
        public VisualSettingsView()
        {
            InitializeComponent();
        }

        public event RoutedEventHandler ResizeRequested;
        public event RoutedEventHandler FitScreenRequested;
        public event RoutedEventHandler CenterRequested;
        public event RoutedEventHandler WindowTopRequested;
        public event RoutedEventHandler WindowBottomRequested;
        public event RoutedEventHandler ControlPanelCenterRequested;
        public event RoutedEventHandler ResetVisualRequested;
        public event SelectionChangedEventHandler BrightnessSelectionChanged;
        public event SelectionChangedEventHandler ContrastSelectionChanged;
        public event SelectionChangedEventHandler PlaybackFpsSelectionChanged;

        private void OnResizeClick(object sender, RoutedEventArgs e)
            => ResizeRequested?.Invoke(sender, e);

        private void OnFitScreenClick(object sender, RoutedEventArgs e)
            => FitScreenRequested?.Invoke(sender, e);

        private void OnCenterClick(object sender, RoutedEventArgs e)
            => CenterRequested?.Invoke(sender, e);

        private void OnWindowTopClick(object sender, RoutedEventArgs e)
            => WindowTopRequested?.Invoke(sender, e);

        private void OnWindowBottomClick(object sender, RoutedEventArgs e)
            => WindowBottomRequested?.Invoke(sender, e);

        private void OnControlPanelCenterClick(object sender, RoutedEventArgs e)
            => ControlPanelCenterRequested?.Invoke(sender, e);

        private void OnResetVisualAdjustmentsClick(object sender, RoutedEventArgs e)
            => ResetVisualRequested?.Invoke(sender, e);

        private void OnBrightnessSelectionChanged(object sender, SelectionChangedEventArgs e)
            => BrightnessSelectionChanged?.Invoke(sender, e);

        private void OnContrastSelectionChanged(object sender, SelectionChangedEventArgs e)
            => ContrastSelectionChanged?.Invoke(sender, e);

        private void OnPlaybackFpsSelectionChanged(object sender, SelectionChangedEventArgs e)
            => PlaybackFpsSelectionChanged?.Invoke(sender, e);
    }
}

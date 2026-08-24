using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace KillConfirmGameBar
{
    public sealed partial class HeaderStatusView : UserControl
    {
        public HeaderStatusView()
        {
            InitializeComponent();
        }

        public event RoutedEventHandler LanguageToggleRequested;
        public event RoutedEventHandler OpenGuideRequested;
        public event RoutedEventHandler UpdateRequested;
        public event SelectionChangedEventHandler GameStyleSelectionChanged;

        private void OnLanguageToggleClick(object sender, RoutedEventArgs e)
            => LanguageToggleRequested?.Invoke(sender, e);

        private void OnOpenGuideClick(object sender, RoutedEventArgs e)
            => OpenGuideRequested?.Invoke(sender, e);

        private void OnUpdateClick(object sender, RoutedEventArgs e)
            => UpdateRequested?.Invoke(sender, e);

        private void OnGameStyleSelectionChanged(object sender, SelectionChangedEventArgs e)
            => GameStyleSelectionChanged?.Invoke(sender, e);
    }
}

using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace KillConfirmGameBar
{
    public sealed partial class StatusDetailsView : UserControl
    {
        public StatusDetailsView()
        {
            InitializeComponent();
        }

        public event RoutedEventHandler RetryServiceRequested;
        public event RoutedEventHandler CopyDiagnosticRequested;
        public event RoutedEventHandler FreePortRequested;
        public event RoutedEventHandler OpenLogsRequested;
        public event RoutedEventHandler SelectCsFolderRequested;
        public event RoutedEventHandler InstallCfgRequested;

        private void OnRetryServiceClick(object sender, RoutedEventArgs e)
            => RetryServiceRequested?.Invoke(sender, e);

        private void OnCopyServiceDiagnosticClick(object sender, RoutedEventArgs e)
            => CopyDiagnosticRequested?.Invoke(sender, e);

        private void OnFreePortClick(object sender, RoutedEventArgs e)
            => FreePortRequested?.Invoke(sender, e);

        private void OnOpenLogsClick(object sender, RoutedEventArgs e)
            => OpenLogsRequested?.Invoke(sender, e);

        private void OnSelectCsFolderClick(object sender, RoutedEventArgs e)
            => SelectCsFolderRequested?.Invoke(sender, e);

        private void OnInstallCfgClick(object sender, RoutedEventArgs e)
            => InstallCfgRequested?.Invoke(sender, e);
    }
}

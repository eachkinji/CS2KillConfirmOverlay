using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace KillConfirmGameBar.Controls
{
    public sealed partial class UpdateOverlayView : UserControl
    {
        public UpdateOverlayView()
        {
            InitializeComponent();
        }

        public event RoutedEventHandler CloseClicked;
        public event RoutedEventHandler OpenAuthorGitHubClicked;
        public event RoutedEventHandler OpenBilibiliClicked;
        public event RoutedEventHandler OpenGitHubClicked;
        public event RoutedEventHandler ToggleReleaseNotesClicked;
        public event RoutedEventHandler OpenQuarkClicked;
        public event RoutedEventHandler CopyQuarkClicked;
        public event RoutedEventHandler DownloadClicked;
        public event RoutedEventHandler InstallClicked;
        public event RoutedEventHandler OpenFolderClicked;

        private void OnCloseUpdateOverlayClick(object sender, RoutedEventArgs e) => CloseClicked?.Invoke(sender, e);

        private void OnOpenAuthorGitHubClick(object sender, RoutedEventArgs e) => OpenAuthorGitHubClicked?.Invoke(sender, e);

        private void OnOpenBilibiliClick(object sender, RoutedEventArgs e) => OpenBilibiliClicked?.Invoke(sender, e);

        private void OnOpenGitHubClick(object sender, RoutedEventArgs e) => OpenGitHubClicked?.Invoke(sender, e);

        private void OnToggleReleaseNotesClick(object sender, RoutedEventArgs e) => ToggleReleaseNotesClicked?.Invoke(sender, e);

        private void OnOpenQuarkUpdateClick(object sender, RoutedEventArgs e) => OpenQuarkClicked?.Invoke(sender, e);

        private void OnCopyQuarkUpdateClick(object sender, RoutedEventArgs e) => CopyQuarkClicked?.Invoke(sender, e);

        private void OnDownloadUpdateClick(object sender, RoutedEventArgs e) => DownloadClicked?.Invoke(sender, e);

        private void OnInstallUpdateClick(object sender, RoutedEventArgs e) => InstallClicked?.Invoke(sender, e);

        private void OnOpenUpdateFolderClick(object sender, RoutedEventArgs e) => OpenFolderClicked?.Invoke(sender, e);
    }
}

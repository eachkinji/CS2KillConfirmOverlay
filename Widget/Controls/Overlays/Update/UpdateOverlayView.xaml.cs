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
        public event RoutedEventHandler OpenDownloadLinkClicked;
        public event RoutedEventHandler ToggleReleaseNotesClicked;
        public event RoutedEventHandler CopyQuarkClicked;

        public void RefreshCredits()
        {
            UpdateCreditsCommunityPanel?.ApplyLanguage();
        }

        private void OnCloseUpdateOverlayClick(object sender, RoutedEventArgs e) => CloseClicked?.Invoke(sender, e);

        private void OnOpenAuthorGitHubClick(object sender, RoutedEventArgs e) => OpenAuthorGitHubClicked?.Invoke(sender, e);

        private void OnOpenBilibiliClick(object sender, RoutedEventArgs e) => OpenBilibiliClicked?.Invoke(sender, e);

        private void OnOpenDownloadLinkClick(object sender, RoutedEventArgs e) => OpenDownloadLinkClicked?.Invoke(sender, e);

        private void OnToggleReleaseNotesClick(object sender, RoutedEventArgs e) => ToggleReleaseNotesClicked?.Invoke(sender, e);

        private void OnCopyQuarkUpdateClick(object sender, RoutedEventArgs e) => CopyQuarkClicked?.Invoke(sender, e);
    }
}

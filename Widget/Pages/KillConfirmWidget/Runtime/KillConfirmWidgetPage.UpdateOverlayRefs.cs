using Windows.UI.Xaml.Controls;

namespace KillConfirmGameBar
{
    public sealed partial class KillConfirmWidgetPage
    {
        private TextBlock UpdateDialogTitleText => UpdateOverlay.UpdateDialogTitleText;
        private TextBlock UpdateDialogVersionText => UpdateOverlay.UpdateDialogVersionText;
        private TextBlock UpdateDialogBodyText => UpdateOverlay.UpdateDialogBodyText;
        private TextBlock UpdateAboutText => UpdateOverlay.UpdateAboutText;
        private TextBlock UpdateAuthorGitHubText => UpdateOverlay.UpdateAuthorGitHubText;
        private TextBlock UpdateAuthorBilibiliText => UpdateOverlay.UpdateAuthorBilibiliText;
        private TextBlock UpdateDownloadLinkText => UpdateOverlay.UpdateDownloadLinkText;
        private TextBlock UpdateReleaseTitleText => UpdateOverlay.UpdateReleaseTitleText;
        private TextBlock UpdateReleaseInfoText => UpdateOverlay.UpdateReleaseInfoText;
        private TextBlock UpdateQuarkCodeText => UpdateOverlay.UpdateQuarkCodeText;
        private ScrollViewer UpdateReleaseScrollViewer => UpdateOverlay.UpdateReleaseScrollViewer;
        private Button UpdateReleaseToggleButton => UpdateOverlay.UpdateReleaseToggleButton;
        private Button UpdateCopyQuarkButton => UpdateOverlay.UpdateCopyQuarkButton;
        private Button UpdateCloseButton => UpdateOverlay.UpdateCloseButton;

        private void WireUpdateOverlayEvents()
        {
            UpdateOverlay.CloseClicked += OnCloseUpdateOverlayClick;
            UpdateOverlay.OpenAuthorGitHubClicked += OnOpenAuthorGitHubClick;
            UpdateOverlay.OpenBilibiliClicked += OnOpenBilibiliClick;
            UpdateOverlay.OpenDownloadLinkClicked += OnOpenDownloadLinkClick;
            UpdateOverlay.ToggleReleaseNotesClicked += OnToggleReleaseNotesClick;
            UpdateOverlay.CopyQuarkClicked += OnCopyQuarkUpdateClick;
        }
    }
}

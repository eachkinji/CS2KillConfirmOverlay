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
        private TextBlock UpdateProjectHomeText => UpdateOverlay.UpdateProjectHomeText;
        private TextBlock UpdateReleaseTitleText => UpdateOverlay.UpdateReleaseTitleText;
        private TextBlock UpdateReleaseInfoText => UpdateOverlay.UpdateReleaseInfoText;
        private TextBlock UpdateQuarkHintText => UpdateOverlay.UpdateQuarkHintText;
        private TextBlock UpdateQuarkCodeText => UpdateOverlay.UpdateQuarkCodeText;
        private TextBlock UpdateDownloadStatusText => UpdateOverlay.UpdateDownloadStatusText;
        private ScrollViewer UpdateReleaseScrollViewer => UpdateOverlay.UpdateReleaseScrollViewer;
        private Button UpdateReleaseToggleButton => UpdateOverlay.UpdateReleaseToggleButton;
        private Button UpdateOpenQuarkButton => UpdateOverlay.UpdateOpenQuarkButton;
        private Button UpdateCopyQuarkButton => UpdateOverlay.UpdateCopyQuarkButton;
        private Button UpdateDownloadButton => UpdateOverlay.UpdateDownloadButton;
        private Button UpdateInstallButton => UpdateOverlay.UpdateInstallButton;
        private Button UpdateOpenFolderButton => UpdateOverlay.UpdateOpenFolderButton;
        private Button UpdateCloseButton => UpdateOverlay.UpdateCloseButton;
        private ProgressBar UpdateDownloadProgress => UpdateOverlay.UpdateDownloadProgress;

        private void WireUpdateOverlayEvents()
        {
            UpdateOverlay.CloseClicked += OnCloseUpdateOverlayClick;
            UpdateOverlay.OpenAuthorGitHubClicked += OnOpenAuthorGitHubClick;
            UpdateOverlay.OpenBilibiliClicked += OnOpenBilibiliClick;
            UpdateOverlay.OpenGitHubClicked += OnOpenGitHubClick;
            UpdateOverlay.ToggleReleaseNotesClicked += OnToggleReleaseNotesClick;
            UpdateOverlay.OpenQuarkClicked += OnOpenQuarkUpdateClick;
            UpdateOverlay.CopyQuarkClicked += OnCopyQuarkUpdateClick;
            UpdateOverlay.DownloadClicked += OnDownloadUpdateClick;
            UpdateOverlay.InstallClicked += OnInstallUpdateClick;
            UpdateOverlay.OpenFolderClicked += OnOpenUpdateFolderClick;
        }
    }
}

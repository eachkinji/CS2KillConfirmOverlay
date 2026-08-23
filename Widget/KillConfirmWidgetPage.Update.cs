using System;
using System.Threading.Tasks;
using KillConfirmGameBar.Services;
using Windows.ApplicationModel;
using Windows.ApplicationModel.DataTransfer;
using Windows.Data.Json;
using Windows.System;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.Web.Http;

namespace KillConfirmGameBar
{
    public sealed partial class KillConfirmWidgetPage
    {
        private async void OnUpdateClick(object sender, RoutedEventArgs e)
        {
            if (_updateCheckInProgress)
            {
                return;
            }

            await CheckForUpdatesAsync(true);
        }

        private async Task CheckForUpdatesAsync(bool interactive)
        {
            if (_updateCheckInProgress)
            {
                return;
            }

            _updateCheckInProgress = true;
            UpdateUpdateButtonVisualState();

            try
            {
                using (HttpClient client = new HttpClient())
                {
                    client.DefaultRequestHeaders.TryAppendWithoutValidation("User-Agent", "KillConfirmOverlayUpdater/1.0");
                    string payload = await client.GetStringAsync(LatestReleaseUri);
                    App.Log("Update check payload received from GitHub.");

                    if (TryParseLatestRelease(payload, out Version latestVersion, out string latestVersionText, out string pageUrl, out string releaseNotes, out DateTimeOffset? publishedAt))
                    {
                        Version currentVersion = GetCurrentPackageVersion();
                        _latestReleaseVersion = latestVersionText;
                        _latestReleasePageUrl = pageUrl ?? string.Empty;
                        _latestReleaseNotes = releaseNotes ?? string.Empty;
                        _latestReleasePublishedAt = publishedAt;

                        if (currentVersion < latestVersion)
                        {
                            _updateAvailabilityState = UpdateAvailabilityState.UpdateAvailable;
                        }
                        else
                        {
                            _updateAvailabilityState = UpdateAvailabilityState.UpToDate;
                        }
                    }
                    else
                    {
                        ClearLatestReleaseInfo();
                        _updateAvailabilityState = UpdateAvailabilityState.Unavailable;
                    }
                }
            }
            catch (Exception ex)
            {
                App.Log("Update check failed: " + ex);
                ClearLatestReleaseInfo();
                _updateAvailabilityState = UpdateAvailabilityState.Unavailable;
            }
            finally
            {
                _updateCheckInProgress = false;
                UpdateUpdateButtonVisualState();
            }

            if (!interactive)
            {
                return;
            }

            switch (_updateAvailabilityState)
            {
                case UpdateAvailabilityState.UpdateAvailable:
                    await PromptForUpdateAsync();
                    break;
                case UpdateAvailabilityState.UpToDate:
                    await PromptForUpdateAsync();
                    break;
                default:
                    await PromptForUpdateAsync();
                    ShowStatusHint(LocalizationManager.Text("UpdateCheckFailedHint"), Color.FromArgb(255, 75, 85, 99));
                    break;
            }
        }

        private async Task PromptForUpdateAsync()
        {
            if (string.IsNullOrWhiteSpace(_latestReleaseVersion))
            {
                _latestReleaseVersion = GetDisplayVersion();
            }

            ShowUpdateOverlay();
            await Task.CompletedTask;
        }

        private void ShowUpdateOverlay()
        {
            bool updateAvailable = _updateAvailabilityState == UpdateAvailabilityState.UpdateAvailable;

            UpdateDialogTitleText.Text = updateAvailable
                ? LocalizationManager.Text("UpdatePromptTitle")
                : LocalizationManager.Text("VersionAboutTitle");
            UpdateDialogVersionText.Text = _latestReleaseVersion;
            if (updateAvailable)
            {
                UpdateDialogBodyText.Text = string.Format(LocalizationManager.Text("UpdatePromptBody"), _latestReleaseVersion);
            }
            else if (_updateAvailabilityState == UpdateAvailabilityState.Unavailable)
            {
                UpdateDialogBodyText.Text = string.Format(LocalizationManager.Text("UpdateUnavailableBody"), GetDisplayVersion());
            }
            else
            {
                UpdateDialogBodyText.Text = string.Format(LocalizationManager.Text("UpdateAlreadyLatestBody"), GetDisplayVersion());
            }
            UpdateAboutText.Text = LocalizationManager.Text("VersionAboutBody");
            UpdateReleaseInfoText.Text = BuildReleaseInfoText();
            UpdateAuthorGitHubText.Text = LocalizationManager.Text("AuthorGitHub");
            UpdateAuthorBilibiliText.Text = LocalizationManager.Text("AuthorBilibili");
            UpdateDownloadLinkText.Text = LocalizationManager.Text("OpenProjectDownloadLink");
            UpdateReleaseTitleText.Text = LocalizationManager.Text("OpenReleasePage");
            _releaseNotesExpanded = false;
            UpdateReleaseNotesVisualState();
            UpdateQuarkCodeText.Text = string.Format(LocalizationManager.Text("UpdateQuarkCode"), QuarkUpdateCode);
            UpdateCopyQuarkButton.Content = LocalizationManager.Text("UpdateCopyQuark");
            UpdateOverlay.RefreshCredits();
            UpdateCloseButton.IsEnabled = true;
            UpdateOverlay.Visibility = Visibility.Visible;
        }

        private string BuildReleaseInfoText()
        {
            string published = _latestReleasePublishedAt.HasValue
                ? _latestReleasePublishedAt.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm")
                : LocalizationManager.Text("UnknownReleaseTime");
            string notes = string.IsNullOrWhiteSpace(_latestReleaseNotes)
                ? LocalizationManager.Text("NoReleaseNotes")
                : _latestReleaseNotes.Trim();
            return string.Format(LocalizationManager.Text("ReleaseInfoBody"), published, notes);
        }

        private void HideUpdateOverlay()
        {
            UpdateOverlay.Visibility = Visibility.Collapsed;
        }

        private void OnCloseUpdateOverlayClick(object sender, RoutedEventArgs e)
        {
            HideUpdateOverlay();
        }

        private async void OnOpenDownloadLinkClick(object sender, RoutedEventArgs e)
        {
            bool launched = await TryLaunchFullTrustHelperAsync(OpenQuarkUpdateParameterGroupId);
            if (!launched)
            {
                launched = await Launcher.LaunchUriAsync(new Uri(QuarkUpdateUrl));
            }

            ShowStatusHint(
                launched
                    ? LocalizationManager.Text("UpdateOpenQuarkStarting")
                    : LocalizationManager.Text("UpdateOpenQuarkFailed"),
                launched
                    ? Color.FromArgb(255, 180, 90, 0)
                    : Color.FromArgb(255, 185, 28, 28));
        }

        private async void OnOpenAuthorGitHubClick(object sender, RoutedEventArgs e)
        {
            bool launched = await TryLaunchFullTrustHelperAsync(OpenAuthorGitHubParameterGroupId);
            if (!launched)
            {
                launched = await Launcher.LaunchUriAsync(new Uri(AuthorGitHubUrl));
            }

            ShowStatusHint(
                launched ? LocalizationManager.Text("OpenGitHubStarting") : LocalizationManager.Text("OpenGitHubFailed"),
                launched ? Color.FromArgb(255, 180, 90, 0) : Color.FromArgb(255, 185, 28, 28));
        }

        private async void OnOpenBilibiliClick(object sender, RoutedEventArgs e)
        {
            bool launched = await TryLaunchFullTrustHelperAsync(OpenAuthorBilibiliParameterGroupId);
            if (!launched)
            {
                launched = await Launcher.LaunchUriAsync(new Uri(AuthorBilibiliUrl));
            }

            ShowStatusHint(
                launched ? LocalizationManager.Text("OpenBilibiliStarting") : LocalizationManager.Text("OpenBilibiliFailed"),
                launched ? Color.FromArgb(255, 180, 90, 0) : Color.FromArgb(255, 185, 28, 28));
        }

        private void OnToggleReleaseNotesClick(object sender, RoutedEventArgs e)
        {
            _releaseNotesExpanded = !_releaseNotesExpanded;
            UpdateReleaseNotesVisualState();
        }

        private void UpdateReleaseNotesVisualState()
        {
            if (UpdateReleaseScrollViewer == null || UpdateReleaseToggleButton == null)
            {
                return;
            }

            UpdateReleaseScrollViewer.MaxHeight = _releaseNotesExpanded ? 120 : 0;
            UpdateReleaseScrollViewer.Visibility = _releaseNotesExpanded ? Visibility.Visible : Visibility.Collapsed;
            UpdateReleaseToggleButton.Content = _releaseNotesExpanded
                ? LocalizationManager.Text("CollapseReleaseNotes")
                : LocalizationManager.Text("ExpandReleaseNotes");
        }

        private async void OnOpenReleaseClick(object sender, RoutedEventArgs e)
        {
            string url = string.IsNullOrWhiteSpace(_latestReleasePageUrl)
                ? LatestReleasePageFallbackUrl
                : _latestReleasePageUrl;
            bool launched = await Launcher.LaunchUriAsync(new Uri(url));
            ShowStatusHint(
                launched ? LocalizationManager.Text("OpenReleaseStarting") : LocalizationManager.Text("OpenGitHubFailed"),
                launched ? Color.FromArgb(255, 180, 90, 0) : Color.FromArgb(255, 185, 28, 28));
        }

        private void OnCopyQuarkUpdateClick(object sender, RoutedEventArgs e)
        {
            var package = new DataPackage();
            package.SetText(QuarkUpdateUrl + Environment.NewLine + LocalizationManager.Text("UpdateQuarkCodePlain") + QuarkUpdateCode);
            Clipboard.SetContent(package);
            ShowStatusHint(LocalizationManager.Text("UpdateQuarkCopied"), Color.FromArgb(255, 5, 122, 85));
        }

        private void UpdateUpdateButtonVisualState()
        {
            if (UpdateButton == null || VersionText == null || UpdateIndicatorDot == null)
            {
                return;
            }

            VersionText.Text = GetUpdateButtonLabel();

            bool valorant = GameStyleService.Current == GameStyleMode.Valorant;
            GameThemePalette theme = GameThemePalette.Current;
            Color background = theme.Field;
            Color border = theme.SoftBorder;
            Color foreground = valorant ? theme.Text : Color.FromArgb(255, 138, 106, 54);
            Color dot = GetUpdateIndicatorColor();

            if (_updateCheckInProgress)
            {
                background = theme.SubtleField;
                border = valorant ? theme.Secondary : Color.FromArgb(255, 185, 220, 236);
                foreground = valorant ? theme.Secondary : Color.FromArgb(255, 46, 136, 184);
            }
            else if (_updateAvailabilityState == UpdateAvailabilityState.UpdateAvailable)
            {
                background = theme.WarningField;
                border = theme.WarningBorder;
                foreground = theme.WarningText;
            }
            else if (_updateAvailabilityState == UpdateAvailabilityState.UpToDate)
            {
                background = valorant ? Color.FromArgb(255, 16, 44, 42) : Color.FromArgb(255, 235, 253, 245);
                border = Color.FromArgb(255, 52, 211, 153);
                foreground = valorant ? Color.FromArgb(255, 167, 255, 235) : Color.FromArgb(255, 5, 122, 85);
            }

            UpdateButton.Background = new SolidColorBrush(background);
            UpdateButton.BorderBrush = new SolidColorBrush(border);
            VersionText.Foreground = new SolidColorBrush(foreground);
            UpdateIndicatorDot.Fill = new SolidColorBrush(dot);

            string tooltipBody = ResolveUpdateTooltipBody();
            ToolTipService.SetToolTip(
                UpdateButton,
                string.IsNullOrWhiteSpace(tooltipBody)
                    ? GetDisplayVersion()
                    : GetDisplayVersion() + "\n" + tooltipBody);
        }

        private Color GetUpdateIndicatorColor()
        {
            if (_updateCheckInProgress)
            {
                return Color.FromArgb(255, 46, 136, 184);
            }

            switch (_updateAvailabilityState)
            {
                case UpdateAvailabilityState.UpToDate:
                    return Color.FromArgb(255, 52, 211, 153);
                case UpdateAvailabilityState.UpdateAvailable:
                    return Color.FromArgb(255, 180, 90, 0);
                default:
                    return Color.FromArgb(255, 75, 85, 99);
            }
        }

        private string ResolveUpdateTooltipBody()
        {
            if (_updateCheckInProgress)
            {
                return LocalizationManager.Text("UpdateCheckingTooltip");
            }

            switch (_updateAvailabilityState)
            {
                case UpdateAvailabilityState.UpToDate:
                    return LocalizationManager.Text("UpdateLatestTooltip");
                case UpdateAvailabilityState.UpdateAvailable:
                    return string.Format(LocalizationManager.Text("UpdateAvailableTooltip"), _latestReleaseVersion);
                default:
                    return LocalizationManager.Text("UpdateUnavailableTooltip");
            }
        }

        private void ClearLatestReleaseInfo()
        {
            _latestReleaseVersion = string.Empty;
            _latestReleasePageUrl = string.Empty;
            _latestReleaseNotes = string.Empty;
            _latestReleasePublishedAt = null;
        }

        private static Version GetCurrentPackageVersion()
        {
            PackageVersion version = Package.Current.Id.Version;
            return new Version(version.Major, version.Minor, version.Build, version.Revision);
        }

        private static bool TryParseLatestRelease(
            string payload,
            out Version latestVersion,
            out string latestVersionText,
            out string pageUrl,
            out string releaseNotes,
            out DateTimeOffset? publishedAt)
        {
            latestVersion = new Version(0, 0, 0, 0);
            latestVersionText = string.Empty;
            pageUrl = string.Empty;
            releaseNotes = string.Empty;
            publishedAt = null;

            JsonObject root = JsonObject.Parse(payload);
            string tagName = root.ContainsKey("tag_name")
                ? root.GetNamedString("tag_name")
                : string.Empty;
            string releaseName = root.ContainsKey("name")
                ? root.GetNamedString("name")
                : string.Empty;
            pageUrl = root.ContainsKey("html_url")
                ? root.GetNamedString("html_url")
                : string.Empty;
            releaseNotes = root.ContainsKey("body")
                ? root.GetNamedString("body")
                : string.Empty;
            if (root.ContainsKey("published_at")
                && DateTimeOffset.TryParse(root.GetNamedString("published_at"), out DateTimeOffset parsedPublishedAt))
            {
                publishedAt = parsedPublishedAt;
            }

            string versionText = !string.IsNullOrWhiteSpace(tagName) ? tagName : releaseName;
            if (!TryParseVersion(versionText, out latestVersion))
            {
                return false;
            }

            latestVersionText = NormalizeVersionText(versionText);
            return true;
        }

        private static bool TryParseVersion(string text, out Version version)
        {
            version = null;
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            string normalized = NormalizeVersionText(text);
            return Version.TryParse(normalized, out version);
        }

        private static string NormalizeVersionText(string text)
        {
            string normalized = (text ?? string.Empty).Trim();
            if (normalized.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized.Substring(1);
            }

            return normalized;
        }

        private static string GetDisplayVersion()
        {
            try
            {
                PackageVersion version = Package.Current.Id.Version;
                return $"{version.Major}.{version.Minor}.{version.Build}";
            }
            catch (Exception)
            {
                return "?";
            }
        }

        private string GetUpdateButtonLabel()
        {
            if (_updateAvailabilityState == UpdateAvailabilityState.UpdateAvailable)
            {
                return LocalizationManager.Current == UiLanguage.SimplifiedChinese
                    ? $"版本：{GetCompactDisplayVersion()} 需更新"
                    : $"Version:{GetCompactDisplayVersion()} Update required";
            }

            return LocalizationManager.Current == UiLanguage.SimplifiedChinese
                ? $"版本：{GetCompactDisplayVersion()} 作者 Zac"
                : $"Version:{GetCompactDisplayVersion()} Author Zac";
        }

        private static string GetCompactDisplayVersion()
        {
            try
            {
                PackageVersion version = Package.Current.Id.Version;
                return $"{version.Major}.{version.Minor}.{version.Build}";
            }
            catch (Exception)
            {
                return "?";
            }
        }

        private enum UpdateAvailabilityState
        {
            Unknown,
            UpToDate,
            UpdateAvailable,
            Unavailable
        }

    }
}

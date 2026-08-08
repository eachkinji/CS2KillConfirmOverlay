using System;
using System.Threading.Tasks;
using KillConfirmGameBar.Services;
using Windows.ApplicationModel;
using Windows.ApplicationModel.DataTransfer;
using Windows.Data.Json;
using Windows.Storage;
using Windows.Storage.Streams;
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

                    if (TryParseLatestRelease(payload, out Version latestVersion, out string latestVersionText, out string downloadUrl, out string assetName, out string pageUrl, out string releaseNotes, out DateTimeOffset? publishedAt))
                    {
                        Version currentVersion = GetCurrentPackageVersion();
                        _latestReleaseVersion = latestVersionText;
                        _latestReleaseDownloadUrl = downloadUrl ?? string.Empty;
                        _latestReleaseAssetName = assetName ?? string.Empty;
                        _latestReleasePageUrl = pageUrl ?? string.Empty;
                        _latestReleaseNotes = releaseNotes ?? string.Empty;
                        _latestReleasePublishedAt = publishedAt;

                        if (currentVersion < latestVersion && !string.IsNullOrWhiteSpace(_latestReleaseDownloadUrl))
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
            bool hasInstaller = !string.IsNullOrWhiteSpace(_latestReleaseDownloadUrl)
                && !string.IsNullOrWhiteSpace(_latestReleaseAssetName);

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
            UpdateProjectHomeText.Text = LocalizationManager.Text("OpenProjectHome");
            UpdateReleaseTitleText.Text = LocalizationManager.Text("OpenReleasePage");
            _releaseNotesExpanded = false;
            UpdateReleaseNotesVisualState();
            UpdateQuarkHintText.Text = LocalizationManager.Text("UpdateQuarkHint");
            UpdateQuarkCodeText.Text = string.Format(LocalizationManager.Text("UpdateQuarkCode"), QuarkUpdateCode);
            UpdateOpenQuarkButton.Content = LocalizationManager.Text("UpdateOpenQuark");
            UpdateCopyQuarkButton.Content = LocalizationManager.Text("UpdateCopyQuark");
            UpdateDownloadButton.Content = LocalizationManager.Text("UpdateDownloadInstaller");
            UpdateInstallButton.Content = LocalizationManager.Text("UpdateInstallNow");
            UpdateOpenFolderButton.Content = LocalizationManager.Text("UpdateOpenDownloadFolder");
            UpdateDownloadStatusText.Text = hasInstaller
                ? LocalizationManager.Text("UpdateReadyToDownload")
                : LocalizationManager.Text("UpdateNoInstallerHint");
            UpdateDownloadProgress.Value = 0;
            UpdateDownloadProgress.IsIndeterminate = false;
            _updateInstallerReady = false;
            UpdateDownloadButton.IsEnabled = !_updateDownloadInProgress && hasInstaller;
            UpdateInstallButton.IsEnabled = false;
            UpdateOpenFolderButton.IsEnabled = true;
            UpdateCloseButton.IsEnabled = !_updateDownloadInProgress;
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
            if (_updateDownloadInProgress)
            {
                return;
            }

            UpdateOverlay.Visibility = Visibility.Collapsed;
        }

        private void OnCloseUpdateOverlayClick(object sender, RoutedEventArgs e)
        {
            HideUpdateOverlay();
        }

        private async void OnOpenQuarkUpdateClick(object sender, RoutedEventArgs e)
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

        private async void OnOpenGitHubClick(object sender, RoutedEventArgs e)
        {
            bool launched = await TryLaunchFullTrustHelperAsync(OpenProjectGitHubParameterGroupId);
            if (!launched)
            {
                launched = await Launcher.LaunchUriAsync(new Uri(ProjectGitHubUrl));
            }

            ShowStatusHint(
                launched ? LocalizationManager.Text("OpenGitHubStarting") : LocalizationManager.Text("OpenGitHubFailed"),
                launched ? Color.FromArgb(255, 180, 90, 0) : Color.FromArgb(255, 185, 28, 28));
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
                ? ProjectGitHubUrl + "/releases"
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

        private async void OnDownloadUpdateClick(object sender, RoutedEventArgs e)
        {
            if (_updateDownloadInProgress)
            {
                return;
            }

            await DownloadAndLaunchUpdateAsync();
        }

        private async Task DownloadAndLaunchUpdateAsync()
        {
            if (string.IsNullOrWhiteSpace(_latestReleaseDownloadUrl) || string.IsNullOrWhiteSpace(_latestReleaseAssetName))
            {
                ShowStatusHint(LocalizationManager.Text("UpdateNoInstallerHint"), Color.FromArgb(255, 75, 85, 99));
                return;
            }

            _updateDownloadInProgress = true;
            UpdateDownloadButton.IsEnabled = false;
            UpdateInstallButton.IsEnabled = false;
            UpdateCloseButton.IsEnabled = false;
            UpdateDownloadProgress.Value = 0;
            UpdateDownloadProgress.IsIndeterminate = false;
            UpdateDownloadStatusText.Text = LocalizationManager.Text("UpdateDownloading");

            try
            {
                _updateInstallerReady = false;
                await WritePendingUpdateFileAsync();
                await DeleteUpdateDownloadResultAsync();
                bool launched = await TryLaunchFullTrustHelperAsync(DownloadPendingUpdateParameterGroupId);
                if (!launched)
                {
                    throw new InvalidOperationException("Full-trust update downloader did not launch.");
                }

                UpdateDownloadProgress.IsIndeterminate = true;
                UpdateDownloadResult downloadResult = await WaitForUpdateDownloadResultAsync();
                if (!downloadResult.Success)
                {
                    throw new InvalidOperationException(string.IsNullOrWhiteSpace(downloadResult.Error)
                        ? "Full-trust update downloader failed."
                        : downloadResult.Error);
                }

                UpdateDownloadProgress.IsIndeterminate = false;
                UpdateDownloadProgress.Value = 100;
                _updateInstallerReady = true;
                UpdateDownloadStatusText.Text = LocalizationManager.Text("UpdateDownloadedReady");
                UpdateInstallButton.IsEnabled = true;
                UpdateOpenFolderButton.IsEnabled = true;
                ShowStatusHint(LocalizationManager.Text("UpdateDownloadedReady"), Color.FromArgb(255, 5, 122, 85));
            }
            catch (Exception ex)
            {
                App.Log("Update download failed: " + ex);
                UpdateDownloadProgress.IsIndeterminate = false;
                _updateInstallerReady = false;
                UpdateDownloadStatusText.Text = LocalizationManager.Text("UpdateDownloadFailed");
                ShowStatusHint(LocalizationManager.Text("UpdateDownloadFailed"), Color.FromArgb(255, 185, 28, 28));
            }
            finally
            {
                _updateDownloadInProgress = false;
                UpdateDownloadButton.IsEnabled = true;
                UpdateInstallButton.IsEnabled = _updateInstallerReady;
                UpdateOpenFolderButton.IsEnabled = true;
                UpdateCloseButton.IsEnabled = true;
            }
        }

        private async void OnInstallUpdateClick(object sender, RoutedEventArgs e)
        {
            if (!_updateInstallerReady)
            {
                ShowStatusHint(LocalizationManager.Text("UpdateInstallNoFile"), Color.FromArgb(255, 75, 85, 99));
                return;
            }

            try
            {
                await WritePendingUpdateFileAsync();
                bool launched = await TryLaunchFullTrustHelperAsync(RunPendingUpdateParameterGroupId);
                ShowStatusHint(
                    launched
                        ? LocalizationManager.Text("UpdateStartingHint")
                        : LocalizationManager.Text("UpdateLaunchFailed"),
                    launched
                        ? Color.FromArgb(255, 180, 90, 0)
                        : Color.FromArgb(255, 185, 28, 28));
            }
            catch (Exception ex)
            {
                App.Log("Update installer launch failed: " + ex);
                ShowStatusHint(LocalizationManager.Text("UpdateLaunchFailed"), Color.FromArgb(255, 185, 28, 28));
            }
        }

        private async void OnOpenUpdateFolderClick(object sender, RoutedEventArgs e)
        {
            try
            {
                bool launched = await TryLaunchFullTrustHelperAsync(OpenUpdateFolderParameterGroupId);
                if (!launched)
                {
                    StorageFolder updateFolder = await ApplicationData.Current.LocalFolder.CreateFolderAsync(
                        "updates",
                        CreationCollisionOption.OpenIfExists);
                    launched = await Launcher.LaunchFolderAsync(updateFolder);
                }

                ShowStatusHint(
                    launched
                        ? LocalizationManager.Text("UpdateFolderOpening")
                        : LocalizationManager.Text("UpdateFolderOpenFailed"),
                    launched
                        ? Color.FromArgb(255, 180, 90, 0)
                        : Color.FromArgb(255, 185, 28, 28));
            }
            catch (Exception ex)
            {
                App.Log("Open update folder failed: " + ex);
                ShowStatusHint(LocalizationManager.Text("UpdateFolderOpenFailed"), Color.FromArgb(255, 185, 28, 28));
            }
        }

        private async Task WritePendingUpdateFileAsync()
        {
            JsonObject payload = new JsonObject
            {
                ["version"] = JsonValue.CreateStringValue(_latestReleaseVersion ?? string.Empty),
                ["download_url"] = JsonValue.CreateStringValue(_latestReleaseDownloadUrl ?? string.Empty),
                ["asset_name"] = JsonValue.CreateStringValue(_latestReleaseAssetName ?? "KillConfirmGameBar_Update.exe"),
                ["installer_path"] = JsonValue.CreateStringValue(string.Empty)
            };

            StorageFile pendingFile = await ApplicationData.Current.LocalFolder.CreateFileAsync(
                PendingUpdateFileName,
                CreationCollisionOption.ReplaceExisting);
            await FileIO.WriteTextAsync(pendingFile, payload.Stringify());
        }

        private async Task DeleteUpdateDownloadResultAsync()
        {
            try
            {
                IStorageItem item = await ApplicationData.Current.LocalFolder.TryGetItemAsync(UpdateDownloadResultFileName);
                if (item is StorageFile file)
                {
                    await file.DeleteAsync(StorageDeleteOption.PermanentDelete);
                }
            }
            catch (Exception ex)
            {
                App.Log("Delete update download result failed: " + ex);
            }
        }

        private async Task<UpdateDownloadResult> WaitForUpdateDownloadResultAsync()
        {
            DateTimeOffset deadline = DateTimeOffset.Now.AddMinutes(10);
            while (DateTimeOffset.Now < deadline)
            {
                IStorageItem item = await ApplicationData.Current.LocalFolder.TryGetItemAsync(UpdateDownloadResultFileName);
                if (item is StorageFile file)
                {
                    string text = await FileIO.ReadTextAsync(file);
                    UpdateDownloadResult result = ParseUpdateDownloadResult(text);
                    if (result.Completed)
                    {
                        return result;
                    }

                    UpdateDownloadProgressUi(result.Percent);
                }

                await Task.Delay(500);
            }

            return new UpdateDownloadResult
            {
                Success = false,
                Error = LocalizationManager.Text("UpdateDownloadFailed")
            };
        }

        private static UpdateDownloadResult ParseUpdateDownloadResult(string text)
        {
            if (!JsonObject.TryParse(text, out JsonObject json))
            {
                return new UpdateDownloadResult { Success = false, Error = "Invalid update download result." };
            }

            bool success = json.TryGetValue("success", out IJsonValue successValue)
                && successValue.ValueType == JsonValueType.Boolean
                && successValue.GetBoolean();
            bool completed = json.TryGetValue("completed", out IJsonValue completedValue)
                && completedValue.ValueType == JsonValueType.Boolean
                && completedValue.GetBoolean();
            double? percent = json.TryGetValue("percent", out IJsonValue percentValue)
                && percentValue.ValueType == JsonValueType.Number
                    ? percentValue.GetNumber()
                    : (double?)null;
            string installerPath = json.TryGetValue("installer_path", out IJsonValue installerPathValue)
                && installerPathValue.ValueType == JsonValueType.String
                    ? installerPathValue.GetString()
                    : string.Empty;
            string error = json.TryGetValue("error", out IJsonValue errorValue)
                && errorValue.ValueType == JsonValueType.String
                    ? errorValue.GetString()
                    : string.Empty;

            return new UpdateDownloadResult
            {
                Success = success,
                Completed = completed,
                Percent = percent,
                InstallerPath = installerPath,
                Error = error
            };
        }

        private void UpdateDownloadProgressUi(double? percent)
        {
            if (percent.HasValue)
            {
                double safePercent = Math.Max(0.0, Math.Min(100.0, percent.Value));
                UpdateDownloadProgress.IsIndeterminate = false;
                UpdateDownloadProgress.Value = safePercent;
                UpdateDownloadStatusText.Text = string.Format(LocalizationManager.Text("UpdateDownloadProgress"), safePercent);
            }
            else
            {
                UpdateDownloadProgress.IsIndeterminate = true;
                UpdateDownloadStatusText.Text = LocalizationManager.Text("UpdateDownloading");
            }
        }

        private async Task<StorageFile> DownloadUpdateInstallerAsync(Uri downloadUri, string assetName)
        {
            string safeAssetName = PathSafeFileName(assetName, "KillConfirmGameBar_Update.exe");
            StorageFolder updateFolder = await GetExternalUpdateFolderAsync();
            StorageFile installerFile = await updateFolder.CreateFileAsync(
                safeAssetName,
                CreationCollisionOption.ReplaceExisting);

            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.TryAppendWithoutValidation("User-Agent", "KillConfirmOverlayUpdater/1.0");
                HttpResponseMessage response = await client.GetAsync(downloadUri, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                ulong? totalBytes = response.Content.Headers.ContentLength;
                using (IInputStream input = await response.Content.ReadAsInputStreamAsync())
                using (IRandomAccessStream output = await installerFile.OpenAsync(FileAccessMode.ReadWrite))
                {
                    ulong downloadedBytes = 0;
                    const uint bufferSize = 1024 * 128;
                    while (true)
                    {
                        IBuffer buffer = new Windows.Storage.Streams.Buffer(bufferSize);
                        IBuffer readBuffer = await input.ReadAsync(buffer, bufferSize, InputStreamOptions.None);
                        if (readBuffer.Length == 0)
                        {
                            break;
                        }

                        await output.WriteAsync(readBuffer);
                        downloadedBytes += readBuffer.Length;
                        UpdateDownloadProgressUi(downloadedBytes, totalBytes);
                    }

                    await output.FlushAsync();
                }
            }

            return installerFile;
        }

        private static async Task<StorageFolder> GetExternalUpdateFolderAsync()
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string updateFolderPath = System.IO.Path.Combine(localAppData, "KillConfirmGameBar", "updates");
            System.IO.Directory.CreateDirectory(updateFolderPath);
            return await StorageFolder.GetFolderFromPathAsync(updateFolderPath);
        }

        private void UpdateDownloadProgressUi(ulong downloadedBytes, ulong? totalBytes)
        {
            if (totalBytes.HasValue && totalBytes.Value > 0)
            {
                double percent = Math.Min(100.0, downloadedBytes * 100.0 / totalBytes.Value);
                UpdateDownloadProgress.IsIndeterminate = false;
                UpdateDownloadProgress.Value = percent;
                UpdateDownloadStatusText.Text = string.Format(LocalizationManager.Text("UpdateDownloadProgress"), percent);
            }
            else
            {
                UpdateDownloadProgress.IsIndeterminate = true;
                UpdateDownloadStatusText.Text = LocalizationManager.Text("UpdateDownloading");
            }
        }

        private static string PathSafeFileName(string value, string fallback)
        {
            string name = string.IsNullOrWhiteSpace(value) ? fallback : value;
            foreach (char invalidChar in System.IO.Path.GetInvalidFileNameChars())
            {
                name = name.Replace(invalidChar, '_');
            }

            return string.IsNullOrWhiteSpace(name) ? fallback : name;
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
            _latestReleaseDownloadUrl = string.Empty;
            _latestReleaseAssetName = string.Empty;
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
            out string downloadUrl,
            out string assetName,
            out string pageUrl,
            out string releaseNotes,
            out DateTimeOffset? publishedAt)
        {
            latestVersion = new Version(0, 0, 0, 0);
            latestVersionText = string.Empty;
            downloadUrl = string.Empty;
            assetName = string.Empty;
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

            if (!root.ContainsKey("assets"))
            {
                return false;
            }

            JsonArray assets = root.GetNamedArray("assets");
            foreach (IJsonValue assetValue in assets)
            {
                if (assetValue.ValueType != JsonValueType.Object)
                {
                    continue;
                }

                JsonObject asset = assetValue.GetObject();
                string name = asset.ContainsKey("name") ? asset.GetNamedString("name") : string.Empty;
                string browserDownloadUrl = asset.ContainsKey("browser_download_url")
                    ? asset.GetNamedString("browser_download_url")
                    : string.Empty;

                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(browserDownloadUrl))
                {
                    continue;
                }

                if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                    && name.StartsWith("KillConfirmGameBar_Setup_", StringComparison.OrdinalIgnoreCase))
                {
                    assetName = name;
                    downloadUrl = browserDownloadUrl;
                    return true;
                }
            }

            return false;
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

        private static string GetUpdateButtonLabel()
        {
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

        private sealed class UpdateDownloadResult
        {
            public bool Success { get; set; }
            public bool Completed { get; set; }
            public double? Percent { get; set; }
            public string InstallerPath { get; set; }
            public string Error { get; set; }
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

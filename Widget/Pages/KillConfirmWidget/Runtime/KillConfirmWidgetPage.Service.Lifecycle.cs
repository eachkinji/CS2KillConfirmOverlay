using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using KillConfirmGameBar.Services;
using Windows.ApplicationModel;
using Windows.Data.Json;
using Windows.Foundation;
using Windows.Foundation.Metadata;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.Web.Http;

namespace KillConfirmGameBar
{
    public sealed partial class KillConfirmWidgetPage
    {

        private static async Task<ServiceHealthCheckResult> WaitForServiceReadyAsync()
        {
            App.Log("WaitForServiceReadyAsync: polling for service health.");
            DateTimeOffset deadline = DateTimeOffset.UtcNow + ServiceStartupTimeout;
            ServiceHealthCheckResult latest = null;
            while (DateTimeOffset.UtcNow < deadline)
            {
                latest = await CheckServiceHealthAsync();
                if (latest.IsHealthy)
                {
                    App.Log("WaitForServiceReadyAsync: service became healthy.");
                    return latest;
                }

                await Task.Delay(ServiceStartupPollInterval);
            }

            latest = await CheckServiceHealthAsync();
            App.Log("WaitForServiceReadyAsync: timeout reached. final health=" + latest.IsHealthy);
            if (!latest.IsHealthy && latest.Diagnostic?.Code == "SVC-07")
            {
                latest.Diagnostic = CreateServiceDiagnostic("SVC-04", "ServiceDiagStartupTimeout", latest.Diagnostic.TechnicalDetail);
            }
            return latest;
        }

        private async Task ShowServiceStartupFailureAsync(ServiceDiagnosticInfo fallback)
        {
            ServiceDiagnosticInfo diagnostic = await ResolveServiceFailureAsync(fallback);
            ShowServiceDiagnostic(diagnostic);
        }

        private void ShowServiceDiagnostic(ServiceDiagnosticInfo diagnostic)
        {
            _currentServiceDiagnostic = diagnostic ?? CreateServiceDiagnostic("SVC-07", "ServiceDiagConnectionFailed");
            string text = FormatServiceDiagnostic(_currentServiceDiagnostic);
            StatusDetailsSection.ServiceDiagnosticText.Text = text;
            StatusDetailsSection.ServiceDiagnosticRow.Visibility = Visibility.Visible;
            StatusDetailsSection.FreePortButton.Visibility = _currentServiceDiagnostic.CanFreePort ? Visibility.Visible : Visibility.Collapsed;
            ToolTipService.SetToolTip(StatusDetailsSection.ServiceDiagnosticText, text);
            SetNamedToolTip(HeaderStatusSection.ConnectionStatusBadge, LocalizationManager.Text("ServiceStatusTitle"), text);
            UpdateStatusDetailRowVisibility();
            RefreshStatusHint(false);
            App.Log("Service diagnostic shown: " + text);
        }

        private void HideServiceDiagnostic()
        {
            _currentServiceDiagnostic = null;
            StatusDetailsSection.ServiceDiagnosticRow.Visibility = Visibility.Collapsed;
            StatusDetailsSection.FreePortButton.Visibility = Visibility.Collapsed;
            ToolTipService.SetToolTip(StatusDetailsSection.ServiceDiagnosticText, null);
            UpdateStatusDetailRowVisibility();
        }

        private static ServiceDiagnosticInfo CreateServiceDiagnostic(
            string code,
            string messageKey,
            string technicalDetail = null,
            bool canFreePort = false)
        {
            return new ServiceDiagnosticInfo
            {
                Code = code,
                MessageKey = messageKey,
                TechnicalDetail = SanitizeDiagnosticDetail(technicalDetail),
                CanFreePort = canFreePort
            };
        }

        private static string FormatServiceDiagnostic(ServiceDiagnosticInfo diagnostic)
        {
            if (diagnostic == null)
            {
                return LocalizationManager.Text("ServiceOffline");
            }

            string message = LocalizationManager.Text(diagnostic.MessageKey);
            return string.IsNullOrWhiteSpace(diagnostic.TechnicalDetail)
                ? diagnostic.Code + ": " + message
                : diagnostic.Code + ": " + message + " (" + diagnostic.TechnicalDetail + ")";
        }

        private static async Task<ServiceDiagnosticInfo> ResolveServiceFailureAsync(ServiceDiagnosticInfo fallback)
        {
            string serviceLog = GetCurrentLogSession(
                await TryReadLocalLogAsync("service.log"),
                "service starting");
            string bootstrapLog = GetCurrentLogSession(
                await TryReadLocalLogAsync("bootstrap.log"),
                "process entry");
            string combined = (serviceLog + "\n" + bootstrapLog).ToLowerInvariant();
            string lastError = FindLastErrorLine(serviceLog + "\n" + bootstrapLog);

            if (combined.Contains("os error 10048") || combined.Contains("address already in use"))
            {
                string portOwner = FindTaggedLogDetail(
                    serviceLog + "\n" + bootstrapLog,
                    "port 10087 owner: ");
                return CreateServiceDiagnostic("SVC-01", "ServiceDiagPortInUse", portOwner ?? lastError, true);
            }

            if (combined.Contains("os error 10013") || combined.Contains("access forbidden"))
            {
                return CreateServiceDiagnostic("SVC-02", "ServiceDiagPortBlocked", lastError);
            }

            if (combined.Contains("control authentication") || combined.Contains("control-token"))
            {
                return CreateServiceDiagnostic("SVC-05", "ServiceDiagAuthFailed", lastError);
            }

            if (combined.Contains("fatal error"))
            {
                return CreateServiceDiagnostic("SVC-06", "ServiceDiagCrashed", lastError);
            }

            return fallback ?? CreateServiceDiagnostic("SVC-07", "ServiceDiagConnectionFailed");
        }

        private static string GetCurrentLogSession(string log, string marker)
        {
            if (string.IsNullOrEmpty(log))
            {
                return string.Empty;
            }

            int index = log.LastIndexOf(marker, StringComparison.OrdinalIgnoreCase);
            return index >= 0 ? log.Substring(index) : log;
        }

        private static string FindTaggedLogDetail(string log, string tag)
        {
            if (string.IsNullOrWhiteSpace(log) || string.IsNullOrWhiteSpace(tag))
            {
                return null;
            }

            int index = log.LastIndexOf(tag, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                return null;
            }

            int start = index + tag.Length;
            int end = log.IndexOfAny(new[] { '\r', '\n' }, start);
            return end >= 0 ? log.Substring(start, end - start).Trim() : log.Substring(start).Trim();
        }

        private static string FindLastErrorLine(string log)
        {
            if (string.IsNullOrWhiteSpace(log))
            {
                return null;
            }

            string[] lines = log.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            for (int index = lines.Length - 1; index >= 0; index--)
            {
                string lower = lines[index].ToLowerInvariant();
                if (lower.Contains("fatal") || lower.Contains("error") || lower.Contains("failed"))
                {
                    return lines[index];
                }
            }

            return null;
        }

        private static string SanitizeDiagnosticDetail(string detail)
        {
            if (string.IsNullOrWhiteSpace(detail))
            {
                return null;
            }

            string compact = detail.Replace("\r", " ").Replace("\n", " ").Trim();
            return compact.Length <= 180 ? compact : compact.Substring(0, 177) + "...";
        }

        private static async Task<string> TryReadLocalLogAsync(string fileName)
        {
            try
            {
                StorageFile file = await ApplicationData.Current.LocalFolder.GetFileAsync(fileName);
                return await FileIO.ReadTextAsync(file);
            }
            catch
            {
                return string.Empty;
            }
        }

        private static async Task<ServiceHealthCheckResult> CheckServiceHealthAsync(bool retryAuthentication = true)
        {
            try
            {
                using (var client = await LocalServiceAuth.CreateHttpClientAsync())
                using (HttpResponseMessage response = await client.GetAsync(ServiceHealthUri))
                {
                    if (response.IsSuccessStatusCode)
                    {
                        return new ServiceHealthCheckResult { IsHealthy = true };
                    }

                    int statusCode = (int)response.StatusCode;
                    if (statusCode == 401 || statusCode == 403)
                    {
                        if (retryAuthentication)
                        {
                            LocalServiceAuth.InvalidateCachedToken();
                            return await CheckServiceHealthAsync(false);
                        }

                        return new ServiceHealthCheckResult
                        {
                            Diagnostic = CreateServiceDiagnostic("SVC-05", "ServiceDiagAuthFailed", "HTTP " + statusCode)
                        };
                    }

                    return new ServiceHealthCheckResult
                    {
                        Diagnostic = CreateServiceDiagnostic("SVC-07", "ServiceDiagConnectionFailed", "HTTP " + statusCode)
                    };
                }
            }
            catch (Exception ex)
            {
                string detail = ex.GetType().Name + " 0x" + ex.HResult.ToString("X8") + ": " + ex.Message;
                bool authenticationFailure = ex.Message?.IndexOf("authentication", StringComparison.OrdinalIgnoreCase) >= 0
                    || ex.Message?.IndexOf("token", StringComparison.OrdinalIgnoreCase) >= 0;
                return new ServiceHealthCheckResult
                {
                    Diagnostic = authenticationFailure
                        ? CreateServiceDiagnostic("SVC-05", "ServiceDiagAuthFailed", detail)
                        : CreateServiceDiagnostic("SVC-07", "ServiceDiagConnectionFailed", detail)
                };
            }
        }

        private async Task RefreshGsiStatusAsync()
        {
            _gsiStatusCheckPending = true;
            _lastGsiStatusCheck = DateTimeOffset.Now;

            try
            {
                GsiStatusSnapshot snapshot = await GsiStatusMonitor.Instance.RefreshAsync();
                _lastGsiPosts = snapshot.Posts;
                _lastGsiParseErrors = snapshot.ParseErrors;
                UpdateGsiStatus(
                    snapshot.ServiceReachable,
                    snapshot.IsGreen,
                    snapshot.Posts,
                    snapshot.LastPostAgeMs,
                    snapshot.ParseErrors);
            }
            catch (Exception)
            {
                UpdateGsiStatus(false, false, 0, null, 0);
            }
            finally
            {
                _gsiStatusCheckPending = false;
            }
        }

        private static double? TryGetJsonNumber(JsonObject json, string key)
        {
            if (!json.ContainsKey(key))
            {
                return null;
            }

            IJsonValue value = json.GetNamedValue(key);
            return value.ValueType == JsonValueType.Number
                ? value.GetNumber()
                : (double?)null;
        }

        internal async Task ShutdownCompanionAsync()
        {
            if (_shutdownRequested)
            {
                return;
            }

            _shutdownRequested = true;
            StopKillEventClient();
            await RequestServiceShutdownAsync();
        }

        private async void OnServiceConnectionFailure(object sender, ServiceConnectionFailureEventArgs failure)
        {
            if (!_isPageActive || _shutdownRequested || failure == null)
            {
                return;
            }

            // KillEventClient retries the HTTP poll every second. Collapse those
            // failures into one recovery attempt so an exited companion is
            // relaunched instead of leaving the widget permanently at SVC-07.
            if (System.Threading.Interlocked.CompareExchange(ref _serviceRecoveryPending, 1, 0) != 0)
            {
                return;
            }

            try
            {
                App.Log(
                    "Service event connection failure: kind=" + failure.Kind
                    + ", hresult=0x" + failure.HResult.ToString("X8")
                    + ", detail=" + failure.Detail);

                await Task.Delay(300);
                if (!_isPageActive
                    || _shutdownRequested
                    || _serviceConnectionState == KillEventConnectionState.Connected)
                {
                    return;
                }

                ServiceHealthCheckResult health = await CheckServiceHealthAsync();
                if (!health.IsHealthy && _isPageActive && !_shutdownRequested)
                {
                    App.Log("Event connection lost with no healthy companion; attempting automatic recovery.");
                    await EnsureServiceAvailableAsync();
                    health = await CheckServiceHealthAsync();
                }

                if (health.IsHealthy)
                {
                    UpdateConnectionState(
                        _eventClient?.ConnectionState == KillEventConnectionState.Connected
                            ? KillEventConnectionState.Connected
                            : KillEventConnectionState.Connecting);
                    return;
                }

                ServiceDiagnosticInfo fallback;
                if (failure.Kind == ServiceConnectionFailureKind.AuthenticationFailed)
                {
                    fallback = CreateServiceDiagnostic("SVC-05", "ServiceDiagAuthFailed", failure.Detail);
                }
                else if (failure.Kind == ServiceConnectionFailureKind.ConnectionClosed)
                {
                    fallback = CreateServiceDiagnostic("SVC-07", "ServiceDiagConnectionClosed", failure.Detail);
                }
                else if (failure.Kind == ServiceConnectionFailureKind.MessageReadFailed)
                {
                    fallback = CreateServiceDiagnostic("SVC-07", "ServiceDiagEventDataInvalid", failure.Detail);
                }
                else
                {
                    string detail = "0x" + failure.HResult.ToString("X8")
                        + (string.IsNullOrWhiteSpace(failure.Detail) ? string.Empty : ": " + failure.Detail);
                    fallback = CreateServiceDiagnostic("SVC-07", "ServiceDiagConnectionFailed", detail);
                }

                ServiceDiagnosticInfo diagnostic = await ResolveServiceFailureAsync(health.Diagnostic ?? fallback);
                if (_serviceConnectionState != KillEventConnectionState.Connected)
                {
                    ShowServiceDiagnostic(diagnostic);
                }
            }
            finally
            {
                System.Threading.Interlocked.Exchange(ref _serviceRecoveryPending, 0);
            }
        }

        private void OnEventsDropped(object sender, EventsDroppedEventArgs e)
        {
            _ = Dispatcher.RunAsync(
                Windows.UI.Core.CoreDispatcherPriority.Normal,
                () => ShowTransientStatusHint(
                    LocalizationManager.Text("EventsDroppedHint"),
                    Windows.UI.Color.FromArgb(255, 180, 90, 0)));
        }

        private void StopKillEventClient()
        {
            if (_eventClient == null)
            {
                return;
            }

            _eventClient.KillReceived -= OnKillReceived;
            _eventClient.ConnectionStateChanged -= OnConnectionStateChanged;
            _eventClient.ConnectionFailure -= OnServiceConnectionFailure;
            _eventClient.EventsDropped -= OnEventsDropped;
            _eventClient.Dispose();
            _eventClient = null;
        }

        internal static async Task RequestServiceShutdownAsync()
        {
            try
            {
                // Normal UI shutdown releases only this process's lease. The
                // service exits when the last registered UI process closes.
                if (await ServiceLauncher.UnregisterCurrentProcessAsync())
                {
                    return;
                }

                // Compatibility fallback for an older companion that does not
                // expose the process-lifetime endpoints yet.
                using (var client = await LocalServiceAuth.CreateHttpClientAsync())
                using (var content = new HttpStringContent(string.Empty, UnicodeEncoding.Utf8, "text/plain"))
                {
                    await client.PostAsync(ServiceShutdownUri, content);
                }
            }
            catch (Exception ex)
            {
                App.Log("Service shutdown request failed: " + ex.Message);
            }
        }
    }
}

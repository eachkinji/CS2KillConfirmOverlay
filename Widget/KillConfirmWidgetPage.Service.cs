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
        private sealed class ServiceDiagnosticInfo
        {
            public string Code { get; set; }
            public string MessageKey { get; set; }
            public string TechnicalDetail { get; set; }
            public bool CanFreePort { get; set; }
        }

        private sealed class ServiceHealthCheckResult
        {
            public bool IsHealthy { get; set; }
            public ServiceDiagnosticInfo Diagnostic { get; set; }
        }

        private ServiceDiagnosticInfo _currentServiceDiagnostic;
        private void ConfigureWidgetCapabilities()
        {
            if (_widget == null)
            {
                return;
            }

            try
            {
                _widget.MinWindowSize = MinWidgetSize;
                _widget.MaxWindowSize = MaxWidgetSize;
                _widget.HorizontalResizeSupported = true;
                _widget.VerticalResizeSupported = true;
            }
            catch (Exception)
            {
            }
        }

        private void StartKillEventClient()
        {
            if (_eventClient != null)
            {
                return;
            }

            _eventClient = new KillEventClient(Dispatcher);
            _eventClient.KillReceived += OnKillReceived;
            _eventClient.ConnectionStateChanged += OnConnectionStateChanged;
            _eventClient.ConnectionFailure += OnServiceConnectionFailure;
            _eventClient.EventsDropped += OnEventsDropped;
            _eventClient.Start();
        }

        private void UpdateConnectionStateFromHealth(bool serviceHealthy)
        {
            if (!serviceHealthy)
            {
                UpdateConnectionState(KillEventConnectionState.Disconnected);
                return;
            }

            // A successful /health request proves only that the service process is alive.
            // The SVC indicator must follow the event poll, otherwise a hung /events request
            // can remain green while no kill effects are reaching the widget.
            UpdateConnectionState(
                _eventClient == null
                    ? KillEventConnectionState.Connecting
                    : _eventClient.ConnectionState);
        }

        private async Task EnsureServiceAvailableAsync()
        {
            App.Log("EnsureServiceAvailableAsync: entered. pageActive=" + _isPageActive);
            if (!_isPageActive)
            {
                App.Log("EnsureServiceAvailableAsync: skipped because page is inactive.");
                return;
            }

            ServiceHealthCheckResult initialHealth = await CheckServiceHealthAsync();
            App.Log("EnsureServiceAvailableAsync: initial health=" + initialHealth.IsHealthy);
            if (initialHealth.IsHealthy)
            {
                UpdateConnectionStateFromHealth(true);
                await SyncServiceSettingsAsync();
                return;
            }

            await ServiceStartupGate.WaitAsync();
            try
            {
                App.Log("EnsureServiceAvailableAsync: entered startup gate.");
                if (!_isPageActive)
                {
                    App.Log("EnsureServiceAvailableAsync: aborted inside gate because page is inactive.");
                    return;
                }

                ServiceHealthCheckResult gatedHealth = await CheckServiceHealthAsync();
                App.Log("EnsureServiceAvailableAsync: gated health=" + gatedHealth.IsHealthy);
                if (gatedHealth.IsHealthy)
                {
                    UpdateConnectionStateFromHealth(true);
                    await SyncServiceSettingsAsync();
                    return;
                }

                if (gatedHealth.Diagnostic?.Code == "SVC-05")
                {
                    UpdateConnectionState(KillEventConnectionState.Disconnected);
                    ShowServiceDiagnostic(gatedHealth.Diagnostic);
                    return;
                }

                UpdateConnectionState(KillEventConnectionState.Connecting);
                App.Log("EnsureServiceAvailableAsync: attempting packaged service launch.");

                bool launched = await TryLaunchPackagedServiceAsync();
                App.Log("EnsureServiceAvailableAsync: launch result=" + launched);
                if (!launched)
                {
                    UpdateConnectionState(KillEventConnectionState.Disconnected);
                    await ShowServiceStartupFailureAsync(CreateServiceDiagnostic("SVC-03", "ServiceDiagLaunchFailed"));
                    return;
                }

                ServiceHealthCheckResult ready = await WaitForServiceReadyAsync();
                App.Log("EnsureServiceAvailableAsync: service ready after launch=" + ready.IsHealthy);
                if (_isPageActive)
                {
                    UpdateConnectionStateFromHealth(ready.IsHealthy);
                }

                if (ready.IsHealthy)
                {
                    await SyncServiceSettingsAsync();
                }
                else
                {
                    await ShowServiceStartupFailureAsync(
                        ready.Diagnostic ?? CreateServiceDiagnostic("SVC-04", "ServiceDiagStartupTimeout"));
                }
            }
            finally
            {
                App.Log("EnsureServiceAvailableAsync: leaving startup gate.");
                ServiceStartupGate.Release();
            }
        }

        private async Task SyncServiceSettingsAsync()
        {
            await SyncDeveloperModeAsync();
            await SyncSelectedVoicePackAsync();
            await SyncMoneyRewardModeAsync();
            await SyncAudioDeviceAsync();
            await SyncCrossfireGameplaySettingsAsync();
            await SyncCsolGameplaySettingsAsync();
            await SyncSharedStreakSettingsAsync();
            await SyncSpectatedKillEffectsAsync();
            await SyncGsiGameVersionAsync();
        }

        // The service always starts on the system default device, so re-apply the
        // user's saved device whenever the widget (re)connects; otherwise the
        // 2-second default-device watcher silently drops their selection.
        private async Task SyncAudioDeviceAsync()
        {
            string saved = ApplicationData.Current.LocalSettings.Values[AudioDeviceSettingKey] as string;
            if (string.IsNullOrWhiteSpace(saved)
                || string.Equals(saved, "default", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            try
            {
                var request = new JsonObject
                {
                    ["device"] = JsonValue.CreateStringValue(saved)
                };
                using (var client = await LocalServiceAuth.CreateHttpClientAsync())
                using (var content = new HttpStringContent(
                    request.Stringify(),
                    UnicodeEncoding.Utf8,
                    "application/json"))
                using (HttpResponseMessage response = await client.PostAsync(AudioDeviceUri, content))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        App.Log("Set audio device failed: status=" + response.StatusCode);
                    }
                }
            }
            catch (Exception ex)
            {
                App.Log("Set audio device failed: " + ex);
            }
        }

        private async Task SyncGsiGameVersionAsync()
        {
            try
            {
                await GsiGameVersionSettingsStore.SyncAsync();
            }
            catch (Exception ex)
            {
                App.Log("Set GSI game version failed: " + ex);
            }
        }

        private async Task CheckServerHealthAsync()
        {
            App.Log("CheckServerHealthAsync: manual health check requested.");
            UpdateConnectionState(KillEventConnectionState.Connecting);

            ServiceHealthCheckResult health = await CheckServiceHealthAsync();
            App.Log("CheckServerHealthAsync: health result=" + health.IsHealthy);
            UpdateConnectionStateFromHealth(health.IsHealthy);

            if (health.IsHealthy)
            {
                await SyncServiceSettingsAsync();
            }
            else
            {
                await ShowServiceStartupFailureAsync(
                    health.Diagnostic ?? CreateServiceDiagnostic("SVC-07", "ServiceDiagConnectionFailed"));
            }
        }

        private static async Task<bool> TryLaunchPackagedServiceAsync()
        {
            string parameterGroupId = DeveloperModeSettingsStore.IsEnabled
                ? PackagedServiceDeveloperParameterGroupId
                : PackagedServiceParameterGroupId;
            return await TryLaunchFullTrustHelperAsync(parameterGroupId);
        }

        private static async Task SyncDeveloperModeAsync()
        {
            try
            {
                await DeveloperModeSettingsStore.SyncToServiceAsync();
            }
            catch (Exception ex)
            {
                App.Log("Failed to sync developer mode: " + ex);
            }
        }

        internal static async Task<bool> TryLaunchFullTrustHelperAsync(string parameterGroupId)
        {
            try
            {
                App.Log("Launching full-trust helper. group=" + parameterGroupId);
                if (!ApiInformation.IsTypePresent("Windows.ApplicationModel.FullTrustProcessLauncher"))
                {
                    App.Log("FullTrustProcessLauncher is not available on this Windows build.");
                    return false;
                }

                IAsyncAction launchAction = LaunchFullTrustProcessForCurrentAppWithParameters(parameterGroupId);
                if (launchAction == null)
                {
                    App.Log("FullTrustProcessLauncher returned no launch action.");
                    return false;
                }

                await launchAction;
                App.Log("Full-trust helper launch call returned without exception. group=" + parameterGroupId);
                return true;
            }
            catch (Exception ex)
            {
                App.Log(
                    "Failed to launch packaged service: type=" + ex.GetType().FullName
                    + ", hresult=0x" + ex.HResult.ToString("X8")
                    + ", message=" + ex.Message
                    + ", detail=" + ex);
                return false;
            }
        }

        private static IAsyncAction LaunchFullTrustProcessForCurrentAppWithParameters(string parameterGroupId)
        {
            IntPtr runtimeClassName = IntPtr.Zero;
            IFullTrustProcessLauncherStatics launcherStatics = null;

            try
            {
                int hr = WindowsCreateString(
                    FullTrustProcessLauncherRuntimeClass,
                    FullTrustProcessLauncherRuntimeClass.Length,
                    out runtimeClassName);
                Marshal.ThrowExceptionForHR(hr);

                System.Guid iid = FullTrustProcessLauncherStaticsGuid;
                hr = RoGetActivationFactory(runtimeClassName, ref iid, out launcherStatics);
                Marshal.ThrowExceptionForHR(hr);

                return launcherStatics.LaunchFullTrustProcessForCurrentAppWithParametersAsync(parameterGroupId);
            }
            finally
            {
                if (runtimeClassName != IntPtr.Zero)
                {
                    WindowsDeleteString(runtimeClassName);
                }

                if (launcherStatics != null)
                {
                    Marshal.ReleaseComObject(launcherStatics);
                }
            }
        }

        [DllImport("api-ms-win-core-winrt-string-l1-1-0.dll", ExactSpelling = true)]
        private static extern int WindowsCreateString(
            [MarshalAs(UnmanagedType.LPWStr)] string sourceString,
            int length,
            out IntPtr hstring);

        [DllImport("api-ms-win-core-winrt-string-l1-1-0.dll", ExactSpelling = true)]
        private static extern int WindowsDeleteString(IntPtr hstring);

        [DllImport("api-ms-win-core-winrt-l1-1-0.dll", ExactSpelling = true)]
        private static extern int RoGetActivationFactory(
            IntPtr activatableClassId,
            ref System.Guid iid,
            [MarshalAs(UnmanagedType.Interface)] out IFullTrustProcessLauncherStatics factory);

        [ComImport]
        [System.Runtime.InteropServices.Guid("D784837F-1100-3C6B-A455-F6262CC331B6")]
        [InterfaceType(ComInterfaceType.InterfaceIsIInspectable)]
        private interface IFullTrustProcessLauncherStatics
        {
            [return: MarshalAs(UnmanagedType.Interface)]
            IAsyncAction LaunchFullTrustProcessForCurrentAppAsync();

            [return: MarshalAs(UnmanagedType.Interface)]
            IAsyncAction LaunchFullTrustProcessForCurrentAppWithParametersAsync(
                [MarshalAs(UnmanagedType.HString)] string parameterGroupId);

            [return: MarshalAs(UnmanagedType.Interface)]
            IAsyncAction LaunchFullTrustProcessForAppAsync(
                [MarshalAs(UnmanagedType.HString)] string fullTrustPackageRelativeAppId);

            [return: MarshalAs(UnmanagedType.Interface)]
            IAsyncAction LaunchFullTrustProcessForAppWithParametersAsync(
                [MarshalAs(UnmanagedType.HString)] string fullTrustPackageRelativeAppId,
                [MarshalAs(UnmanagedType.HString)] string parameterGroupId);
        }

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
            ServiceDiagnosticText.Text = text;
            ServiceDiagnosticRow.Visibility = Visibility.Visible;
            FreePortButton.Visibility = _currentServiceDiagnostic.CanFreePort ? Visibility.Visible : Visibility.Collapsed;
            ToolTipService.SetToolTip(ServiceDiagnosticText, text);
            SetNamedToolTip(ConnectionStatusBadge, LocalizationManager.Text("ServiceStatusTitle"), text);
            UpdateStatusDetailRowVisibility();
            RefreshStatusHint(false);
            App.Log("Service diagnostic shown: " + text);
        }

        private void HideServiceDiagnostic()
        {
            _currentServiceDiagnostic = null;
            ServiceDiagnosticRow.Visibility = Visibility.Collapsed;
            FreePortButton.Visibility = Visibility.Collapsed;
            ToolTipService.SetToolTip(ServiceDiagnosticText, null);
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
                using (var client = await LocalServiceAuth.CreateHttpClientAsync())
                using (HttpResponseMessage response = await client.GetAsync(GsiStatusUri))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        UpdateGsiStatus(false, false, 0, null, 0);
                        return;
                    }

                    string responseText = await response.Content.ReadAsStringAsync();
                    JsonObject json = JsonObject.Parse(responseText);
                    double posts = json.GetNamedNumber("posts", 0);
                    double parseErrors = json.GetNamedNumber("parse_errors", 0);
                    double? ageMs = TryGetJsonNumber(json, "last_post_age_ms");
                    bool recentlySeen = posts > 0 && ageMs.HasValue && ageMs.Value <= RecentGsiAgeMs;
                    _lastGsiPosts = posts;
                    _lastGsiParseErrors = parseErrors;
                    UpdateGsiStatus(true, recentlySeen, posts, ageMs, parseErrors);
                }
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
            if (!_isPageActive || failure == null)
            {
                return;
            }

            App.Log(
                "Service event connection failure: kind=" + failure.Kind
                + ", hresult=0x" + failure.HResult.ToString("X8")
                + ", detail=" + failure.Detail);

            await Task.Delay(300);
            if (_serviceConnectionState == KillEventConnectionState.Connected)
            {
                return;
            }

            ServiceHealthCheckResult health = await CheckServiceHealthAsync();
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

            ServiceDiagnosticInfo diagnostic = health.IsHealthy
                ? fallback
                : await ResolveServiceFailureAsync(health.Diagnostic ?? fallback);
            if (_serviceConnectionState != KillEventConnectionState.Connected)
            {
                ShowServiceDiagnostic(diagnostic);
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

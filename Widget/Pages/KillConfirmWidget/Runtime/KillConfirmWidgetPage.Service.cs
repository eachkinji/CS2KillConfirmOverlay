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
        private int _serviceRecoveryPending;
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
                _widget.PinningSupported = true;
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

        private async Task<bool> WaitForKillEventConnectionAsync(TimeSpan timeout)
        {
            if (!_isPageActive || _shutdownRequested)
            {
                return false;
            }

            StartKillEventClient();
            DateTimeOffset deadline = DateTimeOffset.UtcNow + timeout;
            while (_isPageActive
                && !_shutdownRequested
                && DateTimeOffset.UtcNow < deadline)
            {
                if (_eventClient?.ConnectionState == KillEventConnectionState.Connected)
                {
                    return true;
                }

                await Task.Delay(50);
            }

            return _eventClient?.ConnectionState == KillEventConnectionState.Connected;
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
            if (!await ServiceLauncher.RegisterCurrentProcessAsync())
            {
                App.Log("Failed to register UI process with companion lifetime monitor.");
            }
            await SyncDeveloperModeAsync();
            await SyncSelectedVoicePackAsync();
            await SyncMoneyRewardModeAsync();
            await SyncAudioDeviceAsync();
            await SyncCrossfireGameplaySettingsAsync();
            await SyncCsolGameplaySettingsAsync();
            await SyncDagoujiaoSettingsAsync();
            await SyncBombAudioSettingsAsync();
            await SyncSharedStreakSettingsAsync();
            await SyncSpectatedKillEffectsAsync();
            await SyncGsiGameVersionAsync();
            await SyncProcessPrioritySettingsAsync();
            await SyncInterruptPreviousKillAudioAsync();
            await SyncStreakGainSettingsAsync();
        }

        private static async Task SyncStreakGainSettingsAsync()
        {
            try
            {
                await StreakGainSettingsStore.SyncAsync();
            }
            catch (Exception ex)
            {
                App.Log("Sync streak gain settings failed: " + ex);
            }
        }

        private static async Task SyncInterruptPreviousKillAudioAsync()
        {
            try
            {
                await InterruptPreviousKillAudioSettingsStore.SyncAsync();
            }
            catch (Exception ex)
            {
                App.Log("Sync interrupt previous kill audio failed: " + ex);
            }
        }

        private static async Task SyncProcessPrioritySettingsAsync()
        {
            try
            {
                await ProcessPrioritySettingsStore.ApplyPersistedAsync();
            }
            catch (Exception ex)
            {
                App.Log("Apply persisted process priorities failed: " + ex);
            }
        }

        private static async Task SyncBombAudioSettingsAsync()
        {
            try
            {
                await BombAudioSettingsStore.SyncAsync();
            }
            catch (Exception ex)
            {
                App.Log("Set bomb audio settings failed: " + ex);
            }
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
            int port = PortSettingsStore.CurrentPort;
            return await ServiceLauncher.LaunchAsync(port);
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
    }
}

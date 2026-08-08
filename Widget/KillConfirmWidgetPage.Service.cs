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
            _eventClient.Start();
        }

        private async Task EnsureServiceAvailableAsync()
        {
            App.Log("EnsureServiceAvailableAsync: entered. pageActive=" + _isPageActive);
            if (!_isPageActive)
            {
                App.Log("EnsureServiceAvailableAsync: skipped because page is inactive.");
                return;
            }

            bool initialHealth = await IsServiceHealthyAsync();
            App.Log("EnsureServiceAvailableAsync: initial health=" + initialHealth);
            if (initialHealth)
            {
                if (_isPageActive)
                {
                    UpdateConnectionState(KillEventConnectionState.Connected);
                }

                await SyncSelectedVoicePackAsync();
                await SyncMoneyRewardModeAsync();
                await SyncCrossfireGameplaySettingsAsync();
                await SyncSharedStreakSettingsAsync();
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

                bool gatedHealth = await IsServiceHealthyAsync();
                App.Log("EnsureServiceAvailableAsync: gated health=" + gatedHealth);
                if (gatedHealth)
                {
                    UpdateConnectionState(KillEventConnectionState.Connected);
                    await SyncSelectedVoicePackAsync();
                    await SyncMoneyRewardModeAsync();
                    await SyncCrossfireGameplaySettingsAsync();
                    await SyncSharedStreakSettingsAsync();
                    return;
                }

                UpdateConnectionState(KillEventConnectionState.Connecting);
                App.Log("EnsureServiceAvailableAsync: attempting packaged service launch.");

                bool launched = await TryLaunchPackagedServiceAsync();
                App.Log("EnsureServiceAvailableAsync: launch result=" + launched);
                if (!launched)
                {
                    UpdateConnectionState(KillEventConnectionState.Disconnected);
                    await ShowServiceStartupFailureAsync();
                    return;
                }

                bool ready = await WaitForServiceReadyAsync();
                App.Log("EnsureServiceAvailableAsync: service ready after launch=" + ready);
                if (_isPageActive)
                {
                    UpdateConnectionState(ready
                        ? KillEventConnectionState.Connected
                        : KillEventConnectionState.Disconnected);
                }

                if (ready)
                {
                    HideServiceDiagnostic();
                    await SyncSelectedVoicePackAsync();
                    await SyncMoneyRewardModeAsync();
                    await SyncCrossfireGameplaySettingsAsync();
                    await SyncSharedStreakSettingsAsync();
                }
                else
                {
                    await ShowServiceStartupFailureAsync();
                }
            }
            finally
            {
                App.Log("EnsureServiceAvailableAsync: leaving startup gate.");
                ServiceStartupGate.Release();
            }
        }

        private async Task CheckServerHealthAsync()
        {
            App.Log("CheckServerHealthAsync: manual health check requested.");
            UpdateConnectionState(KillEventConnectionState.Connecting);

            bool isHealthy = await IsServiceHealthyAsync();
            App.Log("CheckServerHealthAsync: health result=" + isHealthy);
            UpdateConnectionState(isHealthy
                ? KillEventConnectionState.Connected
                : KillEventConnectionState.Disconnected);

            if (isHealthy)
            {
                HideServiceDiagnostic();
                await SyncSelectedVoicePackAsync();
                await SyncMoneyRewardModeAsync();
                await SyncCrossfireGameplaySettingsAsync();
                await SyncSharedStreakSettingsAsync();
            }
            else
            {
                await ShowServiceStartupFailureAsync();
            }
        }

        private static async Task<bool> TryLaunchPackagedServiceAsync()
        {
            return await TryLaunchFullTrustHelperAsync(PackagedServiceParameterGroupId);
        }

        private static async Task<bool> TryLaunchFullTrustHelperAsync(string parameterGroupId)
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

        private static async Task<bool> WaitForServiceReadyAsync()
        {
            App.Log("WaitForServiceReadyAsync: polling for service health.");
            DateTimeOffset deadline = DateTimeOffset.UtcNow + ServiceStartupTimeout;
            while (DateTimeOffset.UtcNow < deadline)
            {
                if (await IsServiceHealthyAsync())
                {
                    App.Log("WaitForServiceReadyAsync: service became healthy.");
                    return true;
                }

                await Task.Delay(ServiceStartupPollInterval);
            }

            bool finalHealth = await IsServiceHealthyAsync();
            App.Log("WaitForServiceReadyAsync: timeout reached. final health=" + finalHealth);
            return finalHealth;
        }

        private async Task ShowServiceStartupFailureAsync()
        {
            string hint = await ResolveServiceFailureHintAsync();
            ServiceDiagnosticText.Text = hint;
            ServiceDiagnosticRow.Visibility = Visibility.Visible;
            ToolTipService.SetToolTip(ServiceDiagnosticText, hint);
            UpdateStatusDetailRowVisibility();
            App.Log("Service diagnostic shown: " + hint);
        }

        private void HideServiceDiagnostic()
        {
            ServiceDiagnosticRow.Visibility = Visibility.Collapsed;
            ToolTipService.SetToolTip(ServiceDiagnosticText, null);
            UpdateStatusDetailRowVisibility();
        }

        private static async Task<string> ResolveServiceFailureHintAsync()
        {
            string serviceLog = await TryReadLocalLogAsync("service.log");
            string bootstrapLog = await TryReadLocalLogAsync("bootstrap.log");
            string combined = (serviceLog + "\n" + bootstrapLog).ToLowerInvariant();

            if (combined.Contains("os error 10048"))
            {
                return LocalizationManager.Text("ServicePortInUseHint");
            }

            if (combined.Contains("os error 10013"))
            {
                return LocalizationManager.Text("ServicePortBlockedHint");
            }

            if (combined.Contains("fatal error"))
            {
                return LocalizationManager.Text("ServiceFailedSeeLogs");
            }

            return LocalizationManager.Text("ServiceFailedGeneric");
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

        private static async Task<bool> IsServiceHealthyAsync()
        {
            try
            {
                using (var client = await LocalServiceAuth.CreateHttpClientAsync())
                using (HttpResponseMessage response = await client.GetAsync(ServiceHealthUri))
                {
                    return response.IsSuccessStatusCode;
                }
            }
            catch (Exception)
            {
                return false;
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
                        UpdateGsiStatus(false, false, 0, null);
                        return;
                    }

                    string responseText = await response.Content.ReadAsStringAsync();
                    JsonObject json = JsonObject.Parse(responseText);
                    double posts = json.GetNamedNumber("posts", 0);
                    double? ageMs = TryGetJsonNumber(json, "last_post_age_ms");
                    bool recentlySeen = posts > 0 && ageMs.HasValue && ageMs.Value <= RecentGsiAgeMs;
                    UpdateGsiStatus(true, recentlySeen, posts, ageMs);
                }
            }
            catch (Exception)
            {
                UpdateGsiStatus(false, false, 0, null);
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

        private void StopKillEventClient()
        {
            if (_eventClient == null)
            {
                return;
            }

            _eventClient.KillReceived -= OnKillReceived;
            _eventClient.ConnectionStateChanged -= OnConnectionStateChanged;
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

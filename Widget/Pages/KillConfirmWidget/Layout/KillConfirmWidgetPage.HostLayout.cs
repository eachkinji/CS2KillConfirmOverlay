using Microsoft.Gaming.XboxGameBar;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KillConfirmGameBar.Services;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Core;
using Windows.ApplicationModel.DataTransfer;
using Windows.Data.Json;
using Windows.Devices.Input;
using Windows.Foundation;
using Windows.Foundation.Metadata;
using Windows.Storage.AccessCache;
using Windows.Storage.Pickers;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;
using Windows.Web.Http;
using Windows.System;

namespace KillConfirmGameBar
{
    public sealed partial class KillConfirmWidgetPage : Page
    {

        private void LogHostLayoutIfChanged(string reason, Rect coreBounds, Frame frame)
        {
            Rect widgetBounds = new Rect();
            try
            {
                if (_widget != null)
                {
                    widgetBounds = _widget.WindowBounds;
                }
            }
            catch
            {
            }

            string signature = string.Format(
                "widget={0:F2},{1:F2},{2:F2},{3:F2};core={4:F2},{5:F2};frame={6:F2},{7:F2};page={8:F2},{9:F2};root={10:F2},{11:F2};panel={12:F2},{13:F2}",
                widgetBounds.X,
                widgetBounds.Y,
                widgetBounds.Width,
                widgetBounds.Height,
                coreBounds.Width,
                coreBounds.Height,
                frame?.ActualWidth ?? 0,
                frame?.ActualHeight ?? 0,
                ActualWidth,
                ActualHeight,
                LayoutRoot?.ActualWidth ?? 0,
                LayoutRoot?.ActualHeight ?? 0,
                ControlPanel?.ActualWidth ?? 0,
                ControlPanel?.ActualHeight ?? 0);
            if (string.Equals(signature, _lastHostLayoutSignature, StringComparison.Ordinal))
            {
                return;
            }

            _lastHostLayoutSignature = signature;
            App.LogCrash("Host layout [" + reason + "] " + signature);
        }

        private async Task InitializeWidgetLayoutAsync()
        {
            await RestoreFixedPanelBaselineOnceAsync();
            if (!_isPageActive || _widget == null)
            {
                return;
            }

            SynchronizeHostPageLayout("initial");
            RequestFixedWidgetLayoutRefresh();
        }

        private void RequestFixedWidgetLayoutRefresh(string source = "automatic")
        {
            if (_widget == null)
            {
                return;
            }

            int requestVersion = Interlocked.Increment(ref _widgetLayoutRefreshRequestVersion);
            _ = RefreshFixedWidgetLayoutAsync(requestVersion, source);
        }

        private async Task RefreshFixedWidgetLayoutAndCenterAsync(string source)
        {
            if (_widget == null)
            {
                return;
            }

            int requestVersion = Interlocked.Increment(ref _widgetLayoutRefreshRequestVersion);
            await RefreshFixedWidgetLayoutAsync(requestVersion, source);
            if (!_isPageActive || _widget == null || requestVersion != _widgetLayoutRefreshRequestVersion)
            {
                return;
            }

            await CenterWidgetWindowAsync(source);
            SynchronizeHostPageLayout("after-center-" + source);
        }

        private async Task<bool> WaitForHostLayoutSizeAsync(
            XboxGameBarWidget widget,
            Size expected,
            int requestVersion,
            int timeoutMs = 900)
        {
            var clock = Stopwatch.StartNew();
            while (clock.ElapsedMilliseconds < timeoutMs)
            {
                if (!_isPageActive || _widget == null || !ReferenceEquals(widget, _widget)
                    || requestVersion != _widgetLayoutRefreshRequestVersion)
                {
                    return false;
                }

                SynchronizeHostPageLayout("wait-host-size");
                Rect widgetBounds = widget.WindowBounds;
                Rect coreBounds = Window.Current.Bounds;
                Frame frame = Window.Current.Content as Frame;
                bool widgetReady = IsHostDimensionReady(widgetBounds.Width, expected.Width)
                    && IsHostDimensionReady(widgetBounds.Height, expected.Height);
                bool coreReady = IsHostDimensionReady(coreBounds.Width, expected.Width)
                    && IsHostDimensionReady(coreBounds.Height, expected.Height);
                bool frameReady = frame != null
                    && IsHostDimensionReady(frame.ActualWidth, expected.Width)
                    && IsHostDimensionReady(frame.ActualHeight, expected.Height);
                bool pageReady = IsHostDimensionReady(ActualWidth, expected.Width)
                    && IsHostDimensionReady(ActualHeight, expected.Height)
                    && IsHostDimensionReady(LayoutRoot.ActualWidth, expected.Width)
                    && IsHostDimensionReady(LayoutRoot.ActualHeight, expected.Height);
                if (widgetReady && coreReady && frameReady && pageReady)
                {
                    return true;
                }

                await Task.Delay(16);
            }

            return false;
        }

        private static bool IsHostDimensionReady(double actual, double expected)
        {
            return actual > 0 && Math.Abs(actual - expected) <= 0.5;
        }

        private async Task RefreshFixedWidgetLayoutAsync(int requestVersion, string source)
        {
            await _widgetLayoutRefreshGate.WaitAsync();
            try
            {
                if (!_isPageActive
                    || _widget == null
                    || requestVersion != _widgetLayoutRefreshRequestVersion)
                {
                    return;
                }

                XboxGameBarWidget widget = _widget;
                SynchronizeHostPageLayout("before-host-refresh");

                // Game Bar can keep the old UWP composition surface when the desktop
                // switches to a stretched in-game resolution. Asking for 550x600 again
                // is commonly coalesced as a no-op, leaving the Page arranged in a
                // smaller top-left client area. A two-DIP nudge followed by the real
                // size forces the host to rebuild both its composition and input bounds.
                var nudgeSize = new Size(
                    DefaultWidgetSize.Width - HostLayoutRefreshNudge,
                    DefaultWidgetSize.Height - HostLayoutRefreshNudge);
                bool nudgeAccepted = await widget.TryResizeWindowAsync(nudgeSize);
                bool nudgeSettled = nudgeAccepted
                    && await WaitForHostLayoutSizeAsync(widget, nudgeSize, requestVersion);
                if (!_isPageActive || _widget == null || !ReferenceEquals(widget, _widget)
                    || requestVersion != _widgetLayoutRefreshRequestVersion)
                {
                    return;
                }
                bool resizeAccepted = await widget.TryResizeWindowAsync(DefaultWidgetSize);
                bool resizeSettled = resizeAccepted
                    && await WaitForHostLayoutSizeAsync(widget, DefaultWidgetSize, requestVersion);

                // A resize request can be accepted before the compositor applies it.
                // Retry once if the real CoreWindow/Page never returned to 550x600.
                bool retryAccepted = false;
                if (!resizeSettled && requestVersion == _widgetLayoutRefreshRequestVersion)
                {
                    retryAccepted = await widget.TryResizeWindowAsync(DefaultWidgetSize);
                    resizeSettled = retryAccepted
                        && await WaitForHostLayoutSizeAsync(widget, DefaultWidgetSize, requestVersion);
                }

                if (!_isPageActive
                    || _widget == null
                    || !ReferenceEquals(widget, _widget))
                {
                    return;
                }

                SynchronizeHostPageLayout("after-host-refresh");
                SavePanelOffset();
                App.LogCrash(
                    "Fixed widget host refreshed. source=" + source
                    + ", nudgeAccepted=" + nudgeAccepted
                    + ", nudgeSettled=" + nudgeSettled
                    + ", restoreAccepted=" + resizeAccepted
                    + ", retryAccepted=" + retryAccepted
                    + ", restoreSettled=" + resizeSettled
                    + ", requestCurrent=" + (requestVersion == _widgetLayoutRefreshRequestVersion)
                    + ", viewport=" + ActualWidth + "x" + ActualHeight
                    + ", panel=" + ControlPanel.ActualWidth + "x" + ControlPanel.ActualHeight);
            }
            catch (Exception ex)
            {
                App.LogCrash("Fixed widget host refresh failed: " + ex);
            }
            finally
            {
                _widgetLayoutRefreshGate.Release();
            }
        }

        private void ApplyPanelTransform()
        {
            if (_panelDragTransform == null)
            {
                _panelDragTransform = new TranslateTransform
                {
                    X = _panelOffsetX,
                    Y = _panelOffsetY
                };
                ControlPanel.RenderTransform = _panelDragTransform;
                ControlPanel.RenderTransformOrigin = new Point(0, 0);
            }
            else
            {
                _panelDragTransform.X = _panelOffsetX;
                _panelDragTransform.Y = _panelOffsetY;
            }
        }

        private void LoadPanelOffset()
        {
            _panelOffsetX = ReadDoubleSetting(PanelOffsetXSettingKey, 0);
            _panelOffsetY = ReadDoubleSetting(PanelOffsetYSettingKey, 0);
            ApplyPanelTransform();
        }

        private async Task RestoreFixedPanelBaselineOnceAsync()
        {
            if (_widget == null)
            {
                return;
            }

            var values = ApplicationData.Current.LocalSettings.Values;
            if (values[FixedPanelBaselineMigrationKey] is bool migrated && migrated)
            {
                return;
            }

            // Old releases persisted both a scaled Game Bar host size and panel
            // offsets expressed in that scaled coordinate system. Removing the
            // transform alone cannot undo either persisted value.
            values.Remove(RemovedControlPanelScaleSettingKey);
            _panelOffsetX = 0;
            _panelOffsetY = 0;
            ApplyPanelTransform();
            SavePanelOffset();

            try
            {
                bool resized = await _widget.TryResizeWindowAsync(DefaultWidgetSize);
                await Task.Delay(100);
                if (!_isPageActive || _widget == null)
                {
                    return;
                }

                await _widget.CenterWindowAsync();
                values[FixedPanelBaselineMigrationKey] = true;
                App.Log("Fixed panel baseline restored. resizeAccepted=" + resized);
            }
            catch (Exception ex)
            {
                // Leave the migration pending so the next activation retries.
                App.Log("Restore fixed panel baseline failed: " + ex.Message);
            }
        }

        private void SavePanelOffset()
        {
            ApplicationData.Current.LocalSettings.Values[PanelOffsetXSettingKey] = _panelOffsetX;
            ApplicationData.Current.LocalSettings.Values[PanelOffsetYSettingKey] = _panelOffsetY;
        }

        private static double ReadDoubleSetting(string key, double fallback)
        {
            object stored = ApplicationData.Current.LocalSettings.Values[key];
            if (stored is double number)
            {
                return number;
            }
            if (stored is int integer)
            {
                return integer;
            }
            if (stored is long longValue)
            {
                return longValue;
            }
            return fallback;
        }

        private async void OnTestEventClick(object sender, RoutedEventArgs e)
        {
            TestPreset preset = GetSelectedTestPreset();
            if (preset == null)
            {
                return;
            }

            await SendTestEventAsync(preset);
        }

        private async void OnReloadAudioClick(object sender, RoutedEventArgs e)
        {
            await ReloadAudioOutputAsync();
        }

        private void OnGameStyleSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressGameStyleEvents)
            {
                return;
            }

            GameStyleService.Current = GetSelectedGameStyle();
        }

        private async void OnOpenGuideClick(object sender, RoutedEventArgs e)
        {
            HeaderStatusSection.OpenGuideButton.IsEnabled = false;
            try
            {
                string parameterGroupId = DeveloperModeSettingsStore.IsEnabled
                    ? OpenSettingsWindowDeveloperParameterGroupId
                    : OpenSettingsWindowParameterGroupId;
                bool launched = await TryLaunchFullTrustHelperAsync(parameterGroupId);
                App.Log("Open settings: external launcher result=" + launched);
                if (launched)
                {
                    return;
                }
            }
            catch (Exception ex)
            {
                App.Log("Failed to open guide: " + ex);
            }
            finally
            {
                HeaderStatusSection.OpenGuideButton.IsEnabled = true;
            }

            ShowGuideOpenFailedHint();
        }

        private void ShowGuideOpenFailedHint()
        {
            string hint = LocalizationManager.Text("OpenGuideFailed");
            ShowStatusHint(hint, Color.FromArgb(255, 180, 90, 0));
        }


        private async void OnRetryServiceClick(object sender, RoutedEventArgs e)
        {
            StatusDetailsSection.RetryServiceButton.IsEnabled = false;
            try
            {
                ShowStatusHint(LocalizationManager.Text("RetryServiceRunning"), Color.FromArgb(255, 180, 90, 0));
                await EnsureServiceAvailableAsync();
            }
            catch (Exception ex)
            {
                App.Log("Retry service failed: " + ex);
                ShowServiceDiagnostic(CreateServiceDiagnostic(
                    "SVC-03",
                    "ServiceDiagLaunchFailed",
                    ex.GetType().Name + " 0x" + ex.HResult.ToString("X8") + ": " + ex.Message));
            }
            finally
            {
                StatusDetailsSection.RetryServiceButton.IsEnabled = true;
            }
        }

        private void OnCopyServiceDiagnosticClick(object sender, RoutedEventArgs e)
        {
            try
            {
                PackageVersion version = Package.Current.Id.Version;
                string versionText = version.Major + "." + version.Minor + "." + version.Build + "." + version.Revision;
                string diagnostic = _currentServiceDiagnostic == null
                    ? LocalizationManager.Text("ServiceRunning")
                    : FormatServiceDiagnostic(_currentServiceDiagnostic);
                string report = "KillConfirm " + versionText
                    + "\r\nTime: " + DateTimeOffset.Now.ToString("u")
                    + "\r\nState: " + _serviceConnectionState
                    + "\r\n" + diagnostic;

                var data = new DataPackage();
                data.SetText(report);
                Clipboard.SetContent(data);
                Clipboard.Flush();
                ShowStatusHint(LocalizationManager.Text("DiagnosticCopied"), Color.FromArgb(255, 5, 122, 85));
            }
            catch (Exception ex)
            {
                App.Log("Copy service diagnostic failed: " + ex);
                ShowStatusHint(LocalizationManager.Text("DiagnosticCopyFailed"), Color.FromArgb(255, 185, 28, 28));
            }
        }

        private async void OnOpenLogsClick(object sender, RoutedEventArgs e)
        {
            try
            {
                bool launched = await TryLaunchFullTrustHelperAsync(OpenRuntimeLogsParameterGroupId);
                if (!launched)
                {
                    await Launcher.LaunchFolderAsync(ApplicationData.Current.LocalFolder);
                }
            }
            catch (Exception ex)
            {
                App.Log("Failed to open log folder: " + ex);
            }
        }

        private async void OnFreePortClick(object sender, RoutedEventArgs e)
        {
            try
            {
                App.Log("Free port requested from widget.");
                StatusDetailsSection.ServiceDiagnosticText.Text = LocalizationManager.Text("FreePortRunning");
                ToolTipService.SetToolTip(StatusDetailsSection.ServiceDiagnosticText, StatusDetailsSection.ServiceDiagnosticText.Text);

                bool launched = await TryLaunchFullTrustHelperAsync(FreeServicePortParameterGroupId);
                if (!launched)
                {
                    StatusDetailsSection.ServiceDiagnosticText.Text = LocalizationManager.Text("FreePortFailed");
                    ToolTipService.SetToolTip(StatusDetailsSection.ServiceDiagnosticText, StatusDetailsSection.ServiceDiagnosticText.Text);
                    App.Log("Free port helper launch failed.");
                    return;
                }

                await Task.Delay(1200);
                await EnsureServiceAvailableAsync();
            }
            catch (Exception ex)
            {
                StatusDetailsSection.ServiceDiagnosticText.Text = LocalizationManager.Text("FreePortFailed");
                ToolTipService.SetToolTip(StatusDetailsSection.ServiceDiagnosticText, StatusDetailsSection.ServiceDiagnosticText.Text);
                App.Log("Free port failed: " + ex);
            }
        }

        private void OnWidgetVisibleChanged(XboxGameBarWidget sender, object args)
        {
            SyncWidgetPresentationState();
        }

        private void OnGameBarDisplayModeChanged(XboxGameBarWidget sender, object args)
        {
            SyncWidgetPresentationState();
        }

        private void OnClickThroughEnabledChanged(XboxGameBarWidget sender, object args)
        {
            SyncWidgetPresentationState();
        }

        private void OnWidgetWindowStateChanged(XboxGameBarWidget sender, object args)
        {
            SyncWidgetPresentationState();
        }

        private void OnWidgetPinnedChanged(XboxGameBarWidget sender, object args)
        {
            SyncWidgetPresentationState();
        }

        private void OnControlPanelStateTimerTick(object sender, object e)
        {
            SyncWidgetPresentationState();
            if (!string.Equals(
                    _loadedCsGameVersion,
                    GsiGameVersionSettingsStore.Load(),
                    StringComparison.Ordinal))
            {
                _ = LoadSavedCsFolderAsync();
            }
            if (IsControlPanelVisible()
                && !_gsiStatusCheckPending
                && DateTimeOffset.Now - _lastGsiStatusCheck > TimeSpan.FromMilliseconds(GsiStatusRefreshMs))
            {
                _ = RefreshGsiStatusAsync();
            }
        }

        private void OnStatusHintTimerTick(object sender, object e)
        {
            AdvanceStatusHint();
        }

        private void OnConnectionStateChanged(object sender, KillEventConnectionState state)
        {
            UpdateConnectionState(state);
        }

        private enum CfgDetectionState
        {
            NotSelected,
            Checking,
            Ready,
            Missing,
            Outdated,
            Error
        }
    }
}

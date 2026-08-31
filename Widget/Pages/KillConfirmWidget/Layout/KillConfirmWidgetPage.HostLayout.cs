using Microsoft.Gaming.XboxGameBar;
using System;
using System.Collections.Generic;
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

        private bool _isRepeatTestLooping;
        private int _repeatTestLoopToken;

        private async void OnRepeatTestClick(object sender, RoutedEventArgs e)
        {
            _isRepeatTestLooping = !_isRepeatTestLooping;
            int token = ++_repeatTestLoopToken;
            UpdateRepeatTestButtonVisuals();
            if (!_isRepeatTestLooping)
            {
                return;
            }

            await RunRepeatTestLoopAsync(token);
        }

        private async Task RunRepeatTestLoopAsync(int token)
        {
            while (_isRepeatTestLooping && token == _repeatTestLoopToken && _isPageActive)
            {
                TestPreset preset = GetSelectedTestPreset();
                if (preset == null)
                {
                    break;
                }

                await SendTestEventAsync(preset);
                if (!await WaitForRepeatTestPlaybackAsync(token))
                {
                    break;
                }
            }

            if (token == _repeatTestLoopToken)
            {
                _isRepeatTestLooping = false;
                UpdateRepeatTestButtonVisuals();
            }
        }

        private async Task<bool> WaitForRepeatTestPlaybackAsync(int token)
        {
            Controls.KillConfirmAnimation primary =
                GetFeedbackAnimation(KillFeedbackFrameDefinition.GetLegacyPrimaryLayer(GameStyleService.Current));
            if (primary == null)
            {
                await Task.Delay(400);
                return _isRepeatTestLooping && token == _repeatTestLoopToken;
            }

            // Wait for the playback to start (the animation becomes Visible).
            for (int i = 0; i < 40; i++)
            {
                if (token != _repeatTestLoopToken || !_isRepeatTestLooping || !_isPageActive)
                {
                    return false;
                }
                if (primary.Visibility == Visibility.Visible)
                {
                    break;
                }
                await Task.Delay(50);
            }

            // Wait for the playback to finish (the animation collapses).
            for (int i = 0; i < 200; i++)
            {
                if (token != _repeatTestLoopToken || !_isRepeatTestLooping || !_isPageActive)
                {
                    return false;
                }
                if (primary.Visibility == Visibility.Collapsed)
                {
                    return true;
                }
                await Task.Delay(50);
            }

            return true;
        }

        private void UpdateRepeatTestButtonVisuals()
        {
            SolidColorBrush highlight = _isRepeatTestLooping
                ? new SolidColorBrush(Color.FromArgb(96, 245, 158, 11))
                : null;
            if (MiniRepeatTestButton != null)
            {
                MiniRepeatTestButton.Background = highlight;
            }
            if (PackTestSectionView != null && PackTestSectionView.RepeatTestButton != null)
            {
                PackTestSectionView.RepeatTestButton.Background = highlight;
            }
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

using Microsoft.Gaming.XboxGameBar;
using System;
using KillConfirmGameBar.Services;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;

namespace KillConfirmGameBar
{
    public sealed partial class KillConfirmWidgetPage
    {
        private void UpdateControlPanelVisibility()
        {
            bool showControlPanel = IsControlPanelVisible();

            ControlPanel.Visibility = showControlPanel ? Visibility.Visible : Visibility.Collapsed;
            ControlPanel.IsHitTestVisible = showControlPanel;
            ControlPanel.Opacity = showControlPanel ? 1.0 : 0.0;
            GameStyleMode style = GameStyleService.Current;
            KillFeedbackVisibilitySettingsValues visibility =
                KillFeedbackVisibilitySettingsStore.Load(style);
            bool primaryIsOptionalCrosshair = style == GameStyleMode.Overwatch
                || style == GameStyleMode.Apex
                || style == GameStyleMode.ModernWarfare2019;
            bool showPrimaryOutline = showControlPanel
                && (!primaryIsOptionalCrosshair || visibility.CrosshairEnabled);
            AnimationDragOutline.Visibility = showPrimaryOutline ? Visibility.Visible : Visibility.Collapsed;
            AnimationDragOutline.IsHitTestVisible = showPrimaryOutline;
            bool showOverwatchCardOutline = showControlPanel
                && visibility.LowerEnabled
                && (style == GameStyleMode.Overwatch
                    || style == GameStyleMode.Apex
                    || style == GameStyleMode.ModernWarfare2019);
            OverwatchCardDragOutline.Visibility = showOverwatchCardOutline ? Visibility.Visible : Visibility.Collapsed;
            OverwatchCardDragOutline.IsHitTestVisible = showOverwatchCardOutline;
            bool showModernWarfare2019UpperOutline = showControlPanel
                && ((style == GameStyleMode.ModernWarfare2019 && visibility.UpperEnabled)
                    || (GameStyleService.IsAuxiliaryKillMarkStyle(style)
                        && visibility.CrosshairEnabled));
            ModernWarfare2019UpperDragOutline.Visibility = showModernWarfare2019UpperOutline
                ? Visibility.Visible
                : Visibility.Collapsed;
            ModernWarfare2019UpperDragOutline.IsHitTestVisible = showModernWarfare2019UpperOutline;
            if (!showControlPanel
                || (!showPrimaryOutline && _isAnimationFrameSelected)
                || (!showOverwatchCardOutline && _isOverwatchCardFrameSelected)
                || (!showModernWarfare2019UpperOutline && _isModernWarfare2019UpperFrameSelected))
            {
                _isAnimationFrameSelected = false;
                _isOverwatchCardFrameSelected = false;
                _isModernWarfare2019UpperFrameSelected = false;
                UpdateAnimationDragOutlineSelectionVisual();
            }
        }

        private bool IsControlPanelVisible()
        {
            return _isWidgetVisible
                && _displayMode == XboxGameBarDisplayMode.Foreground
                && _windowState != XboxGameBarWidgetWindowState.Minimized;
        }

        private void SyncWidgetPresentationState()
        {
            if (_widget == null)
            {
                return;
            }

            bool wasControlPanelVisible = IsControlPanelVisible();
            try
            {
                _isWidgetVisible = _widget.Visible;
                _displayMode = _widget.GameBarDisplayMode;
                _windowState = _widget.WindowState;
                _isPinned = _widget.Pinned;
                _clickThroughEnabled = _widget.ClickThroughEnabled;
            }
            catch (Exception)
            {
            }

            UpdateControlPanelVisibility();
            if (wasControlPanelVisible != IsControlPanelVisible())
            {
                // Preserve the known-good v3.3.2 behavior: a visibility transition
                // forces Game Bar to refresh the existing host window for the current
                // display mode. The requested size itself always remains 550 x 600.
                RequestFixedWidgetLayoutRefresh();
            }
        }

        private void UpdateConnectionState(KillEventConnectionState state)
        {
            _serviceConnectionState = state;

            switch (state)
            {
                case KillEventConnectionState.Connected:
                    ConnectionDot.Background = new SolidColorBrush(Color.FromArgb(255, 52, 211, 153));
                    SetNamedToolTip(ConnectionStatusBadge, LocalizationManager.Text("ServiceStatusTitle"), LocalizationManager.Text("ServiceRunning"));
                    HideServiceDiagnostic();
                    break;
                case KillEventConnectionState.Connecting:
                    ConnectionDot.Background = new SolidColorBrush(Color.FromArgb(255, 180, 90, 0));
                    SetNamedToolTip(ConnectionStatusBadge, LocalizationManager.Text("ServiceStatusTitle"), LocalizationManager.Text("ServiceStarting"));
                    break;
                default:
                    ConnectionDot.Background = new SolidColorBrush(Color.FromArgb(255, 185, 28, 28));
                    SetNamedToolTip(
                        ConnectionStatusBadge,
                        LocalizationManager.Text("ServiceStatusTitle"),
                        _currentServiceDiagnostic == null
                            ? LocalizationManager.Text("ServiceOffline")
                            : FormatServiceDiagnostic(_currentServiceDiagnostic));
                    break;
            }

            RefreshStatusHint(false);
        }

        private void UpdateCfgStatus(CfgDetectionState state, string label, string detail)
        {
            _cfgDetectionState = state;
            _cfgStatusDetail = detail ?? string.Empty;
            CfgStatusText.Text = string.IsNullOrWhiteSpace(label) ? ResolveCfgStatusLabel(state) : label;
            CfgHintText.Text = ResolveCfgHintText(state, _cfgStatusDetail);
            CfgActionRow.Visibility = state == CfgDetectionState.Ready
                ? Visibility.Collapsed
                : Visibility.Visible;
            CfgInstallButton.Visibility = state == CfgDetectionState.Missing || state == CfgDetectionState.Outdated
                ? Visibility.Visible
                : Visibility.Collapsed;
            SetNamedToolTip(
                CfgInstallButton,
                LocalizationManager.Text(state == CfgDetectionState.Outdated ? "UpdateCfgTitle" : "AddMissingCfgTitle"),
                LocalizationManager.Text(state == CfgDetectionState.Outdated ? "UpdateCfgTooltip" : "AddMissingCfgTooltip"));
            UpdateStatusDetailRowVisibility();

            switch (state)
            {
                case CfgDetectionState.Ready:
                    CfgDot.Background = new SolidColorBrush(Color.FromArgb(255, 52, 211, 153));
                    SetNamedToolTip(CfgStatusBadge, LocalizationManager.Text("CfgStatusTitle"), LocalizationManager.Text("CfgReadyTooltip") + _cfgStatusDetail);
                    break;
                case CfgDetectionState.Checking:
                    CfgDot.Background = new SolidColorBrush(Color.FromArgb(255, 180, 90, 0));
                    SetNamedToolTip(CfgStatusBadge, LocalizationManager.Text("CfgStatusTitle"), LocalizationManager.Text("CheckingCfgTooltip"));
                    break;
                case CfgDetectionState.Missing:
                    CfgDot.Background = new SolidColorBrush(Color.FromArgb(255, 180, 90, 0));
                    SetNamedToolTip(CfgStatusBadge, LocalizationManager.Text("CfgStatusTitle"), LocalizationManager.Text("CfgMissingTooltip") + _cfgStatusDetail);
                    break;
                case CfgDetectionState.Outdated:
                    CfgDot.Background = new SolidColorBrush(Color.FromArgb(255, 185, 28, 28));
                    SetNamedToolTip(CfgStatusBadge, LocalizationManager.Text("CfgStatusTitle"), LocalizationManager.Text("CfgOutdatedHint"));
                    break;
                case CfgDetectionState.Error:
                    CfgDot.Background = new SolidColorBrush(Color.FromArgb(255, 185, 28, 28));
                    SetNamedToolTip(CfgStatusBadge, LocalizationManager.Text("CfgStatusTitle"), _cfgStatusDetail);
                    break;
                default:
                    CfgDot.Background = new SolidColorBrush(Color.FromArgb(255, 75, 85, 99));
                    SetNamedToolTip(CfgStatusBadge, LocalizationManager.Text("CfgStatusTitle"), LocalizationManager.Text("SelectCsRootTooltip"));
                    break;
            }

            RefreshStatusHint(false);
        }

        private void UpdateStatusDetailRowVisibility()
        {
            if (StatusDetailRow == null)
            {
                return;
            }

            bool hasVisibleContent =
                ServiceDiagnosticRow?.Visibility == Visibility.Visible ||
                CfgActionRow?.Visibility == Visibility.Visible;

            StatusDetailRow.Visibility = hasVisibleContent
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void UpdateGsiStatus(bool serviceReachable, bool recentlySeen, double posts, double? ageMs, double parseErrors)
        {
            _gsiRecentlySeen = recentlySeen;

            if (recentlySeen)
            {
                GsiDot.Background = new SolidColorBrush(Color.FromArgb(255, 52, 211, 153));
                SetNamedToolTip(GsiStatusBadge, LocalizationManager.Text("GsiStatusTitle"), LocalizationManager.Text("GsiReceivingTooltip"));
            }
            else if (serviceReachable && posts > 0)
            {
                GsiDot.Background = new SolidColorBrush(Color.FromArgb(255, 180, 90, 0));
                SetNamedToolTip(GsiStatusBadge, LocalizationManager.Text("GsiStatusTitle"), LocalizationManager.Text("GsiStaleTooltip"));
            }
            else if (serviceReachable)
            {
                GsiDot.Background = new SolidColorBrush(Color.FromArgb(255, 75, 85, 99));
                SetNamedToolTip(GsiStatusBadge, LocalizationManager.Text("GsiStatusTitle"), LocalizationManager.Text("GsiWaitingTooltip"));
            }
            else
            {
                GsiDot.Background = new SolidColorBrush(Color.FromArgb(255, 185, 28, 28));
                SetNamedToolTip(GsiStatusBadge, LocalizationManager.Text("GsiStatusTitle"), LocalizationManager.Text("ServiceOffline"));
            }

            UpdateStatusDetailRowVisibility();
            RefreshStatusHint(false);
        }

        private static string ResolveCfgStatusLabel(CfgDetectionState state)
        {
            switch (state)
            {
                case CfgDetectionState.Checking:
                    return LocalizationManager.Text("CfgChecking");
                case CfgDetectionState.Ready:
                    return LocalizationManager.Text("CfgReady");
                case CfgDetectionState.Missing:
                    return LocalizationManager.Text("CfgMissing");
                case CfgDetectionState.Outdated:
                    return LocalizationManager.Text("CfgOutdated");
                case CfgDetectionState.Error:
                    return LocalizationManager.Text("CfgCheckFailed");
                default:
                    return LocalizationManager.Text("CfgNotChecked");
            }
        }

        private static string ResolveCfgHintText(CfgDetectionState state, string detail)
        {
            if (state == CfgDetectionState.NotSelected)
            {
                return LocalizationManager.Text("CfgSelectRootHint");
            }

            if (state == CfgDetectionState.Outdated)
            {
                return LocalizationManager.Text("CfgOutdatedHint");
            }

            if (state == CfgDetectionState.Error)
            {
                return string.IsNullOrWhiteSpace(detail)
                    ? LocalizationManager.Text("CfgWrongFolderHint")
                    : detail;
            }

            if (string.IsNullOrWhiteSpace(detail))
            {
                return LocalizationManager.Text("CfgSelectRootHint");
            }

            return LocalizationManager.Text("CfgSavedFolderPrefix") + detail;
        }
    }
}

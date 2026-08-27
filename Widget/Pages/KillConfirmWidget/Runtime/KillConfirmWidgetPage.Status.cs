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
            bool showCrosshair = showControlPanel && visibility.CrosshairEnabled
                && GameStyleService.SupportsCrosshairAreaEffect(style);
            bool showLower = showControlPanel && visibility.LowerEnabled;
            bool showUpper = showControlPanel && visibility.UpperEnabled
                && KillFeedbackFrameDefinition.IsSupported(style, KillFeedbackLayer.Upper);
            CrosshairDragOutline.Visibility = showCrosshair ? Visibility.Visible : Visibility.Collapsed;
            CrosshairDragOutline.IsHitTestVisible = showCrosshair;
            LowerDragOutline.Visibility = showLower ? Visibility.Visible : Visibility.Collapsed;
            LowerDragOutline.IsHitTestVisible = showLower;
            UpperDragOutline.Visibility = showUpper ? Visibility.Visible : Visibility.Collapsed;
            UpperDragOutline.IsHitTestVisible = showUpper;
            if (_selectedFeedbackLayer.HasValue
                && GetFeedbackFrameOutline(_selectedFeedbackLayer.Value).Visibility != Visibility.Visible)
            {
                EndAnimationDrag();
                _selectedFeedbackLayer = null;
            }
            UpdateAnimationDragOutlineSelectionVisual();
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
            bool stateReadSucceeded = false;
            try
            {
                _isWidgetVisible = _widget.Visible;
                _displayMode = _widget.GameBarDisplayMode;
                _windowState = _widget.WindowState;
                _isPinned = _widget.Pinned;
                _clickThroughEnabled = _widget.ClickThroughEnabled;
                _hasGameBarSetupState = true;
                stateReadSucceeded = true;
            }
            catch (Exception)
            {
            }

            if (stateReadSucceeded)
            {
                GameBarRuntimeStatusStore.Publish(_isPinned, _clickThroughEnabled);
            }

            UpdateControlPanelVisibility();
            UpdateGameBarSetupGuidance();
            if (wasControlPanelVisible != IsControlPanelVisible())
            {
                // Preserve the known-good v3.3.2 behavior: a visibility transition
                // forces Game Bar to refresh the existing host window for the current
                // display mode. The requested size itself always remains 550 x 600.
                RequestFixedWidgetLayoutRefresh();
            }
        }

        private void UpdateGameBarSetupGuidance()
        {
            if (GameBarSetupGuideLayer == null)
            {
                return;
            }

            bool panelVisible = _hasGameBarSetupState && IsControlPanelVisible();
            // Game Bar reports ClickThroughEnabled=true after the user has
            // pressed the toolbar button whose Chinese action text is
            // "禁用单击浏览". That is the ready state for an overlay: mouse
            // input passes through to the game. Only guide the user while the
            // feature is not yet active.
            bool showClickThroughGuide = panelVisible && !_clickThroughEnabled;
            bool showPinGuide = panelVisible && !showClickThroughGuide && !_isPinned;

            ClickThroughSetupGuide.Visibility = showClickThroughGuide
                ? Visibility.Visible
                : Visibility.Collapsed;
            PinSetupGuide.Visibility = showPinGuide
                ? Visibility.Visible
                : Visibility.Collapsed;
            GameBarSetupGuideLayer.Visibility = showClickThroughGuide || showPinGuide
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void UpdateConnectionState(KillEventConnectionState state)
        {
            _serviceConnectionState = state;

            switch (state)
            {
                case KillEventConnectionState.Connected:
                    HeaderStatusSection.ConnectionDot.Background = new SolidColorBrush(Color.FromArgb(255, 52, 211, 153));
                    SetNamedToolTip(HeaderStatusSection.ConnectionStatusBadge, LocalizationManager.Text("ServiceStatusTitle"), LocalizationManager.Text("ServiceRunning"));
                    HideServiceDiagnostic();
                    break;
                case KillEventConnectionState.Connecting:
                    HeaderStatusSection.ConnectionDot.Background = new SolidColorBrush(Color.FromArgb(255, 180, 90, 0));
                    SetNamedToolTip(HeaderStatusSection.ConnectionStatusBadge, LocalizationManager.Text("ServiceStatusTitle"), LocalizationManager.Text("ServiceStarting"));
                    break;
                default:
                    HeaderStatusSection.ConnectionDot.Background = new SolidColorBrush(Color.FromArgb(255, 185, 28, 28));
                    SetNamedToolTip(
                        HeaderStatusSection.ConnectionStatusBadge,
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
            StatusDetailsSection.CfgStatusText.Text = string.IsNullOrWhiteSpace(label) ? ResolveCfgStatusLabel(state) : label;
            StatusDetailsSection.CfgHintText.Text = ResolveCfgHintText(state, _cfgStatusDetail);
            StatusDetailsSection.CfgActionRow.Visibility = state == CfgDetectionState.Ready
                ? Visibility.Collapsed
                : Visibility.Visible;
            StatusDetailsSection.CfgInstallButton.Visibility = state == CfgDetectionState.Missing || state == CfgDetectionState.Outdated
                ? Visibility.Visible
                : Visibility.Collapsed;
            UpdateCfgActionButtonPresentation(state);
            UpdateStatusDetailRowVisibility();

            switch (state)
            {
                case CfgDetectionState.Ready:
                    HeaderStatusSection.CfgDot.Background = new SolidColorBrush(Color.FromArgb(255, 52, 211, 153));
                    SetNamedToolTip(HeaderStatusSection.CfgStatusBadge, LocalizationManager.Text("CfgStatusTitle"), LocalizationManager.Text("CfgReadyTooltip") + _cfgStatusDetail);
                    break;
                case CfgDetectionState.Checking:
                    HeaderStatusSection.CfgDot.Background = new SolidColorBrush(Color.FromArgb(255, 180, 90, 0));
                    SetNamedToolTip(HeaderStatusSection.CfgStatusBadge, LocalizationManager.Text("CfgStatusTitle"), LocalizationManager.Text("CheckingCfgTooltip"));
                    break;
                case CfgDetectionState.Missing:
                    HeaderStatusSection.CfgDot.Background = new SolidColorBrush(Color.FromArgb(255, 180, 90, 0));
                    SetNamedToolTip(HeaderStatusSection.CfgStatusBadge, LocalizationManager.Text("CfgStatusTitle"), LocalizationManager.Text("CfgMissingTooltip") + _cfgStatusDetail);
                    break;
                case CfgDetectionState.Outdated:
                    HeaderStatusSection.CfgDot.Background = new SolidColorBrush(Color.FromArgb(255, 185, 28, 28));
                    SetNamedToolTip(HeaderStatusSection.CfgStatusBadge, LocalizationManager.Text("CfgStatusTitle"), LocalizationManager.Text("CfgOutdatedHint"));
                    break;
                case CfgDetectionState.Error:
                    HeaderStatusSection.CfgDot.Background = new SolidColorBrush(Color.FromArgb(255, 185, 28, 28));
                    SetNamedToolTip(HeaderStatusSection.CfgStatusBadge, LocalizationManager.Text("CfgStatusTitle"), _cfgStatusDetail);
                    break;
                default:
                    HeaderStatusSection.CfgDot.Background = new SolidColorBrush(Color.FromArgb(255, 75, 85, 99));
                    SetNamedToolTip(HeaderStatusSection.CfgStatusBadge, LocalizationManager.Text("CfgStatusTitle"), LocalizationManager.Text("SelectCsRootTooltip"));
                    break;
            }

            RefreshStatusHint(false);
        }

        private void UpdateCfgActionButtonPresentation(CfgDetectionState state)
        {
            bool isUpdate = state == CfgDetectionState.Outdated;
            StatusDetailsSection.CfgInstallButton.Content = LocalizationManager.Text(
                isUpdate ? "UpdateCfgAction" : "Add");
            SetNamedToolTip(
                StatusDetailsSection.CfgInstallButton,
                LocalizationManager.Text(isUpdate ? "UpdateCfgTitle" : "AddMissingCfgTitle"),
                LocalizationManager.Text(isUpdate ? "UpdateCfgTooltip" : "AddMissingCfgTooltip"));
        }

        private void UpdateStatusDetailRowVisibility()
        {
            if (StatusDetailsSection.StatusDetailRow == null)
            {
                return;
            }

            bool hasVisibleContent =
                StatusDetailsSection.ServiceDiagnosticRow?.Visibility == Visibility.Visible ||
                StatusDetailsSection.CfgActionRow?.Visibility == Visibility.Visible;

            StatusDetailsSection.StatusDetailRow.Visibility = hasVisibleContent
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void UpdateGsiStatus(bool serviceReachable, bool recentlySeen, double posts, double? ageMs, double parseErrors)
        {
            _gsiRecentlySeen = recentlySeen;

            if (recentlySeen)
            {
                HeaderStatusSection.GsiDot.Background = new SolidColorBrush(Color.FromArgb(255, 52, 211, 153));
                SetNamedToolTip(HeaderStatusSection.GsiStatusBadge, LocalizationManager.Text("GsiStatusTitle"), LocalizationManager.Text("GsiReceivingTooltip"));
            }
            else if (serviceReachable && posts > 0)
            {
                HeaderStatusSection.GsiDot.Background = new SolidColorBrush(Color.FromArgb(255, 180, 90, 0));
                SetNamedToolTip(HeaderStatusSection.GsiStatusBadge, LocalizationManager.Text("GsiStatusTitle"), LocalizationManager.Text("GsiStaleTooltip"));
            }
            else if (serviceReachable)
            {
                HeaderStatusSection.GsiDot.Background = new SolidColorBrush(Color.FromArgb(255, 75, 85, 99));
                SetNamedToolTip(HeaderStatusSection.GsiStatusBadge, LocalizationManager.Text("GsiStatusTitle"), LocalizationManager.Text("GsiWaitingTooltip"));
            }
            else
            {
                HeaderStatusSection.GsiDot.Background = new SolidColorBrush(Color.FromArgb(255, 185, 28, 28));
                SetNamedToolTip(HeaderStatusSection.GsiStatusBadge, LocalizationManager.Text("GsiStatusTitle"), LocalizationManager.Text("ServiceOffline"));
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
                string actionHint = LocalizationManager.Text("CfgCheckFailedFolderHint");
                return string.IsNullOrWhiteSpace(detail)
                    ? actionHint
                    : actionHint + "\n" + detail;
            }

            if (string.IsNullOrWhiteSpace(detail))
            {
                return LocalizationManager.Text("CfgSelectRootHint");
            }

            return LocalizationManager.Text("CfgSavedFolderPrefix") + detail;
        }
    }
}

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

        private async void OnGameStyleServiceChanged(object sender, GameStyleMode mode)
        {
            if (!_isPageActive)
            {
                return;
            }

            try
            {
                await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
                {
                    if (!_isPageActive)
                    {
                        return;
                    }

                    try
                    {
                        _animationPreloadToken++;
                        PrimaryKillAnimation?.ReleaseAnimationResourcesForPackChange();
                        BadgeKillAnimation?.ReleaseAnimationResourcesForPackChange();
                        OverwatchCardAnimation?.ReleaseAnimationResourcesForPackChange();
                        ModernWarfare2019UpperAnimation?.ReleaseAnimationResourcesForPackChange();
                        _suppressGameStyleEvents = true;
                        try
                        {
                            SelectGameStyleItem(mode);
                        }
                        finally
                        {
                            _suppressGameStyleEvents = false;
                        }

                        LoadAnimationPlacementSettings();
                        ApplyGameStyleUi();
                        await ReloadAudioVolumeForCurrentGameAsync();
                        await InitializePackSelectorsAsync();
                        await SyncSelectedVoicePackAsync();
                        await SyncCrossfireGameplaySettingsAsync();
                        await SyncCsolGameplaySettingsAsync();
                        await SyncDagoujiaoSettingsAsync();
                        await SyncSharedStreakSettingsAsync();
                        await WarmStartupAnimationCacheAsync(0);
                    }
                    catch (Exception ex)
                    {
                        App.LogCrash("Game style switch failed: " + ex);
                    }
                });
            }
            catch (Exception ex)
            {
                // Dispatcher itself can become unavailable while Game Bar is
                // tearing the page down. Do not let that lifecycle race escape
                // an async-void event handler and terminate the widget process.
                if (_isPageActive)
                {
                    App.LogCrash("Game style dispatch failed: " + ex);
                }
            }
        }

        private void OnKillReceived(object sender, KillEvent e)
        {
            HandleKillEvent(e);
        }

        private async void OnResizeClick(object sender, RoutedEventArgs e)
        {
            if (_widget == null)
            {
                return;
            }

            try
            {
                await _widget.TryResizeWindowAsync(DefaultWidgetSize);
            }
            catch (Exception)
            {
            }
        }

        private async void OnCenterClick(object sender, RoutedEventArgs e)
        {
            await CenterWidgetWindowAsync("visual-toolbar");
        }

        private async Task CenterWidgetWindowAsync(string source)
        {
            XboxGameBarWidget widget = _widget;
            if (widget == null)
            {
                return;
            }

            try
            {
                await widget.CenterWindowAsync();
            }
            catch (Exception ex)
            {
                App.Log("Center widget window failed (" + source + "): " + ex.Message);
            }
        }

        private void OnLowerThirdClick(object sender, RoutedEventArgs e)
        {
            SetNonCrosshairAnimationPlacement(AnimationPlacementMode.Bottom);
        }

        private void OnHighPositionClick(object sender, RoutedEventArgs e)
        {
            SetNonCrosshairAnimationPlacement(AnimationPlacementMode.Top);
        }

        private void OnIconCenterClick(object sender, RoutedEventArgs e)
        {
            SetNonCrosshairAnimationPlacement(AnimationPlacementMode.Center);
        }

        private async void OnCrosshairCenterClick(object sender, RoutedEventArgs e)
        {
            if (!GameStyleService.SupportsCrosshairAreaEffect(GameStyleService.Current))
            {
                return;
            }

            if (GameStyleService.IsAuxiliaryKillMarkStyle(GameStyleService.Current))
            {
                _modernWarfare2019UpperHorizontalOffset = 0;
                _modernWarfare2019UpperVerticalOffset = 0;
                ApplyModernWarfare2019UpperTransform();
                SaveModernWarfare2019UpperPlacementSettings();
            }
            else
            {
                _animationPlacement = AnimationPlacementMode.Center;
                _animationOffset = 0;
                _animationHorizontalOffset = 0;
                ApplyAnimationTransform();
                SaveAnimationPlacementSettings();
            }

            if (_widget == null)
            {
                return;
            }

            try
            {
                await _widget.CenterWindowAsync();
            }
            catch (Exception ex)
            {
                App.Log("Center crosshair effect window failed: " + ex.Message);
            }
        }

        private void OnWindowTopClick(object sender, RoutedEventArgs e)
        {
            MoveControlPanelToEdge(toTop: true);
        }

        private void OnWindowBottomClick(object sender, RoutedEventArgs e)
        {
            MoveControlPanelToEdge(toTop: false);
        }

        private void OnControlPanelCenterClick(object sender, RoutedEventArgs e)
        {
            if (!TryGetCenteredControlPanelOffset(out Point centeredOffset))
            {
                return;
            }

            SetPanelOffset(centeredOffset.X, centeredOffset.Y);
            SavePanelOffset();
        }

        private void MoveControlPanelToEdge(bool toTop)
        {
            if (ControlPanel == null)
            {
                return;
            }

            if (!TryGetControlPanelVerticalRange(out _, out double bottomOffset))
            {
                return;
            }

            // ControlPanel is top-aligned with a 5 px margin. Its render transform
            // is already used by the drag-to-move feature, so the preset buttons
            // use that same lightweight path instead of moving the Game Bar host.
            double targetY = toTop ? 0 : bottomOffset;
            SetPanelOffset(_panelOffsetX, targetY);
            SavePanelOffset();
        }

        private bool TryGetControlPanelVerticalRange(out double topOffset, out double bottomOffset)
        {
            topOffset = 0;
            bottomOffset = 0;
            if (ControlPanel == null || ControlPanel.ActualHeight <= 0 || ActualHeight <= 0)
            {
                return false;
            }

            bottomOffset = ActualHeight
                - ControlPanel.ActualHeight
                - ControlPanel.Margin.Top
                - ControlPanel.Margin.Bottom;
            return true;
        }

        private bool TryGetCenteredControlPanelOffset(out Point centeredOffset)
        {
            centeredOffset = new Point();
            if (ControlPanel == null
                || ControlPanel.ActualWidth <= 0
                || ControlPanel.ActualHeight <= 0
                || ActualWidth <= 0
                || ActualHeight <= 0)
            {
                return false;
            }

            double panelHeight = ControlPanel.ActualHeight;
            centeredOffset = new Point(
                0,
                ((ActualHeight - panelHeight) / 2.0) - ControlPanel.Margin.Top);
            return true;
        }

        private void OnMoveUpClick(object sender, RoutedEventArgs e)
        {
            NudgeAnimation(-AnimationOffsetStep);
        }

        private void OnMoveDownClick(object sender, RoutedEventArgs e)
        {
            NudgeAnimation(AnimationOffsetStep);
        }

        private void OnMoveLeftClick(object sender, RoutedEventArgs e)
        {
            NudgeAnimationHorizontal(-AnimationOffsetStep);
        }

        private void OnMoveRightClick(object sender, RoutedEventArgs e)
        {
            NudgeAnimationHorizontal(AnimationOffsetStep);
        }

        private void OnScaleUpClick(object sender, RoutedEventArgs e)
        {
            ScaleAnimation(ScaleUpFactor);
        }

        private void OnScaleDownClick(object sender, RoutedEventArgs e)
        {
            ScaleAnimation(ScaleDownFactor);
        }

        private void WireMoveWindowEvents()
        {
            // Drag the status hint card (the non-interactive background of the top
            // strip) to move the control panel, like dragging a window title bar.
            WireDragElement(HeaderStatusSection.StatusHintBox);
            // The collapsed mini panel is also draggable from its empty background.
            WireDragElement(MiniPanel);
        }

        private void OnAnimationLogicalViewportSizeChanged(object sender, EventArgs e)
        {
            UpdateAnimationDragOutlineSize();
        }

        private void UpdateAnimationDragOutlineSize()
        {
            double availableWidth = AnimationLayer?.ActualWidth > 0 ? AnimationLayer.ActualWidth : DefaultWidgetSize.Width;
            double availableHeight = AnimationLayer?.ActualHeight > 0 ? AnimationLayer.ActualHeight : DefaultWidgetSize.Height;
            bool overwatch = GameStyleService.Current == GameStyleMode.Overwatch;
            bool apex = GameStyleService.Current == GameStyleMode.Apex;
            bool modernWarfare2019 = GameStyleService.Current == GameStyleMode.ModernWarfare2019;
            ResolvePrimaryAnimationDragViewport(
                overwatch,
                apex,
                modernWarfare2019,
                out double displayWidth,
                out double displayHeight,
                out double displayCenterOffsetX,
                out double displayCenterOffsetY);
            bool directValorantPresentation = Controls.KillConfirmAnimation.IsValorantPresentationConfigured;
            double fit = directValorantPresentation
                ? 1.0
                : Math.Min(1.0, Math.Min(availableWidth / displayWidth, availableHeight / displayHeight));
            AnimationDragOutline.Width = Math.Max(40, displayWidth * fit);
            AnimationDragOutline.Height = Math.Max(40, displayHeight * fit);
            AnimationDragOutlineTransform.X = displayCenterOffsetX * fit;
            AnimationDragOutlineTransform.Y = displayCenterOffsetY * fit;

            double cardWidth = modernWarfare2019
                ? ModernWarfare2019LowerFrameWidth
                : overwatch
                    ? Math.Max(1, OverwatchCardAnimation?.OverwatchSelectionViewportWidth ?? 180)
                    : apex
                        ? Math.Max(1, OverwatchCardAnimation?.ApexCardSelectionViewportWidth ?? 96)
                        : Math.Max(1, OverwatchCardAnimation?.SelectionViewportWidth ?? 180);
            double cardHeight = modernWarfare2019
                ? ModernWarfare2019LowerFrameHeight
                : overwatch
                    ? Math.Max(1, OverwatchCardAnimation?.OverwatchSelectionViewportHeight ?? 44)
                    : apex
                        ? Math.Max(1, OverwatchCardAnimation?.ApexCardSelectionViewportHeight ?? 56)
                        : Math.Max(1, OverwatchCardAnimation?.SelectionViewportHeight ?? 44);
            double cardFit = apex
                ? Math.Min(1.0, Math.Min(availableWidth / 560.0, availableHeight / 360.0))
                : modernWarfare2019
                    ? Math.Min(
                        1.0,
                        Math.Min(availableWidth / cardWidth, availableHeight / cardHeight))
                    : Math.Min(1.0, Math.Min(availableWidth / 550.0, availableHeight / 600.0));
            OverwatchCardDragOutline.Width = Math.Max(40, cardWidth * cardFit);
            OverwatchCardDragOutline.Height = Math.Max(28, cardHeight * cardFit);
            OverwatchCardDragOutlineTransform.X = apex
                ? OverwatchCardAnimation.ApexCardSelectionViewportCenterOffsetX * cardFit
                : overwatch
                    ? OverwatchCardAnimation.SelectionViewportCenterOffsetX * cardFit
                    : 0;
            OverwatchCardDragOutlineTransform.Y = apex
                ? OverwatchCardAnimation.ApexCardSelectionViewportCenterOffsetY * cardFit
                : overwatch
                    ? OverwatchCardAnimation.SelectionViewportCenterOffsetY * cardFit
                    : 0;

            bool battlefieldKillMark = GameStyleService.IsAuxiliaryKillMarkStyle(
                GameStyleService.Current);
            double auxiliaryFrameWidth = battlefieldKillMark
                ? ModernWarfare2019CrosshairFrameWidth
                : ModernWarfare2019UpperFrameWidth;
            double auxiliaryFrameHeight = battlefieldKillMark
                ? ModernWarfare2019CrosshairFrameHeight
                : ModernWarfare2019UpperFrameHeight;
            double upperFit = Math.Min(
                1.0,
                Math.Min(
                    availableWidth / auxiliaryFrameWidth,
                    availableHeight / auxiliaryFrameHeight));
            ModernWarfare2019UpperDragOutline.Width = Math.Max(
                40,
                auxiliaryFrameWidth * upperFit);
            ModernWarfare2019UpperDragOutline.Height = Math.Max(
                40,
                auxiliaryFrameHeight * upperFit);
            ModernWarfare2019UpperDragOutlineTransform.X = battlefieldKillMark
                ? ModernWarfare2019CrosshairFrameOffsetX * upperFit
                : 0.0;
            ModernWarfare2019UpperDragOutlineTransform.Y = battlefieldKillMark
                ? ModernWarfare2019CrosshairFrameOffsetY * upperFit
                : 0.0;
        }

        private void ResolvePrimaryAnimationDragViewport(
            bool overwatch,
            bool apex,
            bool modernWarfare2019,
            out double width,
            out double height,
            out double centerOffsetX,
            out double centerOffsetY)
        {
            centerOffsetX = 0;
            centerOffsetY = 0;
            if (overwatch)
            {
                width = 320;
                height = 320;
                return;
            }

            if (apex)
            {
                width = ApexCrosshairFrameWidth;
                height = ApexCrosshairFrameHeight;
                return;
            }

            if (modernWarfare2019)
            {
                width = ModernWarfare2019CrosshairFrameWidth;
                height = ModernWarfare2019CrosshairFrameHeight;
                centerOffsetX = ModernWarfare2019CrosshairFrameOffsetX;
                centerOffsetY = ModernWarfare2019CrosshairFrameOffsetY;
                return;
            }

            switch (GameStyleService.Current)
            {
                case GameStyleMode.Battlefield5:
                    width = PrimaryKillAnimation?.Battlefield5LowerSelectionViewportWidth ?? 360;
                    height = PrimaryKillAnimation?.Battlefield5LowerSelectionViewportHeight ?? 150;
                    centerOffsetY = PrimaryKillAnimation?.Battlefield5LowerSelectionViewportCenterOffsetY ?? 30;
                    return;
                case GameStyleMode.Battlefield4:
                    width = PrimaryKillAnimation?.Battlefield4LowerSelectionViewportWidth ?? 360;
                    height = PrimaryKillAnimation?.Battlefield4LowerSelectionViewportHeight ?? 100;
                    centerOffsetY = PrimaryKillAnimation?.Battlefield4LowerSelectionViewportCenterOffsetY ?? 65;
                    return;
                case GameStyleMode.Battlefield2042:
                    width = PrimaryKillAnimation?.Battlefield2042LowerSelectionViewportWidth ?? 440;
                    height = PrimaryKillAnimation?.Battlefield2042LowerSelectionViewportHeight ?? 170;
                    centerOffsetY = PrimaryKillAnimation?.Battlefield2042LowerSelectionViewportCenterOffsetY ?? 45;
                    return;
                case GameStyleMode.Pubg:
                    width = PrimaryKillAnimation?.PubgLowerSelectionViewportWidth ?? 420;
                    height = PrimaryKillAnimation?.PubgLowerSelectionViewportHeight ?? 125;
                    centerOffsetY = PrimaryKillAnimation?.PubgLowerSelectionViewportCenterOffsetY ?? 30;
                    return;
                case GameStyleMode.DeltaForce:
                    width = PrimaryKillAnimation?.DeltaForceLowerSelectionViewportWidth ?? 360;
                    height = PrimaryKillAnimation?.DeltaForceLowerSelectionViewportHeight ?? 125;
                    centerOffsetY = PrimaryKillAnimation?.DeltaForceLowerSelectionViewportCenterOffsetY ?? 37;
                    return;
                default:
                    width = Math.Max(1, PrimaryKillAnimation?.SelectionViewportWidth ?? 550);
                    height = Math.Max(1, PrimaryKillAnimation?.SelectionViewportHeight ?? 412.5);
                    centerOffsetX = PrimaryKillAnimation?.SelectionViewportCenterOffsetX ?? 0;
                    centerOffsetY = PrimaryKillAnimation?.SelectionViewportCenterOffsetY ?? 0;
                    return;
            }
        }

        private void OnAnimationFramePointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (e.Pointer.PointerDeviceType != PointerDeviceType.Mouse
                && e.Pointer.PointerDeviceType != PointerDeviceType.Touch)
            {
                return;
            }

            var pointerPoint = e.GetCurrentPoint(Window.Current.Content);
            if (e.Pointer.PointerDeviceType == PointerDeviceType.Mouse
                && pointerPoint.Properties.IsRightButtonPressed)
            {
                return;
            }

            _animationDragPointerId = e.Pointer.PointerId;
            _animationDragPointerStart = pointerPoint.Position;
            _activeAnimationDragOutline = sender as Border;
            // Mark selected immediately; drag is armed in PointerMoved once the
            // pointer travels past ClickVsDragThresholdPx. A press without
            // movement leaves the outline selected so the wheel can resize it.
            SelectAnimationFrame(_activeAnimationDragOutline);
            _activeAnimationDragOutline?.CapturePointer(e.Pointer);
            e.Handled = true;
        }
    }
}

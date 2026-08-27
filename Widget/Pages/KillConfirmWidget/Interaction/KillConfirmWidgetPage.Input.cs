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
                        LowerFeedbackAnimation?.ReleaseAnimationResourcesForPackChange();
                        LowerBadgeAnimation?.ReleaseAnimationResourcesForPackChange();
                        CrosshairFeedbackAnimation?.ReleaseAnimationResourcesForPackChange();
                        UpperFeedbackAnimation?.ReleaseAnimationResourcesForPackChange();
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

            SetFeedbackFramePlacement(KillFeedbackLayer.Crosshair, AnimationPlacementMode.Center);

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
            double availableWidth = LowerFeedbackLayer?.ActualWidth > 0 ? LowerFeedbackLayer.ActualWidth : DefaultWidgetSize.Width;
            double availableHeight = LowerFeedbackLayer?.ActualHeight > 0 ? LowerFeedbackLayer.ActualHeight : DefaultWidgetSize.Height;
            GameStyleMode style = GameStyleService.Current;
            ResolveLowerFeedbackViewport(out double width, out double height, out double offsetX, out double offsetY);
            double fit = Controls.KillConfirmAnimation.IsValorantPresentationConfigured ? 1.0
                : style == GameStyleMode.Overwatch ? Math.Min(1.0, Math.Min(availableWidth / 550.0, availableHeight / 600.0))
                : style == GameStyleMode.Apex ? Math.Min(1.0, Math.Min(availableWidth / 560.0, availableHeight / 360.0))
                : Math.Min(1.0, Math.Min(availableWidth / width, availableHeight / height));
            LowerDragOutline.Width = Math.Max(40, width * fit);
            double minimumLowerHeight = KillFeedbackFrameDefinition.GetLegacyPrimaryLayer(style) == KillFeedbackLayer.Crosshair ? 28 : 40;
            LowerDragOutline.Height = Math.Max(minimumLowerHeight, height * fit);
            LowerDragOutlineTransform.X = offsetX * fit;
            LowerDragOutlineTransform.Y = offsetY * fit;

            double crosshairWidth = style == GameStyleMode.Overwatch ? 320
                : style == GameStyleMode.Apex ? ApexCrosshairFrameWidth : ModernWarfare2019CrosshairFrameWidth;
            double crosshairHeight = style == GameStyleMode.Overwatch ? 320
                : style == GameStyleMode.Apex ? ApexCrosshairFrameHeight : ModernWarfare2019CrosshairFrameHeight;
            double crosshairFit = Math.Min(1.0, Math.Min(availableWidth / crosshairWidth, availableHeight / crosshairHeight));
            CrosshairDragOutline.Width = Math.Max(40, crosshairWidth * crosshairFit);
            CrosshairDragOutline.Height = Math.Max(40, crosshairHeight * crosshairFit);
            CrosshairDragOutlineTransform.X = 0;
            CrosshairDragOutlineTransform.Y = 0;

            double upperFit = Math.Min(1.0, Math.Min(availableWidth / ModernWarfare2019UpperFrameWidth,
                availableHeight / ModernWarfare2019UpperFrameHeight));
            UpperDragOutline.Width = Math.Max(40, ModernWarfare2019UpperFrameWidth * upperFit);
            UpperDragOutline.Height = Math.Max(40, ModernWarfare2019UpperFrameHeight * upperFit);
            UpperDragOutlineTransform.X = 0;
            UpperDragOutlineTransform.Y = 0;
        }

        private void ResolveLowerFeedbackViewport(out double width, out double height,
            out double centerOffsetX, out double centerOffsetY)
        {
            centerOffsetX = 0;
            centerOffsetY = 0;
            switch (GameStyleService.Current)
            {
                case GameStyleMode.Overwatch:
                    width = Math.Max(1, LowerFeedbackAnimation?.OverwatchSelectionViewportWidth ?? 180);
                    height = Math.Max(1, LowerFeedbackAnimation?.OverwatchSelectionViewportHeight ?? 44);
                    centerOffsetX = LowerFeedbackAnimation?.SelectionViewportCenterOffsetX ?? 0;
                    centerOffsetY = LowerFeedbackAnimation?.SelectionViewportCenterOffsetY ?? 0;
                    return;
                case GameStyleMode.Apex:
                    width = Math.Max(1, LowerFeedbackAnimation?.ApexCardSelectionViewportWidth ?? 96);
                    height = Math.Max(1, LowerFeedbackAnimation?.ApexCardSelectionViewportHeight ?? 56);
                    centerOffsetX = LowerFeedbackAnimation?.ApexCardSelectionViewportCenterOffsetX ?? 0;
                    centerOffsetY = LowerFeedbackAnimation?.ApexCardSelectionViewportCenterOffsetY ?? 0;
                    return;
                case GameStyleMode.ModernWarfare2019:
                    width = ModernWarfare2019LowerFrameWidth;
                    height = ModernWarfare2019LowerFrameHeight;
                    return;
                case GameStyleMode.Battlefield5:
                    width = LowerFeedbackAnimation?.Battlefield5LowerSelectionViewportWidth ?? 360;
                    height = LowerFeedbackAnimation?.Battlefield5LowerSelectionViewportHeight ?? 150;
                    centerOffsetY = LowerFeedbackAnimation?.Battlefield5LowerSelectionViewportCenterOffsetY ?? 30;
                    return;
                case GameStyleMode.Battlefield4:
                    width = LowerFeedbackAnimation?.Battlefield4LowerSelectionViewportWidth ?? 360;
                    height = LowerFeedbackAnimation?.Battlefield4LowerSelectionViewportHeight ?? 100;
                    centerOffsetY = LowerFeedbackAnimation?.Battlefield4LowerSelectionViewportCenterOffsetY ?? 65;
                    return;
                case GameStyleMode.Battlefield2042:
                    width = LowerFeedbackAnimation?.Battlefield2042LowerSelectionViewportWidth ?? 600;
                    height = LowerFeedbackAnimation?.Battlefield2042LowerSelectionViewportHeight ?? 170;
                    centerOffsetY = LowerFeedbackAnimation?.Battlefield2042LowerSelectionViewportCenterOffsetY ?? 45;
                    return;
                case GameStyleMode.Pubg:
                    width = LowerFeedbackAnimation?.PubgLowerSelectionViewportWidth ?? 420;
                    height = LowerFeedbackAnimation?.PubgLowerSelectionViewportHeight ?? 125;
                    centerOffsetY = LowerFeedbackAnimation?.PubgLowerSelectionViewportCenterOffsetY ?? 30;
                    return;
                case GameStyleMode.DeltaForce:
                    width = LowerFeedbackAnimation?.DeltaForceLowerSelectionViewportWidth ?? 360;
                    height = LowerFeedbackAnimation?.DeltaForceLowerSelectionViewportHeight ?? 125;
                    centerOffsetY = LowerFeedbackAnimation?.DeltaForceLowerSelectionViewportCenterOffsetY ?? 37;
                    return;
                default:
                    width = Math.Max(1, LowerFeedbackAnimation?.SelectionViewportWidth ?? 550);
                    height = Math.Max(1, LowerFeedbackAnimation?.SelectionViewportHeight ?? 412.5);
                    centerOffsetX = LowerFeedbackAnimation?.SelectionViewportCenterOffsetX ?? 0;
                    centerOffsetY = LowerFeedbackAnimation?.SelectionViewportCenterOffsetY ?? 0;
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
            _feedbackDragStyle = GameStyleService.Current;
            // Mark selected immediately; drag is armed in PointerMoved once the
            // pointer travels past ClickVsDragThresholdPx. A press without
            // movement leaves the outline selected so the wheel can resize it.
            SelectAnimationFrame(_activeAnimationDragOutline);
            _activeAnimationDragOutline?.CapturePointer(e.Pointer);
            e.Handled = true;
        }
    }
}

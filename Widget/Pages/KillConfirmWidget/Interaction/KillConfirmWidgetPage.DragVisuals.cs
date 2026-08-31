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

        private void OnAnimationDragHintSizeChanged(object sender, SizeChangedEventArgs e)
        {
            // Localized hints can wrap. Keep the entire hint above the frame
            // instead of using a fixed offset sized for a single line.
            if (sender is Border hint && hint.RenderTransform is TranslateTransform transform)
            {
                transform.Y = -e.NewSize.Height - 6.0;
            }
        }

        private static Brush CreateDragOutlineScratchBrush()
        {
            LinearGradientBrush brush = new LinearGradientBrush
            {
                StartPoint = new Point(0, 1),
                EndPoint = new Point(1, 0)
            };
            Color transparent = Colors.Transparent;
            Color scratch = Color.FromArgb(0x58, 0x86, 0x86, 0x86);
            const int stripeCount = 11;
            for (int index = 0; index < stripeCount; index++)
            {
                double start = index / (double)stripeCount;
                double leading = Math.Min(1.0, start + 0.055);
                double scratchStart = Math.Min(1.0, start + 0.060);
                double scratchEnd = Math.Min(1.0, start + 0.073);
                double trailing = Math.Min(1.0, start + 0.078);
                brush.GradientStops.Add(new GradientStop { Color = transparent, Offset = start });
                brush.GradientStops.Add(new GradientStop { Color = transparent, Offset = leading });
                brush.GradientStops.Add(new GradientStop { Color = scratch, Offset = scratchStart });
                brush.GradientStops.Add(new GradientStop { Color = scratch, Offset = scratchEnd });
                brush.GradientStops.Add(new GradientStop { Color = transparent, Offset = trailing });
            }
            return brush;
        }

        private bool IsPointerOnDragOutline(object originalSource)
        {
            if (ReferenceEquals(originalSource, LowerDragOutline)
                || ReferenceEquals(originalSource, CrosshairDragOutline)
                || ReferenceEquals(originalSource, UpperDragOutline))
            {
                return true;
            }
            DependencyObject current = originalSource as DependencyObject;
            while (current != null)
            {
                if (ReferenceEquals(current, LowerDragOutline)
                    || ReferenceEquals(current, CrosshairDragOutline)
                    || ReferenceEquals(current, UpperDragOutline))
                {
                    return true;
                }
                current = VisualTreeHelper.GetParent(current);
            }
            return false;
        }

        private void OnWindowPointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (!_selectedFeedbackLayer.HasValue || IsPointerOnDragOutline(e.OriginalSource)) return;
            _selectedFeedbackLayer = null;
            UpdateAnimationDragOutlineSelectionVisual();
            e.Handled = true;
        }

        private void EndAnimationDrag()
        {
            bool wasDragging = _isDraggingAnimation;
            Border activeOutline = _activeAnimationDragOutline;
            KillFeedbackLayer? layer = GetFeedbackFrameLayer(_activeAnimationDragOutline);
            _isDraggingAnimation = false;
            _animationDragPointerId = 0;
            _activeAnimationDragOutline = null;
            activeOutline?.ReleasePointerCaptures();
            if (wasDragging && layer.HasValue && _feedbackDragStyle == GameStyleService.Current
                && KillFeedbackFrameDefinition.IsSupported(GameStyleService.Current, layer.Value))
            {
                SaveFeedbackFramePlacement(layer.Value);
            }
        }

        private void WireDragElement(UIElement element)
        {
            if (element == null)
            {
                return;
            }

            element.AddHandler(
                UIElement.PointerPressedEvent,
                new PointerEventHandler(OnMoveWindowPointerPressed),
                true);
            element.AddHandler(
                UIElement.PointerMovedEvent,
                new PointerEventHandler(OnMoveWindowPointerMoved),
                true);
            element.AddHandler(
                UIElement.PointerReleasedEvent,
                new PointerEventHandler(OnMoveWindowPointerReleased),
                true);
            element.AddHandler(
                UIElement.PointerCanceledEvent,
                new PointerEventHandler(OnMoveWindowPointerCanceled),
                true);
            element.AddHandler(
                UIElement.PointerCaptureLostEvent,
                new PointerEventHandler(OnMoveWindowPointerCaptureLost),
                true);
        }

        private void OnMoveWindowPointerPressed(object sender, PointerRoutedEventArgs e)
        {
            App.Log("PanelDrag pressed. device=" + e.Pointer.PointerDeviceType);

            if (IsInteractiveControl(e.OriginalSource)
                || (e.Pointer.PointerDeviceType != PointerDeviceType.Mouse
                    && e.Pointer.PointerDeviceType != PointerDeviceType.Touch))
            {
                return;
            }

            _isDraggingPanel = true;
            _dragPointerId = e.Pointer.PointerId;
            _dragPointerStart = e.GetCurrentPoint(Window.Current.Content).Position;
            _panelDragStartX = _panelOffsetX;
            _panelDragStartY = _panelOffsetY;
            if (sender is UIElement element)
            {
                element.CapturePointer(e.Pointer);
            }
            App.Log("PanelDrag started. offset=" + _panelOffsetX + "," + _panelOffsetY);
            e.Handled = true;
        }

        private void OnMoveWindowPointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (!_isDraggingPanel || e.Pointer.PointerId != _dragPointerId)
            {
                return;
            }

            Point current = e.GetCurrentPoint(Window.Current.Content).Position;
            double dx = current.X - _dragPointerStart.X;
            double dy = current.Y - _dragPointerStart.Y;
            SetPanelOffset(_panelDragStartX + dx, _panelDragStartY + dy);
            e.Handled = true;
        }

        private void OnMoveWindowPointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (e.Pointer.PointerId != _dragPointerId)
            {
                return;
            }

            if (sender is UIElement element)
            {
                element.ReleasePointerCapture(e.Pointer);
            }
            EndPanelDrag();
            e.Handled = true;
        }

        private void OnMoveWindowPointerCaptureLost(object sender, PointerRoutedEventArgs e)
        {
            EndPanelDrag();
        }

        private void OnMoveWindowPointerCanceled(object sender, PointerRoutedEventArgs e)
        {
            EndPanelDrag();
            e.Handled = true;
        }

        private void EndPanelDrag()
        {
            if (!_isDraggingPanel)
            {
                return;
            }

            _isDraggingPanel = false;
            _dragPointerId = 0;
            SavePanelOffset();
        }

        private static bool IsInteractiveControl(object originalSource)
        {
            DependencyObject current = originalSource as DependencyObject;
            while (current != null)
            {
                if (current is Button
                    || current is ComboBox
                    || current is ToggleSwitch
                    || current is TextBox
                    || current is CheckBox
                    || current is ListViewItem
                    || current is Slider)
                {
                    return true;
                }
                current = VisualTreeHelper.GetParent(current);
            }
            return false;
        }

        private void OnCollapsePanelToggle(object sender, RoutedEventArgs e)
        {
            // This button only changes the panel presentation. Window-close
            // behavior is handled by App.OnWindowCloseRequested.
            SetPanelCollapsed(!_panelCollapsed);
        }

        private void SetPanelCollapsed(bool collapsed)
        {
            _panelCollapsed = collapsed;
            if (MainPanelContent != null)
            {
                MainPanelContent.Visibility = collapsed ? Visibility.Collapsed : Visibility.Visible;
            }
            if (MiniPanel != null)
            {
                MiniPanel.Visibility = collapsed ? Visibility.Visible : Visibility.Collapsed;
            }
            if (ControlPanel != null)
            {
                ControlPanel.Width = collapsed ? double.NaN : 452;
                ControlPanel.Padding = collapsed
                    ? new Thickness(8, 6, 8, 6)
                    : new Thickness(4);
            }
            ApplicationData.Current.LocalSettings.Values[PanelCollapsedSettingKey] = collapsed;
        }

        private void SetPanelOffset(double x, double y)
        {
            Point clamped = ClampPanelOffset(x, y);
            _panelOffsetX = clamped.X;
            _panelOffsetY = clamped.Y;
            ApplyPanelTransform();
        }

        private Point ClampPanelOffset(double x, double y)
        {
            double panelWidth = ControlPanel.ActualWidth > 0 ? ControlPanel.ActualWidth : DefaultWidgetSize.Width;
            double panelHeight = ControlPanel.ActualHeight > 0 ? ControlPanel.ActualHeight : DefaultWidgetSize.Height;
            double windowWidth = ActualWidth > 0 ? ActualWidth : DefaultWidgetSize.Width;
            double windowHeight = ActualHeight > 0 ? ActualHeight : DefaultWidgetSize.Height;

            // The panel is centered horizontally and top-aligned (Margin 5) at rest.
            double restLeft = (windowWidth - panelWidth) / 2.0;
            double leftAlignedX = -restLeft;
            double rightAlignedX = windowWidth - panelWidth - restLeft;
            double minX = Math.Min(leftAlignedX, rightAlignedX);
            double maxX = Math.Max(leftAlignedX, rightAlignedX);
            double topOffset = 0;
            double bottomOffset = windowHeight
                - panelHeight
                - ControlPanel.Margin.Top
                - ControlPanel.Margin.Bottom;
            double minY = Math.Min(topOffset, bottomOffset);
            double maxY = Math.Max(topOffset, bottomOffset);

            return new Point(
                Math.Max(minX, Math.Min(maxX, x)),
                Math.Max(minY, Math.Min(maxY, y)));
        }

        private void OnPanelViewportSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_isDraggingPanel || ControlPanel == null)
            {
                return;
            }

            Point clamped = ClampPanelOffset(_panelOffsetX, _panelOffsetY);
            _panelOffsetX = clamped.X;
            _panelOffsetY = clamped.Y;
            ApplyPanelTransform();
        }

    }
}

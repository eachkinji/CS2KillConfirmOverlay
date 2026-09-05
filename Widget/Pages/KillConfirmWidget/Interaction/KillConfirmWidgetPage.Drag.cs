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
using Windows.UI.Xaml.Shapes;
using Windows.Web.Http;
using Windows.System;

namespace KillConfirmGameBar
{
    public sealed partial class KillConfirmWidgetPage : Page
    {

        private void OnAnimationFramePointerMoved(object sender, PointerRoutedEventArgs e)
        {
            KillFeedbackLayer? layer = GetFeedbackFrameLayer(_activeAnimationDragOutline);
            if (e.Pointer.PointerId != _animationDragPointerId || !layer.HasValue) return;
            if (_feedbackDragStyle != GameStyleService.Current)
            {
                EndAnimationDrag();
                return;
            }
            Point current = e.GetCurrentPoint(Window.Current.Content).Position;
            double dx = current.X - _animationDragPointerStart.X;
            double dy = current.Y - _animationDragPointerStart.Y;
            if (!_isDraggingAnimation)
            {
                if (dx * dx + dy * dy <= ClickVsDragThresholdPx * ClickVsDragThresholdPx) return;
                _isDraggingAnimation = true;
                Point start = GetFeedbackFramePosition(layer.Value);
                _animationDragStartX = start.X;
                _animationDragStartY = start.Y;
            }
            SetFeedbackFramePosition(layer.Value, _animationDragStartX + dx, _animationDragStartY + dy, save: false);
            e.Handled = true;
        }

        private void OnAnimationFramePointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (e.Pointer.PointerId != _animationDragPointerId)
            {
                return;
            }
            _activeAnimationDragOutline?.ReleasePointerCapture(e.Pointer);
            EndAnimationDrag();
            e.Handled = true;
        }

        private void OnAnimationFramePointerCanceled(object sender, PointerRoutedEventArgs e)
        {
            EndAnimationDrag();
            e.Handled = true;
        }

        private void OnAnimationFramePointerCaptureLost(object sender, PointerRoutedEventArgs e)
        {
            EndAnimationDrag();
        }

        private void OnAnimationFramePointerWheelChanged(object sender, PointerRoutedEventArgs e)
        {
            KillFeedbackLayer? layer = GetFeedbackFrameLayer(sender as Border);
            if (!layer.HasValue || _selectedFeedbackLayer != layer) return;
            int delta = e.GetCurrentPoint(sender as UIElement).Properties.MouseWheelDelta;
            if (delta != 0)
            {
                ScaleFeedbackFrame(layer.Value, delta > 0 ? ScaleUpFactor : ScaleDownFactor);
            }
            e.Handled = true;
        }

        private void OnAnimationFramePointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Border outline && outline.Opacity < 1.0)
            {
                outline.Opacity = 1.0;
            }
        }

        private void OnAnimationFrameContextRequested(
            UIElement sender,
            ContextRequestedEventArgs e)
        {
            Border outline = sender as Border;
            if (outline == null)
            {
                return;
            }

            _animationContextOutline = outline;
            SelectAnimationFrame(outline);

            MenuFlyout menu = new MenuFlyout();
            menu.Items.Add(CreateAnimationFrameMenuItem(
                LocalizationManager.Text("FrameTopFifth"),
                "top",
                "\uE74A"));
            menu.Items.Add(CreateAnimationFrameMenuItem(
                LocalizationManager.Text("FrameCenter"),
                "center",
                "\uE8E3"));
            menu.Items.Add(CreateAnimationFrameMenuItem(
                LocalizationManager.Text("FrameBottomFifth"),
                "bottom",
                "\uE74B"));
            menu.Items.Add(new MenuFlyoutSeparator());
            menu.Items.Add(CreateAnimationFrameMenuItem(
                LocalizationManager.Text("EnlargeTitle"),
                "larger",
                "\uE8A3"));
            menu.Items.Add(CreateAnimationFrameMenuItem(
                LocalizationManager.Text("ShrinkTitle"),
                "smaller",
                "\uE71F"));
            menu.Items.Add(CreateAnimationFrameMenuItem(
                LocalizationManager.Text("MoveUpTitle"),
                "up",
                "\uE74A"));
            menu.Items.Add(CreateAnimationFrameMenuItem(
                LocalizationManager.Text("MoveDownTitle"),
                "down",
                "\uE74B"));
            menu.Items.Add(CreateAnimationFrameMenuItem(
                LocalizationManager.Text("MoveLeftTitle"),
                "left",
                "\uE76B"));
            menu.Items.Add(CreateAnimationFrameMenuItem(
                LocalizationManager.Text("MoveRightTitle"),
                "right",
                "\uE76C"));

            Point position;
            if (e.TryGetPosition(outline, out position))
            {
                menu.ShowAt(outline, position);
            }
            else
            {
                menu.ShowAt(outline);
            }
            e.Handled = true;
        }

        private MenuFlyoutItem CreateAnimationFrameMenuItem(
            string text,
            string command,
            string glyph)
        {
            MenuFlyoutItem item = new MenuFlyoutItem
            {
                Text = text,
                Tag = command,
                Icon = new FontIcon
                {
                    FontFamily = new FontFamily("Segoe MDL2 Assets"),
                    Glyph = glyph
                }
            };
            item.Click += OnAnimationFrameMenuItemClick;
            return item;
        }

        private async void OnAnimationFrameMenuItemClick(object sender, RoutedEventArgs e)
        {
            string command = (sender as FrameworkElement)?.Tag as string;
            Border outline = _animationContextOutline;
            if (outline == null || string.IsNullOrWhiteSpace(command))
            {
                return;
            }

            switch (command)
            {
                case "top":
                    SetAnimationFramePlacement(outline, AnimationPlacementMode.Top);
                    break;
                case "center":
                    SetAnimationFramePlacement(outline, AnimationPlacementMode.Center);
                    await CenterWidgetWindowAsync("animation-frame-menu");
                    break;
                case "bottom":
                    SetAnimationFramePlacement(outline, AnimationPlacementMode.Bottom);
                    break;
                case "larger":
                    ScaleSelectedAnimationFrame(outline, ScaleUpFactor);
                    break;
                case "smaller":
                    ScaleSelectedAnimationFrame(outline, ScaleDownFactor);
                    break;
                case "up":
                    MoveAnimationFrameVertically(outline, -AnimationOffsetStep);
                    break;
                case "down":
                    MoveAnimationFrameVertically(outline, AnimationOffsetStep);
                    break;
                case "left":
                    MoveAnimationFrameHorizontally(outline, -AnimationOffsetStep);
                    break;
                case "right":
                    MoveAnimationFrameHorizontally(outline, AnimationOffsetStep);
                    break;
            }
        }

        private void SelectAnimationFrame(Border outline)
        {
            _selectedFeedbackLayer = GetFeedbackFrameLayer(outline);
            UpdateAnimationDragOutlineSelectionVisual();
        }

        private void SetAnimationFramePlacement(Border outline, AnimationPlacementMode placement)
        {
            KillFeedbackLayer? layer = GetFeedbackFrameLayer(outline);
            if (layer.HasValue) SetFeedbackFramePlacement(layer.Value, placement);
        }

        private void ScaleSelectedAnimationFrame(Border outline, double factor)
        {
            KillFeedbackLayer? layer = GetFeedbackFrameLayer(outline);
            if (layer.HasValue) ScaleFeedbackFrame(layer.Value, factor);
        }

        private void MoveAnimationFrameHorizontally(Border outline, double delta)
        {
            KillFeedbackLayer? layer = GetFeedbackFrameLayer(outline);
            if (!layer.HasValue) return;
            Point position = GetFeedbackFramePosition(layer.Value);
            SetFeedbackFramePosition(layer.Value, position.X + delta, position.Y,
                save: true, preserveVerticalPlacement: true);
        }

        private void MoveAnimationFrameVertically(Border outline, double delta)
        {
            KillFeedbackLayer? layer = GetFeedbackFrameLayer(outline);
            if (!layer.HasValue) return;
            Point position = GetFeedbackFramePosition(layer.Value);
            SetFeedbackFramePosition(layer.Value, position.X, position.Y + delta, save: true);
        }

        private void OnAnimationFramePointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Border outline && GetFeedbackFrameLayer(outline) != _selectedFeedbackLayer)
            {
                outline.Opacity = DragOutlineUnselectedOpacity;
            }
        }

        private void UpdateAnimationDragOutlineSelectionVisual()
        {
            foreach (KillFeedbackLayer layer in Enum.GetValues(typeof(KillFeedbackLayer)))
            {
                ApplyDragOutlineSelectionVisual(layer, _selectedFeedbackLayer == layer);
            }
        }

        private void ApplyDragOutlineSelectionVisual(KillFeedbackLayer layer, bool selected)
        {
            Border outline = GetFeedbackFrameOutline(layer);
            Border hint = GetFeedbackFrameHint(layer);
            Ellipse centerDot = GetFeedbackFrameCenterDot(layer);
            SolidColorBrush brush = GetFeedbackFrameBrush(layer, selected);
            outline.BorderBrush = brush;
            hint.BorderBrush = brush;
            outline.BorderThickness = new Thickness(selected ? DragOutlineSelectedThickness : 2.0);
            outline.Background = selected ? _dragOutlineScratchBrush : _dragOutlineTransparentBrush;
            outline.Opacity = selected ? DragOutlineSelectedOpacity : DragOutlineUnselectedOpacity;
            hint.Visibility = selected ? Visibility.Visible : Visibility.Collapsed;
            centerDot.Visibility = selected ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}

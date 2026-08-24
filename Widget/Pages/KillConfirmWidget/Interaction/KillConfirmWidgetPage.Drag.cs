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

        private void OnAnimationFramePointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (e.Pointer.PointerId != _animationDragPointerId)
            {
                return;
            }

            Point current = e.GetCurrentPoint(Window.Current.Content).Position;
            double dx = current.X - _animationDragPointerStart.X;
            double dy = current.Y - _animationDragPointerStart.Y;
            // Promote a press to a drag only after the cursor moves past the
            // click threshold. Stays a click below that, ready for wheel resize.
            if (!_isDraggingAnimation)
            {
                if (dx * dx + dy * dy <= ClickVsDragThresholdPx * ClickVsDragThresholdPx)
                {
                    return;
                }
                _isDraggingAnimation = true;
                if (_isModernWarfare2019UpperFrameSelected)
                {
                    _animationDragStartX = _modernWarfare2019UpperHorizontalOffset;
                    _animationDragStartY = GetAuxiliaryLayerResolvedVerticalOffset();
                }
                else if (_isOverwatchCardFrameSelected)
                {
                    _animationDragStartX = _overwatchCardHorizontalOffset;
                    _animationDragStartY = GetBottomOffset() + _overwatchCardVerticalOffset;
                }
                else
                {
                    _animationDragStartX = _animationHorizontalOffset;
                    _animationDragStartY = GetResolvedAnimationOffset();
                    _animationPlacement = AnimationPlacementMode.Manual;
                }
            }

            if (_isModernWarfare2019UpperFrameSelected)
            {
                double scale = _modernWarfare2019UpperScale > 0
                    ? _modernWarfare2019UpperScale
                    : 1.0;
                _modernWarfare2019UpperHorizontalOffset = Math.Max(
                    -GetMaxAnimationHorizontalOffset(),
                    Math.Min(
                        GetMaxAnimationHorizontalOffset(),
                        _animationDragStartX + (dx / scale)));
                double resolvedVerticalOffset = Math.Max(
                    -GetMaxAnimationOffset(),
                    Math.Min(
                        GetMaxAnimationOffset(),
                        _animationDragStartY + (dy / scale)));
                _modernWarfare2019UpperVerticalOffset = resolvedVerticalOffset
                    - GetAuxiliaryLayerBaseVerticalOffset();
                ApplyModernWarfare2019UpperTransform();
            }
            else if (_isOverwatchCardFrameSelected)
            {
                double scale = _overwatchCardScale > 0 ? _overwatchCardScale : 1.0;
                _overwatchCardHorizontalOffset = Math.Max(-GetMaxAnimationHorizontalOffset(), Math.Min(
                    GetMaxAnimationHorizontalOffset(),
                    _animationDragStartX + (dx / scale)));
                double resolvedVerticalOffset = Math.Max(-GetMaxAnimationOffset(), Math.Min(
                    GetMaxAnimationOffset(),
                    _animationDragStartY + (dy / scale)));
                _overwatchCardVerticalOffset = resolvedVerticalOffset - GetBottomOffset();
                ApplyOverwatchCardTransform();
            }
            else
            {
                double scale = Controls.KillConfirmAnimation.IsValorantPresentationConfigured
                    ? 1.0
                    : (_animationScale > 0 ? _animationScale : 1.0);
                _animationHorizontalOffset = Math.Max(-GetMaxAnimationHorizontalOffset(), Math.Min(
                    GetMaxAnimationHorizontalOffset(),
                    _animationDragStartX + (dx / scale)));
                _animationOffset = Math.Max(-GetMaxAnimationOffset(), Math.Min(
                    GetMaxAnimationOffset(),
                    _animationDragStartY + (dy / scale)));
                ApplyAnimationTransform();
            }
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
            bool cardFrame = ReferenceEquals(sender, OverwatchCardDragOutline);
            bool upperFrame = ReferenceEquals(sender, ModernWarfare2019UpperDragOutline);
            if ((cardFrame && !_isOverwatchCardFrameSelected)
                || (upperFrame && !_isModernWarfare2019UpperFrameSelected)
                || (!cardFrame && !upperFrame && !_isAnimationFrameSelected))
            {
                return;
            }
            int delta = e.GetCurrentPoint(sender as UIElement).Properties.MouseWheelDelta;
            if (delta != 0)
            {
                double factor = delta > 0 ? ScaleUpFactor : ScaleDownFactor;
                if (upperFrame)
                {
                    ScaleModernWarfare2019Upper(factor);
                }
                else if (cardFrame)
                {
                    ScaleOverwatchCard(factor);
                }
                else
                {
                    ScaleAnimation(factor);
                }
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
            bool cardFrame = ReferenceEquals(outline, OverwatchCardDragOutline);
            bool upperFrame = ReferenceEquals(outline, ModernWarfare2019UpperDragOutline);
            _isAnimationFrameSelected = !cardFrame && !upperFrame;
            _isOverwatchCardFrameSelected = cardFrame;
            _isModernWarfare2019UpperFrameSelected = upperFrame;
            UpdateAnimationDragOutlineSelectionVisual();
        }

        private void SetAnimationFramePlacement(Border outline, AnimationPlacementMode placement)
        {
            double targetVerticalOffset = placement == AnimationPlacementMode.Top
                ? GetTopOffset()
                : placement == AnimationPlacementMode.Bottom
                    ? GetBottomOffset()
                    : 0.0;

            if (ReferenceEquals(outline, ModernWarfare2019UpperDragOutline))
            {
                _modernWarfare2019UpperHorizontalOffset = 0;
                _modernWarfare2019UpperVerticalOffset = targetVerticalOffset
                    - GetAuxiliaryLayerBaseVerticalOffset();
                ApplyModernWarfare2019UpperTransform();
                SaveModernWarfare2019UpperPlacementSettings();
                return;
            }

            if (ReferenceEquals(outline, OverwatchCardDragOutline))
            {
                _overwatchCardHorizontalOffset = 0;
                _overwatchCardVerticalOffset = targetVerticalOffset - GetBottomOffset();
                ApplyOverwatchCardTransform();
                SaveOverwatchCardPlacementSettings();
                return;
            }

            _animationPlacement = placement;
            _animationOffset = targetVerticalOffset;
            _animationHorizontalOffset = 0;
            ApplyAnimationTransform();
            SaveAnimationPlacementSettings();
        }

        private void ScaleSelectedAnimationFrame(Border outline, double factor)
        {
            if (ReferenceEquals(outline, ModernWarfare2019UpperDragOutline))
            {
                ScaleModernWarfare2019Upper(factor);
            }
            else if (ReferenceEquals(outline, OverwatchCardDragOutline))
            {
                ScaleOverwatchCard(factor);
            }
            else
            {
                ScaleAnimation(factor);
            }
        }

        private void MoveAnimationFrameHorizontally(Border outline, double delta)
        {
            double maxOffset = GetMaxAnimationHorizontalOffset();
            if (ReferenceEquals(outline, ModernWarfare2019UpperDragOutline))
            {
                _modernWarfare2019UpperHorizontalOffset = Math.Max(
                    -maxOffset,
                    Math.Min(maxOffset, _modernWarfare2019UpperHorizontalOffset + delta));
                ApplyModernWarfare2019UpperTransform();
                SaveModernWarfare2019UpperPlacementSettings();
                return;
            }

            if (ReferenceEquals(outline, OverwatchCardDragOutline))
            {
                _overwatchCardHorizontalOffset = Math.Max(
                    -maxOffset,
                    Math.Min(maxOffset, _overwatchCardHorizontalOffset + delta));
                ApplyOverwatchCardTransform();
                SaveOverwatchCardPlacementSettings();
                return;
            }

            NudgeAnimationHorizontal(delta);
        }

        private void MoveAnimationFrameVertically(Border outline, double delta)
        {
            double maxOffset = GetMaxAnimationOffset();
            if (ReferenceEquals(outline, ModernWarfare2019UpperDragOutline))
            {
                double resolvedOffset = GetAuxiliaryLayerResolvedVerticalOffset();
                resolvedOffset = Math.Max(
                    -maxOffset,
                    Math.Min(maxOffset, resolvedOffset + delta));
                _modernWarfare2019UpperVerticalOffset = resolvedOffset
                    - GetAuxiliaryLayerBaseVerticalOffset();
                ApplyModernWarfare2019UpperTransform();
                SaveModernWarfare2019UpperPlacementSettings();
                return;
            }

            if (ReferenceEquals(outline, OverwatchCardDragOutline))
            {
                double resolvedOffset = GetBottomOffset()
                    + _overwatchCardVerticalOffset;
                resolvedOffset = Math.Max(
                    -maxOffset,
                    Math.Min(maxOffset, resolvedOffset + delta));
                _overwatchCardVerticalOffset = resolvedOffset - GetBottomOffset();
                ApplyOverwatchCardTransform();
                SaveOverwatchCardPlacementSettings();
                return;
            }

            NudgeAnimation(delta);
        }

        private void OnAnimationFramePointerExited(object sender, PointerRoutedEventArgs e)
        {
            bool selected = ReferenceEquals(sender, OverwatchCardDragOutline)
                ? _isOverwatchCardFrameSelected
                : ReferenceEquals(sender, ModernWarfare2019UpperDragOutline)
                    ? _isModernWarfare2019UpperFrameSelected
                    : _isAnimationFrameSelected;
            if (!selected && sender is Border outline)
            {
                outline.Opacity = DragOutlineUnselectedOpacity;
            }
        }

        private void UpdateAnimationDragOutlineSelectionVisual()
        {
            ApplyDragOutlineSelectionVisual(AnimationDragOutline, _isAnimationFrameSelected);
            ApplyDragOutlineSelectionVisual(OverwatchCardDragOutline, _isOverwatchCardFrameSelected);
            ApplyDragOutlineSelectionVisual(
                ModernWarfare2019UpperDragOutline,
                _isModernWarfare2019UpperFrameSelected);
        }

        private void ApplyDragOutlineSelectionVisual(Border outline, bool selected)
        {
            if (selected)
            {
                outline.BorderBrush = _dragOutlineSelectedBrush;
                outline.BorderThickness = new Thickness(DragOutlineSelectedThickness);
                outline.Background = _dragOutlineScratchBrush;
                outline.Opacity = DragOutlineSelectedOpacity;
            }
            else
            {
                outline.BorderBrush = _dragOutlineDefaultBrush;
                outline.BorderThickness = new Thickness(2.0);
                outline.Background = _dragOutlineTransparentBrush;
                outline.Opacity = DragOutlineUnselectedOpacity;
            }
        }
    }
}

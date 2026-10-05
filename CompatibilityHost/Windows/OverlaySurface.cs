using KillConfirmCompatibility.Contracts;
using KillConfirmCompatibility.Controls;
using Microsoft.Graphics.Canvas;
using System;
using System.Numerics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace KillConfirmCompatibility.Desktop.Windowing
{
    internal sealed class OverlaySurface : Window, IDisposable
    {
        private readonly KillConfirmAnimation _animation;
        private readonly Image _image = new();
        private readonly Border _frame;
        private readonly TextBlock _label;
        private CanvasRenderTarget _target;
        private WriteableBitmap _bitmap;
        private IntPtr _handle;
        private bool _editing, _desiredVisible;
        private NativeWindows.Rect _bounds;
        private ElementLayout _layout;
        private NativeWindows.Point? _dragStart;
        private double _startX, _startY;
        public Action LayoutChanged;
        public Action FinishEditing;
        public Size? FixedSize { get; set; }
        public Func<bool> DirtyOverride { get; set; }
        public Action<CanvasDrawingSession, double, double> DrawOverride { get; set; }
        public string ElementKey { get; }
        public bool IsDragging => _dragStart.HasValue;
        public OverlaySurface(string key, string label, KillConfirmAnimation animation)
        {
            ElementKey = key; _animation = animation;
            Title = "Kill Confirm Compatibility · " + label;
            WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent;
            ShowInTaskbar = false; ShowActivated = false; Topmost = true; ResizeMode = ResizeMode.NoResize;
            Width = 1; Height = 1;
            var grid = new Grid(); grid.Children.Add(_image);
            _frame = new Border { BorderBrush = Brushes.Orange, BorderThickness = new Thickness(2), Background = new SolidColorBrush(Color.FromArgb(15, 255, 180, 30)), Visibility = System.Windows.Visibility.Collapsed };
            _label = new TextBlock { Text = label, Foreground = Brushes.White, Background = Brushes.Black, FontSize = 13, HorizontalAlignment = System.Windows.HorizontalAlignment.Left, VerticalAlignment = System.Windows.VerticalAlignment.Top, Padding = new Thickness(5) };
            _frame.Child = _label; grid.Children.Add(_frame); Content = grid;
            SourceInitialized += (s, e) => { _handle = new WindowInteropHelper(this).Handle; NativeWindows.SetInputMode(_handle, _editing); };
            MouseLeftButtonDown += BeginDrag; MouseMove += MoveDrag; MouseLeftButtonUp += EndDrag;
            LostMouseCapture += (s, e) => { if (_dragStart.HasValue) { _dragStart = null; LayoutChanged?.Invoke(); } };
            MouseWheel += ChangeScale;
            KeyDown += OnKey;
            new WindowInteropHelper(this).EnsureHandle();
        }
        public void Place(NativeWindows.Rect bounds, ElementLayout layout, bool visible, bool editing, double badgeOffset = 0)
        {
            _bounds = bounds; _layout = layout; _desiredVisible = visible && layout.Visible;
            if (_editing != editing)
            {
                _editing = editing; NativeWindows.SetInputMode(_handle, editing);
                _frame.Visibility = editing ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            }
            double dpi = Math.Max(96, NativeWindows.GetDpiForWindow(_handle)) / 96.0;
            double width = Math.Max(80, FixedSize?.Width ?? _animation.DisplayViewportWidth) * layout.Scale;
            double height = Math.Max(50, FixedSize?.Height ?? _animation.DisplayViewportHeight) * layout.Scale;
            int pixelsWide = Math.Max(1, (int)Math.Round(width * dpi));
            int pixelsHigh = Math.Max(1, (int)Math.Round(height * dpi));
            int x = bounds.Left + (int)Math.Round(bounds.Width * layout.X - pixelsWide / 2.0);
            int y = bounds.Top + (int)Math.Round(bounds.Height * layout.Y - pixelsHigh / 2.0 + badgeOffset * layout.Scale * dpi);
            NativeWindows.SetWindowPos(_handle, new IntPtr(-1), x, y, pixelsWide, pixelsHigh, 0x10);
            Opacity = editing ? 1 : Math.Clamp(_animation.Opacity, 0, 1);
            RefreshVisibility();
        }
        private void RefreshVisibility()
        {
            bool show = _desiredVisible && (_editing || _animation.Visibility == global::Windows.UI.Xaml.Visibility.Visible);
            if (show && !IsVisible) Show();
            if (!show && IsVisible) Hide();
        }
        public void Render()
        {
            if (!_editing) Opacity = Math.Clamp(_animation.Opacity, 0, 1);
            RefreshVisibility();
            if (!IsVisible) return;
            int width = Math.Clamp((int)Math.Ceiling(ActualWidth * VisualTreeHelper.GetDpi(this).DpiScaleX), 1, 1536);
            int height = Math.Clamp((int)Math.Ceiling(ActualHeight * VisualTreeHelper.GetDpi(this).DpiScaleY), 1, 1024);
            if (!(DirtyOverride?.Invoke() ?? _animation.IsFrameDirty) && _bitmap != null && _bitmap.PixelWidth == width && _bitmap.PixelHeight == height) return;
            if (_target == null || (int)_target.SizeInPixels.Width != width || (int)_target.SizeInPixels.Height != height)
            {
                _target?.Dispose();
                _target = new CanvasRenderTarget(CanvasDevice.GetSharedDevice(), width, height, 96);
                _bitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Pbgra32, null);
                _image.Source = _bitmap;
            }
            using (var session = _target.CreateDrawingSession())
                if (DrawOverride == null) _animation.DrawDesktopFrame(session, width, height);
                else DrawOverride(session, width, height);
            byte[] pixels = _target.GetPixelBytes();
            _bitmap.WritePixels(new Int32Rect(0, 0, width, height), pixels, width * 4, 0);
        }
        private void BeginDrag(object sender, MouseButtonEventArgs e)
        {
            if (!_editing || _layout == null || !NativeWindows.GetCursorPos(out var point)) return;
            _dragStart = point; _startX = _layout.X; _startY = _layout.Y; CaptureMouse(); Activate(); e.Handled = true;
        }
        private void MoveDrag(object sender, MouseEventArgs e)
        {
            if (!_dragStart.HasValue || !NativeWindows.GetCursorPos(out var point)) return;
            _layout.X = _startX + (point.X - _dragStart.Value.X) / (double)_bounds.Width;
            _layout.Y = _startY + (point.Y - _dragStart.Value.Y) / (double)_bounds.Height;
            _layout.Normalize(); Place(_bounds, _layout, _desiredVisible, _editing);
        }
        private void EndDrag(object sender, MouseButtonEventArgs e)
        {
            if (!_dragStart.HasValue) return;
            _dragStart = null; ReleaseMouseCapture(); LayoutChanged?.Invoke(); e.Handled = true;
        }
        private void ChangeScale(object sender, MouseWheelEventArgs e)
        {
            if (!_editing || _layout == null) return;
            _layout.Scale *= e.Delta > 0 ? 1.05 : 1 / 1.05;
            _layout.Normalize(); Place(_bounds, _layout, _desiredVisible, _editing); LayoutChanged?.Invoke(); e.Handled = true;
        }
        private void OnKey(object sender, KeyEventArgs e)
        {
            if (!_editing || _layout == null) return;
            if (e.Key == Key.Escape || e.Key == Key.Enter) { FinishEditing?.Invoke(); e.Handled = true; return; }
            double step = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? 10 : 1;
            if (e.Key == Key.Left) _layout.X -= step / _bounds.Width;
            else if (e.Key == Key.Right) _layout.X += step / _bounds.Width;
            else if (e.Key == Key.Up) _layout.Y -= step / _bounds.Height;
            else if (e.Key == Key.Down) _layout.Y += step / _bounds.Height;
            else return;
            _layout.Normalize(); Place(_bounds, _layout, _desiredVisible, _editing); LayoutChanged?.Invoke(); e.Handled = true;
        }
        public void Dispose() { _target?.Dispose(); _animation.StopDesktopPlayback(); Close(); }
    }
}

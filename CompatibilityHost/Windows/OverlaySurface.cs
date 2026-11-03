using KillConfirmCompatibility.Contracts;
using KillConfirmCompatibility.Controls;
using Microsoft.Graphics.Canvas;
using System;
using System.Numerics;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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
        private readonly Thumb _resize;
        private readonly string _labelText;
        private double _resizeStartScale, _resizeWidth, _resizeHeight, _resizeX, _resizeY;
        private bool _resizing;
        private CanvasRenderTarget _target;
        private WriteableBitmap _bitmap;
        private byte[] _pixels;
        private global::Windows.Storage.Streams.IBuffer _pixelBuffer;
        private IntPtr _handle;
        private bool _editing, _desiredVisible;
        private NativeWindows.Rect _bounds;
        private ElementLayout _layout;
        private NativeWindows.Point? _dragStart;
        private double _startX, _startY;
        private double _badgeOffset;
        private NativeWindows.Rect _fullPlacement;
        private NativeWindows.Rect? _lastCroppedPlacement;
        private Int32Rect? _lastSourceCrop;
        private bool _croppedPresentation;
        private bool _rendering;
        private LayeredFrameWindow _playbackWindow;
        private long _pixelRevision;
        private Int32Rect _rasterRegion;
        private NativeWindows.Rect _renderedPlacement;
        private bool _editorSelected;
        internal bool IsEditorSelected => _editorSelected;
        internal static Color EditorColor(string key) => key switch
        {
            "Upper" => Color.FromRgb(245, 158, 11),
            "Crosshair" => Color.FromRgb(56, 189, 248),
            "Lower" or "Badge" => Color.FromRgb(52, 211, 153),
            "Danmaku" => Color.FromRgb(192, 132, 252),
            _ => Color.FromRgb(77, 173, 255)
        };
        public Action LayoutChanged;
        public Action FinishEditing;
        public Size? FixedSize { get; set; }
        public bool PreserveRenderAspectRatio { get; set; } = true;
        public Func<bool> DirtyOverride { get; set; }
        public Action<CanvasDrawingSession, double, double> DrawOverride { get; set; }
        public string ElementKey { get; }
        public bool IsDragging => _dragStart.HasValue || _resizing;
        private bool _hasRenderedPixels;
        internal bool IsPresentationVisible => _croppedPresentation ? _playbackWindow?.IsVisible == true : IsVisible;
        internal Size PresentationWindowSize => _croppedPresentation && _lastCroppedPlacement.HasValue
            ? new Size(_lastCroppedPlacement.Value.Width, _lastCroppedPlacement.Value.Height)
            : new Size(ActualWidth * VisualTreeHelper.GetDpi(this).DpiScaleX, ActualHeight * VisualTreeHelper.GetDpi(this).DpiScaleY);
        internal bool HasRenderedPixels => IsPresentationVisible && _animation.Opacity > 0 && _hasRenderedPixels;
        internal long PresentedFrames { get; private set; }
        internal Action FramePresented { get; set; }
        internal Size PresentedSize => new Size(Math.Max(0, _fullPlacement.Width), Math.Max(0, _fullPlacement.Height));
        internal Size RasterSize => _bitmap == null ? default : new Size(_bitmap.PixelWidth, _bitmap.PixelHeight);
        internal Size ContentSize => new Size(_animation.DesktopRenderViewport.Width, _animation.DesktopRenderViewport.Height);
        internal string PlaybackDiagnostics => _animation.DesktopPlaybackDiagnostics;
        internal bool IsPlaying => _animation.IsDesktopPlaying;
        internal double DrawMilliseconds { get; private set; }
        internal double ReadbackMilliseconds { get; private set; }
        internal double CopyMilliseconds { get; private set; }
        internal void ResetPreviewPixels() => _hasRenderedPixels = false;
        internal void ValidateCroppedPlacement(string screenshot = null)
        {
            if (!_croppedPresentation || _bitmap == null) throw new InvalidOperationException("Cropped feedback frame was not presented.");
            if (_playbackWindow == null || !_lastCroppedPlacement.HasValue) throw new InvalidOperationException("No atomic feedback frame was committed.");
            NativeWindows.GetWindowRect(_playbackWindow.Handle, out var window);
            if (!window.Equals(_lastCroppedPlacement.Value) || !_playbackWindow.Placement.Equals(window))
                throw new InvalidOperationException("Committed feedback pixels and native window geometry differ.");
            if (!_renderedPlacement.Equals(_fullPlacement)) throw new InvalidOperationException("A resized frame reused stale raster pixels.");
            for (int y = 0; y < _bitmap.PixelHeight; y++)
            for (int x = 0; x < _bitmap.PixelWidth; x++)
                if (_pixels[(y * _bitmap.PixelWidth + x) * 4 + 3] != 0 &&
                    (_fullPlacement.Left + _rasterRegion.X + x < window.Left ||
                     _fullPlacement.Left + _rasterRegion.X + x + 1 > window.Right ||
                     _fullPlacement.Top + _rasterRegion.Y + y < window.Top ||
                     _fullPlacement.Top + _rasterRegion.Y + y + 1 > window.Bottom))
                    throw new InvalidOperationException("Feedback window clipped a visible effect pixel.");
            if (screenshot != null)
            {
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(_playbackWindow.CopyPresentedBitmap()));
                using var stream = System.IO.File.Create(screenshot); encoder.Save(stream);
            }
        }
        internal void ValidateCropLayoutTransitions()
        {
            double originalScale = _layout.Scale;
            bool visible = _desiredVisible;
            Place(_bounds, _layout, visible, true, _badgeOffset);
            UpdateLayout();
            Render();
            if (_croppedPresentation || _image.Stretch != Stretch.Fill)
                throw new InvalidOperationException("Layout editor did not retain native-resolution placement.");
            _layout.Scale = originalScale * 1.25;
            Place(_bounds, _layout, visible, false, _badgeOffset);
            ValidateCroppedPlacement();
            Render(); ValidateCroppedPlacement();
            _layout.Scale = originalScale;
            Place(_bounds, _layout, visible, false, _badgeOffset);
            ValidateCroppedPlacement();
            Render(); ValidateCroppedPlacement();
        }
        internal void SavePreviewImage(string path)
        {
            if (_bitmap == null) throw new InvalidOperationException("No rendered bitmap.");
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(_bitmap));
            using var stream = System.IO.File.Create(path);
            encoder.Save(stream);
        }
        internal void ValidatePixelStripes(int startX, int startY, int count)
        {
            if (_bitmap == null || !_renderedPlacement.Equals(_fullPlacement)) throw new InvalidOperationException("No current native-resolution raster.");
            for (int x = 0; x < count; x++)
            {
                int localX = startX + x - _rasterRegion.X, localY = startY - _rasterRegion.Y;
                if (localX < 0 || localX >= _bitmap.PixelWidth || localY < 0 || localY >= _bitmap.PixelHeight)
                    throw new InvalidOperationException("Native-resolution detail was cropped.");
                byte alpha = _pixels[(localY * _bitmap.PixelWidth + localX) * 4 + 3];
                if (alpha != (x % 2 == 0 ? 255 : 0)) throw new InvalidOperationException("A one-pixel detail was blurred by resampling.");
            }
        }
        internal (int Different, int Missing, int Added, int MaxError) CompareCommittedReference(byte[] reference, int width, int height)
        {
            ValidateCroppedPlacement();
            if (_fullPlacement.Width != width || _fullPlacement.Height != height) throw new InvalidOperationException("Playback changed scale in the middle of a frame.");
            var committed = _playbackWindow.CopyPresentedBitmap();
            var actual = new byte[committed.PixelWidth * committed.PixelHeight * 4];
            committed.CopyPixels(actual, committed.PixelWidth * 4, 0);
            var placement = _playbackWindow.Placement;
            int different = 0, missing = 0, added = 0, maximum = 0;
            for (int screenY = Math.Max(_bounds.Top, _fullPlacement.Top); screenY < Math.Min(_bounds.Bottom, _fullPlacement.Bottom); screenY++)
            for (int screenX = Math.Max(_bounds.Left, _fullPlacement.Left); screenX < Math.Min(_bounds.Right, _fullPlacement.Right); screenX++)
            {
                int expectedIndex = ((screenY - _fullPlacement.Top) * width + screenX - _fullPlacement.Left) * 4;
                int x = screenX - placement.Left, y = screenY - placement.Top;
                int index = x >= 0 && y >= 0 && x < committed.PixelWidth && y < committed.PixelHeight ? (y * committed.PixelWidth + x) * 4 : -1;
                int error = 0;
                for (int channel = 0; channel < 4; channel++) error = Math.Max(error, Math.Abs(reference[expectedIndex + channel] - (index < 0 ? 0 : actual[index + channel])));
                maximum = Math.Max(maximum, error);
                if (error > 3) different++;
                if (reference[expectedIndex + 3] > 8 && (index < 0 || actual[index + 3] == 0)) missing++;
                if (index >= 0 && actual[index + 3] > 8 && reference[expectedIndex + 3] == 0) added++;
            }
            return (different, missing, added, maximum);
        }
        public OverlaySurface(string key, string label, KillConfirmAnimation animation)
        {
            ElementKey = key; _animation = animation; _labelText = label;
            _animation.LogicalViewportSizeChanged += OnViewportSizeChanged;
            Title = "Kill Confirm Compatibility · " + label;
            WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent;
            ShowInTaskbar = false; ShowActivated = false; Topmost = true; ResizeMode = ResizeMode.NoResize;
            Width = 1; Height = 1;
            var grid = new Grid(); grid.Children.Add(_image);
            var color = EditorColor(key);
            _frame = new Border { BorderBrush = new SolidColorBrush(color), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(5), Background = Brushes.Transparent, Visibility = System.Windows.Visibility.Collapsed };
            _label = new TextBlock { Text = label, Foreground = new SolidColorBrush(Color.FromRgb(15, 23, 42)), Background = new SolidColorBrush(color), FontSize = 12, FontWeight = System.Windows.FontWeights.SemiBold, HorizontalAlignment = System.Windows.HorizontalAlignment.Left, VerticalAlignment = System.Windows.VerticalAlignment.Top, Padding = new Thickness(7, 4, 7, 4) };
            _frame.Child = _label; grid.Children.Add(_frame); Content = grid;
            _resize = new Thumb { Width = 18, Height = 18, HorizontalAlignment = System.Windows.HorizontalAlignment.Right, VerticalAlignment = System.Windows.VerticalAlignment.Bottom, Cursor = Cursors.SizeNWSE, Background = new SolidColorBrush(color), Visibility = System.Windows.Visibility.Collapsed };
            grid.Children.Add(_resize);
            _resize.DragStarted += (s,e) => { _resizing = true; _resizeStartScale = _layout.Scale; _resizeWidth = ActualWidth; _resizeHeight = ActualHeight; _resizeX = _resizeY = 0; };
            _resize.DragDelta += (s,e) => {
                _resizeX += e.HorizontalChange; _resizeY += e.VerticalChange;
                _layout.Scale = _resizeStartScale * Math.Max(0.1, 1 + (_resizeX * _resizeWidth + _resizeY * _resizeHeight) / Math.Max(1, _resizeWidth * _resizeWidth + _resizeHeight * _resizeHeight));
                _layout.Normalize(); Place(_bounds, _layout, _desiredVisible, _editing);
            };
            _resize.DragCompleted += (s,e) => { _resizing = false; LayoutChanged?.Invoke(); };
            var menu = new ContextMenu();
            var center = new MenuItem { Header = "居中" }; center.Click += (s,e) => { if (_layout == null) return; _layout.X = _layout.Y = 0.5; UpdateLayoutPlacement(); }; menu.Items.Add(center);
            var reset = new MenuItem { Header = "恢复这个元素的位置与大小" }; reset.Click += (s,e) => {
                if (_layout == null) return;
                var fresh = new DisplayConfiguration().GetLayout(KillConfirmCompatibility.Services.GameStyleService.ToStorageValue(KillConfirmCompatibility.Services.GameStyleService.Current)).GetElement(ElementKey);
                _layout.X = fresh.X; _layout.Y = fresh.Y; _layout.Scale = fresh.Scale; UpdateLayoutPlacement();
            }; menu.Items.Add(reset);
            var visible = new MenuItem { Header = "显示 / 隐藏这个元素" }; visible.Click += (s,e) => { if (_layout == null) return; _layout.Visible = !_layout.Visible; UpdateLayoutPlacement(); }; menu.Items.Add(visible);
            _frame.ContextMenu = menu;
            SourceInitialized += (s, e) => { _handle = new WindowInteropHelper(this).Handle; NativeWindows.SetInputMode(_handle, _editing && _editorSelected); };
            MouseLeftButtonDown += BeginDrag; MouseMove += MoveDrag; MouseLeftButtonUp += EndDrag;
            LostMouseCapture += (s, e) => { if (_dragStart.HasValue) { _dragStart = null; LayoutChanged?.Invoke(); } };
            MouseWheel += ChangeScale;
            KeyDown += OnKey;
            new WindowInteropHelper(this).EnsureHandle();
        }
        internal void SetEditorSelected(bool selected)
        {
            if (_editorSelected == selected) return;
            _editorSelected = selected;
            if (!selected)
            {
                _dragStart = null;
                if (_resize.IsDragging) _resize.CancelDrag();
                _resizing = false;
                if (IsMouseCaptured) ReleaseMouseCapture();
            }
            ApplyEditorSelection();
        }
        private void ApplyEditorSelection()
        {
            bool active = _editing && _editorSelected;
            NativeWindows.SetInputMode(_handle, active);
            var color = EditorColor(ElementKey);
            _frame.BorderThickness = new Thickness(active ? 3 : 1);
            _frame.Background = active ? new SolidColorBrush(Color.FromArgb(24, color.R, color.G, color.B)) : Brushes.Transparent;
            _resize.Visibility = active ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            if (_layout != null) _label.Text = $"{_labelText} · {_layout.Scale:P0}" + (active ? " · 正在编辑" : " · 点击顶部按钮选择") + (_layout.Visible ? "" : " · 已隐藏");
        }
        public void Place(NativeWindows.Rect bounds, ElementLayout layout, bool visible, bool editing, double badgeOffset = 0)
        {
            _badgeOffset = badgeOffset;
            _bounds = bounds; _layout = layout; _desiredVisible = visible && (editing || layout.Visible);
            if (_editing != editing)
            {
                if (!editing)
                {
                    _dragStart = null;
                    if (_resize.IsDragging) _resize.CancelDrag();
                    _resizing = false;
                    if (IsMouseCaptured) ReleaseMouseCapture();
                }
                _editing = editing;
                _frame.Visibility = editing ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
                ApplyEditorSelection();
            }
            if (editing) _label.Text = $"{_labelText} · {layout.Scale:P0}" + (_editorSelected ? " · 正在编辑" : " · 点击顶部按钮选择") + (layout.Visible ? "" : " · 已隐藏");
            double dpi = Math.Max(96, NativeWindows.GetDpiForWindow(_handle)) / 96.0;
            var viewport = _animation.DesktopRenderViewport;
            double fit = _animation.DesktopDisplayFit;
            double width = Math.Max(80, FixedSize?.Width ?? viewport.Width * fit) * layout.Scale;
            double height = Math.Max(50, FixedSize?.Height ?? viewport.Height * fit) * layout.Scale;
            int pixelsWide = Math.Max(1, (int)Math.Round(width * dpi));
            int pixelsHigh = Math.Max(1, (int)Math.Round(height * dpi));
            double offsetX = FixedSize.HasValue ? 0 : (viewport.X + viewport.Width / 2 - _animation.LogicalViewportWidth / 2) * fit;
            double offsetY = FixedSize.HasValue ? 0 : (viewport.Y + viewport.Height / 2 - _animation.LogicalViewportHeight / 2) * fit;
            int x = bounds.Left + (int)Math.Round(bounds.Width * layout.X - pixelsWide / 2.0 + offsetX * layout.Scale * dpi);
            int y = bounds.Top + (int)Math.Round(bounds.Height * layout.Y - pixelsHigh / 2.0 + (badgeOffset + offsetY) * layout.Scale * dpi);
            var previousPlacement = _fullPlacement;
            _fullPlacement = new NativeWindows.Rect { Left = x, Top = y, Right = x + pixelsWide, Bottom = y + pixelsHigh };
            bool crop = !_editing && DrawOverride == null;
            bool wasCropped = _croppedPresentation;
            // SetWindowPos may pump rendering callbacks; publish the new mode
            // before resizing so entering the layout editor cannot re-crop it.
            _croppedPresentation = crop;
            if (crop && IsVisible) Hide();
            if (!crop) _playbackWindow?.Hide();
            if (!crop || !wasCropped)
            {
                _image.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
                _image.VerticalAlignment = System.Windows.VerticalAlignment.Top;
                _image.Stretch = Stretch.Fill;
                _lastCroppedPlacement = null;
                _lastSourceCrop = null;
                NativeWindows.SetWindowPos(_handle, new IntPtr(-1), x, y, pixelsWide, pixelsHigh, 0x10);
            }
            else if (!previousPlacement.Equals(_fullPlacement))
            {
                // Keep the hidden editor/probe on the correct monitor for DPI.
                NativeWindows.SetWindowPos(_handle, new IntPtr(-1), x, y, pixelsWide, pixelsHigh, 0x10);
            }
            Opacity = editing ? 1 : Math.Clamp(_animation.Opacity, 0, 1);
            RefreshVisibility();
            // Re-rasterize at the new physical size before committing pixels
            // and placement together. Never stretch the previous small frame.
            if (!_rendering && (!wasCropped || !previousPlacement.Equals(_fullPlacement))) Render();
        }
        private void OnViewportSizeChanged(object sender, EventArgs args)
        {
            if (_layout != null) Place(_bounds, _layout, _desiredVisible, _editing, _badgeOffset);
        }
        internal void AdvanceAndRender()
        {
            if (_rendering) return;
            _animation.AdvanceDesktopFrame();
            Render();
        }
        private void RefreshVisibility()
        {
            bool show = _desiredVisible && (_editing || _animation.Visibility == global::Windows.UI.Xaml.Visibility.Visible);
            if (_croppedPresentation)
            {
                if (IsVisible) Hide();
                if (!show) _playbackWindow?.Hide();
                return;
            }
            if (show && !IsVisible) Show();
            if (!show && IsVisible) Hide();
        }
        public void Render()
        {
            if (_rendering) return;
            _rendering = true;
            try { RenderCore(); }
            finally { _rendering = false; }
        }
        private void RenderCore()
        {
            if (!_editing) Opacity = Math.Clamp(_animation.Opacity, 0, 1);
            RefreshVisibility();
            if (_croppedPresentation ? !_desiredVisible || _animation.Visibility != global::Windows.UI.Xaml.Visibility.Visible : !IsVisible) return;
            int fullWidth = Math.Max(1, _fullPlacement.Width), fullHeight = Math.Max(1, _fullPlacement.Height);
            bool placementChanged = !_renderedPlacement.Equals(_fullPlacement);
            if (!(DirtyOverride?.Invoke() ?? _animation.IsFrameDirty) && _bitmap != null && !placementChanged)
            {
                if (_croppedPresentation) PresentCroppedFrame(_pixels, _bitmap.PixelWidth, _bitmap.PixelHeight);
                UpdateEditorImage();
                return;
            }
            long drawStart = System.Diagnostics.Stopwatch.GetTimestamp();
            var device = CanvasDevice.GetSharedDevice();
            // Record vector/text/image commands at the final screen scale. Their
            // conservative bounds let us omit empty pixels without a low-res
            // prepass, and without losing small details or effect fringes.
            using var commands = new CanvasCommandList(device);
            using (var recording = commands.CreateDrawingSession())
                if (DrawOverride == null) _animation.DrawDesktopFrame(recording, fullWidth, fullHeight, cropEmptySpace: true, clearTarget: false);
                else DrawOverride(recording, fullWidth, fullHeight);
            var region = CalculateRasterRegion(commands.GetBounds(device), _fullPlacement, _bounds);
            if (!placementChanged && Contains(_rasterRegion, region) &&
                (long)_rasterRegion.Width * _rasterRegion.Height <= (long)region.Width * region.Height * 2)
                region = _rasterRegion;
            if (_rasterRegion != region) _lastSourceCrop = null;
            _rasterRegion = region;
            int width = region.Width, height = region.Height;
            if (_target == null || (int)_target.SizeInPixels.Width != width || (int)_target.SizeInPixels.Height != height)
            {
                _target?.Dispose();
                _lastSourceCrop = null;
                _target = new CanvasRenderTarget(device, width, height, 96);
                _bitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Pbgra32, null);
                _pixels = new byte[checked(width * height * 4)];
                _pixelBuffer = _pixels.AsBuffer();
                _image.Source = _bitmap;
            }
            using (var session = _target.CreateDrawingSession())
            {
                session.Clear(Colors.Transparent);
                session.Transform = Matrix3x2.CreateTranslation(-region.X, -region.Y);
                session.DrawImage(commands);
            }
            _renderedPlacement = _fullPlacement;
            UpdateEditorImage();
            long readStart = System.Diagnostics.Stopwatch.GetTimestamp();
            _target.GetPixelBytes(_pixelBuffer);
            long copyStart = System.Diagnostics.Stopwatch.GetTimestamp();
            byte[] pixels = _pixels;
            _hasRenderedPixels = false;
            for (int index = 3; index < pixels.Length; index += 4)
                if (pixels[index] != 0) { _hasRenderedPixels = true; break; }
            _bitmap.WritePixels(new Int32Rect(0, 0, width, height), pixels, width * 4, 0);
            _pixelRevision++;
            if (_croppedPresentation) PresentCroppedFrame(pixels, width, height);
            DrawMilliseconds += System.Diagnostics.Stopwatch.GetElapsedTime(drawStart, readStart).TotalMilliseconds;
            ReadbackMilliseconds += System.Diagnostics.Stopwatch.GetElapsedTime(readStart, copyStart).TotalMilliseconds;
            CopyMilliseconds += System.Diagnostics.Stopwatch.GetElapsedTime(copyStart).TotalMilliseconds;
            PresentedFrames++;
            FramePresented?.Invoke();
        }
        // Keep animation geometry unchanged, but do not make WPF composite a
        // 1100x900 transparent canvas (multiplied by desktop DPI) every frame.
        // Include every nonzero alpha pixel and a filtering guard around it.
        internal static Int32Rect VisiblePixelBounds(byte[] pixels, int width, int height)
        {
            int left = width, top = height, right = -1, bottom = -1;
            for (int y = 0; y < height; y++)
            for (int x = 0, i = (y * width) * 4 + 3; x < width; x++, i += 4)
                if (pixels[i] != 0) { left = Math.Min(left, x); right = Math.Max(right, x); top = Math.Min(top, y); bottom = y; }
            if (right < 0) return new Int32Rect(0, 0, 1, 1);
            // Quantize outward so small alpha changes do not resize the native
            // window on every fade sample. Never remove a nonzero pixel.
            left = Math.Max(0, left - 2) / 16 * 16; top = Math.Max(0, top - 2) / 16 * 16;
            right = Math.Min(width, (right + 18) / 16 * 16); bottom = Math.Min(height, (bottom + 18) / 16 * 16);
            return new Int32Rect(left, top, right - left, bottom - top);
        }
        private void PresentCroppedFrame(byte[] pixels, int width, int height)
        {
            if (!_desiredVisible || _animation.Visibility != global::Windows.UI.Xaml.Visibility.Visible) { _playbackWindow?.Hide(); return; }
            var crop = VisiblePixelBounds(pixels, width, height);
            // Keep a small amount of spare transparent space while fading.
            // Moving/resizing a layered HWND itself can stall composition.
            if (_lastSourceCrop is Int32Rect previous && previous.X <= crop.X && previous.Y <= crop.Y &&
                previous.X + previous.Width >= crop.X + crop.Width && previous.Y + previous.Height >= crop.Y + crop.Height &&
                (long)previous.Width * previous.Height <= (long)crop.Width * crop.Height * 2)
                crop = previous;
            _lastSourceCrop = crop;
            // One raster pixel is one physical screen pixel at every zoom.
            int x = _rasterRegion.X + crop.X, y = _rasterRegion.Y + crop.Y;
            var placement = new NativeWindows.Rect { Left = _fullPlacement.Left + x, Top = _fullPlacement.Top + y,
                Right = _fullPlacement.Left + x + crop.Width, Bottom = _fullPlacement.Top + y + crop.Height };
            _playbackWindow ??= new LayeredFrameWindow(Title + " · Playback");
            _playbackWindow.Present(_bitmap, _pixelRevision, width, height, crop, placement, _animation.Opacity);
            _lastCroppedPlacement = placement;
        }
        private void UpdateEditorImage()
        {
            if (_croppedPresentation || _bitmap == null) return;
            var dpi = VisualTreeHelper.GetDpi(this);
            _image.Width = _bitmap.PixelWidth / dpi.DpiScaleX;
            _image.Height = _bitmap.PixelHeight / dpi.DpiScaleY;
            _image.Margin = new Thickness(_rasterRegion.X / dpi.DpiScaleX, _rasterRegion.Y / dpi.DpiScaleY, 0, 0);
        }
        private static bool Contains(Int32Rect outer, Int32Rect inner) => outer.Width > 0 && outer.Height > 0 &&
            outer.X <= inner.X && outer.Y <= inner.Y && outer.X + outer.Width >= inner.X + inner.Width && outer.Y + outer.Height >= inner.Y + inner.Height;
        internal static Int32Rect CalculateRasterRegion(global::Windows.Foundation.Rect content, NativeWindows.Rect placement, NativeWindows.Rect screen)
        {
            // Clip only outside the selected screen, never reduce resolution.
            double left = Math.Max(0, screen.Left - placement.Left), top = Math.Max(0, screen.Top - placement.Top);
            double right = Math.Min(placement.Width, screen.Right - placement.Left), bottom = Math.Min(placement.Height, screen.Bottom - placement.Top);
            if (double.IsFinite(content.X) && double.IsFinite(content.Y) && double.IsFinite(content.Width) && double.IsFinite(content.Height))
            {
                left = Math.Max(left, Math.Floor((content.X - 4) / 32) * 32);
                top = Math.Max(top, Math.Floor((content.Y - 4) / 32) * 32);
                right = Math.Min(right, Math.Ceiling((content.X + content.Width + 4) / 32) * 32);
                bottom = Math.Min(bottom, Math.Ceiling((content.Y + content.Height + 4) / 32) * 32);
            }
            if (right <= left || bottom <= top) return new Int32Rect(0, 0, 1, 1);
            return new Int32Rect((int)left, (int)top, (int)(right - left), (int)(bottom - top));
        }
        internal static Size CalculateRenderSize(double width, double height, bool preserveAspectRatio) =>
            new Size(Math.Max(1, Math.Round(width)), Math.Max(1, Math.Round(height)));
        private void BeginDrag(object sender, MouseButtonEventArgs e)
        {
            if (!_editing || !_editorSelected || _layout == null || _resizing || e.OriginalSource == _resize || !NativeWindows.GetCursorPos(out var point)) return;
            _dragStart = point; _startX = _layout.X; _startY = _layout.Y; CaptureMouse(); Activate(); e.Handled = true;
        }
        private void UpdateLayoutPlacement() { Place(_bounds, _layout, true, _editing); LayoutChanged?.Invoke(); }
        private void MoveDrag(object sender, MouseEventArgs e)
        {
            if (_dragStart.HasValue && e.LeftButton != MouseButtonState.Pressed)
            {
                _dragStart = null; ReleaseMouseCapture(); LayoutChanged?.Invoke(); return;
            }
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
            if (!_editing || !_editorSelected || _layout == null) return;
            _layout.Scale *= e.Delta > 0 ? 1.05 : 1 / 1.05;
            _layout.Normalize(); Place(_bounds, _layout, _desiredVisible, _editing); LayoutChanged?.Invoke(); e.Handled = true;
        }
        internal void HandleEditorKey(KeyEventArgs e) => OnKey(this, e);
        private void OnKey(object sender, KeyEventArgs e)
        {
            if (!_editing || !_editorSelected || _layout == null) return;
            if (e.Key == Key.Escape || e.Key == Key.Enter) { FinishEditing?.Invoke(); e.Handled = true; return; }
            double step = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? 10 : 1;
            if (e.Key == Key.Left) _layout.X -= step / _bounds.Width;
            else if (e.Key == Key.Right) _layout.X += step / _bounds.Width;
            else if (e.Key == Key.Up) _layout.Y -= step / _bounds.Height;
            else if (e.Key == Key.Down) _layout.Y += step / _bounds.Height;
            else return;
            _layout.Normalize(); Place(_bounds, _layout, _desiredVisible, _editing); LayoutChanged?.Invoke(); e.Handled = true;
        }
        public void Dispose() { _animation.LogicalViewportSizeChanged -= OnViewportSizeChanged; _playbackWindow?.Dispose(); _target?.Dispose(); _animation.StopDesktopPlayback(); Close(); }
    }
}

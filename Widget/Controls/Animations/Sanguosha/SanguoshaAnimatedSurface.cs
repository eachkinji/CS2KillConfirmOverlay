using System;
using System.Diagnostics;
using System.Numerics;
using KillConfirmCompatibility.Contracts;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Windows.Foundation;
using Windows.Graphics.DirectX;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Xaml;

namespace KillConfirmGameBar.Controls
{
    // Only SGS uses this swap-chain/game-loop surface. No XAML state is accessed
    // from Draw: its source, dimensions, DPI and clock are immutable snapshots.
    internal sealed class SanguoshaAnimatedSurface : IDisposable
    {
        private readonly object _gate = new object();
        private readonly CoreDispatcher _dispatcher;
        internal CanvasAnimatedControl Control { get; private set; }
        private SanguoshaLz4Playback _source;
        private CanvasBitmap _bitmap;
        private int _uploaded = -1, _revision;
        private int _width, _height;
        private float _dpi;
        private long _started;
        private double _offset, _duration;
        private bool _running, _loop, _ended, _disposed;
        private Action _completed;
        private Action<Exception> _failed;
        internal event Action<int, bool, double, double> FrameDrawn;
        internal int UploadedFrame { get { lock (_gate) return _uploaded; } }
        internal bool Loop { get { lock (_gate) return _loop; } set { lock (_gate) _loop = value; } }
        internal bool StaticFrame { get; set; }
        internal SanguoshaAnimatedSurface(CoreDispatcher dispatcher)
        {
            _dispatcher = dispatcher;
            Control = new CanvasAnimatedControl {
                UseSharedDevice = true, DpiScale = 1, ClearColor = Colors.Transparent,
                IsFixedTimeStep = false, TargetElapsedTime = TimeSpan.FromTicks(166667), Paused = true,
                HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Visibility = Visibility.Collapsed
            };
            Control.Draw += OnDraw;
            Control.CreateResources += OnCreateResources;
        }
        private void OnCreateResources(CanvasAnimatedControl sender, CanvasCreateResourcesEventArgs args)
        {
            lock (_gate) { _bitmap?.Dispose(); _bitmap = null; _uploaded = -1; }
        }
        internal void Start(SanguoshaLz4Playback source, int width, int height, float widthDips, float heightDips, float dpi,
            double offsetMs, Action completed, Action<Exception> failed)
        {
            Control.Paused = true;
            lock (_gate)
            {
                if (_disposed) throw new ObjectDisposedException("SanguoshaAnimatedSurface");
                if (!ReferenceEquals(_source, source) || _width != width || _height != height || _dpi != dpi)
                { _bitmap?.Dispose(); _bitmap = null; _uploaded = -1; }
                _source = source; _width = width; _height = height; _dpi = dpi;
                _offset = offsetMs; _duration = source.Source.Duration.TotalMilliseconds;
                _started = Stopwatch.GetTimestamp(); _running = true; _ended = false; _revision++;
                _completed = completed; _failed = failed;
            }
            Control.Width = widthDips; Control.Height = heightDips; Control.Visibility = Visibility.Visible;
            Control.ResetElapsedTime(); Control.Paused = false;
        }
        internal void Pause()
        {
            Control.Paused = true;
            lock (_gate) { _running = false; _revision++; _completed = null; _failed = null; }
            Control.Visibility = Visibility.Collapsed;
        }
        internal void DetachSource()
        {
            Pause();
            lock (_gate) { _source = null; _bitmap?.Dispose(); _bitmap = null; _uploaded = -1; }
        }
        private void OnDraw(ICanvasAnimatedControl sender, CanvasAnimatedDrawEventArgs args)
        {
            long began = Stopwatch.GetTimestamp();
            lock (_gate)
            {
                args.DrawingSession.Clear(Colors.Transparent);
                if (!_running || _source == null || _disposed) return;
                double elapsed = _offset + (Stopwatch.GetTimestamp() - _started) * 1000d / Stopwatch.Frequency;
                if (_loop) elapsed %= _duration;
                if (elapsed >= _duration)
                {
                    if (!_ended) { _ended = true; QueueCompletion(_revision, null); }
                    return;
                }
                int frame = StaticFrame ? 80 : _source.Source.FrameAt(TimeSpan.FromTicks((long)(elapsed * TimeSpan.TicksPerMillisecond)));
                bool uploaded = false; double uploadMs = 0;
                try
                {
                    // Catch decoder/storage failures, while Win2D device-loss
                    // exceptions from graphics operations remain owned by Win2D.
                    if (_bitmap == null || _uploaded != frame)
                    {
                        long uploadStart = Stopwatch.GetTimestamp();
                        using (var lease = _source.AcquireFrame(frame)) Upload(args.DrawingSession, lease.Bytes, frame);
                        uploadMs = (Stopwatch.GetTimestamp() - uploadStart) * 1000d / Stopwatch.Frequency; uploaded = true;
                    }
                }
                catch (InvalidOperationException error) { QueueCompletion(_revision, error); _running = false; return; }
                catch (System.IO.IOException error) { QueueCompletion(_revision, error); _running = false; return; }
                catch (TimeoutException error) { QueueCompletion(_revision, error); _running = false; return; }
                DrawBitmap(args.DrawingSession);
                FrameDrawn?.Invoke(frame, uploaded, (Stopwatch.GetTimestamp() - began) * 1000d / Stopwatch.Frequency, uploadMs);
            }
        }
        // Used by the focused pixel validation as well as by the live game loop.
        internal void DrawExactFrame(CanvasDrawingSession session, int frame)
        {
            lock (_gate)
            {
                if (_source == null) throw new InvalidOperationException("No SGS animated source.");
                if (_bitmap == null || _uploaded != frame) using (var lease = _source.AcquireFrame(frame)) Upload(session, lease.Bytes, frame);
                DrawBitmap(session);
            }
        }
        private void Upload(CanvasDrawingSession session, byte[] bytes, int frame)
        {
            if (Math.Abs(session.Dpi - _dpi) > .001) throw new InvalidOperationException("Animated surface DPI differs from the exact cached raster.");
            if (_bitmap == null) _bitmap = CanvasBitmap.CreateFromBytes(session.Device, bytes, _width, _height, DirectXPixelFormat.B8G8R8A8UIntNormalized, _dpi, CanvasAlphaMode.Premultiplied);
            else _bitmap.SetPixelBytes(bytes);
            _uploaded = frame;
        }
        private void DrawBitmap(CanvasDrawingSession session)
        {
            session.Transform = Matrix3x2.Identity;
            session.DrawImage(_bitmap, Vector2.Zero, new Rect(0, 0, _bitmap.Size.Width, _bitmap.Size.Height), 1,
                CanvasImageInterpolation.NearestNeighbor, CanvasComposite.Copy);
        }
        private async void QueueCompletion(int revision, Exception error)
        {
            try
            {
                await _dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => {
                    Action complete; Action<Exception> fail;
                    lock (_gate) { if (_disposed || revision != _revision) return; complete = _completed; fail = _failed; }
                    if (error == null) complete?.Invoke(); else fail?.Invoke(error);
                });
            }
            catch (Exception) { /* The widget/dispatcher can disappear while it closes. */ }
        }
        public void Dispose()
        {
            if (_disposed) return;
            Control.Paused = true;
            lock (_gate)
            {
                _disposed = true; _running = false; _revision++; _source = null; _completed = null; _failed = null; FrameDrawn = null;
                _bitmap?.Dispose(); _bitmap = null;
            }
            Control.Draw -= OnDraw; Control.CreateResources -= OnCreateResources;
            Control.RemoveFromVisualTree(); Control = null;
        }
    }
}

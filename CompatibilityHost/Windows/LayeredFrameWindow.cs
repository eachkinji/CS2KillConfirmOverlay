using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace KillConfirmCompatibility.Desktop.Windowing
{
    // Playback has no WPF visual tree: UpdateLayeredWindow commits the pixels,
    // screen origin, size and opacity together. Moving a WPF HWND separately
    // from its queued Image layout briefly displayed the previous frame offset.
    internal sealed class LayeredFrameWindow : IDisposable
    {
        private const string ClassName = "KillConfirm.AtomicFeedbackFrame";
        private static readonly WindowProcedure Procedure = WindowProc;
        private static ushort _class;
        private IntPtr _window, _dc, _dib, _oldBitmap, _bits;
        private int _width, _height, _scaledWidth, _scaledHeight;
        private BitmapSource _source, _scaled;
        private long _revision = -1;
        private Int32Rect _crop;
        private byte _alpha;
        internal bool IsVisible { get; private set; }
        internal NativeWindows.Rect Placement { get; private set; }
        internal IntPtr Handle => _window;

        internal LayeredFrameWindow(string title)
        {
            if (_class == 0)
            {
                var cls = new WindowClass { Size = (uint)Marshal.SizeOf<WindowClass>(), Procedure = Procedure,
                    Instance = GetModuleHandle(null), Name = ClassName };
                _class = RegisterClassEx(ref cls);
                if (_class == 0) throw new Win32Exception();
            }
            // Layered, transparent to input, tool window, no activation, topmost.
            _window = CreateWindowEx(0x080800A8, ClassName, title, 0x80000000,
                0, 0, 1, 1, IntPtr.Zero, IntPtr.Zero, GetModuleHandle(null), IntPtr.Zero);
            if (_window == IntPtr.Zero) throw new Win32Exception();
            _dc = CreateCompatibleDC(IntPtr.Zero);
            if (_dc == IntPtr.Zero) { DestroyWindow(_window); _window = IntPtr.Zero; throw new Win32Exception(); }
        }

        internal void Present(BitmapSource source, long revision, int fullWidth, int fullHeight,
            Int32Rect crop, NativeWindows.Rect placement, double opacity)
        {
            byte alpha = (byte)Math.Round(Math.Clamp(opacity, 0, 1) * 255);
            bool sourceChanged = _source != source || _scaledWidth != fullWidth || _scaledHeight != fullHeight;
            if (!sourceChanged && _revision == revision && _crop == crop && Placement.Equals(placement) && _alpha == alpha)
            {
                Show(); return;
            }
            if (sourceChanged)
            {
                _source = source; _scaledWidth = fullWidth; _scaledHeight = fullHeight;
                // Scale the complete logical frame before cropping. Independently
                // stretching each crop introduces fractional-pixel anchor drift.
                _scaled = source.PixelWidth == fullWidth && source.PixelHeight == fullHeight ? source
                    : new TransformedBitmap(source, new ScaleTransform(
                        Math.BitIncrement(fullWidth / (double)source.PixelWidth),
                        Math.BitIncrement(fullHeight / (double)source.PixelHeight)));
                // WIC truncates its transformed dimensions; round the scale
                // upward by one ULP so an exact integer cannot lose one pixel.
                if (_scaled.PixelWidth != fullWidth || _scaled.PixelHeight != fullHeight)
                    throw new InvalidOperationException($"Feedback scaler returned {_scaled.PixelWidth}x{_scaled.PixelHeight}, expected {fullWidth}x{fullHeight}.");
            }
            EnsureBitmap(crop.Width, crop.Height);
            if (sourceChanged || _revision != revision || _crop != crop)
                _scaled.CopyPixels(crop, _bits, checked(_width * _height * 4), checked(_width * 4));
            var destination = new NativeWindows.Point { X = placement.Left, Y = placement.Top };
            var size = new PixelSize { Width = _width, Height = _height };
            var origin = new NativeWindows.Point();
            var blend = new Blend { ConstantAlpha = alpha, AlphaFormat = 1 };
            if (!UpdateLayeredWindow(_window, IntPtr.Zero, ref destination, ref size, _dc, ref origin, 0, ref blend, 2))
                throw new Win32Exception();
            Placement = placement; _revision = revision; _crop = crop; _alpha = alpha;
            Show();
        }

        private void Show()
        {
            if (IsVisible) return;
            ShowWindow(_window, 4); // SW_SHOWNOACTIVATE, after the first complete frame.
            IsVisible = true;
        }
        internal void Hide()
        {
            if (!IsVisible) return;
            ShowWindow(_window, 0); IsVisible = false;
        }
        private void EnsureBitmap(int width, int height)
        {
            if (_dib != IntPtr.Zero && _width == width && _height == height) return;
            ReleaseBitmap();
            var info = new BitmapHeader { Size = (uint)Marshal.SizeOf<BitmapHeader>(), Width = width, Height = -height, Planes = 1, BitCount = 32 };
            _dib = CreateDIBSection(_dc, ref info, 0, out _bits, IntPtr.Zero, 0);
            if (_dib == IntPtr.Zero) throw new Win32Exception();
            _oldBitmap = SelectObject(_dc, _dib);
            _width = width; _height = height;
        }
        internal BitmapSource CopyPresentedBitmap()
        {
            if (_dib == IntPtr.Zero) throw new InvalidOperationException("No committed feedback frame.");
            var pixels = new byte[checked(_width * _height * 4)];
            Marshal.Copy(_bits, pixels, 0, pixels.Length);
            return BitmapSource.Create(_width, _height, 96, 96, PixelFormats.Pbgra32, null, pixels, _width * 4);
        }
        private void ReleaseBitmap()
        {
            if (_dib == IntPtr.Zero) return;
            SelectObject(_dc, _oldBitmap); DeleteObject(_dib);
            _dib = _bits = _oldBitmap = IntPtr.Zero;
        }
        public void Dispose()
        {
            Hide();
            ReleaseBitmap();
            if (_dc != IntPtr.Zero) { DeleteDC(_dc); _dc = IntPtr.Zero; }
            if (_window != IntPtr.Zero) { DestroyWindow(_window); _window = IntPtr.Zero; }
            _source = _scaled = null;
        }

        private static IntPtr WindowProc(IntPtr window, uint message, IntPtr wParam, IntPtr lParam) =>
            message == 0x84 ? new IntPtr(-1) : message == 0x21 ? new IntPtr(3) : DefWindowProc(window, message, wParam, lParam);
        private delegate IntPtr WindowProcedure(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct WindowClass
        {
            public uint Size, Style; public WindowProcedure Procedure; public int ClassExtra, WindowExtra;
            public IntPtr Instance, Icon, Cursor, Background; public string Menu, Name; public IntPtr SmallIcon;
        }
        [StructLayout(LayoutKind.Sequential)] private struct BitmapHeader
        {
            public uint Size; public int Width, Height; public ushort Planes, BitCount;
            public uint Compression, ImageSize; public int XPels, YPels; public uint ColorsUsed, ColorsImportant;
        }
        [StructLayout(LayoutKind.Sequential)] private struct PixelSize { public int Width, Height; }
        [StructLayout(LayoutKind.Sequential, Pack = 1)] private struct Blend { public byte Operation, Flags, ConstantAlpha, AlphaFormat; }
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr GetModuleHandle(string name);
        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern ushort RegisterClassEx(ref WindowClass windowClass);
        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern IntPtr CreateWindowEx(uint extendedStyle, string className, string title, uint style, int x, int y, int width, int height, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr parameter);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr DefWindowProc(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr window, int command);
        [DllImport("user32.dll")] private static extern bool DestroyWindow(IntPtr window);
        [DllImport("user32.dll", SetLastError = true)] private static extern bool UpdateLayeredWindow(IntPtr window, IntPtr screen, ref NativeWindows.Point position, ref PixelSize size, IntPtr source, ref NativeWindows.Point origin, uint colorKey, ref Blend blend, uint flags);
        [DllImport("gdi32.dll", SetLastError = true)] private static extern IntPtr CreateCompatibleDC(IntPtr source);
        [DllImport("gdi32.dll", SetLastError = true)] private static extern IntPtr CreateDIBSection(IntPtr dc, ref BitmapHeader info, uint usage, out IntPtr bits, IntPtr section, uint offset);
        [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr dc, IntPtr value);
        [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr value);
        [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr dc);
    }
}

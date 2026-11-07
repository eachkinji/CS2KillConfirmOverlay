using System;
using System.Runtime.InteropServices;
using Microsoft.Graphics.Canvas;

namespace KillConfirmCompatibility.Desktop.Windowing
{
    // The swap chain stays at the source canvas size. DWM scales the visual;
    // changing placement never allocates a new render target or reads pixels.
    internal sealed class CompositionFrameWindow : IDisposable
    {
        private readonly CanvasSwapChain _chain;
        private IntPtr _host;
        private readonly int _width, _height;
        private NativeWindows.Rect _full;
        private float _opacity = -1;
        public IntPtr Handle => GetHostWindow(_host);
        public NativeWindows.Rect Placement { get; private set; }
        public bool IsVisible { get; private set; }

        public CompositionFrameWindow(CanvasDevice device, int width, int height)
        {
            _width = width; _height = height;
            _chain = new CanvasSwapChain(device, width, height, 96);
            try { Marshal.ThrowExceptionForHR(CreateHost(((WinRT.IWinRTObject)_chain).NativeObject.ThisPtr, width, height, out _host)); }
            catch { _chain.Dispose(); throw; }
        }

        public void Present(CanvasRenderTarget target)
        {
            using (var session = _chain.CreateDrawingSession(default(global::Windows.UI.Color)))
            {
                session.Blend = CanvasBlend.Copy;
                session.DrawImage(target);
            }
            _chain.Present(0);
        }

        internal byte[] ReadCopiedPixels(CanvasRenderTarget target)
        {
            using (var session = _chain.CreateDrawingSession(default(global::Windows.UI.Color)))
            {
                session.Blend = CanvasBlend.Copy; session.DrawImage(target);
            }
            var pixels = new byte[checked(_width * _height * 4)];
            Marshal.ThrowExceptionForHR(ReadBack(_host, pixels, pixels.Length));
            _chain.Present(0);
            return pixels;
        }
        [DllImport("KillConfirmComposition.dll")] private static extern int ReadBack(IntPtr host, [Out] byte[] pixels, int length);

        public void Place(NativeWindows.Rect full, NativeWindows.Rect screen, double opacity)
        {
            var clipped = new NativeWindows.Rect {
                Left = Math.Max(full.Left, screen.Left), Top = Math.Max(full.Top, screen.Top),
                Right = Math.Min(full.Right, screen.Right), Bottom = Math.Min(full.Bottom, screen.Bottom) };
            if (clipped.Width <= 0 || clipped.Height <= 0 || opacity <= 0) { Hide(); return; }
            float alpha = (float)Math.Clamp(opacity, 0, 1);
            if (IsVisible && Placement.Equals(clipped) && _full.Equals(full) && _opacity == alpha) return;
            Marshal.ThrowExceptionForHR(PlaceHost(_host, clipped.Left, clipped.Top, clipped.Width, clipped.Height,
                (float)full.Width / _width, (float)full.Height / _height,
                full.Left - clipped.Left, full.Top - clipped.Top, alpha, 1));
            Placement = clipped; _full = full; _opacity = alpha; IsVisible = true;
        }

        public void Hide()
        {
            if (!IsVisible) return;
            Marshal.ThrowExceptionForHR(PlaceHost(_host, Placement.Left, Placement.Top, Math.Max(1, Placement.Width), Math.Max(1, Placement.Height),
                1, 1, 0, 0, 0, 0));
            IsVisible = false;
        }

        public void Dispose()
        {
            if (_host != IntPtr.Zero) { DestroyHost(_host); _host = IntPtr.Zero; }
            _chain.Dispose(); IsVisible = false;
        }
        [DllImport("KillConfirmComposition.dll")] private static extern int CreateHost(IntPtr canvas, int width, int height, out IntPtr host);
        [DllImport("KillConfirmComposition.dll")] private static extern void DestroyHost(IntPtr host);
        [DllImport("KillConfirmComposition.dll")] private static extern IntPtr GetHostWindow(IntPtr host);
        [DllImport("KillConfirmComposition.dll")] private static extern int PlaceHost(IntPtr host, int x, int y, int width, int height,
            float scaleX, float scaleY, float offsetX, float offsetY, float opacity, int visible);
    }
}

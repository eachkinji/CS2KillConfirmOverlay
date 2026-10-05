using Microsoft.Graphics.Canvas;
using System;

namespace KillConfirmCompatibility.Desktop.Rendering
{
    public sealed class DesktopCanvas : ICanvasResourceCreatorWithDpi
    {
        public CanvasDevice Device => CanvasDevice.GetSharedDevice();
        public float Dpi => 96;
        public double Width { get; set; } = 400;
        public double Height { get; set; } = 300;
        public bool IsDirty { get; private set; } = true;
        public void Invalidate() => IsDirty = true;
        public void MarkPresented() => IsDirty = false;
        public float ConvertPixelsToDips(int pixels) => pixels;
        public int ConvertDipsToPixels(float dips, CanvasDpiRounding rounding) =>
            rounding == CanvasDpiRounding.Floor ? (int)Math.Floor(dips) :
            rounding == CanvasDpiRounding.Ceiling ? (int)Math.Ceiling(dips) : (int)Math.Round(dips);
    }
    public sealed class DesktopDrawEventArgs : EventArgs
    {
        public DesktopDrawEventArgs(CanvasDrawingSession session) { DrawingSession = session; }
        public CanvasDrawingSession DrawingSession { get; }
    }
}

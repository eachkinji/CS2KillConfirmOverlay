using Microsoft.Graphics.Canvas;
using System;
using System.Runtime.CompilerServices;
using Windows.Foundation;

namespace KillConfirmCompatibility.Desktop.Rendering
{
    // Immutable artwork is scanned once at its original resolution. Removing
    // transparent padding before transforming avoids processing huge empty
    // rectangles at high zoom, while retaining every nonzero alpha texel.
    internal static class DesktopBitmapBounds
    {
        private sealed class Bounds { internal Rect Value; }
        private static readonly ConditionalWeakTable<CanvasBitmap, Bounds> Cache = new();
        internal static Rect Get(CanvasBitmap bitmap) => Cache.GetValue(bitmap, Find).Value;
        private static Bounds Find(CanvasBitmap bitmap)
        {
            int width = (int)bitmap.SizeInPixels.Width, height = (int)bitmap.SizeInPixels.Height;
            byte[] pixels = bitmap.GetPixelBytes();
            int left = width, top = height, right = -1, bottom = -1;
            for (int y = 0; y < height; y++)
            for (int x = 0, i = (y * width) * 4 + 3; x < width; x++, i += 4)
                if (pixels[i] != 0) { left = Math.Min(left, x); right = Math.Max(right, x); top = Math.Min(top, y); bottom = y; }
            if (right < 0) return new Bounds();
            left = Math.Max(0, left - 2); top = Math.Max(0, top - 2);
            right = Math.Min(width, right + 3); bottom = Math.Min(height, bottom + 3);
            return new Bounds { Value = new Rect(left, top, right - left, bottom - top) };
        }
    }
}

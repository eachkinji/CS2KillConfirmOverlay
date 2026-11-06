using System;
using System.Numerics;
using Microsoft.Graphics.Canvas;
using Windows.Foundation;

namespace KillConfirmGameBar.Controls
{
    public sealed partial class KillConfirmAnimation
    {
        private static Rect SnapCalabiyauSprite(Rect rect, Matrix3x2 transform, float dpi)
        {
            // Align only axis-aligned sprites. Rounding rotated quads would
            // deform their motion; independently rounding effect tiles opens seams.
            if (Math.Abs(transform.M12) > 0.00001 || Math.Abs(transform.M21) > 0.00001)
                return rect;
            double pixelsPerDip = dpi / 96.0;
            double xScale = transform.M11 * pixelsPerDip, yScale = transform.M22 * pixelsPerDip;
            if (Math.Abs(xScale * rect.Width) < 1 || Math.Abs(yScale * rect.Height) < 1)
                return rect;
            double left = Math.Round(rect.Left * xScale + transform.M31 * pixelsPerDip);
            double right = Math.Round(rect.Right * xScale + transform.M31 * pixelsPerDip);
            double top = Math.Round(rect.Top * yScale + transform.M32 * pixelsPerDip);
            double bottom = Math.Round(rect.Bottom * yScale + transform.M32 * pixelsPerDip);
            return new Rect((left - transform.M31 * pixelsPerDip) / xScale,
                (top - transform.M32 * pixelsPerDip) / yScale,
                (right - left) / xScale, (bottom - top) / yScale);
        }

        private static CanvasImageInterpolation CalabiyauInterpolation(Rect target, Rect source,
            Matrix3x2 transform, float dpi, bool tiledEffect)
        {
            // Existing effect atlases have a one-pixel gutter for linear sampling.
            // A wider cubic kernel could sample the next tile, so preserve that contract.
            if (tiledEffect) return CanvasImageInterpolation.Linear;
            double xScale = Math.Sqrt(transform.M11 * transform.M11 + transform.M12 * transform.M12) * dpi / 96.0;
            double yScale = Math.Sqrt(transform.M21 * transform.M21 + transform.M22 * transform.M22) * dpi / 96.0;
            double width = target.Width * xScale, height = target.Height * yScale;
            if (Math.Abs(width - source.Width) < 0.01 && Math.Abs(height - source.Height) < 0.01)
                return CanvasImageInterpolation.Linear;
            // Match VAL's sharper cubic enlargement and antialiased reduction,
            // using the actual transform/DPI instead of just logical sprite size.
            return width > source.Width + 0.5 || height > source.Height + 0.5
                ? CanvasImageInterpolation.Cubic : CanvasImageInterpolation.HighQualityCubic;
        }
    }
}

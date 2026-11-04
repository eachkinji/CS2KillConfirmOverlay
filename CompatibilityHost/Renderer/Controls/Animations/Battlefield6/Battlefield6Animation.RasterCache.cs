using System;
using System.Collections.Generic;
using Microsoft.Graphics.Canvas;
using Windows.Foundation;
using Windows.UI;

namespace KillConfirmCompatibility.Controls
{
    public sealed partial class KillConfirmAnimation
    {
        private static readonly Dictionary<CanvasBitmap, Dictionary<int, Battlefield6RasterIcon>> Battlefield6RasterCache =
            new Dictionary<CanvasBitmap, Dictionary<int, Battlefield6RasterIcon>>();

        private sealed class Battlefield6RasterIcon : IDisposable
        {
            public CanvasBitmap Base;
            public CanvasRenderTarget Glow;
            public bool OwnsBase;
            public void Dispose()
            {
                Glow?.Dispose();
                if (OwnsBase) Base?.Dispose();
            }
        }

        private static Battlefield6RasterIcon GetBattlefield6RasterIcon(CanvasDrawingSession ds, CanvasBitmap source)
        {
            // Use the final/full icon size, not its changing entrance scale. Include the
            // caller's transform and render-target DPI for UWP, desktop and offscreen draws.
            var matrix = ds.Transform;
            double scaleX = Math.Sqrt(matrix.M11 * matrix.M11 + matrix.M12 * matrix.M12);
            double scaleY = Math.Sqrt(matrix.M21 * matrix.M21 + matrix.M22 * matrix.M22);
            double pixels = Battlefield6IconSize * Math.Max(scaleX, scaleY) * ds.Dpi / 96.0;
            int level = 64;
            while (level < pixels && level < 2048) level *= 2;
            return GetBattlefield6RasterIcon(source, level);
        }

        private static Battlefield6RasterIcon GetBattlefield6RasterIcon(CanvasBitmap source, int level)
        {
            lock (Battlefield6IconCache)
            {
                if (!Battlefield6RasterCache.TryGetValue(source, out Dictionary<int, Battlefield6RasterIcon> levels))
                {
                    levels = new Dictionary<int, Battlefield6RasterIcon>();
                    Battlefield6RasterCache[source] = levels;
                }
                if (levels.TryGetValue(level, out Battlefield6RasterIcon cached)) return cached;
                var result = new Battlefield6RasterIcon();
                try
                {
                    result.Base = CreateBattlefield6RasterLevel(source, level);
                    result.OwnsBase = !ReferenceEquals(result.Base, source);
                    double padding = level / 8.0;
                    result.Glow = new CanvasRenderTarget(source.Device,
                        (float)(level + padding * 2), (float)(level + padding * 2), 96);
                    using (CanvasDrawingSession glow = result.Glow.CreateDrawingSession())
                    {
                        glow.Clear(Colors.Transparent);
                        double spread = level / 16.0;
                        foreach (var offset in Battlefield6GlowOffsets)
                        {
                            DrawBattlefield6RasterCopy(glow, result.Base, padding + offset.X * spread,
                                padding + offset.Y * spread, level, 0.016f);
                            DrawBattlefield6RasterCopy(glow, result.Base, padding + offset.X * spread * 0.55,
                                padding + offset.Y * spread * 0.55, level, 0.011f);
                        }
                        DrawBattlefield6RasterCopy(glow, result.Base, padding, padding, level, 0.009f);
                    }
                    levels[level] = result;
                    return result;
                }
                catch { result.Dispose(); throw; }
            }
        }

        private static CanvasBitmap CreateBattlefield6RasterLevel(CanvasBitmap source, int level)
        {
            CanvasBitmap current = source;
            bool ownsCurrent = false;
            int currentSize = (int)Math.Max(source.SizeInPixels.Width, source.SizeInPixels.Height);
            try
            {
                // A 2048 -> 40 high-quality draw can choose an enormous sampling kernel.
                // Halve at each stage instead. Intermediate surfaces are not retained;
                // every requested sharper level always starts from the native original.
                while (currentSize > level)
                {
                    int nextSize = Math.Max(level, (currentSize + 1) / 2);
                    CanvasRenderTarget next = new CanvasRenderTarget(source.Device, nextSize, nextSize, 96);
                    try
                    {
                        using (CanvasDrawingSession ds = next.CreateDrawingSession())
                        {
                            ds.Clear(Colors.Transparent);
                            ds.DrawImage(current, new Rect(0, 0, nextSize, nextSize), current.Bounds,
                                1, CanvasImageInterpolation.HighQualityCubic);
                        }
                    }
                    catch { next.Dispose(); throw; }
                    if (ownsCurrent) current.Dispose();
                    current = next;
                    ownsCurrent = true;
                    currentSize = nextSize;
                }
                return current;
            }
            catch { if (ownsCurrent) current.Dispose(); throw; }
        }

        private static void DrawBattlefield6RasterCopy(CanvasDrawingSession ds, CanvasBitmap bitmap,
            double x, double y, double size, float opacity)
        {
            ds.DrawImage(bitmap, new Rect(x, y, size, size), bitmap.Bounds, opacity, CanvasImageInterpolation.Linear);
        }

        private static void ClearBattlefield6RasterCache()
        {
            // Called while holding the same cache lock, before native originals are disposed.
            foreach (var levels in Battlefield6RasterCache.Values)
                foreach (Battlefield6RasterIcon raster in levels.Values) raster.Dispose();
            Battlefield6RasterCache.Clear();
        }
    }
}

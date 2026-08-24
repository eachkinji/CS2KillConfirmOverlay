using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Threading.Tasks;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.Graphics.Canvas.Effects;
using Microsoft.Graphics.Canvas.Text;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Text;

namespace KillConfirmGameBar.Controls
{
    public sealed partial class KillConfirmAnimation
    {
        private void PrepareBattlefield2042KillIconCache(Battlefield2042KillIconItem item)
        {
            if (item?.Icon == null || item.RenderCache != null)
            {
                return;
            }

            const double cacheSize = 64;
            const double cacheCenter = cacheSize / 2.0;
            float cacheDpi = GetBattlefield2042GlowCacheDpi();
            string cacheKey = item.IconFileName + "|" + item.IsHeadshot + "|" + cacheDpi.ToString("F2", CultureInfo.InvariantCulture);
            if (_battlefield2042KillIconRenderCaches.TryGetValue(cacheKey, out Battlefield2042KillIconRenderCache cached))
            {
                item.RenderCache = cached;
                return;
            }

            CanvasRenderTarget baseSurface = null;
            CanvasRenderTarget bloomSurface = null;
            try
            {
                baseSurface = new CanvasRenderTarget(
                    CanvasDevice.GetSharedDevice(),
                    (float)cacheSize,
                    (float)cacheSize,
                    cacheDpi);
                bloomSurface = new CanvasRenderTarget(
                    CanvasDevice.GetSharedDevice(),
                    (float)cacheSize,
                    (float)cacheSize,
                    cacheDpi);

                Color skullColor = item.IsHeadshot ? Battlefield2042HeadshotColor : Colors.White;
                double skullOpacity = item.IsHeadshot ? Battlefield2042HeadshotColor.A / 255.0 : 1.0;
                Rect skullRect = new Rect(
                    cacheCenter - Battlefield2042KillIconSize / 2.0,
                    cacheCenter - Battlefield2042KillIconSize / 2.0,
                    Battlefield2042KillIconSize,
                    Battlefield2042KillIconSize);

                using (CanvasDrawingSession baseSession = baseSurface.CreateDrawingSession())
                {
                    baseSession.Clear(Colors.Transparent);
                    CanvasBitmap shadow = GetCachedBattlefield2042Icon("SmoothCircle.png");
                    if (shadow != null)
                    {
                        DrawBattlefield2042TintedImage(
                            baseSession,
                            shadow,
                            new Rect(cacheCenter - 20, cacheCenter - 20, 40, 40),
                            Color.FromArgb(255, 0, 0, 0),
                            0.27058825);
                    }

                    DrawBattlefield2042TintedImage(
                        baseSession,
                        item.Icon,
                        skullRect,
                        skullColor,
                        skullOpacity);
                }

                using (CanvasDrawingSession bloomSession = bloomSurface.CreateDrawingSession())
                using (var tintEffect = new ColorMatrixEffect
                {
                    Source = item.Icon,
                    ColorMatrix = CreateBattlefield2042AlphaTintMatrix(skullColor)
                })
                {
                    bloomSession.Clear(Colors.Transparent);
                    DrawBattlefield2042TintedImageBloomOnly(
                        bloomSession,
                        tintEffect,
                        item.Icon,
                        skullRect,
                        skullOpacity,
                        item.IsHeadshot ? 0.55 : 0.42);
                }

                item.RenderCache = new Battlefield2042KillIconRenderCache(
                    baseSurface,
                    bloomSurface,
                    cacheCenter);
                _battlefield2042KillIconRenderCaches[cacheKey] = item.RenderCache;
            }
            catch
            {
                baseSurface?.Dispose();
                bloomSurface?.Dispose();
            }
        }

        private static void DrawBattlefield2042CachedKillIcon(
            CanvasDrawingSession drawingSession,
            Battlefield2042KillIconRenderCache cache,
            double centerX,
            double centerY)
        {
            if (cache?.BaseSurface == null || cache.BloomSurface == null)
            {
                return;
            }

            Rect source = cache.BaseSurface.Bounds;
            double physicalScale = Math.Max(1.0, cache.BaseSurface.Dpi / 96.0);
            double targetX = Math.Round((centerX - cache.CenterOffset) * physicalScale) / physicalScale;
            double targetY = Math.Round((centerY - cache.CenterOffset) * physicalScale) / physicalScale;
            Rect target = new Rect(
                targetX,
                targetY,
                cache.BaseSurface.Size.Width,
                cache.BaseSurface.Size.Height);
            CanvasBlend previousBlend = drawingSession.Blend;
            try
            {
                drawingSession.Blend = CanvasBlend.Add;
                drawingSession.DrawImage(
                    cache.BloomSurface,
                    target,
                    source,
                    1.0f,
                    CanvasImageInterpolation.Linear);
            }
            finally
            {
                drawingSession.Blend = previousBlend;
            }

            drawingSession.DrawImage(
                cache.BaseSurface,
                target,
                source,
                1.0f,
                CanvasImageInterpolation.NearestNeighbor);
        }

        private static void DrawBattlefield2042TintedImage(
            CanvasDrawingSession drawingSession,
            CanvasBitmap image,
            Rect target,
            Color tint,
            double opacity)
        {
            if (image == null || opacity <= 0)
            {
                return;
            }

            using (var tintEffect = new ColorMatrixEffect
            {
                Source = image,
                ColorMatrix = CreateBattlefield2042AlphaTintMatrix(tint)
            })
            {
                var source = new Rect(0, 0, image.SizeInPixels.Width, image.SizeInPixels.Height);
                drawingSession.DrawImage(
                    tintEffect,
                    target,
                    source,
                    (float)Clamp01(opacity),
                    CanvasImageInterpolation.HighQualityCubic);
            }
        }

        private static void DrawBattlefield2042TintedImageWithBloom(
            CanvasDrawingSession drawingSession,
            CanvasBitmap image,
            Rect target,
            Color tint,
            double opacity,
            double bloomStrength)
        {
            if (image == null || opacity <= 0)
            {
                return;
            }

            using (var tintEffect = new ColorMatrixEffect
            {
                Source = image,
                ColorMatrix = CreateBattlefield2042AlphaTintMatrix(tint)
            })
            {
                DrawBattlefield2042TintedImageBloomOnly(
                    drawingSession,
                    tintEffect,
                    image,
                    target,
                    opacity,
                    bloomStrength);
                var source = new Rect(0, 0, image.SizeInPixels.Width, image.SizeInPixels.Height);
                drawingSession.DrawImage(
                    tintEffect,
                    target,
                    source,
                    (float)Clamp01(opacity),
                    CanvasImageInterpolation.HighQualityCubic);
            }
        }

        private static void DrawBattlefield2042TintedImageBloomOnly(
            CanvasDrawingSession drawingSession,
            ColorMatrixEffect tintEffect,
            CanvasBitmap image,
            Rect target,
            double opacity,
            double bloomStrength)
        {
            var source = new Rect(0, 0, image.SizeInPixels.Width, image.SizeInPixels.Height);
            CanvasBlend previousBlend = drawingSession.Blend;
            try
            {
                drawingSession.Blend = CanvasBlend.Add;
                double innerOpacity = opacity * bloomStrength * 0.16;
                double outerOpacity = opacity * bloomStrength * 0.075;
                double diagonalOpacity = opacity * bloomStrength * 0.10;
                foreach (Vector2 offset in Battlefield2042BloomInnerOffsets)
                {
                    drawingSession.DrawImage(
                        tintEffect,
                        OffsetBattlefield2042Rect(target, offset.X, offset.Y),
                        source,
                        (float)Clamp01(innerOpacity),
                        CanvasImageInterpolation.Linear);
                }

                foreach (Vector2 offset in Battlefield2042BloomDiagonalOffsets)
                {
                    drawingSession.DrawImage(
                        tintEffect,
                        OffsetBattlefield2042Rect(target, offset.X, offset.Y),
                        source,
                        (float)Clamp01(diagonalOpacity),
                        CanvasImageInterpolation.Linear);
                }

                foreach (Vector2 offset in Battlefield2042BloomOuterOffsets)
                {
                    drawingSession.DrawImage(
                        tintEffect,
                        OffsetBattlefield2042Rect(target, offset.X, offset.Y),
                        source,
                        (float)Clamp01(outerOpacity),
                        CanvasImageInterpolation.Linear);
                }
            }
            finally
            {
                drawingSession.Blend = previousBlend;
            }
        }

        private static Matrix5x4 CreateBattlefield2042AlphaTintMatrix(Color tint)
        {
            return new Matrix5x4
            {
                M41 = tint.R / 255.0f,
                M42 = tint.G / 255.0f,
                M43 = tint.B / 255.0f,
                M44 = tint.A / 255.0f
            };
        }

        private static Rect OffsetBattlefield2042Rect(Rect rect, double x, double y)
        {
            return new Rect(rect.X + x, rect.Y + y, rect.Width, rect.Height);
        }
    }
}

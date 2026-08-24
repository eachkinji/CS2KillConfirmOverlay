using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using KillConfirmGameBar.Services;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Effects;
using Windows.Foundation;
using Windows.UI;

namespace KillConfirmGameBar.Controls
{
    public sealed partial class KillConfirmAnimation
    {
        private void DrawValorantTintedImage(
            CanvasDrawingSession drawingSession,
            CanvasBitmap image,
            Rect target,
            Rect source,
            Color tintColor,
            bool additive)
        {
            CanvasBlend previousBlend = drawingSession.Blend;
            if (additive)
            {
                drawingSession.Blend = CanvasBlend.Add;
            }

            DrawMultiplyTintImage(drawingSession, image, target, source, tintColor, 1);
            drawingSession.Blend = previousBlend;
        }

        private static void DrawValorantHalo(
            CanvasDrawingSession drawingSession,
            double cx,
            double cy,
            Color accent,
            double radius,
            double elapsedMs,
            double opacity)
        {
            if (opacity <= 0)
            {
                return;
            }

            const int Segments = 72;
            double scaledRadius = radius * ValorantDemoVfxScale;
            double ringWidth = 1.5 * ValorantDemoVfxScale;
            double minY = cy - scaledRadius;
            double maxY = cy + scaledRadius;
            double yRange = Math.Max(0.001, maxY - minY);
            double angleOffset = (elapsedMs % 2200.0) / 2200.0 * Math.PI * 2.0;
            double clampedOpacity = Clamp01(opacity);

            for (int i = 0; i < Segments; i++)
            {
                double a0 = angleOffset + Math.PI * 2.0 * i / Segments;
                double a1 = angleOffset + Math.PI * 2.0 * (i + 1) / Segments;
                double y0 = cy + Math.Sin(a0) * scaledRadius;
                double y1 = cy + Math.Sin(a1) * scaledRadius;
                double alphaFactor = Clamp01((maxY - ((y0 + y1) * 0.5)) / yRange) * clampedOpacity;
                byte alpha = (byte)Math.Max(0, Math.Min(255, Math.Round(alphaFactor * 255.0)));
                if (alpha <= 2)
                {
                    continue;
                }

                drawingSession.DrawLine(
                    (float)(cx + Math.Cos(a0) * scaledRadius),
                    (float)y0,
                    (float)(cx + Math.Cos(a1) * scaledRadius),
                    (float)y1,
                    Color.FromArgb(alpha, accent.R, accent.G, accent.B),
                    (float)ringWidth);
            }
        }

        private void DrawCenteredImageAt(CanvasDrawingSession drawingSession, CanvasBitmap image, double cx, double cy, double width, double height, double scale, double opacity)
        {
            if (image == null || opacity <= 0 || scale <= 0)
            {
                return;
            }

            Rect target = SnapValorantRectToPhysicalPixels(BuildCenteredImageRect(image, cx, cy, width, height, scale));
            var source = new Rect(0, 0, image.SizeInPixels.Width, image.SizeInPixels.Height);
            drawingSession.DrawImage(image, target, source, 1.0f, GetValorantImageInterpolation(target, source));
        }

        private void DrawCenteredTintedImageAt(
            CanvasDrawingSession drawingSession,
            CanvasBitmap image,
            double cx,
            double cy,
            double width,
            double height,
            double scale,
            Color tint,
            double opacity)
        {
            if (image == null || opacity <= 0 || scale <= 0)
            {
                return;
            }

            Rect target = SnapValorantRectToPhysicalPixels(
                BuildCenteredImageRect(image, cx, cy, width, height, scale));
            var source = new Rect(0, 0, image.SizeInPixels.Width, image.SizeInPixels.Height);
            DrawMultiplyTintImage(drawingSession, image, target, source, tint, opacity);
        }

        private void DrawCenteredImageWithShadowAt(
            CanvasDrawingSession drawingSession,
            CanvasBitmap image,
            double cx,
            double cy,
            double width,
            double height,
            double scale,
            double opacity,
            Color shadowColor,
            double shadowBlur,
            double shadowOffsetX,
            double shadowOffsetY,
            double shadowOpacity,
            float brightness = 1.0f,
            float contrast = 1.0f)
        {
            if (image == null || opacity <= 0 || scale <= 0)
            {
                return;
            }

            var target = BuildCenteredImageRect(image, cx, cy, width, height, scale);
            var source = new Rect(0, 0, image.SizeInPixels.Width, image.SizeInPixels.Height);
            DrawImageWithSoftShadow(
                drawingSession,
                image,
                target,
                source,
                opacity,
                shadowColor,
                shadowBlur,
                shadowOffsetX,
                shadowOffsetY,
                shadowOpacity,
                brightness,
                contrast);
        }

        private void DrawRotatedCenteredImageWithShadowAt(
            CanvasDrawingSession drawingSession,
            CanvasBitmap image,
            double cx,
            double cy,
            double width,
            double height,
            double scale,
            double degrees,
            double opacity,
            Color shadowColor,
            double shadowBlur,
            double shadowOffsetX,
            double shadowOffsetY,
            double shadowOpacity,
            float brightness = 1.0f,
            float contrast = 1.0f)
        {
            Matrix3x2 previous = drawingSession.Transform;
            drawingSession.Transform = Matrix3x2.CreateRotation((float)(degrees * Math.PI / 180.0), new Vector2((float)cx, (float)cy)) * previous;
            DrawCenteredImageWithShadowAt(drawingSession, image, cx, cy, width, height, scale, opacity, shadowColor, shadowBlur, shadowOffsetX, shadowOffsetY, shadowOpacity, brightness, contrast);
            drawingSession.Transform = previous;
        }

        private void DrawCenteredFlashImageAt(
            CanvasDrawingSession drawingSession,
            CanvasBitmap image,
            double cx,
            double cy,
            double width,
            double height,
            double scale,
            double opacity,
            Color flashColor,
            float brightness = 1.0f,
            float contrast = 1.0f)
        {
            if (image == null || opacity <= 0 || scale <= 0)
            {
                return;
            }

            var target = BuildCenteredImageRect(image, cx, cy, width, height, scale);
            var source = new Rect(0, 0, image.SizeInPixels.Width, image.SizeInPixels.Height);
            // Reference's icon_flash is a red flash, not a white flash. Earlier we
            // boosted the emblem to 1.8x brightness which produced a near-white
            // pulse and let the red wash out. Keep a tighter, more opaque red
            // glow and only mildly brighten the emblem so the red dominates.
            DrawSoftSilhouette(drawingSession, image, target, source, flashColor, 4, 0, 0, opacity, true);
            DrawBrightnessContrastImage(drawingSession, image, target, source, opacity, 1.25f * brightness, contrast);
        }

        private static Rect BuildCenteredImageRect(CanvasBitmap image, double cx, double cy, double width, double height, double scale)
        {
            double fitScale = Math.Min(width / image.SizeInPixels.Width, height / image.SizeInPixels.Height) * scale;
            double w = image.SizeInPixels.Width * fitScale;
            double h = image.SizeInPixels.Height * fitScale;
            return new Rect(cx - w / 2.0, cy - h / 2.0, w, h);
        }

        private Rect SnapValorantRectToPhysicalPixels(Rect rect)
        {
            double physicalScale = Math.Max(0.1, GetRenderResolutionScale());
            double left = Math.Round(rect.Left * physicalScale) / physicalScale;
            double top = Math.Round(rect.Top * physicalScale) / physicalScale;
            double right = Math.Round(rect.Right * physicalScale) / physicalScale;
            double bottom = Math.Round(rect.Bottom * physicalScale) / physicalScale;
            double minimumSize = 1.0 / physicalScale;
            return new Rect(
                left,
                top,
                Math.Max(minimumSize, right - left),
                Math.Max(minimumSize, bottom - top));
        }

        private double SnapValorantCoordinateToPhysicalPixel(double coordinate)
        {
            double physicalScale = Math.Max(0.1, GetRenderResolutionScale());
            return Math.Round(coordinate * physicalScale) / physicalScale;
        }

        private CanvasImageInterpolation GetValorantImageInterpolation(Rect target, Rect source)
        {
            double physicalScale = Math.Max(0.1, GetRenderResolutionScale());
            bool isUpscaling = target.Width * physicalScale > source.Width + 0.5
                || target.Height * physicalScale > source.Height + 0.5;
            return isUpscaling
                ? ValorantUpscaleInterpolation
                : ValorantDownscaleInterpolation;
        }

    }
}

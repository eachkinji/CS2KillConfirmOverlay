using System;
using System.Numerics;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Effects;
using Windows.Foundation;
using Windows.UI;

namespace KillConfirmGameBar.Controls
{
    public sealed partial class KillConfirmAnimation
    {
        private void DrawNativeDissolvedImage(
            CanvasDrawingSession ds, CanvasBitmap image, CanvasBitmap mask,
            double cx, double cy, double width, double height,
            double reveal, double opacity)
        {
            DrawNativeDissolvedTintedImage(ds, image, mask, cx, cy, width,
                height, 1.0, reveal, Colors.White, opacity);
        }

        private void DrawNativeDissolvedTintedImage(
            CanvasDrawingSession ds, CanvasBitmap image, CanvasBitmap mask,
            double cx, double cy, double width, double height, double scale,
            double reveal, Color tint, double opacity)
        {
            if (image == null || opacity <= 0 || scale <= 0 || reveal <= 0)
            {
                return;
            }

            var target = SnapValorantRectToPhysicalPixels(new Rect(
                cx - (width * scale / 2.0), cy - (height * scale / 2.0),
                width * scale, height * scale));
            var source = new Rect(0, 0, image.SizeInPixels.Width, image.SizeInPixels.Height);
            using (var tintEffect = new ColorMatrixEffect
            {
                Source = image,
                ColorMatrix = CreateMultiplyTintMatrix(tint),
                CacheOutput = false
            })
            {
                if (mask == null || reveal >= 0.999)
                {
                    ds.DrawImage(tintEffect, target, source,
                        (float)Clamp01(opacity), GetValorantImageInterpolation(target, source));
                    return;
                }

                using (var luminance = new LuminanceToAlphaEffect
                {
                    Source = mask,
                    CacheOutput = false
                })
                using (var threshold = new LinearTransferEffect
                {
                    Source = luminance,
                    // AnimatedUMG reveals the brightest dissolve texels first.
                    // The old negative slope inverted every native mask (most
                    // visibly the top-down ring ramp).
                    AlphaSlope = 10.0f,
                    AlphaOffset = (float)((Clamp01(reveal) * 11.0) - 10.0),
                    RedDisable = true,
                    GreenDisable = true,
                    BlueDisable = true,
                    ClampOutput = true,
                    CacheOutput = false
                })
                using (var masked = new AlphaMaskEffect
                {
                    Source = tintEffect,
                    AlphaMask = threshold,
                    CacheOutput = false
                })
                {
                    ds.DrawImage(masked, target, source,
                        (float)Clamp01(opacity), GetValorantImageInterpolation(target, source));
                }
            }
        }

        private void DrawNativeStretchedImage(
            CanvasDrawingSession ds, CanvasBitmap image, double cx, double cy,
            double width, double height, double degrees, double opacity)
        {
            if (image == null || opacity <= 0)
            {
                return;
            }

            var target = SnapValorantRectToPhysicalPixels(new Rect(
                cx - width / 2.0, cy - height / 2.0, width, height));
            var source = new Rect(0, 0, image.SizeInPixels.Width, image.SizeInPixels.Height);
            Matrix3x2 previous = ds.Transform;
            if (Math.Abs(degrees) > 0.001)
            {
                ds.Transform = Matrix3x2.CreateRotation(
                    (float)(degrees * Math.PI / 180.0),
                    new Vector2((float)cx, (float)cy)) * previous;
            }

            ds.DrawImage(image, target, source, (float)Clamp01(opacity),
                GetValorantImageInterpolation(target, source));
            ds.Transform = previous;
        }

        private void DrawNativeTintedStretchedImage(
            CanvasDrawingSession ds, CanvasBitmap image, double cx, double cy,
            double width, double height, double degrees, Color tint, double opacity)
        {
            if (image == null || opacity <= 0)
            {
                return;
            }

            var target = SnapValorantRectToPhysicalPixels(new Rect(
                cx - width / 2.0, cy - height / 2.0, width, height));
            var source = new Rect(0, 0, image.SizeInPixels.Width, image.SizeInPixels.Height);
            Matrix3x2 previous = ds.Transform;
            if (Math.Abs(degrees) > 0.001)
            {
                ds.Transform = Matrix3x2.CreateRotation(
                    (float)(degrees * Math.PI / 180.0),
                    new Vector2((float)cx, (float)cy)) * previous;
            }

            DrawMultiplyTintImage(ds, image, target, source, tint, opacity);
            ds.Transform = previous;
        }

        private void DrawNativeTintedSource(
            CanvasDrawingSession ds, CanvasBitmap image, Rect target, Rect source,
            Color tint, double opacity, bool additive)
        {
            CanvasBlend previous = ds.Blend;
            if (additive)
            {
                ds.Blend = CanvasBlend.Add;
            }

            DrawMultiplyTintImage(ds, image, target, source, tint, opacity);
            ds.Blend = previous;
        }
    }
}

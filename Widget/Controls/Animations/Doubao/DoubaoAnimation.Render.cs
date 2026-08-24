using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KillConfirmGameBar.Services;
using Microsoft.Graphics.Canvas;
using Windows.Foundation;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Xaml;

namespace KillConfirmGameBar.Controls
{
    public sealed partial class KillConfirmAnimation
    {
        private void DrawDoubaoFrame(CanvasDrawingSession drawingSession)
        {
            if (!_isDoubaoActive || _currentDoubaoBitmap == null)
            {
                return;
            }

            double elapsed = _playbackClock.Elapsed.TotalMilliseconds;
            double centerX = DoubaoFrameWidth / 2.0;
            double centerY = DoubaoFrameHeight / 2.0;

            // Simple scale-in (no elastic overshoot — the flash layer carries the impact)
            // and exit fade.
            double entry = EaseOutQuad(Clamp01(elapsed / DoubaoImpactMs));
            double scale = Lerp(0.7, 1.0, entry);
            double opacity = Clamp01(elapsed / 70.0);
            if (elapsed > DoubaoFadeStartMs)
            {
                double exitT = Clamp01((elapsed - DoubaoFadeStartMs) / (DoubaoDurationMs - DoubaoFadeStartMs));
                opacity *= 1.0 - EaseInCubic(exitT);
            }

            // Layer 1: kill badge image.
            DrawDoubaoBitmap(drawingSession, centerX, centerY, scale, opacity);

            // Layer 2: flash overlay — bright at impact, fades over DoubaoFlashMs.
            // Replaces the retired procedural shockwaves / sparkles / holo brackets.
            CanvasBitmap flash = _currentDoubaoFlashBitmap;
            if (flash != null && opacity > 0.01)
            {
                double flashRamp = Clamp01(elapsed / 60.0);
                double flashDecay = Clamp01(1.0 - elapsed / DoubaoFlashMs);
                double flashAlpha = flashRamp * flashDecay * opacity;
                if (flashAlpha > 0.01)
                {
                    DrawDoubaoFlashOverlay(drawingSession, centerX, centerY, flash, scale, flashAlpha);
                }
            }
        }

        private static void DrawDoubaoFlashOverlay(
            CanvasDrawingSession drawingSession,
            double centerX,
            double centerY,
            CanvasBitmap flash,
            double scale,
            double alpha)
        {
            double imageWidth = flash.SizeInPixels.Width;
            double imageHeight = flash.SizeInPixels.Height;
            if (imageWidth <= 0 || imageHeight <= 0)
            {
                return;
            }

            // Cover the frame area (a touch larger than the badge), centered, rising with
            // the badge scale-in. Additive blend so the flash reads as a bright glint.
            double fit = Math.Min(DoubaoFrameWidth / imageWidth, DoubaoFrameHeight / imageHeight) * scale;
            double width = imageWidth * fit;
            double height = imageHeight * fit;
            var target = new Rect(centerX - width / 2.0, centerY - height / 2.0, width, height);
            var source = new Rect(0, 0, imageWidth, imageHeight);

            CanvasBlend previousBlend = drawingSession.Blend;
            drawingSession.Blend = CanvasBlend.Add;
            drawingSession.DrawImage(
                flash,
                target,
                source,
                (float)Clamp01(alpha),
                CanvasImageInterpolation.Linear);
            drawingSession.Blend = previousBlend;
        }

        private Rect DrawDoubaoBitmap(
            CanvasDrawingSession drawingSession,
            double centerX,
            double centerY,
            double scale,
            double opacity)
        {
            double imageWidth = _currentDoubaoBitmap.SizeInPixels.Width;
            double imageHeight = _currentDoubaoBitmap.SizeInPixels.Height;
            if (imageWidth <= 0 || imageHeight <= 0 || opacity <= 0)
            {
                return Rect.Empty;
            }

            double fitScale = Math.Min(640.0 / imageWidth, 380.0 / imageHeight) * scale;
            double width = imageWidth * fitScale;
            double height = imageHeight * fitScale;
            var target = new Rect(centerX - width / 2.0, centerY - height / 2.0, width, height);
            var source = new Rect(0, 0, imageWidth, imageHeight);
            drawingSession.DrawImage(
                _currentDoubaoBitmap,
                target,
                source,
                (float)Clamp01(opacity),
                CanvasImageInterpolation.Linear);

            return target;
        }

        private static double EaseOutQuad(double t)
        {
            return 1.0 - (1.0 - t) * (1.0 - t);
        }

        private static double EaseInCubic(double t)
        {
            return t * t * t;
        }
    }
}

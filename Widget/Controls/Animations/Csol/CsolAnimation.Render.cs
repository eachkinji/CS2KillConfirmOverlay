using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KillConfirmGameBar.Services;
using Microsoft.Graphics.Canvas;
using Windows.Foundation;
using Windows.Storage;

namespace KillConfirmGameBar.Controls
{
    public sealed partial class KillConfirmAnimation
    {
        private void DrawCsolKillFrame(CanvasDrawingSession drawingSession, int frame)
        {
            CsolKillAsset asset = _currentCsolAsset;
            if (asset == null)
            {
                return;
            }

            double elapsedSeconds = _playbackClock.Elapsed.TotalSeconds;
            float alpha = (float)CsolAlpha(elapsedSeconds);
            if (alpha <= 0)
            {
                return;
            }

            // Top row: kill-streak banner (1..9) and boss kill (10). The artwork
            // ships with an alpha channel and composites over the widget.
            CanvasBitmap streak = asset.KillCount >= 1 && asset.KillCount <= 10
                ? asset.Streak[asset.KillCount - 1]
                : null;
            if (streak != null)
            {
                DrawCsolCenteredImage(
                    drawingSession,
                    streak,
                    CsolFrameWidth / 2.0,
                    64,
                    460,
                    78,
                    alpha);
            }

            // Bottom row: special icon layer (headshot / melee / revenge / assist).
            CanvasBitmap special = GetCsolSpecialBitmap(asset);
            if (special != null)
            {
                DrawCsolCenteredImage(
                    drawingSession,
                    special,
                    CsolFrameWidth / 2.0,
                    190,
                    460,
                    150,
                    alpha);
            }
        }

        private static CanvasBitmap GetCsolSpecialBitmap(CsolKillAsset asset)
        {
            switch (asset.SpecialKey)
            {
                case "headshot":
                    return asset.Headshot;
                case "melee":
                    return asset.Melee;
                case "revenge":
                    return asset.Revenge;
                case "firstkill":
                    return asset.FirstKill;
                case "assist":
                    return asset.Assist;
                default:
                    return null;
            }
        }

        private static double CsolAlpha(double elapsedSeconds)
        {
            if (elapsedSeconds < CsolHoldSeconds)
            {
                return 1.0;
            }

            double fade = (elapsedSeconds - CsolHoldSeconds) / CsolFadeSeconds;
            return Math.Max(0.0, 1.0 - fade);
        }

        private static void DrawCsolCenteredImage(
            CanvasDrawingSession drawingSession,
            CanvasBitmap image,
            double centerX,
            double centerY,
            double maxWidth,
            double maxHeight,
            float alpha)
        {
            if (image == null || alpha <= 0)
            {
                return;
            }

            double imageWidth = image.SizeInPixels.Width;
            double imageHeight = image.SizeInPixels.Height;
            if (imageWidth <= 0 || imageHeight <= 0)
            {
                return;
            }

            double fitScale = Math.Min(maxWidth / imageWidth, maxHeight / imageHeight);
            fitScale = Math.Min(fitScale, 1.0);
            double scaledWidth = imageWidth * fitScale;
            double scaledHeight = imageHeight * fitScale;
            var target = new Rect(
                centerX - scaledWidth / 2.0,
                centerY - scaledHeight / 2.0,
                scaledWidth,
                scaledHeight);
            var source = new Rect(0, 0, imageWidth, imageHeight);
            drawingSession.DrawImage(image, target, source, alpha);
        }

    }
}

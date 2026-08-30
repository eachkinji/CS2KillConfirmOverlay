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
        // One deliberately isolated native-data playback sample. 00031 is the
        // public RGX 11z Pro pack; the shipped Valorant asset calls it Afterglow.
        private const int ValorantNativeAfterglowFrameCount = 137;
        private const double ValorantNativeUmgScale = 0.323;
        private const double ValorantNativeWheelSpinStartMs = 750.0;
        private const double ValorantNativeWheelSpinDurationMs = 1000.0;
        private const double ValorantNativeExitStartMs = 1953.2667;
        private const double ValorantNativeExitEndMs = 2108.2667;
        private static readonly Color ValorantNativeHeadshotRed = Color.FromArgb(255, 255, 0, 0);

        private void DrawNativeAfterglowFrame(CanvasDrawingSession drawingSession, int frame, ValorantKillAsset asset)
        {
            double elapsedMs = frame * (1000.0 / FrameSequenceFps);
            double rootOpacity = 1.0 - NativeLinearProgress(elapsedMs, ValorantNativeExitStartMs, ValorantNativeExitEndMs);
            if (rootOpacity <= 0)
            {
                return;
            }

            double rootY = NativeSampleLinear(
                elapsedMs,
                new[] { 0.0, 50.0, 100.0 },
                new[] { -30.0, -30.0, 0.0 }) * ValorantNativeUmgScale;
            double cx = ValorantFrameWidth / 2.0;
            double cy = (ValorantFrameHeight / 2.0) + rootY;
            int nativeKillCount = Math.Max(1, Math.Min(5, asset.KillCount));

            DrawNativeAfterglowParticles(drawingSession, asset, nativeKillCount, elapsedMs, cx, cy, rootOpacity);

            double shadowOpacity = NativeSampleLinear(
                elapsedMs,
                new[] { 0.0, 150.0, 1803.2667, 1903.2667 },
                new[] { 0.0, 1.0, 1.0, 0.0 }) * rootOpacity;
            DrawNativeStretchedImage(
                drawingSession,
                asset.Textures.Shadow,
                cx,
                cy,
                512.0 * ValorantNativeUmgScale,
                512.0 * ValorantNativeUmgScale,
                0,
                shadowOpacity);

            double wheelOpacity = NativeSampleLinear(
                elapsedMs,
                new[] { 0.0, 50.0, 150.0, ValorantNativeExitStartMs, 2103.2667 },
                new[] { 0.0, 0.0, 1.0, 1.0, 0.0 }) * rootOpacity;
            DrawNativeAfterglowWheel(drawingSession, asset, nativeKillCount, elapsedMs, cx, cy, wheelOpacity);

            double frameDissolve = NativeLinearProgress(elapsedMs, 150.0, 450.0);
            DrawNativeDissolvedImage(
                drawingSession,
                asset.Frame,
                asset.Textures.FrameDissolve,
                cx,
                cy,
                256.0 * ValorantNativeUmgScale,
                256.0 * ValorantNativeUmgScale,
                frameDissolve,
                rootOpacity);

            double badgeDissolve = elapsedMs < 1803.2667
                ? NativeLinearProgress(elapsedMs, 150.0, 170.0)
                : 1.0 - NativeLinearProgress(elapsedMs, 1803.2667, 2103.2667);
            double badgeScale = NativeSampleLinear(
                elapsedMs,
                new[] { 0.45, 2.6833, 17.5167, 1803.2667, 1903.25 },
                new[] { 1.0, 0.6, 1.0, 1.0, 0.6 });
            Color badgeTint = Colors.White;
            if (asset.IsHeadshot)
            {
                badgeScale *= GetNativeHeadshotBadgeScale(elapsedMs);
                badgeTint = GetNativeHeadshotFlickerColor(elapsedMs, false);
            }

            DrawNativeDissolvedTintedImage(
                drawingSession,
                asset.Emblem,
                asset.Textures.BadgeDissolve,
                cx,
                cy,
                76.0 * ValorantNativeUmgScale,
                100.0 * ValorantNativeUmgScale,
                badgeScale,
                badgeDissolve,
                badgeTint,
                rootOpacity);

            if (asset.IsHeadshot)
            {
                DrawNativeAfterglowHeadshot(drawingSession, asset, elapsedMs, cx, cy, rootOpacity);
            }
        }

        private void DrawNativeAfterglowParticles(
            CanvasDrawingSession drawingSession,
            ValorantKillAsset asset,
            int killCount,
            double elapsedMs,
            double cx,
            double cy,
            double opacity)
        {
            double fxElapsed = elapsedMs - 150.0;
            CanvasBitmap tierParticle;
            int tierFrames;
            if (killCount < 3)
            {
                tierParticle = asset.BaseParticle;
                tierFrames = 49;
            }
            else if (killCount == 3)
            {
                tierParticle = asset.Textures.BaseParticleT2;
                tierFrames = 49;
            }
            else
            {
                tierParticle = asset.Textures.BaseParticleT3;
                tierFrames = 42;
            }

            DrawNativeParticle(
                drawingSession,
                tierParticle,
                tierFrames,
                40.0,
                fxElapsed,
                cx,
                cy,
                112,
                112,
                0,
                206.0 / 256.0,
                0,
                45,
                true,
                false,
                asset.Accent,
                opacity);

            DrawNativeParticle(
                drawingSession,
                asset.HeroFlame,
                20,
                35.0,
                fxElapsed,
                cx,
                cy,
                96,
                108,
                0.4934375,
                0.49079242,
                0,
                -30,
                false,
                false,
                asset.Accent,
                opacity);

            if (killCount >= 4)
            {
                DrawNativeParticle(
                    drawingSession,
                    asset.LargeSparks,
                    52,
                    40.0,
                    fxElapsed,
                    cx,
                    cy,
                    105,
                    105,
                    0.49052733,
                    0.5185547,
                    0,
                    2,
                    false,
                    true,
                    asset.Accent,
                    opacity);
            }

            if (!asset.IsHeadshot)
            {
                return;
            }

            double particleOffset = 100.0 * ValorantNativeUmgScale;
            double[] offsetsX = { -particleOffset, particleOffset, particleOffset, -particleOffset };
            double[] offsetsY = { -particleOffset, -particleOffset, particleOffset, particleOffset };
            double[] angles = { -45.0, 45.0, 135.0, -135.0 };
            for (int index = 0; index < angles.Length; index++)
            {
                DrawNativeRotatedParticle(
                    drawingSession,
                    asset.XSparks,
                    29,
                    40.0,
                    elapsedMs,
                    cx + offsetsX[index],
                    cy + offsetsY[index],
                    56,
                    170,
                    angles[index],
                    ValorantNativeHeadshotRed,
                    opacity);
            }
        }

        private void DrawNativeAfterglowWheel(
            CanvasDrawingSession drawingSession,
            ValorantKillAsset asset,
            int killCount,
            double elapsedMs,
            double cx,
            double cy,
            double opacity)
        {
            if (opacity <= 0)
            {
                return;
            }

            double spin = killCount > 1
                ? -360.0 * NativeLinearProgress(
                    elapsedMs,
                    ValorantNativeWheelSpinStartMs,
                    ValorantNativeWheelSpinStartMs + ValorantNativeWheelSpinDurationMs)
                : 0.0;
            DrawNativeStretchedImage(
                drawingSession,
                asset.Textures.Ring,
                cx,
                cy,
                180.0 * ValorantNativeUmgScale,
                180.0 * ValorantNativeUmgScale,
                spin,
                opacity);

            double radius = 147.0 * ValorantNativeUmgScale;
            int currentIndex = killCount - 1;
            for (int index = 0; index < 5; index++)
            {
                double pipOpacity = index < currentIndex ? 1.0 : 0.3;
                double pipScale = 1.0;
                double currentLift = 0.0;
                if (index == currentIndex)
                {
                    pipOpacity = NativeSampleLinear(
                        elapsedMs,
                        new[] { 0.0, 150.0, 300.0, 750.0 },
                        new[] { 0.0, 1.0, 1.0, 0.6 });
                    pipScale = NativeSampleLinear(
                        elapsedMs,
                        new[] { 0.0, 300.0, 750.0 },
                        new[] { 1.2, 1.2, 1.0 });
                    currentLift = NativeSampleLinear(
                        elapsedMs,
                        new[] { 0.0, 300.0, 750.0 },
                        new[] { 15.0, 15.0, 0.0 }) * ValorantNativeUmgScale;
                }

                double angle = (index * 72.0) + spin;
                double radians = angle * Math.PI / 180.0;
                double distance = radius + currentLift;
                double x = cx + Math.Sin(radians) * distance;
                double y = cy - Math.Cos(radians) * distance;
                DrawNativeStretchedImage(
                    drawingSession,
                    asset.Bar,
                    x,
                    y,
                    64.0 * ValorantNativeUmgScale * pipScale,
                    64.0 * ValorantNativeUmgScale * pipScale,
                    angle,
                    pipOpacity * opacity);
            }
        }

        private void DrawNativeAfterglowHeadshot(
            CanvasDrawingSession drawingSession,
            ValorantKillAsset asset,
            double elapsedMs,
            double cx,
            double cy,
            double opacity)
        {
            double reticleScale = NativeSampleLinear(
                elapsedMs,
                new[] { 0.0, 250.0 },
                new[] { 2.0, 1.0 });
            Color tint = GetNativeHeadshotFlickerColor(elapsedMs, true);
            DrawNativeTintedStretchedImage(
                drawingSession,
                asset.Headshot,
                cx + (0.85 * ValorantNativeUmgScale),
                cy + (-21.0 * ValorantNativeUmgScale),
                128.0 * 0.3 * ValorantNativeUmgScale * reticleScale,
                128.0 * 0.3 * ValorantNativeUmgScale * reticleScale,
                0,
                tint,
                opacity);
        }

        private void DrawNativeParticle(
            CanvasDrawingSession drawingSession,
            CanvasBitmap image,
            int frameCount,
            double fps,
            double elapsedMs,
            double cx,
            double cy,
            double width,
            double height,
            double anchorX,
            double anchorY,
            double offsetX,
            double offsetY,
            bool mirrored,
            bool additive,
            Color tint,
            double opacity)
        {
            if (image == null || elapsedMs < 0 || opacity <= 0)
            {
                return;
            }

            int frameIndex = (int)Math.Floor(elapsedMs * fps / 1000.0);
            if (frameIndex < 0 || frameIndex >= frameCount)
            {
                return;
            }

            double sourceFrameHeight = image.SizeInPixels.Height / (double)frameCount;
            var source = new Rect(0, frameIndex * sourceFrameHeight, image.SizeInPixels.Width, sourceFrameHeight);
            double scaledWidth = width * ValorantDemoVfxScale;
            double scaledHeight = height * ValorantDemoVfxScale;
            var target = SnapValorantRectToPhysicalPixels(new Rect(
                cx + (offsetX * ValorantDemoVfxScale) - (anchorX * scaledWidth),
                cy + (offsetY * ValorantDemoVfxScale) - (anchorY * scaledHeight),
                scaledWidth,
                scaledHeight));
            DrawNativeTintedSource(drawingSession, image, target, source, tint, opacity, additive);
            if (!mirrored)
            {
                return;
            }

            Matrix3x2 previous = drawingSession.Transform;
            drawingSession.Transform = Matrix3x2.CreateScale(-1, 1, new Vector2((float)cx, (float)cy)) * previous;
            DrawNativeTintedSource(drawingSession, image, target, source, tint, opacity, additive);
            drawingSession.Transform = previous;
        }

        private void DrawNativeRotatedParticle(
            CanvasDrawingSession drawingSession,
            CanvasBitmap image,
            int frameCount,
            double fps,
            double elapsedMs,
            double cx,
            double cy,
            double width,
            double height,
            double degrees,
            Color tint,
            double opacity)
        {
            Matrix3x2 previous = drawingSession.Transform;
            drawingSession.Transform = Matrix3x2.CreateRotation(
                (float)(degrees * Math.PI / 180.0),
                new Vector2((float)cx, (float)cy)) * previous;
            DrawNativeParticle(
                drawingSession,
                image,
                frameCount,
                fps,
                elapsedMs,
                cx,
                cy,
                width,
                height,
                0.5,
                0.5,
                0,
                0,
                false,
                false,
                tint,
                opacity);
            drawingSession.Transform = previous;
        }

        private void DrawNativeDissolvedImage(
            CanvasDrawingSession drawingSession,
            CanvasBitmap image,
            CanvasBitmap dissolveMask,
            double cx,
            double cy,
            double width,
            double height,
            double dissolveProgress,
            double opacity)
        {
            DrawNativeDissolvedTintedImage(
                drawingSession,
                image,
                dissolveMask,
                cx,
                cy,
                width,
                height,
                1.0,
                dissolveProgress,
                Colors.White,
                opacity);
        }

        private void DrawNativeDissolvedTintedImage(
            CanvasDrawingSession drawingSession,
            CanvasBitmap image,
            CanvasBitmap dissolveMask,
            double cx,
            double cy,
            double width,
            double height,
            double scale,
            double dissolveProgress,
            Color tint,
            double opacity)
        {
            if (image == null || opacity <= 0 || scale <= 0 || dissolveProgress <= 0)
            {
                return;
            }

            var target = SnapValorantRectToPhysicalPixels(new Rect(
                cx - (width * scale / 2.0),
                cy - (height * scale / 2.0),
                width * scale,
                height * scale));
            var source = new Rect(0, 0, image.SizeInPixels.Width, image.SizeInPixels.Height);
            using (var tintEffect = new ColorMatrixEffect
            {
                Source = image,
                ColorMatrix = CreateMultiplyTintMatrix(tint),
                CacheOutput = false
            })
            {
                if (dissolveMask == null || dissolveProgress >= 0.999)
                {
                    drawingSession.DrawImage(tintEffect, target, source, (float)Clamp01(opacity), GetValorantImageInterpolation(target, source));
                    return;
                }

                using (var luminance = new LuminanceToAlphaEffect { Source = dissolveMask, CacheOutput = false })
                using (var threshold = new LinearTransferEffect
                {
                    Source = luminance,
                    AlphaSlope = -10.0f,
                    AlphaOffset = (float)(Clamp01(dissolveProgress) * 11.0),
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
                    drawingSession.DrawImage(masked, target, source, (float)Clamp01(opacity), GetValorantImageInterpolation(target, source));
                }
            }
        }

        private void DrawNativeStretchedImage(
            CanvasDrawingSession drawingSession,
            CanvasBitmap image,
            double cx,
            double cy,
            double width,
            double height,
            double degrees,
            double opacity)
        {
            if (image == null || opacity <= 0)
            {
                return;
            }

            var target = SnapValorantRectToPhysicalPixels(new Rect(cx - width / 2.0, cy - height / 2.0, width, height));
            var source = new Rect(0, 0, image.SizeInPixels.Width, image.SizeInPixels.Height);
            Matrix3x2 previous = drawingSession.Transform;
            if (Math.Abs(degrees) > 0.001)
            {
                drawingSession.Transform = Matrix3x2.CreateRotation(
                    (float)(degrees * Math.PI / 180.0),
                    new Vector2((float)cx, (float)cy)) * previous;
            }

            drawingSession.DrawImage(image, target, source, (float)Clamp01(opacity), GetValorantImageInterpolation(target, source));
            drawingSession.Transform = previous;
        }

        private void DrawNativeTintedStretchedImage(
            CanvasDrawingSession drawingSession,
            CanvasBitmap image,
            double cx,
            double cy,
            double width,
            double height,
            double degrees,
            Color tint,
            double opacity)
        {
            if (image == null || opacity <= 0)
            {
                return;
            }

            var target = SnapValorantRectToPhysicalPixels(new Rect(cx - width / 2.0, cy - height / 2.0, width, height));
            var source = new Rect(0, 0, image.SizeInPixels.Width, image.SizeInPixels.Height);
            Matrix3x2 previous = drawingSession.Transform;
            if (Math.Abs(degrees) > 0.001)
            {
                drawingSession.Transform = Matrix3x2.CreateRotation(
                    (float)(degrees * Math.PI / 180.0),
                    new Vector2((float)cx, (float)cy)) * previous;
            }

            DrawMultiplyTintImage(drawingSession, image, target, source, tint, opacity);
            drawingSession.Transform = previous;
        }

        private void DrawNativeTintedSource(
            CanvasDrawingSession drawingSession,
            CanvasBitmap image,
            Rect target,
            Rect source,
            Color tint,
            double opacity,
            bool additive)
        {
            CanvasBlend previousBlend = drawingSession.Blend;
            if (additive)
            {
                drawingSession.Blend = CanvasBlend.Add;
            }

            DrawMultiplyTintImage(drawingSession, image, target, source, tint, opacity);
            drawingSession.Blend = previousBlend;
        }

        private static double GetNativeHeadshotBadgeScale(double elapsedMs)
        {
            return NativeSampleLinear(
                elapsedMs,
                new[] { 0.0, 50.0, 100.0, 150.0, 200.0, 250.0, 300.0, 350.0 },
                new[] { 1.155, 1.0, 1.106, 1.0, 1.076, 1.0, 1.049, 1.0 });
        }

        private static Color GetNativeHeadshotFlickerColor(double elapsedMs, bool inverse)
        {
            if (elapsedMs < 0 || elapsedMs > 550.0)
            {
                return inverse ? ValorantNativeHeadshotRed : Colors.White;
            }

            double[] times = { 0.0, 50.0, 100.0, 150.0, 200.0, 250.0, 300.0, 350.0 };
            double[] values = inverse
                ? new[] { 0.0, 1.0, 0.0, 1.0, 0.0, 1.0, 0.0, 1.0 }
                : new[] { 1.0, 0.0, 1.0, 0.0, 1.0, 0.0, 1.0, 0.0 };
            double whiteAmount = NativeSampleLinear(elapsedMs, times, values);
            return LerpValorantColor(ValorantNativeHeadshotRed, Colors.White, whiteAmount);
        }

        private static double NativeLinearProgress(double elapsedMs, double fromMs, double toMs)
        {
            if (toMs <= fromMs)
            {
                return elapsedMs >= toMs ? 1.0 : 0.0;
            }

            return Clamp01((elapsedMs - fromMs) / (toMs - fromMs));
        }

        private static double NativeSampleLinear(double elapsedMs, double[] times, double[] values)
        {
            if (times == null || values == null || times.Length == 0 || times.Length != values.Length)
            {
                return 0;
            }

            if (elapsedMs <= times[0])
            {
                return values[0];
            }

            for (int index = 1; index < times.Length; index++)
            {
                if (elapsedMs <= times[index])
                {
                    double progress = NativeLinearProgress(elapsedMs, times[index - 1], times[index]);
                    return Lerp(values[index - 1], values[index], progress);
                }
            }

            return values[values.Length - 1];
        }
    }
}

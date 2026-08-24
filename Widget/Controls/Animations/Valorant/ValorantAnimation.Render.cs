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
        private void DrawValorantKillFrame(CanvasDrawingSession drawingSession, int frame)
        {
            ValorantKillAsset asset = _currentValorantAsset;
            if (asset == null)
            {
                return;
            }

            double elapsedMs = frame * (1000.0 / FrameSequenceFps);
            double cx = ValorantFrameWidth / 2.0;
            double cy = ValorantFrameHeight / 2.0;
            ValorantDemoProfile profile = asset.DemoProfile ?? GetValorantDemoProfile(asset.PackKey);

            DrawValorantParticleCss(
                drawingSession,
                asset.BaseParticle,
                49,
                25,
                elapsedMs,
                cx,
                cy,
                112 * profile.BaseParticleScale,
                112 * profile.BaseParticleScale,
                0,
                206.0 / 256.0,
                0,
                profile.BaseParticleYOffset,
                true,
                false,
                asset.Accent);

            DrawValorantHalo(drawingSession, cx, cy, asset.Accent, profile.HaloRadius, elapsedMs, 1);

            DrawValorantBars(drawingSession, asset, cx, cy, elapsedMs);
            DrawCenteredImageAt(
                drawingSession,
                asset.Frame,
                cx,
                cy,
                ValorantDemoFrameCssWidth * profile.FrameWidthScale * ValorantDemoVfxScale,
                ValorantDemoFrameCssHeight * ValorantDemoVfxScale,
                1,
                1);
            if (asset.Blade != null)
            {
                double bladeSpin = ResolveValorantBladeRotation(profile, asset.SpinDirection, elapsedMs);
                DrawRotatedCenteredImageAt(drawingSession, asset.Blade, cx, cy, ValorantDemoBladeCssSize * ValorantDemoVfxScale, ValorantDemoBladeCssSize * ValorantDemoVfxScale, 1, bladeSpin, 1);
            }

            double emblemY = cy + (GetValorantDemoEmblemYOffset(elapsedMs) * ValorantDemoVfxScale);
            DrawCenteredImageAt(drawingSession, asset.Emblem, cx, emblemY, ValorantDemoEmblemCssSize * profile.EmblemScale * ValorantDemoVfxScale, ValorantDemoEmblemCssSize * profile.EmblemScale * ValorantDemoVfxScale, 1, 1);
            double flashOpacity = GetValorantDemoFlashOpacity(elapsedMs);
            if (flashOpacity > 0)
            {
                DrawCenteredFlashImageAt(
                    drawingSession,
                    asset.Emblem,
                    cx,
                    cy,
                    ValorantDemoEmblemCssSize * profile.EmblemScale * ValorantDemoVfxScale,
                    ValorantDemoEmblemCssSize * profile.EmblemScale * ValorantDemoVfxScale,
                    1,
                    flashOpacity,
                    ValorantDemoFlashColor,
                    asset.Brightness,
                    asset.Contrast);
            }

            if (asset.IsHeadshot)
            {
                double headshotScale = Lerp(1.8, 1.0, CubicBezierEase(Clamp01(elapsedMs / 250.0), 0.22, 0.9, 0.28, 1));
                double flicker = GetValorantDemoHeadshotFlickerAmount(elapsedMs);
                Windows.UI.Color headshotTint = flicker > 0
                    ? LerpValorantColor(ValorantDemoFlashColor, Windows.UI.Colors.White, flicker)
                    : ValorantDemoFlashColor;
                DrawCenteredTintedImageAt(
                    drawingSession,
                    asset.Headshot,
                    cx + (profile.HeadshotX * ValorantDemoVfxScale),
                    cy + (profile.HeadshotY * ValorantDemoVfxScale),
                    ValorantDemoHeadshotCssSize * ValorantDemoVfxScale,
                    ValorantDemoHeadshotCssSize * ValorantDemoVfxScale,
                    headshotScale,
                    headshotTint,
                    1);
            }

            if (asset.HeroFlame != null && profile.HeroFlame)
            {
                DrawValorantParticleCss(drawingSession, asset.HeroFlame, 20, 29, elapsedMs, cx, cy, 96, 108, 0.4934375, 0.49079242, 0, -30, false, false, asset.Accent);
            }

            if (asset.KillCount >= 5)
            {
                DrawValorantParticleCss(drawingSession, asset.LargeSparks, 52, 25, elapsedMs, cx, cy, 105 * profile.LargeSparksScale, 105 * profile.LargeSparksScale, 0.49052733, 0.5185547, 0, 2, false, true, asset.Accent);
            }

            if (asset.IsHeadshot)
            {
                DrawValorantParticleCss(drawingSession, asset.XSparks, 29, 25, elapsedMs, cx, cy, 56, 170, 0.48015872, 0.56722003, 0, -8, false, false, ValorantDemoFlashColor);
            }
        }

        private void DrawValorantBars(CanvasDrawingSession drawingSession, ValorantKillAsset asset, double cx, double cy, double elapsedMs)
        {
            if (asset.Bar == null)
            {
                return;
            }

            ValorantDemoProfile profile = asset.DemoProfile ?? GetValorantDemoProfile(asset.PackKey);
            int[] angles = ValorantDemoBarAngles[Math.Max(0, Math.Min(5, asset.KillCount - 1))];
            double spinDuration = asset.KillCount >= 5 ? 1000.0 : 700.0;
            double spin = (asset.KillCount >= 5 ? 360.0 : 180.0)
                * CubicBezierEase(Clamp01((elapsedMs - 750.0) / spinDuration), 0.22, 0.9, 0.28, 1);
            double baseDistance = (36.0 + profile.BarRadiusOffset) * ValorantDemoVfxScale;
            double distance = GetValorantDemoBarDistance(elapsedMs, baseDistance);
            double scale = GetValorantDemoBarScale(elapsedMs);
            foreach (int angle in angles)
            {
                double radians = (angle + spin) * Math.PI / 180.0;
                double x = cx + (Math.Sin(radians) * distance);
                double y = cy - (Math.Cos(radians) * distance);
                DrawRotatedCenteredImageAt(drawingSession, asset.Bar, x, y, 32 * ValorantDemoVfxScale, 32 * ValorantDemoVfxScale, scale, angle + spin, 1);
            }
        }

        private void DrawValorantParticleCss(
            CanvasDrawingSession drawingSession,
            CanvasBitmap image,
            int frameCount,
            int intervalMs,
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
            Color tintColor)
        {
            if (image == null)
            {
                return;
            }

            int frame = intervalMs <= 0 ? 0 : (int)Math.Floor(elapsedMs / intervalMs);
            frame = Math.Max(0, Math.Min(frameCount - 1, frame));
            double frameHeight = image.SizeInPixels.Height / (double)frameCount;
            var source = new Rect(0, frame * frameHeight, image.SizeInPixels.Width, frameHeight);
            double scaledWidth = width * ValorantDemoVfxScale;
            double scaledHeight = height * ValorantDemoVfxScale;
            double left = cx + (offsetX * ValorantDemoVfxScale) - (anchorX * scaledWidth);
            double top = cy + (offsetY * ValorantDemoVfxScale) - (anchorY * scaledHeight);
            Rect target = SnapValorantRectToPhysicalPixels(new Rect(left, top, scaledWidth, scaledHeight));
            if (mirrored)
            {
                DrawValorantTintedImage(drawingSession, image, target, source, tintColor, additive);
                Matrix3x2 previous = drawingSession.Transform;
                drawingSession.Transform = Matrix3x2.CreateScale(-1, 1, new Vector2((float)cx, (float)cy)) * previous;
                DrawValorantTintedImage(drawingSession, image, target, source, tintColor, additive);
                drawingSession.Transform = previous;
                return;
            }

            DrawValorantTintedImage(drawingSession, image, target, source, tintColor, additive);
        }

    }
}

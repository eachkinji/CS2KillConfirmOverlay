using System;
using System.Collections.Generic;
using System.Numerics;
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
        private const double ValorantFrameWidth = 607;
        private const double ValorantFrameHeight = 436;
        private const int ValorantFrameCount = 156;
        private const float ValorantGaiaBrightness = 1.3f;
        private const float ValorantGaiaContrast = 1.1f;
        private static readonly Dictionary<string, ValorantKillAsset> ValorantCache = new Dictionary<string, ValorantKillAsset>();
        private static readonly Random ValorantSpinRandom = new Random();
        private static readonly object ValorantSpinRandomLock = new object();

        private async Task<AnimationAsset> LoadValorantKillAssetAsync(
            string packKey,
            int killCount,
            bool isHeadshot,
            IProgress<int> progress = null)
        {
            string normalizedKey = ValorantPackService.IsValorantPackKey(packKey)
                ? packKey.Trim().ToLowerInvariant()
                : ValorantPackService.DefaultKey;
            string cacheKey = normalizedKey + ":" + Math.Max(1, Math.Min(6, killCount)) + ":" + isHeadshot;
            if (!ValorantCache.TryGetValue(cacheKey, out ValorantKillAsset asset))
            {
                asset = await LoadValorantKillAssetCoreAsync(normalizedKey, killCount, isHeadshot, progress);
                ValorantCache[cacheKey] = asset;
            }

            asset.SpinDirection = NextValorantSpinDirection();
            progress?.Report(100);
            return new AnimationAsset(
                new SpriteMetadata
                {
                    FrameWidth = (int)ValorantFrameWidth,
                    FrameHeight = (int)ValorantFrameHeight,
                    Frames = ValorantFrameCount,
                    Fps = FrameSequenceFps
                },
                asset);
        }

        private async Task<ValorantKillAsset> LoadValorantKillAssetCoreAsync(
            string packKey,
            int killCount,
            bool isHeadshot,
            IProgress<int> progress)
        {
            string folder = ValorantPackService.GetFolder(packKey) ?? ValorantPackService.GetFolder(ValorantPackService.DefaultKey);
            string root = $"ms-appx:///Assets/GameStyles/valorant/killconfirm/{folder}";
            ValorantDemoProfile profile = GetValorantDemoProfile(packKey);

            progress?.Report(20);
            var asset = new ValorantKillAsset
            {
                PackKey = packKey,
                KillCount = Math.Max(1, Math.Min(6, killCount)),
                IsHeadshot = isHeadshot,
                Accent = profile.Accent,
                Brightness = profile.IsGaia ? ValorantGaiaBrightness : 1.0f,
                Contrast = profile.IsGaia ? ValorantGaiaContrast : 1.0f,
                SpinDirection = NextValorantSpinDirection(),
                DemoProfile = profile,
                Frame = await LoadValorantTextureAsync(root, profile.Frame),
                Emblem = await LoadValorantTextureAsync(root, profile.Emblem),
                Bar = await LoadValorantTextureAsync(root, profile.Bar),
                Blade = string.IsNullOrWhiteSpace(profile.Blade) ? null : await LoadValorantTextureAsync(root, profile.Blade),
                Headshot = await LoadValorantTextureAsync(root, "killicon_valorant_headshot.png")
            };

            progress?.Report(60);
            asset.BaseParticle = await LoadValorantTextureAsync(root, "killicon_valorant_particle_base_t1.png");
            asset.HeroFlame = await TryLoadValorantTextureAsync(root, "killicon_valorant_particle_hero_flame.png");
            asset.LargeSparks = await LoadValorantTextureAsync(root, "killicon_valorant_particle_large_sparks.png");
            asset.XSparks = await LoadValorantTextureAsync(root, "killicon_valorant_particle_x_sparks.png");
            return asset;
        }

        private static async Task<CanvasBitmap> LoadValorantTextureAsync(string root, string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new InvalidOperationException("Missing Valorant texture.");
            }

            return await LoadBitmapFromApplicationUriAsync(root + "/textures/" + fileName);
        }

        private static async Task<CanvasBitmap> TryLoadValorantTextureAsync(string root, string fileName)
        {
            try
            {
                return await LoadValorantTextureAsync(root, fileName);
            }
            catch
            {
                return null;
            }
        }

        private void DrawValorantKillFrame(CanvasDrawingSession drawingSession, int frame)
        {
            ValorantKillAsset asset = _currentValorantAsset;
            if (asset == null)
            {
                return;
            }

            double elapsedMs = frame * (1000.0 / FrameSequenceFps);
            double opacity = GetValorantDemoLifeOpacity(asset.KillCount, elapsedMs);
            if (opacity <= 0)
            {
                return;
            }

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
                opacity,
                true,
                false,
                asset.Accent,
                null,
                0,
                0);
            DrawValorantHalo(drawingSession, asset.Accent, cx, cy, elapsedMs, opacity, profile.HaloRadius);

            DrawValorantBars(drawingSession, asset, cx, cy, elapsedMs, opacity);
            DrawCenteredImageWithShadowAt(
                drawingSession,
                asset.Frame,
                cx,
                cy,
                ValorantDemoFrameCssWidth * profile.FrameWidthScale * ValorantDemoVfxScale,
                ValorantDemoFrameCssHeight * ValorantDemoVfxScale,
                1,
                opacity,
                Color.FromArgb(255, 0, 0, 0),
                12,
                0,
                0,
                0.62,
                asset.Brightness,
                asset.Contrast);
            if (asset.Blade != null)
            {
                double bladeSpin = ResolveValorantBladeRotation(profile, asset.SpinDirection, elapsedMs);
                DrawRotatedCenteredImageWithShadowAt(drawingSession, asset.Blade, cx, cy, ValorantDemoBladeCssSize * ValorantDemoVfxScale, ValorantDemoBladeCssSize * ValorantDemoVfxScale, 1, bladeSpin, opacity, Color.FromArgb(255, 0, 0, 0), 12, 0, 0, 0.62, asset.Brightness, asset.Contrast);
            }

            double emblemY = cy + (GetValorantDemoEmblemYOffset(elapsedMs) * ValorantDemoVfxScale);
            DrawCenteredImageWithShadowAt(drawingSession, asset.Emblem, cx, emblemY, ValorantDemoEmblemCssSize * profile.EmblemScale * ValorantDemoVfxScale, ValorantDemoEmblemCssSize * profile.EmblemScale * ValorantDemoVfxScale, 1, opacity, Color.FromArgb(255, 0, 0, 0), 12, 0, 0, 0.62, asset.Brightness, asset.Contrast);
            double flashOpacity = GetValorantDemoFlashOpacity(elapsedMs) * opacity;
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
                Color headshotGlow = elapsedMs <= 250.0
                    ? LerpColor(Colors.White, ValorantDemoFlashColor, Clamp01(elapsedMs / 250.0))
                    : ValorantDemoFlashColor;
                DrawCenteredImageWithShadowAt(
                    drawingSession,
                    asset.Headshot,
                    cx + (profile.HeadshotX * ValorantDemoVfxScale),
                    cy + (profile.HeadshotY * ValorantDemoVfxScale),
                    ValorantDemoHeadshotCssSize * ValorantDemoVfxScale,
                    ValorantDemoHeadshotCssSize * ValorantDemoVfxScale,
                    headshotScale,
                    opacity,
                    headshotGlow,
                    8,
                    0,
                    0,
                    0.9);
            }

            if (asset.HeroFlame != null && profile.HeroFlame)
            {
                DrawValorantParticleCss(drawingSession, asset.HeroFlame, 20, 29, elapsedMs, cx, cy, 96, 108, 0.4934375, 0.49079242, 0, -30, 0.7 * opacity, false, false, asset.Accent, null, 0, 0);
            }

            if (asset.KillCount >= 5)
            {
                DrawValorantParticleCss(drawingSession, asset.LargeSparks, 52, 25, elapsedMs, cx, cy, 105 * profile.LargeSparksScale, 105 * profile.LargeSparksScale, 0.49052733, 0.5185547, 0, 2, 0.82 * opacity, false, true, asset.Accent, null, 0, 0);
            }

            if (asset.IsHeadshot)
            {
                DrawValorantParticleCss(drawingSession, asset.XSparks, 29, 25, elapsedMs, cx, cy, 56, 170, 0.48015872, 0.56722003, 0, -8, 0.9 * opacity, false, false, ValorantDemoFlashColor, null, 0, 0);
            }
        }

        private static void DrawValorantBars(CanvasDrawingSession drawingSession, ValorantKillAsset asset, double cx, double cy, double elapsedMs, double opacity)
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
                DrawRotatedCenteredImageWithShadowAt(drawingSession, asset.Bar, x, y, 32 * ValorantDemoVfxScale, 32 * ValorantDemoVfxScale, scale, angle + spin, opacity, asset.Accent, 5, 0, 0, 0.75, asset.Brightness, asset.Contrast);
            }
        }

        private static void DrawValorantParticleCss(
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
            double opacity,
            bool mirrored,
            bool additive,
            Color? tintColor,
            Color? glowColor,
            double glowBlur,
            double glowOpacity)
        {
            if (image == null || opacity <= 0)
            {
                return;
            }

            CanvasBlend previousBlend = drawingSession.Blend;
            if (additive)
            {
                drawingSession.Blend = CanvasBlend.Add;
            }

            int frame = intervalMs <= 0 ? 0 : (int)Math.Floor(elapsedMs / intervalMs);
            frame = Math.Max(0, Math.Min(frameCount - 1, frame));
            double frameHeight = image.SizeInPixels.Height / (double)frameCount;
            var source = new Rect(0, frame * frameHeight, image.SizeInPixels.Width, frameHeight);
            double scaledWidth = width * ValorantDemoVfxScale;
            double scaledHeight = height * ValorantDemoVfxScale;
            double left = cx + (offsetX * ValorantDemoVfxScale) - (anchorX * scaledWidth);
            double top = cy + (offsetY * ValorantDemoVfxScale) - (anchorY * scaledHeight);
            var target = new Rect(left, top, scaledWidth, scaledHeight);
            if (mirrored)
            {
                DrawImageWithOptionalGlow(drawingSession, image, target, source, opacity, tintColor, glowColor, glowBlur, glowOpacity);
                Matrix3x2 previous = drawingSession.Transform;
                drawingSession.Transform = Matrix3x2.CreateScale(-1, 1, new Vector2((float)cx, (float)cy)) * previous;
                DrawImageWithOptionalGlow(drawingSession, image, target, source, opacity, tintColor, glowColor, glowBlur, glowOpacity);
                drawingSession.Transform = previous;
                drawingSession.Blend = previousBlend;
                return;
            }

            DrawImageWithOptionalGlow(drawingSession, image, target, source, opacity, tintColor, glowColor, glowBlur, glowOpacity);
            drawingSession.Blend = previousBlend;
        }

        private static void DrawValorantHalo(CanvasDrawingSession drawingSession, Color accent, double cx, double cy, double elapsedMs, double opacity, double radius)
        {
            const int Segments = 72;
            double scaledRadius = radius * ValorantDemoVfxScale;
            double ringWidth = 2.2 * ValorantDemoVfxScale;
            double rotation = (elapsedMs % 2200.0) / 2200.0 * Math.PI * 2.0;
            double minY = cy - scaledRadius;
            double maxY = cy + scaledRadius;
            double yRange = Math.Max(0.001, maxY - minY);

            for (int i = 0; i < Segments; i++)
            {
                double a0 = (Math.PI * 2.0 * i / Segments) + rotation;
                double a1 = (Math.PI * 2.0 * (i + 1) / Segments) + rotation;
                double y0 = cy + Math.Sin(a0) * scaledRadius;
                double y1 = cy + Math.Sin(a1) * scaledRadius;
                double alphaFactor = Clamp01((maxY - ((y0 + y1) * 0.5)) / yRange);
                byte alpha = (byte)Math.Max(0, Math.Min(255, Math.Round(opacity * alphaFactor * 255.0)));
                if (alpha <= 2)
                {
                    continue;
                }

                Color color = Color.FromArgb(alpha, accent.R, accent.G, accent.B);
                drawingSession.DrawLine(
                    (float)(cx + Math.Cos(a0) * scaledRadius),
                    (float)(cy + Math.Sin(a0) * scaledRadius),
                    (float)(cx + Math.Cos(a1) * scaledRadius),
                    (float)(cy + Math.Sin(a1) * scaledRadius),
                    color,
                    (float)ringWidth);
            }
        }

        private static void DrawCenteredImageAt(CanvasDrawingSession drawingSession, CanvasBitmap image, double cx, double cy, double width, double height, double scale, double opacity)
        {
            if (image == null || opacity <= 0 || scale <= 0)
            {
                return;
            }

            var target = BuildCenteredImageRect(image, cx, cy, width, height, scale);
            var source = new Rect(0, 0, image.SizeInPixels.Width, image.SizeInPixels.Height);
            drawingSession.DrawImage(image, target, source, (float)Math.Max(0.0, Math.Min(1.0, opacity)), CanvasImageInterpolation.Linear);
        }

        private static void DrawCenteredImageWithShadowAt(
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

        private static void DrawRotatedCenteredImageWithShadowAt(
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

        private static void DrawCenteredFlashImageAt(
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
            DrawSoftSilhouette(drawingSession, image, target, source, flashColor, 12, 0, 0, opacity * 0.9, true);
            DrawBrightnessContrastImage(drawingSession, image, target, source, opacity, 1.8f * brightness, contrast);
        }

        private static Rect BuildCenteredImageRect(CanvasBitmap image, double cx, double cy, double width, double height, double scale)
        {
            double fitScale = Math.Min(width / image.SizeInPixels.Width, height / image.SizeInPixels.Height) * scale;
            double w = image.SizeInPixels.Width * fitScale;
            double h = image.SizeInPixels.Height * fitScale;
            return new Rect(cx - w / 2.0, cy - h / 2.0, w, h);
        }

        private static void DrawImageWithOptionalGlow(
            CanvasDrawingSession drawingSession,
            CanvasBitmap image,
            Rect target,
            Rect source,
            double opacity,
            Color? tintColor,
            Color? glowColor,
            double glowBlur,
            double glowOpacity)
        {
            if (glowColor != null && glowOpacity > 0 && glowBlur > 0)
            {
                DrawSoftSilhouette(drawingSession, image, target, source, glowColor.Value, glowBlur, 0, 0, opacity * glowOpacity, true);
            }

            if (tintColor != null)
            {
                DrawMultiplyTintImage(drawingSession, image, target, source, tintColor.Value, opacity);
                return;
            }

            drawingSession.DrawImage(image, target, source, (float)Math.Max(0.0, Math.Min(1.0, opacity)), CanvasImageInterpolation.Linear);
        }

        private static void DrawImageWithSoftShadow(
            CanvasDrawingSession drawingSession,
            CanvasBitmap image,
            Rect target,
            Rect source,
            double opacity,
            Color shadowColor,
            double shadowBlur,
            double shadowOffsetX,
            double shadowOffsetY,
            double shadowOpacity,
            float brightness = 1.0f,
            float contrast = 1.0f)
        {
            if (shadowOpacity > 0 && shadowBlur > 0)
            {
                bool additive = shadowColor.R > 8 || shadowColor.G > 8 || shadowColor.B > 8;
                DrawSoftSilhouette(drawingSession, image, target, source, shadowColor, shadowBlur, shadowOffsetX, shadowOffsetY, opacity * shadowOpacity, additive);
            }

            if (Math.Abs(brightness - 1.0f) > 0.001f || Math.Abs(contrast - 1.0f) > 0.001f)
            {
                DrawBrightnessContrastImage(drawingSession, image, target, source, opacity, brightness, contrast);
                return;
            }

            drawingSession.DrawImage(image, target, source, (float)Math.Max(0.0, Math.Min(1.0, opacity)), CanvasImageInterpolation.Linear);
        }

        private static void DrawSoftSilhouette(
            CanvasDrawingSession drawingSession,
            CanvasBitmap image,
            Rect target,
            Rect source,
            Color color,
            double blur,
            double offsetX,
            double offsetY,
            double opacity,
            bool additive)
        {
            if (opacity <= 0)
            {
                return;
            }

            CanvasBlend previousBlend = drawingSession.Blend;
            if (additive)
            {
                drawingSession.Blend = CanvasBlend.Add;
            }

            Rect baseTarget = OffsetRect(target, offsetX * ValorantDemoVfxScale, offsetY * ValorantDemoVfxScale);
            double blurScale = blur * ValorantDemoVfxScale;
            double[] radii = { 0.0, blurScale * 0.26, blurScale * 0.52 };
            double[] opacityFactors = { 0.34, 0.28, 0.18 };

            for (int layer = 0; layer < radii.Length; layer++)
            {
                double radius = radii[layer];
                double layerOpacity = opacity * opacityFactors[layer];
                if (radius <= 0.01)
                {
                    DrawAlphaTintImage(drawingSession, image, baseTarget, source, color, layerOpacity);
                    continue;
                }

                DrawAlphaTintImage(drawingSession, image, OffsetRect(baseTarget, radius, 0), source, color, layerOpacity);
                DrawAlphaTintImage(drawingSession, image, OffsetRect(baseTarget, -radius, 0), source, color, layerOpacity);
                DrawAlphaTintImage(drawingSession, image, OffsetRect(baseTarget, 0, radius), source, color, layerOpacity);
                DrawAlphaTintImage(drawingSession, image, OffsetRect(baseTarget, 0, -radius), source, color, layerOpacity);
                DrawAlphaTintImage(drawingSession, image, OffsetRect(baseTarget, radius * 0.707, radius * 0.707), source, color, layerOpacity * 0.7);
                DrawAlphaTintImage(drawingSession, image, OffsetRect(baseTarget, -radius * 0.707, radius * 0.707), source, color, layerOpacity * 0.7);
                DrawAlphaTintImage(drawingSession, image, OffsetRect(baseTarget, radius * 0.707, -radius * 0.707), source, color, layerOpacity * 0.7);
                DrawAlphaTintImage(drawingSession, image, OffsetRect(baseTarget, -radius * 0.707, -radius * 0.707), source, color, layerOpacity * 0.7);
            }

            drawingSession.Blend = previousBlend;
        }

        private static Rect OffsetRect(Rect rect, double offsetX, double offsetY)
        {
            return new Rect(rect.X + offsetX, rect.Y + offsetY, rect.Width, rect.Height);
        }

        private static void DrawAlphaTintImage(CanvasDrawingSession drawingSession, CanvasBitmap image, Rect target, Rect source, Color tint, double opacity)
        {
            float clampedOpacity = (float)Math.Max(0.0, Math.Min(1.0, opacity));
            var tintEffect = new ColorMatrixEffect
            {
                Source = image,
                ColorMatrix = CreateAlphaTintMatrix(tint)
            };

            drawingSession.DrawImage(tintEffect, target, source, clampedOpacity, CanvasImageInterpolation.Linear);
        }

        private static void DrawBrightnessContrastImage(CanvasDrawingSession drawingSession, CanvasBitmap image, Rect target, Rect source, double opacity, float brightness, float contrast = 1.0f)
        {
            var brightnessEffect = new ColorMatrixEffect
            {
                Source = image,
                ColorMatrix = CreateBrightnessContrastMatrix(brightness, contrast)
            };

            drawingSession.DrawImage(brightnessEffect, target, source, (float)Math.Max(0.0, Math.Min(1.0, opacity)), CanvasImageInterpolation.Linear);
        }

        private static void DrawMultiplyTintImage(CanvasDrawingSession drawingSession, CanvasBitmap image, Rect target, Rect source, Color tint, double opacity)
        {
            var tintEffect = new ColorMatrixEffect
            {
                Source = image,
                ColorMatrix = CreateMultiplyTintMatrix(tint)
            };

            drawingSession.DrawImage(tintEffect, target, source, (float)Math.Max(0.0, Math.Min(1.0, opacity)), CanvasImageInterpolation.Linear);
        }

        private static Matrix5x4 CreateAlphaTintMatrix(Color tint)
        {
            return new Matrix5x4
            {
                M41 = tint.R / 255.0f,
                M42 = tint.G / 255.0f,
                M43 = tint.B / 255.0f,
                M44 = 1.0f
            };
        }

        private static Matrix5x4 CreateBrightnessContrastMatrix(float brightness, float contrast)
        {
            float clampedBrightness = Math.Max(0.0f, brightness);
            float clampedContrast = Math.Max(0.0f, contrast);
            float scale = clampedBrightness * clampedContrast;
            float offset = (0.5f - 0.5f * clampedContrast) * clampedBrightness;
            return new Matrix5x4
            {
                M11 = scale,
                M22 = scale,
                M33 = scale,
                M44 = 1.0f,
                M51 = offset,
                M52 = offset,
                M53 = offset
            };
        }

        private static Matrix5x4 CreateMultiplyTintMatrix(Color tint)
        {
            return new Matrix5x4
            {
                M11 = tint.R / 255.0f,
                M22 = tint.G / 255.0f,
                M33 = tint.B / 255.0f,
                M44 = 1.0f
            };
        }

        private static Color LerpColor(Color from, Color to, double amount)
        {
            amount = Clamp01(amount);
            return Color.FromArgb(
                (byte)Math.Round(Lerp(from.A, to.A, amount)),
                (byte)Math.Round(Lerp(from.R, to.R, amount)),
                (byte)Math.Round(Lerp(from.G, to.G, amount)),
                (byte)Math.Round(Lerp(from.B, to.B, amount)));
        }

        private static void DrawRotatedCenteredImageAt(CanvasDrawingSession drawingSession, CanvasBitmap image, double cx, double cy, double width, double height, double scale, double degrees, double opacity)
        {
            Matrix3x2 previous = drawingSession.Transform;
            drawingSession.Transform = Matrix3x2.CreateRotation((float)(degrees * Math.PI / 180.0), new Vector2((float)cx, (float)cy)) * previous;
            DrawCenteredImageAt(drawingSession, image, cx, cy, width, height, scale, opacity);
            drawingSession.Transform = previous;
        }
    }
}

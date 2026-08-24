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
        private void DrawImageWithOptionalGlow(
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

        private void DrawImageWithSoftShadow(
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

        private void DrawSoftSilhouette(
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

            EnsureValorantEffects();
            double blurScale = Math.Max(0.01, blur * ValorantDemoVfxScale);
            double sourceUnitsPerTargetUnit = Math.Max(
                source.Width / Math.Max(1.0, target.Width),
                source.Height / Math.Max(1.0, target.Height));
            double sourceBlur = blurScale * sourceUnitsPerTargetUnit;
            double targetPadding = blurScale * 3.0;
            double sourcePadding = sourceBlur * 3.0;
            Rect shadowTarget = new Rect(
                target.X + (offsetX * ValorantDemoVfxScale) - targetPadding,
                target.Y + (offsetY * ValorantDemoVfxScale) - targetPadding,
                target.Width + targetPadding * 2.0,
                target.Height + targetPadding * 2.0);
            Rect shadowSource = new Rect(
                source.X - sourcePadding,
                source.Y - sourcePadding,
                source.Width + sourcePadding * 2.0,
                source.Height + sourcePadding * 2.0);
            _valorantShadowEffect.Source = image;
            _valorantShadowEffect.ShadowColor = Color.FromArgb(255, color.R, color.G, color.B);
            _valorantShadowEffect.BlurAmount = (float)Math.Min(250.0, sourceBlur);
            drawingSession.DrawImage(
                _valorantShadowEffect,
                shadowTarget,
                shadowSource,
                (float)Clamp01(opacity),
                CanvasImageInterpolation.Linear);

            drawingSession.Blend = previousBlend;
        }

        private void EnsureValorantEffects()
        {
            if (_valorantShadowEffect == null)
            {
                _valorantShadowEffect = new ShadowEffect
                {
                    Optimization = EffectOptimization.Speed,
                    CacheOutput = false
                };
            }

            if (_valorantColorMatrixEffect == null)
            {
                _valorantColorMatrixEffect = new ColorMatrixEffect
                {
                    CacheOutput = false
                };
            }
        }

        private void ReleaseValorantEffects()
        {
            _valorantShadowEffect?.Dispose();
            _valorantShadowEffect = null;
            _valorantColorMatrixEffect?.Dispose();
            _valorantColorMatrixEffect = null;
        }

        private void DrawBrightnessContrastImage(CanvasDrawingSession drawingSession, CanvasBitmap image, Rect target, Rect source, double opacity, float brightness, float contrast = 1.0f)
        {
            EnsureValorantEffects();
            _valorantColorMatrixEffect.Source = image;
            _valorantColorMatrixEffect.ColorMatrix = CreateBrightnessContrastMatrix(brightness, contrast);
            drawingSession.DrawImage(_valorantColorMatrixEffect, target, source, (float)Clamp01(opacity), GetValorantImageInterpolation(target, source));
        }

        private void DrawMultiplyTintImage(CanvasDrawingSession drawingSession, CanvasBitmap image, Rect target, Rect source, Color tint, double opacity)
        {
            EnsureValorantEffects();
            _valorantColorMatrixEffect.Source = image;
            _valorantColorMatrixEffect.ColorMatrix = CreateMultiplyTintMatrix(tint);
            drawingSession.DrawImage(_valorantColorMatrixEffect, target, source, (float)Clamp01(opacity), GetValorantImageInterpolation(target, source));
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

        private void DrawRotatedCenteredImageAt(CanvasDrawingSession drawingSession, CanvasBitmap image, double cx, double cy, double width, double height, double scale, double degrees, double opacity)
        {
            cx = SnapValorantCoordinateToPhysicalPixel(cx);
            cy = SnapValorantCoordinateToPhysicalPixel(cy);
            Matrix3x2 previous = drawingSession.Transform;
            drawingSession.Transform = Matrix3x2.CreateRotation((float)(degrees * Math.PI / 180.0), new Vector2((float)cx, (float)cy)) * previous;
            DrawCenteredImageAt(drawingSession, image, cx, cy, width, height, scale, opacity);
            drawingSession.Transform = previous;
        }
    }
}

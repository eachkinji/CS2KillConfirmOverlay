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
        private static void DrawBattlefield2042CachedGlow(
            CanvasDrawingSession drawingSession,
            Battlefield2042GlowCache cache,
            double x,
            double y,
            double opacity)
        {
            if (cache?.Surface == null || opacity <= 0)
            {
                return;
            }

            Rect source = new Rect(0, 0, cache.Surface.Size.Width, cache.Surface.Size.Height);
            Rect target = new Rect(
                x + cache.OffsetX,
                y + cache.OffsetY,
                source.Width,
                source.Height);
            CanvasBlend previousBlend = drawingSession.Blend;
            try
            {
                drawingSession.Blend = CanvasBlend.Add;
                drawingSession.DrawImage(
                    cache.Surface,
                    target,
                    source,
                    (float)Clamp01(opacity),
                    CanvasImageInterpolation.Linear);
            }
            finally
            {
                drawingSession.Blend = previousBlend;
            }
        }

        private static void DrawBattlefield2042CachedGlowingTextAtLayoutOrigin(
            CanvasDrawingSession drawingSession,
            string text,
            double originX,
            double originY,
            double scale,
            Color color,
            double glowStrength,
            CanvasTextFormat format,
            Battlefield2042GlowCache cache)
        {
            if (string.IsNullOrEmpty(text) || scale <= 0 || color.A == 0)
            {
                return;
            }

            if (cache != null)
            {
                DrawBattlefield2042CachedGlow(
                    drawingSession,
                    cache,
                    Math.Round(originX),
                    Math.Round(originY),
                    color.A / 255.0);
                DrawBattlefieldTextAtLayoutOrigin(
                    drawingSession,
                    text,
                    originX,
                    originY,
                    scale,
                    color,
                    format);
                return;
            }

            DrawBattlefield2042GlowingTextAtLayoutOrigin(
                drawingSession,
                text,
                originX,
                originY,
                scale,
                color,
                glowStrength,
                format);
        }

        private static void DrawBattlefield2042CachedGlowingRectangle(
            CanvasDrawingSession drawingSession,
            Rect rect,
            Color color,
            double opacity,
            double bloomStrength,
            Battlefield2042GlowCache cache)
        {
            opacity = Clamp01(opacity);
            bloomStrength = Clamp01(bloomStrength);
            if (opacity <= 0 || rect.Width <= 0 || rect.Height <= 0)
            {
                return;
            }

            if (cache == null)
            {
                DrawBattlefield2042GlowingRectangle(
                    drawingSession,
                    rect,
                    color,
                    opacity,
                    bloomStrength);
                return;
            }

            DrawBattlefield2042CachedGlow(drawingSession, cache, rect.X, rect.Y, opacity);
            byte coreAlpha = (byte)Math.Max(
                0,
                Math.Min(255, Math.Round(opacity * (0.12 + bloomStrength * 0.08) * 255)));
            drawingSession.FillRectangle(
                rect,
                Color.FromArgb(coreAlpha, color.R, color.G, color.B));
        }

        private static void DrawBattlefield2042GlowingTextAtLayoutOrigin(
            CanvasDrawingSession drawingSession,
            string text,
            double originX,
            double originY,
            double scale,
            Color color,
            double glowStrength,
            CanvasTextFormat format)
        {
            if (string.IsNullOrEmpty(text) || scale <= 0 || color.A == 0)
            {
                return;
            }

            glowStrength = Clamp01(glowStrength);
            byte glowAlpha = (byte)Math.Max(
                0,
                Math.Min(255, Math.Round(color.A * (0.34 + glowStrength * 0.34))));
            using (CanvasCommandList glowSource = new CanvasCommandList(drawingSession))
            {
                using (CanvasDrawingSession glowSession = glowSource.CreateDrawingSession())
                {
                    glowSession.Transform =
                        Matrix3x2.CreateScale((float)scale)
                        * Matrix3x2.CreateTranslation(
                            (float)Math.Round(originX),
                            (float)Math.Round(originY));
                    using (CanvasSolidColorBrush glowBrush = new CanvasSolidColorBrush(
                        glowSession,
                        Color.FromArgb(glowAlpha, color.R, color.G, color.B)))
                    {
                        glowSession.DrawText(text, 0, 0, glowBrush, format);
                    }
                }

                DrawBattlefield2042BlurredSource(
                    drawingSession,
                    glowSource,
                    (float)(3.2 + glowStrength * 1.4));
                DrawBattlefield2042BlurredSource(
                    drawingSession,
                    glowSource,
                    (float)(0.9 + glowStrength * 0.75));
            }

            DrawBattlefieldTextAtLayoutOrigin(
                drawingSession,
                text,
                originX,
                originY,
                scale,
                color,
                format);
        }

        private static void DrawBattlefield2042GlowingRectangle(
            CanvasDrawingSession drawingSession,
            Rect rect,
            Color color,
            double opacity,
            double bloomStrength)
        {
            opacity = Clamp01(opacity);
            bloomStrength = Clamp01(bloomStrength);
            if (opacity <= 0 || rect.Width <= 0 || rect.Height <= 0)
            {
                return;
            }

            byte glowAlpha = (byte)Math.Max(
                0,
                Math.Min(
                    255,
                    Math.Round(opacity * (0.34 + bloomStrength * 0.26) * 255)));
            using (CanvasCommandList glowSource = new CanvasCommandList(drawingSession))
            {
                using (CanvasDrawingSession glowSession = glowSource.CreateDrawingSession())
                {
                    glowSession.FillRectangle(
                        rect,
                        Color.FromArgb(glowAlpha, color.R, color.G, color.B));
                }

                DrawBattlefield2042BlurredSource(
                    drawingSession,
                    glowSource,
                    (float)(4.8 + bloomStrength * 2.4));
                DrawBattlefield2042BlurredSource(
                    drawingSession,
                    glowSource,
                    (float)(1.25 + bloomStrength * 1.3));
            }

            byte coreAlpha = (byte)Math.Max(
                0,
                Math.Min(
                    255,
                    Math.Round(opacity * (0.12 + bloomStrength * 0.08) * 255)));
            drawingSession.FillRectangle(
                rect,
                Color.FromArgb(coreAlpha, color.R, color.G, color.B));
        }

        private static void DrawBattlefield2042BlurredSource(
            CanvasDrawingSession drawingSession,
            CanvasCommandList source,
            float blurAmount)
        {
            using (GaussianBlurEffect blur = new GaussianBlurEffect
            {
                Source = source,
                BlurAmount = Math.Max(0.01f, blurAmount),
                Optimization = EffectOptimization.Speed,
                BorderMode = EffectBorderMode.Soft
            })
            {
                CanvasBlend previousBlend = drawingSession.Blend;
                try
                {
                    drawingSession.Blend = CanvasBlend.Add;
                    drawingSession.DrawImage(blur);
                }
                finally
                {
                    drawingSession.Blend = previousBlend;
                }
            }
        }

        private void DrawBattlefield2042KilllogExitGlitch(
            CanvasDrawingSession drawingSession,
            double now)
        {
            if (_battlefield2042HudState.LastKillLogTriggerTimeMs < 0)
            {
                return;
            }

            double elapsed = now - _battlefield2042HudState.LastKillLogTriggerTimeMs;
            if (elapsed < 3100 || elapsed >= Battlefield2042KillLogDurationMs)
            {
                return;
            }

            Battlefield2042GlitchBar[] bars = elapsed < 3133.3333
                ? Battlefield2042KilllogGlitchBarsA
                : Battlefield2042KilllogGlitchBarsB;
            DrawBattlefield2042GlitchBars(
                drawingSession,
                bars,
                Battlefield2042FrameWidth / 2.0,
                150,
                Battlefield2042KilllogGlitchColor,
                0);
        }
        private static void DrawBattlefield2042Text(
            CanvasDrawingSession drawingSession,
            string text,
            double x,
            double y,
            double scale,
            Color color,
            CanvasTextFormat format)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            Color shadow = Color.FromArgb((byte)(color.A * 0.72), 0, 0, 0);
            DrawBattlefieldText(drawingSession, text, x + 1, y + 1, scale, shadow, format);
            DrawBattlefieldText(drawingSession, text, x, y, scale, color, format);
        }

        private static void DrawBattlefield2042Image(
            CanvasDrawingSession drawingSession,
            CanvasBitmap image,
            Rect target,
            double opacity)
        {
            if (image == null || opacity <= 0)
            {
                return;
            }

            drawingSession.DrawImage(
                image,
                target,
                new Rect(0, 0, image.SizeInPixels.Width, image.SizeInPixels.Height),
                (float)Clamp01(opacity),
                CanvasImageInterpolation.Linear);
        }

    }
}

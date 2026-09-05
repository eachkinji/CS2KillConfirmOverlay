using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Threading.Tasks;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Text;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Text;
using Windows.UI.Xaml;

namespace KillConfirmGameBar.Controls
{
    public sealed partial class KillConfirmAnimation
    {
        private void DrawModernWarfare2019Money(
            CanvasDrawingSession drawingSession,
            CanvasTextFormat format,
            double elapsedMs)
        {
            string text = "+$" + _modernWarfare2019MoneyReward.ToString(CultureInfo.InvariantCulture);
            double opacity = elapsedMs <= ModernWarfare2019MoneyHoldEndMs
                ? 1.0
                : 1.0 - ModernWarfare2019SmoothStep(
                    (elapsedMs - ModernWarfare2019MoneyHoldEndMs)
                    / (ModernWarfare2019MoneyEndMs - ModernWarfare2019MoneyHoldEndMs));
            double scale = ResolveModernWarfare2019ImpactScale(elapsedMs, 2.65);
            // Reserve 480 logical pixels for four-digit rewards, then leave a
            // fixed 70-pixel gap before the feed column.
            Vector2 position = new Vector2(
                (float)(1380 + _modernWarfare2019RightFeedOffset),
                340);
            Color shadow = Color.FromArgb(ToModernWarfare2019Byte(opacity * 118), 26, 18, 5);
            Color fill = Color.FromArgb(ToModernWarfare2019Byte(opacity * 255), 255, 201, 31);
            DrawModernWarfare2019MoneyGlow(
                drawingSession,
                text,
                position,
                format,
                elapsedMs);
            DrawModernWarfare2019ImpactText(
                drawingSession,
                text,
                position,
                scale,
                shadow,
                fill,
                format,
                new Vector2(1.5f, 1.8f),
                480);
        }

        private static void DrawModernWarfare2019MoneyGlow(
            CanvasDrawingSession drawingSession,
            string text,
            Vector2 position,
            CanvasTextFormat format,
            double elapsedMs)
        {
            CanvasBitmap glow = _modernWarfare2019MoneyGlowBitmap;
            if (glow == null
                || elapsedMs < ModernWarfare2019MoneyGlowStartMs
                || elapsedMs >= ModernWarfare2019MoneyGlowEndMs)
            {
                return;
            }

            double opacity = elapsedMs < ModernWarfare2019MoneyGlowPeakMs
                ? ModernWarfare2019SmoothStep(
                    (elapsedMs - ModernWarfare2019MoneyGlowStartMs)
                    / (ModernWarfare2019MoneyGlowPeakMs - ModernWarfare2019MoneyGlowStartMs))
                : 1.0 - ModernWarfare2019SmoothStep(
                    (elapsedMs - ModernWarfare2019MoneyGlowPeakMs)
                    / (ModernWarfare2019MoneyGlowEndMs - ModernWarfare2019MoneyGlowPeakMs));
            double expansion = Lerp(
                0.72,
                1.12,
                ModernWarfare2019EaseOutCubic(
                    (elapsedMs - ModernWarfare2019MoneyGlowStartMs)
                    / (ModernWarfare2019MoneyGlowEndMs - ModernWarfare2019MoneyGlowStartMs)));

            double textWidth;
            using (CanvasTextLayout layout = new CanvasTextLayout(
                CanvasDevice.GetSharedDevice(),
                text,
                format,
                1600,
                320))
            {
                textWidth = Math.Max(1.0, layout.LayoutBounds.Width);
            }

            double glowWidth = Math.Max(620, Math.Min(980, textWidth + 220)) * expansion;
            double glowHeight = glowWidth / 3.0;
            double centerX = position.X + (textWidth / 2.0);
            double centerY = position.Y + 82;
            Rect target = new Rect(
                centerX - (glowWidth / 2.0),
                centerY - (glowHeight / 2.0),
                glowWidth,
                glowHeight);
            Rect source = new Rect(0, 0, glow.SizeInPixels.Width, glow.SizeInPixels.Height);
            drawingSession.DrawImage(
                glow,
                target,
                source,
                (float)Clamp01(opacity * 0.92),
                CanvasImageInterpolation.Linear);
        }

        private void DrawModernWarfare2019Feed(
            CanvasDrawingSession drawingSession,
            CanvasTextFormat format)
        {
            long nowUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            int slot = 0;
            for (int index = _modernWarfare2019FeedItems.Count - 1;
                index >= 0 && slot < 5;
                index--)
            {
                ModernWarfare2019FeedItem item = _modernWarfare2019FeedItems[index];
                double elapsedMs = Math.Max(0, nowUnixMs - item.SpawnUnixMs);
                if (elapsedMs >= ModernWarfare2019FeedEndMs)
                {
                    continue;
                }

                double opacity = elapsedMs <= ModernWarfare2019FeedHoldEndMs
                    ? 1.0
                    : 1.0 - ModernWarfare2019SmoothStep(
                        (elapsedMs - ModernWarfare2019FeedHoldEndMs)
                        / (ModernWarfare2019FeedEndMs - ModernWarfare2019FeedHoldEndMs));
                opacity *= Clamp01(elapsedMs / 24.0);
                double scale = ResolveModernWarfare2019ImpactScale(elapsedMs, 2.85);
                Vector2 position = new Vector2(
                    (float)(1930 + _modernWarfare2019RightFeedOffset),
                    360 + (slot * 145));
                Color shadow = Color.FromArgb(ToModernWarfare2019Byte(opacity * 122), 24, 17, 5);
                Color fill = item.IsHeadshot
                    ? Color.FromArgb(ToModernWarfare2019Byte(opacity * 255), 255, 211, 42)
                    : item.IsAssist
                        ? Color.FromArgb(ToModernWarfare2019Byte(opacity * 255), 244, 224, 154)
                    : Color.FromArgb(ToModernWarfare2019Byte(opacity * 255), 243, 184, 25);
                DrawModernWarfare2019ImpactText(
                    drawingSession,
                    item.Text,
                    position,
                    scale,
                    shadow,
                    fill,
                    format,
                    new Vector2(1.4f, 1.7f),
                    double.PositiveInfinity);
                slot++;
            }
        }

        private static CanvasTextFormat CreateModernWarfare2019MoneyFormat()
        {
            return new CanvasTextFormat
            {
                FontFamily = "Bahnschrift SemiBold",
                FontSize = 150,
                FontWeight = FontWeights.Bold,
                WordWrapping = CanvasWordWrapping.NoWrap,
                HorizontalAlignment = CanvasHorizontalAlignment.Left,
                VerticalAlignment = CanvasVerticalAlignment.Top
            };
        }

        private static CanvasTextFormat CreateModernWarfare2019FeedFormat()
        {
            return new CanvasTextFormat
            {
                FontFamily = "Microsoft YaHei UI",
                FontSize = 140,
                FontWeight = FontWeights.Bold,
                WordWrapping = CanvasWordWrapping.NoWrap,
                HorizontalAlignment = CanvasHorizontalAlignment.Left,
                VerticalAlignment = CanvasVerticalAlignment.Top
            };
        }

        private static void DrawModernWarfare2019ImpactText(
            CanvasDrawingSession drawingSession,
            string text,
            Vector2 position,
            double scale,
            Color shadow,
            Color fill,
            CanvasTextFormat format,
            Vector2 shadowOffset,
            double maximumWidth)
        {
            double fitScale;
            using (CanvasTextLayout layout = new CanvasTextLayout(
                CanvasDevice.GetSharedDevice(),
                text,
                format,
                1600,
                320))
            {
                double textWidth = Math.Max(1.0, layout.LayoutBounds.Width);
                fitScale = Math.Min(1.0, maximumWidth / textWidth);
            }

            Matrix3x2 previous = drawingSession.Transform;
            // Preserve the glyph aspect ratio. If an unusually long value ever
            // exceeds the reserved column, scale it uniformly rather than
            // distorting only its horizontal axis.
            drawingSession.Transform = Matrix3x2.CreateScale(
                (float)(scale * fitScale),
                position) * previous;
            try
            {
                drawingSession.DrawText(text, position + shadowOffset, shadow, format);
                drawingSession.DrawText(text, position, fill, format);
            }
            finally
            {
                drawingSession.Transform = previous;
            }
        }

    }
}

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
        private void DrawModernWarfare2019LowerBanner(
            CanvasDrawingSession drawingSession,
            double elapsedMs)
        {
            double opacity = ResolveModernWarfare2019LowerBannerOpacity(elapsedMs);
            if (opacity <= 0)
            {
                return;
            }

            const double centerX = ModernWarfare2019FrameWidth / 2.0;
            const double centerY = ModernWarfare2019FrameHeight / 2.0;
            double entranceProgress;
            double cardScale;
            double bandScale;
            if (elapsedMs < 82)
            {
                entranceProgress = ModernWarfare2019EaseOutCubic(elapsedMs / 82.0);
                cardScale = Lerp(0.48, 1.24, entranceProgress);
                bandScale = cardScale;
            }
            else if (elapsedMs < 196)
            {
                entranceProgress = ModernWarfare2019EaseOutBack((elapsedMs - 82) / 114.0);
                cardScale = Lerp(1.24, 1.0, entranceProgress);
                bandScale = cardScale;
            }
            else
            {
                entranceProgress = 1.0;
                cardScale = 1.0;
                if (elapsedMs < 320)
                {
                    double contraction = ModernWarfare2019SmoothStep((elapsedMs - 196) / 124.0);
                    bandScale = Lerp(1.0, 0.18, contraction);
                }
                else if (elapsedMs < 500)
                {
                    double contraction = ModernWarfare2019SmoothStep((elapsedMs - 320) / 180.0);
                    bandScale = Lerp(0.18, 0.0, contraction);
                }
                else
                {
                    bandScale = 0.0;
                }
            }

            DrawModernWarfare2019LowerDotBand(
                drawingSession,
                centerX,
                centerY,
                bandScale,
                entranceProgress,
                elapsedMs,
                opacity);

            string text = "第"
                + _modernWarfare2019KillCount.ToString(CultureInfo.InvariantCulture)
                + "杀";
            using (CanvasTextFormat format = new CanvasTextFormat
            {
                FontFamily = "Microsoft YaHei UI",
                FontSize = 38,
                FontWeight = FontWeights.Normal,
                WordWrapping = CanvasWordWrapping.NoWrap,
                HorizontalAlignment = CanvasHorizontalAlignment.Center,
                VerticalAlignment = CanvasVerticalAlignment.Center
            })
            using (CanvasTextLayout textLayout = new CanvasTextLayout(
                CanvasDevice.GetSharedDevice(),
                text,
                format,
                1000,
                80))
            {
                double textWidth = Math.Ceiling(Math.Max(1, textLayout.LayoutBounds.Width));
                double textHeight = Math.Ceiling(Math.Max(1, textLayout.LayoutBounds.Height));
                double cardWidth = textWidth + 18;
                double cardHeight = textHeight + 10;
                Rect cardRect = new Rect(
                    centerX - (cardWidth / 2.0),
                    centerY - (cardHeight / 2.0),
                    cardWidth,
                    cardHeight);

                Color cardFill = Color.FromArgb(ToModernWarfare2019Byte(opacity * 172), 3, 4, 4);
                Color borderGlow = Color.FromArgb(ToModernWarfare2019Byte(opacity * 48), 255, 48, 45);
                Color border = Color.FromArgb(ToModernWarfare2019Byte(opacity * 238), 235, 61, 58);
                Color textShadow = Color.FromArgb(ToModernWarfare2019Byte(opacity * 92), 40, 3, 3);
                Color textFill = Color.FromArgb(ToModernWarfare2019Byte(opacity * 255), 255, 91, 88);

                Matrix3x2 previous = drawingSession.Transform;
                Vector2 center = new Vector2((float)centerX, (float)centerY);
                drawingSession.Transform = Matrix3x2.CreateScale((float)cardScale, center) * previous;
                try
                {
                    drawingSession.FillRectangle(cardRect, cardFill);
                    drawingSession.DrawRectangle(cardRect, borderGlow, 3.2f);
                    drawingSession.DrawRectangle(cardRect, border, 1.35f);

                    Rect shadowRect = new Rect(
                        cardRect.X + 0.9,
                        cardRect.Y + 1.0,
                        cardRect.Width,
                        cardRect.Height);
                    drawingSession.DrawText(text, shadowRect, textShadow, format);
                    drawingSession.DrawText(text, cardRect, textFill, format);
                }
                finally
                {
                    drawingSession.Transform = previous;
                }
            }
        }

        private static void DrawModernWarfare2019LowerDotBand(
            CanvasDrawingSession drawingSession,
            double centerX,
            double centerY,
            double scale,
            double entranceProgress,
            double elapsedMs,
            double opacity)
        {
            if (scale <= 0.001 || opacity <= 0.001)
            {
                return;
            }

            double halfWidth = 304 * scale;
            double halfHeight = 27 * scale;
            double waveStrength = (1.0 - Clamp01(entranceProgress)) * 6.0;
            const double spacing = 6.0;

            for (double x = -380; x <= 380; x += spacing)
            {
                if (Math.Abs(x) > halfWidth)
                {
                    continue;
                }

                double edgeFade = Math.Pow(
                    1.0 - (Math.Abs(x) / Math.Max(1.0, halfWidth)),
                    0.55);
                double wave = Math.Sin((x * 0.075) - (elapsedMs * 0.045)) * waveStrength;
                for (double y = -30; y <= 30; y += spacing)
                {
                    if (Math.Abs(y) > halfHeight)
                    {
                        continue;
                    }

                    double rowFade = 1.0 - (Math.Abs(y) / Math.Max(spacing, halfHeight + spacing));
                    double shimmer = 0.78 + (0.22 * Math.Sin((x * 0.11) + (y * 0.19)));
                    byte alpha = ToModernWarfare2019Byte(
                        opacity * edgeFade * rowFade * shimmer * 185.0);
                    Color dot = Color.FromArgb(alpha, 226, 55, 52);
                    drawingSession.FillCircle(
                        (float)(centerX + x),
                        (float)(centerY + y + wave),
                        1.45f,
                        dot);
                }
            }
        }

        private static double ResolveModernWarfare2019LowerBannerOpacity(double elapsedMs)
        {
            if (elapsedMs < ModernWarfare2019LowerBannerHoldEndMs)
            {
                return 1.0;
            }

            if (elapsedMs < 990)
            {
                return Lerp(1.0, 0.12, (elapsedMs - 930) / 60.0);
            }

            if (elapsedMs < 1050)
            {
                return Lerp(0.12, 1.0, (elapsedMs - 990) / 60.0);
            }

            if (elapsedMs < 1110)
            {
                return Lerp(1.0, 0.12, (elapsedMs - 1050) / 60.0);
            }

            if (elapsedMs < 1170)
            {
                return Lerp(0.12, 1.0, (elapsedMs - 1110) / 60.0);
            }

            return 1.0 - ModernWarfare2019SmoothStep(
                (elapsedMs - 1170) / (ModernWarfare2019LowerBannerEndMs - 1170));
        }

        private static double ResolveModernWarfare2019ImpactScale(double elapsedMs, double initialScale)
        {
            if (elapsedMs < 76)
            {
                return Lerp(
                    initialScale,
                    0.88,
                    ModernWarfare2019EaseOutCubic(elapsedMs / 76.0));
            }

            if (elapsedMs < 158)
            {
                return Lerp(
                    0.88,
                    1.0,
                    ModernWarfare2019EaseOutBack((elapsedMs - 76) / 82.0));
            }

            return 1.0;
        }

        private static double ModernWarfare2019EaseOutCubic(double value)
        {
            double progress = Clamp01(value);
            double inverse = 1.0 - progress;
            return 1.0 - (inverse * inverse * inverse);
        }

        private static double ModernWarfare2019EaseOutBack(double value)
        {
            const double back = 1.70158;
            double progress = Clamp01(value) - 1.0;
            return 1.0 + ((back + 1.0) * progress * progress * progress)
                + (back * progress * progress);
        }

        private static double ModernWarfare2019SmoothStep(double value)
        {
            double progress = Clamp01(value);
            return progress * progress * (3.0 - (2.0 * progress));
        }

        private static void DrawModernWarfare2019DiagonalArms(
            CanvasDrawingSession drawingSession,
            double centerX,
            double centerY,
            double innerRadius,
            double outerRadius,
            double strokeWidth,
            Color core,
            Color glow)
        {
            const double diagonal = 0.7071067811865476;
            int[] signs = { -1, 1 };
            foreach (int xSign in signs)
            {
                foreach (int ySign in signs)
                {
                    double dx = diagonal * xSign;
                    double dy = diagonal * ySign;
                    float x0 = (float)(centerX + dx * innerRadius);
                    float y0 = (float)(centerY + dy * innerRadius);
                    float x1 = (float)(centerX + dx * outerRadius);
                    float y1 = (float)(centerY + dy * outerRadius);

                    drawingSession.DrawLine(x0, y0, x1, y1, glow, (float)(strokeWidth + 4.0));
                    drawingSession.DrawLine(x0, y0, x1, y1, core, (float)strokeWidth);

                    float radius = (float)(strokeWidth / 2.0);
                    drawingSession.FillCircle(x0, y0, radius, core);
                    drawingSession.FillCircle(x1, y1, radius, core);
                }
            }
        }

        private static byte ToModernWarfare2019Byte(double value)
        {
            return (byte)Math.Max(0, Math.Min(255, Math.Round(value)));
        }

    }
}

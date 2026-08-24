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
        private void DrawModernWarfare2019UpperBanner(
            CanvasDrawingSession drawingSession,
            double elapsedMs)
        {
            const double centerX = ModernWarfare2019FrameWidth / 2.0;
            const double iconCenterY = (ModernWarfare2019FrameHeight / 2.0) - 72;
            const double textCenterY = (ModernWarfare2019FrameHeight / 2.0) + 52;
            double exitOpacity = elapsedMs <= ModernWarfare2019UpperFadeStartMs
                ? 1.0
                : 1.0 - ModernWarfare2019SmoothStep(
                    (elapsedMs - ModernWarfare2019UpperFadeStartMs)
                    / (ModernWarfare2019UpperEndMs - ModernWarfare2019UpperFadeStartMs));
            double entranceOpacity = ModernWarfare2019SmoothStep(elapsedMs / 70.0);
            double contentOpacity = entranceOpacity * exitOpacity;
            if (contentOpacity <= 0.001)
            {
                return;
            }

            double iconScale = ResolveModernWarfare2019UpperImpactScale(elapsedMs, 2.25, 0);
            double textScale = ResolveModernWarfare2019UpperImpactScale(elapsedMs, 3.15, 12);
            double textCurtainScale = ResolveModernWarfare2019UpperCurtainScale(elapsedMs);
            double textCurtainOpacity = ResolveModernWarfare2019UpperCurtainOpacity(elapsedMs)
                * exitOpacity;
            double iconCurtainScale = ResolveModernWarfare2019UpperIconCurtainScale(elapsedMs);
            double iconCurtainOpacity = ResolveModernWarfare2019UpperIconCurtainOpacity(elapsedMs)
                * exitOpacity;

            string text = GetModernWarfare2019UpperLabel(_modernWarfare2019KillCount);
            using (CanvasTextFormat textFormat = new CanvasTextFormat
            {
                FontFamily = "Microsoft YaHei UI",
                FontSize = 46,
                FontWeight = FontWeights.Normal,
                WordWrapping = CanvasWordWrapping.NoWrap,
                HorizontalAlignment = CanvasHorizontalAlignment.Center,
                VerticalAlignment = CanvasVerticalAlignment.Center
            })
            using (CanvasTextLayout textLayout = new CanvasTextLayout(
                CanvasDevice.GetSharedDevice(),
                text,
                textFormat,
                1000,
                86))
            {
                double textWidth = Math.Ceiling(Math.Max(1, textLayout.LayoutBounds.Width));
                DrawModernWarfare2019UpperTextBar(
                    drawingSession,
                    centerX,
                    textCenterY,
                    textWidth,
                    elapsedMs,
                    exitOpacity);

                if (iconCurtainScale > 0.001 && iconCurtainOpacity > 0.001)
                {
                    DrawModernWarfare2019UpperCurtain(
                        drawingSession,
                        centerX,
                        iconCenterY,
                        270,
                        136,
                        iconCurtainScale,
                        elapsedMs,
                        iconCurtainOpacity,
                        0.0);
                }

                if (textCurtainScale > 0.001 && textCurtainOpacity > 0.001)
                {
                    DrawModernWarfare2019UpperCurtain(
                        drawingSession,
                        centerX,
                        textCenterY,
                        650,
                        104,
                        textCurtainScale,
                        elapsedMs,
                        textCurtainOpacity,
                        1.7);
                }

                CanvasBitmap icon = _modernWarfare2019UpperIconBitmap;
                if (icon != null)
                {
                    double iconSize = 98 * iconScale;
                    Rect target = new Rect(
                        centerX - (iconSize / 2.0),
                        iconCenterY - (iconSize / 2.0),
                        iconSize,
                        iconSize);
                    Rect source = new Rect(0, 0, icon.SizeInPixels.Width, icon.SizeInPixels.Height);
                    drawingSession.DrawImage(
                        icon,
                        target,
                        source,
                        (float)Clamp01(contentOpacity),
                        CanvasImageInterpolation.Linear);
                }

                Rect textRect = new Rect(
                    centerX - ((textWidth + 36) / 2.0),
                    textCenterY - 37,
                    textWidth + 36,
                    74);
                Matrix3x2 previous = drawingSession.Transform;
                Vector2 textCenter = new Vector2((float)centerX, (float)textCenterY);
                drawingSession.Transform = Matrix3x2.CreateScale((float)textScale, textCenter) * previous;
                try
                {
                    Color shadow = Color.FromArgb(
                        ToModernWarfare2019Byte(contentOpacity * 118),
                        44,
                        31,
                        16);
                    Color fill = Color.FromArgb(
                        ToModernWarfare2019Byte(contentOpacity * 255),
                        246,
                        246,
                        241);
                    Rect shadowRect = new Rect(
                        textRect.X + 1.5,
                        textRect.Y + 1.8,
                        textRect.Width,
                        textRect.Height);
                    drawingSession.DrawText(text, shadowRect, shadow, textFormat);
                    drawingSession.DrawText(text, textRect, fill, textFormat);
                }
                finally
                {
                    drawingSession.Transform = previous;
                }
            }
        }

        private static void DrawModernWarfare2019UpperTextBar(
            CanvasDrawingSession drawingSession,
            double centerX,
            double centerY,
            double textWidth,
            double elapsedMs,
            double exitOpacity)
        {
            if (elapsedMs < 135)
            {
                return;
            }

            double compactWidth = textWidth + 34;
            double width;
            double opacity;
            if (elapsedMs < 215)
            {
                double progress = ModernWarfare2019EaseOutCubic((elapsedMs - 135) / 80.0);
                width = Lerp(compactWidth, 626, progress);
                opacity = Lerp(0.28, 0.82, progress);
            }
            else if (elapsedMs < 345)
            {
                width = 626;
                opacity = 0.82;
            }
            else if (elapsedMs < 495)
            {
                double progress = ModernWarfare2019SmoothStep((elapsedMs - 345) / 150.0);
                width = Lerp(626, compactWidth, progress);
                opacity = Lerp(0.82, 0.42, progress);
            }
            else
            {
                width = compactWidth;
                opacity = 0.42;
            }

            const double height = 54;
            Rect bar = new Rect(centerX - (width / 2.0), centerY - (height / 2.0), width, height);
            Color body = Color.FromArgb(
                ToModernWarfare2019Byte(exitOpacity * opacity * 255),
                243,
                184,
                25);
            Color highlight = Color.FromArgb(
                ToModernWarfare2019Byte(exitOpacity * opacity * 82),
                255,
                211,
                42);
            drawingSession.FillRectangle(bar, body);
            drawingSession.FillRectangle(
                new Rect(bar.X, bar.Y, bar.Width, 1.2),
                highlight);
        }

        private static void DrawModernWarfare2019UpperCurtain(
            CanvasDrawingSession drawingSession,
            double centerX,
            double centerY,
            double baseWidth,
            double baseHeight,
            double scale,
            double elapsedMs,
            double opacity,
            double phaseOffset)
        {
            double halfWidth = (baseWidth / 2.0) * scale;
            double halfHeight = (baseHeight / 2.0) * scale;
            const double spacing = 6.2;
            const double squareSize = 3.25;
            if (halfWidth <= spacing || halfHeight <= spacing)
            {
                return;
            }

            int blinkStep = (int)Math.Floor((elapsedMs / 78.0) + phaseOffset);
            for (double x = -baseWidth / 2.0; x <= baseWidth / 2.0; x += spacing)
            {
                if (Math.Abs(x) > halfWidth)
                {
                    continue;
                }

                double normalizedX = Math.Abs(x) / Math.Max(1.0, halfWidth);
                double horizontalFade = Math.Pow(1.0 - normalizedX, 0.42);

                for (double y = -baseHeight / 2.0; y <= baseHeight / 2.0; y += spacing)
                {
                    if (Math.Abs(y) > halfHeight)
                    {
                        continue;
                    }

                    double normalizedY = Math.Abs(y) / Math.Max(1.0, halfHeight);
                    double verticalFade = Math.Pow(1.0 - normalizedY, 0.30);
                    int rowIndex = (int)Math.Round(y / spacing);
                    double rowVisibility = ((Math.Abs(rowIndex) + blinkStep) % 5 == 0)
                        ? 0.12
                        : 1.0;
                    byte alpha = ToModernWarfare2019Byte(
                        opacity * horizontalFade * verticalFade * rowVisibility * 194.0);
                    drawingSession.FillRectangle(
                        new Rect(
                            centerX + x - (squareSize / 2.0),
                            centerY + y - (squareSize / 2.0),
                            squareSize,
                            squareSize),
                        Color.FromArgb(alpha, 245, 176, 65));
                }
            }
        }

        private static double ResolveModernWarfare2019UpperImpactScale(
            double elapsedMs,
            double initialScale,
            double delayMs)
        {
            double local = elapsedMs - delayMs;
            if (local <= 0)
            {
                return initialScale;
            }

            if (local < 62)
            {
                return Lerp(initialScale, 0.84, ModernWarfare2019EaseOutCubic(local / 62.0));
            }

            if (local < 122)
            {
                return Lerp(0.84, 1.0, ModernWarfare2019EaseOutBack((local - 62) / 60.0));
            }

            return 1.0;
        }

        private static double ResolveModernWarfare2019UpperCurtainScale(double elapsedMs)
        {
            if (elapsedMs < 155)
            {
                return 0.0;
            }

            if (elapsedMs < 235)
            {
                return ModernWarfare2019EaseOutCubic((elapsedMs - 155) / 80.0);
            }

            if (elapsedMs < 325)
            {
                return 1.0;
            }

            if (elapsedMs < 520)
            {
                return 1.0 - ModernWarfare2019SmoothStep((elapsedMs - 325) / 195.0);
            }

            return 0.0;
        }

        private static double ResolveModernWarfare2019UpperCurtainOpacity(double elapsedMs)
        {
            if (elapsedMs < 155)
            {
                return 0.0;
            }

            if (elapsedMs < 225)
            {
                return ModernWarfare2019SmoothStep((elapsedMs - 155) / 70.0);
            }

            if (elapsedMs < 335)
            {
                return 1.0;
            }

            if (elapsedMs < 520)
            {
                return 1.0 - ModernWarfare2019SmoothStep((elapsedMs - 335) / 185.0);
            }

            return 0.0;
        }

        private static double ResolveModernWarfare2019UpperIconCurtainScale(double elapsedMs)
        {
            if (elapsedMs < 155)
            {
                return 0.0;
            }

            if (elapsedMs < 235)
            {
                return ModernWarfare2019EaseOutCubic((elapsedMs - 155) / 80.0);
            }

            if (elapsedMs < 325)
            {
                return 1.0;
            }

            if (elapsedMs < 520)
            {
                return Lerp(
                    1.0,
                    0.52,
                    ModernWarfare2019SmoothStep((elapsedMs - 325) / 195.0));
            }

            return 0.52;
        }

        private static double ResolveModernWarfare2019UpperIconCurtainOpacity(double elapsedMs)
        {
            if (elapsedMs < 155)
            {
                return 0.0;
            }

            if (elapsedMs < 225)
            {
                return ModernWarfare2019SmoothStep((elapsedMs - 155) / 70.0);
            }

            if (elapsedMs < 335)
            {
                return 1.0;
            }

            if (elapsedMs < 520)
            {
                return Lerp(
                    1.0,
                    0.68,
                    ModernWarfare2019SmoothStep((elapsedMs - 335) / 185.0));
            }

            return 0.68;
        }

        private static string GetModernWarfare2019UpperLabel(int killCount)
        {
            return killCount <= 1
                ? "击杀"
                : GetModernWarfare2019StreakLabel(killCount);
        }

    }
}

using System;
using System.Collections.Generic;
using System.Numerics;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;

namespace KillConfirmGameBar.Controls
{
    public sealed partial class KillConfirmAnimation
    {
        private static void DrawCenteredScaledImage(
            CanvasDrawingSession drawingSession,
            CanvasBitmap image,
            double x,
            double y,
            double width,
            double height,
            double scale,
            double opacity)
        {
            if (image == null || opacity <= 0 || scale <= 0)
            {
                return;
            }

            double imageWidth = image.SizeInPixels.Width;
            double imageHeight = image.SizeInPixels.Height;
            if (imageWidth <= 0 || imageHeight <= 0)
            {
                return;
            }

            double fitScale = Math.Min(width / imageWidth, height / imageHeight);
            double scaledWidth = imageWidth * fitScale * scale;
            double scaledHeight = imageHeight * fitScale * scale;
            double anchoredX = (CodeKillFrameWidth / 2.0) + x;
            double anchoredY = (CodeKillFrameHeight / 2.0) + y;
            var target = new Rect(
                anchoredX + (width - scaledWidth) / 2.0,
                anchoredY + (height - scaledHeight) / 2.0,
                scaledWidth,
                scaledHeight);

            var source = new Rect(0, 0, image.SizeInPixels.Width, image.SizeInPixels.Height);
            drawingSession.DrawImage(image, target, source, (float)Math.Max(0.0, Math.Min(1.0, opacity)));
        }

        private static void ApplyCode2KillFramePatch(int frame, ref TransformSample main)
        {
            switch (frame)
            {
                case 0:
                    main.Y += 160;
                    main.Scale *= 0.94;
                    break;
                case 1:
                    main.Scale *= 0.78;
                    break;
                case 2:
                    main.X += 48;
                    main.Y += 83;
                    main.Scale *= 0.69;
                    break;
                case 3:
                    main.Y += 28;
                    main.Scale *= 0.55;
                    break;
                case 4:
                    main.Scale *= 0.55;
                    break;
                case 5:
                    main.X += 6;
                    main.Y += 38;
                    main.Scale *= 0.63;
                    break;
                case 6:
                    main.X += 25;
                    main.Y += 28;
                    main.Scale *= 0.69;
                    break;
                case 7:
                    main.X += 23;
                    main.Y += 33;
                    main.Scale *= 0.77;
                    break;
                case 8:
                    main.X -= 20;
                    main.Y += 20;
                    main.Scale *= 0.87;
                    break;
                case 9:
                    main.X += 6;
                    main.Y += 29;
                    main.Scale *= 0.93;
                    break;
                case 10:
                    main.X -= 6;
                    main.Y += 25;
                    break;
                case 11:
                    main.X -= 18;
                    main.Y += 20;
                    break;
            }
        }

        private static TransformSample SampleTrack(IReadOnlyList<TransformKey> keys, double progress)
        {
            if (progress <= keys[0].Progress)
            {
                return keys[0].ToSample();
            }

            for (int i = 1; i < keys.Count; i++)
            {
                TransformKey previous = keys[i - 1];
                TransformKey next = keys[i];
                if (progress <= next.Progress)
                {
                    double local = (progress - previous.Progress) / Math.Max(0.0001, next.Progress - previous.Progress);
                    return new TransformSample(
                        Lerp(previous.X, next.X, local),
                        Lerp(previous.Y, next.Y, local),
                        Lerp(previous.Scale, next.Scale, local),
                        Lerp(previous.Opacity, next.Opacity, local));
                }
            }

            return keys[keys.Count - 1].ToSample();
        }

        private static TransformSample SampleMainTrack(int frame, double progress)
        {
            if (_mainAnimationStyle == 2 && frame <= 4)
            {
                return SampleTrack(new[]
                {
                    new TransformKey(0.0000, -180, -180, 0.16, 0.00),
                    new TransformKey(0.0180, -180, -180, 0.34, 0.35),
                    new TransformKey(0.0360, -180, -180, 0.58, 0.68),
                    new TransformKey(0.0540, -180, -180, 0.82, 0.90),
                    new TransformKey(0.0720, -180, -180, 1.00, 1.00),
                    new TransformKey(1.0000, -180, -180, 1.00, 1.00)
                }, progress);
            }

            return SampleTrack(new[]
            {
                new TransformKey(0.0000, -180, 96, 4.80, 1.00),
                new TransformKey(0.0222, -180, -180, 2.75, 1.00),
                new TransformKey(0.0444, -164, -196, 2.28, 1.00),
                new TransformKey(0.0667, -159, -202, 2.02, 1.00),
                new TransformKey(0.0889, -167, -194, 1.80, 1.00),
                new TransformKey(0.1111, -162, -198, 1.62, 1.00),
                new TransformKey(0.1333, -170, -191, 1.46, 1.00),
                new TransformKey(0.1556, -166, -194, 1.32, 1.00),
                new TransformKey(0.1778, -172, -188, 1.22, 1.00),
                new TransformKey(0.2000, -169, -190, 1.15, 1.00),
                new TransformKey(0.2222, -174, -186, 1.10, 1.00),
                new TransformKey(0.2444, -172, -187, 1.06, 1.00),
                new TransformKey(0.2667, -176, -184, 1.03, 1.00),
                new TransformKey(0.2889, -175, -184, 1.01, 1.00),
                new TransformKey(0.3111, -179, -181, 1.00, 1.00),
                new TransformKey(0.3500, -180, -180, 1.00, 1.00),
                new TransformKey(0.7143, -180, -180, 1.00, 1.00),
                new TransformKey(0.8571, -180, -180, 1.00, 0.55),
                new TransformKey(1.0000, -180, -180, 1.00, 0.00)
            }, progress);
        }

        private static double Clamp01(double value)
        {
            return Math.Max(0.0, Math.Min(1.0, value));
        }

        private static double Lerp(double a, double b, double t)
        {
            return a + (b - a) * t;
        }

        private static void ApplyColorBoost(byte[] pixelData)
        {
            if (pixelData == null || pixelData.Length < 4)
            {
                return;
            }

            double brightnessBoost = _brightnessBoost;
            double contrastBoost = _contrastBoost;
            if (brightnessBoost <= 0 && contrastBoost <= 0)
            {
                return;
            }

            for (int index = 0; index <= pixelData.Length - 4; index += 4)
            {
                byte alpha = pixelData[index + 3];
                if (alpha == 0)
                {
                    continue;
                }

                pixelData[index] = AdjustChannel(pixelData[index], alpha, brightnessBoost, contrastBoost);
                pixelData[index + 1] = AdjustChannel(pixelData[index + 1], alpha, brightnessBoost, contrastBoost);
                pixelData[index + 2] = AdjustChannel(pixelData[index + 2], alpha, brightnessBoost, contrastBoost);
            }
        }

        private static byte AdjustChannel(byte premultipliedChannel, byte alpha, double brightnessBoost, double contrastBoost)
        {
            double normalizedAlpha = alpha / 255.0;
            if (normalizedAlpha <= 0)
            {
                return 0;
            }

            double unpremultiplied = Math.Min(1.0, premultipliedChannel / (255.0 * normalizedAlpha));
            if (brightnessBoost > 0)
            {
                double gamma = 1.0 - (brightnessBoost * 0.4);
                unpremultiplied = Math.Pow(unpremultiplied, gamma);
            }

            if (contrastBoost > 0)
            {
                double contrastFactor = 1.0 + (contrastBoost * 1.35);
                unpremultiplied = ((unpremultiplied - 0.5) * contrastFactor) + 0.5;
            }

            unpremultiplied = Math.Max(0.0, Math.Min(1.0, unpremultiplied));
            double repremultiplied = unpremultiplied * normalizedAlpha * 255.0;

            if (repremultiplied <= 0)
            {
                return 0;
            }

            if (repremultiplied >= alpha)
            {
                return alpha;
            }

            return (byte)Math.Round(repremultiplied);
        }
    }
}

using System;
using System.Numerics;
using Microsoft.Graphics.Canvas;
using Windows.Foundation;
using Windows.UI;

namespace KillConfirmCompatibility.Controls
{
    public sealed partial class KillConfirmAnimation
    {
        private void DrawCookedFlipbook(
            CanvasDrawingSession ds, CanvasBitmap atlas, int frameCount, double fps,
            double elapsedMs, double cx, double cy, double width, double height,
            bool mirrorX, bool additive, Color tint, double opacity)
        {
            if (atlas == null || elapsedMs < 0 || opacity <= 0)
            {
                return;
            }

            int index = (int)Math.Floor(elapsedMs * fps / 1000.0);
            if (index < 0 || index >= frameCount)
            {
                return;
            }

            double sourceHeight = atlas.SizeInPixels.Height / (double)frameCount;
            var source = new Rect(0, index * sourceHeight, atlas.SizeInPixels.Width, sourceHeight);
            double targetWidth = width * CookedAfterglowUmgScale;
            double targetHeight = height * CookedAfterglowUmgScale;
            var target = SnapValorantRectToPhysicalPixels(new Rect(
                cx - (targetWidth / 2.0), cy - (targetHeight / 2.0),
                targetWidth, targetHeight));

            Matrix3x2 previous = ds.Transform;
            if (mirrorX)
            {
                ds.Transform = Matrix3x2.CreateScale(-1, 1,
                    new Vector2((float)cx, (float)cy)) * previous;
            }

            DrawNativeTintedSource(ds, atlas, target, source, tint, opacity, additive);
            ds.Transform = previous;
        }

        private void DrawCookedRotatedFlipbook(
            CanvasDrawingSession ds, CanvasBitmap atlas, int frameCount, double fps,
            double elapsedMs, double cx, double cy, double width, double height,
            double degrees, Color tint, double opacity)
        {
            Matrix3x2 previous = ds.Transform;
            ds.Transform = Matrix3x2.CreateRotation(
                (float)(degrees * Math.PI / 180.0),
                new Vector2((float)cx, (float)cy)) * previous;
            DrawCookedFlipbook(ds, atlas, frameCount, fps, elapsedMs,
                cx, cy, width, height, false, false, tint, opacity);
            ds.Transform = previous;
        }

        private static double CookedWheelAngle(double spinElapsedMs, int kills)
        {
            if (kills <= 1 || spinElapsedMs < 0)
            {
                return 0.0;
            }

            double target = kills < 5 ? -360.0 / kills : 720.0;
            double speed = kills < 5 ? 8.0 : 5.0;
            int ticks = Math.Max(0, (int)Math.Floor(
                spinElapsedMs * FrameSequenceFps / 1000.0));
            double angle = 0.0;
            double alpha = Math.Min(1.0, speed / FrameSequenceFps);
            for (int i = 0; i < ticks; i++)
            {
                angle += (target - angle) * alpha;
                if (Math.Abs(target - angle) <= 0.1)
                {
                    return target;
                }
            }

            return angle;
        }

        private static double CookedHeadshotBadgeScale(double elapsedMs)
        {
            return CookedChannel(elapsedMs,
                new[] { 0.0, 50.0, 100.0, 150.0, 200.0, 250.0, 300.0, 350.0 },
                new[] { 1.155, 1.0, 1.106, 1.0, 1.076, 1.0, 1.049, 1.0 },
                new[] { 0.0, -0.000008, 0.0, -0.000005, 0.0, -0.000005, 0.0, 0.0 },
                new[] { 0.0, -0.000008, 0.0, -0.000005, 0.0, -0.000005, 0.0, 0.0 },
                new[] { 2, 2, 2, 2, 2, 2, 2, 2 });
        }

        private static Color CookedHeadshotColor(double elapsedMs, bool reticle)
        {
            if (elapsedMs < 0 || elapsedMs > 550.0)
            {
                return reticle ? Color.FromArgb(255, 255, 0, 0) : Colors.White;
            }

            double[] times = { 0.0, 50.0, 100.0, 150.0, 200.0, 250.0, 300.0, 350.0 };
            double[] values = reticle
                ? new[] { 1.0, 0.0, 1.0, 0.0, 1.0, 0.0, 1.0, 0.0 }
                : new[] { 0.0, 1.0, 0.0, 1.0, 0.0, 1.0, 0.0, 1.0 };
            double white = CookedChannel(elapsedMs, times, values,
                null, null, new[] { 2, 2, 2, 2, 2, 2, 2, 2 });
            return LerpValorantColor(Color.FromArgb(255, 255, 0, 0), Colors.White, white);
        }

        private static Color LerpValorantColor(Color from, Color to, double amount)
        {
            amount = Clamp01(amount);
            return Color.FromArgb(
                (byte)(from.A + ((to.A - from.A) * amount)),
                (byte)(from.R + ((to.R - from.R) * amount)),
                (byte)(from.G + ((to.G - from.G) * amount)),
                (byte)(from.B + ((to.B - from.B) * amount)));
        }

        private static double CookedProgress(double value, double start, double end)
        {
            return end <= start
                ? (value >= end ? 1.0 : 0.0)
                : Clamp01((value - start) / (end - start));
        }

        private static double CookedChildElapsedMs(
            double introTimelineMs, double eventTimelineMs, double playbackSpeed)
        {
            return (introTimelineMs - eventTimelineMs)
                / Math.Max(0.001, playbackSpeed);
        }

        // Evaluates FMovieSceneFloatChannel. InterpMode uses the native
        // ERichCurveInterpMode values: 0 linear, 1 constant, 2 cubic Hermite.
        private static double CookedChannel(
            double elapsedMs, double[] timesMs, double[] values,
            double[] arriveTangents, double[] leaveTangents, int[] interpModes)
        {
            if (timesMs == null || values == null || timesMs.Length == 0
                || timesMs.Length != values.Length)
            {
                return 0.0;
            }

            if (elapsedMs <= timesMs[0])
            {
                return values[0];
            }

            for (int i = 0; i < timesMs.Length - 1; i++)
            {
                if (elapsedMs > timesMs[i + 1])
                {
                    continue;
                }

                double spanMs = timesMs[i + 1] - timesMs[i];
                double t = spanMs <= 0 ? 1.0 : Clamp01((elapsedMs - timesMs[i]) / spanMs);
                int mode = interpModes != null && i < interpModes.Length ? interpModes[i] : 0;
                if (mode == 1)
                {
                    return values[i];
                }

                if (mode != 2)
                {
                    return Lerp(values[i], values[i + 1], t);
                }

                // Stored tangents are value per MovieScene tick (60,000 Hz).
                double tickSpan = spanMs * 60.0;
                double m0 = leaveTangents != null && i < leaveTangents.Length
                    ? leaveTangents[i] * tickSpan : 0.0;
                double m1 = arriveTangents != null && i + 1 < arriveTangents.Length
                    ? arriveTangents[i + 1] * tickSpan : 0.0;
                double t2 = t * t;
                double t3 = t2 * t;
                return ((2 * t3) - (3 * t2) + 1) * values[i]
                    + (t3 - (2 * t2) + t) * m0
                    + ((-2 * t3) + (3 * t2)) * values[i + 1]
                    + (t3 - t2) * m1;
            }

            return values[values.Length - 1];
        }    }
}

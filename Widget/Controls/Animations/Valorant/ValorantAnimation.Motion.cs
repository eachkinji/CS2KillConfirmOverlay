using System;

namespace KillConfirmGameBar.Controls
{
    public sealed partial class KillConfirmAnimation
    {
        private static int NextValorantSpinDirection()
        {
            lock (ValorantSpinRandomLock)
            {
                return ValorantSpinRandom.Next(0, 2) == 0 ? -1 : 1;
            }
        }

        private static double ResolveValorantBladeRotation(ValorantDemoProfile profile, int spinDirection, double elapsedMs)
        {
            double baseDegreesPerSecond = profile != null && profile.IsGaia ? 1100.0 : 1350.0;
            const double windowSeconds = 1.5;
            double t = Clamp01(elapsedMs / (windowSeconds * 1000.0));
            double eased = 1.0 - Math.Pow(1.0 - t, 4.0);
            double totalDegrees = baseDegreesPerSecond * windowSeconds * eased * 0.25;
            return -(totalDegrees * (spinDirection < 0 ? -1 : 1));
        }

        private static double GetValorantDemoLifeOpacity(int killCount, double elapsedMs)
        {
            if (killCount >= 5 && elapsedMs < ValorantDemoFivePlusShowMs)
            {
                return Clamp01(elapsedMs / ValorantDemoFivePlusShowMs);
            }

            if (elapsedMs <= ValorantDemoFadeStartMs)
            {
                return 1;
            }

            return 1.0 - Clamp01((elapsedMs - ValorantDemoFadeStartMs) / (ValorantDemoDurationMs - ValorantDemoFadeStartMs));
        }

        private static double GetValorantDemoEmblemYOffset(double elapsedMs)
        {
            return Lerp(-16.0, 0.0, CubicBezierEase(Clamp01(elapsedMs / 100.0), 0.22, 0.9, 0.28, 1));
        }

        private static double GetValorantDemoFlashOpacity(double elapsedMs)
        {
            const double FlashDurationMs = 620.0;
            if (elapsedMs <= 0 || elapsedMs >= FlashDurationMs)
            {
                return 0;
            }

            double percent = Clamp01(elapsedMs / FlashDurationMs) * 100.0;
            double[] keys =
            {
                0.0, 1.35, 12.11, 13.46, 14.81, 25.57, 26.92, 28.27,
                39.03, 40.38, 41.73, 52.5, 53.0, 100.0
            };
            double[] values =
            {
                0.0, 0.9, 0.9, 0.0, 0.9, 0.9, 0.0, 0.9,
                0.9, 0.0, 0.9, 0.9, 0.0, 0.0
            };

            for (int index = 1; index < keys.Length; index++)
            {
                if (percent <= keys[index])
                {
                    double span = Math.Max(0.0001, keys[index] - keys[index - 1]);
                    double local = (percent - keys[index - 1]) / span;
                    return Lerp(values[index - 1], values[index], local);
                }
            }

            return 0;
        }

        // Mirrors the reference's headshot overlay: lerp the headshot color
        // toward white using (1-t)^2 * sin(18Hz * t). The squared decay makes
        // the flicker intense at impact and fade out, while the 18Hz sine gives
        // a rapid strobe while it lasts.
        private const double ValorantDemoHeadshotFlickerDurationMs = 700.0;
        private const double ValorantDemoHeadshotFlickerFrequencyHz = 18.0;

        private static double GetValorantDemoHeadshotFlickerAmount(double elapsedMs)
        {
            if (elapsedMs <= 0 || elapsedMs >= ValorantDemoHeadshotFlickerDurationMs)
            {
                return 0;
            }

            double t = Clamp01(elapsedMs / ValorantDemoHeadshotFlickerDurationMs);
            double envelope = (1.0 - t) * (1.0 - t);
            double sine = Math.Sin(elapsedMs * ValorantDemoHeadshotFlickerFrequencyHz * 2.0 * Math.PI / 1000.0);
            return Clamp01(envelope * sine);
        }

        private static Windows.UI.Color LerpValorantColor(Windows.UI.Color a, Windows.UI.Color b, double t)
        {
            t = Clamp01(t);
            return Windows.UI.Color.FromArgb(
                (byte)(a.A + (b.A - a.A) * t),
                (byte)(a.R + (b.R - a.R) * t),
                (byte)(a.G + (b.G - a.G) * t),
                (byte)(a.B + (b.B - a.B) * t));
        }

        private static double GetValorantDemoBarDistance(double elapsedMs, double baseDistance)
        {
            const double Duration = 620.0;
            double percent = Clamp01(elapsedMs / Duration) * 100.0;
            double extra = 9.0 * ValorantDemoVfxScale;
            if (percent <= 3.22)
            {
                return baseDistance;
            }

            if (percent <= 29.03)
            {
                return Lerp(baseDistance, baseDistance + extra, CubicBezierEase((percent - 3.22) / (29.03 - 3.22), 0.22, 0.9, 0.28, 1));
            }

            if (percent <= 43.54)
            {
                return baseDistance + extra;
            }

            return Lerp(baseDistance + extra, baseDistance, CubicBezierEase((percent - 43.54) / (100.0 - 43.54), 0.22, 0.9, 0.28, 1));
        }

        private static double GetValorantDemoBarScale(double elapsedMs)
        {
            const double Duration = 620.0;
            double percent = Clamp01(elapsedMs / Duration) * 100.0;
            if (percent <= 3.22)
            {
                return 1.6;
            }

            if (percent <= 29.03)
            {
                return Lerp(1.6, 1.0, CubicBezierEase((percent - 3.22) / (29.03 - 3.22), 0.22, 0.9, 0.28, 1));
            }

            return 1.0;
        }

        private static bool IsBetween(double value, double min, double max)
        {
            return value >= min && value <= max;
        }

        private static double CubicBezierEase(double value, double x1, double y1, double x2, double y2)
        {
            value = Clamp01(value);
            double low = 0;
            double high = 1;
            double t = value;
            for (int i = 0; i < 8; i++)
            {
                t = (low + high) / 2.0;
                double x = CubicBezier(t, 0, x1, x2, 1);
                if (x < value)
                {
                    low = t;
                }
                else
                {
                    high = t;
                }
            }

            return CubicBezier(t, 0, y1, y2, 1);
        }

        private static double CubicBezier(double t, double p0, double p1, double p2, double p3)
        {
            double oneMinusT = 1.0 - t;
            return (oneMinusT * oneMinusT * oneMinusT * p0)
                + (3.0 * oneMinusT * oneMinusT * t * p1)
                + (3.0 * oneMinusT * t * t * p2)
                + (t * t * t * p3);
        }
    }
}

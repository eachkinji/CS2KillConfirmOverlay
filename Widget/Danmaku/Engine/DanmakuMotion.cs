using System;

namespace KillConfirmGameBar.Danmaku.Engine
{
    internal static class DanmakuMotion
    {
        public static double ResolveFlightDuration(
            DanmakuSpeedMode speed,
            double configuredMaximumSeconds,
            Random random)
        {
            double maximum = DanmakuReactionPolicies.ClampFlightSeconds(configuredMaximumSeconds);
            double duration;
            switch (speed)
            {
                case DanmakuSpeedMode.UltraSlow:
                    duration = 11.0 + (random.NextDouble() * 1.0);
                    break;
                case DanmakuSpeedMode.VerySlow:
                    duration = 7.4 + (random.NextDouble() * 0.8);
                    break;
                case DanmakuSpeedMode.Fast:
                    duration = 2.8 + (random.NextDouble() * 0.5);
                    break;
                case DanmakuSpeedMode.Slow:
                    duration = 4.6 + (random.NextDouble() * 0.4);
                    break;
                case DanmakuSpeedMode.Normal:
                default:
                    duration = 3.8 + (random.NextDouble() * 0.6);
                    break;
            }
            return Math.Min(maximum, duration);
        }

        public static float ResolveX(float startX, float endX, double elapsedSeconds, double durationSeconds)
        {
            if (durationSeconds <= 0)
            {
                return endX;
            }

            double progress = Math.Max(0.0, Math.Min(1.0, elapsedSeconds / durationSeconds));
            return (float)(startX + ((endX - startX) * progress));
        }
    }
}

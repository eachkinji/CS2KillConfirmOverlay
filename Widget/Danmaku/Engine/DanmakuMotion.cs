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
            double maximum = Math.Max(12.0, DanmakuReactionPolicies.ClampFlightSeconds(configuredMaximumSeconds));
            double duration;
            switch (speed)
            {
                case DanmakuSpeedMode.Leisurely: duration = 18.0; break;
                case DanmakuSpeedMode.Drifting: duration = 24.0; break;
                case DanmakuSpeedMode.Slowest: duration = 30.0; break;
                case DanmakuSpeedMode.UltraSlow:
                default: duration = 12.0; break;
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

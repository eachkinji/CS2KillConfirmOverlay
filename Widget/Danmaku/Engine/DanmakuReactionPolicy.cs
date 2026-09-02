using System;

namespace KillConfirmGameBar.Danmaku.Engine
{
    internal sealed class DanmakuReactionPolicy
    {
        public DanmakuReactionPolicy(int coreCount, int atmosphereCount, int priority)
        {
            CoreCount = coreCount;
            AtmosphereCount = atmosphereCount;
            Priority = priority;
        }

        public int CoreCount { get; }
        public int AtmosphereCount { get; }
        public int Priority { get; }
        public int TotalCount { get { return CoreCount + AtmosphereCount; } }
    }

    internal static class DanmakuReactionPolicies
    {
        public const int MinimumVisibleCount = 5;
        public const int MaximumVisibleCount = 7;
        public const double MaximumFlightSeconds = 15.0;

        public static DanmakuReactionPolicy Resolve(DanmakuEventKind kind)
        {
            switch (kind)
            {
                case DanmakuEventKind.Assist:
                    return new DanmakuReactionPolicy(2, 3, 35);
                case DanmakuEventKind.Death:
                    return new DanmakuReactionPolicy(3, 2, 60);
                case DanmakuEventKind.Kill:
                    return new DanmakuReactionPolicy(3, 2, 55);
                case DanmakuEventKind.FirstKill:
                    return new DanmakuReactionPolicy(3, 2, 65);
                case DanmakuEventKind.Headshot:
                    return new DanmakuReactionPolicy(4, 2, 75);
                case DanmakuEventKind.GrenadeKill:
                    return new DanmakuReactionPolicy(4, 2, 80);
                case DanmakuEventKind.KnifeKill:
                    return new DanmakuReactionPolicy(4, 2, 85);
                case DanmakuEventKind.MultiKill:
                    return new DanmakuReactionPolicy(4, 2, 90);
                case DanmakuEventKind.EpicStreak:
                    return new DanmakuReactionPolicy(5, 2, 100);
                case DanmakuEventKind.LastKill:
                    return new DanmakuReactionPolicy(5, 2, 100);
                case DanmakuEventKind.BombPlant:
                    return new DanmakuReactionPolicy(4, 2, 85);
                case DanmakuEventKind.BombDefuse:
                    return new DanmakuReactionPolicy(4, 2, 90);
                case DanmakuEventKind.RoundWin:
                    return new DanmakuReactionPolicy(3, 2, 70);
                case DanmakuEventKind.RoundLoss:
                    return new DanmakuReactionPolicy(3, 2, 70);
                case DanmakuEventKind.HostageInteract:
                    return new DanmakuReactionPolicy(3, 2, 75);
                case DanmakuEventKind.HostageRescue:
                    return new DanmakuReactionPolicy(4, 2, 85);
                case DanmakuEventKind.General:
                default:
                    return new DanmakuReactionPolicy(2, 3, 10);
            }
        }

        public static int ClampVisibleCount(int value)
        {
            return Math.Max(MinimumVisibleCount, Math.Min(MaximumVisibleCount, value));
        }

        public static double ClampFlightSeconds(double value)
        {
            return Math.Max(3.0, Math.Min(MaximumFlightSeconds, value));
        }
    }
}

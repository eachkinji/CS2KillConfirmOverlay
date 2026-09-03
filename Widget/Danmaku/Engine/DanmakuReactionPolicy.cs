using System;
using System.Collections.Generic;

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
        public const int EventMaximumVisibleCount = 12;
        // Event barrages may share a lane once the preceding message has moved far
        // enough ahead. Keep the lane count readable while allowing several
        // closely-spaced game events to become visible without waiting for a full
        // flight to finish.
        public const int EventMaximumActiveCount = 24;
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

    internal sealed class DanmakuEventDynamics
    {
        public DanmakuEventDynamics(int burstCount, double burstIntervalSeconds, double aftermathIntervalSeconds)
        {
            BurstCount = Math.Max(1, burstCount);
            BurstIntervalSeconds = Math.Max(0.15, burstIntervalSeconds);
            AftermathIntervalSeconds = Math.Max(BurstIntervalSeconds, aftermathIntervalSeconds);
        }

        public int BurstCount { get; }
        public double BurstIntervalSeconds { get; }
        public double AftermathIntervalSeconds { get; }
    }

    internal static class DanmakuEventSemantics
    {
        private static readonly string[] PositiveStances = { "cheer_praise", "hype_excitement" };
        private static readonly string[] PositiveForbidden =
        {
            "flame_streamer", "flame_player", "flame_team", "flame_audience",
            "flame_caster_host", "flame_external_figure", "cynical_sarcastic", "melancholy_lament"
        };
        private static readonly string[] NegativeStances =
        {
            "flame_streamer", "flame_player", "cynical_sarcastic"
        };
        private static readonly string[] LossStances =
        {
            "flame_streamer", "flame_player", "flame_team", "cynical_sarcastic", "melancholy_lament"
        };
        private static readonly string[] NegativeForbidden =
        {
            "cheer_praise", "hype_excitement", "comfort_support"
        };
        private static readonly string[] SupportStances =
        {
            "cheer_praise", "hype_excitement", "comfort_support"
        };
        private static readonly string[] TacticalStances =
        {
            "hype_excitement", "neutral_informative", "tease_playful"
        };
        private static readonly string[] PositiveFormats =
        {
            "repeated_symbols", "single_word_or_char", "slang_argot", "exaggeration_hyperbole", "plain_statement"
        };
        private static readonly string[] NegativeFormats =
        {
            "rhetorical_question", "repeated_symbols", "single_word_or_char", "slang_argot", "direct_address_at"
        };

        public static IReadOnlyCollection<string> RequiredStances(DanmakuEventKind kind)
        {
            if (kind == DanmakuEventKind.Death) return NegativeStances;
            if (kind == DanmakuEventKind.RoundLoss) return LossStances;
            if (kind == DanmakuEventKind.Assist) return SupportStances;
            if (kind == DanmakuEventKind.BombPlant || kind == DanmakuEventKind.HostageInteract) return TacticalStances;
            return PositiveStances;
        }

        public static IReadOnlyCollection<string> ForbiddenStances(DanmakuEventKind kind)
        {
            if (kind == DanmakuEventKind.Death || kind == DanmakuEventKind.RoundLoss) return NegativeForbidden;
            if (kind == DanmakuEventKind.BombPlant || kind == DanmakuEventKind.HostageInteract)
            {
                return new[] { "melancholy_lament" };
            }
            return PositiveForbidden;
        }

        public static IReadOnlyDictionary<string, double> PreferredFormats(DanmakuEventKind kind)
        {
            string[] formats = kind == DanmakuEventKind.Death || kind == DanmakuEventKind.RoundLoss
                ? NegativeFormats
                : PositiveFormats;
            var result = new Dictionary<string, double>(StringComparer.Ordinal);
            for (int i = 0; i < formats.Length; i++)
            {
                result[formats[i]] = i == 0 ? 2.5 : 1.6;
            }
            return result;
        }

        public static DanmakuEventDynamics ResolveDynamics(DanmakuEventKind kind)
        {
            switch (kind)
            {
                case DanmakuEventKind.EpicStreak: return new DanmakuEventDynamics(7, 0.20, 0.85);
                case DanmakuEventKind.KnifeKill:
                case DanmakuEventKind.MultiKill:
                case DanmakuEventKind.LastKill: return new DanmakuEventDynamics(5, 0.25, 0.95);
                case DanmakuEventKind.Death: return new DanmakuEventDynamics(5, 0.25, 0.90);
                case DanmakuEventKind.FirstKill:
                case DanmakuEventKind.Headshot:
                case DanmakuEventKind.RoundWin:
                case DanmakuEventKind.RoundLoss:
                case DanmakuEventKind.BombDefuse:
                case DanmakuEventKind.HostageRescue: return new DanmakuEventDynamics(4, 0.30, 1.05);
                case DanmakuEventKind.Kill: return new DanmakuEventDynamics(5, 0.22, 1.10);
                case DanmakuEventKind.GrenadeKill: return new DanmakuEventDynamics(4, 0.28, 1.10);
                case DanmakuEventKind.Assist: return new DanmakuEventDynamics(3, 0.40, 1.25);
                case DanmakuEventKind.BombPlant: return new DanmakuEventDynamics(3, 0.45, 1.30);
                case DanmakuEventKind.HostageInteract:
                default: return new DanmakuEventDynamics(2, 0.50, 1.40);
            }
        }
    }
}

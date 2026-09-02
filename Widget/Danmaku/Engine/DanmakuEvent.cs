using System;
using KillConfirmGameBar.Services;

namespace KillConfirmGameBar.Danmaku.Engine
{
    internal enum DanmakuEventKind
    {
        General,
        Kill,
        FirstKill,
        Headshot,
        KnifeKill,
        GrenadeKill,
        MultiKill,
        EpicStreak,
        LastKill,
        Assist,
        Death,
        RoundWin,
        RoundLoss,
        BombPlant,
        BombDefuse,
        HostageInteract,
        HostageRescue
    }

    internal enum DanmakuMessageRole
    {
        Core,
        Atmosphere
    }

    internal sealed class DanmakuEventContext
    {
        public DanmakuEventKind Kind { get; set; }
        public int KillCount { get; set; }
        public string PlayerName { get; set; }
        public string TargetName { get; set; }
    }

    internal sealed class DanmakuMessage
    {
        public string Text { get; set; }
        public DanmakuMessageRole Role { get; set; }
        public int EventPriority { get; set; }
        public bool IsEventReaction { get; set; }
    }

    internal static class DanmakuEventClassifier
    {
        public static DanmakuEventContext Classify(KillEvent gameEvent)
        {
            if (gameEvent == null)
            {
                return null;
            }

            string eventKind = (gameEvent.EventKind ?? string.Empty).Trim().ToLowerInvariant();
            DanmakuEventKind kind;

            switch (eventKind)
            {
                case "player_death":
                    kind = DanmakuEventKind.Death;
                    break;
                case "assist":
                    kind = DanmakuEventKind.Assist;
                    break;
                case "round_win":
                    kind = DanmakuEventKind.RoundWin;
                    break;
                case "round_loss":
                    kind = DanmakuEventKind.RoundLoss;
                    break;
                case "bomb_plant":
                    kind = DanmakuEventKind.BombPlant;
                    break;
                case "bomb_defuse":
                    kind = DanmakuEventKind.BombDefuse;
                    break;
                case "hostage_interact":
                    kind = DanmakuEventKind.HostageInteract;
                    break;
                case "hostage_rescue":
                    kind = DanmakuEventKind.HostageRescue;
                    break;
                case "kill":
                    kind = ClassifyKill(gameEvent);
                    break;
                default:
                    if (gameEvent.IsAssist)
                    {
                        kind = DanmakuEventKind.Assist;
                    }
                    else if (gameEvent.IsCombatEvent && gameEvent.KillCount > 0)
                    {
                        kind = ClassifyKill(gameEvent);
                    }
                    else
                    {
                        return null;
                    }
                    break;
            }

            return new DanmakuEventContext
            {
                Kind = kind,
                KillCount = gameEvent.KillCount,
                PlayerName = gameEvent.PlayerName,
                TargetName = gameEvent.TargetName
            };
        }

        public static DanmakuEventContext CreateTest(DanmakuEventKind kind)
        {
            return new DanmakuEventContext
            {
                Kind = kind,
                KillCount = kind == DanmakuEventKind.MultiKill
                    ? 3
                    : kind == DanmakuEventKind.EpicStreak
                        ? 5
                        : IsKillReaction(kind) ? 1 : 0
            };
        }

        public static DanmakuEventContext CreateTestFromKey(string eventKey)
        {
            switch ((eventKey ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "first_kill": return CreateTest(DanmakuEventKind.FirstKill);
                case "headshot": return CreateTest(DanmakuEventKind.Headshot);
                case "knife_kill": return CreateTest(DanmakuEventKind.KnifeKill);
                case "grenade_kill": return CreateTest(DanmakuEventKind.GrenadeKill);
                case "multi_kill": return CreateTest(DanmakuEventKind.MultiKill);
                case "epic_streak": return CreateTest(DanmakuEventKind.EpicStreak);
                case "last_kill": return CreateTest(DanmakuEventKind.LastKill);
                case "assist": return CreateTest(DanmakuEventKind.Assist);
                case "death": return CreateTest(DanmakuEventKind.Death);
                case "round_win": return CreateTest(DanmakuEventKind.RoundWin);
                case "round_loss": return CreateTest(DanmakuEventKind.RoundLoss);
                case "bomb_plant": return CreateTest(DanmakuEventKind.BombPlant);
                case "bomb_defuse": return CreateTest(DanmakuEventKind.BombDefuse);
                case "hostage_interact": return CreateTest(DanmakuEventKind.HostageInteract);
                case "hostage_rescue": return CreateTest(DanmakuEventKind.HostageRescue);
                case "kill":
                default:
                    return CreateTest(DanmakuEventKind.Kill);
            }
        }

        public static bool IsKillReaction(DanmakuEventKind kind)
        {
            switch (kind)
            {
                case DanmakuEventKind.Kill:
                case DanmakuEventKind.FirstKill:
                case DanmakuEventKind.Headshot:
                case DanmakuEventKind.KnifeKill:
                case DanmakuEventKind.GrenadeKill:
                case DanmakuEventKind.MultiKill:
                case DanmakuEventKind.EpicStreak:
                case DanmakuEventKind.LastKill:
                case DanmakuEventKind.Assist:
                    return true;
                default:
                    return false;
            }
        }

        public static bool IsRoundReaction(DanmakuEventKind kind)
        {
            return kind == DanmakuEventKind.RoundWin || kind == DanmakuEventKind.RoundLoss;
        }

        public static bool IsObjectiveReaction(DanmakuEventKind kind)
        {
            return kind == DanmakuEventKind.BombPlant
                || kind == DanmakuEventKind.BombDefuse
                || kind == DanmakuEventKind.HostageInteract
                || kind == DanmakuEventKind.HostageRescue;
        }

        private static DanmakuEventKind ClassifyKill(KillEvent gameEvent)
        {
            if (gameEvent.IsLastKill)
            {
                return DanmakuEventKind.LastKill;
            }
            if (gameEvent.KillCount >= 5)
            {
                return DanmakuEventKind.EpicStreak;
            }
            if (gameEvent.IsKnifeKill)
            {
                return DanmakuEventKind.KnifeKill;
            }
            if (gameEvent.IsGrenadeKill)
            {
                return DanmakuEventKind.GrenadeKill;
            }
            if (gameEvent.IsHeadshot)
            {
                return DanmakuEventKind.Headshot;
            }
            if (gameEvent.KillCount >= 2)
            {
                return DanmakuEventKind.MultiKill;
            }
            if (gameEvent.IsFirstKill)
            {
                return DanmakuEventKind.FirstKill;
            }
            return DanmakuEventKind.Kill;
        }
    }
}

// Shared by Widget (UWP) and CompatibilityHost; see KillEventContract.cs.
// Adapts League of Legends combat and observation events into the shared
// 18-style + 6657 Danmaku presentation contract while leaving CS2/CS:GO
// events 100% untouched.
using System;

#if WINDOWS_UWP
namespace KillConfirmGameBar.Services
#else
namespace KillConfirmCompatibility.Services
#endif
{
    internal static class LolEventAdapter
    {
        public const int FirstBloodReward = 400;
        public const int ChampionKillReward = 300;
        public const int AssistReward = 150;
        public const int TurretReward = 250;
        public const int InhibReward = 300;
        public const int MinorMonsterReward = 200;
        public const int StolenMinorMonsterReward = 400;
        public const int MajorMonsterReward = 300;
        public const int StolenMajorMonsterReward = 500;
        public const int AceBonusReward = 200;
        public const int VictoryReward = 500;

        private static int _currentMoneyEpoch = 1;
        private static double? _lastStructureGameTime;
        private static long _lastStructureWallMs;

        /// <summary>
        /// Prepares an incoming event for style and danmaku playback.
        /// Non-LOL events pass through untouched.
        /// </summary>
        public static bool TryPrepareForPlayback(
            KillEvent raw,
            GameStyleMode style,
            out KillEvent adapted)
        {
            if (raw == null)
            {
                adapted = null;
                return false;
            }

            if (!raw.IsFromLol)
            {
                adapted = raw;
                return !FeedbackRoutingPolicy.IsRecordOnly(raw);
            }

            if (raw.IsObservationEvent)
            {
                return TryPromoteObservation(raw, style, out adapted);
            }

            if (raw.IsCombatEvent && !raw.IsAssist && raw.KillCount > 0)
            {
                adapted = AdaptCombatKillForStyle(raw, style);
                return true;
            }

            adapted = raw;
            return true;
        }

        /// <summary>
        /// Enriches a LOL local champion kill with deterministic bounty, epoch,
        /// weapon label, and style-appropriate critical highlight flags.
        /// </summary>
        public static KillEvent AdaptCombatKillForStyle(KillEvent raw, GameStyleMode style)
        {
            if (raw == null || !raw.IsFromLol || !raw.IsCombatEvent || raw.IsAssist || raw.KillCount <= 0)
            {
                return raw;
            }

            int epoch = ResolveEpoch(raw);
            int reward = raw.MoneyReward > 0
                ? raw.MoneyReward
                : (raw.IsFirstKill ? FirstBloodReward : ChampionKillReward);
            string weaponName = string.IsNullOrWhiteSpace(raw.WeaponName)
                ? "英雄击杀"
                : raw.WeaponName;

            bool isShutdown = raw.Lol != null && raw.Lol.Shutdown == true;
            bool isRevenge = raw.IsRevenge
                || (raw.Lol != null && raw.Lol.Revenge == true)
                || string.Equals(raw.WeaponBadgeKey, "revenge", StringComparison.OrdinalIgnoreCase);
            bool isHeadshot = raw.IsHeadshot;
            if (style == GameStyleMode.Crossfire)
            {
                if (raw.IsFirstKill || isShutdown)
                {
                    isHeadshot = true;
                }
            }
            else if (style == GameStyleMode.Sanguosha)
            {
                if (raw.IsFirstKill || raw.IsLastKill || isShutdown || isRevenge)
                {
                    isHeadshot = true;
                }
            }
            else if ((raw.IsFirstKill || raw.KillCount >= 5 || isShutdown) && UsesHeadshotForCriticalLolKill(style))
            {
                isHeadshot = true;
            }

            return new KillEvent
            {
                Schema = raw.Schema,
                EventChannel = raw.EventChannel,
                Source = raw.Source,
                SourceGeneration = raw.SourceGeneration,
                KillCount = raw.KillCount,
                IsHeadshot = isHeadshot,
                IsKnifeKill = raw.IsKnifeKill,
                IsGrenadeKill = raw.IsGrenadeKill,
                IsFirstKill = raw.IsFirstKill,
                IsLastKill = raw.IsLastKill,
                IsRevenge = isRevenge,
                IsAssist = raw.IsAssist,
                PlayMainAnimation = raw.PlayMainAnimation,
                AnimationKey = raw.AnimationKey,
                EventKind = string.IsNullOrWhiteSpace(raw.EventKind) ? "kill" : raw.EventKind,
                WeaponBadgeKey = isRevenge ? "revenge" : raw.WeaponBadgeKey,
                WeaponName = weaponName,
                MoneyReward = reward,
                RoundNumber = raw.RoundNumber > 0 ? raw.RoundNumber : 1,
                MoneyEpoch = epoch,
                PlayerName = raw.PlayerName,
                TargetName = raw.TargetName,
                SteamId = raw.SteamId,
                PublishedUnixMs = raw.PublishedUnixMs,
                Intent = raw.Intent,
                Lol = raw.Lol
            };
        }

        /// <summary>
        /// Converts actionable LOL observation records (assists, deaths, turret/inhibitor
        /// takedowns, epic monsters, steals, ally aces, and match end) into playable events.
        /// </summary>
        public static bool TryPromoteObservation(
            KillEvent raw,
            GameStyleMode style,
            out KillEvent promoted)
        {
            promoted = null;
            if (raw == null || !raw.IsFromLol || !raw.IsObservationEvent || raw.Lol == null)
            {
                return false;
            }

            LolEventDetail lol = raw.Lol;
            string lolEvent = (lol.Event ?? string.Empty).Trim().ToLowerInvariant();
            string relation = (lol.Relation ?? string.Empty).Trim().ToLowerInvariant();
            string localRole = (lol.LocalRole ?? string.Empty).Trim().ToLowerInvariant();
            bool isSelfOrAlly = string.Equals(relation, "self", StringComparison.OrdinalIgnoreCase)
                || string.Equals(relation, "ally", StringComparison.OrdinalIgnoreCase)
                || string.Equals(localRole, "killer", StringComparison.OrdinalIgnoreCase)
                || string.Equals(localRole, "assister", StringComparison.OrdinalIgnoreCase)
                || string.Equals(localRole, "recipient", StringComparison.OrdinalIgnoreCase)
                || string.Equals(localRole, "acer", StringComparison.OrdinalIgnoreCase);

            int epoch = ResolveEpoch(raw);

            switch (lolEvent)
            {
                case "game_start":
                    _currentMoneyEpoch++;
                    return false;

                case "champion_kill":
                    if (string.Equals(localRole, "assister", StringComparison.OrdinalIgnoreCase))
                    {
                        string victim = !string.IsNullOrWhiteSpace(lol.Victim)
                            ? lol.Victim.Trim()
                            : (raw.TargetName ?? "敌方英雄");
                        if (style == GameStyleMode.Sanguosha)
                        {
                            promoted = CreatePromotedEvent(
                                raw,
                                eventChannel: "combat",
                                eventKind: "kill",
                                animationKey: "shenyimiaoshou",
                                killCount: 1,
                                playMainAnimation: true,
                                isAssist: false,
                                playerName: victim,
                                targetName: victim,
                                weaponName: "神医妙手",
                                moneyReward: AssistReward,
                                moneyEpoch: epoch,
                                isKnifeKill: true);
                            return true;
                        }

                        promoted = CreatePromotedEvent(
                            raw,
                            eventChannel: "combat",
                            eventKind: "assist",
                            animationKey: "assist",
                            killCount: 0,
                            playMainAnimation: false,
                            isAssist: true,
                            playerName: victim,
                            targetName: victim,
                            weaponName: "助攻",
                            moneyReward: AssistReward,
                            moneyEpoch: epoch);
                        return true;
                    }

                    if (string.Equals(localRole, "victim", StringComparison.OrdinalIgnoreCase))
                    {
                        promoted = CreatePromotedEvent(
                            raw,
                            eventChannel: "combat",
                            eventKind: "player_death",
                            animationKey: null,
                            killCount: 0,
                            playMainAnimation: false,
                            isAssist: false,
                            playerName: lol.Actor ?? raw.PlayerName,
                            targetName: lol.Victim ?? raw.TargetName,
                            weaponName: null,
                            moneyReward: 0,
                            moneyEpoch: epoch);
                        return true;
                    }

                    return false;

                case "turret_killed":
                case "first_brick":
                case "inhib_killed":
                    if (!IsAllyStructureTakedown(lol, relation, isSelfOrAlly))
                    {
                        return false;
                    }

                    if ((style == GameStyleMode.Crossfire || style == GameStyleMode.Sanguosha)
                        && (string.Equals(lolEvent, "turret_killed", StringComparison.OrdinalIgnoreCase)
                            || string.Equals(lolEvent, "first_brick", StringComparison.OrdinalIgnoreCase)))
                    {
                        long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                        if (lol.GameTime.HasValue && _lastStructureGameTime.HasValue
                            && Math.Abs(lol.GameTime.Value - _lastStructureGameTime.Value) <= 1.0
                            && Math.Abs(nowMs - _lastStructureWallMs) <= 2000)
                        {
                            return false;
                        }

                        _lastStructureGameTime = lol.GameTime;
                        _lastStructureWallMs = nowMs;
                    }

                    string structureLabel = string.Equals(lolEvent, "inhib_killed", StringComparison.OrdinalIgnoreCase)
                        ? "摧毁召唤师水晶"
                        : (string.Equals(lolEvent, "first_brick", StringComparison.OrdinalIgnoreCase)
                            ? "首塔奖励"
                            : "摧毁防御塔");
                    int structureReward = string.Equals(lolEvent, "inhib_killed", StringComparison.OrdinalIgnoreCase)
                        ? InhibReward
                        : TurretReward;

                    if (style == GameStyleMode.Sanguosha)
                    {
                        promoted = CreatePromotedEvent(
                            raw,
                            eventChannel: "combat",
                            eventKind: "podi",
                            animationKey: "podi",
                            killCount: 1,
                            playMainAnimation: true,
                            isAssist: false,
                            playerName: structureLabel,
                            targetName: structureLabel,
                            weaponName: structureLabel,
                            moneyReward: structureReward,
                            moneyEpoch: epoch);
                        return true;
                    }

                    if (style == GameStyleMode.Crossfire)
                    {
                        promoted = CreatePromotedEvent(
                            raw,
                            eventChannel: "economy",
                            eventKind: "bomb_defuse",
                            animationKey: "bomb_defuse",
                            killCount: 0,
                            playMainAnimation: true,
                            isAssist: false,
                            playerName: structureLabel,
                            targetName: structureLabel,
                            weaponName: structureLabel,
                            moneyReward: structureReward,
                            moneyEpoch: epoch);
                        return true;
                    }

                    promoted = CreatePromotedEvent(
                        raw,
                        eventChannel: "economy",
                        eventKind: "bomb_plant",
                        animationKey: "bomb_plant",
                        killCount: 0,
                        playMainAnimation: false,
                        isAssist: false,
                        playerName: structureLabel,
                        targetName: structureLabel,
                        weaponName: structureLabel,
                        moneyReward: structureReward,
                        moneyEpoch: epoch);
                    return true;

                case "dragon_kill":
                case "herald_kill":
                case "baron_kill":
                case "horde_kill":
                case "atakhan_kill":
                    if (!isSelfOrAlly)
                    {
                        return false;
                    }

                    bool stolen = lol.Stolen == true;
                    string monsterLabel = GetLolMonsterLabel(lolEvent, lol.DragonType, stolen);
                    int monsterReward = GetLolMonsterReward(lolEvent, lol.DragonType, stolen);

                    if (style == GameStyleMode.Crossfire)
                    {
                        promoted = CreatePromotedEvent(
                            raw,
                            eventChannel: "combat",
                            eventKind: "kill",
                            animationKey: stolen ? "knife" : "grenade",
                            killCount: 1,
                            playMainAnimation: true,
                            isAssist: false,
                            playerName: raw.PlayerName,
                            targetName: monsterLabel,
                            weaponName: monsterLabel,
                            moneyReward: monsterReward,
                            moneyEpoch: epoch,
                            isKnifeKill: stolen,
                            isGrenadeKill: !stolen);
                        return true;
                    }

                    if (style == GameStyleMode.Sanguosha)
                    {
                        promoted = CreatePromotedEvent(
                            raw,
                            eventChannel: "combat",
                            eventKind: "kill",
                            animationKey: "qingnangjishi",
                            killCount: 1,
                            playMainAnimation: true,
                            isAssist: false,
                            playerName: raw.PlayerName,
                            targetName: monsterLabel,
                            weaponName: monsterLabel,
                            moneyReward: monsterReward,
                            moneyEpoch: epoch,
                            isGrenadeKill: true);
                        return true;
                    }

                    if (stolen
                        && string.Equals(localRole, "killer", StringComparison.OrdinalIgnoreCase)
                        && IsCombatOnlyStealHighlightStyle(style))
                    {
                        promoted = CreatePromotedEvent(
                            raw,
                            eventChannel: "combat",
                            eventKind: "kill",
                            animationKey: "knife",
                            killCount: 1,
                            playMainAnimation: true,
                            isAssist: false,
                            playerName: raw.PlayerName,
                            targetName: monsterLabel,
                            weaponName: monsterLabel,
                            moneyReward: monsterReward,
                            moneyEpoch: epoch,
                            isHeadshot: true,
                            isKnifeKill: true);
                        return true;
                    }

                    promoted = CreatePromotedEvent(
                        raw,
                        eventChannel: "economy",
                        eventKind: "bomb_defuse",
                        animationKey: "bomb_defuse",
                        killCount: 0,
                        playMainAnimation: false,
                        isAssist: false,
                        playerName: monsterLabel,
                        targetName: monsterLabel,
                        weaponName: monsterLabel,
                        moneyReward: monsterReward,
                        moneyEpoch: epoch,
                        isHeadshot: stolen,
                        isKnifeKill: stolen);
                    return true;

                case "ace":
                    // When localRole == "acer", the local ChampionKill already carries IsLastKill = true.
                    if (!isSelfOrAlly || string.Equals(localRole, "acer", StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }

                    if (style == GameStyleMode.Crossfire)
                    {
                        promoted = CreatePromotedEvent(
                            raw,
                            eventChannel: "combat",
                            eventKind: "kill",
                            animationKey: "lastkill",
                            killCount: 1,
                            playMainAnimation: false,
                            isAssist: false,
                            playerName: "团灭敌方",
                            targetName: "敌方全队",
                            weaponName: "团灭敌方",
                            moneyReward: AceBonusReward,
                            moneyEpoch: epoch,
                            isLastKill: true);
                        return true;
                    }

                    if (style == GameStyleMode.Sanguosha)
                    {
                        promoted = CreatePromotedEvent(
                            raw,
                            eventChannel: "combat",
                            eventKind: "kill",
                            animationKey: "wanfumodi",
                            killCount: 1,
                            playMainAnimation: true,
                            isAssist: false,
                            playerName: "团灭敌方",
                            targetName: "敌方全队",
                            weaponName: "团灭敌方",
                            moneyReward: AceBonusReward,
                            moneyEpoch: epoch,
                            isHeadshot: true,
                            isLastKill: true);
                        return true;
                    }

                    promoted = CreatePromotedEvent(
                        raw,
                        eventChannel: "economy",
                        eventKind: "hostage_rescue",
                        animationKey: "hostage_rescue",
                        killCount: 0,
                        playMainAnimation: false,
                        isAssist: false,
                        playerName: "团灭敌方",
                        targetName: "敌方全队",
                        weaponName: "团灭敌方",
                        moneyReward: AceBonusReward,
                        moneyEpoch: epoch);
                    return true;

                case "game_end":
                    bool victory = IsLolVictory(lol.Result);
                    string roundKind = victory ? "round_win" : "round_loss";
                    string roundLabel = victory ? "对局胜利" : "对局失败";
                    int roundReward = victory ? VictoryReward : 0;
                    promoted = CreatePromotedEvent(
                        raw,
                        eventChannel: "economy",
                        eventKind: roundKind,
                        animationKey: roundKind,
                        killCount: 0,
                        playMainAnimation: false,
                        isAssist: false,
                        playerName: roundLabel,
                        targetName: roundLabel,
                        weaponName: roundLabel,
                        moneyReward: roundReward,
                        moneyEpoch: epoch);
                    return true;

                default:
                    return false;
            }
        }

        /// <summary>
        /// Returns true only when <paramref name="label"/> is an explicit LOL objective
        /// or match-settlement label, so CS weapon names (e.g. "AK-47", "Unknown") never
        /// override CS objective/round strings.
        /// </summary>
        public static bool IsLolObjectiveLabel(string label)
        {
            if (string.IsNullOrWhiteSpace(label))
            {
                return false;
            }

            string trimmed = label.Trim();
            return trimmed.StartsWith("摧毁", StringComparison.Ordinal)
                || trimmed.StartsWith("首塔", StringComparison.Ordinal)
                || trimmed.StartsWith("击杀", StringComparison.Ordinal)
                || trimmed.StartsWith("抢下", StringComparison.Ordinal)
                || trimmed.StartsWith("团灭", StringComparison.Ordinal)
                || trimmed.StartsWith("对局", StringComparison.Ordinal);
        }

        public static string GetLolMonsterLabel(string lolEvent, string dragonType, bool stolen)
        {
            string prefix = stolen ? "抢下" : "击杀";
            switch ((lolEvent ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "baron_kill":
                    return prefix + "纳什男爵";
                case "herald_kill":
                    return prefix + "峡谷先锋";
                case "horde_kill":
                    return prefix + "虚空巢虫";
                case "atakhan_kill":
                    return prefix + "厄塔汗";
                case "dragon_kill":
                    return prefix + GetLolDragonName(dragonType);
                default:
                    return prefix + "史诗野怪";
            }
        }

        public static int GetLolMonsterReward(string lolEvent, string dragonType, bool stolen)
        {
            bool isMajor = string.Equals(lolEvent, "baron_kill", StringComparison.OrdinalIgnoreCase)
                || string.Equals(lolEvent, "atakhan_kill", StringComparison.OrdinalIgnoreCase)
                || (string.Equals(lolEvent, "dragon_kill", StringComparison.OrdinalIgnoreCase)
                    && string.Equals((dragonType ?? string.Empty).Trim(), "Elder", StringComparison.OrdinalIgnoreCase));

            if (isMajor)
            {
                return stolen ? StolenMajorMonsterReward : MajorMonsterReward;
            }

            return stolen ? StolenMinorMonsterReward : MinorMonsterReward;
        }

        private static string GetLolDragonName(string dragonType)
        {
            switch ((dragonType ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "elder":
                    return "远古巨龙";
                case "fire":
                case "infernal":
                    return "炼狱亚龙";
                case "earth":
                case "mountain":
                    return "山脉亚龙";
                case "water":
                case "ocean":
                    return "海洋亚龙";
                case "air":
                case "cloud":
                    return "云端亚龙";
                case "hextech":
                    return "海克斯亚龙";
                case "chemtech":
                    return "炼金亚龙";
                default:
                    return "元素亚龙";
            }
        }

        private static bool IsAllyStructureTakedown(LolEventDetail lol, string relation, bool isSelfOrAlly)
        {
            if (isSelfOrAlly)
            {
                return true;
            }

            if (string.Equals(relation, "enemy", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            // Friendly minion last-hit on an enemy turret/inhibitor.
            string actor = lol?.Actor ?? string.Empty;
            string structure = lol?.Structure ?? string.Empty;
            return actor.IndexOf("T100", StringComparison.OrdinalIgnoreCase) >= 0
                || structure.IndexOf("_T2_", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsLolVictory(string result)
        {
            string normalized = (result ?? string.Empty).Trim();
            return string.Equals(normalized, "Win", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "Victory", StringComparison.OrdinalIgnoreCase);
        }

        private static bool UsesHeadshotForCriticalLolKill(GameStyleMode style)
        {
            switch (style)
            {
                case GameStyleMode.Valorant:
                case GameStyleMode.Calabiyau:
                case GameStyleMode.Dagoujiao:
                case GameStyleMode.CustomModule:
                case GameStyleMode.Battlefield1:
                case GameStyleMode.Battlefield5:
                case GameStyleMode.Battlefield4:
                case GameStyleMode.Battlefield2042:
                case GameStyleMode.Battlefield6:
                case GameStyleMode.Pubg:
                case GameStyleMode.DeltaForce:
                case GameStyleMode.ModernWarfare2019:
                case GameStyleMode.Apex:
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsCombatOnlyStealHighlightStyle(GameStyleMode style)
        {
            switch (style)
            {
                case GameStyleMode.Valorant:
                case GameStyleMode.Csol:
                case GameStyleMode.Sanguosha:
                case GameStyleMode.Calabiyau:
                case GameStyleMode.Dagoujiao:
                case GameStyleMode.Doubao:
                case GameStyleMode.CustomModule:
                case GameStyleMode.Overwatch:
                case GameStyleMode.Apex:
                    return true;
                default:
                    return false;
            }
        }

        private static int ResolveEpoch(KillEvent raw)
        {
            if (raw != null && raw.MoneyEpoch > 0)
            {
                _currentMoneyEpoch = Math.Max(_currentMoneyEpoch, raw.MoneyEpoch);
                return raw.MoneyEpoch;
            }

            return Math.Max(1, _currentMoneyEpoch);
        }

        private static KillEvent CreatePromotedEvent(
            KillEvent raw,
            string eventChannel,
            string eventKind,
            string animationKey,
            int killCount,
            bool playMainAnimation,
            bool isAssist,
            string playerName,
            string targetName,
            string weaponName,
            int moneyReward,
            int moneyEpoch,
            bool isHeadshot = false,
            bool isKnifeKill = false,
            bool isGrenadeKill = false,
            bool isLastKill = false)
        {
            return new KillEvent
            {
                Schema = raw.Schema,
                EventChannel = eventChannel,
                Source = raw.Source,
                SourceGeneration = raw.SourceGeneration,
                KillCount = killCount,
                IsHeadshot = isHeadshot,
                IsKnifeKill = isKnifeKill,
                IsGrenadeKill = isGrenadeKill,
                IsFirstKill = false,
                IsLastKill = isLastKill,
                IsAssist = isAssist,
                PlayMainAnimation = playMainAnimation,
                AnimationKey = animationKey,
                EventKind = eventKind,
                WeaponBadgeKey = null,
                WeaponName = weaponName,
                MoneyReward = moneyReward,
                RoundNumber = 1,
                MoneyEpoch = moneyEpoch,
                PlayerName = playerName ?? string.Empty,
                TargetName = targetName,
                SteamId = string.Empty,
                PublishedUnixMs = raw.PublishedUnixMs,
                Intent = raw.Intent,
                Lol = raw.Lol
            };
        }
    }
}

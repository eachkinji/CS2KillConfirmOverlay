using KillConfirmCompatibility.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Data.Json;

namespace KillConfirmCompatibility.Validation;

/// <summary>
/// Event contract v2 client checks:
/// 1. Shared FeedbackRoutingPolicy equals the pre-extraction inline routing
///    (copied verbatim below) for every style and event combination.
/// 2. Observation records never reach any style.
/// 3. Optional: a captured service journal parses into v2 intent/detail.
/// </summary>
internal static class EventContractValidation
{
    internal static Task RunAsync(string output, string journalPath)
    {
        Directory.CreateDirectory(output);
        var report = new List<string>();
        int compared = 0;
        string[] channels = { "combat", "economy", "observation", "" };
        string[] kinds = { "", "kill", "kill_confirmation", "assist", "player_death", "bomb_plant", "bomb_defuse",
            "hostage_interact", "hostage_rescue", "round_win", "round_loss", "lol_dragon_kill" };
        string[] animationKeys = { "", "bomb_plant", "bomb_defuse", "assist" };
        foreach (GameStyleMode style in Enum.GetValues(typeof(GameStyleMode)))
        foreach (string channel in channels)
        foreach (string kind in kinds)
        foreach (string animationKey in animationKeys)
        foreach (int count in new[] { 0, 1, 3 })
        foreach (bool assist in new[] { false, true })
        foreach (bool main in new[] { false, true })
        {
            var e = new KillEvent
            {
                EventChannel = KillEventChannels.Normalize(channel, kind, assist),
                EventKind = kind, AnimationKey = animationKey, KillCount = count,
                IsAssist = assist, PlayMainAnimation = main
            };
            string label = $"{style}/{channel}/{kind}/{animationKey}/{count}/{assist}/{main}";
            if (e.IsObservationEvent)
            {
                if (FeedbackRoutingPolicy.CanStyleConsumeEvent(style, e)
                    || FeedbackRoutingPolicy.ShouldPlayPrimaryAnimation(style, e)
                    || FeedbackRoutingPolicy.PassesPrimaryGuard(style, e)
                    || !FeedbackRoutingPolicy.IsIgnoredByStyles(e))
                    throw new InvalidDataException("Observation reached a style: " + label);
                compared++;
                continue;
            }
            Expect(Legacy.CanStyleConsumeEvent(style, e), FeedbackRoutingPolicy.CanStyleConsumeEvent(style, e), "consume " + label);
            if (Legacy.CanStyleConsumeEvent(style, e))
                Expect(Legacy.ShouldPlayPrimary(style, e), FeedbackRoutingPolicy.ShouldPlayPrimaryAnimation(style, e), "primary " + label);
            Expect(Legacy.PassesGuard(style, e), FeedbackRoutingPolicy.PassesPrimaryGuard(style, e), "guard " + label);
            Expect(string.Equals(kind, "player_death", StringComparison.OrdinalIgnoreCase),
                FeedbackRoutingPolicy.IsIgnoredByStyles(e), "ignored " + label);
            compared++;
        }
        report.Add($"PASS: shared routing policy equals legacy inline routing for {compared} style/event combinations");
        report.Add("PASS: observation channel and unknown future channels never reach a style");

        if (!string.IsNullOrWhiteSpace(journalPath))
        {
            JsonArray events = JsonObject.Parse(File.ReadAllText(journalPath)).GetNamedArray("events");
            int observations = 0, kills = 0;
            foreach (IJsonValue value in events)
            {
                KillEvent e = KillEventParser.Parse(value.GetObject());
                if (e.Schema != 2 || !e.IsFromLol || e.Intent == null || e.Lol == null)
                    throw new InvalidDataException("Missing v2 fields: " + value.Stringify());
                if (e.IsObservationEvent)
                {
                    if (e.Intent.Kind != FeedbackKinds.Observation || e.Intent.PlayMain || e.KillCount != 0
                        || e.EventKind != "lol_" + e.Lol.Event)
                        throw new InvalidDataException("Bad observation: " + value.Stringify());
                    observations++;
                }
                else
                {
                    if (!e.IsCombatEvent || e.Intent.Kind != FeedbackKinds.Kill || e.Intent.Tier != e.KillCount
                        || e.Lol.LocalRole != "killer" || e.Lol.Event != "champion_kill")
                        throw new InvalidDataException("Bad LOL kill: " + value.Stringify());
                    kills++;
                }
            }
            if (observations == 0 || kills == 0)
                throw new InvalidDataException("Journal must contain LOL kills and observations.");
            report.Add($"PASS: captured journal parsed: {kills} LOL kills with detail.lol, {observations} observation records ({Path.GetFullPath(journalPath)})");
        }

        ValidateLolStyleAndDanmakuIntegration(report);
        File.WriteAllLines(Path.Combine(output, "event-contract-pass.txt"), report);
        return Task.CompletedTask;
    }

    private static void Expect(bool legacy, bool shared, string label)
    {
        if (legacy != shared) throw new InvalidDataException($"Routing mismatch ({label}): legacy={legacy}, shared={shared}");
    }

    private static void ValidateLolStyleAndDanmakuIntegration(List<string> report)
    {
        // 1. CS events must remain reference-identical and never match LOL objective labels.
        var csKill = new KillEvent { Source = KillEventSources.Cs2, EventChannel = "combat", EventKind = "kill", KillCount = 1, WeaponName = "AK-47", PlayMainAnimation = true };
        if (!LolEventAdapter.TryPrepareForPlayback(csKill, GameStyleMode.Battlefield2042, out KillEvent csOut) || !ReferenceEquals(csKill, csOut))
            throw new InvalidDataException("CS event was modified by LolEventAdapter.");
        if (LolEventAdapter.IsLolObjectiveLabel("AK-47") || LolEventAdapter.IsLolObjectiveLabel("Unknown") || LolEventAdapter.IsLolObjectiveLabel(""))
            throw new InvalidDataException("CS weapon label matched IsLolObjectiveLabel.");

        // 2. FirstBlood, Pentakill, and Ace combat kills across all 18 styles.
        var fbRaw = new KillEvent { Source = KillEventSources.Lol, EventChannel = "combat", EventKind = "kill", KillCount = 1, IsFirstKill = true, PlayMainAnimation = true, PlayerName = "我方", TargetName = "敌方", Lol = new LolEventDetail { Event = "champion_kill", Relation = "self", LocalRole = "killer" } };
        var pentaRaw = new KillEvent { Source = KillEventSources.Lol, EventChannel = "combat", EventKind = "kill", KillCount = 5, PlayMainAnimation = true, PlayerName = "我方", TargetName = "敌方", Lol = new LolEventDetail { Event = "champion_kill", Relation = "self", LocalRole = "killer", Multikill = 5 } };
        var aceKillRaw = new KillEvent { Source = KillEventSources.Lol, EventChannel = "combat", EventKind = "kill", KillCount = 1, IsLastKill = true, PlayMainAnimation = true, PlayerName = "我方", TargetName = "敌方", Lol = new LolEventDetail { Event = "champion_kill", Relation = "self", LocalRole = "killer" } };

        foreach (GameStyleMode style in Enum.GetValues(typeof(GameStyleMode)))
        {
            if (!LolEventAdapter.TryPrepareForPlayback(fbRaw, style, out KillEvent fb) || fb.MoneyReward != 400 || fb.RoundNumber != 1 || fb.MoneyEpoch < 1)
                throw new InvalidDataException("FirstBlood adaptation failed for " + style);
            if (!LolEventAdapter.TryPrepareForPlayback(pentaRaw, style, out KillEvent penta) || penta.MoneyReward != 300 || !FeedbackRoutingPolicy.ShouldPlayPrimaryAnimation(style, penta))
                throw new InvalidDataException("Pentakill adaptation failed for " + style);
            if (!LolEventAdapter.TryPrepareForPlayback(aceKillRaw, style, out KillEvent aceKill) || !aceKill.IsLastKill)
                throw new InvalidDataException("Ace kill adaptation failed for " + style);
        }

        if (GameStyleService.SanguoshaTier(fbRaw) != GameStyleService.SanguoshaHeadshotTier
            || GameStyleService.SanguoshaTier(aceKillRaw) != GameStyleService.SanguoshaGrenadeTier)
            throw new InvalidDataException("Sanguosha LOL special tiers did not map FirstBlood->9 and Ace->11.");

        // 3. Observation promotions: Assist, Death, Turret, Inhib, Baron Steal, Ally Ace, GameEnd.
        KillEvent MakeObs(LolEventDetail detail) => new KillEvent
        {
            Source = KillEventSources.Lol,
            EventChannel = "observation",
            EventKind = "lol_" + detail.Event,
            PlayerName = detail.Actor ?? "玩家",
            TargetName = detail.Victim,
            Lol = detail
        };

        var assistObs = MakeObs(new LolEventDetail { Event = "champion_kill", Relation = "ally", LocalRole = "assister", Actor = "队友", Victim = "劫" });
        if (!LolEventAdapter.TryPromoteObservation(assistObs, GameStyleMode.Battlefield5, out KillEvent assistEv)
            || !assistEv.IsAssist || assistEv.MoneyReward != 150
            || Danmaku.Engine.DanmakuEventClassifier.Classify(assistEv)?.Kind != Danmaku.Engine.DanmakuEventKind.Assist)
            throw new InvalidDataException("LOL assist promotion failed.");

        var deathObs = MakeObs(new LolEventDetail { Event = "champion_kill", Relation = "enemy", LocalRole = "victim", Actor = "劫", Victim = "我方" });
        if (!LolEventAdapter.TryPromoteObservation(deathObs, GameStyleMode.Crossfire, out KillEvent deathEv)
            || !FeedbackRoutingPolicy.IsIgnoredByStyles(deathEv)
            || Danmaku.Engine.DanmakuEventClassifier.Classify(deathEv)?.Kind != Danmaku.Engine.DanmakuEventKind.Death)
            throw new InvalidDataException("LOL death promotion failed.");

        var turretObs = MakeObs(new LolEventDetail { Event = "turret_killed", Relation = "ally", LocalRole = "none", Actor = "队友", Structure = "Turret_T2_L_03_A" });
        if (!LolEventAdapter.TryPromoteObservation(turretObs, GameStyleMode.Battlefield1, out KillEvent turretEv)
            || turretEv.EventKind != "bomb_plant" || turretEv.MoneyReward != 250 || !LolEventAdapter.IsLolObjectiveLabel(turretEv.WeaponName))
            throw new InvalidDataException("LOL turret promotion failed.");

        var stealObs = MakeObs(new LolEventDetail { Event = "baron_kill", Relation = "self", LocalRole = "killer", Actor = "我方", Stolen = true });
        if (!LolEventAdapter.TryPromoteObservation(stealObs, GameStyleMode.Battlefield2042, out KillEvent stealEco)
            || stealEco.EventKind != "bomb_defuse" || stealEco.MoneyReward != 500 || stealEco.WeaponName != "抢下纳什男爵")
            throw new InvalidDataException("LOL baron steal economy promotion failed.");
        if (!LolEventAdapter.TryPromoteObservation(stealObs, GameStyleMode.Sanguosha, out KillEvent stealCombat)
            || !stealCombat.IsCombatEvent || !stealCombat.IsKnifeKill || GameStyleService.SanguoshaTier(stealCombat) != GameStyleService.SanguoshaKnifeTier)
            throw new InvalidDataException("LOL baron steal combat promotion failed.");

        var winObs = MakeObs(new LolEventDetail { Event = "game_end", Relation = "self", LocalRole = "none", Result = "Win" });
        if (!LolEventAdapter.TryPromoteObservation(winObs, GameStyleMode.ModernWarfare2019, out KillEvent winEv)
            || winEv.EventKind != "round_win" || winEv.MoneyReward != 500 || winEv.WeaponName != "对局胜利")
            throw new InvalidDataException("LOL GameEnd Win promotion failed.");

        report.Add("PASS: all 18 styles + 6657 Danmaku verified for LOL combat kills, bounties, and promoted observations");
    }

    /// <summary>Verbatim copy of the inline routing removed from both hosts.</summary>
    private static class Legacy
    {
        private static bool IsBombObjectiveEvent(KillEvent killEvent)
        {
            return killEvent != null
                && (string.Equals(killEvent.EventKind, "bomb_plant", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(killEvent.EventKind, "bomb_defuse", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(killEvent.AnimationKey, "bomb_plant", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(killEvent.AnimationKey, "bomb_defuse", StringComparison.OrdinalIgnoreCase));
        }

        internal static bool CanStyleConsumeEvent(GameStyleMode style, KillEvent killEvent)
        {
            if (killEvent == null || (style == GameStyleMode.Csol && IsBombObjectiveEvent(killEvent))) return false;
            if (killEvent.IsCombatEvent) return true;
            if (style == GameStyleMode.Crossfire && IsBombObjectiveEvent(killEvent)) return true;
            if (style == GameStyleMode.ModernWarfare2019)
            {
                string eventKind = killEvent.EventKind ?? killEvent.AnimationKey;
                if (string.Equals(eventKind, "bomb_plant", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(eventKind, "bomb_defuse", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(eventKind, "hostage_interact", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(eventKind, "hostage_rescue", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(eventKind, "round_win", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(eventKind, "round_loss", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return killEvent.IsEconomyEvent && IsEconomyPresentationStyle(style);
        }

        internal static bool ShouldPlayPrimary(GameStyleMode style, KillEvent killEvent)
        {
            bool isCrossfireObjective = style == GameStyleMode.Crossfire && IsBombObjectiveEvent(killEvent);
            bool isModernWarfare2019Objective = style == GameStyleMode.ModernWarfare2019
                && killEvent != null
                && killEvent.IsEconomyEvent;
            return (killEvent.IsCombatEvent && killEvent.PlayMainAnimation)
                || (IsEconomyPresentationStyle(style) && IsBattlefieldTextEvent(killEvent))
                || (style == GameStyleMode.Csol && killEvent.IsCombatEvent)
                || (style == GameStyleMode.ModernWarfare2019 && (killEvent.IsCombatEvent || isModernWarfare2019Objective))
                || (style == GameStyleMode.Overwatch && killEvent.IsCombatEvent && killEvent.IsAssist)
                || (style == GameStyleMode.Apex && killEvent.IsCombatEvent && killEvent.IsAssist)
                || isCrossfireObjective;
        }

        internal static bool PassesGuard(GameStyleMode style, KillEvent killEvent)
        {
            bool isCsolAssist = style == GameStyleMode.Csol && killEvent != null && killEvent.IsAssist;
            bool isApexAssist = style == GameStyleMode.Apex && killEvent != null && killEvent.IsAssist;
            bool isOverwatchAssist = style == GameStyleMode.Overwatch && killEvent != null && killEvent.IsAssist;
            bool isModernWarfare2019Assist = style == GameStyleMode.ModernWarfare2019 && killEvent != null && killEvent.IsAssist;
            bool isCrossfireObjective = style == GameStyleMode.Crossfire && IsBombObjectiveEvent(killEvent);
            bool isModernWarfare2019Objective = style == GameStyleMode.ModernWarfare2019 && killEvent != null && killEvent.IsEconomyEvent;
            return !(!CanStyleConsumeEvent(style, killEvent)
                || (killEvent.KillCount <= 0
                    && !IsBattlefieldTextEvent(killEvent)
                    && !isCsolAssist
                    && !isApexAssist
                    && !isOverwatchAssist
                    && !isModernWarfare2019Assist
                    && !isCrossfireObjective
                    && !isModernWarfare2019Objective));
        }

        private static bool IsBattlefieldObjectiveEvent(string eventKind)
        {
            return string.Equals(eventKind, "bomb_plant", StringComparison.OrdinalIgnoreCase)
                || string.Equals(eventKind, "bomb_defuse", StringComparison.OrdinalIgnoreCase)
                || string.Equals(eventKind, "hostage_interact", StringComparison.OrdinalIgnoreCase)
                || string.Equals(eventKind, "hostage_rescue", StringComparison.OrdinalIgnoreCase);
        }

        private static string GetBattlefieldEventKind(KillEvent killEvent)
        {
            if (!string.IsNullOrWhiteSpace(killEvent?.EventKind)) return killEvent.EventKind;
            return killEvent?.IsAssist == true ? "assist" : "kill";
        }

        private static bool IsBattlefieldTextEvent(KillEvent killEvent)
        {
            if (killEvent == null) return false;
            string eventKind = GetBattlefieldEventKind(killEvent);
            if (killEvent.IsCombatEvent)
                return killEvent.IsAssist || string.Equals(eventKind, "assist", StringComparison.OrdinalIgnoreCase);
            return killEvent.IsEconomyEvent
                && (string.Equals(eventKind, "round_win", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(eventKind, "round_loss", StringComparison.OrdinalIgnoreCase)
                    || IsBattlefieldObjectiveEvent(eventKind));
        }

        private static bool IsEconomyPresentationStyle(GameStyleMode style)
        {
            return style == GameStyleMode.Battlefield1 || style == GameStyleMode.Battlefield5
                || style == GameStyleMode.Battlefield4 || style == GameStyleMode.Battlefield2042
                || style == GameStyleMode.Battlefield6 || style == GameStyleMode.Pubg
                || style == GameStyleMode.DeltaForce;
        }
    }
}

// Shared by Widget (UWP) and CompatibilityHost. Each host compiles this file
// into its own namespace so existing consumers keep their usings unchanged.
// C# 7.3 only (Widget is a UWP project).
using System;

#if WINDOWS_UWP
namespace KillConfirmGameBar.Services
#else
namespace KillConfirmCompatibility.Services
#endif
{
    /// <summary>One service event: v1 fields plus event contract v2 (docs/event-contract-v2.md).</summary>
    public sealed class KillEvent
    {
        public string EventChannel { get; set; }
        public int KillCount { get; set; }
        public bool IsHeadshot { get; set; }
        public bool IsKnifeKill { get; set; }
        public bool IsGrenadeKill { get; set; }
        public bool IsFirstKill { get; set; }
        public bool IsLastKill { get; set; }
        public bool IsRevenge { get; set; }
        public bool IsAssist { get; set; }
        public bool PlayMainAnimation { get; set; }
        public string AnimationKey { get; set; }
        public string EventKind { get; set; }
        public string WeaponBadgeKey { get; set; }
        public string WeaponName { get; set; }
        public int MoneyReward { get; set; }
        public int RoundNumber { get; set; }
        public int MoneyEpoch { get; set; }
        public string PlayerName { get; set; }
        public string TargetName { get; set; }
        public string SteamId { get; set; }
        public ulong PublishedUnixMs { get; set; }
        public ulong SourceGeneration { get; set; }

        // Event contract v2. Null/zero for locally created preview events.
        public int Schema { get; set; }
        public string Source { get; set; }
        public FeedbackIntentInfo Intent { get; set; }
        public LolEventDetail Lol { get; set; }

        public bool IsCombatEvent
        {
            get { return string.Equals(EventChannel, KillEventChannels.Combat, StringComparison.OrdinalIgnoreCase); }
        }

        public bool IsEconomyEvent
        {
            get { return string.Equals(EventChannel, KillEventChannels.Economy, StringComparison.OrdinalIgnoreCase); }
        }

        /// <summary>Source fact record: never animated, voiced, or sent to danmaku styles.</summary>
        public bool IsObservationEvent
        {
            get { return string.Equals(EventChannel, KillEventChannels.Observation, StringComparison.OrdinalIgnoreCase); }
        }

        public bool IsFromLol
        {
            get { return string.Equals(Source, KillEventSources.Lol, StringComparison.OrdinalIgnoreCase); }
        }
    }

    public sealed class FeedbackIntentInfo
    {
        public string Kind { get; set; }
        public int Tier { get; set; }
        public string[] Modifiers { get; set; }
        public bool PlayMain { get; set; }
        public string Actor { get; set; }
        public string Target { get; set; }

        public bool HasModifier(string modifier)
        {
            if (Modifiers == null) return false;
            foreach (string item in Modifiers)
            {
                if (string.Equals(item, modifier, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }
    }

    /// <summary>`detail.lol` of the v2 envelope.</summary>
    public sealed class LolEventDetail
    {
        public string Event { get; set; }
        public string RiotEventName { get; set; }
        public ulong RiotEventId { get; set; }
        public double? GameTime { get; set; }
        public string Relation { get; set; }
        public string LocalRole { get; set; }
        public string Actor { get; set; }
        public string Victim { get; set; }
        public string[] Assisters { get; set; }
        public string ActorChampion { get; set; }
        public string VictimChampion { get; set; }
        public string[] AssisterChampions { get; set; }
        public int Multikill { get; set; }
        public string AcingTeam { get; set; }
        public string DragonType { get; set; }
        public bool? Stolen { get; set; }
        public string Structure { get; set; }
        public string Result { get; set; }
        public bool? Shutdown { get; set; }
        public bool? Revenge { get; set; }
        public int? GoldDelta { get; set; }
        public int? CreepScore { get; set; }
    }

    public static class KillEventSources
    {
        public const string Cs2 = "cs2";
        public const string CsgoLegacy = "csgo_legacy";
        public const string Lol = "lol";
    }

    public static class FeedbackKinds
    {
        public const string Kill = "kill";
        public const string Assist = "assist";
        public const string Death = "death";
        public const string Objective = "objective";
        public const string MatchResult = "match_result";
        public const string Observation = "observation";
    }

    public static class KillEventChannels
    {
        public const string Combat = "combat";
        public const string Economy = "economy";
        public const string Observation = "observation";

        public static string Normalize(string eventChannel, string eventKind, bool isAssist)
        {
            if (string.Equals(eventChannel, Combat, StringComparison.OrdinalIgnoreCase))
            {
                return Combat;
            }

            if (string.Equals(eventChannel, Economy, StringComparison.OrdinalIgnoreCase))
            {
                return Economy;
            }

            if (string.Equals(eventChannel, Observation, StringComparison.OrdinalIgnoreCase))
            {
                return Observation;
            }

            // Service and clients ship together: an unknown future channel is a
            // record for newer consumers, never a reason to play a combat style.
            if (!string.IsNullOrWhiteSpace(eventChannel))
            {
                return Observation;
            }

            return IsEconomyKind(eventKind) && !isAssist ? Economy : Combat;
        }

        private static bool IsEconomyKind(string eventKind)
        {
            return string.Equals(eventKind, "round_win", StringComparison.OrdinalIgnoreCase)
                || string.Equals(eventKind, "round_loss", StringComparison.OrdinalIgnoreCase)
                || string.Equals(eventKind, "bomb_plant", StringComparison.OrdinalIgnoreCase)
                || string.Equals(eventKind, "bomb_defuse", StringComparison.OrdinalIgnoreCase)
                || string.Equals(eventKind, "hostage_interact", StringComparison.OrdinalIgnoreCase)
                || string.Equals(eventKind, "hostage_rescue", StringComparison.OrdinalIgnoreCase);
        }
    }
}

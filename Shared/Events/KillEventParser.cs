// Shared by Widget (UWP) and CompatibilityHost; see KillEventContract.cs.
using System;
using System.Collections.Generic;
using Windows.Data.Json;

#if WINDOWS_UWP
namespace KillConfirmGameBar.Services
#else
namespace KillConfirmCompatibility.Services
#endif
{
    /// <summary>
    /// Single client parser for `/events` items. Type-tolerant: a malformed
    /// optional field yields its default instead of dropping the event.
    /// </summary>
    public static class KillEventParser
    {
        public static KillEvent Parse(JsonObject json)
        {
            bool isAssist = Bool(json, "is_assist", false);
            string eventKind = Str(json, "event_kind");
            string weaponBadgeKey = Str(json, "weapon_badge_key");
            return new KillEvent
            {
                EventChannel = KillEventChannels.Normalize(Str(json, "event_channel"), eventKind, isAssist),
                KillCount = (int)Num(json, "kill_count"),
                IsHeadshot = Bool(json, "is_headshot", false),
                IsKnifeKill = Bool(json, "is_knife_kill", false),
                IsGrenadeKill = Bool(json, "is_grenade_kill", false),
                IsFirstKill = Bool(json, "is_first_kill", false),
                IsLastKill = Bool(json, "is_last_kill", false),
                IsRevenge = Bool(json, "is_revenge", false)
                    || string.Equals(weaponBadgeKey, "revenge", StringComparison.OrdinalIgnoreCase),
                IsAssist = isAssist,
                PlayMainAnimation = Bool(json, "play_main_animation", true),
                AnimationKey = Str(json, "animation_key"),
                EventKind = eventKind,
                WeaponBadgeKey = weaponBadgeKey,
                WeaponName = Str(json, "weapon_name"),
                MoneyReward = (int)Num(json, "money_reward"),
                RoundNumber = (int)Num(json, "round_number"),
                MoneyEpoch = (int)Num(json, "money_epoch"),
                PlayerName = Str(json, "player_name"),
                TargetName = Str(json, "target_name"),
                SteamId = Str(json, "steamid"),
                SourceGeneration = ToUInt64(Num(json, "source_generation")),
                PublishedUnixMs = ToUInt64(Num(json, "published_unix_ms")),
                Schema = (int)Num(json, "schema"),
                Source = Str(json, "source"),
                Intent = ParseIntent(Obj(json, "intent")),
                Lol = ParseLol(Obj(Obj(json, "detail"), "lol"))
            };
        }

        private static FeedbackIntentInfo ParseIntent(JsonObject json)
        {
            if (json == null) return null;
            return new FeedbackIntentInfo
            {
                Kind = Str(json, "kind"),
                Tier = (int)Num(json, "tier"),
                Modifiers = Strings(json, "modifiers"),
                PlayMain = Bool(json, "play_main", false),
                Actor = Str(json, "actor"),
                Target = Str(json, "target")
            };
        }

        private static LolEventDetail ParseLol(JsonObject json)
        {
            if (json == null) return null;
            IJsonValue time;
            IJsonValue stolen;
            IJsonValue shutdown;
            IJsonValue revenge;
            IJsonValue goldDelta;
            IJsonValue creepScore;
            return new LolEventDetail
            {
                Event = Str(json, "event"),
                RiotEventName = Str(json, "riot_event_name"),
                RiotEventId = ToUInt64(Num(json, "riot_event_id")),
                GameTime = json.TryGetValue("game_time", out time) && time.ValueType == JsonValueType.Number
                    ? time.GetNumber() : (double?)null,
                Relation = Str(json, "relation"),
                LocalRole = Str(json, "local_role"),
                Actor = Str(json, "actor"),
                Victim = Str(json, "victim"),
                Assisters = Strings(json, "assisters"),
                ActorChampion = Str(json, "actor_champion"),
                VictimChampion = Str(json, "victim_champion"),
                AssisterChampions = Strings(json, "assister_champions"),
                Multikill = (int)Num(json, "multikill"),
                AcingTeam = Str(json, "acing_team"),
                DragonType = Str(json, "dragon_type"),
                Stolen = json.TryGetValue("stolen", out stolen) && stolen.ValueType == JsonValueType.Boolean
                    ? stolen.GetBoolean() : (bool?)null,
                Structure = Str(json, "structure"),
                Result = Str(json, "result"),
                Shutdown = json.TryGetValue("shutdown", out shutdown) && shutdown.ValueType == JsonValueType.Boolean
                    ? shutdown.GetBoolean() : (bool?)null,
                Revenge = json.TryGetValue("revenge", out revenge) && revenge.ValueType == JsonValueType.Boolean
                    ? revenge.GetBoolean() : (bool?)null,
                GoldDelta = json.TryGetValue("gold_delta", out goldDelta) && goldDelta.ValueType == JsonValueType.Number
                    ? (int)goldDelta.GetNumber() : (int?)null,
                CreepScore = json.TryGetValue("creep_score", out creepScore) && creepScore.ValueType == JsonValueType.Number
                    ? (int)creepScore.GetNumber() : (int?)null
            };
        }

        private static string Str(JsonObject json, string key)
        {
            IJsonValue value;
            return json != null && json.TryGetValue(key, out value) && value.ValueType == JsonValueType.String
                ? value.GetString() : string.Empty;
        }

        private static double Num(JsonObject json, string key)
        {
            IJsonValue value;
            return json != null && json.TryGetValue(key, out value) && value.ValueType == JsonValueType.Number
                ? value.GetNumber() : 0;
        }

        private static bool Bool(JsonObject json, string key, bool fallback)
        {
            IJsonValue value;
            return json != null && json.TryGetValue(key, out value) && value.ValueType == JsonValueType.Boolean
                ? value.GetBoolean() : fallback;
        }

        private static JsonObject Obj(JsonObject json, string key)
        {
            IJsonValue value;
            return json != null && json.TryGetValue(key, out value) && value.ValueType == JsonValueType.Object
                ? value.GetObject() : null;
        }

        private static string[] Strings(JsonObject json, string key)
        {
            IJsonValue value;
            if (json == null || !json.TryGetValue(key, out value) || value.ValueType != JsonValueType.Array)
            {
                return new string[0];
            }
            var result = new List<string>();
            foreach (IJsonValue item in value.GetArray())
            {
                if (item.ValueType == JsonValueType.String) result.Add(item.GetString());
            }
            return result.ToArray();
        }

        internal static ulong ToUInt64(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0)
            {
                return 0;
            }

            return value >= ulong.MaxValue ? ulong.MaxValue : (ulong)value;
        }
    }
}

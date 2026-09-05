using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Data.Json;
using Windows.Storage;

namespace KillConfirmGameBar.Danmaku.Engine
{
    internal sealed class SemanticMixRatio
    {
        public double Core { get; }
        public double Semantic { get; }
        public double Atmosphere { get; }
        public double Ambient { get; }

        public SemanticMixRatio(double core, double semantic, double atmosphere, double ambient)
        {
            double sum = core + semantic + atmosphere + ambient;
            if (sum <= 0.0001)
            {
                Core = 0.50;
                Semantic = 0.25;
                Atmosphere = 0.15;
                Ambient = 0.10;
            }
            else
            {
                Core = core / sum;
                Semantic = semantic / sum;
                Atmosphere = atmosphere / sum;
                Ambient = ambient / sum;
            }
        }

        public static SemanticMixRatio Default { get; } =
            new SemanticMixRatio(0.50, 0.25, 0.15, 0.10);
    }

    internal sealed class SemanticEventProfile
    {
        public IReadOnlyDictionary<string, double> PreferredTopics { get; }
        public IReadOnlyDictionary<string, double> PreferredStances { get; }
        public IReadOnlyDictionary<string, double> PreferredTargets { get; }
        public IReadOnlyCollection<string> AllowedContexts { get; }
        public SemanticMixRatio MixRatio { get; }
        public double ImpulseDurationSeconds { get; }
        public double ImpulseBurstIntervalSeconds { get; }
        public double ImpulseStrength { get; }

        public SemanticEventProfile(
            IReadOnlyDictionary<string, double> preferredTopics,
            IReadOnlyDictionary<string, double> preferredStances,
            IReadOnlyDictionary<string, double> preferredTargets,
            IReadOnlyCollection<string> allowedContexts,
            SemanticMixRatio mixRatio,
            double impulseDurationSeconds,
            double impulseBurstIntervalSeconds,
            double impulseStrength)
        {
            PreferredTopics = preferredTopics ?? new Dictionary<string, double>(StringComparer.Ordinal);
            PreferredStances = preferredStances ?? new Dictionary<string, double>(StringComparer.Ordinal);
            PreferredTargets = preferredTargets ?? new Dictionary<string, double>(StringComparer.Ordinal);
            AllowedContexts = allowedContexts ?? new HashSet<string>(StringComparer.Ordinal) { "standalone", "game_event", "stream_context" };
            MixRatio = mixRatio ?? SemanticMixRatio.Default;
            ImpulseDurationSeconds = Math.Max(1.0, impulseDurationSeconds);
            ImpulseBurstIntervalSeconds = Math.Max(0.5, impulseBurstIntervalSeconds);
            ImpulseStrength = Math.Max(0.1, impulseStrength);
        }
    }

    internal sealed class AmbientProfile
    {
        public IReadOnlyDictionary<string, double> PreferredTopics { get; }
        public IReadOnlyDictionary<string, double> PreferredStances { get; }
        public IReadOnlyDictionary<string, double> PreferredTargets { get; }
        public IReadOnlyCollection<string> AllowedContexts { get; }
        public double BaseIntervalSeconds { get; }
        public double IntervalJitter { get; }

        public AmbientProfile(
            IReadOnlyDictionary<string, double> preferredTopics,
            IReadOnlyDictionary<string, double> preferredStances,
            IReadOnlyDictionary<string, double> preferredTargets,
            IReadOnlyCollection<string> allowedContexts,
            double baseIntervalSeconds,
            double intervalJitter)
        {
            PreferredTopics = preferredTopics ?? new Dictionary<string, double>(StringComparer.Ordinal);
            PreferredStances = preferredStances ?? new Dictionary<string, double>(StringComparer.Ordinal);
            PreferredTargets = preferredTargets ?? new Dictionary<string, double>(StringComparer.Ordinal);
            AllowedContexts = allowedContexts ?? new HashSet<string>(StringComparer.Ordinal);
            BaseIntervalSeconds = Math.Max(1.0, baseIntervalSeconds);
            IntervalJitter = Math.Max(0.0, Math.Min(0.8, intervalJitter));
        }

        public static AmbientProfile Default { get; } = new AmbientProfile(
            new Dictionary<string, double>(StringComparer.Ordinal)
            {
                ["streamer_skill_gameplay"] = 1.5,
                ["streamer_appearance_pig_weight"] = 1.2,
                ["historical_memes"] = 1.2,
                ["daily_life_work"] = 1.0
            },
            new Dictionary<string, double>(StringComparer.Ordinal)
            {
                ["tease_playful"] = 1.5,
                ["cynical_sarcastic"] = 1.2,
                ["neutral_informative"] = 1.0
            },
            new Dictionary<string, double>(StringComparer.Ordinal)
            {
                ["streamer"] = 1.5,
                ["chat_audience"] = 1.2
            },
            new HashSet<string>(StringComparer.Ordinal)
            {
                "standalone",
                "stream_context"
            },
            3.2,
            0.35);
    }

    internal static class SemanticProfileRepository
    {
        private static readonly object SyncRoot = new object();
        private static Dictionary<DanmakuEventKind, SemanticEventProfile> _eventProfiles =
            new Dictionary<DanmakuEventKind, SemanticEventProfile>();
        private static AmbientProfile _ambientProfile = AmbientProfile.Default;
        private static Task _loadTask;

        public static AmbientProfile Ambient => _ambientProfile;

        public static Task EnsureLoadedAsync()
        {
            lock (SyncRoot)
            {
                if (_loadTask == null)
                {
                    _loadTask = LoadAsync();
                }
                return _loadTask;
            }
        }

        public static SemanticEventProfile GetProfile(DanmakuEventKind kind)
        {
            lock (SyncRoot)
            {
                SemanticEventProfile profile;
                if (_eventProfiles.TryGetValue(kind, out profile))
                {
                    return profile;
                }
            }
            return new SemanticEventProfile(null, null, null, null, SemanticMixRatio.Default, 5.0, 1.2, 1.0);
        }

        private static async Task LoadAsync()
        {
            try
            {
                StorageFile file = await StorageFile.GetFileFromApplicationUriAsync(
                    new Uri("ms-appx:///Danmaku/Pools/semantic_event_profiles.json"));
                string text = await FileIO.ReadTextAsync(file);
                JsonObject root;
                if (!JsonObject.TryParse(text, out root))
                {
                    throw new InvalidOperationException("semantic_event_profiles.json is not valid JSON.");
                }

                if (root.ContainsKey("ambient") && root.GetNamedValue("ambient").ValueType == JsonValueType.Object)
                {
                    _ambientProfile = ParseAmbientProfile(root.GetNamedObject("ambient"));
                }

                if (root.ContainsKey("events") && root.GetNamedValue("events").ValueType == JsonValueType.Object)
                {
                    JsonObject eventsObj = root.GetNamedObject("events");
                    var loaded = new Dictionary<DanmakuEventKind, SemanticEventProfile>();
                    foreach (string key in eventsObj.Keys)
                    {
                        DanmakuEventKind kind;
                        if (TryParseEventKind(key, out kind))
                        {
                            JsonObject profileObj = eventsObj.GetNamedObject(key);
                            loaded[kind] = ParseEventProfile(profileObj);
                        }
                    }

                    lock (SyncRoot)
                    {
                        _eventProfiles = loaded;
                    }
                }
            }
            catch (Exception ex)
            {
                App.Log("SemanticProfileRepository.LoadAsync fallback: " + ex.Message);
            }
        }

        private static AmbientProfile ParseAmbientProfile(JsonObject obj)
        {
            var topics = ParseWeightMap(obj, "preferred_topics");
            var stances = ParseWeightMap(obj, "preferred_stances");
            var targets = ParseWeightMap(obj, "preferred_targets");
            var contexts = new HashSet<string>(StringComparer.Ordinal);
            if (obj.ContainsKey("allowed_contexts") && obj.GetNamedValue("allowed_contexts").ValueType == JsonValueType.Array)
            {
                JsonArray arr = obj.GetNamedArray("allowed_contexts");
                for (int i = 0; i < arr.Count; i++)
                {
                    contexts.Add(arr[i].GetString());
                }
            }
            double baseInterval = obj.GetNamedNumber("base_interval_seconds", 3.2);
            double jitter = obj.GetNamedNumber("interval_jitter", 0.35);

            return new AmbientProfile(topics, stances, targets, contexts, baseInterval, jitter);
        }

        private static SemanticEventProfile ParseEventProfile(JsonObject obj)
        {
            var topics = ParseWeightMap(obj, "preferred_topics");
            var stances = ParseWeightMap(obj, "preferred_stances");
            var targets = ParseWeightMap(obj, "preferred_targets");
            var contexts = new HashSet<string>(StringComparer.Ordinal);
            if (obj.ContainsKey("allowed_contexts") && obj.GetNamedValue("allowed_contexts").ValueType == JsonValueType.Array)
            {
                JsonArray arr = obj.GetNamedArray("allowed_contexts");
                for (int i = 0; i < arr.Count; i++)
                {
                    contexts.Add(arr[i].GetString());
                }
            }
            if (contexts.Count == 0)
            {
                contexts.Add("standalone");
                contexts.Add("game_event");
                contexts.Add("stream_context");
            }

            SemanticMixRatio mixRatio = SemanticMixRatio.Default;
            if (obj.ContainsKey("mix_ratio") && obj.GetNamedValue("mix_ratio").ValueType == JsonValueType.Object)
            {
                JsonObject mix = obj.GetNamedObject("mix_ratio");
                mixRatio = new SemanticMixRatio(
                    mix.GetNamedNumber("core", 0.50),
                    mix.GetNamedNumber("semantic", 0.25),
                    mix.GetNamedNumber("atmosphere", 0.15),
                    mix.GetNamedNumber("ambient", 0.10));
            }

            double duration = obj.GetNamedNumber("impulse_duration_seconds", 5.0);
            double interval = obj.GetNamedNumber("impulse_burst_interval_seconds", 1.2);
            double strength = obj.GetNamedNumber("impulse_strength", 1.0);

            return new SemanticEventProfile(topics, stances, targets, contexts, mixRatio, duration, interval, strength);
        }

        private static Dictionary<string, double> ParseWeightMap(JsonObject parent, string propertyName)
        {
            var map = new Dictionary<string, double>(StringComparer.Ordinal);
            if (parent.ContainsKey(propertyName) && parent.GetNamedValue(propertyName).ValueType == JsonValueType.Object)
            {
                JsonObject obj = parent.GetNamedObject(propertyName);
                foreach (string key in obj.Keys)
                {
                    map[key] = obj.GetNamedNumber(key, 1.0);
                }
            }
            return map;
        }

        private static bool TryParseEventKind(string key, out DanmakuEventKind kind)
        {
            switch (key.ToLowerInvariant())
            {
                case "kill": kind = DanmakuEventKind.Kill; return true;
                case "first_kill": kind = DanmakuEventKind.FirstKill; return true;
                case "headshot": kind = DanmakuEventKind.Headshot; return true;
                case "knife_kill": kind = DanmakuEventKind.KnifeKill; return true;
                case "grenade_kill": kind = DanmakuEventKind.GrenadeKill; return true;
                case "multi_kill": kind = DanmakuEventKind.MultiKill; return true;
                case "epic_streak": kind = DanmakuEventKind.EpicStreak; return true;
                case "last_kill": kind = DanmakuEventKind.LastKill; return true;
                case "assist": kind = DanmakuEventKind.Assist; return true;
                case "death": kind = DanmakuEventKind.Death; return true;
                case "round_win": kind = DanmakuEventKind.RoundWin; return true;
                case "round_loss": kind = DanmakuEventKind.RoundLoss; return true;
                case "bomb_plant": kind = DanmakuEventKind.BombPlant; return true;
                case "bomb_defuse": kind = DanmakuEventKind.BombDefuse; return true;
                case "hostage_interact": kind = DanmakuEventKind.HostageInteract; return true;
                case "hostage_rescue": kind = DanmakuEventKind.HostageRescue; return true;
                default: kind = DanmakuEventKind.General; return false;
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Data.Json;
using Windows.Storage;

namespace KillConfirmGameBar.Danmaku.Engine
{
    internal sealed class DanmakuLibraryReference
    {
        public int Index { get; set; }
    }

    internal sealed class DanmakuEventReferencePool
    {
        public DanmakuEventReferencePool(
            IReadOnlyList<DanmakuLibraryReference> core,
            IReadOnlyList<DanmakuLibraryReference> water)
        {
            Core = core;
            Water = water;
        }

        public IReadOnlyList<DanmakuLibraryReference> Core { get; }
        public IReadOnlyList<DanmakuLibraryReference> Water { get; }
    }

    internal static class DanmakuEventPoolRepository
    {
        private static readonly object SyncRoot = new object();
        private static readonly DanmakuEventKind[] SupportedKinds =
        {
            DanmakuEventKind.Kill,
            DanmakuEventKind.FirstKill,
            DanmakuEventKind.Headshot,
            DanmakuEventKind.KnifeKill,
            DanmakuEventKind.GrenadeKill,
            DanmakuEventKind.MultiKill,
            DanmakuEventKind.EpicStreak,
            DanmakuEventKind.LastKill,
            DanmakuEventKind.Assist,
            DanmakuEventKind.Death,
            DanmakuEventKind.RoundWin,
            DanmakuEventKind.RoundLoss,
            DanmakuEventKind.BombPlant,
            DanmakuEventKind.BombDefuse,
            DanmakuEventKind.HostageInteract,
            DanmakuEventKind.HostageRescue
        };

        private static Dictionary<DanmakuEventKind, DanmakuEventReferencePool> _pools =
            new Dictionary<DanmakuEventKind, DanmakuEventReferencePool>();
        private static Task _loadTask;

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

        public static IReadOnlyList<string> GetMessages(
            DanmakuEventKind kind,
            DanmakuMessageRole role)
        {
            IReadOnlyList<DanmakuLibraryReference> references;
            lock (SyncRoot)
            {
                DanmakuEventReferencePool pool;
                if (!_pools.TryGetValue(kind, out pool) || pool == null)
                {
                    return null;
                }
                references = role == DanmakuMessageRole.Core ? pool.Core : pool.Water;
            }

            var result = new List<string>(references.Count);
            for (int i = 0; i < references.Count; i++)
            {
                DanmakuLibraryReference reference = references[i];
                string text;
                if (DanmakuRepository.TryGetByIndex(reference.Index, out text))
                {
                    result.Add(text);
                }
            }
            return result;
        }

        private static async Task LoadAsync()
        {
            try
            {
                await DanmakuRepository.EnsureLoadedAsync();
                StorageFile file = await StorageFile.GetFileFromApplicationUriAsync(
                    new Uri("ms-appx:///Danmaku/Pools/event_reactions.json"));
                string text = await FileIO.ReadTextAsync(file);
                JsonObject root;
                if (!JsonObject.TryParse(text, out root))
                {
                    throw new InvalidOperationException("event_reactions.json is not a JSON object.");
                }

                var loaded = new Dictionary<DanmakuEventKind, DanmakuEventReferencePool>();
                for (int i = 0; i < SupportedKinds.Length; i++)
                {
                    DanmakuEventKind kind = SupportedKinds[i];
                    string key = ToStorageKey(kind);
                    if (!root.ContainsKey(key)
                        || root.GetNamedValue(key).ValueType != JsonValueType.Object)
                    {
                        throw new InvalidOperationException("Missing danmaku event reference pool: " + key);
                    }

                    JsonObject poolObject = root.GetNamedObject(key);
                    List<DanmakuLibraryReference> core = ReadReferences(poolObject, "core");
                    List<DanmakuLibraryReference> water = ReadReferences(poolObject, "water");
                    DanmakuReactionPolicy policy = DanmakuReactionPolicies.Resolve(kind);
                    if (core.Count < policy.CoreCount || water.Count < policy.AtmosphereCount)
                    {
                        throw new InvalidOperationException(
                            "Danmaku event pool does not satisfy its core/water reaction quota: " + key);
                    }
                    ValidateReferences(core, key, "core");
                    ValidateReferences(water, key, "water");
                    loaded[kind] = new DanmakuEventReferencePool(core, water);
                }

                lock (SyncRoot)
                {
                    _pools = loaded;
                }
            }
            catch (Exception ex)
            {
                // No fallback text: invalid references mean no event danmaku.
                App.Log("DanmakuEventPoolRepository.LoadAsync failed: " + ex.Message);
            }
        }

        private static List<DanmakuLibraryReference> ReadReferences(
            JsonObject pool,
            string propertyName)
        {
            var result = new List<DanmakuLibraryReference>();
            if (!pool.ContainsKey(propertyName)
                || pool.GetNamedValue(propertyName).ValueType != JsonValueType.Array)
            {
                return result;
            }

            JsonArray items = pool.GetNamedArray(propertyName);
            for (int i = 0; i < items.Count; i++)
            {
                IJsonValue value = items[i];
                if (value.ValueType != JsonValueType.Object)
                {
                    continue;
                }

                JsonObject reference = value.GetObject();
                if (!reference.ContainsKey("index")
                    || reference.GetNamedValue("index").ValueType != JsonValueType.Number)
                {
                    continue;
                }

                double rawIndex = reference.GetNamedNumber("index");
                int index = (int)rawIndex;
                if (index <= 0 || Math.Abs(rawIndex - index) > 0.001)
                {
                    continue;
                }

                result.Add(new DanmakuLibraryReference
                {
                    Index = index
                });
            }
            return result;
        }

        private static void ValidateReferences(
            IReadOnlyList<DanmakuLibraryReference> references,
            string eventKey,
            string role)
        {
            for (int i = 0; i < references.Count; i++)
            {
                string ignored;
                if (!DanmakuRepository.TryGetByIndex(references[i].Index, out ignored))
                {
                    throw new InvalidOperationException(
                        "Invalid 6657 reference: " + eventKey + "/" + role + " #" + (i + 1));
                }
            }
        }

        private static string ToStorageKey(DanmakuEventKind kind)
        {
            switch (kind)
            {
                case DanmakuEventKind.Kill: return "kill";
                case DanmakuEventKind.FirstKill: return "first_kill";
                case DanmakuEventKind.Headshot: return "headshot";
                case DanmakuEventKind.KnifeKill: return "knife_kill";
                case DanmakuEventKind.GrenadeKill: return "grenade_kill";
                case DanmakuEventKind.MultiKill: return "multi_kill";
                case DanmakuEventKind.EpicStreak: return "epic_streak";
                case DanmakuEventKind.LastKill: return "last_kill";
                case DanmakuEventKind.Assist: return "assist";
                case DanmakuEventKind.Death: return "death";
                case DanmakuEventKind.RoundWin: return "round_win";
                case DanmakuEventKind.RoundLoss: return "round_loss";
                case DanmakuEventKind.BombPlant: return "bomb_plant";
                case DanmakuEventKind.BombDefuse: return "bomb_defuse";
                case DanmakuEventKind.HostageInteract: return "hostage_interact";
                case DanmakuEventKind.HostageRescue: return "hostage_rescue";
                default: return string.Empty;
            }
        }
    }
}

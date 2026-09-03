using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Data.Json;
using Windows.Storage;

namespace KillConfirmGameBar.Danmaku.Engine
{
    internal sealed class DanmakuEventPoolEntry
    {
        public string Id { get; set; }
        public string Text { get; set; }
        public string Intent { get; set; }
        public string Family { get; set; }
        public string Phase { get; set; }
        public string Derivation { get; set; }
        public string SourceExcerpt { get; set; }
        public int SourceIndex { get; set; }
    }

    internal static class DanmakuEventPoolRepository
    {
        internal const string EventPoolDirectoryName = "EventPools";
        internal const string LifecyclePoolDirectoryName = "LifecyclePools";
        internal const int MinimumEventPoolSize = 1000;

        private static readonly object SyncRoot = new object();
        private static IReadOnlyList<DanmakuEventPoolEntry> _openingEntries = Array.Empty<DanmakuEventPoolEntry>();
        private static IReadOnlyList<DanmakuEventPoolEntry> _sessionEndEntries = Array.Empty<DanmakuEventPoolEntry>();
        private static Dictionary<DanmakuEventKind, IReadOnlyList<DanmakuEventPoolEntry>> _eventPools =
            new Dictionary<DanmakuEventKind, IReadOnlyList<DanmakuEventPoolEntry>>();
        private static Task _loadTask;
        private static bool _isLoadCompleted;
        private static bool _isAvailable;

        public static bool IsLoadCompleted
        {
            get { lock (SyncRoot) { return _isLoadCompleted; } }
        }

        public static bool IsAvailable
        {
            get { lock (SyncRoot) { return _isAvailable; } }
        }

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

        public static IReadOnlyList<DanmakuEventPoolEntry> GetOpeningEntries(bool directCallOnly)
        {
            IReadOnlyList<DanmakuEventPoolEntry> source;
            lock (SyncRoot)
            {
                source = new List<DanmakuEventPoolEntry>(_openingEntries);
            }
            if (!directCallOnly)
            {
                return source;
            }

            var result = new List<DanmakuEventPoolEntry>();
            for (int i = 0; i < source.Count; i++)
            {
                DanmakuEventPoolEntry entry = source[i];
                if (entry != null
                    && (string.Equals(entry.Intent, "open_door", StringComparison.Ordinal)
                        || string.Equals(entry.Intent, "urge_start", StringComparison.Ordinal)))
                {
                    result.Add(entry);
                }
            }
            return result.Count > 0 ? result : source;
        }

        public static IReadOnlyList<DanmakuEventPoolEntry> GetSessionEndEntries()
        {
            lock (SyncRoot)
            {
                return new List<DanmakuEventPoolEntry>(_sessionEndEntries);
            }
        }

        public static IReadOnlyList<DanmakuEventPoolEntry> GetEventEntries(DanmakuEventKind kind)
        {
            lock (SyncRoot)
            {
                IReadOnlyList<DanmakuEventPoolEntry> pool;
                if (!_eventPools.TryGetValue(kind, out pool) || pool == null)
                {
                    return Array.Empty<DanmakuEventPoolEntry>();
                }
                return new List<DanmakuEventPoolEntry>(pool);
            }
        }

        public static IReadOnlyList<string> GetEventTexts(
            DanmakuEventKind kind,
            int skip,
            int count)
        {
            IReadOnlyList<DanmakuEventPoolEntry> entries = GetEventEntries(kind);
            var result = new List<string>(Math.Max(0, count));
            for (int i = Math.Max(0, skip); i < entries.Count && result.Count < count; i++)
            {
                DanmakuEventPoolEntry entry = entries[i];
                if (entry != null && !string.IsNullOrWhiteSpace(entry.Text))
                {
                    result.Add(entry.Text);
                }
            }
            return result;
        }

        private static async Task LoadAsync()
        {
            try
            {
                await DanmakuRepository.EnsureLoadedAsync();
                JsonObject openingRoot = await ReadPoolFileAsync(
                    LifecyclePoolDirectoryName, "opening_wait.json");
                IReadOnlyList<DanmakuEventPoolEntry> opening = ReadEntries(
                    openingRoot.GetNamedArray("entries"), "opening_wait");
                JsonObject endingRoot = await ReadPoolFileAsync(
                    LifecyclePoolDirectoryName, "session_end.json");
                IReadOnlyList<DanmakuEventPoolEntry> ending = ReadEntries(
                    endingRoot.GetNamedArray("entries"), "session_end");

                var eventPools = new Dictionary<DanmakuEventKind, IReadOnlyList<DanmakuEventPoolEntry>>();
                foreach (DanmakuEventKind kind in SupportedEventKinds())
                {
                    string key = ToStorageKey(kind);
                    JsonObject eventObject = await ReadPoolFileAsync(
                        EventPoolDirectoryName, key + ".json");
                    string storedEvent = ReadOptionalString(eventObject, "event");
                    if (!string.Equals(storedEvent, key, StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException("Event pool identity mismatch: " + key);
                    }

                    IReadOnlyList<DanmakuEventPoolEntry> entries = ReadEntries(
                        eventObject.GetNamedArray("entries"), key);
                    if (entries.Count < MinimumEventPoolSize)
                    {
                        throw new InvalidOperationException(
                            "Event pool must contain at least " + MinimumEventPoolSize + " entries: " + key);
                    }
                    eventPools[kind] = entries;
                }

                if (opening.Count == 0 || ending.Count == 0)
                {
                    throw new InvalidOperationException("Lifecycle source-derived pool is empty.");
                }

                lock (SyncRoot)
                {
                    _openingEntries = opening;
                    _sessionEndEntries = ending;
                    _eventPools = eventPools;
                    _isAvailable = true;
                }
                App.Log("DanmakuEventPoolRepository loaded source-derived event pools: events="
                    + eventPools.Count + ", opening=" + opening.Count + ", end=" + ending.Count);
            }
            catch (Exception ex)
            {
                lock (SyncRoot)
                {
                    _openingEntries = Array.Empty<DanmakuEventPoolEntry>();
                    _sessionEndEntries = Array.Empty<DanmakuEventPoolEntry>();
                    _eventPools = new Dictionary<DanmakuEventKind, IReadOnlyList<DanmakuEventPoolEntry>>();
                    _isAvailable = false;
                }
                App.Log("DanmakuEventPoolRepository.LoadAsync failed: " + ex.Message);
            }
            finally
            {
                lock (SyncRoot)
                {
                    _isLoadCompleted = true;
                }
            }
        }

        private static async Task<JsonObject> ReadPoolFileAsync(string directoryName, string fileName)
        {
            StorageFile file = await StorageFile.GetFileFromApplicationUriAsync(
                new Uri("ms-appx:///Danmaku/" + directoryName + "/" + fileName));
            string jsonText = await FileIO.ReadTextAsync(file);
            JsonObject root;
            if (!JsonObject.TryParse(jsonText, out root))
            {
                throw new InvalidOperationException(fileName + " is not a JSON object.");
            }
            return root;
        }

        private static IReadOnlyList<DanmakuEventPoolEntry> ReadEntries(
            JsonArray messages,
            string poolKey)
        {
            var result = new List<DanmakuEventPoolEntry>(messages.Count);
            var seenIds = new HashSet<string>(StringComparer.Ordinal);
            var seenTexts = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < messages.Count; i++)
            {
                if (messages[i].ValueType != JsonValueType.Object)
                {
                    throw new InvalidOperationException("Invalid source-derived entry: " + poolKey);
                }

                JsonObject item = messages[i].GetObject();
                string id = ReadRequiredString(item, "id", poolKey);
                string text = ReadRequiredString(item, "text", poolKey);
                string sourceExcerpt = ReadRequiredString(item, "source_excerpt", poolKey);
                string derivation = ReadRequiredString(item, "derivation", poolKey);
                int sourceIndex = ReadRequiredPositiveInteger(item, "source_index", poolKey);
                string originalText;
                if (!DanmakuRepository.TryGetByIndex(sourceIndex, out originalText)
                    || string.IsNullOrWhiteSpace(originalText)
                    || originalText.IndexOf(sourceExcerpt, StringComparison.Ordinal) < 0)
                {
                    throw new InvalidOperationException(
                        "Source-derived excerpt mismatch: " + poolKey + " #" + sourceIndex);
                }
                if (text.IndexOf('\r') >= 0 || text.IndexOf('\n') >= 0)
                {
                    throw new InvalidOperationException("Source-derived text must be single-line: " + id);
                }
                if (!seenIds.Add(id) || !seenTexts.Add(text))
                {
                    throw new InvalidOperationException("Duplicate source-derived entry: " + id);
                }

                result.Add(new DanmakuEventPoolEntry
                {
                    Id = id,
                    Text = text,
                    Intent = ReadOptionalString(item, "intent"),
                    Family = ReadOptionalString(item, "family"),
                    Phase = ReadOptionalString(item, "phase"),
                    Derivation = derivation,
                    SourceExcerpt = sourceExcerpt,
                    SourceIndex = sourceIndex
                });
            }
            return result;
        }

        private static string ReadRequiredString(JsonObject item, string propertyName, string poolKey)
        {
            string value = ReadOptionalString(item, propertyName);
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException("Missing " + propertyName + " in " + poolKey);
            }
            return value;
        }

        private static string ReadOptionalString(JsonObject item, string propertyName)
        {
            if (item == null
                || !item.ContainsKey(propertyName)
                || item.GetNamedValue(propertyName).ValueType != JsonValueType.String)
            {
                return null;
            }
            return item.GetNamedString(propertyName);
        }

        private static int ReadRequiredPositiveInteger(JsonObject item, string propertyName, string poolKey)
        {
            if (item == null
                || !item.ContainsKey(propertyName)
                || item.GetNamedValue(propertyName).ValueType != JsonValueType.Number)
            {
                throw new InvalidOperationException("Missing " + propertyName + " in " + poolKey);
            }
            double rawValue = item.GetNamedNumber(propertyName);
            int value = (int)rawValue;
            if (value <= 0 || Math.Abs(rawValue - value) >= 0.001)
            {
                throw new InvalidOperationException("Invalid " + propertyName + " in " + poolKey);
            }
            return value;
        }

        private static IEnumerable<DanmakuEventKind> SupportedEventKinds()
        {
            yield return DanmakuEventKind.Kill;
            yield return DanmakuEventKind.FirstKill;
            yield return DanmakuEventKind.Headshot;
            yield return DanmakuEventKind.KnifeKill;
            yield return DanmakuEventKind.GrenadeKill;
            yield return DanmakuEventKind.MultiKill;
            yield return DanmakuEventKind.EpicStreak;
            yield return DanmakuEventKind.LastKill;
            yield return DanmakuEventKind.Assist;
            yield return DanmakuEventKind.Death;
            yield return DanmakuEventKind.RoundWin;
            yield return DanmakuEventKind.RoundLoss;
            yield return DanmakuEventKind.BombPlant;
            yield return DanmakuEventKind.BombDefuse;
            yield return DanmakuEventKind.HostageInteract;
            yield return DanmakuEventKind.HostageRescue;
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

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Data.Json;
using Windows.Storage;

namespace KillConfirmGameBar.Danmaku.Engine
{
    internal sealed class SemanticAnnotationEntry
    {
        public int Index { get; }
        public IReadOnlyList<string> Targets { get; }
        public IReadOnlyList<string> Stances { get; }
        public IReadOnlyList<string> Topics { get; }
        public IReadOnlyList<string> Formats { get; }
        public IReadOnlyList<string> Culture { get; }
        public string Context { get; }
        public string SafetySeverity { get; }
        public IReadOnlyList<string> SafetyFlags { get; }
        public double Confidence { get; }

        public SemanticAnnotationEntry(
            int index,
            IReadOnlyList<string> targets,
            IReadOnlyList<string> stances,
            IReadOnlyList<string> topics,
            IReadOnlyList<string> formats,
            IReadOnlyList<string> culture,
            string context,
            string safetySeverity,
            IReadOnlyList<string> safetyFlags,
            double confidence)
        {
            Index = index;
            Targets = targets ?? Array.Empty<string>();
            Stances = stances ?? Array.Empty<string>();
            Topics = topics ?? Array.Empty<string>();
            Formats = formats ?? Array.Empty<string>();
            Culture = culture ?? Array.Empty<string>();
            Context = context ?? "standalone";
            SafetySeverity = safetySeverity ?? "safe";
            SafetyFlags = safetyFlags ?? Array.Empty<string>();
            Confidence = confidence;
        }

        public bool IsSafe =>
            !string.Equals(SafetySeverity, "toxic_vulgar", StringComparison.OrdinalIgnoreCase);
    }

    internal static class SemanticAnnotationRepository
    {
        private static readonly object SyncRoot = new object();
        private static IReadOnlyList<SemanticAnnotationEntry> _allEntries = Array.Empty<SemanticAnnotationEntry>();
        private static Dictionary<int, SemanticAnnotationEntry> _byIndex = new Dictionary<int, SemanticAnnotationEntry>();
        private static Dictionary<string, List<SemanticAnnotationEntry>> _byTopic = new Dictionary<string, List<SemanticAnnotationEntry>>(StringComparer.Ordinal);
        private static Dictionary<string, List<SemanticAnnotationEntry>> _byStance = new Dictionary<string, List<SemanticAnnotationEntry>>(StringComparer.Ordinal);
        private static Dictionary<string, List<SemanticAnnotationEntry>> _byTarget = new Dictionary<string, List<SemanticAnnotationEntry>>(StringComparer.Ordinal);
        private static Task _loadTask;
        private static bool _isLoaded;

        public static bool IsAvailable
        {
            get
            {
                lock (SyncRoot)
                {
                    return _isLoaded && _allEntries.Count > 0;
                }
            }
        }

        public static IReadOnlyList<SemanticAnnotationEntry> AllEntries
        {
            get
            {
                lock (SyncRoot)
                {
                    return _allEntries;
                }
            }
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

        public static bool TryGetEntryByIndex(int oneBasedIndex, out SemanticAnnotationEntry entry)
        {
            lock (SyncRoot)
            {
                return _byIndex.TryGetValue(oneBasedIndex, out entry);
            }
        }

        public static IReadOnlyList<SemanticAnnotationEntry> QueryCandidates(
            IReadOnlyDictionary<string, double> topics,
            IReadOnlyDictionary<string, double> stances,
            IReadOnlyDictionary<string, double> targets)
        {
            lock (SyncRoot)
            {
                if (!_isLoaded || _allEntries.Count == 0)
                {
                    return Array.Empty<SemanticAnnotationEntry>();
                }

                var candidateSet = new HashSet<SemanticAnnotationEntry>();

                if (topics != null)
                {
                    foreach (string topic in topics.Keys)
                    {
                        List<SemanticAnnotationEntry> list;
                        if (_byTopic.TryGetValue(topic, out list))
                        {
                            for (int i = 0; i < list.Count; i++)
                            {
                                candidateSet.Add(list[i]);
                            }
                        }
                    }
                }

                if (stances != null)
                {
                    foreach (string stance in stances.Keys)
                    {
                        List<SemanticAnnotationEntry> list;
                        if (_byStance.TryGetValue(stance, out list))
                        {
                            for (int i = 0; i < list.Count; i++)
                            {
                                candidateSet.Add(list[i]);
                            }
                        }
                    }
                }

                if (targets != null)
                {
                    foreach (string target in targets.Keys)
                    {
                        List<SemanticAnnotationEntry> list;
                        if (_byTarget.TryGetValue(target, out list))
                        {
                            for (int i = 0; i < list.Count; i++)
                            {
                                candidateSet.Add(list[i]);
                            }
                        }
                    }
                }

                if (candidateSet.Count == 0)
                {
                    return _allEntries;
                }

                var result = new List<SemanticAnnotationEntry>(candidateSet.Count);
                foreach (var entry in candidateSet)
                {
                    result.Add(entry);
                }
                return result;
            }
        }

        private static async Task LoadAsync()
        {
            try
            {
                StorageFile file = await StorageFile.GetFileFromApplicationUriAsync(
                    new Uri("ms-appx:///Danmaku/Annotation/6657_annotations_v1.json"));
                string jsonText = await FileIO.ReadTextAsync(file);
                JsonObject root;
                if (!JsonObject.TryParse(jsonText, out root))
                {
                    throw new InvalidOperationException("6657_annotations_v1.json is not a valid JSON object.");
                }

                if (!root.ContainsKey("annotations") || root.GetNamedValue("annotations").ValueType != JsonValueType.Array)
                {
                    throw new InvalidOperationException("6657_annotations_v1.json missing annotations array.");
                }

                JsonArray array = root.GetNamedArray("annotations");
                var entries = new List<SemanticAnnotationEntry>(array.Count);
                var byIndex = new Dictionary<int, SemanticAnnotationEntry>(array.Count);
                var byTopic = new Dictionary<string, List<SemanticAnnotationEntry>>(StringComparer.Ordinal);
                var byStance = new Dictionary<string, List<SemanticAnnotationEntry>>(StringComparer.Ordinal);
                var byTarget = new Dictionary<string, List<SemanticAnnotationEntry>>(StringComparer.Ordinal);

                for (int i = 0; i < array.Count; i++)
                {
                    IJsonValue itemVal = array[i];
                    if (itemVal.ValueType != JsonValueType.Object)
                    {
                        continue;
                    }

                    JsonObject itemObj = itemVal.GetObject();
                    int index = (int)itemObj.GetNamedNumber("index", 0);
                    if (index <= 0)
                    {
                        continue;
                    }

                    string[] targets = ReadStringArray(itemObj, "targets");
                    string[] stances = ReadStringArray(itemObj, "stances");
                    string[] topics = ReadStringArray(itemObj, "topics");
                    string[] formats = ReadStringArray(itemObj, "formats");
                    string[] culture = ReadStringArray(itemObj, "culture");
                    string context = itemObj.ContainsKey("context") ? itemObj.GetNamedString("context", "standalone") : "standalone";
                    
                    string severity = "safe";
                    string[] flags = Array.Empty<string>();
                    if (itemObj.ContainsKey("safety") && itemObj.GetNamedValue("safety").ValueType == JsonValueType.Object)
                    {
                        JsonObject safetyObj = itemObj.GetNamedObject("safety");
                        severity = safetyObj.GetNamedString("severity", "safe");
                        flags = ReadStringArray(safetyObj, "flags");
                    }

                    double confidence = itemObj.GetNamedNumber("confidence", 1.0);

                    var entry = new SemanticAnnotationEntry(
                        index, targets, stances, topics, formats, culture, context, severity, flags, confidence);

                    entries.Add(entry);
                    byIndex[index] = entry;

                    IndexEntry(byTopic, topics, entry);
                    IndexEntry(byStance, stances, entry);
                    IndexEntry(byTarget, targets, entry);
                }

                lock (SyncRoot)
                {
                    _allEntries = entries;
                    _byIndex = byIndex;
                    _byTopic = byTopic;
                    _byStance = byStance;
                    _byTarget = byTarget;
                    _isLoaded = entries.Count > 0;
                }

                App.Log("SemanticAnnotationRepository loaded successfully. Total entries: " + entries.Count);
            }
            catch (Exception ex)
            {
                lock (SyncRoot)
                {
                    _isLoaded = false;
                }
                App.Log("SemanticAnnotationRepository.LoadAsync failed (safe fallback active): " + ex.Message);
            }
        }

        private static void IndexEntry(Dictionary<string, List<SemanticAnnotationEntry>> indexMap, string[] keys, SemanticAnnotationEntry entry)
        {
            if (keys == null) return;
            for (int i = 0; i < keys.Length; i++)
            {
                string key = keys[i];
                if (string.IsNullOrWhiteSpace(key)) continue;
                List<SemanticAnnotationEntry> list;
                if (!indexMap.TryGetValue(key, out list))
                {
                    list = new List<SemanticAnnotationEntry>();
                    indexMap[key] = list;
                }
                list.Add(entry);
            }
        }

        private static string[] ReadStringArray(JsonObject parent, string propertyName)
        {
            if (!parent.ContainsKey(propertyName) || parent.GetNamedValue(propertyName).ValueType != JsonValueType.Array)
            {
                return Array.Empty<string>();
            }

            JsonArray arr = parent.GetNamedArray(propertyName);
            string[] result = new string[arr.Count];
            for (int i = 0; i < arr.Count; i++)
            {
                result[i] = arr[i].GetString();
            }
            return result;
        }
    }
}

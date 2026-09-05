using System;
using System.Collections.Generic;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Data.Json;
using Windows.Storage;

namespace KillConfirmGameBar.Danmaku.Engine
{
    internal sealed class SemanticAnnotationEntity
    {
        public string Name { get; }
        public string Type { get; }

        public SemanticAnnotationEntity(string name, string type)
        {
            Name = name ?? string.Empty;
            Type = type ?? string.Empty;
        }
    }

    internal sealed class SemanticAnnotationEntry
    {
        public int Index { get; }
        public IReadOnlyList<string> Targets { get; }
        public IReadOnlyList<string> Stances { get; }
        public IReadOnlyList<string> Topics { get; }
        public IReadOnlyList<string> Formats { get; }
        public IReadOnlyList<string> Culture { get; }
        public IReadOnlyList<SemanticAnnotationEntity> Entities { get; }
        public string Context { get; }
        public string SafetySeverity { get; }
        public IReadOnlyList<string> SafetyFlags { get; }
        public double Confidence { get; }
        public bool HasProOrExternalEntity { get; }

        public SemanticAnnotationEntry(
            int index,
            IReadOnlyList<string> targets,
            IReadOnlyList<string> stances,
            IReadOnlyList<string> topics,
            IReadOnlyList<string> formats,
            IReadOnlyList<string> culture,
            IReadOnlyList<SemanticAnnotationEntity> entities,
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
            Entities = entities ?? Array.Empty<SemanticAnnotationEntity>();
            Context = context ?? "standalone";
            SafetySeverity = safetySeverity ?? "safe";
            SafetyFlags = safetyFlags ?? Array.Empty<string>();
            Confidence = confidence;
            HasProOrExternalEntity = EvaluateHasProOrExternalEntity(Targets, Entities);
        }

        private static readonly System.Text.RegularExpressions.Regex ProTextBlacklistRegex =
            new System.Text.RegularExpressions.Regex(
                @"(?i)(NiKo|niko|s1mple|simple|donk|ZywOo|zywoo|载物|dev1ce|device|地外丝|karrigan|大表哥|表猪|m0NESY|m0nesy|小孩|sh1ro|若子|broky|ropz|twistzz|总监|aleksib|小李子|jL|b1t|electronic|cadian|点子哥|snax|fallen|tarik|shroud|Shroud|kennyS|coldzera|flusha|stewie2k|swag|tenz|ququ|QUQU|佳代子|伟伟|马西西|冬瓜强|玩播|茄子|老汤|马圣|阿杜|dupreeh|FaZe|faze|Falcons|falcons|猎鹰|Vitality|小蜜蜂|Spirit|绿龙|Navi|NaVi|NAVI|MOUZ|mouz|老鼠|G2|g2|Astralis|Heroic|Virtus|VP|Cloud9|C9|Liquid|液体|Complexity|coL|FURIA|黑豹|BLG|blg|TES|tes|T1|t1|EDG|edg|MyGO|mygo|原神|鸣潮|崩铁|明日方舟|绝区零|无畏契约|瓦罗兰特|王者荣耀|英雄联盟|LOL|DOTA|刀塔|星铁|黑神话|郑哲伟|刘培祥|陈彦川|枫哥|峰哥|黄眉|爱弥斯|爱音|喵梦|丰川|初华|海铃|爱拍|陈子豪|灰泽满|思诺心仪|长崎|素世|祥子|睦|高松灯|千早|乐奈)",
                System.Text.RegularExpressions.RegexOptions.Compiled);

        public static bool HasProOrExternalText(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            return ProTextBlacklistRegex.IsMatch(text);
        }

        private static bool EvaluateHasProOrExternalEntity(
            IReadOnlyList<string> targets,
            IReadOnlyList<SemanticAnnotationEntity> entities)
        {
            if (targets != null)
            {
                for (int i = 0; i < targets.Count; i++)
                {
                    string t = targets[i];
                    if (string.Equals(t, "pro_player", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(t, "pro_team", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(t, "external_figure", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(t, "caster_host", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            if (entities != null)
            {
                for (int i = 0; i < entities.Count; i++)
                {
                    SemanticAnnotationEntity e = entities[i];
                    if (e != null)
                    {
                        string type = e.Type;
                        if (string.Equals(type, "player", StringComparison.OrdinalIgnoreCase)
                            || string.Equals(type, "team", StringComparison.OrdinalIgnoreCase)
                            || string.Equals(type, "coach", StringComparison.OrdinalIgnoreCase)
                            || string.Equals(type, "caster", StringComparison.OrdinalIgnoreCase)
                            || string.Equals(type, "acg_character", StringComparison.OrdinalIgnoreCase)
                            || string.Equals(type, "org", StringComparison.OrdinalIgnoreCase)
                            || string.Equals(type, "other", StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }

                        if (string.Equals(type, "streamer", StringComparison.OrdinalIgnoreCase))
                        {
                            string name = e.Name;
                            if (!string.Equals(name, "玩机器", StringComparison.OrdinalIgnoreCase)
                                && !string.Equals(name, "刘一博", StringComparison.OrdinalIgnoreCase))
                            {
                                return true;
                            }
                        }
                    }
                }
            }

            return false;
        }
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

        private static readonly System.Text.RegularExpressions.Regex ProBlacklistRegex =
            new System.Text.RegularExpressions.Regex(
                @"(?i)(NiKo|niko|s1mple|simple|donk|ZywOo|zywoo|载物|dev1ce|device|地外丝|karrigan|大表哥|表猪|m0NESY|m0nesy|小孩|sh1ro|若子|broky|ropz|twistzz|总监|aleksib|小李子|jL|b1t|electronic|cadian|点子哥|snax|fallen|tarik|shroud|Shroud|kennyS|coldzera|flusha|stewie2k|swag|tenz|ququ|QUQU|佳代子|伟伟|马西西|冬瓜强|玩播|茄子|老汤|马圣|阿杜|dupreeh|FaZe|faze|Falcons|falcons|猎鹰|Vitality|小蜜蜂|Spirit|绿龙|Navi|NaVi|NAVI|MOUZ|mouz|老鼠|G2|g2|Astralis|Heroic|Virtus|VP|Cloud9|C9|Liquid|液体|Complexity|coL|FURIA|黑豹|BLG|blg|TES|tes|T1|t1|EDG|edg|MyGO|mygo|原神|鸣潮|崩铁|明日方舟|绝区零|无畏契约|瓦罗兰特|王者荣耀|英雄联盟|LOL|DOTA|刀塔|星铁|黑神话|郑哲伟|刘培祥|陈彦川|枫哥|峰哥|黄眉|爱弥斯|爱音|喵梦|丰川|初华|海铃|爱拍|陈子豪|灰泽满|思诺心仪|长崎|素世|祥子|睦|高松灯|千早|乐奈|异灵术|神棍|陈结冰|魔提斯|Mortis|大玩庄园|刘宫)",
                System.Text.RegularExpressions.RegexOptions.Compiled);

        public static bool HasProOrExternalText(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            return ProBlacklistRegex.IsMatch(text);
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
            IReadOnlyDictionary<string, double> targets,
            bool forbidProEntities = false)
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
                                if (!forbidProEntities || !list[i].HasProOrExternalEntity)
                                {
                                    candidateSet.Add(list[i]);
                                }
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
                                if (!forbidProEntities || !list[i].HasProOrExternalEntity)
                                {
                                    candidateSet.Add(list[i]);
                                }
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
                                if (!forbidProEntities || !list[i].HasProOrExternalEntity)
                                {
                                    candidateSet.Add(list[i]);
                                }
                            }
                        }
                    }
                }

                if (candidateSet.Count == 0)
                {
                    if (!forbidProEntities)
                    {
                        return _allEntries;
                    }
                    var filtered = new List<SemanticAnnotationEntry>(_allEntries.Count);
                    for (int i = 0; i < _allEntries.Count; i++)
                    {
                        if (!_allEntries[i].HasProOrExternalEntity)
                        {
                            filtered.Add(_allEntries[i]);
                        }
                    }
                    return filtered;
                }

                var result = new List<SemanticAnnotationEntry>(candidateSet.Count);
                foreach (var entry in candidateSet)
                {
                    result.Add(entry);
                }
                return result;
            }
        }

        public static IReadOnlyList<SemanticAnnotationEntry> QueryCandidatesByRequiredStances(
            IReadOnlyCollection<string> requiredStances,
            bool forbidProEntities = false)
        {
            lock (SyncRoot)
            {
                if (!_isLoaded || _allEntries.Count == 0)
                {
                    return Array.Empty<SemanticAnnotationEntry>();
                }
                if (requiredStances == null || requiredStances.Count == 0)
                {
                    if (!forbidProEntities)
                    {
                        return _allEntries;
                    }
                    var filtered = new List<SemanticAnnotationEntry>(_allEntries.Count);
                    for (int i = 0; i < _allEntries.Count; i++)
                    {
                        if (!_allEntries[i].HasProOrExternalEntity)
                        {
                            filtered.Add(_allEntries[i]);
                        }
                    }
                    return filtered;
                }

                var candidateSet = new HashSet<SemanticAnnotationEntry>();
                foreach (string stance in requiredStances)
                {
                    List<SemanticAnnotationEntry> list;
                    if (!_byStance.TryGetValue(stance, out list))
                    {
                        continue;
                    }
                    for (int i = 0; i < list.Count; i++)
                    {
                        if (!forbidProEntities || !list[i].HasProOrExternalEntity)
                        {
                            candidateSet.Add(list[i]);
                        }
                    }
                }

                return new List<SemanticAnnotationEntry>(candidateSet);
            }
        }

        public static IReadOnlyList<SemanticAnnotationEntry> QueryOpeningCandidates()
        {
            lock (SyncRoot)
            {
                if (!_isLoaded || _allEntries.Count == 0)
                {
                    return Array.Empty<SemanticAnnotationEntry>();
                }

                List<SemanticAnnotationEntry> lazinessList;
                if (!_byTopic.TryGetValue("streamer_schedule_laziness", out lazinessList) || lazinessList == null)
                {
                    lazinessList = new List<SemanticAnnotationEntry>(0);
                }

                var result = new List<SemanticAnnotationEntry>(lazinessList.Count);
                for (int i = 0; i < lazinessList.Count; i++)
                {
                    SemanticAnnotationEntry e = lazinessList[i];
                    if (!e.HasProOrExternalEntity)
                    {
                        result.Add(e);
                    }
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
                    List<SemanticAnnotationEntity> entities = ReadEntities(itemObj, "entities");
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
                        index, targets, stances, topics, formats, culture, entities, context, severity, flags, confidence);

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

        private static List<SemanticAnnotationEntity> ReadEntities(JsonObject parent, string propertyName)
        {
            if (!parent.ContainsKey(propertyName) || parent.GetNamedValue(propertyName).ValueType != JsonValueType.Array)
            {
                return new List<SemanticAnnotationEntity>(0);
            }

            JsonArray arr = parent.GetNamedArray(propertyName);
            var result = new List<SemanticAnnotationEntity>(arr.Count);
            for (int i = 0; i < arr.Count; i++)
            {
                IJsonValue itemVal = arr[i];
                if (itemVal.ValueType == JsonValueType.Object)
                {
                    JsonObject obj = itemVal.GetObject();
                    string name = obj.ContainsKey("name") ? obj.GetNamedString("name", string.Empty) : string.Empty;
                    string type = obj.ContainsKey("type") ? obj.GetNamedString("type", string.Empty) : string.Empty;
                    result.Add(new SemanticAnnotationEntity(name, type));
                }
            }
            return result;
        }
    }
}

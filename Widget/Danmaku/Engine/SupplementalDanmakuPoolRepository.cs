using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Data.Json;
using Windows.Storage;

namespace KillConfirmGameBar.Danmaku.Engine
{
    internal enum SupplementalDanmakuPoolKind
    {
        OpeningWait,
        SessionEnd,
        KillPraise,
        DeathQuestion,
        DeathFlame
    }

    internal sealed class SupplementalDanmakuEntry
    {
        public string Id { get; set; }
        public string Text { get; set; }
        public string Intent { get; set; }
        public string Family { get; set; }
        public string Phase { get; set; }
        public int SourceIndex { get; set; }
    }

    internal static class SupplementalDanmakuPoolRepository
    {
        private static readonly object SyncRoot = new object();
        private static Dictionary<SupplementalDanmakuPoolKind, IReadOnlyList<SupplementalDanmakuEntry>> _pools =
            new Dictionary<SupplementalDanmakuPoolKind, IReadOnlyList<SupplementalDanmakuEntry>>();
        private static Task _loadTask;
        private static bool _isLoadCompleted;
        private static bool _isAvailable;

        public static bool IsLoadCompleted
        {
            get
            {
                lock (SyncRoot)
                {
                    return _isLoadCompleted;
                }
            }
        }

        public static bool IsAvailable
        {
            get
            {
                lock (SyncRoot)
                {
                    return _isAvailable;
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

        public static IReadOnlyList<SupplementalDanmakuEntry> GetEntries(
            SupplementalDanmakuPoolKind kind)
        {
            lock (SyncRoot)
            {
                IReadOnlyList<SupplementalDanmakuEntry> entries;
                if (!_pools.TryGetValue(kind, out entries) || entries == null)
                {
                    return Array.Empty<SupplementalDanmakuEntry>();
                }
                return new List<SupplementalDanmakuEntry>(entries);
            }
        }

        public static IReadOnlyList<SupplementalDanmakuEntry> GetOpeningEntries(bool directCallOnly)
        {
            IReadOnlyList<SupplementalDanmakuEntry> source = GetEntries(
                SupplementalDanmakuPoolKind.OpeningWait);
            if (!directCallOnly)
            {
                return source;
            }

            var result = new List<SupplementalDanmakuEntry>();
            for (int i = 0; i < source.Count; i++)
            {
                SupplementalDanmakuEntry entry = source[i];
                if (entry != null
                    && (string.Equals(entry.Intent, "open_door", StringComparison.Ordinal)
                        || string.Equals(entry.Intent, "urge_start", StringComparison.Ordinal)))
                {
                    result.Add(entry);
                }
            }
            return result.Count > 0 ? result : source;
        }

        private static async Task LoadAsync()
        {
            try
            {
                await DanmakuRepository.EnsureLoadedAsync();

                var loaded = new Dictionary<SupplementalDanmakuPoolKind, IReadOnlyList<SupplementalDanmakuEntry>>
                {
                    [SupplementalDanmakuPoolKind.OpeningWait] = await LoadPoolAsync(
                        "supplemental_opening_wait_v2.json"),
                    [SupplementalDanmakuPoolKind.SessionEnd] = await LoadPoolAsync(
                        "supplemental_session_end_v2.json"),
                    [SupplementalDanmakuPoolKind.KillPraise] = await LoadPoolAsync(
                        "supplemental_kill_praise_v2.json"),
                    [SupplementalDanmakuPoolKind.DeathQuestion] = await LoadPoolAsync(
                        "supplemental_death_question_v2.json"),
                    [SupplementalDanmakuPoolKind.DeathFlame] = await LoadPoolAsync(
                        "supplemental_death_flame_source_v2.json")
                };

                foreach (KeyValuePair<SupplementalDanmakuPoolKind, IReadOnlyList<SupplementalDanmakuEntry>> pair in loaded)
                {
                    if (pair.Value == null || pair.Value.Count == 0)
                    {
                        throw new InvalidOperationException(
                            "Supplemental danmaku pool is empty: " + pair.Key);
                    }
                }

                lock (SyncRoot)
                {
                    _pools = loaded;
                    _isAvailable = true;
                }

                App.Log(
                    "SupplementalDanmakuPoolRepository loaded: opening=140, end=10, kill=140, death_question=40, death_flame=15");
            }
            catch (Exception ex)
            {
                lock (SyncRoot)
                {
                    _pools = new Dictionary<SupplementalDanmakuPoolKind, IReadOnlyList<SupplementalDanmakuEntry>>();
                    _isAvailable = false;
                }
                App.Log("SupplementalDanmakuPoolRepository.LoadAsync failed: " + ex.Message);
            }
            finally
            {
                lock (SyncRoot)
                {
                    _isLoadCompleted = true;
                }
            }
        }

        private static async Task<IReadOnlyList<SupplementalDanmakuEntry>> LoadPoolAsync(
            string fileName)
        {
            StorageFile file = await StorageFile.GetFileFromApplicationUriAsync(
                new Uri("ms-appx:///Danmaku/EventFitAnnotationV2/" + fileName));
            string jsonText = await FileIO.ReadTextAsync(file);
            JsonObject root;
            if (!JsonObject.TryParse(jsonText, out root)
                || !root.ContainsKey("messages")
                || root.GetNamedValue("messages").ValueType != JsonValueType.Array)
            {
                throw new InvalidOperationException(
                    "Invalid supplemental danmaku JSON: " + fileName);
            }

            JsonArray messages = root.GetNamedArray("messages");
            var result = new List<SupplementalDanmakuEntry>(messages.Count);
            var seenIds = new HashSet<string>(StringComparer.Ordinal);
            var seenTexts = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < messages.Count; i++)
            {
                if (messages[i].ValueType != JsonValueType.Object)
                {
                    throw new InvalidOperationException(
                        "Supplemental danmaku entry is not an object: " + fileName + " #" + (i + 1));
                }

                JsonObject item = messages[i].GetObject();
                string id = ReadOptionalString(item, "id");
                string text = ReadOptionalString(item, "text");
                string sourceText = ReadOptionalString(item, "source_text");
                int sourceIndex = ReadOptionalPositiveInteger(item, "source_index");

                if (sourceIndex > 0)
                {
                    string originalText;
                    if (!DanmakuRepository.TryGetByIndex(sourceIndex, out originalText)
                        || string.IsNullOrWhiteSpace(originalText)
                        || !string.Equals(originalText, sourceText, StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException(
                            "Supplemental source reference mismatch: " + fileName + " #" + sourceIndex);
                    }
                    text = originalText;
                }

                if (string.IsNullOrWhiteSpace(id)
                    || string.IsNullOrWhiteSpace(text)
                    || !seenIds.Add(id)
                    || !seenTexts.Add(text))
                {
                    throw new InvalidOperationException(
                        "Invalid or duplicate supplemental entry: " + fileName + " #" + (i + 1));
                }

                result.Add(new SupplementalDanmakuEntry
                {
                    Id = id,
                    Text = text,
                    Intent = ReadOptionalString(item, "intent"),
                    Family = ReadOptionalString(item, "family"),
                    Phase = ReadOptionalString(item, "phase"),
                    SourceIndex = sourceIndex
                });
            }

            return result;
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

        private static int ReadOptionalPositiveInteger(JsonObject item, string propertyName)
        {
            if (item == null
                || !item.ContainsKey(propertyName)
                || item.GetNamedValue(propertyName).ValueType != JsonValueType.Number)
            {
                return 0;
            }

            double rawValue = item.GetNamedNumber(propertyName);
            int value = (int)rawValue;
            return value > 0 && Math.Abs(rawValue - value) < 0.001 ? value : 0;
        }
    }
}

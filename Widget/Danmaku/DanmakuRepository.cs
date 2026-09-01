using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Data.Json;
using Windows.Storage;

namespace KillConfirmGameBar.Danmaku
{
    public static class DanmakuRepository
    {
        private static readonly object SyncRoot = new object();
        private static IReadOnlyList<string> _kill = new string[0];
        private static IReadOnlyList<string> _death = new string[0];
        private static IReadOnlyList<string> _general = new string[0];
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

        public static bool TryGetByIndex(
            string section,
            int oneBasedIndex,
            out string text)
        {
            text = null;
            if (oneBasedIndex <= 0)
            {
                return false;
            }

            IReadOnlyList<string> source;
            lock (SyncRoot)
            {
                switch ((section ?? string.Empty).Trim().ToLowerInvariant())
                {
                    case "kill":
                        source = _kill;
                        break;
                    case "death":
                        source = _death;
                        break;
                    case "general":
                        source = _general;
                        break;
                    default:
                        return false;
                }

                int zeroBasedIndex = oneBasedIndex - 1;
                if (zeroBasedIndex < 0 || zeroBasedIndex >= source.Count)
                {
                    return false;
                }
                text = source[zeroBasedIndex];
            }

            return !string.IsNullOrWhiteSpace(text);
        }

        private static async Task LoadAsync()
        {
            try
            {
                StorageFile file = await StorageFile.GetFileFromApplicationUriAsync(
                    new Uri("ms-appx:///Danmaku/6657_memes.json"));
                string jsonText = await FileIO.ReadTextAsync(file);
                JsonObject root;
                if (!JsonObject.TryParse(jsonText, out root))
                {
                    throw new InvalidOperationException("6657_memes.json is not a JSON object.");
                }

                IReadOnlyList<string> kill = ReadSection(root, "kill");
                IReadOnlyList<string> death = ReadSection(root, "death");
                IReadOnlyList<string> general = ReadSection(root, "general");
                if (kill.Count == 0 || death.Count == 0 || general.Count == 0)
                {
                    throw new InvalidOperationException("6657_memes.json contains an empty required section.");
                }

                lock (SyncRoot)
                {
                    _kill = kill;
                    _death = death;
                    _general = general;
                }
            }
            catch (Exception ex)
            {
                // Strict source rule: if the 6657 library cannot be loaded,
                // leave every section empty and emit no danmaku.
                App.Log("DanmakuRepository.LoadAsync failed: " + ex.Message);
            }
        }

        private static IReadOnlyList<string> ReadSection(JsonObject root, string section)
        {
            var result = new List<string>();
            if (!root.ContainsKey(section)
                || root.GetNamedValue(section).ValueType != JsonValueType.Array)
            {
                return result;
            }

            JsonArray values = root.GetNamedArray(section);
            for (int i = 0; i < values.Count; i++)
            {
                IJsonValue value = values[i];
                if (value.ValueType == JsonValueType.String)
                {
                    // Preserve the exact 6657 array position and text. Empty
                    // entries stay in place and simply fail reference validation.
                    result.Add(value.GetString());
                }
                else
                {
                    // A non-string entry also occupies its original sequence number.
                    result.Add(null);
                }
            }
            return result;
        }
    }
}

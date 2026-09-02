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
        private static IReadOnlyList<string> _messages = new string[0];
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

        public static bool TryGetByIndex(int oneBasedIndex, out string text)
        {
            text = null;
            if (oneBasedIndex <= 0)
            {
                return false;
            }

            lock (SyncRoot)
            {
                int zeroBasedIndex = oneBasedIndex - 1;
                if (zeroBasedIndex < 0 || zeroBasedIndex >= _messages.Count)
                {
                    return false;
                }
                text = _messages[zeroBasedIndex];
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
                JsonArray root;
                if (!JsonArray.TryParse(jsonText, out root))
                {
                    throw new InvalidOperationException("6657_memes.json is not a JSON array.");
                }

                IReadOnlyList<string> messages = ReadMessages(root);
                if (messages.Count == 0)
                {
                    throw new InvalidOperationException("6657_memes.json is empty.");
                }

                lock (SyncRoot)
                {
                    _messages = messages;
                }
            }
            catch (Exception ex)
            {
                // Strict source rule: if the 6657 library cannot be loaded,
                // leave the library empty and emit no danmaku.
                App.Log("DanmakuRepository.LoadAsync failed: " + ex.Message);
            }
        }

        private static IReadOnlyList<string> ReadMessages(JsonArray values)
        {
            var result = new List<string>();
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

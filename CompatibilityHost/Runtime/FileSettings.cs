using KillConfirmCompatibility.Contracts;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace KillConfirmCompatibility.Desktop.Runtime
{
    // All processes merge individual keys under the same lock; unrelated writes
    // cannot replace one another. The file is readable without package identity.
    internal sealed class FileSettings : IDictionary<string, object>
    {
        private readonly string _path;
        private readonly IDictionary<string, object> _legacy;
        private Dictionary<string, object> _cache;
        private DateTimeOffset _nextRead;
        public FileSettings(string path, IDictionary<string, object> legacy = null) { _path = path; _legacy = legacy; }
        private Dictionary<string, object> Snapshot()
        {
            if (_cache != null && DateTimeOffset.UtcNow < _nextRead) return _cache;
            var values = _legacy == null ? new Dictionary<string, object>() : new Dictionary<string, object>(_legacy);
            foreach (var pair in SharedSettingsFile.Read(_path))
                try { values[pair.Key] = pair.Value.ToObject(); } catch (FormatException) { } catch (InvalidDataException) { }
            _cache = values; _nextRead = DateTimeOffset.UtcNow.AddMilliseconds(200); return values;
        }
        public object this[string key]
        {
            get { var values = Snapshot(); return values.TryGetValue(key, out var value) ? value : null; }
            set { var encoded = SettingValue.From(value); if (value != null && encoded == null) throw new ArgumentException("Unsupported setting type."); SharedSettingsFile.Merge(_path, new Dictionary<string, SettingValue> { [key] = encoded }); _cache = null; }
        }
        public ICollection<string> Keys => Snapshot().Keys;
        public ICollection<object> Values => Snapshot().Values;
        public int Count => Snapshot().Count;
        public bool IsReadOnly => false;
        public void Add(string key, object value) { if (ContainsKey(key)) throw new ArgumentException("Key exists."); this[key] = value; }
        public void Add(KeyValuePair<string, object> value) => Add(value.Key, value.Value);
        public void Clear() { SharedSettingsFile.Merge(_path, Snapshot().ToDictionary(p => p.Key, p => (SettingValue)null)); _cache = null; }
        public bool Contains(KeyValuePair<string, object> value) => TryGetValue(value.Key, out var actual) && Equals(actual, value.Value);
        public bool ContainsKey(string key) => Snapshot().ContainsKey(key);
        public void CopyTo(KeyValuePair<string, object>[] array, int index) => ((ICollection<KeyValuePair<string, object>>)Snapshot()).CopyTo(array, index);
        public IEnumerator<KeyValuePair<string, object>> GetEnumerator() => Snapshot().GetEnumerator();
        public bool Remove(string key) { bool exists = ContainsKey(key); this[key] = null; return exists; }
        public bool Remove(KeyValuePair<string, object> value) => Contains(value) && Remove(value.Key);
        public bool TryGetValue(string key, out object value) => Snapshot().TryGetValue(key, out value);
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}

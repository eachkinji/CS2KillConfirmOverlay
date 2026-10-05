using System.Collections;
using System.Collections.Generic;

namespace KillConfirmCompatibility.Desktop.Runtime
{
    internal sealed class SafeSettings : IDictionary<string, object>
    {
        private readonly IDictionary<string, object> _values;
        public SafeSettings(IDictionary<string, object> values) { _values = values; }
        // UWP settings callers expect an absent value to be null. Preserve that
        // contract with the desktop WinRT projection as well.
        public object this[string key] { get => _values.TryGetValue(key, out object value) ? value : null; set => _values[key] = value; }
        public ICollection<string> Keys => _values.Keys;
        public ICollection<object> Values => _values.Values;
        public int Count => _values.Count;
        public bool IsReadOnly => _values.IsReadOnly;
        public void Add(string key, object value) => _values.Add(key, value);
        public void Add(KeyValuePair<string, object> value) => _values.Add(value);
        public void Clear() => _values.Clear();
        public bool Contains(KeyValuePair<string, object> value) => _values.Contains(value);
        public bool ContainsKey(string key) => _values.ContainsKey(key);
        public void CopyTo(KeyValuePair<string, object>[] array, int index) => _values.CopyTo(array, index);
        public IEnumerator<KeyValuePair<string, object>> GetEnumerator() => _values.GetEnumerator();
        public bool Remove(string key) => _values.Remove(key);
        public bool Remove(KeyValuePair<string, object> value) => _values.Remove(value);
        public bool TryGetValue(string key, out object value) => _values.TryGetValue(key, out value);
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}

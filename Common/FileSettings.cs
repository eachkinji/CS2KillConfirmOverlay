using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading;
namespace KillConfirmCompatibility.Contracts
{
    [DataContract]
    public sealed class SettingScalar
    {
        [DataMember] public string Kind { get; set; }
        [DataMember] public string Text { get; set; }
        public static SettingScalar From(object value)
        {
            string kind = value is bool ? "bool" : value is int ? "int" : value is long ? "long" : value is float ? "float" : value is double ? "double" : value is string ? "string" : null;
            if (kind == null) throw new ArgumentException("Unsupported setting value: " + value?.GetType().Name);
            return new SettingScalar { Kind=kind, Text=Convert.ToString(value,CultureInfo.InvariantCulture) };
        }
        public object Value { get { switch(Kind) { case "bool": return bool.Parse(Text); case "int": return int.Parse(Text,CultureInfo.InvariantCulture); case "long": return long.Parse(Text,CultureInfo.InvariantCulture); case "float": return float.Parse(Text,CultureInfo.InvariantCulture); case "double": return double.Parse(Text,CultureInfo.InvariantCulture); default: return Text; } } }
    }
    // Ordinary JSON, with per-key merges and the same atomic writes as layouts.
    // No ApplicationData, AppX service, registry registration or package identity.
    public sealed class FileSettings : IDictionary<string,object>
    {
        public const string FileName="settings.json";
        private readonly string _path;
        private readonly object _gate=new object();
        private Dictionary<string,SettingScalar> _snapshot=new Dictionary<string,SettingScalar>();
        private DateTime _lastRead;
        public FileSettings(string path) { _path=path; Refresh(true); }
        private Dictionary<string,SettingScalar> Read() => DisplayFiles.Read<Dictionary<string,SettingScalar>>(_path) ?? new Dictionary<string,SettingScalar>();
        private void Refresh(bool force=false)
        {
            if (!force && DateTime.UtcNow-_lastRead < TimeSpan.FromMilliseconds(100)) return;
            _snapshot=Read(); _lastRead=DateTime.UtcNow;
        }
        private void Change(Action<Dictionary<string,SettingScalar>> action)
        {
            lock (_gate)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_path));
                FileStream fileGate=null;
                for(int attempt=0;;attempt++)
                    try { fileGate=new FileStream(_path+".lock",FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None); break; }
                    catch(IOException) when(attempt<200) { Thread.Sleep(10); }
                using(fileGate)
                {
                    var current=Read(); action(current); DisplayFiles.Write(_path,current);
                    _snapshot=current; _lastRead=DateTime.UtcNow;
                }
            }
        }
        public object this[string key]
        {
            get { lock(_gate) { Refresh(); return _snapshot.TryGetValue(key,out var scalar) ? scalar.Value : null; } }
            set { var scalar=value==null ? null : SettingScalar.From(value); Change(values => { if(scalar==null) values.Remove(key); else values[key]=scalar; }); }
        }
        public ICollection<string> Keys { get { lock(_gate) { Refresh(); return _snapshot.Keys.ToArray(); } } }
        public ICollection<object> Values { get { lock(_gate) { Refresh(); return _snapshot.Values.Select(v=>v.Value).ToArray(); } } }
        public int Count => Keys.Count;
        public bool IsReadOnly => false;
        public void Add(string key,object value) { var scalar=SettingScalar.From(value); Change(values=>values.Add(key,scalar)); }
        public bool ContainsKey(string key) => Keys.Contains(key);
        public bool Remove(string key) { bool removed=false; Change(values=>removed=values.Remove(key)); return removed; }
        public bool TryGetValue(string key,out object value) { value=this[key]; return value!=null; }
        public void Add(KeyValuePair<string,object> item) => Add(item.Key,item.Value);
        public void Clear() => Change(values=>values.Clear());
        public bool Contains(KeyValuePair<string,object> item) => Equals(this[item.Key],item.Value);
        public void CopyTo(KeyValuePair<string,object>[] array,int index) { foreach(var item in this) array[index++]=item; }
        public bool Remove(KeyValuePair<string,object> item) => Contains(item) && Remove(item.Key);
        public IEnumerator<KeyValuePair<string,object>> GetEnumerator()
        {
            lock(_gate) { Refresh(); return _snapshot.Select(p=>new KeyValuePair<string,object>(p.Key,p.Value.Value)).ToArray().AsEnumerable().GetEnumerator(); }
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization;

namespace KillConfirmCompatibility.Contracts
{
    [DataContract]
    public sealed class SettingValue
    {
        [DataMember] public string Kind { get; set; }
        [DataMember] public string Value { get; set; }
        public static SettingValue From(object value)
        {
            if (value is string) return new SettingValue { Kind = "string", Value = (string)value };
            if (value is bool) return new SettingValue { Kind = "bool", Value = (bool)value ? "true" : "false" };
            if (value is int) return new SettingValue { Kind = "int", Value = ((int)value).ToString(CultureInfo.InvariantCulture) };
            if (value is long) return new SettingValue { Kind = "long", Value = ((long)value).ToString(CultureInfo.InvariantCulture) };
            if (value is double) return new SettingValue { Kind = "double", Value = ((double)value).ToString("R", CultureInfo.InvariantCulture) };
            return null;
        }
        public object ToObject()
        {
            switch (Kind)
            {
                case "string": return Value;
                case "bool": return bool.Parse(Value);
                case "int": return int.Parse(Value, CultureInfo.InvariantCulture);
                case "long": return long.Parse(Value, CultureInfo.InvariantCulture);
                case "double": return double.Parse(Value, CultureInfo.InvariantCulture);
                default: throw new InvalidDataException("Unknown setting type.");
            }
        }
        public bool Same(SettingValue other) => other != null && Kind == other.Kind && Value == other.Value;
    }
    public static class SharedSettingsFile
    {
        public const string FileName = "desktop-settings.json";
        public static Dictionary<string, SettingValue> Read(string path) =>
            DisplayFiles.Read<Dictionary<string, SettingValue>>(path) ?? new Dictionary<string, SettingValue>();
        public static void Merge(string path, IDictionary<string, SettingValue> changes)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using (DisplayFiles.AcquireFileLock(path + ".lock"))
            {
                var settings = Read(path);
                foreach (var pair in changes)
                    if (pair.Value == null) settings.Remove(pair.Key); else settings[pair.Key] = pair.Value;
                DisplayFiles.Write(path, settings);
            }
        }
    }
    [DataContract]
    public sealed class WidgetStatusSnapshot
    {
        [DataMember] public long Timestamp { get; set; }
        [DataMember] public bool Active { get; set; }
        [DataMember] public bool Pinned { get; set; }
        [DataMember] public bool ClickThrough { get; set; }
    }
}

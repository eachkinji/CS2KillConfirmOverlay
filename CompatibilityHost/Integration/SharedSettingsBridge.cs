using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using KillConfirmCompatibility.Contracts;
using KillConfirmGameBar.Services;
using Windows.Storage;
using Windows.UI.Xaml;

namespace KillConfirmGameBar.Features.CompatibilityDisplay
{
    // The optional widget and the unpackaged desktop control panel use the same
    // ordinary data files. Only changed keys are merged, so a heartbeat cannot
    // overwrite a concurrent edit made by the other application.
    internal static class SharedSettingsBridge
    {
        private static DispatcherTimer _timer;
        private static Dictionary<string, SettingValue> _lastLocal, _lastShared;
        internal static event EventHandler SettingsChanged;
        private static string PathName => Path.Combine(ApplicationData.Current.LocalFolder.Path, SharedSettingsFile.FileName);
        internal static void Start()
        {
            if (_timer != null) return;
            try
            {
                var local = Capture();
                var shared = SharedSettingsFile.Read(PathName);
                foreach (var pair in shared) Apply(pair.Key, pair.Value);
                SharedSettingsFile.Merge(PathName, local.Where(p => !shared.ContainsKey(p.Key)).ToDictionary(p => p.Key, p => p.Value));
                _lastLocal = Capture(); _lastShared = SharedSettingsFile.Read(PathName);
                _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
                _timer.Tick += (s, e) => Synchronize(); _timer.Start();
            }
            catch (Exception error) { App.Log("Desktop settings bridge startup: " + error.Message); }
        }
        internal static void Synchronize()
        {
            try
            {
                var local = Capture(); var shared = SharedSettingsFile.Read(PathName);
                var changes = new Dictionary<string, SettingValue>();
                foreach (var pair in local)
                    if (!_lastLocal.TryGetValue(pair.Key, out var previous) || !pair.Value.Same(previous)) changes[pair.Key] = pair.Value;
                foreach (var key in _lastLocal.Keys) if (!local.ContainsKey(key)) changes[key] = null;
                bool imported = false;
                foreach (var pair in shared)
                    if (!changes.ContainsKey(pair.Key) && (!_lastShared.TryGetValue(pair.Key, out var previous) || !pair.Value.Same(previous)))
                    { Apply(pair.Key, pair.Value); imported = true; }
                foreach (var key in _lastShared.Keys)
                    if (!shared.ContainsKey(key) && !changes.ContainsKey(key)) { ApplicationData.Current.LocalSettings.Values.Remove(key); imported = true; }
                if (changes.Count != 0) SharedSettingsFile.Merge(PathName, changes);
                _lastLocal = Capture(); _lastShared = SharedSettingsFile.Read(PathName);
                if (imported) SettingsChanged?.Invoke(null, EventArgs.Empty);
            }
            catch (Exception error) { App.Log("Desktop settings bridge: " + error.Message); }
        }
        private static Dictionary<string, SettingValue> Capture() => ApplicationData.Current.LocalSettings.Values
            .Select(p => new { p.Key, Value = SettingValue.From(p.Value) }).Where(p => p.Value != null).ToDictionary(p => p.Key, p => p.Value);
        private static void Apply(string key, SettingValue value)
        {
            object decoded = value.ToObject();
            if (key == GameStyleService.SettingKey) GameStyleService.Current = GameStyleService.FromKey(decoded as string);
            else ApplicationData.Current.LocalSettings.Values[key] = decoded;
        }
    }
}

using KillConfirmCompatibility.Contracts;
using KillConfirmCompatibility.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace KillConfirmCompatibility.Desktop.Runtime
{
    // DesktopData is authoritative and survives uninstalling the optional
    // widget. Its sandbox receives a copy of selected data and imported packs.
    // Settings are merged per key in both directions, never by whole snapshot.
    internal static class ProfileMirror
    {
        private static Dictionary<string, SettingValue> _lastDesktop = new(), _lastWidget = new();
        private static string _lastConfig, _lastWidgetConfig;
        private static DateTimeOffset _nextSync;
        private static DateTime _catalogTime;
        private static DateTime _widgetCatalogTime;
        private static bool _initialized;
        internal static void Initialize()
        {
            if (_initialized) return; _initialized = true;
            Directory.CreateDirectory(DesktopEnvironment.DataRoot);
            if (DesktopEnvironment.ProfileRoot != null && DesktopEnvironment.WidgetRootOverride == null) return;
            string root = DesktopEnvironment.DataRoot, widget = DesktopEnvironment.WidgetDataRoot;
            string migration = Path.Combine(root, "migration-complete.txt");
            if (!File.Exists(migration))
            {
                foreach (string directory in new[] { "Packs", DisplayFiles.FolderName, "DoubaoImages", "DoubaoAudio", "DagoujiaoImages", "DagoujiaoAudio", "BombAudio" })
                    try { if (Directory.Exists(Path.Combine(widget, directory))) CopyTree(Path.Combine(widget, directory), Path.Combine(root, directory), false); }
                    catch (Exception error) { App.Log("Optional data migration (" + directory + "): " + error.Message); }
                foreach (string file in new[] { "pack-catalog.json", "service-auth-token.txt", "widget_port.txt", "port_search.txt", SharedSettingsFile.FileName })
                    try { if (File.Exists(Path.Combine(widget, file)) && !File.Exists(Path.Combine(root, file))) File.Copy(Path.Combine(widget, file), Path.Combine(root, file)); }
                    catch (Exception error) { App.Log("Optional data migration (" + file + "): " + error.Message); }
                try
                {
                    // Optional migration only. A missing package/store/service
                    // is handled here and never blocks standalone startup.
                    var old = DesktopEnvironment.WidgetRootOverride == null ? Windows.Management.Core.ApplicationDataManager.CreateForPackageFamily(DesktopEnvironment.PackageFamily) : null;
                    var existing = SharedSettingsFile.Read(Path.Combine(root, SharedSettingsFile.FileName));
                    var changes = old == null ? new Dictionary<string, SettingValue>() : old.LocalSettings.Values.Where(p => !existing.ContainsKey(p.Key)).Select(p => new { p.Key, Value = SettingValue.From(p.Value) }).Where(p => p.Value != null).ToDictionary(p => p.Key, p => p.Value);
                    SharedSettingsFile.Merge(Path.Combine(root, SharedSettingsFile.FileName), changes);
                    File.WriteAllText(migration, "Migrated existing settings.");
                }
                catch (Exception error) { App.Log("Optional settings migration: " + error.Message); }
                var migratedValues = SharedSettingsFile.Read(Path.Combine(root, SharedSettingsFile.FileName));
                var migratedPaths = migratedValues.Where(p => p.Value.Kind == "string" && p.Value.Value != null && p.Value.Value.StartsWith(widget + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    .ToDictionary(p => p.Key, p => SettingValue.From(Map(p.Value.Value, widget, root)));
                if (migratedPaths.Count != 0) SharedSettingsFile.Merge(Path.Combine(root, SharedSettingsFile.FileName), migratedPaths);
                var catalog = DisplayFiles.Read<PackCatalog>(Path.Combine(root, "pack-catalog.json"));
                if (catalog != null)
                {
                    foreach (var item in catalog.IconPacks) item.FolderPath = Map(item.FolderPath, widget, root);
                    foreach (var item in catalog.VoicePacks) item.FolderPath = Map(item.FolderPath, widget, root);
                    DisplayFiles.Write(Path.Combine(root, "pack-catalog.json"), catalog);
                }
                File.WriteAllText(migration, "Desktop data initialized.");
            }
            Synchronize();
        }
        internal static void Synchronize()
        {
            try { SynchronizeCore(); }
            catch (Exception error) { _nextSync = DateTimeOffset.UtcNow.AddSeconds(5); App.Log("Optional widget data mirror: " + error.Message); }
        }
        private static void SynchronizeCore()
        {
            if (DesktopStorage.TestDataRoot != null || (DesktopEnvironment.ProfileRoot != null && DesktopEnvironment.WidgetRootOverride == null) || DateTimeOffset.UtcNow < _nextSync) return;
            _nextSync = DateTimeOffset.UtcNow.AddSeconds(1);
            string root = DesktopEnvironment.DataRoot, widget = DesktopEnvironment.WidgetDataRoot;
            Directory.CreateDirectory(widget);
            string desktopSettings = Path.Combine(root, SharedSettingsFile.FileName), widgetSettings = Path.Combine(widget, SharedSettingsFile.FileName);
            var local = SharedSettingsFile.Read(desktopSettings); var remote = SharedSettingsFile.Read(widgetSettings).ToDictionary(p => p.Key, p => MapValue(p.Value, widget, root));
            var imported = new Dictionary<string, SettingValue>();
            foreach (var pair in remote)
                if ((!_lastWidget.TryGetValue(pair.Key, out var previous) || !pair.Value.Same(previous)) &&
                    (!local.TryGetValue(pair.Key, out var actual) || (_lastDesktop.TryGetValue(pair.Key, out var old) && actual.Same(old)))) imported[pair.Key] = pair.Value;
            foreach (var key in _lastWidget.Keys)
                if (!remote.ContainsKey(key) && local.TryGetValue(key, out var actual) && _lastDesktop.TryGetValue(key, out var old) && actual.Same(old)) imported[key] = null;
            if (imported.Count != 0)
            {
                foreach (string directory in new[] { "DoubaoImages", "DoubaoAudio", "DagoujiaoImages", "DagoujiaoAudio", "BombAudio" })
                    if (Directory.Exists(Path.Combine(widget, directory))) CopyTree(Path.Combine(widget, directory), Path.Combine(root, directory), true);
                SharedSettingsFile.Merge(desktopSettings, imported); local = SharedSettingsFile.Read(desktopSettings);
            }
            var outgoing = local.Where(p => !remote.TryGetValue(p.Key, out var old) || !p.Value.Same(old)).ToDictionary(p => p.Key, p => p.Value);
            foreach (var key in _lastDesktop.Keys)
                if (!local.ContainsKey(key)) outgoing[key] = null;
            if (outgoing.Count != 0)
            {
                foreach (string directory in new[] { "DoubaoImages", "DoubaoAudio", "DagoujiaoImages", "DagoujiaoAudio", "BombAudio" })
                    if (Directory.Exists(Path.Combine(root, directory))) CopyTree(Path.Combine(root, directory), Path.Combine(widget, directory), true);
                SharedSettingsFile.Merge(widgetSettings, outgoing.ToDictionary(p => p.Key, p => MapValue(p.Value, root, widget)));
            }
            _lastDesktop = local; _lastWidget = SharedSettingsFile.Read(widgetSettings).ToDictionary(p => p.Key, p => MapValue(p.Value, widget, root));
            string config = Path.Combine(root, DisplayFiles.FolderName, DisplayFiles.ConfigurationName), widgetConfig = Path.Combine(widget, DisplayFiles.FolderName, DisplayFiles.ConfigurationName);
            string localConfig = File.Exists(config) ? File.ReadAllText(config) : null, remoteConfig = File.Exists(widgetConfig) ? File.ReadAllText(widgetConfig) : null;
            if (remoteConfig != null && remoteConfig != _lastWidgetConfig && localConfig == _lastConfig)
            {
                var change = DisplayFiles.Read<DisplayConfiguration>(widgetConfig);
                if (change != null) { DisplayFiles.Write(config, change); localConfig = File.ReadAllText(config); }
            }
            if (localConfig != null && localConfig != remoteConfig)
            {
                var change = DisplayFiles.Read<DisplayConfiguration>(config);
                if (change != null) DisplayFiles.Write(widgetConfig, change);
            }
            _lastConfig = File.Exists(config) ? File.ReadAllText(config) : null; _lastWidgetConfig = File.Exists(widgetConfig) ? File.ReadAllText(widgetConfig) : null;
            foreach (string file in new[] { "service-auth-token.txt", "widget_port.txt", "port_search.txt", "desktop-location.txt" })
            {
                string source = Path.Combine(root, file), target = Path.Combine(widget, file);
                if (File.Exists(source) && (!File.Exists(target) || File.ReadAllText(source) != File.ReadAllText(target))) File.Copy(source, target, true);
            }
            string catalogPath = Path.Combine(root, "pack-catalog.json"); DateTime catalogTime = File.GetLastWriteTimeUtc(catalogPath);
            string widgetCatalogPath = Path.Combine(widget, "pack-catalog.json"); DateTime widgetCatalogTime = File.GetLastWriteTimeUtc(widgetCatalogPath);
            if (_widgetCatalogTime != default && widgetCatalogTime != _widgetCatalogTime && catalogTime == _catalogTime && File.Exists(widgetCatalogPath))
            {
                if (Directory.Exists(Path.Combine(widget, "Packs"))) CopyTree(Path.Combine(widget, "Packs"), Path.Combine(root, "Packs"), true);
                var incoming = DisplayFiles.Read<PackCatalog>(widgetCatalogPath);
                if (incoming != null)
                {
                    foreach (var item in incoming.IconPacks) item.FolderPath = Map(item.FolderPath, widget, root);
                    foreach (var item in incoming.VoicePacks) item.FolderPath = Map(item.FolderPath, widget, root);
                    DisplayFiles.Write(catalogPath, incoming); catalogTime = File.GetLastWriteTimeUtc(catalogPath);
                }
            }
            if (File.Exists(catalogPath) && (_catalogTime != catalogTime || !File.Exists(Path.Combine(widget, "pack-catalog.json"))))
            {
                if (Directory.Exists(Path.Combine(root, "Packs"))) CopyTree(Path.Combine(root, "Packs"), Path.Combine(widget, "Packs"), true);
                var catalog = DisplayFiles.Read<PackCatalog>(catalogPath);
                if (catalog != null)
                {
                    foreach (var item in catalog.IconPacks) item.FolderPath = Map(item.FolderPath, root, widget);
                    foreach (var item in catalog.VoicePacks) item.FolderPath = Map(item.FolderPath, root, widget);
                    DisplayFiles.Write(Path.Combine(widget, "pack-catalog.json"), catalog); _catalogTime = catalogTime;
                }
            }
            _widgetCatalogTime = File.GetLastWriteTimeUtc(widgetCatalogPath);
        }
        private static string Map(string path, string from, string to) => path != null && path.StartsWith(from + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ? to + path.Substring(from.Length) : path;
        private static SettingValue MapValue(SettingValue value, string from, string to) => value?.Kind == "string" ? SettingValue.From(Map(value.Value, from, to)) : value;
        private static void CopyTree(string source, string destination, bool update)
        {
            if ((File.GetAttributes(source) & FileAttributes.ReparsePoint) != 0) throw new IOException("数据目录包含链接，无法自动迁移。");
            Directory.CreateDirectory(destination);
            foreach (string sourceFile in Directory.GetFiles(source))
            {
                if ((File.GetAttributes(sourceFile) & FileAttributes.ReparsePoint) != 0) continue;
                string target = Path.Combine(destination, Path.GetFileName(sourceFile));
                if (!File.Exists(target) || (update && (new FileInfo(sourceFile).Length != new FileInfo(target).Length || File.GetLastWriteTimeUtc(sourceFile) != File.GetLastWriteTimeUtc(target)))) File.Copy(sourceFile, target, true);
            }
            foreach (string folder in Directory.GetDirectories(source)) CopyTree(folder, Path.Combine(destination, Path.GetFileName(folder)), update);
        }
    }
}

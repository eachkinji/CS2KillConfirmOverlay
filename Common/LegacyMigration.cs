using System;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Text.Json.Nodes;
namespace KillConfirmCompatibility.Contracts
{
    // The widget cache is never authoritative. Import the old package data once.
    public static class LegacyMigration
    {
        public static void Import()
        {
            if(Environment.GetEnvironmentVariable("KILLCONFIRM_DATA_ROOT")!=null) return;
            string local=Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            ImportProfile(RuntimePaths.DataRoot,Path.Combine(local,"Packages",RuntimePaths.PackageFamily,"LocalState"),Path.Combine(local,"KillConfirmOverlay","DesktopData"),true);
        }
        public static void ImportProfile(string root,string old,string formerDesktop,bool readPackageSettings)
        {
            Directory.CreateDirectory(root);
            string done=Path.Combine(Path.GetDirectoryName(root),"migration-v49-complete.txt");
            if(File.Exists(done)) return;

            try
            {
                // Read a previous desktop profile only as a one-time data migration;
                // its rejected control-panel program is not retained or built.
                var settings=new FileSettings(Path.Combine(root,FileSettings.FileName));
                string desktopSettings=Path.Combine(formerDesktop,"desktop-settings.json");
                bool desktopImported=false;
                if(File.Exists(desktopSettings))
                {
                    var scalars=JsonNode.Parse(File.ReadAllText(desktopSettings)).AsObject();
                    foreach(var pair in scalars)
                        if(!settings.ContainsKey(pair.Key)) settings[pair.Key]=new SettingScalar {Kind=pair.Value["Kind"].GetValue<string>(),Text=Convert.ToString(pair.Value["Value"]?.GetValue<string>(),CultureInfo.InvariantCulture)}.Value;
                    desktopImported=true;
                }
                // Do not copy runtime logs, video previews, or the widget's new cache.
                foreach(string sourceRoot in new[]{formerDesktop,old})
                {
                foreach(string name in new[]{"Packs","CompatibilityDisplay","DoubaoImages","DoubaoAudio","DagoujiaoImages","DagoujiaoAudio","BombAudio"})
                {
                    string source=Path.Combine(sourceRoot,name);
                    if(!Directory.Exists(source)) continue;
                    foreach(string file in Directory.EnumerateFiles(source,"*",SearchOption.AllDirectories))
                    {
                        string target=Path.Combine(root,Path.GetRelativePath(sourceRoot,file));
                        Directory.CreateDirectory(Path.GetDirectoryName(target));
                        if(!File.Exists(target)) File.Copy(file,target);
                    }
                }
                foreach(string name in new[]{"pack-catalog.json","service-auth-token.txt","widget_port.txt","port_search.txt"})
                    if(File.Exists(Path.Combine(sourceRoot,name)) && !File.Exists(Path.Combine(root,name))) File.Copy(Path.Combine(sourceRoot,name),Path.Combine(root,name));
                }
                bool settingsImported=desktopImported || !Directory.Exists(old);
                if(readPackageSettings)
                try
                {
                    var legacy=Windows.Management.Core.ApplicationDataManager.CreateForPackageFamily(RuntimePaths.PackageFamily);
                    foreach(var pair in legacy.LocalSettings.Values)
                        if(!settings.ContainsKey(pair.Key)) { try { settings[pair.Key]=pair.Value; } catch(ArgumentException) { } }
                    settingsImported=true;
                }
                catch { /* Optional AppX access; ordinary startup continues. Retry only this unfinished migration. */ }
                foreach(string sourceRoot in new[]{old,formerDesktop})
                {
                foreach(var pair in settings.ToArray())
                    if(pair.Value is string text && text.StartsWith(sourceRoot+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)) settings[pair.Key]=root+text.Substring(sourceRoot.Length);
                string catalogPath=Path.Combine(root,"pack-catalog.json");
                if(File.Exists(catalogPath))
                {
                    var catalog=JsonNode.Parse(File.ReadAllText(catalogPath));
                    foreach(string group in new[]{"IconPacks","VoicePacks"})
                        if(catalog[group] is JsonArray items) foreach(var item in items)
                            if(item?["FolderPath"]?.GetValue<string>() is string path && path.StartsWith(sourceRoot+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)) item["FolderPath"]=root+path.Substring(sourceRoot.Length);
                    File.WriteAllText(catalogPath,catalog.ToJsonString());
                }
                }
                if(settingsImported) File.WriteAllText(done,"Existing v49 data imported once. New settings remain authoritative.");
            }
            catch(Exception error) { File.AppendAllText(Path.Combine(root,"migration.log"),DateTimeOffset.Now+" "+error.Message+Environment.NewLine); }
        }
    }
}

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using KillConfirmCompatibility.Contracts;
using Windows.Storage;

namespace KillConfirmCompatibility.Services
{
    internal static class CalabiyauExternalAssetService
    {
        public static string Root => Path.Combine(ResourceLibraryPaths.Root, "calabiyau", "icon_packs");

        public static void RefreshCatalog(PackCatalog catalog)
        {
            var old = catalog.IconPacks.Where(p => CalabiyauPackage.IsExternalKey(p.Key)).ToDictionary(p => p.Key, StringComparer.OrdinalIgnoreCase);
            catalog.IconPacks.RemoveAll(p => CalabiyauPackage.IsExternalKey(p.Key));
            if (!Directory.Exists(Root)) return;
            foreach (string folder in Directory.EnumerateDirectories(Root))
            {
                if (Path.GetFileName(folder).Contains(".")) continue;
                try
                {
                    var m = CalabiyauPackage.Read(folder);
                    if (!File.Exists(Path.Combine(folder, "lower-1.json.gz"))) continue;
                    old.TryGetValue(m.Id, out IconPackItem previous);
                    catalog.IconPacks.Add(new IconPackItem {
                        Key = m.Id, DisplayName = CalabiyauPacks.Find(m.Id)?.DisplayName ?? m.DisplayName, FolderPath = folder,
                        IsBuiltIn = false, OwnsFolder = true, IsVisibleInWidget = previous?.IsVisibleInWidget ?? true
                    });
                }
                catch (Exception error) { App.Log("Calabiyau package discovery: " + error.Message); }
            }
        }

        public static async Task<string> InstallAsync(StorageFolder source)
        {
            string folder = await Task.Run(() => CalabiyauPackage.Install(source.Path, Root));
            await PackCatalogService.RefreshCalabiyauExternalPacksAsync();
            return CalabiyauPackage.Read(folder).DisplayName;
        }

        public static async Task<StorageFile> FileAsync(string key, string relative)
        {
            var item = await PackCatalogService.GetIconPackAsync(key);
            if (item == null || item.IsBuiltIn || string.IsNullOrWhiteSpace(item.FolderPath))
                throw new FileNotFoundException("请先导入所选卡拉彼丘图标包。");
            string root = Path.GetFullPath(item.FolderPath) + Path.DirectorySeparatorChar;
            string path = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
            if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Invalid package path.");
            return await StorageFile.GetFileFromPathAsync(path);
        }
    }
}

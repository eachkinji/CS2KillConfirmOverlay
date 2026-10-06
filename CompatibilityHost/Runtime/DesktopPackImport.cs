using KillConfirmCompatibility.Services;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;

namespace KillConfirmCompatibility.Desktop.Runtime
{
    internal static class DesktopPackImport
    {
        internal static async Task ImportAsync(string source, GameStyleMode game, bool voice)
        {
            string destination = Path.Combine(DesktopStorage.Current.LocalFolder.Path, "Packs", GameStyleService.ToStorageValue(game), voice ? "voice_packs" : "icon_packs", "desktop_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(destination);
            try
            {
                await Task.Run(() => {
                    if (Directory.Exists(source)) CopyDirectory(source, destination);
                    else
                    {
                        using var zip = ZipFile.OpenRead(source);
                        if (zip.Entries.Count > 20000 || zip.Entries.Sum(e => e.Length) > 2L * 1024 * 1024 * 1024) throw new InvalidDataException("资源包过大。");
                        foreach (var entry in zip.Entries)
                        {
                            string target = Path.GetFullPath(Path.Combine(destination, entry.FullName));
                            if (!target.StartsWith(destination + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("资源包包含无效路径。");
                            if (entry.Name.Length == 0) continue;
                            Directory.CreateDirectory(Path.GetDirectoryName(target)); entry.ExtractToFile(target);
                        }
                    }
                });
                string root = destination;
                while (!Directory.EnumerateFiles(root).Any() && Directory.GetDirectories(root).Length == 1) root = Directory.GetDirectories(root)[0];
                var folder = await StorageFolder.GetFolderFromPathAsync(root);
                if (game == GameStyleMode.Crossfire && await CrossfireExternalAssetService.TryInstallAsync(folder, voice))
                {
                    var manifest = Contracts.DisplayFiles.Read<CrossfireExternalAssetService.Manifest>(Path.Combine(root, "manifest.json"));
                    DesktopStorage.Current.LocalSettings.Values[(voice ? "VoicePack." : "KillIconPack.") + "crossfire"] = manifest.Id;
                    string ownedRoot = Path.GetFullPath(Path.Combine(DesktopStorage.Current.LocalFolder.Path, "Packs")) + Path.DirectorySeparatorChar;
                    if (!Path.GetFullPath(destination).StartsWith(ownedRoot, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Invalid import staging path.");
                    Directory.Delete(destination, true);
                    return;
                }
                await PackCatalogService.AddDesktopPackAsync(folder, game, voice, Path.GetFileNameWithoutExtension(source));
            }
            catch { Directory.Delete(destination, true); throw; }
        }
        private static void CopyDirectory(string source, string destination)
        {
            if ((File.GetAttributes(source) & System.IO.FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("请选择普通素材文件夹。");
            foreach (string file in Directory.GetFiles(source))
            {
                if ((File.GetAttributes(file) & System.IO.FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("素材包含链接文件。");
                File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));
            }
            foreach (string folder in Directory.GetDirectories(source)) { string target = Path.Combine(destination, Path.GetFileName(folder)); Directory.CreateDirectory(target); CopyDirectory(folder, target); }
        }
    }
}

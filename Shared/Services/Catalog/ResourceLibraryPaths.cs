// Shared by Widget (UWP) and CompatibilityHost; extracted from both former copies.
using System;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage;

#if WINDOWS_UWP
namespace KillConfirmGameBar.Services
#else
namespace KillConfirmCompatibility.Services
#endif
{
    internal static class ResourceLibraryPaths
    {
        public const string SettingKey = "ResourceLibrary.Root";
        public static string Root
        {
            get
            {
#if SHARED_WIDGET_RUNTIME
                // Only manifest discovery is local in the sandbox. Actual pack
                // files are read from catalog paths through the resource broker.
                return Path.Combine(ApplicationData.Current.LocalFolder.Path, "Packs");
#else
                string value = ApplicationData.Current.LocalSettings.Values[SettingKey] as string;
                return Path.GetFullPath(string.IsNullOrWhiteSpace(value) ? Path.Combine(ApplicationData.Current.LocalFolder.Path, "Packs") : value);
#endif
            }
        }
        public static async Task<StorageFolder> FolderAsync(string relative = "")
        {
            string folder = Path.GetFullPath(Path.Combine(Root, relative));
            if (folder != Root && !folder.StartsWith(Root.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase)) throw new IOException("Invalid material folder.");
            Directory.CreateDirectory(folder);
            return await StorageFolder.GetFolderFromPathAsync(folder);
        }
        public static async Task<StorageFolder> LegacyFolderAsync(string name)
        {
            if (name.IndexOfAny(new[] { '\\', '/' }) >= 0) throw new IOException("Invalid material folder.");
#if SHARED_WIDGET_RUNTIME
            string remote = SharedResources.RemoteLibraryRoot;
            return await SharedResources.ImportedFolderAsync(string.IsNullOrWhiteSpace(remote)
                ? Path.Combine(SharedResources.RemoteDataRoot, name) : Path.Combine(remote, "_legacy", name));
#else
            if (ApplicationData.Current.LocalSettings.Values[SettingKey] is string)
                return await FolderAsync("_legacy/" + name);
            return await ApplicationData.Current.LocalFolder.CreateFolderAsync(name, CreationCollisionOption.OpenIfExists);
#endif
        }
    }
}

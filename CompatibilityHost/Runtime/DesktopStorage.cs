using System;
using System.IO;
using Windows.Foundation.Collections;
using Windows.Storage;

namespace KillConfirmCompatibility.Desktop.Runtime
{
    // Production shares ordinary UserData with the original control panel.
    // Rendering tests use an isolated profile, never the user's data.
    internal sealed class DesktopStorage
    {
        private static DesktopStorage _current;
        public static string TestDataRoot { get; set; }
        public static string AssetsRoot { get; set; }
        public static DesktopStorage Current => _current ??= new DesktopStorage();
        public DesktopSettings LocalSettings { get; }
        public StorageFolder LocalFolder { get; }
        public StorageFolder TemporaryFolder { get; }
        private DesktopStorage()
        {
            if (TestDataRoot == null)
            {
                string root = Contracts.RuntimePaths.DataRoot;
                Directory.CreateDirectory(root);
                var settings=new Contracts.FileSettings(Path.Combine(root, Contracts.FileSettings.FileName));
                Contracts.BundledPacks.Register(settings);
                LocalSettings = new DesktopSettings(settings);
                LocalFolder = StorageFolder.GetFolderFromPathAsync(root).AsTask().GetAwaiter().GetResult();
                TemporaryFolder = LocalFolder;
                AssetsRoot ??= Contracts.RuntimePaths.InstallRoot;
            }
            else
            {
                Directory.CreateDirectory(TestDataRoot);
                LocalFolder = StorageFolder.GetFolderFromPathAsync(Path.GetFullPath(TestDataRoot)).AsTask().GetAwaiter().GetResult();
                TemporaryFolder = LocalFolder;
                LocalSettings = new DesktopSettings(new PropertySet());
            }
        }
        public static async System.Threading.Tasks.Task<StorageFile> AssetFileAsync(Uri uri)
        {
            if (uri.IsFile) return await StorageFile.GetFileFromPathAsync(uri.LocalPath);
            if (uri.Scheme == "ms-appdata")
            {
                string relative = Uri.UnescapeDataString(uri.AbsolutePath);
                if (!relative.StartsWith("/local/", StringComparison.Ordinal)) throw new InvalidDataException("Unsupported app-data resource path.");
                string data = Path.GetFullPath(Current.LocalFolder.Path).TrimEnd(Path.DirectorySeparatorChar);
                string local = Path.GetFullPath(Path.Combine(data, relative.Substring("/local/".Length).Replace('/', Path.DirectorySeparatorChar)));
                if (!local.StartsWith(data + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Asset path escapes data root.");
                return await StorageFile.GetFileFromPathAsync(local);
            }
            if (AssetsRoot == null) return await StorageFile.GetFileFromApplicationUriAsync(uri);
            string root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(AssetsRoot));
            string path = Path.GetFullPath(Path.Combine(root, Uri.UnescapeDataString(uri.AbsolutePath).TrimStart('/').Replace('/', Path.DirectorySeparatorChar)));
            string prefix = Path.EndsInDirectorySeparator(root) ? root : root + Path.DirectorySeparatorChar;
            if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Asset path escapes root.");
            return await StorageFile.GetFileFromPathAsync(path);
        }
    }
    internal sealed class DesktopSettings
    {
        public DesktopSettings(System.Collections.Generic.IDictionary<string, object> values) { Values = new SafeSettings(values); }
        public System.Collections.Generic.IDictionary<string, object> Values { get; }
    }
}

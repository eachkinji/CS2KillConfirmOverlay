using System;
using System.IO;
using Windows.Foundation.Collections;
using Windows.Storage;

namespace KillConfirmCompatibility.Desktop.Runtime
{
    // Production uses the package's existing resource selections. Standalone
    // rendering tests use an isolated temporary profile, never the user's data.
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
                var data = global::Windows.Storage.ApplicationData.Current;
                LocalSettings = new DesktopSettings(data.LocalSettings.Values);
                LocalFolder = data.LocalFolder;
                TemporaryFolder = data.TemporaryFolder;
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
            if (AssetsRoot == null) return await StorageFile.GetFileFromApplicationUriAsync(uri);
            string path = Path.GetFullPath(Path.Combine(AssetsRoot, Uri.UnescapeDataString(uri.AbsolutePath).TrimStart('/').Replace('/', Path.DirectorySeparatorChar)));
            if (!path.StartsWith(Path.GetFullPath(AssetsRoot) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Asset path escapes root.");
            return await StorageFile.GetFileFromPathAsync(path);
        }
    }
    internal sealed class DesktopSettings
    {
        public DesktopSettings(IPropertySet values) { Values = new SafeSettings(values); }
        public System.Collections.Generic.IDictionary<string, object> Values { get; }
    }
}

using System;
using System.IO;
using Windows.Foundation.Collections;
using Windows.Storage;

namespace KillConfirmCompatibility.Desktop.Runtime
{
    // Production uses ordinary desktop data and optionally migrates existing
    // widget selections. Validation profiles never touch the user's data.
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
            if (TestDataRoot == null && DesktopEnvironment.HasPackageIdentity && global::Windows.ApplicationModel.Package.Current.Id.Name != "KillConfirmGameBar.Overlay")
            {
                var data = global::Windows.Storage.ApplicationData.Current;
                LocalFolder = data.LocalFolder; TemporaryFolder = data.TemporaryFolder;
                LocalSettings = new DesktopSettings(data.LocalSettings.Values);
            }
            else if (TestDataRoot == null)
            {
                ProfileMirror.Initialize();
                string root = DesktopEnvironment.DataRoot;
                Directory.CreateDirectory(root);
                LocalFolder = StorageFolder.GetFolderFromPathAsync(root).AsTask().GetAwaiter().GetResult();
                TemporaryFolder = LocalFolder;
                LocalSettings = new DesktopSettings(new FileSettings(Path.Combine(root, Contracts.SharedSettingsFile.FileName)));
                AssetsRoot ??= DesktopEnvironment.InstallRoot;
            }
            else
            {
                string root = TestDataRoot;
                Directory.CreateDirectory(root);
                LocalFolder = StorageFolder.GetFolderFromPathAsync(Path.GetFullPath(root)).AsTask().GetAwaiter().GetResult();
                TemporaryFolder = LocalFolder;
                LocalSettings = new DesktopSettings((IPropertySet)new PropertySet());
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
        public DesktopSettings(System.Collections.Generic.IDictionary<string, object> values) { Values = values; }
        public System.Collections.Generic.IDictionary<string, object> Values { get; }
    }
}

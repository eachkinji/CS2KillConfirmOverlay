using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage;
namespace KillConfirmGameBar.Services
{
    internal static class SharedResources
    {
        internal static Task<StorageFile> AssetFileAsync(Uri uri) => DesktopPlatform.AssetFileAsync(uri);
        internal static Task<StorageFolder> ImportedFolderAsync(string path) => StorageFolder.GetFolderFromPathAsync(path).AsTask();
        internal static Task<StorageFile> FileFromPathAsync(string path) => StorageFile.GetFileFromPathAsync(path).AsTask();
        internal static Task<StorageFolder> AssetFolderAsync(string path) => StorageFolder.GetFolderFromPathAsync(Path.GetFullPath(Path.Combine(KillConfirmCompatibility.Contracts.RuntimePaths.InstallRoot,path))).AsTask();
        internal static string CachedAssetUri(string relative) => "ms-appx:///"+relative.Replace('\\','/');
        internal static async Task ApplyImageAsync(Image image, string source)
        {
            if (image == null || string.IsNullOrWhiteSpace(source)) return;
            try
            {
                StorageFile file = await AssetFileAsync(new Uri(source));
                var bitmap = new BitmapImage();
                if (image.Width > 0 && image.Width <= 128)
                    bitmap.DecodePixelWidth = Math.Max(1, (int)Math.Ceiling(image.Width * DesktopPlatform.Display.LogicalDpi / 96));
                using (var stream = await file.OpenReadAsync()) await bitmap.SetSourceAsync(stream);
                image.Source = bitmap;
            }
            catch { }
        }
    }
}

using Windows.Storage;
namespace KillConfirmGameBar.Services;
internal static class SharedResources
{
    internal static Task<StorageFile> AssetFileAsync(Uri uri) => DesktopPlatform.AssetFileAsync(uri);
    internal static Task<StorageFolder> ImportedFolderAsync(string path) => StorageFolder.GetFolderFromPathAsync(path).AsTask();
    internal static Task<StorageFile> FileFromPathAsync(string path) => StorageFile.GetFileFromPathAsync(path).AsTask();
    internal static Task<StorageFolder> AssetFolderAsync(string path) => StorageFolder.GetFolderFromPathAsync(Path.GetFullPath(Path.Combine(KillConfirmCompatibility.Contracts.RuntimePaths.InstallRoot,path))).AsTask();
    internal static string CachedAssetUri(string relative) => "ms-appx:///"+relative.Replace('\\','/');
}

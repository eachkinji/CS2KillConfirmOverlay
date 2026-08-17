using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using KillConfirmGameBar.Services;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace KillConfirmGameBar
{
    public sealed partial class MainPage
    {
        private static async Task<StorageFolder> ExtractZipToTemporaryFolderAsync(StorageFile zipFile)
        {
            StorageFolder tempRoot = await ApplicationData.Current.TemporaryFolder.CreateFolderAsync(
                "ImportedPack_" + Guid.NewGuid().ToString("N"),
                CreationCollisionOption.FailIfExists);

            using (Stream zipStream = await zipFile.OpenStreamForReadAsync())
            using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Read))
            {
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    if (string.IsNullOrWhiteSpace(entry.FullName))
                    {
                        continue;
                    }

                    string normalizedPath = entry.FullName.Replace('\\', '/');
                    string[] segments = normalizedPath
                        .Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                    if (segments.Length == 0 || segments.Any(IsUnsafeZipPathSegment))
                    {
                        continue;
                    }

                    bool isDirectory = normalizedPath.EndsWith("/", StringComparison.Ordinal);
                    StorageFolder targetFolder = await CreateFolderPathAsync(
                        tempRoot,
                        isDirectory ? segments : segments.Take(segments.Length - 1));

                    if (isDirectory)
                    {
                        continue;
                    }

                    StorageFile targetFile = await targetFolder.CreateFileAsync(
                        segments[segments.Length - 1],
                        CreationCollisionOption.ReplaceExisting);
                    using (Stream entryStream = entry.Open())
                    using (Stream targetStream = await targetFile.OpenStreamForWriteAsync())
                    {
                        targetStream.SetLength(0);
                        await entryStream.CopyToAsync(targetStream);
                    }
                }
            }

            return tempRoot;
        }

        private static bool IsUnsafeZipPathSegment(string segment)
        {
            return string.IsNullOrWhiteSpace(segment)
                || segment == "."
                || segment == ".."
                || segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0;
        }

        private static async Task<StorageFolder> CreateFolderPathAsync(StorageFolder root, IEnumerable<string> segments)
        {
            StorageFolder current = root;
            foreach (string segment in segments)
            {
                current = await current.CreateFolderAsync(segment, CreationCollisionOption.OpenIfExists);
            }

            return current;
        }

        private static async Task<StorageFolder> FindBestPackFolderAsync(StorageFolder root, IReadOnlyList<string> recognizedFileNames)
        {
            StorageFolder bestFolder = root;
            int bestScore = await CountRecognizedFilesAsync(root, recognizedFileNames);
            IReadOnlyList<StorageFolder> subFolders = await root.GetFoldersAsync();
            foreach (StorageFolder subFolder in subFolders)
            {
                (StorageFolder folder, int score) = await FindBestPackFolderRecursiveAsync(subFolder, recognizedFileNames);
                if (score > bestScore)
                {
                    bestFolder = folder;
                    bestScore = score;
                }
            }

            return bestFolder;
        }

        private static async Task<(StorageFolder Folder, int Score)> FindBestPackFolderRecursiveAsync(
            StorageFolder folder,
            IReadOnlyList<string> recognizedFileNames)
        {
            StorageFolder bestFolder = folder;
            int bestScore = await CountRecognizedFilesAsync(folder, recognizedFileNames);
            IReadOnlyList<StorageFolder> subFolders = await folder.GetFoldersAsync();
            foreach (StorageFolder subFolder in subFolders)
            {
                (StorageFolder candidateFolder, int candidateScore) = await FindBestPackFolderRecursiveAsync(subFolder, recognizedFileNames);
                if (candidateScore > bestScore)
                {
                    bestFolder = candidateFolder;
                    bestScore = candidateScore;
                }
            }

            return (bestFolder, bestScore);
        }

        private static async Task<int> CountRecognizedFilesAsync(StorageFolder folder, IReadOnlyList<string> recognizedFileNames)
        {
            IReadOnlyDictionary<string, StorageFile> files = await CollectRecognizedFilesAsync(folder, recognizedFileNames.ToArray());
            return files.Count;
        }

        private async Task<StorageFile> PickSingleFileAsync(string[] fileFilters)
        {
            var picker = new FileOpenPicker();
            foreach (string filter in fileFilters)
            {
                picker.FileTypeFilter.Add(filter);
            }

            return await picker.PickSingleFileAsync();
        }

        private async Task ShowMessageAsync(string title, string message)
        {
            var dialog = new ContentDialog
            {
                Title = title,
                Content = message,
                CloseButtonText = LocalizationManager.Text("Cancel"),
                RequestedTheme = ElementTheme.Light,
                Background = new SolidColorBrush(Color.FromArgb(255, 250, 250, 247)),
                Foreground = new SolidColorBrush(Color.FromArgb(255, 29, 34, 51))
            };
            await dialog.ShowAsync();
        }

        private static async Task<IReadOnlyDictionary<string, StorageFile>> CollectRecognizedFilesAsync(StorageFolder folder, params string[] fileNames)
        {
            var files = new Dictionary<string, StorageFile>(StringComparer.OrdinalIgnoreCase);
            foreach (string fileName in fileNames)
            {
                StorageFile file = await TryGetFileAsync(folder, fileName);
                if (file == null && fileName.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (string extension in new[] { ".mp3", ".m4a" })
                    {
                        file = await TryGetFileAsync(folder, System.IO.Path.ChangeExtension(fileName, extension));
                        if (file != null)
                        {
                            break;
                        }
                    }
                }

                if (file == null && fileName.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                {
                    file = await TryGetIconFileVariantAsync(folder, fileName);
                    if (file == null)
                    {
                        try
                        {
                            StorageFolder badgeex = await folder.GetFolderAsync("badgeex");
                            file = await TryGetIconFileVariantAsync(badgeex, fileName);
                        }
                        catch { }
                    }
                }

                if (file != null)
                {
                    files[fileName] = file;
                }
            }

            return files;
        }

        private static async Task<StorageFile> TryGetIconFileVariantAsync(StorageFolder folder, string canonicalFileName)
        {
            foreach (string extension in IconImageExtensions)
            {
                StorageFile file = await TryGetFileAsync(folder, System.IO.Path.ChangeExtension(canonicalFileName, extension));
                if (file != null)
                {
                    return file;
                }
            }

            return null;
        }

        public static async Task<StorageFolder> GetVoicePackFolderAsync(VoicePackItem item)
        {
            if (item == null)
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(item.FolderPath))
            {
                try
                {
                    return await StorageFolder.GetFolderFromPathAsync(item.FolderPath);
                }
                catch { }
            }

            if (item.IsBuiltIn)
            {
                try
                {
                    StorageFolder installed = Windows.ApplicationModel.Package.Current.InstalledLocation;
                    return await installed.GetFolderAsync(@"KillConfirmService\sounds\" + item.Key);
                }
                catch { }
            }

            return null;
        }

        public static async Task<StorageFolder> GetIconPackFolderAsync(IconPackItem item)
        {
            if (item == null)
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(item.FolderPath))
            {
                try
                {
                    return await StorageFolder.GetFolderFromPathAsync(item.FolderPath);
                }
                catch { }
            }

            if (item.IsBuiltIn)
            {
                try
                {
                    StorageFolder installed = Windows.ApplicationModel.Package.Current.InstalledLocation;
                    if (string.Equals(item.Key, "dagoujiao", StringComparison.OrdinalIgnoreCase))
                    {
                        return await installed.GetFolderAsync(@"Assets\GameStyles\dagoujiao\killconfirm\textures");
                    }
                    if (string.Equals(item.Key, "csol4", StringComparison.OrdinalIgnoreCase) || string.Equals(item.Key, "csol_original", StringComparison.OrdinalIgnoreCase))
                    {
                        return await installed.GetFolderAsync(@"Assets\KillConfirmCode\Csol4");
                    }
                    if (string.Equals(item.Key, "original", StringComparison.OrdinalIgnoreCase) || string.Equals(item.Key, "default", StringComparison.OrdinalIgnoreCase))
                    {
                        return await installed.GetFolderAsync(@"Assets\KillConfirmCode\Original");
                    }
                    if (string.Equals(item.Key, "glory", StringComparison.OrdinalIgnoreCase))
                    {
                        return await installed.GetFolderAsync(@"Assets\KillConfirmCode\Anniversary10");
                    }
                    if (string.Equals(item.Key, "champion", StringComparison.OrdinalIgnoreCase))
                    {
                        return await installed.GetFolderAsync(@"Assets\KillConfirmCode\Anniversary15");
                    }
                }
                catch { }
            }

            return null;
        }

        public static async Task<IReadOnlyDictionary<string, StorageFile>> CollectFilesFromPackFolderAsync(StorageFolder folder, params string[] fileNames)
        {
            if (folder == null)
            {
                return new Dictionary<string, StorageFile>(StringComparer.OrdinalIgnoreCase);
            }
            return await CollectRecognizedFilesAsync(folder, fileNames);
        }

        private static async Task<IReadOnlyDictionary<string, StorageFile>> CollectRecognizedFilesFromFolderAsync(string folderPath, params string[] fileNames)
        {
            try
            {
                StorageFolder folder = await StorageFolder.GetFolderFromPathAsync(folderPath);
                return await CollectRecognizedFilesAsync(folder, fileNames);
            }
            catch
            {
                return new Dictionary<string, StorageFile>(StringComparer.OrdinalIgnoreCase);
            }
        }

        private static async Task<StorageFile> TryGetFileAsync(StorageFolder folder, string fileName)
        {
            try
            {
                return await folder.GetFileAsync(fileName);
            }
            catch
            {
                return null;
            }
        }
    }
}

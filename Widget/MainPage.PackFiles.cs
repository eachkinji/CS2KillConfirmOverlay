using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using KillConfirmGameBar.Services;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Data.Json;
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
            if (folder == null)
            {
                return files;
            }

            IReadOnlyList<StorageFile> allFolderFiles = null;
            try
            {
                allFolderFiles = await folder.GetFilesAsync();
            }
            catch { }

            StorageFolder badgeexFolder = null;
            IReadOnlyList<StorageFile> allBadgeexFiles = null;
            try
            {
                badgeexFolder = await folder.GetFolderAsync("badgeex");
                if (badgeexFolder != null)
                {
                    allBadgeexFiles = await badgeexFolder.GetFilesAsync();
                }
            }
            catch { }

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
                    if (file == null && badgeexFolder != null)
                    {
                        file = await TryGetIconFileVariantAsync(badgeexFolder, fileName);
                    }
                }

                // If still not found by direct lookup, scan files list case-insensitively
                if (file == null && allFolderFiles != null)
                {
                    string targetNameWithoutExt = System.IO.Path.GetFileNameWithoutExtension(fileName);
                    foreach (StorageFile candidate in allFolderFiles)
                    {
                        if (string.Equals(candidate.Name, fileName, StringComparison.OrdinalIgnoreCase)
                            || string.Equals(System.IO.Path.GetFileNameWithoutExtension(candidate.Name), targetNameWithoutExt, StringComparison.OrdinalIgnoreCase))
                        {
                            file = candidate;
                            break;
                        }
                    }
                }

                if (file == null && allBadgeexFiles != null)
                {
                    string targetNameWithoutExt = System.IO.Path.GetFileNameWithoutExtension(fileName);
                    foreach (StorageFile candidate in allBadgeexFiles)
                    {
                        if (string.Equals(candidate.Name, fileName, StringComparison.OrdinalIgnoreCase)
                            || string.Equals(System.IO.Path.GetFileNameWithoutExtension(candidate.Name), targetNameWithoutExt, StringComparison.OrdinalIgnoreCase))
                        {
                            file = candidate;
                            break;
                        }
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
            if (folder == null)
            {
                return null;
            }

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
                    string key = item.Key ?? string.Empty;

                    // Dagoujiao
                    if (string.Equals(key, "dagoujiao", StringComparison.OrdinalIgnoreCase))
                    {
                        return await installed.GetFolderAsync(@"Assets\GameStyles\dagoujiao\killconfirm\textures");
                    }

                    // CSOL
                    if (string.Equals(key, "csol4", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(key, "csol_original", StringComparison.OrdinalIgnoreCase))
                    {
                        return await installed.GetFolderAsync(@"Assets\KillConfirmCode\Csol4");
                    }

                    // CrossFire Built-ins
                    if (string.Equals(key, "original", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(key, "default", StringComparison.OrdinalIgnoreCase))
                    {
                        return await installed.GetFolderAsync(@"Assets\KillConfirmCode\Original");
                    }
                    if (string.Equals(key, "vip", StringComparison.OrdinalIgnoreCase))
                    {
                        return await installed.GetFolderAsync(@"Assets\KillConfirmCode\Vip");
                    }
                    if (string.Equals(key, "angelic_beast", StringComparison.OrdinalIgnoreCase))
                    {
                        return await installed.GetFolderAsync(@"Assets\KillConfirmCode\AngelicBeast");
                    }
                    if (string.Equals(key, "anniversary_10", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(key, "glory", StringComparison.OrdinalIgnoreCase))
                    {
                        return await installed.GetFolderAsync(@"Assets\KillConfirmCode\Anniversary10");
                    }
                    if (string.Equals(key, "anniversary_15", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(key, "champion", StringComparison.OrdinalIgnoreCase))
                    {
                        return await installed.GetFolderAsync(@"Assets\KillConfirmCode\Anniversary15");
                    }
                    if (string.Equals(key, "cfpl", StringComparison.OrdinalIgnoreCase))
                    {
                        return await installed.GetFolderAsync(@"Assets\KillConfirmCode\CFPL");
                    }
                    if (string.Equals(key, "rankmach_2019_1", StringComparison.OrdinalIgnoreCase))
                    {
                        return await installed.GetFolderAsync(@"Assets\KillConfirmCode\Rankmach2019_1");
                    }
                    if (string.Equals(key, "rankmach_2019_2", StringComparison.OrdinalIgnoreCase))
                    {
                        return await installed.GetFolderAsync(@"Assets\KillConfirmCode\Rankmach2019_2");
                    }

                    // Other Game Styles
                    if (string.Equals(key, "battlefield1", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(key, "bf1", StringComparison.OrdinalIgnoreCase))
                    {
                        return await installed.GetFolderAsync(@"Assets\GameStyles\battlefield1\killconfirm\textures");
                    }
                    if (string.Equals(key, "battlefield5", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(key, "bf5", StringComparison.OrdinalIgnoreCase))
                    {
                        return await installed.GetFolderAsync(@"Assets\GameStyles\battlefield5\killconfirm\textures");
                    }
                    if (string.Equals(key, "battlefield4", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(key, "bf4", StringComparison.OrdinalIgnoreCase))
                    {
                        return await installed.GetFolderAsync(@"Assets\GameStyles\battlefield4\killconfirm\textures");
                    }
                    if (string.Equals(key, "battlefield2042", StringComparison.OrdinalIgnoreCase))
                    {
                        return await installed.GetFolderAsync(@"Assets\GameStyles\battlefield2042\killconfirm\textures");
                    }
                    if (string.Equals(key, "pubg", StringComparison.OrdinalIgnoreCase))
                    {
                        return await installed.GetFolderAsync(@"Assets\GameStyles\pubg\killconfirm\textures");
                    }
                    if (string.Equals(key, "deltaforce", StringComparison.OrdinalIgnoreCase))
                    {
                        return await installed.GetFolderAsync(@"Assets\GameStyles\deltaforce\killconfirm\textures");
                    }
                    if (string.Equals(key, "doubao", StringComparison.OrdinalIgnoreCase))
                    {
                        return await installed.GetFolderAsync(@"Assets\GameStyles\doubao\killconfirm\textures");
                    }
                    if (string.Equals(key, "overwatch", StringComparison.OrdinalIgnoreCase))
                    {
                        return await installed.GetFolderAsync(@"Assets\GameStyles\overwatch\killconfirm\textures");
                    }
                    if (string.Equals(key, "modernwarfare2019", StringComparison.OrdinalIgnoreCase))
                    {
                        return await installed.GetFolderAsync(@"Assets\GameStyles\modernwarfare2019\killconfirm\textures");
                    }
                    if (string.Equals(key, "apex", StringComparison.OrdinalIgnoreCase))
                    {
                        return await installed.GetFolderAsync(@"Assets\GameStyles\apex\killconfirm\textures");
                    }

                    // Valorant
                    if (key.StartsWith("valorant_", StringComparison.OrdinalIgnoreCase))
                    {
                        string skinKey = key.Substring("valorant_".Length);
                        return await installed.GetFolderAsync(@"Assets\GameStyles\valorant\killconfirm\" + skinKey + @"\textures");
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

        public static async Task<IReadOnlyDictionary<string, IReadOnlyList<StorageFile>>> CollectVoiceFileGroupsFromPackFolderAsync(
            StorageFolder folder,
            params string[] fileNames)
        {
            var result = new Dictionary<string, IReadOnlyList<StorageFile>>(StringComparer.OrdinalIgnoreCase);
            if (folder == null) return result;

            IReadOnlyList<StorageFile> allFiles;
            try
            {
                allFiles = await folder.GetFilesAsync();
            }
            catch
            {
                return result;
            }

            string[] audioExtensions = { ".wav", ".mp3", ".m4a" };
            foreach (string fileName in fileNames)
            {
                string targetStem = System.IO.Path.GetFileNameWithoutExtension(fileName);
                List<StorageFile> matches = allFiles
                    .Where(file => audioExtensions.Contains(file.FileType, StringComparer.OrdinalIgnoreCase))
                    .Where(file =>
                    {
                        string stem = System.IO.Path.GetFileNameWithoutExtension(file.Name);
                        return string.Equals(stem, targetStem, StringComparison.OrdinalIgnoreCase)
                            || stem.StartsWith(targetStem + "__", StringComparison.OrdinalIgnoreCase);
                    })
                    .OrderBy(file => string.Equals(
                        System.IO.Path.GetFileNameWithoutExtension(file.Name),
                        targetStem,
                        StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                    .ThenBy(file => file.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                if (matches.Count > 0) result[fileName] = matches;
            }
            return result;
        }

        public static async Task<IReadOnlyDictionary<string, IReadOnlyList<StorageFile>>> CollectVoiceFileGroupsFromManifestAsync(
            StorageFolder folder,
            IReadOnlyDictionary<string, string> sourceStemToManifestSlot)
        {
            var result = new Dictionary<string, IReadOnlyList<StorageFile>>(StringComparer.OrdinalIgnoreCase);
            if (folder == null || sourceStemToManifestSlot == null) return result;

            string[] canonicalNames = sourceStemToManifestSlot.Keys
                .Select(stem => stem + ".wav")
                .ToArray();
            IReadOnlyDictionary<string, IReadOnlyList<StorageFile>> discovered =
                await CollectVoiceFileGroupsFromPackFolderAsync(folder, canonicalNames);
            foreach (var pair in discovered) result[pair.Key] = pair.Value;

            try
            {
                StorageFile manifestFile = await folder.GetFileAsync("manifest.json");
                JsonObject manifest = JsonObject.Parse(await FileIO.ReadTextAsync(manifestFile));
                JsonObject audio = manifest.GetNamedObject("audio", null);
                JsonObject slots = audio?.GetNamedObject("slots", null);
                if (slots == null) return result;

                foreach (var mapping in sourceStemToManifestSlot)
                {
                    if (!slots.TryGetValue(mapping.Value, out IJsonValue slotValue)) continue;
                    var manifestNames = new List<string>();
                    if (slotValue.ValueType == JsonValueType.String)
                    {
                        manifestNames.Add(slotValue.GetString());
                    }
                    else if (slotValue.ValueType == JsonValueType.Array)
                    {
                        foreach (IJsonValue value in slotValue.GetArray())
                        {
                            if (value.ValueType == JsonValueType.String) manifestNames.Add(value.GetString());
                        }
                    }

                    var files = new List<StorageFile>();
                    foreach (string manifestName in manifestNames)
                    {
                        if (string.IsNullOrWhiteSpace(manifestName)) continue;
                        try
                        {
                            StorageFile file = await folder.GetFileAsync(manifestName.Replace('/', '\\'));
                            if (file != null) files.Add(file);
                        }
                        catch { }
                    }
                    if (files.Count > 0) result[mapping.Key + ".wav"] = files;
                }
            }
            catch { }

            return result;
        }

        private static IReadOnlyDictionary<string, IReadOnlyList<StorageFile>> ToVoiceFileGroups(
            IReadOnlyDictionary<string, StorageFile> files)
        {
            var result = new Dictionary<string, IReadOnlyList<StorageFile>>(StringComparer.OrdinalIgnoreCase);
            if (files == null) return result;
            foreach (var pair in files)
            {
                if (pair.Value != null) result[pair.Key] = new[] { pair.Value };
            }
            return result;
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

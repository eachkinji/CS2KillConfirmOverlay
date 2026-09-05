using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using KillConfirmGameBar.Helpers;
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
            var theme = GameThemePalette.Current;
            var body = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, FontSize = 14, Foreground = new SolidColorBrush(theme.Text) };
            var scroll = new ScrollViewer { Content = body, MaxHeight = 320, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
            var dialog = CreatePackStatusDialog(title, scroll,
                LocalizationManager.Current == UiLanguage.SimplifiedChinese ? "知道了" : "OK");
            await dialog.ShowAsync();
        }

        private static async Task<IReadOnlyDictionary<string, StorageFile>> CollectRecognizedFilesAsync(StorageFolder folder, params string[] fileNames)
        {
            var files = new Dictionary<string, StorageFile>(StringComparer.OrdinalIgnoreCase);
            if (folder == null)
            {
                return files;
            }

            if (fileNames.Any(name => name.Equals("SPRITE_01.png", StringComparison.OrdinalIgnoreCase)))
            {
                var sources = new List<StorageFolder> { folder };
                foreach (string child in new[] { "Sprite", "badgeex" })
                {
                    try { sources.Add(await folder.GetFolderAsync(child)); } catch { }
                }
                foreach (StorageFolder source in sources)
                {
                    var available = (await source.GetFilesAsync()).ToDictionary(file => file.Name, StringComparer.OrdinalIgnoreCase);
                    foreach (string canonical in CrossfirePackFormat.Files)
                    {
                        if (files.ContainsKey(canonical)) continue;
                        foreach (string candidate in CrossfirePackFormat.Candidates(canonical))
                        {
                            foreach (string extension in IconImageExtensions)
                            {
                                if (available.TryGetValue(Path.ChangeExtension(candidate, extension), out StorageFile file))
                                { files[canonical] = file; break; }
                            }
                            if (files.ContainsKey(canonical)) break;
                        }
                    }
                }
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

                // If still not found by direct lookup, scan files list case-insensitively and via aliases
                if (file == null && allFolderFiles != null)
                {
                    string targetNameWithoutExt = System.IO.Path.GetFileNameWithoutExtension(fileName);
                    if (AudioSlotAliases.SupportedAudioExtensions.Any(ext => fileName.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
                    {
                        var audioMatches = AudioSlotAliases.MatchSlotAudioFiles(allFolderFiles, targetNameWithoutExt);
                        if (audioMatches.Count > 0)
                        {
                            file = audioMatches[0];
                        }
                    }

                    if (file == null)
                    {
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

                    if (string.Equals(key, "custommodule", StringComparison.OrdinalIgnoreCase))
                    {
                        return await installed.GetFolderAsync(
                            @"Assets\GameStyles\custommodule\iconpacks\custommodule");
                    }

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
                        return await PackCatalogService.GetImportedIconFolderAsync("default");
                    }
                    if (string.Equals(key, "vip", StringComparison.OrdinalIgnoreCase))
                    {
                        return await PackCatalogService.GetImportedIconFolderAsync("vip");
                    }
                    if (string.Equals(key, "angelic_beast", StringComparison.OrdinalIgnoreCase))
                    {
                        return await PackCatalogService.GetImportedIconFolderAsync("angelic_beast");
                    }
                    if (string.Equals(key, "anniversary_10", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(key, "glory", StringComparison.OrdinalIgnoreCase))
                    {
                        return await PackCatalogService.GetImportedIconFolderAsync("anniversary_10");
                    }
                    if (string.Equals(key, "anniversary_15", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(key, "champion", StringComparison.OrdinalIgnoreCase))
                    {
                        return await PackCatalogService.GetImportedIconFolderAsync("anniversary_15");
                    }
                    if (string.Equals(key, "cfpl", StringComparison.OrdinalIgnoreCase))
                    {
                        return await PackCatalogService.GetImportedIconFolderAsync("cfpl");
                    }
                    if (string.Equals(key, "rankmach_2019_1", StringComparison.OrdinalIgnoreCase))
                    {
                        return await PackCatalogService.GetImportedIconFolderAsync("rankmach_2019_1");
                    }
                    if (string.Equals(key, "rankmach_2019_2", StringComparison.OrdinalIgnoreCase))
                    {
                        return await PackCatalogService.GetImportedIconFolderAsync("rankmach_2019_2");
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

        private async Task ExportVoicePackAsync(VoicePackItem item)
        {
            await ExportPackFolderAsync(
                await GetVoicePackFolderAsync(item),
                PackCatalogService.GetVoicePackDisplayName(item));
        }

        private async Task ExportIconPackAsync(IconPackItem item)
        {
            await ExportPackFolderAsync(
                await GetIconPackFolderAsync(item),
                PackCatalogService.GetIconPackDisplayName(item));
        }

        private async Task ExportPackFolderAsync(StorageFolder sourceFolder, string displayName)
        {
            bool isChinese = LocalizationManager.Current == UiLanguage.SimplifiedChinese;
            if (sourceFolder == null)
            {
                await ShowMessageAsync(
                    isChinese ? "无法导出" : "Unable to export",
                    isChinese ? "没有找到这个资源包的文件。" : "The files for this pack could not be found.");
                return;
            }

            var picker = new FileSavePicker
            {
                SuggestedStartLocation = PickerLocationId.Downloads,
                SuggestedFileName = SanitizeExportFileName(displayName)
            };
            picker.FileTypeChoices.Add("Audio / icon pack", new List<string> { ".zip" });
            StorageFile destination = await picker.PickSaveFileAsync();
            if (destination == null)
            {
                return;
            }

            StorageFile temporaryZip = await ApplicationData.Current.TemporaryFolder.CreateFileAsync(
                "PackExport_" + Guid.NewGuid().ToString("N") + ".zip",
                CreationCollisionOption.ReplaceExisting);
            try
            {
                using (Stream stream = await temporaryZip.OpenStreamForWriteAsync())
                using (var archive = new ZipArchive(stream, ZipArchiveMode.Create))
                {
                    await AddFolderToArchiveAsync(archive, sourceFolder, string.Empty);
                }

                await temporaryZip.CopyAndReplaceAsync(destination);
            }
            finally
            {
                try { await temporaryZip.DeleteAsync(); } catch { }
            }
        }

        private static async Task AddFolderToArchiveAsync(ZipArchive archive, StorageFolder folder, string relativePath)
        {
            foreach (StorageFile file in await folder.GetFilesAsync())
            {
                string entryName = string.IsNullOrEmpty(relativePath)
                    ? file.Name
                    : relativePath + "/" + file.Name;
                ZipArchiveEntry entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
                using (Stream input = await file.OpenStreamForReadAsync())
                using (Stream output = entry.Open())
                {
                    await input.CopyToAsync(output);
                }
            }

            foreach (StorageFolder child in await folder.GetFoldersAsync())
            {
                string childPath = string.IsNullOrEmpty(relativePath)
                    ? child.Name
                    : relativePath + "/" + child.Name;
                await AddFolderToArchiveAsync(archive, child, childPath);
            }
        }

        private static string SanitizeExportFileName(string value)
        {
            string safe = string.IsNullOrWhiteSpace(value) ? "KillConfirm-Pack" : value.Trim();
            foreach (char invalid in Path.GetInvalidFileNameChars())
            {
                safe = safe.Replace(invalid, '_');
            }
            return safe;
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

            var audioFiles = allFiles
                .Where(file => AudioSlotAliases.SupportedAudioExtensions.Contains(file.FileType, StringComparer.OrdinalIgnoreCase))
                .ToList();

            foreach (string fileName in fileNames)
            {
                string targetStem = System.IO.Path.GetFileNameWithoutExtension(fileName);
                var matches = AudioSlotAliases.MatchSlotAudioFiles(audioFiles, targetStem);
                if (matches.Count > 0)
                {
                    result[fileName] = matches;
                }
            }
            return result;
        }
    }
}

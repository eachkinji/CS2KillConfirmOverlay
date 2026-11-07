using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KillConfirmGameBar.Services;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace KillConfirmGameBar
{
    public sealed partial class MainPage
    {

        private static bool IsPackInMaterialCategory(string gameKey, string packKey)
        {
            return GameStyleService.GetStyleForPackKey(packKey) == GameStyleService.FromKey(gameKey);
        }

        private static async Task<IReadOnlyList<StorageFile>> LoadMaterialFolderFilesAsync(MaterialFolderItem folder, bool isAudio)
        {
            if (folder?.Files != null) return folder.Files;
            if (folder?.Folder == null) return Array.Empty<StorageFile>();
            try
            {
                IReadOnlyList<StorageFile> files = await folder.Folder.GetFilesAsync();
                return files
                    .Where(file => !file.Name.StartsWith("pack_head.", StringComparison.OrdinalIgnoreCase))
                    .Where(file => IsSupportedMaterialFile(file, isAudio))
                    .ToList();
            }
            catch
            {
                return Array.Empty<StorageFile>();
            }
        }

        private static async Task<IReadOnlyList<StorageFile>> PickMaterialFilesAsync(bool isAudio, bool allowMultiple)
        {
            var picker = new FileOpenPicker();
            string[] filters = isAudio
                ? new[] { ".wav", ".mp3", ".m4a" }
                : new[] { ".png", ".jpg", ".jpeg", ".webp", ".tga" };
            foreach (string filter in filters) picker.FileTypeFilter.Add(filter);
            if (allowMultiple)
            {
                IReadOnlyList<StorageFile> files = await picker.PickMultipleFilesAsync();
                return files ?? Array.Empty<StorageFile>();
            }
            StorageFile file = await picker.PickSingleFileAsync();
            return file == null ? Array.Empty<StorageFile>() : new[] { file };
        }

        private static IEnumerable<StorageFile> DistinctMaterialFiles(IEnumerable<StorageFile> files)
        {
            return (files ?? Enumerable.Empty<StorageFile>())
                .Where(file => file != null)
                .GroupBy(MaterialFileIdentity, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First());
        }

        private static string MaterialFileIdentity(StorageFile file)
        {
            if (file == null) return string.Empty;
            return string.IsNullOrWhiteSpace(file.Path) ? file.Name : file.Path;
        }

        private static bool IsSupportedMaterialFile(StorageFile file, bool isAudio)
        {
            if (file == null) return false;
            string extension = System.IO.Path.GetExtension(file.Name)?.ToLowerInvariant() ?? string.Empty;
            return isAudio
                ? extension == ".wav" || extension == ".mp3" || extension == ".m4a"
                : extension == ".png" || extension == ".jpg" || extension == ".jpeg" || extension == ".webp" || extension == ".tga";
        }
    }
}

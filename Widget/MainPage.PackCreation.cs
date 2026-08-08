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
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace KillConfirmGameBar
{
    public sealed partial class MainPage
    {
        private async void OnImportVoicePackClick(object sender, RoutedEventArgs e)
        {
            var picker = new FolderPicker();
            picker.FileTypeFilter.Add("*");
            StorageFolder folder = await picker.PickSingleFolderAsync();
            if (folder == null)
            {
                return;
            }

            await ShowCreateVoicePackDialogAsync(
                folder.DisplayName,
                await CollectRecognizedFilesAsync(folder, VoicePackImportFiles),
                await TryGetAudioFileAsync(folder, "common_overlay"),
                await TryGetCustomPackHeadImageAsync(folder.Path));
        }

        private async void OnImportVoiceZipClick(object sender, RoutedEventArgs e)
        {
            await ImportPackFromZipAsync(
                VoicePackImportFiles,
                async (folder, files) =>
                {
                    await ShowCreateVoicePackDialogAsync(
                        folder.DisplayName,
                        files,
                        await TryGetAudioFileAsync(folder, "common_overlay"),
                        await TryGetCustomPackHeadImageAsync(folder.Path));
                });
        }

        private async void OnImportIconPackClick(object sender, RoutedEventArgs e)
        {
            var picker = new FolderPicker();
            picker.FileTypeFilter.Add("*");
            StorageFolder folder = await picker.PickSingleFolderAsync();
            if (folder == null)
            {
                return;
            }

            await ShowCreateIconPackDialogAsync(
                folder.DisplayName,
                await CollectRecognizedFilesAsync(folder, IconPackImportFiles));
        }

        private async void OnImportIconZipClick(object sender, RoutedEventArgs e)
        {
            await ImportPackFromZipAsync(
                IconPackImportFiles,
                async (folder, files) =>
                {
                    await ShowCreateIconPackDialogAsync(folder.DisplayName, files);
                });
        }

        private async void OnCreateVoicePackClick(object sender, RoutedEventArgs e)
        {
            await ShowCreateVoicePackDialogAsync();
        }

        private async void OnCreateIconPackClick(object sender, RoutedEventArgs e)
        {
            await ShowCreateIconPackDialogAsync();
        }

        private async Task ImportPackFromZipAsync(
            IReadOnlyList<string> recognizedFileNames,
            Func<StorageFolder, IReadOnlyDictionary<string, StorageFile>, Task> showDialogAsync)
        {
            StorageFile zipFile = await PickSingleFileAsync(new[] { ".zip" });
            if (zipFile == null)
            {
                return;
            }

            StorageFolder extractedFolder = null;
            try
            {
                extractedFolder = await ExtractZipToTemporaryFolderAsync(zipFile);
                StorageFolder bestFolder = await FindBestPackFolderAsync(extractedFolder, recognizedFileNames);
                IReadOnlyDictionary<string, StorageFile> files = await CollectRecognizedFilesAsync(bestFolder, recognizedFileNames.ToArray());
                if (files.Count == 0)
                {
                    await ShowMessageAsync(
                        LocalizationManager.Text("ZipImportFailedTitle"),
                        LocalizationManager.Text("ZipImportNoFilesMessage"));
                    return;
                }

                await showDialogAsync(bestFolder, files);
            }
            catch
            {
                await ShowMessageAsync(
                    LocalizationManager.Text("ZipImportFailedTitle"),
                    LocalizationManager.Text("ZipImportFailedMessage"));
            }
            finally
            {
                if (extractedFolder != null)
                {
                    try
                    {
                        await extractedFolder.DeleteAsync(StorageDeleteOption.PermanentDelete);
                    }
                    catch
                    {
                    }
                }
            }
        }
    }
}

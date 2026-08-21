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
        private async void OnImportVoiceMaterialClick(object sender, RoutedEventArgs e)
        {
            var picker = new FileOpenPicker();
            picker.ViewMode = PickerViewMode.List;
            picker.SuggestedStartLocation = PickerLocationId.MusicLibrary;
            picker.FileTypeFilter.Add(".wav");
            picker.FileTypeFilter.Add(".mp3");
            picker.FileTypeFilter.Add(".m4a");
            picker.FileTypeFilter.Add(".ogg");
            picker.FileTypeFilter.Add(".flac");

            IReadOnlyList<StorageFile> files = await picker.PickMultipleFilesAsync();
            if (files == null || files.Count == 0)
            {
                return;
            }

            int count = await PackCatalogService.ImportStagedMaterialsAsync(GameStyleService.Current, isAudio: true, files);
            bool isChinese = LocalizationManager.Current == UiLanguage.SimplifiedChinese;
            string gameName = isChinese ? GameStyleService.ToDisplayName(GameStyleService.Current) : GameStyleService.Current.ToString();
            string msg = isChinese
                ? $"已成功将 {count} 个语音素材导入到【{gameName}】的临时素材池！\\n\\n在新建或编辑语音包时，插槽选择框将优先展示这些素材供您自由指定。"
                : $"Successfully imported {count} voice materials to the staging pool for {gameName}!";

            var dialog = new Windows.UI.Popups.MessageDialog(msg, isChinese ? "语音素材导入完成" : "Materials Imported");
            await dialog.ShowAsync();
        }

        private async void OnImportIconMaterialClick(object sender, RoutedEventArgs e)
        {
            var picker = new FileOpenPicker();
            picker.ViewMode = PickerViewMode.Thumbnail;
            picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
            picker.FileTypeFilter.Add(".png");
            picker.FileTypeFilter.Add(".jpg");
            picker.FileTypeFilter.Add(".jpeg");
            picker.FileTypeFilter.Add(".webp");
            picker.FileTypeFilter.Add(".tga");

            IReadOnlyList<StorageFile> files = await picker.PickMultipleFilesAsync();
            if (files == null || files.Count == 0)
            {
                return;
            }

            int count = await PackCatalogService.ImportStagedMaterialsAsync(GameStyleService.Current, isAudio: false, files);
            bool isChinese = LocalizationManager.Current == UiLanguage.SimplifiedChinese;
            string gameName = isChinese ? GameStyleService.ToDisplayName(GameStyleService.Current) : GameStyleService.Current.ToString();
            string msg = isChinese
                ? $"已成功将 {count} 个图标素材导入到【{gameName}】的临时素材池！\\n\\n在新建或编辑图标包时，插槽选择框将优先展示这些素材供您自由指定。"
                : $"Successfully imported {count} icon materials to the staging pool for {gameName}!";

            var dialog = new Windows.UI.Popups.MessageDialog(msg, isChinese ? "图标素材导入完成" : "Materials Imported");
            await dialog.ShowAsync();
        }

        private async void OnImportVoicePackClick(object sender, RoutedEventArgs e)
        {
            var picker = new FolderPicker();
            picker.FileTypeFilter.Add("*");
            StorageFolder folder = await picker.PickSingleFolderAsync();
            if (folder == null)
            {
                return;
            }

            if (GameStyleService.Current == GameStyleMode.Dagoujiao)
            {
                await ShowCreateDagoujiaoVoicePackDialogAsync(
                    folder.DisplayName,
                    await CollectRecognizedFilesAsync(folder, DagoujiaoVoicePackImportFiles),
                    await TryGetCustomPackHeadImageAsync(folder.Path));
            }
            else if (GameStyleService.Current == GameStyleMode.Doubao)
            {
                await ShowCreateDoubaoVoicePackDialogAsync(
                    folder.DisplayName,
                    await CollectRecognizedFilesAsync(folder, DoubaoVoicePackImportFiles));
            }
            else if (GameStyleService.Current == GameStyleMode.Csol)
            {
                await ShowCreateCsolVoicePackDialogAsync(
                    folder.DisplayName,
                    await CollectRecognizedFilesAsync(folder, CsolVoicePackImportFiles),
                    await TryGetCustomPackHeadImageAsync(folder.Path));
            }
            else
            {
                await ShowCreateVoicePackDialogAsync(
                    folder.DisplayName,
                    await CollectRecognizedFilesAsync(folder, VoicePackImportFiles),
                    await TryGetAudioFileAsync(folder, "common_overlay"),
                    await TryGetCustomPackHeadImageAsync(folder.Path));
            }
        }

        private async void OnImportVoiceZipClick(object sender, RoutedEventArgs e)
        {
            if (GameStyleService.Current == GameStyleMode.Dagoujiao)
            {
                await ImportPackFromZipAsync(
                    DagoujiaoVoicePackImportFiles,
                    async (folder, files) =>
                    {
                        await ShowCreateDagoujiaoVoicePackDialogAsync(
                            folder.DisplayName,
                            files,
                            await TryGetCustomPackHeadImageAsync(folder.Path));
                    });
            }
            else if (GameStyleService.Current == GameStyleMode.Doubao)
            {
                await ImportPackFromZipAsync(
                    DoubaoVoicePackImportFiles,
                    async (folder, files) =>
                    {
                        await ShowCreateDoubaoVoicePackDialogAsync(folder.DisplayName, files);
                    });
            }
            else if (GameStyleService.Current == GameStyleMode.Csol)
            {
                await ImportPackFromZipAsync(
                    CsolVoicePackImportFiles,
                    async (folder, files) =>
                    {
                        await ShowCreateCsolVoicePackDialogAsync(
                            folder.DisplayName,
                            files,
                            await TryGetCustomPackHeadImageAsync(folder.Path));
                    });
            }
            else
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

            if (GameStyleService.Current == GameStyleMode.Dagoujiao)
            {
                await ShowCreateDagoujiaoIconPackDialogAsync(
                    folder.DisplayName,
                    await CollectRecognizedFilesAsync(folder, DagoujiaoIconPackImportFiles));
            }
            else if (GameStyleService.Current == GameStyleMode.Doubao)
            {
                await ShowCreateDoubaoIconPackDialogAsync(
                    folder.DisplayName,
                    await CollectRecognizedFilesAsync(folder, DoubaoIconPackImportFiles));
            }
            else if (GameStyleService.Current == GameStyleMode.Csol)
            {
                await ShowCreateCsolIconPackDialogAsync(
                    folder.DisplayName,
                    await CollectRecognizedFilesAsync(folder, CsolIconPackImportFiles));
            }
            else
            {
                await ShowCreateIconPackDialogAsync(
                    folder.DisplayName,
                    await CollectRecognizedFilesAsync(folder, IconPackImportFiles));
            }
        }

        private async void OnImportIconZipClick(object sender, RoutedEventArgs e)
        {
            if (GameStyleService.Current == GameStyleMode.Dagoujiao)
            {
                await ImportPackFromZipAsync(
                    DagoujiaoIconPackImportFiles,
                    async (folder, files) =>
                    {
                        await ShowCreateDagoujiaoIconPackDialogAsync(folder.DisplayName, files);
                    });
            }
            else if (GameStyleService.Current == GameStyleMode.Doubao)
            {
                await ImportPackFromZipAsync(
                    DoubaoIconPackImportFiles,
                    async (folder, files) =>
                    {
                        await ShowCreateDoubaoIconPackDialogAsync(folder.DisplayName, files);
                    });
            }
            else if (GameStyleService.Current == GameStyleMode.Csol)
            {
                await ImportPackFromZipAsync(
                    CsolIconPackImportFiles,
                    async (folder, files) =>
                    {
                        await ShowCreateCsolIconPackDialogAsync(folder.DisplayName, files);
                    });
            }
            else
            {
                await ImportPackFromZipAsync(
                    IconPackImportFiles,
                    async (folder, files) =>
                    {
                        await ShowCreateIconPackDialogAsync(folder.DisplayName, files);
                    });
            }
        }

        private async void OnCreateVoicePackClick(object sender, RoutedEventArgs e)
        {
            if (GameStyleService.Current == GameStyleMode.Dagoujiao)
            {
                await ShowCreateDagoujiaoVoicePackDialogAsync();
            }
            else if (GameStyleService.Current == GameStyleMode.Doubao)
            {
                await ShowCreateDoubaoVoicePackDialogAsync();
            }
            else if (GameStyleService.Current == GameStyleMode.Csol)
            {
                await ShowCreateCsolVoicePackDialogAsync();
            }
            else
            {
                await ShowCreateVoicePackDialogAsync();
            }
        }

        private async void OnCreateIconPackClick(object sender, RoutedEventArgs e)
        {
            if (GameStyleService.Current == GameStyleMode.Dagoujiao)
            {
                await ShowCreateDagoujiaoIconPackDialogAsync();
            }
            else if (GameStyleService.Current == GameStyleMode.Doubao)
            {
                await ShowCreateDoubaoIconPackDialogAsync();
            }
            else if (GameStyleService.Current == GameStyleMode.Csol)
            {
                await ShowCreateCsolIconPackDialogAsync();
            }
            else
            {
                await ShowCreateIconPackDialogAsync();
            }
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

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

            IReadOnlyList<StorageFile> files = await picker.PickMultipleFilesAsync();
            if (files == null || files.Count == 0)
            {
                return;
            }

            int count = await PackCatalogService.ImportStagedMaterialsAsync(GameStyleService.Current, isAudio: true, files);
            bool isChinese = LocalizationManager.Current == UiLanguage.SimplifiedChinese;
            string gameName = isChinese ? GameStyleService.ToDisplayName(GameStyleService.Current) : GameStyleService.Current.ToString();
            string msg = isChinese
                ? $"已为【{gameName}】导入 {count} 个语音文件。\\n\\n新建或编辑语音包时，可以直接选择这些文件。"
                : $"Imported {count} audio files for {gameName}.\\n\\nYou can select them when creating or editing a voice pack.";

            var dialog = new Windows.UI.Popups.MessageDialog(msg, isChinese ? "语音素材导入完成" : "Materials Imported");
            await dialog.ShowAsync();
        }

        private async void OnImportIconMaterialClick(object sender, RoutedEventArgs e)
        {
            if (await GuardIconPackCreationAsync())
            {
                return;
            }

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
                ? $"已为【{gameName}】导入 {count} 个图标文件。\\n\\n新建或编辑图标包时，可以直接选择这些文件。"
                : $"Imported {count} icon files for {gameName}.\\n\\nYou can select them when creating or editing an icon pack.";

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
                    await CollectVoiceFileGroupsFromManifestAsync(folder, PackCatalogService.DagoujiaoSlotMapping),
                    await TryGetCustomPackHeadImageAsync(folder.Path));
            }
            else if (GameStyleService.Current == GameStyleMode.Doubao)
            {
                await ShowCreateDoubaoVoicePackDialogAsync(
                    folder.DisplayName,
                    await CollectVoiceFileGroupsFromManifestAsync(folder, PackCatalogService.DoubaoSlotMapping));
            }
            else if (GameStyleService.Current == GameStyleMode.Csol)
            {
                await ShowCreateCsolVoicePackDialogAsync(
                    folder.DisplayName,
                    await CollectVoiceFileGroupsFromManifestAsync(folder, PackCatalogService.CsolSlotMapping),
                    await TryGetCustomPackHeadImageAsync(folder.Path));
            }
            else if (GameStyleService.Current == GameStyleMode.Valorant)
            {
                await ShowCreateValorantVoicePackDialogAsync(
                    folder.DisplayName,
                    await CollectVoiceFileGroupsFromManifestAsync(folder, PackCatalogService.ValorantVoiceSlotMapping),
                    await TryGetCustomPackHeadImageAsync(folder.Path));
            }
            else if (GameStyleService.Current == GameStyleMode.Overwatch)
            {
                await ShowCreateOverwatchVoicePackDialogAsync(
                    folder.DisplayName,
                    await CollectVoiceFileGroupsFromManifestAsync(folder, PackCatalogService.OverwatchVoiceSlotMapping),
                    await TryGetCustomPackHeadImageAsync(folder.Path));
            }
            else if (GameStyleService.Current == GameStyleMode.ModernWarfare2019)
            {
                await ShowCreateModernWarfare2019VoicePackDialogAsync(
                    folder.DisplayName,
                    await CollectVoiceFileGroupsFromManifestAsync(folder, PackCatalogService.ModernWarfare2019VoiceSlotMapping),
                    await TryGetCustomPackHeadImageAsync(folder.Path));
            }
            else if (IsEventVoiceGame(GameStyleService.Current))
            {
                await ShowCreateEventVoicePackDialogAsync(
                    GameStyleService.Current,
                    folder.DisplayName,
                    await CollectVoiceFileGroupsFromManifestAsync(folder, PackCatalogService.EventSlotMapping),
                    await TryGetCustomPackHeadImageAsync(folder.Path));
            }
            else
            {
                await ShowCreateVoicePackDialogAsync(
                    folder.DisplayName,
                    await CollectVoiceFileGroupsFromManifestAsync(folder, PackCatalogService.CrossfireSlotMapping),
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
                            await CollectVoiceFileGroupsFromManifestAsync(folder, PackCatalogService.DagoujiaoSlotMapping),
                            await TryGetCustomPackHeadImageAsync(folder.Path));
                    });
            }
            else if (GameStyleService.Current == GameStyleMode.Doubao)
            {
                await ImportPackFromZipAsync(
                    DoubaoVoicePackImportFiles,
                    async (folder, files) =>
                    {
                        await ShowCreateDoubaoVoicePackDialogAsync(
                            folder.DisplayName,
                            await CollectVoiceFileGroupsFromManifestAsync(folder, PackCatalogService.DoubaoSlotMapping));
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
                            await CollectVoiceFileGroupsFromManifestAsync(folder, PackCatalogService.CsolSlotMapping),
                            await TryGetCustomPackHeadImageAsync(folder.Path));
                    });
            }
            else if (GameStyleService.Current == GameStyleMode.Valorant)
            {
                await ImportPackFromZipAsync(
                    ValorantVoicePackImportFiles,
                    async (folder, files) =>
                    {
                        await ShowCreateValorantVoicePackDialogAsync(
                            folder.DisplayName,
                            await CollectVoiceFileGroupsFromManifestAsync(folder, PackCatalogService.ValorantVoiceSlotMapping),
                            await TryGetCustomPackHeadImageAsync(folder.Path));
                    });
            }
            else if (GameStyleService.Current == GameStyleMode.Overwatch)
            {
                await ImportPackFromZipAsync(
                    OverwatchVoicePackImportFiles,
                    async (folder, files) =>
                    {
                        await ShowCreateOverwatchVoicePackDialogAsync(
                            folder.DisplayName,
                            await CollectVoiceFileGroupsFromManifestAsync(folder, PackCatalogService.OverwatchVoiceSlotMapping),
                            await TryGetCustomPackHeadImageAsync(folder.Path));
                    });
            }
            else if (GameStyleService.Current == GameStyleMode.ModernWarfare2019)
            {
                await ImportPackFromZipAsync(
                    ModernWarfare2019VoicePackImportFiles,
                    async (folder, files) =>
                    {
                        await ShowCreateModernWarfare2019VoicePackDialogAsync(
                            folder.DisplayName,
                            await CollectVoiceFileGroupsFromManifestAsync(folder, PackCatalogService.ModernWarfare2019VoiceSlotMapping),
                            await TryGetCustomPackHeadImageAsync(folder.Path));
                    });
            }
            else if (IsEventVoiceGame(GameStyleService.Current))
            {
                await ImportPackFromZipAsync(
                    EventVoicePackImportFiles,
                    async (folder, files) =>
                    {
                        await ShowCreateEventVoicePackDialogAsync(
                            GameStyleService.Current,
                            folder.DisplayName,
                            await CollectVoiceFileGroupsFromManifestAsync(folder, PackCatalogService.EventSlotMapping),
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
                            await CollectVoiceFileGroupsFromManifestAsync(folder, PackCatalogService.CrossfireSlotMapping),
                            await TryGetAudioFileAsync(folder, "common_overlay"),
                            await TryGetCustomPackHeadImageAsync(folder.Path));
                    });
            }
        }

        private async void OnImportIconPackClick(object sender, RoutedEventArgs e)
        {
            if (await GuardIconPackCreationAsync())
            {
                return;
            }

            var picker = new FolderPicker();
            picker.FileTypeFilter.Add("*");
            StorageFolder folder = await picker.PickSingleFolderAsync();
            if (folder == null)
            {
                return;
            }

            StorageFile headImage = await TryGetCustomPackHeadImageAsync(folder.Path);

            if (GameStyleService.Current == GameStyleMode.Dagoujiao)
            {
                await ShowCreateDagoujiaoIconPackDialogAsync(
                    folder.DisplayName,
                    await CollectRecognizedFilesAsync(folder, DagoujiaoIconPackImportFiles),
                    headImage);
            }
            else if (GameStyleService.Current == GameStyleMode.Doubao)
            {
                await ShowCreateDoubaoIconPackDialogAsync(
                    folder.DisplayName,
                    await CollectRecognizedFilesAsync(folder, DoubaoIconPackImportFiles),
                    headImage);
            }
            else if (GameStyleService.Current == GameStyleMode.Csol)
            {
                await ShowCreateCsolIconPackDialogAsync(
                    folder.DisplayName,
                    await CollectRecognizedFilesAsync(folder, CsolIconPackImportFiles),
                    headImage);
            }
            else if (GameStyleService.Current == GameStyleMode.Battlefield1)
            {
                await ShowCreateBattlefield1IconPackDialogAsync(
                    folder.DisplayName,
                    await CollectRecognizedFilesAsync(folder, Battlefield1IconPackImportFiles),
                    headImage);
            }
            else if (GameStyleService.Current == GameStyleMode.Battlefield5)
            {
                await ShowCreateBattlefield5IconPackDialogAsync(
                    folder.DisplayName,
                    await CollectRecognizedFilesAsync(folder, Battlefield5IconPackImportFiles),
                    headImage);
            }
            else if (GameStyleService.Current == GameStyleMode.Battlefield2042)
            {
                await ShowCreateBattlefield2042IconPackDialogAsync(
                    folder.DisplayName,
                    await CollectRecognizedFilesAsync(folder, Battlefield2042IconPackImportFiles),
                    headImage);
            }
            else if (GameStyleService.Current == GameStyleMode.DeltaForce)
            {
                await ShowCreateDeltaForceIconPackDialogAsync(
                    folder.DisplayName,
                    await CollectRecognizedFilesAsync(folder, DeltaForceIconPackImportFiles),
                    headImage);
            }
            else
            {
                await ShowCreateIconPackDialogAsync(
                    folder.DisplayName,
                    await CollectRecognizedFilesAsync(folder, IconPackImportFiles),
                    headImage);
            }
        }

        private async void OnImportIconZipClick(object sender, RoutedEventArgs e)
        {
            if (await GuardIconPackCreationAsync())
            {
                return;
            }

            if (GameStyleService.Current == GameStyleMode.Dagoujiao)
            {
                await ImportPackFromZipAsync(
                    DagoujiaoIconPackImportFiles,
                    async (folder, files) =>
                    {
                        await ShowCreateDagoujiaoIconPackDialogAsync(
                            folder.DisplayName, files,
                            await TryGetCustomPackHeadImageAsync(folder.Path));
                    });
            }
            else if (GameStyleService.Current == GameStyleMode.Doubao)
            {
                await ImportPackFromZipAsync(
                    DoubaoIconPackImportFiles,
                    async (folder, files) =>
                    {
                        await ShowCreateDoubaoIconPackDialogAsync(
                            folder.DisplayName, files,
                            await TryGetCustomPackHeadImageAsync(folder.Path));
                    });
            }
            else if (GameStyleService.Current == GameStyleMode.Csol)
            {
                await ImportPackFromZipAsync(
                    CsolIconPackImportFiles,
                    async (folder, files) =>
                    {
                        await ShowCreateCsolIconPackDialogAsync(
                            folder.DisplayName, files,
                            await TryGetCustomPackHeadImageAsync(folder.Path));
                    });
            }
            else if (GameStyleService.Current == GameStyleMode.Battlefield1)
            {
                await ImportPackFromZipAsync(
                    Battlefield1IconPackImportFiles,
                    async (folder, files) =>
                    {
                        await ShowCreateBattlefield1IconPackDialogAsync(
                            folder.DisplayName, files,
                            await TryGetCustomPackHeadImageAsync(folder.Path));
                    });
            }
            else if (GameStyleService.Current == GameStyleMode.Battlefield5)
            {
                await ImportPackFromZipAsync(
                    Battlefield5IconPackImportFiles,
                    async (folder, files) =>
                    {
                        await ShowCreateBattlefield5IconPackDialogAsync(
                            folder.DisplayName, files,
                            await TryGetCustomPackHeadImageAsync(folder.Path));
                    });
            }
            else if (GameStyleService.Current == GameStyleMode.Battlefield2042)
            {
                await ImportPackFromZipAsync(
                    Battlefield2042IconPackImportFiles,
                    async (folder, files) =>
                    {
                        await ShowCreateBattlefield2042IconPackDialogAsync(
                            folder.DisplayName, files,
                            await TryGetCustomPackHeadImageAsync(folder.Path));
                    });
            }
            else if (GameStyleService.Current == GameStyleMode.DeltaForce)
            {
                await ImportPackFromZipAsync(
                    DeltaForceIconPackImportFiles,
                    async (folder, files) =>
                    {
                        await ShowCreateDeltaForceIconPackDialogAsync(
                            folder.DisplayName, files,
                            await TryGetCustomPackHeadImageAsync(folder.Path));
                    });
            }
            else
            {
                await ImportPackFromZipAsync(
                    IconPackImportFiles,
                    async (folder, files) =>
                    {
                        await ShowCreateIconPackDialogAsync(
                            folder.DisplayName, files,
                            await TryGetCustomPackHeadImageAsync(folder.Path));
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
            else if (GameStyleService.Current == GameStyleMode.Valorant)
            {
                await ShowCreateValorantVoicePackDialogAsync();
            }
            else if (GameStyleService.Current == GameStyleMode.Overwatch)
            {
                await ShowCreateOverwatchVoicePackDialogAsync();
            }
            else if (GameStyleService.Current == GameStyleMode.ModernWarfare2019)
            {
                await ShowCreateModernWarfare2019VoicePackDialogAsync();
            }
            else if (IsEventVoiceGame(GameStyleService.Current))
            {
                await ShowCreateEventVoicePackDialogAsync(GameStyleService.Current);
            }
            else
            {
                await ShowCreateVoicePackDialogAsync();
            }
        }

        private async void OnCreateIconPackClick(object sender, RoutedEventArgs e)
        {
            if (await GuardIconPackCreationAsync())
            {
                return;
            }

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
            else if (GameStyleService.Current == GameStyleMode.Battlefield1)
            {
                await ShowCreateBattlefield1IconPackDialogAsync();
            }
            else if (GameStyleService.Current == GameStyleMode.Battlefield5)
            {
                await ShowCreateBattlefield5IconPackDialogAsync();
            }
            else if (GameStyleService.Current == GameStyleMode.Battlefield2042)
            {
                await ShowCreateBattlefield2042IconPackDialogAsync();
            }
            else if (GameStyleService.Current == GameStyleMode.DeltaForce)
            {
                await ShowCreateDeltaForceIconPackDialogAsync();
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
                StorageFile manifestFile = await TryGetFileAsync(bestFolder, "manifest.json");
                if (files.Count == 0 && manifestFile == null)
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

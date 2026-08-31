using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using KillConfirmGameBar.Helpers;
using KillConfirmGameBar.Services;
using Windows.ApplicationModel.DataTransfer;
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
        private StorageFile _providedPackZipFile;
        private bool _packZipDropInProgress;

        private void OnVoicePackLibraryDragOver(object sender, DragEventArgs e)
        {
            ConfigurePackZipDragOver(e, isVoice: true);
        }

        private void OnIconPackLibraryDragOver(object sender, DragEventArgs e)
        {
            ConfigurePackZipDragOver(e, isVoice: false);
        }

        private static void ConfigurePackZipDragOver(DragEventArgs e, bool isVoice)
        {
            if (!e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                return;
            }

            bool chinese = LocalizationManager.Current == UiLanguage.SimplifiedChinese;
            e.AcceptedOperation = DataPackageOperation.Copy;
            e.DragUIOverride.Caption = chinese
                ? (isVoice ? "释放以导入语音 ZIP" : "释放以导入图标 ZIP")
                : (isVoice ? "Drop to import voice ZIP" : "Drop to import icon ZIP");
            e.DragUIOverride.IsCaptionVisible = true;
            e.Handled = true;
        }

        private async void OnVoicePackLibraryDrop(object sender, DragEventArgs e)
        {
            await ImportDroppedPackZipsAsync(e, isVoice: true);
        }

        private async void OnIconPackLibraryDrop(object sender, DragEventArgs e)
        {
            await ImportDroppedPackZipsAsync(e, isVoice: false);
        }

        private async Task ImportDroppedPackZipsAsync(DragEventArgs e, bool isVoice)
        {
            var deferral = e.GetDeferral();
            e.Handled = true;
            try
            {
                if (_packZipDropInProgress)
                {
                    await ShowMessageAsync(
                        LocalizationManager.Current == UiLanguage.SimplifiedChinese ? "正在导入" : "Import in progress",
                        LocalizationManager.Current == UiLanguage.SimplifiedChinese
                            ? "请等待当前压缩包导入完成。"
                            : "Wait for the current ZIP import to finish.");
                    return;
                }

                IReadOnlyList<IStorageItem> items = await e.DataView.GetStorageItemsAsync();
                List<StorageFile> zipFiles = items
                    .OfType<StorageFile>()
                    .Where(file => string.Equals(file.FileType, ".zip", StringComparison.OrdinalIgnoreCase))
                    .ToList();
                if (zipFiles.Count == 0)
                {
                    await ShowMessageAsync(
                        LocalizationManager.Current == UiLanguage.SimplifiedChinese ? "无法导入" : "Cannot import",
                        LocalizationManager.Current == UiLanguage.SimplifiedChinese
                            ? "请把 ZIP 压缩包拖到资源包库区域。"
                            : "Drop one or more ZIP archives onto the pack library.");
                    return;
                }

                _packZipDropInProgress = true;
                foreach (StorageFile zipFile in zipFiles)
                {
                    _providedPackZipFile = zipFile;
                    try
                    {
                        if (isVoice)
                        {
                            await ImportVoiceZipForCurrentStyleAsync();
                        }
                        else
                        {
                            await ImportIconZipForCurrentStyleAsync();
                        }
                    }
                    finally
                    {
                        _providedPackZipFile = null;
                    }
                }
            }
            catch (Exception ex)
            {
                App.Log("Pack ZIP drop import failed: " + ex);
                await ShowMessageAsync(
                    LocalizationManager.Current == UiLanguage.SimplifiedChinese ? "导入失败" : "Import failed",
                    ex.Message);
            }
            finally
            {
                _providedPackZipFile = null;
                _packZipDropInProgress = false;
                deferral.Complete();
            }
        }

        private async void OnCreateIconPackClick(object sender, RoutedEventArgs e)
        {
            if (GameStyleService.Current == GameStyleMode.CustomModule) { await ShowCustomModuleEditorAsync(null); return; }
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
            else if (GameStyleService.Current == GameStyleMode.Overwatch)
            {
                await ShowCreateOverwatchIconPackDialogAsync(null, null, null);
            }
            else if (GameStyleService.Current == GameStyleMode.ModernWarfare2019)
            {
                await ShowCreateModernWarfare2019IconPackDialogAsync(null, null, null);
            }
            else if (GameStyleService.Current == GameStyleMode.Apex)
            {
                await ShowCreateApexIconPackDialogAsync(null, null, null);
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
            StorageFile zipFile = _providedPackZipFile
                ?? await PickSingleFileAsync(new[] { ".zip" });
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
            catch (Exception ex)
            {
                App.Log("Pack ZIP import failed: " + ex);
                await ShowMessageAsync(
                    LocalizationManager.Text("ZipImportFailedTitle"),
                    LocalizationManager.Text("ZipImportFailedMessage") + "\n\n" + ex.Message);
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

        private async Task ImportValorantPackageFromZipAsync(string packageKind)
        {
            StorageFile zipFile = _providedPackZipFile
                ?? await PickSingleFileAsync(new[] { ".zip" });
            if (zipFile == null)
            {
                return;
            }

            StorageFolder extractedFolder = null;
            try
            {
                extractedFolder = await ExtractZipToTemporaryFolderAsync(zipFile);
                if (string.Equals(packageKind, ValorantExternalAssetService.VoicePackageKind, StringComparison.Ordinal)
                    && !await ValorantExternalAssetService.IsPackageKindAsync(extractedFolder, packageKind))
                {
                    StorageFolder bestFolder = await FindBestPackFolderAsync(
                        extractedFolder,
                        ValorantVoicePackImportFiles);
                    IReadOnlyDictionary<string, IReadOnlyList<StorageFile>> voiceFiles =
                        await CollectVoiceFileGroupsFromManifestAsync(
                            bestFolder,
                            PackCatalogService.ValorantVoiceSlotMapping);
                    if (voiceFiles.Count == 0)
                    {
                        throw new InvalidDataException(
                            LocalizationManager.Current == UiLanguage.SimplifiedChinese
                                ? "压缩包中没有找到 1～5 杀或爆头语音。"
                                : "No kill 1-5 or headshot audio was found in the archive.");
                    }

                    await ShowCreateValorantVoicePackDialogAsync(
                        bestFolder.DisplayName,
                        voiceFiles,
                        await TryGetCustomPackHeadImageAsync(bestFolder.Path));
                    return;
                }

                ValorantPackageInstallResult installed = await ValorantExternalAssetService.InstallPackageAsync(
                    extractedFolder,
                    packageKind);
                await PackCatalogService.RefreshValorantExternalPacksAsync();
                bool chinese = LocalizationManager.Current == UiLanguage.SimplifiedChinese;
                await ShowMessageAsync(
                    chinese ? "瓦资源包已导入" : "VALORANT package imported",
                    (chinese ? "已安装：" : "Installed: ") + installed.DisplayName);
            }
            catch (Exception ex)
            {
                App.Log("VALORANT package import failed: " + ex);
                await ShowMessageAsync(
                    LocalizationManager.Current == UiLanguage.SimplifiedChinese ? "导入失败" : "Import failed",
                    LocalizationManager.Current == UiLanguage.SimplifiedChinese
                        ? "这不是有效的瓦资源包，或包内素材不完整。\n\n" + ex.Message
                        : "This is not a valid VALORANT package, or required material is missing.\n\n" + ex.Message);
            }
            finally
            {
                if (extractedFolder != null)
                {
                    try { await extractedFolder.DeleteAsync(StorageDeleteOption.PermanentDelete); } catch { }
                }
            }
        }

        private async Task ImportValorantPackageFromFolderAsync(StorageFolder folder, string packageKind)
        {
            try
            {
                ValorantPackageInstallResult installed = await ValorantExternalAssetService.InstallPackageAsync(
                    folder,
                    packageKind);
                await PackCatalogService.RefreshValorantExternalPacksAsync();
                bool chinese = LocalizationManager.Current == UiLanguage.SimplifiedChinese;
                await ShowMessageAsync(
                    chinese ? "瓦资源包已导入" : "VALORANT package imported",
                    (chinese ? "已安装：" : "Installed: ") + installed.DisplayName);
            }
            catch (Exception ex)
            {
                App.Log("VALORANT folder package import failed: " + ex);
                await ShowMessageAsync(
                    LocalizationManager.Current == UiLanguage.SimplifiedChinese ? "导入失败" : "Import failed",
                    LocalizationManager.Current == UiLanguage.SimplifiedChinese
                        ? "所选文件夹不是有效的瓦外部资源包。\n\n" + ex.Message
                        : "The selected folder is not a valid VALORANT external package.\n\n" + ex.Message);
            }
        }
    }
}

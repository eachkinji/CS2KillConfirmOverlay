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

        private async Task ImportValorantPackageFromZipAsync(string packageKind)
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
                        ? "这不是有效的瓦外部资源包，或包内素材不完整。"
                        : "This is not a valid VALORANT external package, or required material is missing.");
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
                        ? "所选文件夹不是有效的瓦外部资源包。"
                        : "The selected folder is not a valid VALORANT external package.");
            }
        }
    }
}

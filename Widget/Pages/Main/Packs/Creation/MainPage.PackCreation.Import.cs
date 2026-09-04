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
        private bool _batchPackImport;
        private readonly List<string> _packImportNotes = new List<string>();

        private async void OnBatchImportVoiceZipClick(object sender, RoutedEventArgs e)
            => await PickAndImportPackFilesAsync(isVoice: true, multiple: true);

        private async void OnBatchImportIconZipClick(object sender, RoutedEventArgs e)
            => await PickAndImportPackFilesAsync(isVoice: false, multiple: true);

        private void SetPackImportBusy(bool busy)
        {
            _packZipDropInProgress = busy;
            ImportVoiceZipButton.IsEnabled = !busy;
            BatchImportVoiceZipButton.IsEnabled = !busy;
            ImportIconZipButton.IsEnabled = !busy;
            BatchImportIconZipButton.IsEnabled = !busy;
            CreateVoicePackButton.IsEnabled = !busy;
            CreateIconPackButton.IsEnabled = !busy;
            GameStyleSidebarSelector.IsEnabled = !busy;
            if (!busy)
            {
                _loadedVoicePackStyle = null;
                _loadedIconPackStyle = null;
                _ = EnsureActivePackListLoadedAsync();
            }
        }

        private async Task PickAndImportPackFilesAsync(bool isVoice, bool multiple)
        {
            if (_packZipDropInProgress) return;
            SetPackImportBusy(true);
            try
            {
                var picker = new FileOpenPicker();
                picker.FileTypeFilter.Add(".zip");
                if (multiple)
                {
                    await ImportSelectedPackFilesAsync(await picker.PickMultipleFilesAsync(), isVoice, true);
                }
                else
                {
                    StorageFile file = await picker.PickSingleFileAsync();
                    if (file != null) await ImportSelectedPackFilesAsync(new[] { file }, isVoice, false);
                }
            }
            catch (Exception ex)
            {
                await ShowMessageAsync(LocalizationManager.Text("ZipImportFailedTitle"), ex.Message);
            }
            finally { SetPackImportBusy(false); }
        }

        // All entry points share one queue. Only its owner releases the busy state.
        private async Task ImportSelectedPackFilesAsync(IReadOnlyList<StorageFile> files, bool isVoice, bool batch)
        {
            if (files.Count == 0) return;
            bool chinese = LocalizationManager.Current == UiLanguage.SimplifiedChinese;
            if (!isVoice && GameStyleService.Current != GameStyleMode.CustomModule
                && GameStyleService.Current != GameStyleMode.Valorant
                && await GuardIconPackCreationAsync()) return;
            _batchPackImport = batch;
            _packImportNotes.Clear();
            PackImportQueueResult result = null;
            string title = chinese ? (isVoice ? "批量导入音频包" : "批量导入图标包")
                : (isVoice ? "Import audio packs" : "Import icon packs");
            var theme = GameThemePalette.Current;
            var progress = new TextBlock { TextWrapping = TextWrapping.Wrap, FontSize = 14, Foreground = new SolidColorBrush(theme.Text) };
            var progressBar = new ProgressBar { Minimum = 0, Maximum = files.Count, Height = 6, Foreground = new SolidColorBrush(theme.Accent), Background = new SolidColorBrush(theme.AccentSoft) };
            var status = new StackPanel { Spacing = 16 };
            status.Children.Add(progress);
            status.Children.Add(progressBar);
            status.Children.Add(new TextBlock
            {
                Text = chinese ? "正在整理包内素材，请稍候。完成后会显示导入结果。" : "Preparing pack files. Results will appear when the import finishes.",
                FontSize = 12, Foreground = new SolidColorBrush(theme.MutedText), TextWrapping = TextWrapping.Wrap
            });
            var dialog = CreatePackStatusDialog(title, status);
            bool running = true;
            dialog.Closing += (sender, args) => args.Cancel = running;
            Windows.Foundation.IAsyncOperation<ContentDialogResult> showing = null;
            try
            {
                if (batch) showing = dialog.ShowAsync();
                result = await PackImportQueue.RunAsync(files, file => file.Name, async (file, index) =>
                {
                    _providedPackZipFile = file;
                    progress.Text = $"{index + 1} / {files.Count}\n{file.Name}";
                    try
                    {
                        if (isVoice) await ImportVoiceZipForCurrentStyleAsync();
                        else await ImportIconZipForCurrentStyleAsync();
                    }
                    finally { _providedPackZipFile = null; progressBar.Value = index + 1; }
                });
            }
            finally
            {
                _providedPackZipFile = null;
                _batchPackImport = false;
                running = false;
                if (showing != null) { dialog.Hide(); await showing; }
            }
            if (batch)
            {
                if (GameStyleService.Current == GameStyleMode.Valorant)
                    await PackCatalogService.RefreshValorantExternalPacksAsync();
                string summary = chinese ? $"导入完成：成功 {result.Succeeded} 个，失败 {result.Failures.Count} 个。"
                    : $"Import complete: {result.Succeeded} succeeded, {result.Failures.Count} failed.";
                var details = result.Failures.Concat(_packImportNotes).ToList();
                if (details.Count > 0) summary += "\n\n" + string.Join("\n", details);
                await ShowMessageAsync(title, summary);
            }
            else if (result.Failures.Count > 0)
                await ShowMessageAsync(LocalizationManager.Text("ZipImportFailedTitle"), string.Join("\n", result.Failures));
        }

        private async Task<bool> TryBatchImportIconAsync(
            IReadOnlyDictionary<string, StorageFile> files, StorageFile head,
            Func<string, IReadOnlyDictionary<string, StorageFile>, StorageFile, Task> create)
        {
            if (!_batchPackImport) return false;
            if (files == null || files.Count == 0)
                throw new InvalidDataException(LocalizationManager.Text("ZipImportNoFilesMessage"));
            await create(_providedPackZipFile.DisplayName, files, head);
            return true;
        }

        private async Task<bool> TryBatchImportVoiceAsync(
            IReadOnlyDictionary<string, IReadOnlyList<StorageFile>> files, StorageFile head,
            Func<string, VoicePackBuildOptions, Task> create, StorageFile commonOverlay = null)
        {
            if (!_batchPackImport) return false;
            if (files == null || !files.Values.Any(group => group != null && group.Count > 0))
                throw new InvalidDataException(LocalizationManager.Text("ZipImportNoFilesMessage"));
            await create(_providedPackZipFile.DisplayName, new VoicePackBuildOptions
            {
                SelectedFileGroups = files,
                HeadImageFile = head,
                CommonOverlayFile = commonOverlay
            });
            return true;
        }

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
                ? (isVoice ? "释放以导入音频包" : "释放以导入图标包")
                : (isVoice ? "Drop to import audio packs" : "Drop to import icon packs");
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
            e.Handled = true;
            if (_packZipDropInProgress) return;
            var deferral = e.GetDeferral();
            SetPackImportBusy(true);
            try
            {
                var items = await e.DataView.GetStorageItemsAsync();
                var files = items.OfType<StorageFile>()
                    .Where(file => string.Equals(file.FileType, ".zip", StringComparison.OrdinalIgnoreCase)).ToList();
                if (files.Count == 0)
                    throw new InvalidDataException(LocalizationManager.Current == UiLanguage.SimplifiedChinese
                        ? (isVoice ? "请选择音频包文件。" : "请选择图标包文件。")
                        : "Select pack files to import.");
                await ImportSelectedPackFilesAsync(files, isVoice, files.Count > 1);
            }
            catch (Exception ex)
            {
                await ShowMessageAsync(LocalizationManager.Text("ZipImportFailedTitle"), ex.Message);
            }
            finally
            {
                SetPackImportBusy(false);
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
                if (GameStyleService.Current == GameStyleMode.Crossfire
                    && await CrossfireExternalAssetService.TryInstallAsync(bestFolder,
                        !recognizedFileNames.Any(name => name.EndsWith(".png", StringComparison.OrdinalIgnoreCase))))
                    return;
                IReadOnlyDictionary<string, StorageFile> files = await CollectRecognizedFilesAsync(bestFolder, recognizedFileNames.ToArray());
                StorageFile manifestFile = await TryGetFileAsync(bestFolder, "manifest.json");
                if (files.Count == 0 && manifestFile == null)
                {
                    if (_batchPackImport) throw new InvalidDataException(LocalizationManager.Text("ZipImportNoFilesMessage"));
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
                if (_batchPackImport) throw;
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
                                ? "音频包中没有找到 1～5 杀或爆头语音。"
                                : "No kill 1-5 or headshot audio was found in the audio pack.");
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
                if (_batchPackImport) return;
                await PackCatalogService.RefreshValorantExternalPacksAsync();
                bool chinese = LocalizationManager.Current == UiLanguage.SimplifiedChinese;
                await ShowMessageAsync(
                    chinese ? "瓦资源包已导入" : "VALORANT package imported",
                    (chinese ? "已安装：" : "Installed: ") + installed.DisplayName);
            }
            catch (Exception ex)
            {
                App.Log("VALORANT package import failed: " + ex);
                if (_batchPackImport) throw;
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
                if (_batchPackImport) return;
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

    internal sealed class PackImportQueueResult
    {
        public int Succeeded { get; set; }
        public List<string> Failures { get; } = new List<string>();
    }

    internal static class PackImportQueue
    {
        public static async Task<PackImportQueueResult> RunAsync<T>(
            IReadOnlyList<T> files, Func<T, string> name, Func<T, int, Task> import)
        {
            var result = new PackImportQueueResult();
            for (int index = 0; index < files.Count; index++)
            {
                try
                {
                    await import(files[index], index);
                    result.Succeeded++;
                }
                catch (Exception ex)
                {
                    App.Log("Pack import failed: " + name(files[index]) + ": " + ex);
                    result.Failures.Add(name(files[index]) + ": " + ex.Message);
                }
            }
            return result;
        }
    }
}

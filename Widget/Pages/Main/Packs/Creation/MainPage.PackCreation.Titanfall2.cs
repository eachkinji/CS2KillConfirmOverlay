using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using KillConfirmGameBar.Services;
using Windows.Data.Json;
using Windows.Storage;

namespace KillConfirmGameBar
{
    public sealed partial class MainPage
    {
        private static readonly string[] Titanfall2IconPackImportFiles =
        {
            "badge_kill_pilot.png",
            "badge_headshot.png",
            "badge_double_kill.png",
            "badge_multi_kill.png",
            "badge_kill_titan.png",
            "badge_first_strike.png",
            "badge_victory.png",
            "badge_wings.png",
            "badge_titanfall.png",
            "badge_eject.png",
            "hit_indicator.png",
            "hit_indicator_crit_bg.png",
        };

        private async Task ImportTitanfall2IconZipAsync()
        {
            StorageFile zipFile = _providedPackZipFile ?? await PickSingleFileAsync(new[] { ".zip" });
            if (zipFile == null)
            {
                return;
            }

            StorageFolder extractedFolder = null;
            try
            {
                extractedFolder = await ExtractZipToTemporaryFolderAsync(zipFile);
                StorageFolder manifestFolder = await FindTitanfall2IconManifestFolderAsync(extractedFolder);
                if (manifestFolder != null)
                {
                    await PackCatalogService.ImportTitanfall2IconManifestPackAsync(manifestFolder);
                    return;
                }

                StorageFolder bestFolder = await FindBestPackFolderAsync(extractedFolder, Titanfall2IconPackImportFiles);
                IReadOnlyDictionary<string, StorageFile> files = await CollectRecognizedFilesAsync(
                    bestFolder,
                    Titanfall2IconPackImportFiles);
                StorageFile head = await TryGetCustomPackHeadImageAsync(bestFolder.Path);
                if (await TryBatchImportIconAsync(files, head, PackCatalogService.CreateTitanfall2IconPackAsync))
                {
                    return;
                }

                await ShowCreateTitanfall2IconPackDialogAsync(bestFolder.DisplayName, files, head);
            }
            catch (Exception ex)
            {
                if (_batchPackImport)
                {
                    throw;
                }

                await ShowMessageAsync(LocalizationManager.Text("ZipImportFailedTitle"), ex.Message);
            }
            finally
            {
                if (extractedFolder != null)
                {
                    try { await extractedFolder.DeleteAsync(); } catch { }
                }
            }
        }

        private static async Task<StorageFolder> FindTitanfall2IconManifestFolderAsync(StorageFolder folder)
        {
            try
            {
                StorageFile file = await folder.GetFileAsync("manifest.json");
                if (JsonObject.TryParse(await FileIO.ReadTextAsync(file), out JsonObject manifest))
                {
                    string packageKind = manifest.GetNamedString("package_kind", string.Empty);
                    string gameStyle = manifest.GetNamedString("game_style", string.Empty);
                    bool hasSlots = manifest.TryGetValue("slots", out IJsonValue slotsVal)
                        && slotsVal.ValueType == JsonValueType.Object;
                    if (string.Equals(packageKind, "titanfall2_icon", StringComparison.OrdinalIgnoreCase)
                        || (string.Equals(gameStyle, "titanfall2", StringComparison.OrdinalIgnoreCase) && hasSlots))
                    {
                        return folder;
                    }
                }
            }
            catch (FileNotFoundException)
            {
            }

            foreach (StorageFolder child in await folder.GetFoldersAsync())
            {
                StorageFolder found = await FindTitanfall2IconManifestFolderAsync(child);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private async Task ShowCreateTitanfall2IconPackDialogAsync(
            string initialDisplayName = null,
            IReadOnlyDictionary<string, StorageFile> initialFiles = null,
            StorageFile initialHeadImageFile = null)
        {
            bool zh = LocalizationManager.Current == UiLanguage.SimplifiedChinese;
            var slots = new (string FileName, string Label)[]
            {
                ("badge_kill_pilot.png", zh ? "铁驭击杀勋章" : "Pilot kill medal"),
                ("badge_headshot.png", zh ? "神射手（爆头）勋章" : "Headshot medal"),
                ("badge_double_kill.png", zh ? "双杀勋章" : "Double kill medal"),
                ("badge_multi_kill.png", zh ? "多重击杀（3杀+）勋章" : "Multi-kill (3+) medal"),
                ("badge_kill_titan.png", zh ? "泰坦处决 / 史诗目标勋章" : "Titan / epic kill medal"),
                ("badge_first_strike.png", zh ? "首杀（先发制人）勋章" : "First strike medal"),
                ("badge_victory.png", zh ? "胜利 / 终结勋章" : "Victory / ace medal"),
                ("badge_wings.png", zh ? "铁驭之翼（复仇）勋章" : "Pilot wings medal"),
                ("badge_titanfall.png", zh ? "泰坦降临目标图标" : "Titanfall objective icon"),
                ("badge_eject.png", zh ? "弹射拆弹图标" : "Eject / defuse icon"),
                ("hit_indicator.png", zh ? "准星命中指示图" : "Hit indicator"),
                ("hit_indicator_crit_bg.png", zh ? "准星弱点光晕底图" : "Crit hit glow background"),
            };
            await ShowPackCreationDialogAsync(
                LocalizationManager.Text("CreateIconPack"),
                zh ? "分别设置泰坦陨落 2 的 RUI 勋章与准星素材；未设置的项目保留原版内置素材。"
                   : "Choose Titanfall 2 RUI medals and hit indicators. Empty slots keep built-in assets.",
                slots, GameStyleMode.Titanfall2,
                PackCatalogService.CreateTitanfall2IconPackAsync,
                initialDisplayName, initialFiles,
                defaultPreviewUriForFileName: name => "ms-appx:///Assets/GameStyles/titanfall2/killconfirm/textures/" + name,
                defaultHeadPreviewUri: "ms-appx:///Assets/GameStyles/titanfall2/killconfirm/textures/preview.png",
                initialHeadImageFile: initialHeadImageFile);
        }
    }
}

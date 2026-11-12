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
        private static readonly string[] Rdr2IconPackImportFiles =
        {
            "hitmarker_shot_white.png",
            "hitmarker_kill_red.png",
            "hitmarker_kill_red_tapered.png",
            "hitmarker_critical_crimson.png",
            "hitmarker_perfect_kill_gold.png",
            "deadeye_kill_cross_red.png",
            "reticle_center_dot_default_white.png",
            "reticle_center_dot_enemy_red.png",
            "reticle_accuracy_circle.png",
            "honor_bg.png",
            "honor_bar_grad_half.png",
            "honor_background.png",
            "honor_good.png",
            "honor_bad.png",
            "honor_cowboy_badge_hd.png",
            "honor_skull_badge_hd.png",
            "honor_plus_sign.png",
            "honor_minus_sign.png",
            "overhead_skull.png",
            "overhead_deadeye.png",
        };

        private async Task ImportRdr2IconZipAsync()
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
                StorageFolder manifestFolder = await FindRdr2IconManifestFolderAsync(extractedFolder);
                if (manifestFolder != null)
                {
                    await PackCatalogService.ImportRdr2IconManifestPackAsync(manifestFolder);
                    return;
                }

                StorageFolder bestFolder = await FindBestPackFolderAsync(extractedFolder, Rdr2IconPackImportFiles);
                IReadOnlyDictionary<string, StorageFile> files = await CollectRecognizedFilesAsync(
                    bestFolder,
                    Rdr2IconPackImportFiles);
                StorageFile head = await TryGetCustomPackHeadImageAsync(bestFolder.Path);
                if (await TryBatchImportIconAsync(files, head, PackCatalogService.CreateRdr2IconPackAsync))
                {
                    return;
                }

                await ShowCreateRdr2IconPackDialogAsync(bestFolder.DisplayName, files, head);
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

        private static async Task<StorageFolder> FindRdr2IconManifestFolderAsync(StorageFolder folder)
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
                    if (string.Equals(packageKind, "rdr2_icon", StringComparison.OrdinalIgnoreCase)
                        || (string.Equals(gameStyle, "rdr2", StringComparison.OrdinalIgnoreCase) && hasSlots))
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
                StorageFolder found = await FindRdr2IconManifestFolderAsync(child);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private async Task ShowCreateRdr2IconPackDialogAsync(
            string initialDisplayName = null,
            IReadOnlyDictionary<string, StorageFile> initialFiles = null,
            StorageFile initialHeadImageFile = null)
        {
            bool zh = LocalizationManager.Current == UiLanguage.SimplifiedChinese;
            var slots = new (string FileName, string Label)[]
            {
                ("hitmarker_shot_white.png", zh ? "普通命中四臂白叉标" : "White hitmarker cross"),
                ("hitmarker_kill_red.png", zh ? "致死击杀四臂红叉标" : "Lethal red hitmarker cross"),
                ("hitmarker_kill_red_tapered.png", zh ? "锥形渐变击杀红叉标" : "Tapered lethal red hitmarker"),
                ("hitmarker_critical_crimson.png", zh ? "致命爆头暗红命中叉标" : "Critical crimson headshot cross"),
                ("hitmarker_perfect_kill_gold.png", zh ? "首杀 / 绝杀完美黄金叉标" : "Perfect gold first/clutch cross"),
                ("deadeye_kill_cross_red.png", zh ? "死神之眼手绘锁定红叉" : "Dead Eye painted red X"),
                ("reticle_center_dot_default_white.png", zh ? "准星中心默认白点" : "Default white reticle dot"),
                ("reticle_center_dot_enemy_red.png", zh ? "准星中心索敌红点" : "Enemy red reticle dot"),
                ("reticle_accuracy_circle.png", zh ? "准星精度扩散白环" : "Reticle accuracy bloom ring"),
                ("honor_bg.png", zh ? "底部荣誉条西部笔刷黑底" : "Bottom Honor bar ink-brush backer"),
                ("honor_bar_grad_half.png", zh ? "底部荣誉条红白双向渐变" : "Honor bar red-white gradient"),
                ("honor_background.png", zh ? "善恶徽章圆形黑边底托" : "Honor medallion dark circle backer"),
                ("honor_good.png", zh ? "高荣誉义警牛仔滑块图标" : "High Honor cowboy slider icon"),
                ("honor_bad.png", zh ? "低荣誉亡命徒骷髅滑块图标" : "Low Honor outlaw skull slider icon"),
                ("honor_cowboy_badge_hd.png", zh ? "右侧高清高荣誉牛仔大徽章" : "Right HD High Honor cowboy medallion"),
                ("honor_skull_badge_hd.png", zh ? "右侧高清低荣誉骷髅大徽章" : "Right HD Low Honor skull medallion"),
                ("honor_plus_sign.png", zh ? "善行荣誉增加加号 (+)" : "Honor gain plus (+) badge"),
                ("honor_minus_sign.png", zh ? "恶行荣誉扣减减号 (-)" : "Honor loss minus (-) badge"),
                ("overhead_skull.png", zh ? "淘汰播报西部骷髅角标" : "Elimination skull badge"),
                ("overhead_deadeye.png", zh ? "死神之眼核心十字角标" : "Dead Eye core emblem"),
            };
            await ShowPackCreationDialogAsync(
                LocalizationManager.Text("CreateIconPack"),
                zh ? "分别设置荒野大镖客 2 原版 Scaleform 准星、死神之眼与善恶荣誉贴图；未设置的项目保留内置原版素材。"
                   : "Choose Red Dead Redemption 2 Scaleform reticle, Dead Eye, and Honor HUD textures. Empty slots keep built-in assets.",
                slots, GameStyleMode.Rdr2,
                PackCatalogService.CreateRdr2IconPackAsync,
                initialDisplayName, initialFiles,
                defaultPreviewUriForFileName: name => "ms-appx:///Assets/GameStyles/rdr2/killconfirm/textures/" + name,
                defaultHeadPreviewUri: "ms-appx:///Assets/GameStyles/rdr2/killconfirm/textures/preview.png",
                initialHeadImageFile: initialHeadImageFile);
        }
    }
}

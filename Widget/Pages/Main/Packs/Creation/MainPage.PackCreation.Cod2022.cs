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
        private static readonly string[] Cod2022IconPackImportFiles =
        {
            "hud_damage_feedback_marker.png",
            "hud_damage_feedback_burst.png",
            "hud_glow.png",
            "hud_splash_accent_diamond.png",
            "hud_splash_accent_reticle.png",
            "hud_splash_accent_chevrons_single.png",
            "hud_splash_notched_background_5.png",
            "hud_splash_notched_background_4.png",
            "hud_icon_splash_kill.png",
            "hud_icon_obit_kill.png",
            "hud_icon_obit_headshot.png",
            "hud_icon_obit_destroy.png",
            "widg_gradient_center_out.png",
            "widg_gradient_left_to_right.png",
        };

        private async Task ImportCod2022IconZipAsync()
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
                StorageFolder manifestFolder = await FindCod2022IconManifestFolderAsync(extractedFolder);
                if (manifestFolder != null)
                {
                    await PackCatalogService.ImportCod2022IconManifestPackAsync(manifestFolder);
                    return;
                }

                StorageFolder bestFolder = await FindBestPackFolderAsync(extractedFolder, Cod2022IconPackImportFiles);
                IReadOnlyDictionary<string, StorageFile> files = await CollectRecognizedFilesAsync(
                    bestFolder,
                    Cod2022IconPackImportFiles);
                StorageFile head = await TryGetCustomPackHeadImageAsync(bestFolder.Path);
                if (await TryBatchImportIconAsync(files, head, PackCatalogService.CreateCod2022IconPackAsync))
                {
                    return;
                }

                await ShowCreateCod2022IconPackDialogAsync(bestFolder.DisplayName, files, head);
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

        private static async Task<StorageFolder> FindCod2022IconManifestFolderAsync(StorageFolder folder)
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
                    if (string.Equals(packageKind, "cod2022_icon", StringComparison.OrdinalIgnoreCase)
                        || (string.Equals(gameStyle, "cod2022", StringComparison.OrdinalIgnoreCase) && hasSlots))
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
                StorageFolder found = await FindCod2022IconManifestFolderAsync(child);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private async Task ShowCreateCod2022IconPackDialogAsync(
            string initialDisplayName = null,
            IReadOnlyDictionary<string, StorageFile> initialFiles = null,
            StorageFile initialHeadImageFile = null)
        {
            bool zh = LocalizationManager.Current == UiLanguage.SimplifiedChinese;
            var slots = new (string FileName, string Label)[]
            {
                ("hud_damage_feedback_marker.png", zh ? "准星命中叉标贴图" : "Hitmarker cross texture"),
                ("hud_damage_feedback_burst.png", zh ? "致死爆头中心爆点" : "Lethal burst flash"),
                ("hud_glow.png", zh ? "+XP 跳字琥珀柔光" : "+XP amber glow"),
                ("hud_splash_accent_diamond.png", zh ? "切角横幅中心菱形光斑" : "Splash center diamond accent"),
                ("hud_splash_accent_reticle.png", zh ? "切角横幅顶部战术准星" : "Splash top tactical reticle"),
                ("hud_splash_accent_chevrons_single.png", zh ? "两侧扫动双折角箭头" : "Sweeping side chevron"),
                ("hud_splash_notched_background_5.png", zh ? "切角勋章横幅三切片底板" : "3-slice notched banner backer"),
                ("hud_splash_notched_background_4.png", zh ? "切角勋章横幅副底板" : "Secondary notched banner backer"),
                ("hud_icon_splash_kill.png", zh ? "勋章准星内嵌击杀骷髅" : "Splash reticle skull icon"),
                ("hud_icon_obit_kill.png", zh ? "下方淘汰铭牌普通骷髅" : "Lower nameplate skull icon"),
                ("hud_icon_obit_headshot.png", zh ? "下方淘汰铭牌爆头骷髅" : "Lower nameplate headshot skull"),
                ("hud_icon_obit_destroy.png", zh ? "爆炸 / 史诗目标摧毁图标" : "Explosive / objective destroy icon"),
                ("widg_gradient_center_out.png", zh ? "双向中心扩散渐变贴图" : "Center-out gradient texture"),
                ("widg_gradient_left_to_right.png", zh ? "左向右扫光渐变贴图" : "Left-to-right gradient texture"),
            };
            await ShowPackCreationDialogAsync(
                LocalizationManager.Text("CreateIconPack"),
                zh ? "分别设置现代战争 II (IW9) 的 LUI 战术贴图与勋章素材；未设置的项目保留原版内置素材。"
                   : "Choose Modern Warfare II (IW9) LUI HUD textures and icons. Empty slots keep built-in assets.",
                slots, GameStyleMode.Cod2022,
                PackCatalogService.CreateCod2022IconPackAsync,
                initialDisplayName, initialFiles,
                defaultPreviewUriForFileName: name => "ms-appx:///Assets/GameStyles/cod2022/killconfirm/textures/" + name,
                defaultHeadPreviewUri: "ms-appx:///Assets/GameStyles/cod2022/killconfirm/textures/preview.png",
                initialHeadImageFile: initialHeadImageFile);
        }
    }
}

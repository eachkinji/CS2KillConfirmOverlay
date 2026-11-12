using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using KillConfirmGameBar.Services;
using Windows.Data.Json;
using Windows.Storage;

namespace KillConfirmGameBar
{
    public sealed partial class MainPage
    {
        private static readonly string[] WarThunderIconPackImportFiles =
            PackCatalogService.WarThunderIconSlotFileNames.ToArray();

        private async Task ImportWarThunderIconZipAsync()
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
                StorageFolder manifestFolder = await FindWarThunderIconManifestFolderAsync(extractedFolder);
                if (manifestFolder != null)
                {
                    await PackCatalogService.ImportWarThunderIconManifestPackAsync(manifestFolder);
                    return;
                }

                StorageFolder bestFolder = await FindBestPackFolderAsync(extractedFolder, WarThunderIconPackImportFiles);
                IReadOnlyDictionary<string, StorageFile> files = await CollectRecognizedFilesAsync(
                    bestFolder,
                    WarThunderIconPackImportFiles);
                StorageFile head = await TryGetCustomPackHeadImageAsync(bestFolder.Path);
                if (await TryBatchImportIconAsync(files, head, PackCatalogService.CreateWarThunderIconPackAsync))
                {
                    return;
                }

                await ShowCreateWarThunderIconPackDialogAsync(bestFolder.DisplayName, files, head);
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

        private static async Task<StorageFolder> FindWarThunderIconManifestFolderAsync(StorageFolder folder)
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
                    if (string.Equals(packageKind, "warthunder_icon", StringComparison.OrdinalIgnoreCase)
                        || (GameStyleService.IsWarThunderKey(gameStyle) && hasSlots))
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
                StorageFolder found = await FindWarThunderIconManifestFolderAsync(child);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private async Task ShowCreateWarThunderIconPackDialogAsync(
            string initialDisplayName = null,
            IReadOnlyDictionary<string, StorageFile> initialFiles = null,
            StorageFile initialHeadImageFile = null)
        {
            bool zh = LocalizationManager.Current == UiLanguage.SimplifiedChinese;
            var slots = new (string FileName, string Label)[]
            {
                ("streaks_event_bg.png", zh ? "先拔头筹 / 一锤定音勋章底板" : "Event streak medal backplate"),
                ("streaks_first_blood.png", zh ? "先拔头筹前景纹章" : "First strike emblem"),
                ("streaks_multi_kill_bg.png", zh ? "连杀勋章星徽底板" : "Multi-strike star backplate"),
                ("streaks_multi_kill_ground_2_3_4_x.png", zh ? "连杀勋章陆战坦克层" : "Multi-strike tank layer"),
                ("streaks_multi_kill_air_2_3_4_x.png", zh ? "连杀勋章空战战机层" : "Multi-strike aircraft layer"),
                ("streaks_multi_kill_ship_2_3_4_x.png", zh ? "连杀勋章海战巨舰层" : "Multi-strike warship layer"),
                ("streaks_rank_2.png", zh ? "双杀罗马数字 II 角标" : "Double strike rank II badge"),
                ("streaks_rank_3.png", zh ? "三杀罗马数字 III 角标" : "Triple strike rank III badge"),
                ("streaks_multi_kill_rank_4.png", zh ? "四连杀 IV 角标" : "Multi-strike rank IV badge"),
                ("streaks_multi_kill_rank_x.png", zh ? "五杀及以上 X 角标" : "Multi-strike rank X badge"),
                ("streaks_tank_bg.png", zh ? "百步穿杨陆战底板" : "Sniper tank medal backplate"),
                ("streaks_heroic_sniper.png", zh ? "百步穿杨爆头纹章" : "Sniper headshot emblem"),
                ("streaks_global_bg.png", zh ? "以眼还眼通用勋章底板" : "Eye for Eye global backplate"),
                ("streaks_global_avenge_self.png", zh ? "以眼还眼复仇纹章" : "Eye for Eye revenge emblem"),
                ("streaks_marks_bg.png", zh ? "神挡杀神 / 爆破勋章底板" : "God Mode / Bomb medal backplate"),
                ("streaks_marks_killed_plane_20_ranks_higher.png", zh ? "神挡杀神前景纹章" : "God Mode emblem"),
                ("kill_unit_icon.png", zh ? "准星 3D 击杀上浮骷髅标" : "World floating kill icon"),
                ("hit_unit_icon.png", zh ? "准星 3D 命中上浮标" : "World floating hit icon"),
                ("engine_state_indicator.png", zh ? "X-Ray 发动机状态图标" : "X-Ray engine state icon"),
                ("gun_state_indicator.png", zh ? "X-Ray 火炮炮闩状态图标" : "X-Ray gun breech state icon"),
                ("turret_gear_state_indicator.png", zh ? "X-Ray 炮塔方向机图标" : "X-Ray turret drive state icon"),
                ("track_state_indicator.png", zh ? "X-Ray 履带状态图标" : "X-Ray track state icon"),
                ("crew.png", zh ? "X-Ray 车组成员状态图标" : "X-Ray tank crew icon"),
                ("ship_hit_citadel_icon.png", zh ? "海战命中核心区横幅图标" : "Naval citadel hit ribbon icon"),
            };
            await ShowPackCreationDialogAsync(
                LocalizationManager.Text("CreateIconPack"),
                zh ? "分别设置战争雷霆的多图层勋章、准星击杀标与 X-Ray 毁伤状态图标；未设置的项目保留原版内置素材。"
                   : "Choose War Thunder layered medals, world killmarks, and X-Ray module icons. Empty slots keep built-in assets.",
                slots, GameStyleMode.WarThunder,
                PackCatalogService.CreateWarThunderIconPackAsync,
                initialDisplayName, initialFiles,
                defaultPreviewUriForFileName: name => "ms-appx:///Assets/GameStyles/warthunder/killconfirm/textures/" + name,
                defaultHeadPreviewUri: "ms-appx:///Assets/GameStyles/warthunder/killconfirm/textures/preview.png",
                initialHeadImageFile: initialHeadImageFile);
        }
    }
}

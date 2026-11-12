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
        private static readonly string[] LeagueOfLegendsIconPackImportFiles =
            PackCatalogService.LeagueOfLegendsIconSlotFileNames.ToArray();

        private async Task ImportLeagueOfLegendsIconZipAsync()
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
                StorageFolder manifestFolder = await FindLeagueOfLegendsIconManifestFolderAsync(extractedFolder);
                if (manifestFolder != null)
                {
                    await PackCatalogService.ImportLeagueOfLegendsIconManifestPackAsync(manifestFolder);
                    return;
                }

                StorageFolder bestFolder = await FindBestPackFolderAsync(extractedFolder, LeagueOfLegendsIconPackImportFiles);
                IReadOnlyDictionary<string, StorageFile> files = await CollectRecognizedFilesAsync(
                    bestFolder,
                    LeagueOfLegendsIconPackImportFiles);
                StorageFile head = await TryGetCustomPackHeadImageAsync(bestFolder.Path);
                if (await TryBatchImportIconAsync(files, head, PackCatalogService.CreateLeagueOfLegendsIconPackAsync))
                {
                    return;
                }

                await ShowCreateLeagueOfLegendsIconPackDialogAsync(bestFolder.DisplayName, files, head);
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

        private static async Task<StorageFolder> FindLeagueOfLegendsIconManifestFolderAsync(StorageFolder folder)
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
                    if (string.Equals(packageKind, "leagueoflegends_icon", StringComparison.OrdinalIgnoreCase)
                        || (GameStyleService.IsLeagueOfLegendsKey(gameStyle) && hasSlots))
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
                StorageFolder found = await FindLeagueOfLegendsIconManifestFolderAsync(child);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private async Task ShowCreateLeagueOfLegendsIconPackDialogAsync(
            string initialDisplayName = null,
            IReadOnlyDictionary<string, StorageFile> initialFiles = null,
            StorageFile initialHeadImageFile = null)
        {
            bool zh = LocalizationManager.Current == UiLanguage.SimplifiedChinese;
            var slots = new (string FileName, string Label)[]
            {
                ("AnnouncementText_GoldGradientFill.png", zh ? "海克斯播报大字鎏金渐变贴图" : "Banner title gold gradient fill"),
                ("Ripple_NoShadow.png", zh ? "右侧通告海克斯脉冲波纹贴图" : "Callout Hextech pulse ripple"),
                ("AnnBannerBlue_Underline.png", zh ? "蓝方播报横幅底线切片" : "Blue banner underline"),
                ("AnnBannerRed_Underline.png", zh ? "红方播报横幅底线切片" : "Red banner underline"),
                ("AnnBannerGold_Underline.png", zh ? "鎏金播报横幅底线切片" : "Gold banner underline"),
                ("AnnBannerTopIcon_gold_ring.png", zh ? "单头像顶部金环边框" : "Single-icon top gold ring"),
                ("AnnBannerTopIcon_glow_blue.png", zh ? "单头像顶部海克斯蓝辉光" : "Single-icon top blue glow"),
                ("AnnBannerTopIcon_glow_red.png", zh ? "单头像顶部猩红辉光" : "Single-icon top red glow"),
                ("AnnBannerLeftIcon_gold_ring.png", zh ? "双头像左侧主控金环边框" : "Two-icon left gold ring"),
                ("AnnBannerLeftGlowBlue.png", zh ? "双头像左侧海克斯蓝辉光" : "Two-icon left blue glow"),
                ("AnnBannerLeftGlowRed.png", zh ? "双头像左侧猩红辉光" : "Two-icon left red glow"),
                ("AnnBannerRightIcon_gold_ring.png", zh ? "双头像右侧受击金环边框" : "Two-icon right gold ring"),
                ("RightIconGlowBlue.png", zh ? "双头像右侧海克斯蓝辉光" : "Two-icon right blue glow"),
                ("RightIconGlowRed.png", zh ? "双头像右侧猩红辉光" : "Two-icon right red glow"),
                ("DoubleKillBlueBG.png", zh ? "双杀蓝方纹章底板" : "Double Kill blue crest"),
                ("DoubleKillRedBG.png", zh ? "双杀红方纹章底板" : "Double Kill red crest"),
                ("TripleKillBlueBG.png", zh ? "三杀蓝方纹章底板" : "Triple Kill blue crest"),
                ("TripleKillRedBG.png", zh ? "三杀红方纹章底板" : "Triple Kill red crest"),
                ("QuadraKillBlueBG.png", zh ? "四杀蓝方纹章底板" : "Quadra Kill blue crest"),
                ("QuadraKillRedBG.png", zh ? "四杀红方纹章底板" : "Quadra Kill red crest"),
                ("PentakillBlueBG.png", zh ? "五杀蓝方至尊纹章底板" : "Pentakill blue crest"),
                ("PentakillRedBG.png", zh ? "五杀红方至尊纹章底板" : "Pentakill red crest"),
                ("KillCallout0_Background.png", zh ? "右侧击杀通告条渐变底板" : "Right kill callout gradient background"),
                ("KillCallout0_KillerFrame.png", zh ? "右侧击杀者方框头像边框" : "Killer square portrait frame"),
                ("KillCallout0_TargetFrame.png", zh ? "右侧阵亡者方框头像边框" : "Target square portrait frame"),
                ("KillCallout0_KillIconNormal.png", zh ? "右侧普通击杀双剑图标" : "Normal crossed-swords kill icon"),
                ("KillCallout0_KillIconStreak.png", zh ? "右侧连杀火焰双剑图标" : "Streak kill icon"),
                ("KillCallout0_KillIconShutdown.png", zh ? "右侧终结悬赏图标" : "Shutdown kill icon"),
                ("KillCallout0_KillIconBounty.png", zh ? "右侧高额赏金图标" : "Bounty kill icon"),
                ("KillCallout0_CaptureAltar.png", zh ? "右侧史诗目标占领图标" : "Objective capture icon"),
                ("KillCallout0_ReviveAllyIcon.png", zh ? "右侧复活守护图标" : "Revive ally icon"),
            };
            await ShowPackCreationDialogAsync(
                LocalizationManager.Text("CreateIconPack"),
                zh ? "分别设置英雄联盟海克斯播报横幅与右侧击杀通告贴图；未设置的项目保留原版内置素材。"
                   : "Choose League of Legends Hextech banner and kill-callout textures. Empty slots keep built-in assets.",
                slots, GameStyleMode.LeagueOfLegends,
                PackCatalogService.CreateLeagueOfLegendsIconPackAsync,
                initialDisplayName, initialFiles,
                defaultPreviewUriForFileName: name => "ms-appx:///Assets/GameStyles/leagueoflegends/killconfirm/textures/" + name,
                defaultHeadPreviewUri: "ms-appx:///Assets/GameStyles/leagueoflegends/killconfirm/textures/preview.png",
                initialHeadImageFile: initialHeadImageFile);
        }
    }
}

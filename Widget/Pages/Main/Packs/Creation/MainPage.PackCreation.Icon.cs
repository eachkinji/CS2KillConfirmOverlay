using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KillConfirmGameBar.Services;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace KillConfirmGameBar
{
    public sealed partial class MainPage
    {
        private async Task ShowCreateIconPackDialogAsync(
            string initialDisplayName = null,
            IReadOnlyDictionary<string, StorageFile> initialFiles = null,
            StorageFile initialHeadImageFile = null)
        {
            var slots = new[]
            {
                ("badge_multi1.png", LocalizationManager.Text("SingleKill")),
                ("badge_multi2.png", LocalizationManager.Text("DoubleKill")),
                ("badge_multi3.png", LocalizationManager.Text("TripleKill")),
                ("badge_multi4.png", LocalizationManager.Text("QuadraKill")),
                ("badge_multi5.png", LocalizationManager.Text("PentaKill")),
                ("badge_multi6.png", LocalizationManager.Text("HexaKill")),
                ("badge_headshot.png", LocalizationManager.Text("Headshot")),
                ("badge_headshot_gold.png", LocalizationManager.Current == UiLanguage.SimplifiedChinese ? "黄金爆头" : "Golden headshot"),
                ("badge_knife.png", LocalizationManager.Text("KnifeKill")),
                ("FIRSTKILL.png", LocalizationManager.Text("FirstLastKill")),
                ("LASTKILL.png", LocalizationManager.Text("FirstLastKill")),
                ("KillMark_Upgrade1.png", LocalizationManager.Text("EliteLevel1")),
                ("KillMark_Upgrade2.png", LocalizationManager.Text("EliteLevel2")),
                ("KillMark_Upgrade3.png", LocalizationManager.Text("EliteLevel3")),
                ("multi2_fx.png", LocalizationManager.Text("DoubleKillFX")),
                ("multi3_fx.png", LocalizationManager.Text("TripleKillFX")),
                ("multi4_fx.png", LocalizationManager.Text("QuadraKillFX")),
                ("multi5_fx.png", LocalizationManager.Text("PentaKillFX")),
                ("multi6_fx.png", LocalizationManager.Text("HexaKillFX")),
                ("badge_knife_1.png", LocalizationManager.Text("EliteKnife1")),
                ("badge_knife_2.png", LocalizationManager.Text("EliteKnife2")),
                ("badge_knife_3.png", LocalizationManager.Text("EliteKnife3")),
                ("badge_assault1.png", LocalizationManager.Text("ClassAssault") + " 1"),
                ("badge_assault2.png", LocalizationManager.Text("ClassAssault") + " 2"),
                ("badge_assault3.png", LocalizationManager.Text("ClassAssault") + " 3"),
                ("badge_scout1.png", LocalizationManager.Text("ClassScout") + " 1"),
                ("badge_scout2.png", LocalizationManager.Text("ClassScout") + " 2"),
                ("badge_scout3.png", LocalizationManager.Text("ClassScout") + " 3"),
                ("badge_sniper1.png", LocalizationManager.Text("ClassSniper") + " 1"),
                ("badge_sniper2.png", LocalizationManager.Text("ClassSniper") + " 2"),
                ("badge_sniper3.png", LocalizationManager.Text("ClassSniper") + " 3"),
                ("badge_elite1.png", LocalizationManager.Text("ClassElite") + " 1"),
                ("badge_elite2.png", LocalizationManager.Text("ClassElite") + " 2"),
                ("badge_elite3.png", LocalizationManager.Text("ClassElite") + " 3"),
                ("badge_knife1.png", LocalizationManager.Text("ClassKnife") + " 1"),
                ("badge_knife2.png", LocalizationManager.Text("ClassKnife") + " 2"),
                ("badge_knife3.png", LocalizationManager.Text("ClassKnife") + " 3")
            };

            bool chinese = LocalizationManager.Current == UiLanguage.SimplifiedChinese;
            var extraLabels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["badge_grenade.png"] = "手雷击杀", ["badge_c4.png"] = "安装 C4", ["badge_c4defuse.png"] = "拆除 C4",
                ["badge_wallshot.png"] = "穿墙击杀", ["badge_headwallshot.png"] = "穿墙爆头", ["badge_headwallshot_gold.png"] = "黄金穿墙爆头",
                ["revenge.png"] = "复仇", ["badge_assist.png"] = "助攻", ["badge_smash.png"] = "特殊重击",
                ["killmark_headshot.png"] = "爆头专属叠加", ["killmark_multikill.png"] = "击杀专属叠加",
                ["killmark_knife.png"] = "刀杀专属叠加", ["killmark_grenade.png"] = "手雷专属叠加"
            };
            var existingNames = new HashSet<string>(slots.Select(slot => slot.Item1), StringComparer.OrdinalIgnoreCase);
            slots = slots.Concat(CrossfirePackFormat.Files.Where(name => !existingNames.Contains(name)).Select(name =>
                (name, chinese ? (extraLabels.TryGetValue(name, out string label) ? label :
                (name.StartsWith("SPRITESPECIAL") ? "特殊事件动态叠加 " : name.StartsWith("SPRITENORMAL") ? "普通事件动态叠加 " : "通用动态叠加 ")
                + System.IO.Path.GetFileNameWithoutExtension(name).Split('_').Last()) : name))).ToArray();

            await ShowPackCreationDialogAsync(
                LocalizationManager.Text("CreateIconPack"),
                LocalizationManager.Text("IconPackCreationHint"),
                slots,
                GameStyleMode.Crossfire,
                PackCatalogService.CreateIconPackAsync,
                initialDisplayName,
                initialFiles,
                defaultHeadPreviewUri: CrossfireExternalAssetService.VisualUri("Original", "badge_headshot.PNG"),
                initialHeadImageFile: initialHeadImageFile);
        }

        private async Task ShowPackCreationDialogAsync(
            string title,
            string description,
            (string FileName, string Label)[] slots,
            GameStyleMode currentGame,
            Func<string, IReadOnlyDictionary<string, StorageFile>, StorageFile, Task> createHandler,
            string initialDisplayName = null,
            IReadOnlyDictionary<string, StorageFile> initialFiles = null,
            Func<string, string> defaultPreviewUriForFileName = null,
            StorageFile initialHeadImageFile = null,
            string defaultHeadPreviewUri = null)
        {
            if (await TryBatchImportIconAsync(initialFiles, initialHeadImageFile, createHandler)) return;

            var selectedFiles = initialFiles != null
                ? new Dictionary<string, StorageFile>(initialFiles, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, StorageFile>(StringComparer.OrdinalIgnoreCase);
            StorageFile headImageFile = initialHeadImageFile;

            var layout = CreatePackDialogLayout(
                title,
                description,
                LocalizationManager.Text("IconPackNamePlaceholder"),
                initialDisplayName,
                out var nameBox);

            if (defaultHeadPreviewUri != null)
            {
                var headCard = await CreateHeadImageCardAsync(
                    defaultHeadPreviewUri,
                    headImageFile,
                    f => headImageFile = f,
                    () => headImageFile = null);
                layout.Children.Add(headCard);
            }

            var scroll = new ScrollViewer
            {
                MaxHeight = 500,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };
            var slotPanel = new StackPanel { Spacing = 8 };
            scroll.Content = new Border
            {
                Padding = new Thickness(0, 0, 0, 28),
                Child = slotPanel
            };

            foreach (var slot in slots)
            {
                selectedFiles.TryGetValue(slot.FileName, out StorageFile existingFile);
                var row = await CreateSlotRowAsync(
                    slot.FileName, slot.Label, isAudio: false, currentGame,
                    selectedFiles, existingFile,
                    defaultPreviewUri: defaultPreviewUriForFileName?.Invoke(slot.FileName));
                slotPanel.Children.Add(row);
            }

            layout.Children.Add(scroll);

            ContentDialogResult result = await ShowPackDialogAsync(
                layout, LocalizationManager.Text("Create"), LocalizationManager.Text("Cancel"));
            if (result != ContentDialogResult.Primary || selectedFiles.Count == 0)
            {
                return;
            }

            string displayName = string.IsNullOrWhiteSpace(nameBox.Text)
                ? LocalizationManager.Text("NewPack")
                : nameBox.Text.Trim();
            await createHandler(displayName, selectedFiles, headImageFile);
        }

        // ---- Battlefield / Delta Force icon pack creation dialogs ----
        // Only the static kill icons the animation actually draws are exposed; dynamic
        // frames / decorative textures stay built-in via loader fallback.

        private async Task ShowCreateBattlefield1IconPackDialogAsync(
            string initialDisplayName = null,
            IReadOnlyDictionary<string, StorageFile> initialFiles = null,
            StorageFile initialHeadImageFile = null)
        {
            bool isChinese = LocalizationManager.Current == UiLanguage.SimplifiedChinese;
            var slots = new (string FileName, string Label)[]
            {
                ("killicon_battlefield1_default.png", isChinese ? "普通击杀" : "Default"),
                ("killicon_battlefield1_headshot.png", isChinese ? "爆头" : "Headshot"),
                ("killicon_battlefield1_crit.png", isChinese ? "刀杀/暴击" : "Crit")
            };
            await ShowPackCreationDialogAsync(
                LocalizationManager.Text("CreateIconPack"),
                isChinese
                    ? "战地1可以分别设置普通击杀、爆头和刀杀/暴击图标；未设置的项目继续使用内置图标。"
                    : "Choose separate Battlefield 1 icons for normal kills, headshots, and knife/critical kills. Items left empty keep the built-in icons.",
                slots,
                GameStyleMode.Battlefield1,
                PackCatalogService.CreateBattlefield1IconPackAsync,
                initialDisplayName,
                initialFiles,
                defaultPreviewUriForFileName: fn => $"ms-appx:///Assets/GameStyles/battlefield1/killconfirm/textures/{fn}",
                defaultHeadPreviewUri: "ms-appx:///Assets/GameStyles/battlefield1/killconfirm/textures/killicon_battlefield1_default.png");
        }

        private async Task ShowCreateBattlefield5IconPackDialogAsync(
            string initialDisplayName = null,
            IReadOnlyDictionary<string, StorageFile> initialFiles = null,
            StorageFile initialHeadImageFile = null)
        {
            bool isChinese = LocalizationManager.Current == UiLanguage.SimplifiedChinese;
            var slots = new (string FileName, string Label)[]
            {
                ("killicon_battlefield5_default.png", isChinese ? "普通击杀" : "Default"),
                ("killicon_battlefield5_headshot.png", isChinese ? "爆头" : "Headshot"),
                ("killicon_battlefield5_assist.png", isChinese ? "助攻" : "Assist")
            };
            await ShowPackCreationDialogAsync(
                LocalizationManager.Text("CreateIconPack"),
                isChinese
                    ? "战地5可以分别设置普通击杀、爆头和助攻图标；未设置的项目继续使用内置图标。"
                    : "Choose separate Battlefield V icons for normal kills, headshots, and assists. Items left empty keep the built-in icons.",
                slots,
                GameStyleMode.Battlefield5,
                PackCatalogService.CreateBattlefield5IconPackAsync,
                initialDisplayName,
                initialFiles,
                defaultPreviewUriForFileName: fn => $"ms-appx:///Assets/GameStyles/battlefield5/killconfirm/textures/{fn}",
                defaultHeadPreviewUri: "ms-appx:///Assets/GameStyles/battlefield5/killconfirm/textures/killicon_battlefield5_default.png");
        }

        private async Task ShowCreateBattlefield2042IconPackDialogAsync(
            string initialDisplayName = null,
            IReadOnlyDictionary<string, StorageFile> initialFiles = null,
            StorageFile initialHeadImageFile = null)
        {
            bool isChinese = LocalizationManager.Current == UiLanguage.SimplifiedChinese;
            var slots = new (string FileName, string Label)[]
            {
                ("NormalSkullSprite.png", isChinese ? "普通击杀" : "Default"),
                ("HeadshotSkullSprite.png", isChinese ? "爆头" : "Headshot"),
                ("AssistSprite.png", isChinese ? "助攻" : "Assist")
            };
            await ShowPackCreationDialogAsync(
                LocalizationManager.Text("CreateIconPack"),
                isChinese
                    ? "分别为战地2042的普通击杀、爆头和助攻选择图标。"
                    : "Choose separate Battlefield 2042 icons for normal kills, headshots, and assists.",
                slots,
                GameStyleMode.Battlefield2042,
                PackCatalogService.CreateBattlefield2042IconPackAsync,
                initialDisplayName,
                initialFiles,
                defaultPreviewUriForFileName: fn => $"ms-appx:///Assets/GameStyles/battlefield2042/killconfirm/textures/{fn}",
                defaultHeadPreviewUri: "ms-appx:///Assets/GameLogos/battlefield2042.png");
        }

        private async Task ShowCreateOverwatchIconPackDialogAsync(
            string initialDisplayName,
            IReadOnlyDictionary<string, StorageFile> initialFiles,
            StorageFile initialHeadImageFile)
        {
            bool zh = LocalizationManager.Current == UiLanguage.SimplifiedChinese;
            var slots = new[]
            {
                ("kill_icon_white.png", zh ? "击杀图标" : "Kill icon"),
                ("kill_effect_sheet.png", zh ? "击杀特效图集" : "Kill effect sheet")
            };
            await ShowPackCreationDialogAsync(
                LocalizationManager.Text("CreateIconPack"),
                zh ? "编辑守望先锋默认素材会创建一份可选择的自定义副本。" : "Editing the Overwatch defaults creates a selectable custom copy.",
                slots, GameStyleMode.Overwatch,
                PackCatalogService.CreateOverwatchIconPackAsync,
                initialDisplayName, initialFiles,
                fn => $"ms-appx:///Assets/GameStyles/overwatch/killconfirm/textures/{fn}",
                initialHeadImageFile,
                "ms-appx:///Assets/GameStyles/overwatch/killconfirm/textures/preview.png");
        }

        private async Task ShowCreateModernWarfare2019IconPackDialogAsync(
            string initialDisplayName,
            IReadOnlyDictionary<string, StorageFile> initialFiles,
            StorageFile initialHeadImageFile)
        {
            bool zh = LocalizationManager.Current == UiLanguage.SimplifiedChinese;
            var slots = new[]
            {
                ("killcon.png", zh ? "上方击杀图标" : "Upper kill icon"),
                ("huiguangcod.png", zh ? "金钱提示辉光" : "Money glow")
            };
            await ShowPackCreationDialogAsync(
                LocalizationManager.Text("CreateIconPack"),
                zh ? "编辑 MW2019 默认素材会创建一份可选择的自定义副本。" : "Editing the MW2019 defaults creates a selectable custom copy.",
                slots, GameStyleMode.ModernWarfare2019,
                PackCatalogService.CreateModernWarfare2019IconPackAsync,
                initialDisplayName, initialFiles,
                fn => $"ms-appx:///Assets/GameStyles/modernwarfare2019/killconfirm/textures/{fn}",
                initialHeadImageFile,
                "ms-appx:///Assets/GameLogos/modernwarfare2019.png");
        }

        private async Task ShowCreateApexIconPackDialogAsync(
            string initialDisplayName,
            IReadOnlyDictionary<string, StorageFile> initialFiles,
            StorageFile initialHeadImageFile)
        {
            bool zh = LocalizationManager.Current == UiLanguage.SimplifiedChinese;
            var slots = new[] { ("hitmark.png", zh ? "命中标记" : "Hit marker") };
            await ShowPackCreationDialogAsync(
                LocalizationManager.Text("CreateIconPack"),
                zh ? "编辑 Apex 默认命中标记会创建一份可选择的自定义副本。" : "Editing the Apex hit marker creates a selectable custom copy.",
                slots, GameStyleMode.Apex,
                PackCatalogService.CreateApexIconPackAsync,
                initialDisplayName, initialFiles,
                fn => $"ms-appx:///Assets/GameStyles/apex/killconfirm/textures/{fn}",
                initialHeadImageFile,
                "ms-appx:///Assets/GameLogos/apex.png");
        }

        // BF4 / PUBG 的击杀提示是纯文本 HUD（见 KillConfirmAnimation.Battlefield4.cs /
        // .Pubg.cs 的 ClearXxxIconCache 注释），不绘制任何击杀图标，因此没有图标包可
        // 自定义。阻止这两者在图标包创建/导入时落到 Crossfire 兜底分支——
        // 那会生成一个属于 Crossfire 的包并出现
        // 在 Crossfire 的管理列表里（"不适配本游戏"的包）。
        private static bool IsIconlessGame(GameStyleMode style)
        {
            return style == GameStyleMode.Battlefield4
                || style == GameStyleMode.Pubg;
        }

        private static bool IsIconPackCreationUnavailable(GameStyleMode style)
        {
            return IsIconlessGame(style);
        }

        private async Task<bool> GuardIconPackCreationAsync()
        {
            GameStyleMode style = GameStyleService.Current;
            bool isChinese = LocalizationManager.Current == UiLanguage.SimplifiedChinese;
            if (IsIconlessGame(style))
            {
                await ShowMessageAsync(
                    isChinese ? "无图标包" : "No icon pack",
                    isChinese
                        ? "该游戏的击杀提示为纯文本样式，不绘制击杀图标，因此不支持自定义图标包。"
                        : "This game uses a text-only kill feed and draws no kill icons, so custom icon packs are not supported.");
                return true;
            }

            return false;
        }

        private async Task ShowCreateDeltaForceIconPackDialogAsync(
            string initialDisplayName = null,
            IReadOnlyDictionary<string, StorageFile> initialFiles = null,
            StorageFile initialHeadImageFile = null)
        {
            bool isChinese = LocalizationManager.Current == UiLanguage.SimplifiedChinese;
            var slots = new (string FileName, string Label)[]
            {
                ("killicon_df_default.png", isChinese ? "普通击杀" : "Default"),
                ("killicon_df_headshot.png", isChinese ? "爆头" : "Headshot"),
                ("killicon_df_capture.png", isChinese ? "占点" : "Capture"),
                ("killicon_scrolling_assist.png", isChinese ? "助攻" : "Assist")
            };
            await ShowPackCreationDialogAsync(
                LocalizationManager.Text("CreateIconPack"),
                isChinese
                    ? "三角洲可以分别设置普通击杀、爆头、占点和助攻图标；未设置的项目继续使用内置图标。"
                    : "Choose separate Delta Force icons for normal kills, headshots, captures, and assists. Items left empty keep the built-in icons.",
                slots,
                GameStyleMode.DeltaForce,
                PackCatalogService.CreateDeltaForceIconPackAsync,
                initialDisplayName,
                initialFiles,
                defaultPreviewUriForFileName: fn => $"ms-appx:///Assets/GameStyles/deltaforce/killconfirm/textures/{fn}",
                defaultHeadPreviewUri: "ms-appx:///Assets/GameStyles/deltaforce/killconfirm/textures/killicon_df_default.png");
        }
    }
}

using System.Collections.Generic;
using System.Threading.Tasks;
using KillConfirmGameBar.Services;
using Windows.Storage;

namespace KillConfirmGameBar
{
    public sealed partial class MainPage
    {
        private static readonly string[] Battlefield6IconPackImportFiles =
        {
            "killicon_battlefield6_default.png",
            "killicon_battlefield6_headshot.png",
            "killicon_battlefield6_explosion.png",
            "killicon_battlefield6_crit.png",
            "killicon_battlefield6_destroyvehicle.png",
            "killicon_battlefield6_assist.png",
            "killicon_battlefield6_capture.png",
            "killicon_battlefield6_vehicle_destroy_assist.png",
            "killicon_battlefield6_medic.png",
            "killicon_battlefield6_spot_assist.png",
        };

        private async Task ShowCreateBattlefield6IconPackDialogAsync(
            string initialDisplayName = null,
            IReadOnlyDictionary<string, StorageFile> initialFiles = null,
            StorageFile initialHeadImageFile = null)
        {
            bool zh = LocalizationManager.Current == UiLanguage.SimplifiedChinese;
            var slots = new (string FileName, string Label)[]
            {
                ("killicon_battlefield6_default.png", zh ? "普通击杀" : "Normal kill"),
                ("killicon_battlefield6_headshot.png", zh ? "爆头" : "Headshot"),
                ("killicon_battlefield6_explosion.png", zh ? "爆炸击杀" : "Explosive kill"),
                ("killicon_battlefield6_crit.png", zh ? "暴击" : "Critical hit"),
                ("killicon_battlefield6_destroyvehicle.png", zh ? "摧毁载具" : "Vehicle destroyed"),
                ("killicon_battlefield6_assist.png", zh ? "助攻" : "Assist"),
                ("killicon_battlefield6_capture.png", zh ? "占点" : "Capture"),
                ("killicon_battlefield6_vehicle_destroy_assist.png", zh ? "载具摧毁助攻" : "Vehicle assist"),
                ("killicon_battlefield6_medic.png", zh ? "救援" : "Revive"),
                ("killicon_battlefield6_spot_assist.png", zh ? "侦察助攻" : "Spot assist"),
            };
            await ShowPackCreationDialogAsync(
                LocalizationManager.Text("CreateIconPack"),
                zh ? "分别设置战地 6 的事件图标；未设置的项目保留内置素材。"
                   : "Choose Battlefield 6 event icons. Empty slots keep their built-in assets.",
                slots, GameStyleMode.Battlefield6,
                PackCatalogService.CreateBattlefield6IconPackAsync,
                initialDisplayName, initialFiles,
                defaultPreviewUriForFileName: name => $"ms-appx:///Assets/GameStyles/battlefield6/killconfirm/textures/{name}",
                defaultHeadPreviewUri: "ms-appx:///Assets/GameStyles/battlefield6/killconfirm/textures/killicon_battlefield6_default.png",
                initialHeadImageFile: initialHeadImageFile);
        }
    }
}

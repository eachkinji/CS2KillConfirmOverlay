using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KillConfirmGameBar.Services;
using Windows.Storage;
using Windows.UI.Xaml.Controls;

namespace KillConfirmGameBar
{
    public sealed partial class MainPage
    {
        private const string CustomModuleDefaultLogoUri =
            "ms-appx:///Assets/GameStyles/custommodule/iconpacks/custommodule/pack_head.webp";

        private async Task ShowCreateCustomModuleVoicePackDialogAsync(
            string initialDisplayName = null,
            IReadOnlyDictionary<string, IReadOnlyList<StorageFile>> initialFiles = null,
            StorageFile initialHeadImageFile = null)
        {
            if (await TryBatchImportVoiceAsync(initialFiles, initialHeadImageFile, PackCatalogService.CreateCustomModuleVoicePackAsync)) return;

            bool isChinese = LocalizationManager.Current == UiLanguage.SimplifiedChinese;
            var slots = new (string FileName, string Label)[]
            {
                ("1.wav", isChinese ? "1 杀 · 普通" : "Kill 1 · Normal"),
                ("1-headshot.wav", isChinese ? "1 杀 · 爆头" : "Kill 1 · Headshot"),
                ("2.wav", isChinese ? "2 杀 · 普通" : "Kill 2 · Normal"),
                ("2-headshot.wav", isChinese ? "2 杀 · 爆头" : "Kill 2 · Headshot"),
                ("3.wav", isChinese ? "3 杀 · 普通" : "Kill 3 · Normal"),
                ("3-headshot.wav", isChinese ? "3 杀 · 爆头" : "Kill 3 · Headshot"),
                ("4.wav", isChinese ? "4 杀 · 普通" : "Kill 4 · Normal"),
                ("4-headshot.wav", isChinese ? "4 杀 · 爆头" : "Kill 4 · Headshot"),
                ("5.wav", isChinese ? "5 杀 · 普通" : "Kill 5 · Normal"),
                ("5-headshot.wav", isChinese ? "5 杀 · 爆头" : "Kill 5 · Headshot")
            };

            var selectedFiles = CreateVoiceSelectionMap(initialFiles);
            StorageFile headImageFile = initialHeadImageFile;
            var layout = CreatePackDialogLayout(
                LocalizationManager.Text("CreateVoicePack"),
                isChinese
                    ? "与 CS2 Customizer 一致，共 10 种事件：1～5 杀分别支持普通和爆头语音。空槽按同级普通语音、1 杀爆头、1 杀普通语音的顺序回退。"
                    : "Matches CS2 Customizer with 10 events: normal and headshot audio for kills 1-5. Empty slots fall back through same-level normal, kill-1 headshot, then kill-1 normal.",
                LocalizationManager.Text("VoicePackNamePlaceholder"),
                initialDisplayName,
                out var nameBox);

            layout.Children.Add(await CreateHeadImageCardAsync(
                CustomModuleDefaultLogoUri,
                headImageFile,
                file => headImageFile = file,
                () => headImageFile = null));

            var slotContainer = new StackPanel { Spacing = 8 };
            foreach (var slot in slots)
            {
                selectedFiles.TryGetValue(slot.FileName, out List<StorageFile> existingFiles);
                slotContainer.Children.Add(await CreateVoiceSlotRowAsync(
                    slot.FileName,
                    slot.Label,
                    GameStyleMode.CustomModule,
                    selectedFiles,
                    existingFiles));
            }

            layout.Children.Add(new ScrollViewer
            {
                MaxHeight = 430,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = slotContainer
            });

            ContentDialogResult result = await ShowPackDialogAsync(
                layout,
                LocalizationManager.Text("Create"),
                LocalizationManager.Text("Cancel"));
            if (result != ContentDialogResult.Primary)
            {
                return;
            }

            string packName = string.IsNullOrWhiteSpace(nameBox.Text)
                ? (isChinese ? "自定义语音包" : "Custom voice pack")
                : nameBox.Text.Trim();

            if (headImageFile == null)
            {
                try
                {
                    headImageFile = await StorageFile.GetFileFromApplicationUriAsync(
                        new Uri(CustomModuleDefaultLogoUri));
                }
                catch { }
            }

            await PackCatalogService.CreateCustomModuleVoicePackAsync(packName, new VoicePackBuildOptions
            {
                SelectedFileGroups = AsReadOnlyVoiceSelection(selectedFiles),
                HeadImageFile = headImageFile
            });
        }
    }
}

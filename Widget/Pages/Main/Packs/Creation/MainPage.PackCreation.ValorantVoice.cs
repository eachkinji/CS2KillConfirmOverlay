using System;
using System.Collections.Generic;
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
        // Valorant voice pack creation dialog. Valorant's built-in voice plays
        // tier 1-5 streak voices plus a headshot voice, so the custom pack exposes
        // those six slots (1.wav..5.wav + headshot.wav). The head image uses
        // Valorant's own kill icon as the default cover.
        private async Task ShowCreateValorantVoicePackDialogAsync(
            string initialDisplayName = null,
            IReadOnlyDictionary<string, IReadOnlyList<StorageFile>> initialFiles = null,
            StorageFile initialHeadImageFile = null,
            string defaultHeadPreviewUri = null)
        {
            if (await TryBatchImportVoiceAsync(initialFiles, initialHeadImageFile, PackCatalogService.CreateValorantVoicePackAsync)) return;

            bool isChinese = LocalizationManager.Current == UiLanguage.SimplifiedChinese;
            var slots = new (string FileName, string Label)[]
            {
                ("1.wav", LocalizationManager.Text("ValorantSlot1")),
                ("2.wav", LocalizationManager.Text("ValorantSlot2")),
                ("3.wav", LocalizationManager.Text("ValorantSlot3")),
                ("4.wav", LocalizationManager.Text("ValorantSlot4")),
                ("5.wav", LocalizationManager.Text("ValorantSlot5")),
                ("headshot.wav", LocalizationManager.Text("ValorantSlotHeadshot"))
            };

            var selectedFiles = CreateVoiceSelectionMap(initialFiles);
            StorageFile headImageFile = initialHeadImageFile;

            var layout = CreatePackDialogLayout(
                LocalizationManager.Text("CreateVoicePack"),
                isChinese
                    ? "分别选择 1～5 杀和爆头语音；留空或重置的项目使用默认内置 VAL 语音。"
                    : "Choose Valorant voices for kills 1-5 and headshots. Empty or reset slots use the default built-in Valorant voice.",
                LocalizationManager.Text("VoicePackNamePlaceholder"),
                initialDisplayName,
                out var nameBox);

            var headCard = await CreateHeadImageCardAsync(
                defaultHeadPreviewUri ?? GetValorantVoicePackEmblemUri(ValorantPackService.DefaultKey),
                headImageFile,
                f => headImageFile = f,
                () => headImageFile = null);
            layout.Children.Add(headCard);

            var slotContainer = new StackPanel { Spacing = 8 };
            foreach (var slot in slots)
            {
                selectedFiles.TryGetValue(slot.FileName, out List<StorageFile> existingFiles);
                var row = await CreateVoiceSlotRowAsync(
                    slot.FileName, slot.Label, GameStyleMode.Valorant,
                    selectedFiles, existingFiles);
                slotContainer.Children.Add(row);
            }

            var scroll = new ScrollViewer
            {
                MaxHeight = 420,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = slotContainer
            };
            layout.Children.Add(scroll);

            ContentDialogResult result = await ShowPackDialogAsync(
                layout, LocalizationManager.Text("Create"), LocalizationManager.Text("Cancel"));
            if (result != ContentDialogResult.Primary)
            {
                return;
            }

            string packName = string.IsNullOrWhiteSpace(nameBox.Text)
                ? "无畏契约语音包"
                : nameBox.Text.Trim();

            if (headImageFile == null)
            {
                try
                {
                    headImageFile = await StorageFile.GetFileFromApplicationUriAsync(
                        new Uri(defaultHeadPreviewUri ?? GetValorantVoicePackEmblemUri(ValorantPackService.DefaultKey)));
                }
                catch { }
            }

            await PackCatalogService.CreateValorantVoicePackAsync(packName, new VoicePackBuildOptions
            {
                SelectedFileGroups = AsReadOnlyVoiceSelection(selectedFiles),
                HeadImageFile = headImageFile
            });
        }
    }
}

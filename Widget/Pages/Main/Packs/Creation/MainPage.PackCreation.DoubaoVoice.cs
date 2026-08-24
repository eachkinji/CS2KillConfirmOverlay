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
        private static readonly (string FileName, string LabelKey, string BuiltInDefault)[] DoubaoVoiceSlots =
        {
            ("1kill.wav", "DoubaoSlotVoice1", "1kill.wav"),
            ("2kill.wav", "DoubaoSlotVoice2", "2kill.wav"),
            ("3kill.wav", "DoubaoSlotVoice3", "3kill.wav"),
            ("4kill.wav", "DoubaoSlotVoice4", "4kill.wav"),
            ("5kill.wav", "DoubaoSlotVoice5", "5kill.wav")
        };

        private async Task ShowCreateDoubaoVoicePackDialogAsync(
            string initialDisplayName = null,
            IReadOnlyDictionary<string, IReadOnlyList<StorageFile>> initialFiles = null,
            StorageFile initialHeadImageFile = null)
        {
            var selectedFiles = CreateVoiceSelectionMap(initialFiles);
            StorageFile headImageFile = initialHeadImageFile;

            var layout = CreatePackDialogLayout(
                LocalizationManager.Text("CreateVoicePack"),
                LocalizationManager.Text("DoubaoVoiceCollectionsHint"),
                LocalizationManager.Text("VoicePackNamePlaceholder"),
                initialDisplayName,
                out var nameBox);

            var headCard = await CreateHeadImageCardAsync(
                "ms-appx:///Assets/GameLogos/doubao.png",
                headImageFile,
                f => headImageFile = f,
                () => headImageFile = null);
            layout.Children.Add(headCard);

            var slotContainer = new StackPanel { Spacing = 8 };
            foreach (var slot in DoubaoVoiceSlots)
            {
                selectedFiles.TryGetValue(slot.FileName, out List<StorageFile> existingFiles);
                var row = await CreateVoiceSlotRowAsync(
                    slot.FileName, LocalizationManager.Text(slot.LabelKey),
                    GameStyleMode.Doubao, selectedFiles, existingFiles);
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
                ? "豆包语音包"
                : nameBox.Text.Trim();

            await FillBuiltInVoiceDefaultsAsync(
                selectedFiles, DoubaoVoiceSlots, "ms-appx:///KillConfirmService/sounds/doubao/");

            await PackCatalogService.CreateDoubaoVoicePackAsync(packName, new VoicePackBuildOptions
            {
                SelectedFileGroups = AsReadOnlyVoiceSelection(selectedFiles),
                HeadImageFile = headImageFile
            });
        }
    }
}

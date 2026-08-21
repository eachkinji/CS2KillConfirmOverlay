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
        private static readonly (string FileName, string Label, string BuiltInDefault)[] DagoujiaoVoiceSlots =
        {
            ("common.wav", "DagoujiaoSlotCommon", "common.wav"),
            ("headshot.wav", "DagoujiaoSlotHeadshot", "jiaojiaojiao.wav"),
            ("epic.wav", "DagoujiaoSlotEpic", "epic.wav"),
            ("jiaojiaojiao.wav", "DagoujiaoSlotJiaojiaojiao", "jiaojiaojiao.wav")
        };

        private async Task ShowCreateDagoujiaoVoicePackDialogAsync(
            string initialDisplayName = null,
            IReadOnlyDictionary<string, StorageFile> initialFiles = null,
            StorageFile initialHeadImageFile = null)
        {
            var selectedFiles = initialFiles != null
                ? new Dictionary<string, StorageFile>(initialFiles, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, StorageFile>(StringComparer.OrdinalIgnoreCase);
            StorageFile headImageFile = initialHeadImageFile;

            var layout = CreatePackDialogLayout(
                LocalizationManager.Text("CreateVoicePack"),
                LocalizationManager.Text("DagoujiaoVoiceCollectionsHint"),
                LocalizationManager.Text("VoicePackNamePlaceholder"),
                initialDisplayName,
                out var nameBox);

            var headCard = await CreateHeadImageCardAsync(
                "ms-appx:///Assets/GameLogos/dagoujiao.jpg",
                headImageFile,
                f => headImageFile = f,
                () => headImageFile = null);
            layout.Children.Add(headCard);

            var slotContainer = new StackPanel { Spacing = 8 };
            foreach (var slot in DagoujiaoVoiceSlots)
            {
                selectedFiles.TryGetValue(slot.FileName, out StorageFile existingFile);
                var row = await CreateSlotRowAsync(
                    slot.FileName, LocalizationManager.Text(slot.Label),
                    isAudio: true, GameStyleMode.Dagoujiao,
                    selectedFiles, existingFile);
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
                ? "大狗叫语音包"
                : nameBox.Text.Trim();

            await FillBuiltInDefaultsAsync(
                selectedFiles, DagoujiaoVoiceSlots, "ms-appx:///KillConfirmService/sounds/dagoujiao/");

            await PackCatalogService.CreateDagoujiaoVoicePackAsync(packName, new VoicePackBuildOptions
            {
                SelectedFiles = selectedFiles,
                HeadImageFile = headImageFile
            });
        }
    }
}
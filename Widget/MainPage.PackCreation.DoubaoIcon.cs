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
        private static readonly (string FileName, string LabelKey, string BuiltInDefault)[] DoubaoIconSlots =
        {
            ("1kill.png", "DoubaoSlotIcon1", "1kill.png"),
            ("2kill.png", "DoubaoSlotIcon2", "2kill.png"),
            ("3kill.png", "DoubaoSlotIcon3", "3kill.png"),
            ("4kill.png", "DoubaoSlotIcon4", "4kill.png"),
            ("5kill.png", "DoubaoSlotIcon5", "5kill.png")
        };

        private async Task ShowCreateDoubaoIconPackDialogAsync(
            string initialDisplayName = null,
            IReadOnlyDictionary<string, StorageFile> initialFiles = null,
            StorageFile initialHeadImageFile = null)
        {
            var selectedFiles = initialFiles != null
                ? new Dictionary<string, StorageFile>(initialFiles, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, StorageFile>(StringComparer.OrdinalIgnoreCase);
            StorageFile headImageFile = initialHeadImageFile;

            var layout = CreatePackDialogLayout(
                LocalizationManager.Text("CreateIconPack"),
                LocalizationManager.Text("DoubaoIconCollectionsHint"),
                LocalizationManager.Text("IconPackNamePlaceholder"),
                initialDisplayName,
                out var nameBox);

            var headCard = await CreateHeadImageCardAsync(
                "ms-appx:///Assets/GameLogos/doubao.png",
                headImageFile,
                f => headImageFile = f,
                () => headImageFile = null);
            layout.Children.Add(headCard);

            var slotContainer = new StackPanel { Spacing = 8 };
            foreach (var slot in DoubaoIconSlots)
            {
                selectedFiles.TryGetValue(slot.FileName, out StorageFile existingFile);
                var row = await CreateSlotRowAsync(
                    slot.FileName, LocalizationManager.Text(slot.LabelKey),
                    isAudio: false, GameStyleMode.Doubao,
                    selectedFiles, existingFile,
                    defaultPreviewUri: $"ms-appx:///Assets/GameStyles/doubao/killconfirm/textures/{slot.BuiltInDefault}");
                slotContainer.Children.Add(row);
            }

            var scroll = new ScrollViewer
            {
                MaxHeight = 440,
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
                ? "豆包图标包"
                : nameBox.Text.Trim();

            await FillBuiltInDefaultsAsync(
                selectedFiles, DoubaoIconSlots, "ms-appx:///Assets/GameStyles/doubao/killconfirm/textures/");

            await PackCatalogService.CreateDoubaoIconPackAsync(packName, selectedFiles, headImageFile);
        }
    }
}
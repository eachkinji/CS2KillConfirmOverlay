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
        private static readonly (string FileName, string LabelKey, string BuiltInDefault)[] DagoujiaoIconSlots =
        {
            ("common.png", "DagoujiaoIconCommon", "common.png"),
            ("headshot.png", "DagoujiaoIconHeadshot", "headshot.png"),
            ("epic.jpg", "DagoujiaoIconEpic", "epic.jpg"),
            ("1kill.png", "DagoujiaoIconKill1", "common.png"),
            ("2kill.png", "DagoujiaoIconKill2", "common.png"),
            ("3kill.png", "DagoujiaoIconKill3", "common.png"),
            ("4kill.png", "DagoujiaoIconKill4", "common.png"),
            ("5kill.png", "DagoujiaoIconKill5", "common.png")
        };

        private async Task ShowCreateDagoujiaoIconPackDialogAsync(
            string initialDisplayName = null,
            IReadOnlyDictionary<string, StorageFile> initialFiles = null,
            StorageFile initialHeadImageFile = null)
        {
            if (await TryBatchImportIconAsync(initialFiles, initialHeadImageFile, PackCatalogService.CreateDagoujiaoIconPackAsync)) return;

            var selectedFiles = initialFiles != null
                ? new Dictionary<string, StorageFile>(initialFiles, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, StorageFile>(StringComparer.OrdinalIgnoreCase);
            StorageFile headImageFile = initialHeadImageFile;

            var layout = CreatePackDialogLayout(
                LocalizationManager.Text("CreateIconPack"),
                LocalizationManager.Text("DagoujiaoIconCollectionsHint"),
                LocalizationManager.Text("IconPackNamePlaceholder"),
                initialDisplayName,
                out var nameBox);

            var headCard = await CreateHeadImageCardAsync(
                "ms-appx:///Assets/GameLogos/dagoujiao.jpg",
                headImageFile,
                f => headImageFile = f,
                () => headImageFile = null);
            layout.Children.Add(headCard);

            var slotContainer = new StackPanel { Spacing = 8 };
            foreach (var slot in DagoujiaoIconSlots)
            {
                selectedFiles.TryGetValue(slot.FileName, out StorageFile existingFile);
                var row = await CreateSlotRowAsync(
                    slot.FileName, LocalizationManager.Text(slot.LabelKey),
                    isAudio: false, GameStyleMode.Dagoujiao,
                    selectedFiles, existingFile,
                    defaultPreviewUri: $"ms-appx:///Assets/GameStyles/dagoujiao/killconfirm/textures/{slot.BuiltInDefault}");
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
                ? "大狗叫图标包"
                : nameBox.Text.Trim();

            await FillBuiltInDefaultsAsync(
                selectedFiles, DagoujiaoIconSlots, "ms-appx:///Assets/GameStyles/dagoujiao/killconfirm/textures/");

            await PackCatalogService.CreateDagoujiaoIconPackAsync(packName, selectedFiles, headImageFile);
        }
    }
}
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
        // CSOL icon pack creation dialog.
        // 15 slot rows driven by PackCatalogService.CsolIconSlotFileNames.
        // CSOL has no FX / Elite / WeaponBadge overlays, so the dialog stays
        // minimal: name + 15 image-picker rows.
        private async Task ShowCreateCsolIconPackDialogAsync(
            string initialDisplayName = null,
            IReadOnlyDictionary<string, StorageFile> initialFiles = null,
            StorageFile initialHeadImageFile = null)
        {
            var slots = new (string FileName, string Label)[PackCatalogService.CsolIconSlotFileNames.Count];
            for (int i = 0; i < PackCatalogService.CsolIconSlotFileNames.Count; i++)
            {
                string fileName = PackCatalogService.CsolIconSlotFileNames[i];
                slots[i] = (fileName, LocalizationManager.Text(CsolIconLabelKeyFor(fileName)));
            }

            var selectedFiles = initialFiles != null
                ? new Dictionary<string, StorageFile>(initialFiles, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, StorageFile>(StringComparer.OrdinalIgnoreCase);
            StorageFile headImageFile = initialHeadImageFile;

            var layout = CreatePackDialogLayout(
                LocalizationManager.Text("CreateCsolIconPack"),
                LocalizationManager.Text("CsolIconPackCreationHint"),
                LocalizationManager.Text("CsolIconPackNamePlaceholder"),
                initialDisplayName,
                out var nameBox);

            var headCard = await CreateHeadImageCardAsync(
                "ms-appx:///Assets/KillConfirmCode/Csol4/headshot_kill.png",
                headImageFile,
                f => headImageFile = f,
                () => headImageFile = null);
            layout.Children.Add(headCard);

            var scroll = new ScrollViewer
            {
                MaxHeight = 460,
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
                    slot.FileName, slot.Label, isAudio: false, GameStyleMode.Csol,
                    selectedFiles, existingFile,
                    defaultPreviewUri: "ms-appx:///Assets/KillConfirmCode/Csol4/" + slot.FileName);
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
                ? LocalizationManager.Text("CsolIconPack")
                : nameBox.Text.Trim();

            await PackCatalogService.CreateCsolIconPackAsync(displayName, selectedFiles, headImageFile);
        }

        // Maps a CSOL icon slot filename to its localization key.
        private static string CsolIconLabelKeyFor(string fileName)
        {
            switch (fileName)
            {
                case "1kill.png": return "CsolIconKill1";
                case "2kill.png": return "CsolIconKill2";
                case "3kill.png": return "CsolIconKill3";
                case "4kill.png": return "CsolIconKill4";
                case "5kill.png": return "CsolIconKill5";
                case "6kill.png": return "CsolIconKill6";
                case "7kill.png": return "CsolIconKill7";
                case "8kill.png": return "CsolIconKill8";
                case "9kill.png": return "CsolIconKill9";
                case "10kill.png": return "CsolIconKill10";
                case "headshot_kill.png": return "CsolIconHeadshot";
                case "melee_kill.png": return "CsolIconMelee";
                case "revenge.png": return "CsolIconRevenge";
                case "firstkill.png": return "CsolIconFirstKill";
                case "assist.png": return "CsolIconAssist";
                default: return "CsolIconHeadshot";
            }
        }
    }
}

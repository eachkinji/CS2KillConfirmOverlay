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
        // 13 slot rows driven by PackCatalogService.CsolIconSlotFileNames.
        // CSOL has no FX / Elite / WeaponBadge overlays, so the dialog stays
        // minimal: name + 13 image-picker rows.
        private async Task ShowCreateCsolIconPackDialogAsync(
            string initialDisplayName = null,
            IReadOnlyDictionary<string, StorageFile> initialFiles = null)
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

            var layout = CreatePackDialogLayout(
                LocalizationManager.Text("CreateIconPack"),
                LocalizationManager.Text("IconPackCreationHint"),
                LocalizationManager.Text("IconPackNamePlaceholder"),
                initialDisplayName,
                out var nameBox);

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
                    selectedFiles, existingFile);
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

            await PackCatalogService.CreateCsolIconPackAsync(displayName, selectedFiles);
        }

        // Maps a CSOL icon slot filename to its localization key.
        private static string CsolIconLabelKeyFor(string fileName)
        {
            switch (fileName)
            {
                case "badge_headshot.png": return "CsolIconHeadshot";
                case "badge_knife.png": return "CsolIconKnife";
                case "badge_firstkill.png": return "CsolIconFirstKill";
                case "badge_lastkill.png": return "CsolIconLastKill";
                case "multi2.png": return "CsolIconMulti2";
                case "multi3.png": return "CsolIconMulti3";
                case "multi4.png": return "CsolIconMulti4";
                case "multi5.png": return "CsolIconMulti5";
                case "multi6.png": return "CsolIconMulti6";
                case "multi7.png": return "CsolIconMulti7";
                case "multi8.png": return "CsolIconMulti8";
                case "multi9.png": return "CsolIconMulti9";
                case "multi10.png": return "CsolIconMulti10";
                default: return "CsolIconHeadshot";
            }
        }
    }
}
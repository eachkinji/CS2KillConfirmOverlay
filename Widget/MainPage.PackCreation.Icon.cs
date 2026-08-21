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
        private async Task ShowCreateIconPackDialogAsync(
            string initialDisplayName = null,
            IReadOnlyDictionary<string, StorageFile> initialFiles = null)
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
                ("badge_headshot_gold.png", LocalizationManager.Text("FirstLastKill")),
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

            await ShowPackCreationDialogAsync(
                LocalizationManager.Text("CreateIconPack"),
                LocalizationManager.Text("IconPackCreationHint"),
                slots,
                GameStyleMode.Crossfire,
                PackCatalogService.CreateIconPackAsync,
                initialDisplayName,
                initialFiles);
        }

        private async Task ShowPackCreationDialogAsync(
            string title,
            string description,
            (string FileName, string Label)[] slots,
            GameStyleMode currentGame,
            Func<string, IReadOnlyDictionary<string, StorageFile>, Task> createHandler,
            string initialDisplayName = null,
            IReadOnlyDictionary<string, StorageFile> initialFiles = null)
        {
            var selectedFiles = initialFiles != null
                ? new Dictionary<string, StorageFile>(initialFiles, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, StorageFile>(StringComparer.OrdinalIgnoreCase);

            var layout = CreatePackDialogLayout(
                title,
                description,
                LocalizationManager.Text("IconPackNamePlaceholder"),
                initialDisplayName,
                out var nameBox);

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
            await createHandler(displayName, selectedFiles);
        }
    }
}
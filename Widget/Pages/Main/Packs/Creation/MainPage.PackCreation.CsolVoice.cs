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
        // CSOL voice pack creation dialog.
        // Slot rows driven by CSOL's actual asset layout: 1.wav..10.wav (kill
        // streak 1-10), headshot.wav, knife.wav, revenge.wav (last-kill revenge
        // voice), assist.wav (assist). CSOL has no dedicated first-kill voice.
        // No common-overlay sub-card (CSOL has no shared overlay audio). After
        // Primary the dialog calls PackCatalogService.CreateCsolVoicePackAsync.
        private async Task ShowCreateCsolVoicePackDialogAsync(
            string initialDisplayName = null,
            IReadOnlyDictionary<string, IReadOnlyList<StorageFile>> initialFiles = null,
            StorageFile initialHeadImageFile = null)
        {
            if (await TryBatchImportVoiceAsync(initialFiles, initialHeadImageFile, PackCatalogService.CreateCsolVoicePackAsync)) return;

            var slots = new[]
            {
                ("1.wav", LocalizationManager.Text("CsolSlot1")),
                ("2.wav", LocalizationManager.Text("CsolSlot2")),
                ("3.wav", LocalizationManager.Text("CsolSlot3")),
                ("4.wav", LocalizationManager.Text("CsolSlot4")),
                ("5.wav", LocalizationManager.Text("CsolSlot5")),
                ("6.wav", LocalizationManager.Text("CsolSlot6")),
                ("7.wav", LocalizationManager.Text("CsolSlot7")),
                ("8.wav", LocalizationManager.Text("CsolSlot8")),
                ("9.wav", LocalizationManager.Text("CsolSlot9")),
                ("10.wav", LocalizationManager.Text("CsolSlot10")),
                ("headshot.wav", LocalizationManager.Text("CsolSlotHeadshot")),
                ("knife.wav", LocalizationManager.Text("CsolSlotKnife")),
                ("revenge.wav", LocalizationManager.Text("CsolSlotRevenge")),
                ("assist.wav", LocalizationManager.Text("CsolSlotAssist")),
                ("grenade.wav", LocalizationManager.Text("GrenadeKillVoiceLabel"))
            };

            var selectedFiles = CreateVoiceSelectionMap(initialFiles);
            StorageFile headImageFile = initialHeadImageFile;

            var layout = CreatePackDialogLayout(
                LocalizationManager.Text("CreateCsolVoicePack"),
                LocalizationManager.Text("CsolVoicePackCreationHint"),
                LocalizationManager.Text("CsolVoicePackNamePlaceholder"),
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
                MaxHeight = 380,
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
                selectedFiles.TryGetValue(slot.Item1, out List<StorageFile> existingFiles);
                var row = await CreateVoiceSlotRowAsync(
                    slot.Item1, slot.Item2, GameStyleMode.Csol,
                    selectedFiles, existingFiles);
                slotPanel.Children.Add(row);
            }

            layout.Children.Add(scroll);

            ContentDialogResult result = await ShowPackDialogAsync(
                layout, LocalizationManager.Text("Create"), LocalizationManager.Text("Cancel"));
            if (result != ContentDialogResult.Primary)
            {
                return;
            }

            string displayName = string.IsNullOrWhiteSpace(nameBox.Text)
                ? LocalizationManager.Text("CsolVoicePack")
                : nameBox.Text.Trim();

            await PackCatalogService.CreateCsolVoicePackAsync(
                displayName,
                new VoicePackBuildOptions
                {
                    SelectedFileGroups = AsReadOnlyVoiceSelection(selectedFiles),
                    CommonOverlayEnabled = null,
                    UseBuiltInDefaultCommonOverlay = false,
                    CommonOverlayFile = null,
                    HeadImageFile = headImageFile
                });
        }
    }
}

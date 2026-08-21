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
        // 15 slot rows (1.wav..10.wav, headshot.wav, knife.wav, first.wav,
        // last.wav, assist.wav). No common-overlay sub-card (CSOL has no
        // shared overlay audio). No kill-1 special hint. After Primary the
        // dialog calls PackCatalogService.CreateCsolVoicePackAsync.
        private async Task ShowCreateCsolVoicePackDialogAsync(
            string initialDisplayName = null,
            IReadOnlyDictionary<string, StorageFile> initialFiles = null,
            StorageFile initialHeadImageFile = null)
        {
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
                ("first.wav", LocalizationManager.Text("CsolSlotFirst")),
                ("last.wav", LocalizationManager.Text("CsolSlotLast")),
                ("assist.wav", LocalizationManager.Text("CsolSlotAssist"))
            };

            var selectedFiles = initialFiles != null
                ? new Dictionary<string, StorageFile>(initialFiles, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, StorageFile>(StringComparer.OrdinalIgnoreCase);
            StorageFile headImageFile = initialHeadImageFile;

            var layout = CreatePackDialogLayout(
                LocalizationManager.Text("CreateVoicePack"),
                LocalizationManager.Text("VoicePackCreationHint"),
                LocalizationManager.Text("VoicePackNamePlaceholder"),
                initialDisplayName,
                out var nameBox);

            var headCard = await CreateHeadImageCardAsync(
                "ms-appx:///Assets/KillConfirmCode/Original/badge_headshot.PNG",
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
                selectedFiles.TryGetValue(slot.Item1, out StorageFile existingFile);
                var row = await CreateSlotRowAsync(
                    slot.Item1, slot.Item2, isAudio: true, GameStyleMode.Csol,
                    selectedFiles, existingFile);
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
                ? LocalizationManager.Text("NewPack")
                : nameBox.Text.Trim();

            await PackCatalogService.CreateCsolVoicePackAsync(
                displayName,
                new VoicePackBuildOptions
                {
                    SelectedFiles = selectedFiles,
                    CommonOverlayEnabled = null,
                    UseBuiltInDefaultCommonOverlay = false,
                    CommonOverlayFile = null,
                    HeadImageFile = headImageFile
                });
        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using Windows.Data.Json;
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
        // Keep the editor's slots aligned with the native Valorant audio layers.
        private async Task ShowCreateValorantVoicePackDialogAsync(
            string initialDisplayName = null,
            IReadOnlyDictionary<string, IReadOnlyList<StorageFile>> initialFiles = null,
            StorageFile initialHeadImageFile = null,
            string defaultHeadPreviewUri = null,
            VoicePackItem editingItem = null)
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
                ("headshot.wav", isChinese ? "通用爆头（分级爆头为空时使用）" : "Fallback headshot"),
                ("headshot_1.wav", isChinese ? "1 杀爆头" : "Kill 1 headshot"),
                ("headshot_2.wav", isChinese ? "2 杀爆头" : "Kill 2 headshot"),
                ("headshot_3.wav", isChinese ? "3 杀爆头" : "Kill 3 headshot"),
                ("headshot_4.wav", isChinese ? "4 杀爆头" : "Kill 4 headshot"),
                ("headshot_5.wav", isChinese ? "5 杀爆头" : "Kill 5 headshot"),
                ("appear.wav", isChinese ? "首次出现音效（appear）" : "Appear"),
                ("transition.wav", isChinese ? "连杀切换音效（transition）" : "Transition")
            };

            var selectedFiles = CreateVoiceSelectionMap(initialFiles);
            StorageFile headImageFile = initialHeadImageFile;

            var layout = CreatePackDialogLayout(
                isChinese ? (editingItem == null ? "创建瓦语音包" : "编辑瓦语音包") : "Valorant audio pack",
                isChinese
                    ? "支持连杀、分级爆头、出现和切换音效。连杀留空使用内置语音；爆头和出现/切换留空则不叠加。内置包另存副本，自定义包保存到原包。"
                    : "Edit kill, headshot, appear and transition audio. Empty kill slots use Base; empty optional layers stay silent. Built-in packs save as copies.",
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
            JsonArray enabledSlots = null;
            if (editingItem != null)
            {
                var folder = await GetVoicePackFolderAsync(editingItem);
                if (folder != null)
                {
                    var manifest = JsonObject.Parse(await FileIO.ReadTextAsync(await folder.GetFileAsync("manifest.json")));
                    enabledSlots = manifest.GetNamedObject("audio", null)?.GetNamedArray("overlay_slots", null);
                }
            }
            var overlays = new Dictionary<string, CheckBox>();
            var overlayRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            slotContainer.Children.Add(new TextBlock { Text = isChinese ? "叠加出现 / 切换音效的连杀阶段" : "Enable appear / transition at", TextWrapping = TextWrapping.Wrap });
            for (int kill = 1; kill <= 5; kill++)
            {
                var check = new CheckBox { Content = isChinese ? kill + " 杀" : "Kill " + kill,
                    IsChecked = enabledSlots == null || enabledSlots.Any(value => value.ValueType == JsonValueType.String && value.GetString() == "kill_" + kill) };
                overlays[kill + ".wav"] = check;
                overlayRow.Children.Add(check);
            }
            slotContainer.Children.Add(overlayRow);
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
                layout, isChinese ? (editingItem?.IsBuiltIn == true ? "另存副本" : "保存") : "Save", LocalizationManager.Text("Cancel"));
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

            try
            {
                await PackCatalogService.SaveValorantVoiceEditAsync(editingItem, packName, new VoicePackBuildOptions
                {
                    SelectedFileGroups = AsReadOnlyVoiceSelection(selectedFiles),
                    HeadImageFile = headImageFile,
                    CommonOverlayEnabled = overlays.ToDictionary(pair => pair.Key, pair => pair.Value.IsChecked == true)
                });
            }
            catch (Exception ex) { await ShowMessageAsync(isChinese ? "保存失败，原包已保留" : "Save failed; original retained", ex.Message); }
        }
    }
}

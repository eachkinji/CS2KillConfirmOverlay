using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using KillConfirmGameBar.Services;
using Windows.Data.Json;
using Windows.Storage;
using Windows.UI.Xaml.Controls;

namespace KillConfirmGameBar
{
    public sealed partial class MainPage
    {
        private bool UsesGenericAudioEditor => SettingsGameStyle == GameStyleMode.Calabiyau
            || (ActiveAudioPackKind == AudioPackKind.CharacterVoice && SettingsGameStyle != GameStyleMode.Crossfire)
            || (ActiveAudioPackKind == AudioPackKind.SoundEffect && SettingsGameStyle == GameStyleMode.Crossfire);

        private static bool IsGenericAudioPack(VoicePackItem item)
            => item.Key.IndexOf("_audio_", StringComparison.OrdinalIgnoreCase) >= 0
                || PackCatalogService.GetAudioPackStyle(item) == GameStyleMode.Calabiyau
                || (PackCatalogService.GetAudioPackKind(item) == AudioPackKind.CharacterVoice
                    && PackCatalogService.GetAudioPackStyle(item) != GameStyleMode.Crossfire)
                || (PackCatalogService.GetAudioPackKind(item) == AudioPackKind.SoundEffect
                    && PackCatalogService.GetAudioPackStyle(item) == GameStyleMode.Crossfire);

        private async Task ShowCreateGenericAudioPackDialogAsync(GameStyleMode style, AudioPackKind kind,
            string initialName = null, IReadOnlyDictionary<string, IReadOnlyList<StorageFile>> initialFiles = null,
            StorageFile initialHead = null)
        {
            bool chinese = LocalizationManager.Current == UiLanguage.SimplifiedChinese;
            string label = kind == AudioPackKind.CharacterVoice
                ? (chinese ? "语音包" : "voice pack") : (chinese ? "音效包" : "sound effect pack");
            var files = CreateVoiceSelectionMap(initialFiles);
            StorageFile head = initialHead;
            var layout = CreatePackDialogLayout((chinese ? "新建" : "New ") + label,
                kind == AudioPackKind.CharacterVoice
                    ? (chinese ? "选择角色台词。一个事件可放入多句台词，播放时随机选择；未设置的事件使用通用击杀声音。" : "Choose character dialogue. Multiple clips per event are picked randomly; unset events use the general kill cue.")
                    : (chinese ? "可为图标和装饰的不同事件设置声音；未设置时使用通用击杀声音。" : "Choose sound effects for icon and decoration events; unset events use the general kill cue."),
                chinese ? label + "名称" : "Pack name", initialName, out var nameBox);
            layout.Children.Add(await CreateHeadImageCardAsync(
                "ms-appx:///Assets/GameLogos/" + GameStyleService.ToStorageValue(style) + ".png", head,
                file => head = file, () => head = null));
            var labels = new Dictionary<string, string>
            {
                { "normal", chinese ? "普通击杀" : "Normal kill" }, { "headshot", chinese ? "爆头" : "Headshot" },
                { "knife", chinese ? "近战击杀" : "Melee kill" }, { "grenade", chinese ? "道具击杀" : "Grenade kill" },
                { "first_kill", chinese ? "首次击杀" : "First kill" }, { "last_kill", chinese ? "最后击杀" : "Last kill" }
            };
            var slots = new StackPanel { Spacing = 8 };
            foreach (var slot in PackCatalogService.GenericAudioSlotMapping)
            {
                string fileName = slot.Key + ".wav";
                files.TryGetValue(fileName, out List<StorageFile> selected);
                string title = labels.TryGetValue(slot.Key, out string text) ? text : slot.Key + (chinese ? " 杀" : " kills");
                slots.Children.Add(await CreateVoiceSlotRowAsync(fileName, title, style, files, selected));
            }
            layout.Children.Add(new ScrollViewer { MaxHeight = 420, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = slots });
            if (await ShowPackDialogAsync(layout, LocalizationManager.Text("Create"), LocalizationManager.Text("Cancel")) != ContentDialogResult.Primary) return;
            if (!files.Values.Any(group => group != null && group.Count > 0))
            {
                await ShowMessageAsync(chinese ? "尚未选择声音" : "No audio selected",
                    chinese ? "至少为一个事件选择音频文件。" : "Choose an audio file for at least one event.");
                return;
            }
            await PackCatalogService.CreateAudioPackAsync(style, kind,
                string.IsNullOrWhiteSpace(nameBox.Text) ? GameStyleService.ToDisplayName(style) + " " + label : nameBox.Text.Trim(),
                new VoicePackBuildOptions { SelectedFileGroups = AsReadOnlyVoiceSelection(files), HeadImageFile = head });
        }

        private async Task<bool> TryImportTypedAudioZipAsync()
        {
            if (_providedPackZipFile == null) return false;
            if (!UsesGenericAudioEditor)
            {
                bool typed = false;
                using (var stream = await _providedPackZipFile.OpenStreamForReadAsync())
                using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
                {
                    foreach (var entry in archive.Entries.Where(e => string.Equals(Path.GetFileName(e.FullName), "manifest.json", StringComparison.OrdinalIgnoreCase)))
                    {
                        using (var reader = new StreamReader(entry.Open()))
                            if (JsonObject.TryParse(await reader.ReadToEndAsync(), out JsonObject json)
                                && PackCatalogService.TryParseAudioKind(json.GetNamedString("audio_kind", ""), out _)) { typed = true; break; }
                    }
                }
                if (!typed) return false;
            }
            var extracted = await ExtractZipToTemporaryFolderAsync(_providedPackZipFile);
            try
            {
                var folder = await FindAudioManifestFolderAsync(extracted);
                if (folder == null)
                {
                    if (!UsesGenericAudioEditor) return false;
                    var names = PackCatalogService.GenericAudioSlotMapping.Keys.Select(key => key + ".wav").ToArray();
                    folder = await FindBestPackFolderAsync(extracted, names);
                    var groups = await CollectVoiceFileGroupsFromManifestAsync(folder, PackCatalogService.GenericAudioSlotMapping);
                    if (!groups.Values.Any(group => group != null && group.Count > 0))
                        throw new System.IO.InvalidDataException(LocalizationManager.Text("ZipImportNoFilesMessage"));
                    var head = await TryGetCustomPackHeadImageAsync(folder.Path);
                    if (_batchPackImport)
                        await PackCatalogService.CreateAudioPackAsync(SettingsGameStyle, ActiveAudioPackKind,
                            _providedPackZipFile.DisplayName, new VoicePackBuildOptions { SelectedFileGroups = groups, HeadImageFile = head });
                    else await ShowCreateGenericAudioPackDialogAsync(SettingsGameStyle, ActiveAudioPackKind,
                        _providedPackZipFile.DisplayName, groups, head);
                    return true;
                }
                var manifest = JsonObject.Parse(await FileIO.ReadTextAsync(await folder.GetFileAsync("manifest.json")));
                bool typed = PackCatalogService.TryParseAudioKind(manifest.GetNamedString("audio_kind", ""), out AudioPackKind declared);
                if (!typed && !UsesGenericAudioEditor) return false;
                await PackCatalogService.ImportAudioManifestPackAsync(folder, SettingsGameStyle, ActiveAudioPackKind);
                if (typed && declared != ActiveAudioPackKind)
                {
                    _packImportNotes.Add(_providedPackZipFile.Name + (LocalizationManager.Current == UiLanguage.SimplifiedChinese
                        ? "：已按包内类型放入" + (declared == AudioPackKind.CharacterVoice ? "语音包库" : "音效包库")
                        : ": imported into its declared audio library."));
                    if (!_batchPackImport) SelectGameTab(declared == AudioPackKind.CharacterVoice ? "voice" : "sound_effect");
                }
                return true;
            }
            finally { try { await extracted.DeleteAsync(); } catch { } }
        }

        private static async Task<StorageFolder> FindAudioManifestFolderAsync(StorageFolder folder)
        {
            try
            {
                var file = await folder.GetFileAsync("manifest.json");
                if (JsonObject.TryParse(await FileIO.ReadTextAsync(file), out JsonObject manifest)
                    && manifest.TryGetValue("audio", out IJsonValue audio) && audio.ValueType == JsonValueType.Object) return folder;
            }
            catch (System.IO.FileNotFoundException) { }
            foreach (var child in await folder.GetFoldersAsync())
            {
                var found = await FindAudioManifestFolderAsync(child);
                if (found != null) return found;
            }
            return null;
        }
    }
}

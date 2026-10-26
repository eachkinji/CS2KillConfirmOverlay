using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Data.Json;
using Windows.Storage;

namespace KillConfirmGameBar.Services
{
    public static partial class PackCatalogService
    {
        public static async Task SaveValorantVoiceEditAsync(VoicePackItem item, string name, VoicePackBuildOptions options)
        {
            if (item == null || item.IsBuiltIn)
            {
                await CreateValorantVoicePackAsync(name, options);
                return;
            }
            await ValorantPackEditing.UpdateAsync(item.FolderPath, async stage =>
            {
                var original = JsonObject.Parse(File.ReadAllText(Path.Combine(stage.Path, "manifest.json")).TrimStart('\uFEFF'));
                var originalAudio = original.GetNamedObject("audio", new JsonObject());
                var originalSlots = originalAudio.GetNamedObject("slots", new JsonObject());
                // Remove old auto-discoverable slot files from the staged copy, so
                // clearing a slot cannot revive its old recording on the next load.
                var aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var pair in ValorantVoiceSlotMapping)
                    foreach (string alias in Helpers.AudioSlotAliases.GetStemAliases(pair.Key, pair.Value)) aliases.Add(alias);
                foreach (string path in Directory.GetFiles(stage.Path))
                    if (SupportedAudioExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase)
                        && aliases.Contains(Helpers.AudioSlotAliases.ExtractBaseStem(path))) File.Delete(path);
                await CopySelectedVoiceFilesAsync(stage, options);
                for (int kill = 1; kill <= 5; kill++)
                {
                    if ((await FindAudioFileNamesAsync(stage, kill.ToString())).Count > 0) continue;
                    var file = await StorageFile.GetFileFromApplicationUriAsync(new Uri(
                        "ms-appx:///KillConfirmService/sounds/" + ValorantPackService.DefaultKey + "/" + kill + ".wav"));
                    await file.CopyAsync(stage, kill + ".wav", NameCollisionOption.ReplaceExisting);
                }
                // Audio bytes are cached by path in the service. Give this edit
                // new paths so the next preset sync cannot reuse stale bytes.
                string revision = Guid.NewGuid().ToString("N");
                foreach (string path in Directory.GetFiles(stage.Path))
                    if (SupportedAudioExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase)
                        && aliases.Contains(Helpers.AudioSlotAliases.ExtractBaseStem(path)))
                        File.Move(path, Path.Combine(stage.Path, Path.GetFileNameWithoutExtension(path) + "__edit_" + revision + Path.GetExtension(path)));
                await WriteGeneratedVoiceManifestAsync(stage, name, "valorant", ValorantVoiceSlotMapping, options.CommonOverlayEnabled);
                var generated = JsonObject.Parse(File.ReadAllText(Path.Combine(stage.Path, "manifest.json")));
                var newAudio = generated.GetNamedObject("audio");
                foreach (var pair in newAudio.GetNamedObject("slots")) originalSlots[pair.Key] = pair.Value;
                originalAudio["slots"] = originalSlots;
                originalAudio["overlay_slots"] = newAudio["overlay_slots"];
                var gains = originalAudio.GetNamedObject("slot_gains", new JsonObject());
                foreach (var pair in newAudio.GetNamedObject("slot_gains"))
                    if (!gains.ContainsKey(pair.Key)) gains[pair.Key] = pair.Value;
                originalAudio["slot_gains"] = gains;
                // These historical source claims no longer describe user-edited slots.
                originalAudio.Remove("fallback_slots");
                originalAudio.Remove("recovered_original_slots");
                original["audio"] = originalAudio;
                ValorantPackEditing.SetName(original, name);
                await ValorantPackEditing.SetHeadAsync(stage, original, options.HeadImageFile);
                File.WriteAllText(Path.Combine(stage.Path, "manifest.json"), original.Stringify());
            });
            var catalog = await LoadAsync();
            var current = catalog.VoicePacks.FirstOrDefault(pack => pack.Key == item.Key);
            if (current != null) current.DisplayName = name;
            await SaveAsync(catalog);
            await RefreshValorantExternalPacksAsync();
        }
    }

    internal static class ValorantPackEditing
    {
        public static int Revision { get; private set; }
        private static readonly System.Threading.SemaphoreSlim EditLock = new System.Threading.SemaphoreSlim(1, 1);

        internal static void SetName(JsonObject manifest, string name)
        {
            manifest["name"] = JsonValue.CreateStringValue(name);
            manifest["display_name"] = JsonValue.CreateStringValue(name);
            manifest["display_name_zh_cn"] = JsonValue.CreateStringValue(name);
        }

        internal static async Task SetHeadAsync(StorageFolder folder, JsonObject manifest, StorageFile head)
        {
            if (head == null) return;
            string name = "pack_head" + head.FileType.ToLowerInvariant();
            if (head.FileType.Equals(".tga", StringComparison.OrdinalIgnoreCase))
            {
                name = "pack_head.png";
                await Helpers.TgaDecoder.ConvertTgaToPngAsync(head, folder, name);
            }
            else await head.CopyAsync(folder, name, NameCollisionOption.ReplaceExisting);
            manifest["head_image"] = JsonValue.CreateStringValue(name);
        }

        internal static void CopyTree(string source, string target)
        {
            Directory.CreateDirectory(target);
            foreach (string file in Directory.GetFiles(source)) File.Copy(file, Path.Combine(target, Path.GetFileName(file)), true);
            foreach (string directory in Directory.GetDirectories(source))
            {
                if ((File.GetAttributes(directory) & System.IO.FileAttributes.ReparsePoint) != 0) throw new IOException("Linked folders cannot be edited.");
                CopyTree(directory, Path.Combine(target, Path.GetFileName(directory)));
            }
        }

        // Stage all changes before replacing the active folder. Keep the original
        // outside Packs so a failed save can roll back without duplicate discovery.
        internal static void Commit(string stage, string target, string backup)
        {
            bool existed = Directory.Exists(target);
            if (existed) Directory.Move(target, backup);
            try { Directory.Move(stage, target); }
            catch
            {
                if (existed) Directory.Move(backup, target);
                throw;
            }
        }

        internal static async Task UpdateAsync(string target, Func<StorageFolder, Task> edit, string template = null)
        {
            string root = Path.GetFullPath(ApplicationData.Current.LocalFolder.Path).TrimEnd('\\') + "\\";
            target = Path.GetFullPath(target);
            if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new IOException("Only local custom packs can be edited.");
            await EditLock.WaitAsync();
            string work = Path.Combine(ApplicationData.Current.LocalFolder.Path, "PackEditBackups", Guid.NewGuid().ToString("N"));
            string stage = Path.Combine(work, "staged");
            try
            {
                Directory.CreateDirectory(work);
                if (Directory.Exists(target)) CopyTree(target, stage);
                else if (template != null) CopyTree(template, stage);
                else Directory.CreateDirectory(stage);
                await edit(await StorageFolder.GetFolderFromPathAsync(stage));
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                Commit(stage, target, Path.Combine(work, "original"));
                Revision++;
            }
            finally { EditLock.Release(); }
        }
    }
}

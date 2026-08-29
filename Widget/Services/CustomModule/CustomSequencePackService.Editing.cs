using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Data.Json;
using Windows.Storage;

namespace KillConfirmGameBar.Services
{
    internal sealed class CustomSequenceInput
    {
        public string Slot;
        public StorageFile Sheet;
        public StorageFile Metadata;
        public IReadOnlyList<StorageFile> Frames;
        public StorageFolder SourceFolder;
        public StorageFile Video;
        public double VideoStart;
        public double VideoEnd = 5;
        public int? Fps;
        public double? Hold;
        public string Description;
    }

    internal static partial class CustomSequencePackService
    {
        // The slot editor is deliberately strict. Automatic sibling/folder probing
        // belongs to whole-pack import; here the mode selected by the user decides
        // exactly how the chosen files are interpreted.
        internal static async Task<CustomSequenceInput> CreateAtlasInputAsync(string slot,
            IReadOnlyList<StorageFile> selected)
        {
            var files = selected ?? Array.Empty<StorageFile>();
            var sheets = files.Where(f => f.FileType.Equals(".png", StringComparison.OrdinalIgnoreCase)).ToList();
            var metadata = files.Where(f => f.FileType.Equals(".json", StringComparison.OrdinalIgnoreCase)).ToList();
            if (files.Count != 2 || sheets.Count != 1 || metadata.Count != 1
                || !Path.GetFileNameWithoutExtension(sheets[0].Name).Equals(
                    Path.GetFileNameWithoutExtension(metadata[0].Name), StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Select exactly one matching PNG + JSON pair / 请同时选择一对同名 PNG + JSON，不能多选、少选或混入其他文件。");

            var input = new CustomSequenceInput { Slot = slot, Sheet = sheets[0], Metadata = metadata[0] };
            var info = await ReadMetadataFilesAsync(input.Sheet, input.Metadata);
            input.Description = input.Sheet.Name + " + " + input.Metadata.Name
                + " · " + info.Frames + " frames / 帧 · " + info.Width + "×" + info.Height;
            return input;
        }

        internal static CustomSequenceInput CreateLooseFramesInput(string slot,
            IReadOnlyList<StorageFile> selected, bool fromFolder = false)
        {
            var files = selected ?? Array.Empty<StorageFile>();
            var images = files.Where(f => ImageExtensions.Contains(f.FileType.ToLowerInvariant())).ToList();
            if (images.Count == 0)
                throw new InvalidDataException("No supported frame images / 没有可用的散帧图片。");

            if (fromFolder)
            {
                var jsonStems = new HashSet<string>(files
                    .Where(f => f.FileType.Equals(".json", StringComparison.OrdinalIgnoreCase)
                        && !f.Name.Equals("style.json", StringComparison.OrdinalIgnoreCase))
                    .Select(f => Path.GetFileNameWithoutExtension(f.Name)), StringComparer.OrdinalIgnoreCase);
                if (images.Any(f => f.FileType.Equals(".png", StringComparison.OrdinalIgnoreCase)
                    && jsonStems.Contains(Path.GetFileNameWithoutExtension(f.Name))))
                    throw new InvalidDataException("This folder contains a PNG/JSON atlas / 该目录包含同名 PNG + JSON 图集；请切换到“图集”并同时选择这两个文件，或使用整包导入。");
            }

            return new CustomSequenceInput
            {
                Slot = slot,
                Frames = images,
                Description = images.Count + " frames / 张散帧 · numeric filename order / 按文件名数字排序"
            };
        }

        // Probe before choosing the conversion path: a PNG next to a JSON is an
        // atlas, not one animation frame (CS2 Customizer probe_source / KI-4).
        internal static async Task<CustomSequenceInput> ProbeInputAsync(string slot,
            IReadOnlyList<StorageFile> selected, StorageFolder folder = null)
        {
            var images = selected.Where(f => ImageExtensions.Contains(f.FileType.ToLowerInvariant())).ToList();
            var metadata = selected.Where(f => f.FileType.Equals(".json", StringComparison.OrdinalIgnoreCase)
                && !f.Name.Equals("style.json", StringComparison.OrdinalIgnoreCase)).ToList();
            var pairs = new List<CustomSequenceInput>();

            async Task<StorageFile> SiblingAsync(StorageFile file, string extension)
            {
                string name = Path.GetFileNameWithoutExtension(file.Name) + extension;
                var sibling = selected.FirstOrDefault(f => f.Name.Equals(name, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(Path.GetDirectoryName(f.Path), Path.GetDirectoryName(file.Path), StringComparison.OrdinalIgnoreCase));
                if (sibling != null) return sibling;
                StorageFolder parent = folder;
                if (parent == null)
                {
                    try { parent = await file.GetParentAsync(); }
                    catch (UnauthorizedAccessException) { }
                }
                // A picker grant may cover the selected file but not its siblings.
                // Do not bypass that grant or silently flatten an unprobed atlas.
                if (parent == null) throw new InvalidDataException(
                    file.Name + ": cannot inspect companion files / 无权读取同目录文件，请选择素材目录，或同时选择同名 PNG 和 JSON。");
                return await parent.TryGetItemAsync(name) as StorageFile;
            }

            foreach (var png in images.Where(f => f.FileType.Equals(".png", StringComparison.OrdinalIgnoreCase)))
            {
                var json = await SiblingAsync(png, ".json");
                if (json != null) pairs.Add(new CustomSequenceInput { Slot = slot, Sheet = png, Metadata = json });
            }
            foreach (var json in metadata)
            {
                if (pairs.Any(p => p.Metadata.Path.Equals(json.Path, StringComparison.OrdinalIgnoreCase))) continue;
                var png = await SiblingAsync(json, ".png");
                if (png != null) pairs.Add(new CustomSequenceInput { Slot = slot, Sheet = png, Metadata = json });
                else if (folder == null) throw new InvalidDataException(json.Name + ": missing matching PNG / 缺少同名 PNG 图集。");
            }
            if (pairs.Count > 0)
            {
                if (pairs.Count != 1 || images.Any(f => !f.Path.Equals(pairs[0].Sheet.Path, StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidDataException("Choose one atlas per kill slot / 每个击杀槽位请选择一组 PNG/JSON 图集，不要混选图集和散帧；整包请使用图标包库的导入功能。");
                var input = pairs[0];
                var info = await ReadMetadataFilesAsync(input.Sheet, input.Metadata);
                input.Description = input.Sheet.Name + " + " + input.Metadata.Name
                    + " · " + info.Frames + " frames / 帧 · " + info.Width + "×" + info.Height;
                return input;
            }
            if (images.Count == 0) throw new InvalidDataException("No supported frames or atlas / 没有支持的帧图片或 PNG/JSON 图集。");
            return new CustomSequenceInput { Slot = slot, Frames = images,
                Description = images.Count + " frames / 张帧图片 · numeric filename order / 按文件名数字排序" };
        }

        internal static async Task<List<CustomSequenceInput>> ReadInputsAsync(StorageFolder folder, ICollection<string> warnings)
        {
            var files = (await folder.GetFilesAsync()).ToDictionary(f => f.Name, StringComparer.OrdinalIgnoreCase);
            var directories = (await folder.GetFoldersAsync()).ToDictionary(f => f.Name, StringComparer.OrdinalIgnoreCase);
            var standard = new List<CustomSequenceInput>();
            var incomplete = new List<string>();
            for (int level = 1; level <= 5; level++)
                foreach (string suffix in new[] { "", "hs" })
                {
                    string slot = level + suffix;
                    files.TryGetValue(slot + ".png", out StorageFile sheet);
                    files.TryGetValue(slot + ".json", out StorageFile metadata);
                    if (sheet != null && metadata != null)
                        standard.Add(new CustomSequenceInput { Slot = slot, Sheet = sheet, Metadata = metadata, Description = sheet.Name + " + " + metadata.Name });
                    else if (sheet != null || metadata != null) incomplete.Add(slot);
                }
            // As in probe_pack: standard pairs take precedence over loose sources.
            if (standard.Count > 0)
            {
                if (incomplete.Count > 0) warnings?.Add("Skipped incomplete PNG/JSON pairs / 跳过不完整图集：" + string.Join(", ", incomplete));
                return standard;
            }

            var candidates = new Dictionary<string, IStorageItem>();
            var ranks = new Dictionary<string, int>();
            foreach (IStorageItem item in files.Values.Cast<IStorageItem>().Concat(directories.Values).OrderBy(i => i.Name, StringComparer.Ordinal))
            {
                string slot = CustomSequenceFormat.ParseLevelName(item.Name);
                if (slot == null) continue;
                int rank = item is StorageFolder ? 2 : Path.GetExtension(item.Name).Equals(".json", StringComparison.OrdinalIgnoreCase) ? 0 : 1;
                if (!ranks.TryGetValue(slot, out int previous) || rank < previous)
                { candidates[slot] = item; ranks[slot] = rank; }
            }
            var inputs = new List<CustomSequenceInput>();
            foreach (var pair in candidates.OrderBy(p => p.Key, StringComparer.Ordinal))
            {
                var input = new CustomSequenceInput { Slot = pair.Key, Description = pair.Value.Name };
                if (pair.Value is StorageFolder frames)
                {
                    // Defer probing to preserve partial import for malformed slots.
                    input.Frames = await frames.GetFilesAsync();
                    input.SourceFolder = frames;
                }
                else
                {
                    var file = (StorageFile)pair.Value;
                    if (file.FileType.Equals(".json", StringComparison.OrdinalIgnoreCase))
                    {
                        string stem = Path.GetFileNameWithoutExtension(file.Name);
                        files.TryGetValue(stem + ".png", out StorageFile sheet);
                        input.Metadata = file;
                        input.Sheet = sheet;
                        if (sheet == null)
                        {
                            // Retain support for legacy folders with FPS/hold-only sidecars.
                            var frameFolder = directories.Values.FirstOrDefault(d => CustomSequenceFormat.ParseLevelName(d.Name) == pair.Key);
                            if (frameFolder != null) input.Frames = await frameFolder.GetFilesAsync();
                        }
                    }
                    else input.Frames = new[] { file };
                }
                inputs.Add(input);
            }
            return inputs;
        }

        internal static async Task<JsonObject> ReadManifestAsync(StorageFolder folder, ICollection<string> warnings)
        {
            if (folder == null) return new JsonObject();
            var file = await folder.TryGetItemAsync("style.json") as StorageFile;
            if (file == null) return new JsonObject();
            if ((await file.GetBasicPropertiesAsync()).Size > 1024 * 1024) throw new InvalidDataException("style.json exceeds 1 MB / 配置文件过大。");
            if (JsonObject.TryParse(await FileIO.ReadTextAsync(file), out JsonObject manifest)) return manifest;
            warnings?.Add("Ignored invalid optional style.json / style.json 无效，已使用文件或目录名。");
            return new JsonObject();
        }

        internal static async Task<IconPackItem> SavePackAsync(string name, IEnumerable<CustomSequenceInput> inputs,
            StorageFolder original = null, string existingKey = null, IProgress<string> progress = null,
            ICollection<string> warnings = null, bool allowPartial = false)
        {
            var selected = inputs.ToList();
            if (selected.Count == 0) throw new InvalidDataException("Choose at least one sequence / 请至少添加一组帧素材。");
            if (selected.Any(i => !CustomSequenceFormat.IsSlot(i.Slot)) || selected.Select(i => i.Slot).Distinct().Count() != selected.Count)
                throw new InvalidDataException("Invalid or duplicate kill slots / 击杀槽位无效或重复。");
            var manifest = await ReadManifestAsync(original, warnings);
            manifest["name"] = JsonValue.CreateStringValue(string.IsNullOrWhiteSpace(name) ? "Custom" : name.Trim());
            manifest["pack_version"] = JsonValue.CreateNumberValue(1);
            StorageFolder root = await PackCatalogService.GetGameIconPacksFolderAsync("custommodule");
            StorageFolder destination = await root.CreateFolderAsync(Guid.NewGuid().ToString("N"), CreationCollisionOption.FailIfExists);
            bool saved = false;
            try
            {
                var levels = new JsonArray();
                var failures = new List<string>();
                foreach (var input in selected.OrderBy(i => i.Slot, StringComparer.Ordinal))
                {
                    progress?.Report("Import / 导入 " + input.Slot);
                    try
                    {
                        await WriteInputAsync(input, destination, warnings);
                        levels.Add(JsonValue.CreateStringValue(input.Slot));
                    }
                    catch (Exception ex) when (allowPartial && !(ex is OutOfMemoryException))
                    {
                        failures.Add(input.Slot + ": " + ex.Message);
                        warnings?.Add("Skipped / 已跳过 " + input.Slot + ": " + ex.Message);
                        foreach (string extension in new[] { ".png", ".json" })
                            if (await destination.TryGetItemAsync(input.Slot + extension) is StorageFile file) await file.DeleteAsync();
                    }
                    long bytes = 0;
                    foreach (var file in await destination.GetFilesAsync()) bytes += (long)(await file.GetBasicPropertiesAsync()).Size;
                    if (bytes > CustomSequenceFormat.MaxArchiveBytes) throw new InvalidDataException("Pack exceeds 512 MiB / 素材包超过 512 MiB。");
                }
                if (levels.Count == 0) throw new InvalidDataException("No usable animations / 没有可用的逐帧素材。\n" + string.Join("\n", failures));
                manifest["levels"] = levels;
                await FileIO.WriteTextAsync(await destination.CreateFileAsync("style.json"), manifest.Stringify());
                if (original != null)
                    foreach (string extra in new[] { "preview.png", "readme.txt", "readme.md", "license.txt" })
                        if (await original.TryGetItemAsync(extra) is StorageFile file)
                        {
                            if ((await file.GetBasicPropertiesAsync()).Size > 16 * 1024 * 1024) throw new InvalidDataException("Attachment too large / 附件过大。");
                            await file.CopyAsync(destination, extra);
                        }
                long storedBytes = 0;
                foreach (var file in await destination.GetFilesAsync()) storedBytes += (long)(await file.GetBasicPropertiesAsync()).Size;
                if (storedBytes > CustomSequenceFormat.MaxArchiveBytes) throw new InvalidDataException("Pack exceeds 512 MiB / 素材包超过 512 MiB。");
                var pack = await PackCatalogService.SaveCustomSequencePackAsync(destination, Text(manifest, "name"), existingKey);
                saved = true;
                return pack;
            }
            finally { if (!saved) await destination.DeleteAsync(StorageDeleteOption.PermanentDelete); }
        }

        private static async Task WriteInputAsync(CustomSequenceInput input, StorageFolder target, ICollection<string> warnings)
        {
            if (input.Video != null)
            {
                await ConvertVideoAsync(input, target, warnings);
                return;
            }
            if (input.SourceFolder != null)
            {
                var probed = await ProbeInputAsync(input.Slot, input.Frames, input.SourceFolder);
                probed.Fps = input.Fps;
                probed.Hold = input.Hold;
                input = probed;
            }
            JsonObject json = input.Metadata == null ? new JsonObject() : await ReadJsonFileAsync(input.Metadata);
            if (input.Sheet != null && input.Metadata != null)
            {
                if ((await input.Sheet.GetBasicPropertiesAsync()).Size > (ulong)CustomSequenceFormat.MaxArchiveBytes)
                    throw new InvalidDataException("Sheet too large / 图集文件过大。");
                await input.Sheet.CopyAsync(target, input.Slot + ".png");
                await input.Metadata.CopyAsync(target, input.Slot + ".json");
                await Task.Run(() => ReadMetadataAsync(target, input.Slot, true));
                if (Number(json, "frames", 0) > CustomSequenceFormat.MaxFrames)
                    warnings?.Add(input.Slot + ": using first 600 frames / 仅播放前 600 帧。");
                if (input.Fps.HasValue || input.Hold.HasValue)
                {
                    if (input.Fps.HasValue) json["fps"] = JsonValue.CreateNumberValue(CustomSequenceFormat.ClampFps(input.Fps.Value));
                    if (input.Hold.HasValue) json["hold_seconds"] = JsonValue.CreateNumberValue(CustomSequenceFormat.ClampHold(input.Hold.Value));
                    await FileIO.WriteTextAsync(await target.GetFileAsync(input.Slot + ".json"), json.Stringify());
                }
                return;
            }
            if (input.Frames == null) throw new InvalidDataException("Missing sheet image / 缺少配套 PNG 图集。");
            int fps = input.Fps ?? CustomSequenceFormat.ClampFps(Number(json, "fps", 30));
            int staticFrames = input.Frames.Count(f => ImageExtensions.Contains(f.FileType.ToLowerInvariant()));
            double hold = input.Hold ?? CustomSequenceFormat.ClampHold(Number(json, "hold_seconds", staticFrames == 1 ? 1 : 0));
            await Task.Run(() => ConvertFramesAsync(input.Frames, target, input.Slot, fps, hold, warnings));
        }

        internal static async Task<JsonObject> ReadJsonFileAsync(StorageFile file)
        {
            if ((await file.GetBasicPropertiesAsync()).Size > 1024 * 1024) throw new InvalidDataException("JSON exceeds 1 MB / JSON 文件过大。");
            if (!JsonObject.TryParse(await FileIO.ReadTextAsync(file), out JsonObject json))
                throw new InvalidDataException(file.Name + ": invalid JSON / JSON 格式错误。");
            return json;
        }

        // Windows PNG decoders can expose only one frame of an APNG. Detect its
        // animation-control chunk so unsupported animations never silently flatten.
        private static async Task RejectAnimatedPngAsync(StorageFile file)
        {
            if (!file.FileType.Equals(".png", StringComparison.OrdinalIgnoreCase)) return;
            using (var stream = await file.OpenStreamForReadAsync())
            using (var reader = new BinaryReader(stream))
            {
                if (!reader.ReadBytes(8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })) return;
                while (stream.Position + 12 <= stream.Length)
                {
                    byte[] size = reader.ReadBytes(4);
                    long length = ((long)size[0] << 24) | ((long)size[1] << 16) | ((long)size[2] << 8) | size[3];
                    string kind = System.Text.Encoding.ASCII.GetString(reader.ReadBytes(4));
                    if (kind == "acTL") throw new InvalidDataException(file.Name + ": APNG animation / APNG 请先导出为图集，不能只导入首帧。");
                    if (kind == "IDAT" || kind == "IEND" || length > stream.Length - stream.Position - 4) return;
                    stream.Seek(length + 4, SeekOrigin.Current);
                }
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Windows.Data.Json;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;

namespace KillConfirmGameBar.Services
{
    internal static class CustomSequencePackService
    {
        internal static readonly string[] ImageExtensions = { ".png", ".jpg", ".jpeg", ".bmp", ".webp" };

        internal static async Task<JsonObject> ReadJsonAsync(StorageFolder folder, string name)
        {
            var file = await folder.TryGetItemAsync(name) as StorageFile;
            if (file == null) return new JsonObject();
            if ((await file.GetBasicPropertiesAsync()).Size > 1024 * 1024)
                throw new InvalidDataException("JSON exceeds 1 MB / JSON 文件过大。");
            if (!JsonObject.TryParse(await FileIO.ReadTextAsync(file), out JsonObject json))
                throw new InvalidDataException(name + ": invalid JSON / JSON 格式错误。");
            return json;
        }

        internal static double Number(JsonObject json, string name, double fallback)
        {
            if (!json.TryGetValue(name, out IJsonValue value)) return fallback;
            if (value.ValueType == JsonValueType.Number) return value.GetNumber();
            if (value.ValueType == JsonValueType.String
                && double.TryParse(value.GetString(), System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out double parsed)) return parsed;
            return fallback;
        }

        internal static string Text(JsonObject json, string name, string fallback = "")
            => json.TryGetValue(name, out IJsonValue value) && value.ValueType == JsonValueType.String ? value.GetString() : fallback;

        internal static async Task<CustomSequenceMetadata> ReadMetadataAsync(StorageFolder folder, string slot, bool validatePixels = false)
        {
            JsonObject json = await ReadJsonAsync(folder, slot + ".json");
            var file = await folder.GetFileAsync(slot + ".png");
            using (IRandomAccessStream stream = await file.OpenReadAsync())
            {
                BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);
                int width = CheckedInt(Number(json, "frame_width", 0));
                int height = CheckedInt(Number(json, "frame_height", 0));
                int columns = CheckedInt(Number(json, "cols", 0));
                if (columns == 0 && width > 0) columns = Math.Max(1, (int)decoder.PixelWidth / width);
                int count = CheckedInt(Number(json, "frames", 0));
                var metadata = new CustomSequenceMetadata
                {
                    Width = width, Height = height, Columns = columns,
                    Frames = count == 0 ? columns : Math.Min(CustomSequenceFormat.MaxFrames, count),
                    Fps = CustomSequenceFormat.ClampFps(Number(json, "fps", 30)),
                    HoldSeconds = CustomSequenceFormat.ClampHold(Number(json, "hold_seconds", 0))
                };
                CustomSequenceFormat.ValidateGeometry(metadata, decoder.PixelWidth, decoder.PixelHeight);
                if (validatePixels)
                {
                    try
                    {
                        await decoder.GetPixelDataAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Straight,
                            new BitmapTransform(), ExifOrientationMode.IgnoreExifOrientation, ColorManagementMode.DoNotColorManage);
                    }
                    catch (Exception ex) { throw new InvalidDataException(slot + ": damaged image / 图像损坏。", ex); }
                }
                return metadata;
            }
        }

        internal static async Task<string> ResolveSlotAsync(StorageFolder folder, int level, bool headshot)
        {
            string normal = Math.Max(1, Math.Min(5, level)).ToString();
            if (headshot && await folder.TryGetItemAsync(normal + "hs.png") is StorageFile
                && await folder.TryGetItemAsync(normal + "hs.json") is StorageFile) return normal + "hs";
            return await folder.TryGetItemAsync(normal + ".png") is StorageFile
                && await folder.TryGetItemAsync(normal + ".json") is StorageFile ? normal : null;
        }

        private static int CheckedInt(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0 || value > int.MaxValue)
                throw new InvalidDataException("Invalid frame metadata / 帧配置数值不合法。");
            return (int)value;
        }

        public static async Task<IconPackItem> ImportZipAsync(StorageFile zip, IProgress<string> progress = null)
        {
            StorageFolder temporary = await ApplicationData.Current.TemporaryFolder.CreateFolderAsync(
                "CustomSequence_" + Guid.NewGuid().ToString("N"), CreationCollisionOption.FailIfExists);
            try
            {
                using (Stream input = await zip.OpenStreamForReadAsync())
                using (var archive = new ZipArchive(input, ZipArchiveMode.Read))
                {
                    if (archive.Entries.Count > CustomSequenceFormat.MaxArchiveEntries)
                        throw new InvalidDataException("Too many ZIP entries / ZIP 文件数量过多。");
                    long total = 0;
                    var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        string path = CustomSequenceFormat.SafeArchivePath(entry.FullName);
                        if (!names.Add(path.TrimEnd('/')) || ((entry.ExternalAttributes >> 16) & 0xf000) == 0xa000)
                            throw new InvalidDataException("Duplicate path or symbolic link / ZIP 含重复路径或符号链接。");
                        total = checked(total + entry.Length);
                        if (total > CustomSequenceFormat.MaxArchiveBytes || entry.Length / (double)Math.Max(1, entry.CompressedLength) > 500)
                            throw new InvalidDataException("ZIP exceeds extraction limits / ZIP 超出安全解压限制。");
                    }
                    long copied = 0;
                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        string path = CustomSequenceFormat.SafeArchivePath(entry.FullName);
                        if (path.EndsWith("/")) continue;
                        string[] parts = path.Split('/');
                        StorageFolder destination = temporary;
                        foreach (string part in parts.Take(parts.Length - 1))
                            destination = await destination.CreateFolderAsync(part, CreationCollisionOption.OpenIfExists);
                        StorageFile file = await destination.CreateFileAsync(parts.Last(), CreationCollisionOption.FailIfExists);
                        using (Stream source = entry.Open())
                        using (Stream target = await file.OpenStreamForWriteAsync())
                        {
                            byte[] buffer = new byte[65536];
                            int read;
                            long entryBytes = 0;
                            while ((read = await source.ReadAsync(buffer, 0, buffer.Length)) > 0)
                            {
                                entryBytes += read;
                                copied += read;
                                if (entryBytes > entry.Length || copied > CustomSequenceFormat.MaxArchiveBytes)
                                    throw new InvalidDataException("ZIP size mismatch / ZIP 解压大小异常。");
                                await target.WriteAsync(buffer, 0, read);
                            }
                        }
                    }
                }
                return await ImportFolderAsync(temporary, progress, zip.DisplayName);
            }
            finally { await temporary.DeleteAsync(StorageDeleteOption.PermanentDelete); }
        }

        public static async Task<IconPackItem> ImportFolderAsync(StorageFolder source, IProgress<string> progress = null, string fallbackName = null)
        {
            source = await FindStyleFolderAsync(source);
            JsonObject manifest = await ReadJsonAsync(source, "style.json");
            string name = Text(manifest, "name", fallbackName ?? source.DisplayName).Trim();
            if (string.IsNullOrEmpty(name)) name = "Custom";
            StorageFolder root = await PackCatalogService.GetGameIconPacksFolderAsync("custommodule");
            StorageFolder destination = await root.CreateFolderAsync(Guid.NewGuid().ToString("N"), CreationCollisionOption.FailIfExists);
            bool registered = false;
            try
            {
                int imported = 0;
                long totalBytes = 0;
                var levels = new JsonArray();
                for (int level = 1; level <= 5; level++)
                {
                    foreach (string suffix in new[] { "", "hs" })
                    {
                        string slot = level + suffix;
                        progress?.Report("Import / 导入 " + slot);
                        StorageFile sheet = await source.TryGetItemAsync(slot + ".png") as StorageFile;
                        StorageFile metadata = await source.TryGetItemAsync(slot + ".json") as StorageFile;
                        if (sheet != null && metadata != null)
                        {
                            totalBytes += (long)(await sheet.GetBasicPropertiesAsync()).Size;
                            if (totalBytes > CustomSequenceFormat.MaxArchiveBytes) throw new InvalidDataException("Pack too large / 素材包过大。");
                            await Task.Run(() => ReadMetadataAsync(source, slot, validatePixels: true));
                            await sheet.CopyAsync(destination, sheet.Name, NameCollisionOption.FailIfExists);
                            await metadata.CopyAsync(destination, metadata.Name, NameCollisionOption.FailIfExists);
                        }
                        else if (suffix == "" && (await source.TryGetItemAsync(level.ToString()) as StorageFolder
                            ?? await source.TryGetItemAsync("kill1-" + level) as StorageFolder) is StorageFolder sequence)
                        {
                            await Task.Run(() => ConvertLegacyAsync(sequence, source, destination, slot));
                        }
                        else
                        {
                            if (sheet != null || metadata != null)
                                throw new InvalidDataException(slot + ": missing PNG/JSON pair / 缺少配套 PNG 或 JSON。");
                            continue;
                        }
                        imported++;
                        levels.Add(JsonValue.CreateStringValue(slot));
                    }
                }
                if (imported == 0) throw new InvalidDataException("No 1–5 frame animations found / 未找到 1～5 杀图集或逐帧目录。");
                manifest["pack_version"] = JsonValue.CreateNumberValue(1);
                manifest["name"] = JsonValue.CreateStringValue(name);
                manifest["levels"] = levels;
                StorageFile style = await destination.CreateFileAsync("style.json");
                await FileIO.WriteTextAsync(style, manifest.Stringify());
                foreach (string extra in new[] { "preview.png", "readme.txt", "readme.md", "license.txt" })
                {
                    if (await source.TryGetItemAsync(extra) is StorageFile file)
                    {
                        if ((await file.GetBasicPropertiesAsync()).Size > 16 * 1024 * 1024) throw new InvalidDataException("Attachment too large / 附件过大。");
                        await file.CopyAsync(destination, extra, NameCollisionOption.ReplaceExisting);
                    }
                }
                long storedBytes = 0;
                foreach (StorageFile file in await destination.GetFilesAsync())
                    storedBytes += (long)(await file.GetBasicPropertiesAsync()).Size;
                if (storedBytes > CustomSequenceFormat.MaxArchiveBytes)
                    throw new InvalidDataException("Pack too large / 素材包过大。");
                IconPackItem pack = await PackCatalogService.RegisterCustomSequencePackAsync(destination, name);
                registered = true;
                return pack;
            }
            finally { if (!registered) await destination.DeleteAsync(StorageDeleteOption.PermanentDelete); }
        }

        private static async Task<StorageFolder> FindStyleFolderAsync(StorageFolder folder)
        {
            for (int depth = 0; depth < 5; depth++)
            {
                for (int i = 1; i <= 5; i++)
                    if (await folder.TryGetItemAsync(i + ".json") != null || await folder.TryGetItemAsync(i + "hs.json") != null
                        || await folder.TryGetItemAsync(i.ToString()) is StorageFolder || await folder.TryGetItemAsync("kill1-" + i) is StorageFolder)
                        return folder;
                var children = await folder.GetFoldersAsync();
                if (children.Count != 1) break;
                folder = children[0];
            }
            return folder;
        }

        private static async Task ConvertLegacyAsync(StorageFolder frames, StorageFolder source, StorageFolder target, string slot)
        {
            // Direct legacy playback uses ordinal filename order in the reference player.
            StorageFile[] files = (await frames.GetFilesAsync()).Where(f => ImageExtensions.Contains(f.FileType.ToLowerInvariant()))
                .OrderBy(f => f.Name, StringComparer.Ordinal).Take(CustomSequenceFormat.MaxFrames).ToArray();
            if (files.Length == 0) throw new InvalidDataException(slot + ": empty frame directory / 逐帧目录为空。");
            int width = 0, height = 0;
            foreach (StorageFile file in files)
            {
                using (var stream = await file.OpenReadAsync())
                {
                    BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);
                    if ((long)decoder.PixelWidth * decoder.PixelHeight > CustomSequenceFormat.MaxSourcePixels)
                        throw new InvalidDataException("Frame too large / 单帧图像过大。");
                    width = Math.Max(width, checked((int)decoder.PixelWidth));
                    height = Math.Max(height, checked((int)decoder.PixelHeight));
                }
            }
            if (width > 4096 || height > 4096) throw new InvalidDataException("Frame exceeds 4096 pixels / 单帧边长超过 4096。");
            int columns = Math.Max(1, Math.Min(files.Length, 4096 / width));
            int rows = (files.Length + columns - 1) / columns;
            int sheetWidth = width * columns, sheetHeight = height * rows;
            if ((long)sheetWidth * sheetHeight > CustomSequenceFormat.MaxSourcePixels)
                throw new InvalidDataException("Sequence too large; reduce frame size / 帧序列过大，请降低素材分辨率。");
            byte[] pixels = new byte[checked(sheetWidth * sheetHeight * 4)];
            for (int i = 0; i < files.Length; i++)
            {
                using (var stream = await files[i].OpenReadAsync())
                {
                    BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);
                    byte[] frame = (await decoder.GetPixelDataAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Straight,
                        new BitmapTransform(), ExifOrientationMode.IgnoreExifOrientation, ColorManagementMode.DoNotColorManage)).DetachPixelData();
                    int fw = (int)decoder.PixelWidth, fh = (int)decoder.PixelHeight;
                    int x = (i % columns) * width, y = (i / columns) * height;
                    for (int row = 0; row < fh; row++)
                        System.Buffer.BlockCopy(frame, row * fw * 4, pixels, ((y + row) * sheetWidth + x) * 4, fw * 4);
                }
            }
            StorageFile image = await target.CreateFileAsync(slot + ".png");
            using (var stream = await image.OpenAsync(FileAccessMode.ReadWrite))
            {
                BitmapEncoder encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
                encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Straight, (uint)sheetWidth, (uint)sheetHeight, 96, 96, pixels);
                await encoder.FlushAsync();
            }
            JsonObject json = await ReadJsonAsync(source, slot + ".json");
            json["frame_width"] = JsonValue.CreateNumberValue(width);
            json["frame_height"] = JsonValue.CreateNumberValue(height);
            json["frames"] = JsonValue.CreateNumberValue(files.Length);
            json["cols"] = JsonValue.CreateNumberValue(columns);
            json["rows"] = JsonValue.CreateNumberValue(rows);
            json["fps"] = JsonValue.CreateNumberValue(CustomSequenceFormat.ClampFps(Number(json, "fps", 30)));
            json["hold_seconds"] = JsonValue.CreateNumberValue(CustomSequenceFormat.ClampHold(Number(json, "hold_seconds", 0)));
            json["loop"] = JsonValue.CreateBooleanValue(false);
            json["version"] = JsonValue.CreateNumberValue(1);
            await FileIO.WriteTextAsync(await target.CreateFileAsync(slot + ".json"), json.Stringify());
        }

        public static async Task ExportAsync(string key, StorageFile output)
        {
            StorageFolder folder = await PackCatalogService.GetImportedIconFolderAsync(key);
            if (folder == null) throw new InvalidDataException("Select a pack first / 请先选择素材包。");
            // Build completely in temporary storage before replacing the chosen output file.
            StorageFile temp = await ApplicationData.Current.TemporaryFolder.CreateFileAsync(Guid.NewGuid().ToString("N") + ".zip");
            try
            {
                using (Stream stream = await temp.OpenStreamForWriteAsync())
                using (var archive = new ZipArchive(stream, ZipArchiveMode.Create))
                {
                    foreach (StorageFile file in await folder.GetFilesAsync())
                    {
                        using (Stream source = await file.OpenStreamForReadAsync())
                        using (Stream target = archive.CreateEntry(file.Name, CompressionLevel.Optimal).Open())
                            await source.CopyToAsync(target);
                    }
                }
                await temp.CopyAndReplaceAsync(output);
            }
            finally { await temp.DeleteAsync(StorageDeleteOption.PermanentDelete); }
        }
    }
}

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
    internal static partial class CustomSequencePackService
    {
        internal static readonly string[] ImageExtensions = { ".png", ".jpg", ".jpeg", ".bmp", ".webp" };

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
            return await ReadMetadataFilesAsync(await folder.GetFileAsync(slot + ".png"),
                await folder.GetFileAsync(slot + ".json"), validatePixels);
        }

        private static async Task<CustomSequenceMetadata> ReadMetadataFilesAsync(StorageFile file, StorageFile metadataFile, bool validatePixels = false)
        {
            JsonObject json = await ReadJsonFileAsync(metadataFile);
            await RejectAnimatedPngAsync(file);
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
                    catch (Exception ex) { throw new InvalidDataException(file.Name + ": damaged image / 图像损坏。", ex); }
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

        public static async Task<IconPackItem> ImportZipAsync(StorageFile zip, IProgress<string> progress = null, ICollection<string> warnings = null)
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
                return await ImportFolderAsync(temporary, progress, zip.DisplayName, warnings);
            }
            finally { await temporary.DeleteAsync(StorageDeleteOption.PermanentDelete); }
        }

        public static async Task<IconPackItem> ImportFolderAsync(StorageFolder source, IProgress<string> progress = null,
            string fallbackName = null, ICollection<string> warnings = null)
        {
            StorageFolder selectedFolder = source;
            source = await FindStyleFolderAsync(source);
            string selectedName = source.Path == selectedFolder.Path ? fallbackName ?? source.DisplayName : source.DisplayName;
            JsonObject manifest = await ReadManifestAsync(source, warnings);
            string name = Text(manifest, "name", selectedName).Trim();
            if (string.IsNullOrEmpty(name)) name = selectedName;
            var inputs = await ReadInputsAsync(source, warnings);
            if (inputs.Count == 0) throw new InvalidDataException(
                "No kill levels found / 未找到击杀等级。整包请按 1～5、1hs～5hs 或 kill1、ace、三杀等命名；只有一组帧图片时请使用图标包库的“自定义”按钮。");
            return await SavePackAsync(name, inputs, source, progress: progress, warnings: warnings, allowPartial: true);
        }

        private static async Task<StorageFolder> FindStyleFolderAsync(StorageFolder folder)
        {
            for (int depth = 0; depth < 5; depth++)
            {
                if ((await folder.GetItemsAsync()).Any(item => CustomSequenceFormat.ParseLevelName(item.Name) != null))
                    return folder;
                var children = await folder.GetFoldersAsync();
                if (children.Count != 1) break;
                folder = children[0];
            }
            return folder;
        }

        private static async Task ConvertFramesAsync(IReadOnlyList<StorageFile> frames, StorageFolder target,
            string slot, int fps, double hold, ICollection<string> warnings)
        {
            StorageFile[] ordered = frames.Where(f => ImageExtensions.Contains(f.FileType.ToLowerInvariant()))
                .OrderBy(f => f.Name, Comparer<string>.Create(CustomSequenceFormat.CompareFrameNames)).ToArray();
            if (ordered.Length == 0) throw new InvalidDataException(slot + ": no supported static frames / 没有支持的静态帧。GIF/APNG/动画 WebP 请先导出为 CS2 Customizer 图集包。");
            if (ordered.Length > CustomSequenceFormat.MaxFrames)
                warnings?.Add(slot + ": using first 600 frames / 超过 600 帧，仅导入前 600 张。");
            StorageFile[] files = ordered.Take(CustomSequenceFormat.MaxFrames).ToArray();
            int width = 0, height = 0;
            foreach (StorageFile file in files)
            {
                await RejectAnimatedPngAsync(file);
                using (var stream = await file.OpenReadAsync())
                {
                    BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);
                    if (decoder.FrameCount != 1)
                        throw new InvalidDataException(file.Name + ": animated source / 动图请先导出为图集，不能只导入首帧。");
                    if ((long)decoder.PixelWidth * decoder.PixelHeight > CustomSequenceFormat.MaxSourcePixels)
                        throw new InvalidDataException("Frame too large / 单帧图像过大。");
                    width = Math.Max(width, checked((int)decoder.PixelWidth));
                    height = Math.Max(height, checked((int)decoder.PixelHeight));
                }
            }
            // Only raw frames reach this path. Native PNG/JSON atlases are copied
            // unchanged, even when the whole atlas is wider than a GPU page.
            double scale = Math.Min(1, 1024.0 / Math.Max(width, height));
            if (scale < 1)
            {
                int originalWidth = width, originalHeight = height;
                width = Math.Max(1, (int)Math.Round(width * scale));
                height = Math.Max(1, (int)Math.Round(height * scale));
                warnings?.Add(slot + ": frames resized / 独立帧已等比缩小 " + originalWidth + "×" + originalHeight + " → " + width + "×" + height);
            }
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
                    int fw = Math.Max(1, (int)Math.Round(decoder.PixelWidth * scale));
                    int fh = Math.Max(1, (int)Math.Round(decoder.PixelHeight * scale));
                    byte[] frame = (await decoder.GetPixelDataAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Straight,
                        new BitmapTransform { ScaledWidth = (uint)fw, ScaledHeight = (uint)fh, InterpolationMode = BitmapInterpolationMode.Fant },
                        ExifOrientationMode.IgnoreExifOrientation, ColorManagementMode.DoNotColorManage)).DetachPixelData();
                    int x = (i % columns) * width + (width - fw) / 2, y = (i / columns) * height + (height - fh) / 2;
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
            var json = new JsonObject();
            json["frame_width"] = JsonValue.CreateNumberValue(width);
            json["frame_height"] = JsonValue.CreateNumberValue(height);
            json["frames"] = JsonValue.CreateNumberValue(files.Length);
            json["cols"] = JsonValue.CreateNumberValue(columns);
            json["rows"] = JsonValue.CreateNumberValue(rows);
            json["fps"] = JsonValue.CreateNumberValue(CustomSequenceFormat.ClampFps(fps));
            json["hold_seconds"] = JsonValue.CreateNumberValue(CustomSequenceFormat.ClampHold(hold));
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

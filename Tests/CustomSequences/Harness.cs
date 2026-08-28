using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using System.Runtime.Serialization;
using KillConfirmGameBar.Services;
using Windows.Storage;
using Windows.Graphics.Imaging;

namespace KillConfirmGameBar.Services
{
    [DataContract]
    public sealed class IconPackItem
    {
        [DataMember] public string Key;
        [DataMember] public string FolderPath;
        [DataMember] public string DisplayName;
        [DataMember] public bool IsBuiltIn, IsVisibleInWidget, OwnsFolder;
    }
    [DataContract]
    public sealed class PackCatalog { [DataMember] public List<IconPackItem> IconPacks = new List<IconPackItem>(); }
    internal static class App { internal static void Log(string text) { } }
    public static partial class PackCatalogService
    {
        internal static readonly PackCatalog Catalog = new PackCatalog();
        private static readonly SemaphoreSlim CatalogIoLock = new SemaphoreSlim(1, 1);
        internal const string CatalogFileName = "catalog.json";
        internal static event EventHandler CatalogChanged;
        private static Task<PackCatalog> LoadAsync() => Task.FromResult(Catalog);
        public static Task<StorageFolder> GetGameIconPacksFolderAsync(string key)
            => TestApplicationData.Current.LocalFolder.CreateFolderAsync("packs", CreationCollisionOption.OpenIfExists).AsTask();
        public static Task<StorageFolder> GetImportedIconFolderAsync(string key)
            => StorageFolder.GetFolderFromPathAsync(Catalog.IconPacks.Single(p => p.Key == key).FolderPath).AsTask();
    }

    public sealed class TestApplicationData
    {
        public static TestApplicationData Current = new TestApplicationData();
        public StorageFolder LocalFolder;
        public StorageFolder TemporaryFolder;
        public TestSettings LocalSettings = new TestSettings();
    }
    public sealed class TestSettings { public TestValues Values = new TestValues(); }
    public sealed class TestValues : Dictionary<string, object>
    {
        public new object this[string key] { get => TryGetValue(key, out object value) ? value : null; set => base[key] = value; }
    }
    internal static class ValorantPackService { public const string DefaultKey = "valorant_test"; }
}

internal static class Harness
{
    private static int checks;
    private static void Check(bool condition, string message)
    { checks++; if (!condition) throw new Exception(message); }
    private static async Task Reject(Func<Task> action, string message)
    {
        bool rejected = false;
        try { await action(); } catch (InvalidDataException) { rejected = true; }
        Check(rejected, message);
    }

    private static async Task<StorageFile> Image(StorageFolder folder, string name, byte red)
    {
        var file = await folder.CreateFileAsync(name);
        using (var stream = await file.OpenAsync(FileAccessMode.ReadWrite))
        {
            var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
            byte[] pixels = { 0, 0, red, 128, 0, 255, 0, 255, 255, 0, 0, 0, 20, 40, 60, 255 };
            encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Straight, 4, 1, 96, 96, pixels);
            await encoder.FlushAsync();
        }
        return file;
    }

    private static async Task<StorageFile> Zip(StorageFolder parent, string name, IDictionary<string, byte[]> files)
    {
        var file = await parent.CreateFileAsync(name);
        using (var stream = await file.OpenStreamForWriteAsync())
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create))
            foreach (var pair in files)
                using (var output = archive.CreateEntry(pair.Key, CompressionLevel.NoCompression).Open())
                    await output.WriteAsync(pair.Value, 0, pair.Value.Length);
        return file;
    }

    private static async Task<StorageFile> SizedImage(StorageFolder folder, string name, int width, int height, int cellWidth = 256)
    {
        var file = await folder.CreateFileAsync(name);
        using (var stream = await file.OpenAsync(FileAccessMode.ReadWrite))
        {
            var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
            byte[] pixels = new byte[width * height * 4];
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                pixels[(y * width + x) * 4 + 2] = (byte)(x / cellWidth + 1);
                pixels[(y * width + x) * 4 + 3] = 128;
            }
            encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Straight, (uint)width, (uint)height, 96, 96, pixels);
            await encoder.FlushAsync();
        }
        return file;
    }

    private static async Task Run(string root)
    {
        Directory.CreateDirectory(root);
        TestApplicationData.Current.LocalFolder = await StorageFolder.GetFolderFromPathAsync(root);
        TestApplicationData.Current.TemporaryFolder = await TestApplicationData.Current.LocalFolder.CreateFolderAsync("temp");
        var local = TestApplicationData.Current.LocalFolder;
        Check(GameStyleService.FromKey("custommodule") == GameStyleMode.CustomModule, "custom style lookup");
        Check(GameStyleService.GetStyleForPackKey("custom_module_icon_test") == GameStyleMode.CustomModule, "custom pack routing");
        Check(GameStyleService.DefaultIconPackKey(GameStyleMode.CustomModule) == "custommodule", "empty library does not fall back to CF");
        Check(GameStyleService.DefaultVoicePackKey(GameStyleMode.CustomModule) == "custommodule", "silent audio preset");
        var cfBefore = KillFeedbackVisibilitySettingsStore.Load(GameStyleMode.Crossfire);
        KillFeedbackVisibilitySettingsStore.Save(GameStyleMode.CustomModule, new KillFeedbackVisibilitySettingsValues { LowerEnabled = false, LowerOpacityPercent = 35 });
        Check(!KillFeedbackVisibilitySettingsStore.Load(GameStyleMode.CustomModule).LowerEnabled, "custom visibility persisted");
        Check(KillFeedbackVisibilitySettingsStore.Load(GameStyleMode.CustomModule).LowerOpacityPercent == 35, "custom opacity persisted");
        Check(KillFeedbackVisibilitySettingsStore.Load(GameStyleMode.Crossfire).LowerEnabled == cfBefore.LowerEnabled
            && KillFeedbackVisibilitySettingsStore.Load(GameStyleMode.Crossfire).LowerOpacityPercent == cfBefore.LowerOpacityPercent, "CF appearance unchanged");
        CustomModuleSettingsStore.Save(new CustomModuleSettings { Fps = 24, Hold = .8, Fade = false, Headshots = false });
        var saved = CustomModuleSettingsStore.Load();
        Check(saved.Fps == 24 && saved.Hold == .8 && !saved.Fade && !saved.Headshots, "playback settings persisted");
        for (int fps = 1; fps <= 60; fps++)
        {
            Check(CustomSequenceFormat.At(0, 60, fps, 0, false).Frame == 0, "first frame");
            Check(CustomSequenceFormat.At(60.0 / fps, 60, fps, 0, false).Finished, "end time");
        }
        Check(CustomSequenceFormat.At(.35, 10, 10, 0, false).Frame == 3, "clock selects skipped frame");
        Check(CustomSequenceFormat.At(1.2, 10, 10, .5, true).Frame == 9, "hold final frame");
        Check(Math.Abs(CustomSequenceFormat.At(1.625, 10, 10, .5, true).Opacity - .5f) < .0001, "fade follows hold");
        Check(CustomSequenceFormat.At(1.75, 10, 10, .5, true).Finished, "fade completion");
        Check(Math.Abs(CustomSequenceFormat.At(.06, 10, 10, 0, true).Opacity - .5f) < .0001, "fade in");
        Check(CustomSequenceFormat.ClampFps(double.NaN) == 30 && CustomSequenceFormat.ClampFps(0) == 1 && CustomSequenceFormat.ClampFps(90) == 60, "FPS defaults and clamps");
        Check(CustomSequenceFormat.At(.5, 1, 30, 1.5, false).Frame == 0, "static hold");
        var pageMetadata = new CustomSequenceMetadata { Width = 2, Height = 2, Columns = 3, Frames = 7 };
        byte[] sourcePixels = Enumerable.Range(0, 6 * 6 * 4).Select(i => (byte)i).ToArray();
        for (int start = 0; start < 7; start += 4)
        {
            int count = Math.Min(4, 7 - start);
            byte[] page = CustomSequenceFormat.RepackPage(sourcePixels, 6, pageMetadata, start, count, 2, out int pageWidth, out int pageHeight);
            Check(pageWidth == 4 && pageHeight == 4, "page dimensions including partial last row");
            for (int f = 0; f < count; f++)
            for (int row = 0; row < 2; row++)
            {
                int sourceOffset = ((((start + f) / 3) * 2 + row) * 6 + ((start + f) % 3) * 2) * 4;
                int targetOffset = (((f / 2) * 2 + row) * pageWidth + (f % 2) * 2) * 4;
                Check(sourcePixels.Skip(sourceOffset).Take(8).SequenceEqual(page.Skip(targetOffset).Take(8)), "page pixels and alpha across source rows/page boundaries");
            }
            if (count == 3) Check(page.Skip(40).Take(8).Concat(page.Skip(56).Take(8)).All(v => v == 0), "unused page cells stay transparent");
        }
        using (var csv = new StreamWriter(Path.Combine(root, "timeline.csv")))
        {
            csv.WriteLine("elapsed,frames,fps,hold,fade,index,opacity,finished");
            foreach (int fps in new[] { 1, 10, 30, 60 })
            foreach (int count in new[] { 1, 10, 201 })
            foreach (double hold in new[] { 0, .5, 1.5 })
            foreach (bool fade in new[] { false, true })
            foreach (double elapsed in new[] { 0, .06, .12, .35, 1, 1.2, 1.625, 1.75, 10, 202 })
            {
                var state = CustomSequenceFormat.At(elapsed, count, fps, hold, fade);
                csv.WriteLine(string.Join(",", new[] { elapsed.ToString("R", System.Globalization.CultureInfo.InvariantCulture), count.ToString(), fps.ToString(), hold.ToString("R", System.Globalization.CultureInfo.InvariantCulture), fade ? "1" : "0", state.Frame.ToString(), state.Opacity.ToString("R", System.Globalization.CultureInfo.InvariantCulture), state.Finished ? "1" : "0" }));
            }
        }

        var source = await local.CreateFolderAsync("source");
        var png = await Image(source, "1.png", 200);
        const string metadata = "{\"frame_width\":2,\"frame_height\":1,\"frames\":2,\"cols\":2,\"rows\":1,\"fps\":10,\"hold_seconds\":0.5,\"loop\":false,\"version\":1}";
        await FileIO.WriteTextAsync(await source.CreateFileAsync("1.json"), metadata);
        await FileIO.WriteTextAsync(await source.CreateFileAsync("style.json"), "{\"pack_version\":1,\"name\":\"兼容测试\",\"author\":\"Test\",\"version\":\"1.0\"}");
        var inputs = new Dictionary<string, byte[]>();
        foreach (var file in await source.GetFilesAsync()) inputs["wrapper/" + file.Name] = File.ReadAllBytes(file.Path);
        var zip = await Zip(local, "input.zip", inputs);
        var imported = await CustomSequencePackService.ImportZipAsync(zip);
        Check(imported.DisplayName == "兼容测试", "manifest name retained");
        var folder = await StorageFolder.GetFolderFromPathAsync(imported.FolderPath);
        var m = await CustomSequencePackService.ReadMetadataAsync(folder, "1");
        Check(m.Width == 2 && m.Height == 1 && m.Frames == 2 && m.Columns == 2 && m.Fps == 10 && m.HoldSeconds == .5, "metadata compatibility");
        Check(File.ReadAllBytes(png.Path).SequenceEqual(File.ReadAllBytes(Path.Combine(folder.Path, "1.png"))), "PNG preserved byte-for-byte");
        var output = await local.CreateFileAsync("roundtrip.zip");
        await CustomSequencePackService.ExportAsync(imported.Key, output);
        var roundtrip = await CustomSequencePackService.ImportZipAsync(output);
        Check(File.ReadAllBytes(Path.Combine(roundtrip.FolderPath, "1.json")).SequenceEqual(System.Text.Encoding.UTF8.GetBytes(metadata)), "metadata roundtrip");
        Check(await CustomSequencePackService.ResolveSlotAsync(folder, 1, true) == "1", "headshot falls back to same normal level");
        Check(await CustomSequencePackService.ResolveSlotAsync(folder, 2, true) == null, "missing level never borrows another level");
        Check(await CustomSequencePackService.ResolveSlotAsync(folder, 0, false) == "1", "level lower clamp");
        var headshots = await local.CreateFolderAsync("headshots-only");
        await png.CopyAsync(headshots, "5hs.png");
        await FileIO.WriteTextAsync(await headshots.CreateFileAsync("5hs.json"), metadata);
        var headshotPack = await CustomSequencePackService.ImportFolderAsync(headshots);
        var headshotFolder = await StorageFolder.GetFolderFromPathAsync(headshotPack.FolderPath);
        Check(await CustomSequencePackService.ResolveSlotAsync(headshotFolder, 99, true) == "5hs", "headshot-only pack and upper clamp");
        Check(await CustomSequencePackService.ResolveSlotAsync(headshotFolder, 5, false) == null, "normal kill does not use headshot-only material");
        await png.CopyAsync(folder, "1hs.png");
        Check(await CustomSequencePackService.ResolveSlotAsync(folder, 1, true) == "1", "incomplete headshot pair falls back");

        var legacy = await local.CreateFolderAsync("legacy");
        var frames = await legacy.CreateFolderAsync("1");
        await Image(frames, "10.png", 100); await Image(frames, "2.png", 200);
        await FileIO.WriteTextAsync(await legacy.CreateFileAsync("1.json"), "{\"fps\":24,\"hold_seconds\":1.25}");
        var legacyPack = await CustomSequencePackService.ImportFolderAsync(legacy);
        var legacyFolder = await StorageFolder.GetFolderFromPathAsync(legacyPack.FolderPath);
        m = await CustomSequencePackService.ReadMetadataAsync(legacyFolder, "1");
        Check(m.Frames == 2 && m.Fps == 24 && m.HoldSeconds == 1.25, "legacy metadata retained");
        using (var stream = await (await legacyFolder.GetFileAsync("1.png")).OpenReadAsync())
        {
            var decoder = await BitmapDecoder.CreateAsync(stream);
            byte[] pixels = (await decoder.GetPixelDataAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Straight, new BitmapTransform(), ExifOrientationMode.IgnoreExifOrientation, ColorManagementMode.DoNotColorManage)).DetachPixelData();
            Check(pixels[2] == 200 && pixels[4 * 4 + 2] == 100, "importer numeric frame order (2 before 10)");
            Check(pixels[3] == 128, "alpha retained");
        }
        var unsafeZip = await Zip(local, "unsafe.zip", new Dictionary<string, byte[]> { ["../escaped.png"] = new byte[] { 1 } });
        await Reject(async () => await CustomSequencePackService.ImportZipAsync(unsafeZip), "traversal rejected");
        Check(!File.Exists(Path.Combine(root, "escaped.png")), "no escaped write");
        var duplicate = await Zip(local, "duplicate.zip", new Dictionary<string, byte[]> { ["A.png"] = new byte[] { 1 }, ["a.png"] = new byte[] { 2 } });
        await Reject(async () => await CustomSequencePackService.ImportZipAsync(duplicate), "case duplicate rejected");
        var excessiveEntries = Enumerable.Range(0, CustomSequenceFormat.MaxArchiveEntries + 1).ToDictionary(i => i + ".txt", i => new byte[0]);
        await Reject(async () => await CustomSequencePackService.ImportZipAsync(await Zip(local, "too-many.zip", excessiveEntries)), "entry count limit");
        var symlink = await local.CreateFileAsync("symlink.zip");
        using (var stream = await symlink.OpenStreamForWriteAsync())
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create))
            archive.CreateEntry("link").ExternalAttributes = unchecked((int)0xa1ff0000);
        await Reject(async () => await CustomSequencePackService.ImportZipAsync(symlink), "symbolic link rejected");
        var compressed = await local.CreateFileAsync("compression-limit.zip");
        using (var stream = await compressed.OpenStreamForWriteAsync())
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create))
        using (var entry = archive.CreateEntry("zeros", CompressionLevel.Optimal).Open())
            await entry.WriteAsync(new byte[1024 * 1024], 0, 1024 * 1024);
        await Reject(async () => await CustomSequencePackService.ImportZipAsync(compressed), "compression ratio limit");
        await FileIO.WriteTextAsync(await source.GetFileAsync("1.json"), "{\"padding\":\"" + new string('x', 1024 * 1024) + "\"}");
        await Reject(async () => await CustomSequencePackService.ImportFolderAsync(source), "JSON size limit");
        int before = PackCatalogService.Catalog.IconPacks.Count;
        await FileIO.WriteTextAsync(await source.GetFileAsync("1.json"), "{\"frame_width\":9999,\"frame_height\":1,\"frames\":2,\"cols\":2}");
        await Reject(async () => await CustomSequencePackService.ImportFolderAsync(source), "invalid grid rejected");
        Check(before == PackCatalogService.Catalog.IconPacks.Count, "failed import not registered");
        Check((await TestApplicationData.Current.TemporaryFolder.GetFoldersAsync()).Count == 0, "ZIP staging cleaned");
        var aliasNames = new[] { "1.png", "kill2.json", "3kill.png", "ace.webp", "三杀", "single-headshot.png", "双杀_爆头", "5HEAD.png", "junk.png", "6.png", "hs.png" };
        using (var aliases = new StreamWriter(Path.Combine(root, "aliases.tsv"), false, new System.Text.UTF8Encoding(false)))
            foreach (string name in aliasNames) aliases.WriteLine(name + "\t" + (CustomSequenceFormat.ParseLevelName(name) ?? ""));
        Check(CustomSequenceFormat.ParseLevelName("三杀_爆头.png") == "3hs", "localized level/headshot alias");
        Check(CustomSequenceFormat.ParseLevelName("kill1-5") == "5", "legacy folder alias retained");
        Check(CustomSequenceFormat.CompareFrameNames("frame-2.png", "frame-10.png") < 0, "numeric frame order");
        Check(CustomSequenceFormat.CompareFrameNames("clip9-frame2.png", "clip1-frame3.png") < 0, "last numeric group decides order");
        Check(CustomSequenceFormat.CompareFrameNames("999999999999999999999.png", "10.png") > 0, "large sequence numbers do not overflow");

        var notes = new List<string>();
        var looseZip = await Zip(local, "loose.zip", new Dictionary<string, byte[]>
        {
            ["Theme/kill1.png"] = File.ReadAllBytes(png.Path),
            ["Theme/ace.png"] = File.ReadAllBytes(png.Path),
            ["Theme/三杀_爆头/10.png"] = File.ReadAllBytes(png.Path),
            ["Theme/三杀_爆头/2.png"] = File.ReadAllBytes(png.Path)
        });
        var loose = await CustomSequencePackService.ImportZipAsync(looseZip, warnings: notes);
        Check(loose.DisplayName == "Theme", "wrapper folder supplies missing pack name");
        var looseFolder = await StorageFolder.GetFolderFromPathAsync(loose.FolderPath);
        Check(await CustomSequencePackService.ResolveSlotAsync(looseFolder, 5, false) == "5", "ace static image imported");
        Check(await CustomSequencePackService.ResolveSlotAsync(looseFolder, 3, true) == "3hs", "headshot frame folder imported");
        Check((await CustomSequencePackService.ReadMetadataAsync(looseFolder, "1")).HoldSeconds == 1, "single-frame default hold");
        await FileIO.WriteTextAsync(await looseFolder.CreateFileAsync("style.json", CreationCollisionOption.ReplaceExisting), "{broken");
        var looseAgain = await CustomSequencePackService.ImportFolderAsync(looseFolder, warnings: notes);
        Check(notes.Any(n => n.Contains("style.json")), "invalid optional manifest reported and ignored");

        notes.Clear();
        var mixed = await Zip(local, "mixed.zip", new Dictionary<string, byte[]>
        {
            ["1.png"] = File.ReadAllBytes(png.Path),
            ["1.json"] = System.Text.Encoding.UTF8.GetBytes(metadata),
            ["2.png"] = File.ReadAllBytes(png.Path),
            ["ace.png"] = File.ReadAllBytes(png.Path)
        });
        var mixedPack = await CustomSequencePackService.ImportZipAsync(mixed, warnings: notes);
        var mixedFolder = await StorageFolder.GetFolderFromPathAsync(mixedPack.FolderPath);
        Check(notes.Any(n => n.Contains("2")), "incomplete standard pair is reported");
        Check(await CustomSequencePackService.ResolveSlotAsync(mixedFolder, 2, false) == null, "standard package skips incomplete pair");
        Check(await CustomSequencePackService.ResolveSlotAsync(mixedFolder, 5, false) == null, "standard pairs precede loose inputs");

        var priorityRoot = await local.CreateFolderAsync("priority");
        await png.CopyAsync(priorityRoot, "triple.png");
        await FileIO.WriteTextAsync(await priorityRoot.CreateFileAsync("triple.json"), metadata);
        var lowerPriority = await priorityRoot.CreateFolderAsync("3");
        await Image(lowerPriority, "0.png", 10);
        var priorityPack = await CustomSequencePackService.ImportFolderAsync(priorityRoot);
        Check((await CustomSequencePackService.ReadMetadataAsync(await StorageFolder.GetFolderFromPathAsync(priorityPack.FolderPath), "3")).Fps == 10, "JSON beats loose image and frame directory");

        var frameFiles = await frames.GetFilesAsync();
        var draft = new[] { new CustomSequenceInput { Slot = "2hs", Frames = frameFiles, Fps = 24, Hold = .75 } };
        var custom = await CustomSequencePackService.SavePackAsync("Draft", draft);
        var customFolder = await StorageFolder.GetFolderFromPathAsync(custom.FolderPath);
        var customMetadata = await CustomSequencePackService.ReadMetadataAsync(customFolder, "2hs");
        Check(customMetadata.Frames == 2 && customMetadata.Fps == 24 && customMetadata.HoldSeconds == .75, "manual frames assigned to chosen slot with asset timing");
        Check(await CustomSequencePackService.ResolveSlotAsync(customFolder, 1, false) == null, "new editor does not manufacture other kill levels");
        custom.IsVisibleInWidget = false;
        int catalogCount = PackCatalogService.Catalog.IconPacks.Count;
        var originalPath = custom.FolderPath;
        var edit = await CustomSequencePackService.SavePackAsync("Renamed", new[] {
            new CustomSequenceInput { Slot = "1", Frames = frameFiles, Fps = 40, Hold = .2 }
        }, customFolder, custom.Key);
        Check(edit.Key == custom.Key && edit.DisplayName == "Renamed" && !edit.IsVisibleInWidget, "edit preserves key/visibility");
        Check(catalogCount == PackCatalogService.Catalog.IconPacks.Count, "edit replaces rather than duplicates");
        Check(!Directory.Exists(originalPath), "owned old copy cleaned only after save");
        var editedFolder = await StorageFolder.GetFolderFromPathAsync(edit.FolderPath);
        Check(await CustomSequencePackService.ResolveSlotAsync(editedFolder, 2, true) == null, "cleared slot not copied back during edit");
        string catalogBeforeFailure = await FileIO.ReadTextAsync(await local.GetFileAsync(PackCatalogService.CatalogFileName));
        var invalidInput = new[] { new CustomSequenceInput { Slot = "1", Metadata = await source.GetFileAsync("1.json"), Sheet = png } };
        await Reject(async () => await CustomSequencePackService.SavePackAsync("Broken", invalidInput, editedFolder, edit.Key), "failed replacement rejected");
        Check(Directory.Exists(edit.FolderPath) && (await CustomSequencePackService.ReadMetadataAsync(editedFolder, "1")).Fps == 40, "failed replacement keeps existing assets");
        Check(catalogBeforeFailure == await FileIO.ReadTextAsync(await local.GetFileAsync(PackCatalogService.CatalogFileName)), "failed edit leaves persisted catalog unchanged");
        await Reject(async () => await CustomSequencePackService.SavePackAsync("Empty", new CustomSequenceInput[0], editedFolder, edit.Key), "empty editor cannot erase old pack");
        await Reject(async () => await CustomSequencePackService.SavePackAsync("Escape", new[] { new CustomSequenceInput { Slot = "../1", Frames = frameFiles } }), "manual slot path rejected");

        // Exercise the production catalog write failure and rollback, not a mock save.
        var persistedCatalog = await local.GetFileAsync(PackCatalogService.CatalogFileName);
        await persistedCatalog.RenameAsync("catalog.saved.json");
        var blockedCatalog = await local.CreateFolderAsync(PackCatalogService.CatalogFileName);
        var packRoot = await PackCatalogService.GetGameIconPacksFolderAsync("custommodule");
        int foldersBeforeFailure = (await packRoot.GetFoldersAsync()).Count;
        bool saveFailed = false;
        try { await CustomSequencePackService.SavePackAsync("Cannot save", draft, editedFolder, edit.Key); }
        catch { saveFailed = true; }
        Check(saveFailed && PackCatalogService.Catalog.IconPacks.Single(p => p.Key == edit.Key).FolderPath == edit.FolderPath, "catalog persistence failure rolls back memory");
        Check((await packRoot.GetFoldersAsync()).Count == foldersBeforeFailure, "failed save discards staged material");
        Check(!(await local.GetFilesAsync()).Any(f => f.Name.StartsWith("custom-catalog-", StringComparison.Ordinal)), "catalog temporary file cleaned");
        await blockedCatalog.DeleteAsync();
        await persistedCatalog.RenameAsync(PackCatalogService.CatalogFileName);
        Check(File.ReadAllBytes(png.Path).SequenceEqual(inputs["wrapper/1.png"]), "source images never modified");
        notes.Clear();
        var capped = await CustomSequencePackService.SavePackAsync("Capped", new[] {
            new CustomSequenceInput { Slot = "1", Frames = Enumerable.Repeat(png, 601).ToList() }
        }, warnings: notes);
        Check(notes.Any(n => n.Contains("600")) && (await CustomSequencePackService.ReadMetadataAsync(
            await StorageFolder.GetFolderFromPathAsync(capped.FolderPath), "1")).Frames == 600, "frame truncation is reported");
        var animatedPng = await local.CreateFileAsync("animated.png");
        byte[] pngBytes = File.ReadAllBytes(png.Path);
        byte[] animationChunk = { 0, 0, 0, 8, 97, 99, 84, 76, 0, 0, 0, 2, 0, 0, 0, 0, 0, 0, 0, 0 };
        uint crc = 0xffffffff;
        foreach (byte value in animationChunk.Skip(4).Take(12))
        {
            crc ^= value;
            for (int bit = 0; bit < 8; bit++) crc = (crc >> 1) ^ ((crc & 1) == 0 ? 0 : 0xedb88320);
        }
        crc ^= 0xffffffff;
        for (int i = 0; i < 4; i++) animationChunk[16 + i] = (byte)(crc >> (24 - 8 * i));
        File.WriteAllBytes(animatedPng.Path, pngBytes.Take(33).Concat(animationChunk).Concat(pngBytes.Skip(33)).ToArray());
        await Reject(async () => await CustomSequencePackService.SavePackAsync("APNG", new[] {
            new CustomSequenceInput { Slot = "1", Frames = new[] { animatedPng } }
        }), "APNG cannot silently flatten into first frame");
        var emptyName = await Zip(local, "fallback-name.zip", new Dictionary<string, byte[]>
        {
            ["1.png"] = File.ReadAllBytes(png.Path), ["1.json"] = System.Text.Encoding.UTF8.GetBytes(metadata),
            ["style.json"] = System.Text.Encoding.UTF8.GetBytes("{\"name\":\"\"}")
        });
        Check((await CustomSequencePackService.ImportZipAsync(emptyName)).DisplayName == "fallback-name", "empty manifest name uses ZIP name");

        notes.Clear();
        var partlyBroken = await Zip(local, "partial.zip", new Dictionary<string, byte[]>
        {
            ["1.png"] = File.ReadAllBytes(png.Path), ["1.json"] = System.Text.Encoding.UTF8.GetBytes(metadata),
            ["2.png"] = File.ReadAllBytes(png.Path), ["2.json"] = System.Text.Encoding.UTF8.GetBytes("{\"frame_width\":9999,\"frame_height\":1,\"frames\":2}")
        });
        var partial = await CustomSequencePackService.ImportZipAsync(partlyBroken, warnings: notes);
        var partialFolder = await StorageFolder.GetFolderFromPathAsync(partial.FolderPath);
        Check(notes.Any(n => n.Contains("2:")) && await CustomSequencePackService.ResolveSlotAsync(partialFolder, 2, false) == null, "damaged standard level is reported and not registered");
        Check(await CustomSequencePackService.ResolveSlotAsync(partialFolder, 1, false) == "1", "valid level survives partial package import");

        var small = await local.CreateFileAsync("2.png");
        using (var stream = await small.OpenAsync(FileAccessMode.ReadWrite))
        {
            var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
            encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Straight, 2, 1, 96, 96, new byte[] { 0, 0, 66, 255, 0, 0, 77, 255 });
            await encoder.FlushAsync();
        }
        var aligned = await CustomSequencePackService.SavePackAsync("Centered", new[] {
            new CustomSequenceInput { Slot = "1", Frames = new[] { png, small } }
        });
        using (var stream = await (await (await StorageFolder.GetFolderFromPathAsync(aligned.FolderPath)).GetFileAsync("1.png")).OpenReadAsync())
        {
            var decoder = await BitmapDecoder.CreateAsync(stream);
            byte[] pixels = (await decoder.GetPixelDataAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Straight, new BitmapTransform(),
                ExifOrientationMode.IgnoreExifOrientation, ColorManagementMode.DoNotColorManage)).DetachPixelData();
            Check(pixels[4 * 4 + 3] == 0 && pixels[5 * 4 + 2] == 66, "unequal frames centered without resampling or losing transparency");
        }
        // Regression: the picker must recognize the same PNG as an atlas even
        // when only PNG is selected. Its total edge is not its frame edge.
        var atlasSource = await local.CreateFolderAsync("wide-atlas");
        var widePng = await SizedImage(atlasSource, "sheet.png", 8192, 4);
        var wideJson = await atlasSource.CreateFileAsync("sheet.json");
        const string wideMetadata = "{\"frame_width\":256,\"frame_height\":4,\"frames\":32,\"cols\":32,\"rows\":1,\"fps\":25,\"hold_seconds\":0.75}";
        await FileIO.WriteTextAsync(wideJson, wideMetadata);
        var pngProbe = await CustomSequencePackService.ProbeInputAsync("3", new[] { widePng });
        var jsonProbe = await CustomSequencePackService.ProbeInputAsync("3", new[] { wideJson });
        var pairProbe = await CustomSequencePackService.ProbeInputAsync("3", new[] { wideJson, widePng });
        var folderProbe = await CustomSequencePackService.ProbeInputAsync("3", await atlasSource.GetFilesAsync(), atlasSource);
        foreach (var probeInput in new[] { pngProbe, jsonProbe, pairProbe, folderProbe })
            Check(probeInput.Sheet.Path == widePng.Path && probeInput.Metadata.Path == wideJson.Path
                && probeInput.Frames == null, "PNG, JSON, pair and directory all route to native atlas");
        notes.Clear();
        var widePack = await CustomSequencePackService.SavePackAsync("Wide atlas", new[] { pngProbe }, warnings: notes);
        var wideFolder = await StorageFolder.GetFolderFromPathAsync(widePack.FolderPath);
        var wideInfo = await CustomSequencePackService.ReadMetadataAsync(wideFolder, "3", true);
        await (await wideFolder.GetFileAsync("3.json")).CopyAsync(local, "wide-atlas-result.json");
        Check(wideInfo.Width == 256 && wideInfo.Height == 4 && wideInfo.Frames == 32
            && wideInfo.Fps == 25 && wideInfo.HoldSeconds == .75, "atlas retains real cell geometry and timing");
        Check(File.ReadAllBytes(widePng.Path).SequenceEqual(File.ReadAllBytes(Path.Combine(wideFolder.Path, "3.png")))
            && File.ReadAllBytes(wideJson.Path).SequenceEqual(File.ReadAllBytes(Path.Combine(wideFolder.Path, "3.json")))
            && notes.Count == 0, "8192px atlas copied byte-for-byte without resizing warnings");
        using (var stream = await (await wideFolder.GetFileAsync("3.png")).OpenReadAsync())
        {
            var decoder = await BitmapDecoder.CreateAsync(stream);
            var atlasPixels = (await decoder.GetPixelDataAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied,
                new BitmapTransform(), ExifOrientationMode.IgnoreExifOrientation, ColorManagementMode.DoNotColorManage)).DetachPixelData();
            int columns = Math.Min(wideInfo.Frames, 4096 / wideInfo.Width);
            var page = CustomSequenceFormat.RepackPage(atlasPixels, (int)decoder.PixelWidth, wideInfo, 0, wideInfo.Frames,
                columns, out int pageWidth, out int pageHeight);
            Check(pageWidth == 4096 && pageHeight == 8, "runtime repacks the wide atlas into GPU page geometry");
            for (int f = 0; f < wideInfo.Frames; f++)
            for (int y = 0; y < wideInfo.Height; y++)
            {
                int src = (y * 8192 + f * 256) * 4;
                int dst = (((f / columns) * 4 + y) * pageWidth + (f % columns) * 256) * 4;
                Check(atlasPixels.Skip(src).Take(256 * 4).SequenceEqual(page.Skip(dst).Take(256 * 4)),
                    "UWP frame crop keeps per-frame pixels and alpha after atlas repacking");
            }
        }
        var atlasLevelRoot = await local.CreateFolderAsync("atlas-level-root");
        var atlasLevel = await atlasLevelRoot.CreateFolderAsync("kill2");
        await widePng.CopyAsync(atlasLevel);
        await wideJson.CopyAsync(atlasLevel);
        var nestedAtlas = await CustomSequencePackService.ImportFolderAsync(atlasLevelRoot);
        Check((await CustomSequencePackService.ReadMetadataAsync(
            await StorageFolder.GetFolderFromPathAsync(nestedAtlas.FolderPath), "2")).Frames == 32,
            "loose level directory containing an atlas is not flattened");
        await Reject(async () => await CustomSequencePackService.ProbeInputAsync("1", new[] { widePng, small }),
            "mixed atlas and frames rejected instead of flattening");
        var secondAtlas = await widePng.CopyAsync(atlasSource, "second.png");
        await wideJson.CopyAsync(atlasSource, "second.json");
        await Reject(async () => await CustomSequencePackService.ProbeInputAsync("1", await atlasSource.GetFilesAsync(), atlasSource),
            "multiple atlases in a slot require explicit selection or whole-pack import");
        await FileIO.WriteTextAsync(wideJson, "{");
        await Reject(async () => await CustomSequencePackService.ProbeInputAsync("1", new[] { widePng }),
            "damaged companion JSON does not fall back to static frame");
        await FileIO.WriteTextAsync(wideJson, wideMetadata);
        var missingJson = await atlasSource.CreateFileAsync("missing.json");
        await FileIO.WriteTextAsync(missingJson, wideMetadata);
        await Reject(async () => await CustomSequencePackService.ProbeInputAsync("1", new[] { missingJson }),
            "JSON without image gives actionable missing atlas error");

        var rawSource = await local.CreateFolderAsync("large-raw-frames");
        var raw = await SizedImage(rawSource, "frame.png", 5120, 10, 5120);
        var rawProbe = await CustomSequencePackService.ProbeInputAsync("1", new[] { raw });
        Check(rawProbe.Sheet == null && rawProbe.Frames.Count == 1, "PNG without companion JSON is a raw frame");
        notes.Clear();
        var resized = await CustomSequencePackService.SavePackAsync("Resized raw", new[] { rawProbe }, warnings: notes);
        var resizedFolder = await StorageFolder.GetFolderFromPathAsync(resized.FolderPath);
        var resizedInfo = await CustomSequencePackService.ReadMetadataAsync(resizedFolder, "1", true);
        await (await resizedFolder.GetFileAsync("1.json")).CopyAsync(local, "raw-frame-result.json");
        Check(resizedInfo.Width == 1024 && resizedInfo.Height == 2 && resizedInfo.Frames == 1 && resizedInfo.HoldSeconds == 1
            && notes.Any(n => n.Contains("5120×10") && n.Contains("1024×2")), "only oversized raw frames are proportionally normalized and reported");
        using (var stream = await (await resizedFolder.GetFileAsync("1.png")).OpenReadAsync())
        {
            var decoder = await BitmapDecoder.CreateAsync(stream);
            var pixels = (await decoder.GetPixelDataAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Straight, new BitmapTransform(),
                ExifOrientationMode.IgnoreExifOrientation, ColorManagementMode.DoNotColorManage)).DetachPixelData();
            Check(pixels.Where((v, i) => i % 4 == 3).All(v => v == 128), "raw frame resize preserves partial transparency");
        }
        var smallProbe = await CustomSequencePackService.ProbeInputAsync("1", await frames.GetFilesAsync(), frames);
        Check(smallProbe.Frames.Count == 2 && smallProbe.Sheet == null, "ordinary frame directory still uses numeric sequence conversion");
        var tallSource = await local.CreateFolderAsync("tall-atlas");
        var tallPng = await SizedImage(tallSource, "tall.png", 4, 8192);
        await FileIO.WriteTextAsync(await tallSource.CreateFileAsync("tall.json"),
            "{\"frame_width\":4,\"frame_height\":256,\"frames\":32,\"cols\":1,\"rows\":32}");
        var tallPack = await CustomSequencePackService.SavePackAsync("Tall atlas", new[] {
            await CustomSequencePackService.ProbeInputAsync("1", new[] { tallPng })
        });
        var tallFolder = await StorageFolder.GetFolderFromPathAsync(tallPack.FolderPath);
        Check((await CustomSequencePackService.ReadMetadataAsync(tallFolder, "1", true)).Frames == 32
            && File.ReadAllBytes(tallPng.Path).SequenceEqual(File.ReadAllBytes(Path.Combine(tallFolder.Path, "1.png"))),
            "8192px vertical atlas also stays intact");

        var smallerRaw = await SizedImage(rawSource, "frame2.png", 2560, 5, 2560);
        var scaledPair = await CustomSequencePackService.SavePackAsync("Scaled centered", new[] {
            await CustomSequencePackService.ProbeInputAsync("1", await rawSource.GetFilesAsync(), rawSource)
        });
        using (var stream = await (await (await StorageFolder.GetFolderFromPathAsync(scaledPair.FolderPath)).GetFileAsync("1.png")).OpenReadAsync())
        {
            var decoder = await BitmapDecoder.CreateAsync(stream);
            var pixels = (await decoder.GetPixelDataAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Straight, new BitmapTransform(),
                ExifOrientationMode.IgnoreExifOrientation, ColorManagementMode.DoNotColorManage)).DetachPixelData();
            Check(decoder.PixelWidth == 2048 && decoder.PixelHeight == 2 && pixels[1024 * 4 + 3] == 0
                && pixels[(1024 + 256) * 4 + 3] == 128 && pixels[(1024 + 768) * 4 + 3] == 0,
                "mixed-size raw frames share one scale and stay centered with transparent padding");
        }
        Console.WriteLine("PASS: " + checks + " custom-sequence checks using production format/import/export code and Windows image codecs.");
        Console.WriteLine("Roundtrip fixture: " + output.Path);
    }

    [MTAThread]
    private static int Main(string[] args)
    {
        try { Run(args[0]).GetAwaiter().GetResult(); return 0; }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}

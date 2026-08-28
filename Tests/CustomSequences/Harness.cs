using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using KillConfirmGameBar.Services;
using Windows.Storage;
using Windows.Graphics.Imaging;

namespace KillConfirmGameBar.Services
{
    public sealed class IconPackItem { public string Key; public string FolderPath; public string DisplayName; }
    public static class PackCatalogService
    {
        internal static readonly Dictionary<string, IconPackItem> Packs = new Dictionary<string, IconPackItem>();
        public static Task<StorageFolder> GetGameIconPacksFolderAsync(string key)
            => TestApplicationData.Current.LocalFolder.CreateFolderAsync("packs", CreationCollisionOption.OpenIfExists).AsTask();
        internal static Task<IconPackItem> RegisterCustomSequencePackAsync(StorageFolder folder, string name)
        {
            var pack = new IconPackItem { Key = Guid.NewGuid().ToString("N"), FolderPath = folder.Path, DisplayName = name };
            Packs.Add(pack.Key, pack);
            return Task.FromResult(pack);
        }
        public static Task<StorageFolder> GetImportedIconFolderAsync(string key)
            => StorageFolder.GetFolderFromPathAsync(Packs[key].FolderPath).AsTask();
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
            Check(pixels[2] == 100 && pixels[4 * 4 + 2] == 200, "legacy ordinal frame order");
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
        int before = PackCatalogService.Packs.Count;
        await FileIO.WriteTextAsync(await source.GetFileAsync("1.json"), "{\"frame_width\":9999,\"frame_height\":1,\"frames\":2,\"cols\":2}");
        await Reject(async () => await CustomSequencePackService.ImportFolderAsync(source), "invalid grid rejected");
        Check(before == PackCatalogService.Packs.Count, "failed import not registered");
        Check((await TestApplicationData.Current.TemporaryFolder.GetFoldersAsync()).Count == 0, "ZIP staging cleaned");
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

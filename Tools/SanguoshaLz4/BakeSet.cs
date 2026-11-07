using System.Diagnostics;
using System.IO;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using KillConfirmCompatibility.Contracts;
using KillConfirmCompatibility.Controls;
using KillConfirmCompatibility.Desktop.Runtime;
using Microsoft.Graphics.Canvas;

internal static partial class Program
{
    private sealed class BakeSetSpec
    {
        public string SourceAssetRoot { get; set; }
        public string[] Clips { get; set; } = Array.Empty<string>();
        public BakeVariant[] Variants { get; set; } = Array.Empty<BakeVariant>();
        public string ShakeMode { get; set; } = "production-seeded";
        public int Seed { get; set; } = ShakeSeed;
    }

    private sealed class BakeVariant
    {
        public string ProfileId { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        // Width/Height are expected physical pixels, not target DIPs. Keeping
        // the actual host DPI avoids assuming scaled 96-DPI pixels are identical.
        public float TargetDpi { get; set; } = 96;
        public float? WidthDips { get; set; }
        public float? HeightDips { get; set; }
        public float TargetWidthDips => WidthDips ?? Width;
        public float TargetHeightDips => HeightDips ?? Height;
        // desktop-default preserves the production matrix multiplication, including float rounding.
        // explicit accepts the actual host's design-to-target matrix in DIPs.
        public string TransformKind { get; set; } = "explicit";
        public float[] Matrix { get; set; }
    }

    private static Matrix3x2 BakeMatrix(BakeVariant variant)
    {
        if (variant.TransformKind == "desktop-default")
        {
            const double resolutionScale = 0.6875;
            double fit = Math.Min(variant.Width / 1600d, variant.Height / 900d);
            return Matrix3x2.CreateScale((float)resolutionScale)
                * (Matrix3x2.CreateScale((float)(fit / resolutionScale))
                    * Matrix3x2.CreateTranslation((float)((variant.Width - 1600 * fit) / 2),
                        (float)((variant.Height - 900 * fit) / 2)));
        }
        if (variant.TransformKind != "explicit" || variant.Matrix == null || variant.Matrix.Length != 6
            || variant.Matrix.Any(x => !float.IsFinite(x)))
            throw new InvalidDataException("An explicit finite six-component transform is required.");
        return new Matrix3x2(variant.Matrix[0], variant.Matrix[1], variant.Matrix[2],
            variant.Matrix[3], variant.Matrix[4], variant.Matrix[5]);
    }

    private static string FileHash(string path)
    {
        using var input = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(input));
    }

    private static string SourcePath(string root, string relative)
    {
        string path = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
        if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Source path must remain inside the read-only source asset root.");
        return path;
    }

    private static void CheckIdentifier(string name)
    {
        if (string.IsNullOrEmpty(name) || name.Any(x => !(char.IsAsciiLetterOrDigit(x) || x == '-' || x == '_')))
            throw new InvalidDataException("Clip/profile identifiers must be simple file names.");
    }

    private static async Task BakeSet(string configurationPath, bool validateOnly)
    {
        var spec = JsonSerializer.Deserialize<BakeSetSpec>(File.ReadAllText(configurationPath),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidDataException("Missing bake specification.");
        if (string.IsNullOrWhiteSpace(spec.SourceAssetRoot)) throw new InvalidDataException("SourceAssetRoot is required.");
        string assetRoot = Path.GetFullPath(spec.SourceAssetRoot, Path.GetDirectoryName(configurationPath))
            .TrimEnd(Path.DirectorySeparatorChar);
        if (!Directory.Exists(assetRoot)) throw new DirectoryNotFoundException(assetRoot);
        if (Output.Equals(assetRoot, StringComparison.OrdinalIgnoreCase)
            || Output.StartsWith(assetRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Bake outputs must be outside the source asset directory.");
        if (spec.Clips == null || spec.Variants == null || spec.Clips.Length == 0 || spec.Variants.Length == 0
            || spec.Variants.Any(x => x == null)
            || spec.Clips.Distinct(StringComparer.OrdinalIgnoreCase).Count() != spec.Clips.Length
            || spec.Variants.Select(x => x.ProfileId).Distinct(StringComparer.OrdinalIgnoreCase).Count() != spec.Variants.Length)
            throw new InvalidDataException("Specify a unique, nonempty clip and profile list.");
        if (spec.ShakeMode != "production-seeded" && spec.ShakeMode != "none")
            throw new InvalidDataException("ShakeMode must be production-seeded or none.");
        foreach (string tier in spec.Clips)
        {
            CheckIdentifier(tier);
            string clipPath = SourcePath(assetRoot, tier + "/" + tier + ".sgsd.gz");
            if (!File.Exists(clipPath)) throw new FileNotFoundException("Source clip missing.", clipPath);
        }
        foreach (var variant in spec.Variants)
        {
            CheckIdentifier(variant.ProfileId);
            if (variant.Width < 1 || variant.Height < 1 || variant.Width > 8192 || variant.Height > 8192
                || (long)variant.Width * variant.Height > 16_777_216)
                throw new InvalidDataException("Invalid output dimensions.");
            if (!float.IsFinite(variant.TargetDpi) || variant.TargetDpi < 48 || variant.TargetDpi > 960
                || !float.IsFinite(variant.TargetWidthDips) || !float.IsFinite(variant.TargetHeightDips)
                || variant.TargetWidthDips <= 0 || variant.TargetHeightDips <= 0)
                throw new InvalidDataException("Invalid target DPI or DIP dimensions.");
            if (variant.TargetDpi != 96 && (!variant.WidthDips.HasValue || !variant.HeightDips.HasValue))
                throw new InvalidDataException("Non-96-DPI targets require explicit WidthDips and HeightDips.");
            if (variant.TransformKind == "desktop-default" && (variant.TargetDpi != 96
                || variant.TargetWidthDips != variant.Width || variant.TargetHeightDips != variant.Height))
                throw new InvalidDataException("desktop-default models the production 96-DPI physical render target.");
            _ = BakeMatrix(variant);
            foreach (string tier in spec.Clips)
                if (File.Exists(Path.Combine(Output, "lz4", variant.ProfileId, tier + ".sgslz4"))
                    || File.Exists(Path.Combine(Output, "lz4", variant.ProfileId, tier + ".sgslz4.partial")))
                    throw new IOException("Refusing to overwrite an existing verified export; use a new output directory.");
        }
        if (validateOnly)
        {
            Console.WriteLine(JsonSerializer.Serialize(new { Valid = true, SourceAssetRoot = assetRoot,
                Clips = spec.Clips.Length, Profiles = spec.Variants.Length, Output,
                Scope = "Configuration, input clip existence and output collision checks; no GPU/device/image decode." }, Json));
            return;
        }
        Directory.CreateDirectory(Output);
        Save("bake-set-request", spec);
        var exports = new List<object>();
        var totalClock = Stopwatch.StartNew();
        DesktopWin2D.Initialize();
        var device = CanvasDevice.GetSharedDevice();
        foreach (string tier in spec.Clips)
        {
            string clipPath = SourcePath(assetRoot, tier + "/" + tier + ".sgsd.gz");
            SanguoshaClip clip;
            using (var input = File.OpenRead(clipPath)) clip = SanguoshaClip.Read(input);
            if (clip.DesignWidth != Width || clip.DesignHeight != Height || clip.Fps != Fps
                || clip.Frames.Length < 2 || clip.HasScreen)
                throw new InvalidDataException("Unsupported built-in clip geometry or blend mode: " + tier);
            var pagePaths = clip.Pages.Select(x => SourcePath(assetRoot,
                x.File.Contains('/') ? x.File : tier + "/" + x.File)).ToArray();
            var sources = new[] { clipPath }.Concat(pagePaths).Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(path => new { Path = Path.GetRelativePath(assetRoot, path).Replace('\\', '/'),
                    Bytes = new FileInfo(path).Length, Sha256 = FileHash(path) }).ToArray();
            var pages = new CanvasBitmap[clip.Pages.Length];
            try
            {
                for (int p = 0; p < pages.Length; p++) pages[p] = await LoadPage(device, pagePaths[p]);
                foreach (var variant in spec.Variants)
                {
                    Save("progress", new { State = "baking", Tier = tier, variant.ProfileId, Finished = exports.Count,
                        Expected = spec.Clips.Length * spec.Variants.Length });
                    exports.Add(BakeVariantFrames(device, clip, pages, tier, variant, spec, sources));
                    Save("bake-set-index", new { Complete = false, Exports = exports });
                }
            }
            finally { foreach (var page in pages) page?.Dispose(); }
        }
        Save("bake-set-index", new { Complete = true, TotalSeconds = totalClock.Elapsed.TotalSeconds, Exports = exports });
        Save("progress", new { State = "complete", Finished = exports.Count, Expected = spec.Clips.Length * spec.Variants.Length });
    }

    private static object BakeVariantFrames(CanvasDevice device, SanguoshaClip clip, CanvasBitmap[] pages,
        string tier, BakeVariant variant, BakeSetSpec spec, object sources)
    {
        int frameBytes = checked(variant.Width * variant.Height * 4), count = clip.Frames.Length - 1;
        string folder = Path.Combine(Output, "lz4", variant.ProfileId);
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, tier + ".sgslz4"), partial = path + ".partial";
        Matrix3x2 transform = BakeMatrix(variant);
        using var renderer = new SanguoshaFrameRenderer();
        using var target = new CanvasRenderTarget(device, variant.TargetWidthDips, variant.TargetHeightDips, variant.TargetDpi);
        if (target.SizeInPixels.Width != variant.Width || target.SizeInPixels.Height != variant.Height)
            throw new InvalidDataException("Actual target physical dimensions differ from the declared container dimensions.");
        var compressed = new byte[SanguoshaLz4Codec.MaximumCompressedLength(frameBytes)];
        var decoded = new byte[frameBytes];
        var shake = new SanguoshaShake(clip, spec.Seed);
        var frameHashes = new List<string>(count);
        var blockSizes = new List<int>(count);
        var samples = new List<object>(count);
        var renderMs = new List<double>(count);
        var compressionMs = new List<double>(count);
        var clock = Stopwatch.StartNew();
        using (var file = new FileStream(partial, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        using (var writer = new BinaryWriter(file, Encoding.UTF8, true))
        {
            writer.Write(Lz4Magic); writer.Write(1); writer.Write(variant.Width); writer.Write(variant.Height);
            writer.Write(variant.Width * 4); writer.Write(1); writer.Write(Fps); writer.Write(1);
            writer.Write(count); writer.Write(frameBytes);
            for (int frame = 0; frame < count; frame++)
            {
                long begin = Stopwatch.GetTimestamp();
                double elapsedMs = frame * 1000d / Fps;
                var elapsed = TimeSpan.FromTicks((long)(elapsedMs * TimeSpan.TicksPerMillisecond));
                float shakeX = 0, shakeY = 0;
                if (spec.ShakeMode == "production-seeded") shake.Offset(elapsedMs, out shakeX, out shakeY);
                using (var drawing = target.CreateDrawingSession())
                {
                    drawing.Clear(Colors.Transparent);
                    renderer.RenderDirectAtTime(drawing, clip, pages, elapsed,
                        Matrix3x2.CreateTranslation(shakeX, shakeY) * transform);
                }
                byte[] reference = target.GetPixelBytes();
                if (reference.Length != frameBytes) throw new InvalidDataException("Unexpected reference byte count.");
                string hash = Hash(reference); frameHashes.Add(hash);
                renderMs.Add(Stopwatch.GetElapsedTime(begin).TotalMilliseconds);
                begin = Stopwatch.GetTimestamp();
                int size = SanguoshaLz4Codec.Compress(reference, compressed);
                compressionMs.Add(Stopwatch.GetElapsedTime(begin).TotalMilliseconds);
                SanguoshaLz4Codec.Decompress(compressed, size, decoded);
                if (!reference.AsSpan().SequenceEqual(decoded))
                    throw new InvalidDataException("Compressed block differs from production renderer frame " + frame);
                writer.Write(size); writer.Write(compressed, 0, size); blockSizes.Add(size);
                if (file.Position > 2L * 1024 * 1024 * 1024)
                    throw new InvalidDataException("Export exceeds the production broker's 2 GiB per-file limit.");
                samples.Add(new { Frame = frame, TimeTicks = elapsed.Ticks, ShakeX = shakeX, ShakeY = shakeY });
            }
        }
        // Reopen with the same indexed reader used by both production hosts.
        using (var source = SanguoshaLz4Source.Open(File.OpenRead(partial)))
        {
            if (source.Width != variant.Width || source.Height != variant.Height || source.Fps != Fps
                || source.FrameCount != count || source.FrameBytes != frameBytes)
                throw new InvalidDataException("Persisted header mismatch.");
            for (int frame = 0; frame < count; frame++)
            {
                source.ReadFrame(frame, decoded);
                if (Hash(decoded) != frameHashes[frame])
                    throw new InvalidDataException("Persisted frame differs from original renderer: " + frame);
            }
        }
        File.Move(partial, path);
        var result = new
        {
            Tier = tier, variant.ProfileId, variant.Width, variant.Height, Dpi = variant.TargetDpi,
            WidthDips = variant.TargetWidthDips, HeightDips = variant.TargetHeightDips,
            variant.TransformKind, Matrix = new[] { transform.M11, transform.M12, transform.M21, transform.M22, transform.M31, transform.M32 },
            Fps, Frames = count, SourceEntries = clip.Frames.Length, DurationSeconds = count / (double)Fps,
            PixelFormat = "BGRA8 premultiplied", Format = "KCLZ4F01", Codec = "Production SanguoshaLz4Codec; vendored Native/Lz4",
            spec.ShakeMode, ShakeSeed = spec.Seed, Events = clip.Events.Select(x => new { x.Name, x.Time }),
            BrightnessBoost = 0, ContrastBoost = 0, AppearanceBrightness = 1, AppearanceContrast = 1,
            Opacity = "host applies external alpha; not baked", SourceFiles = sources,
            File = Path.GetRelativePath(Output, path).Replace('\\', '/'), FileBytes = new FileInfo(path).Length,
            FileSha256 = FileHash(path), RawBytes = (long)frameBytes * count, FrameHashes = frameHashes,
            BlockSizes = blockSizes, Samples = samples, RenderReadbackMs = Stats(renderMs), CompressionMs = Stats(compressionMs),
            TotalSeconds = clock.Elapsed.TotalSeconds,
            Verification = new { Passed = true, ComparedFrames = count, ByteMismatchFrames = 0, PersistedHashMismatchFrames = 0,
                Scope = "Linked production SanguoshaFrameRenderer at the declared target/matrix/time/seed -> original BGRA -> independent LZ4 block -> byte equality -> reopened persisted output SHA-256. Does not certify a different host transform, colour profile or off-grid sample time." }
        };
        File.WriteAllText(Path.Combine(folder, tier + ".metadata.json"), JsonSerializer.Serialize(result, Json));
        return new { Tier = tier, variant.ProfileId, variant.Width, variant.Height, Frames = count,
            result.File, result.FileBytes, result.FileSha256, result.RawBytes, result.TotalSeconds, Verified = true };
    }
}

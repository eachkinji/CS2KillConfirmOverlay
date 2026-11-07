using KillConfirmCompatibility.Contracts;
using KillConfirmCompatibility.Controls;
using Microsoft.Graphics.Canvas;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage;

namespace KillConfirmCompatibility.Validation;

// Replays every retained 十周年连破播报 SGSD clip through the production Win2D renderer and, when a
// reference folder is supplied, compares the result with frames rendered by Sanguosha's own
// LayaAir/Spine runtime (premultiplied RGBA PNGs from tools/bake_server.py --page bake.html).
internal static class SanguoshaValidation
{
    private static readonly string[] Tiers =
    {
        "shoupo", "lianpo", "sanpo", "sipo", "wupo", "liupo", "qipo",
        "shenweizhengqiankun", "wanfumodi", "shenyimiaoshou", "qingnangjishi"
    };

    internal static async Task RunAsync(string output, string assets, string reference = null)
    {
        Directory.CreateDirectory(output);
        File.Delete(Path.Combine(output, "pass.txt"));
        File.Delete(Path.Combine(output, "failure.txt"));
        var load = typeof(KillConfirmAnimation).GetMethod("LoadBitmapFromStorageFileAsync", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new MissingMethodException("LoadBitmapFromStorageFileAsync");
        var report = new StringBuilder();
        ValidateSampling(report);
        double worstMean = 0, worstP99 = 0;
        int compared = 0;
        using var renderer = new SanguoshaFrameRenderer();
        foreach (string tier in Tiers)
        {
            string folder = Path.Combine(assets, tier);
            SanguoshaClip clip;
            using (var stream = File.OpenRead(Path.Combine(folder, tier + ".sgsd.gz"))) clip = SanguoshaClip.Read(stream);
            if (clip.Fps != 60 || clip.DesignWidth != 1600 || clip.DesignHeight != 900)
                throw new InvalidDataException(tier + ": unexpected clip header");
            var pages = new CanvasBitmap[clip.Pages.Length];
            try
            {
                for (int i = 0; i < pages.Length; i++)
                {
                    string pagePath = clip.Pages[i].File.IndexOf('/') >= 0
                        ? Path.Combine(assets, clip.Pages[i].File.Replace('/', Path.DirectorySeparatorChar))
                        : Path.Combine(folder, clip.Pages[i].File);
                    var file = await StorageFile.GetFileFromPathAsync(Path.GetFullPath(pagePath));
                    pages[i] = await (Task<CanvasBitmap>)load.Invoke(null, new object[] { file });
                }

                int visibleFrames = 0;
                var frameTimes = new List<double>();
                using var liveTarget = new CanvasRenderTarget(CanvasDevice.GetSharedDevice(), 1600, 900, 96);
                for (int frame = 0; frame < clip.Frames.Length; frame++)
                {
                    bool sampled = reference != null ? frame % 10 == 0 : frame % 30 == 0;
                    var clock = System.Diagnostics.Stopwatch.StartNew();
                    CanvasRenderTarget target;
                    if (!clip.HasScreen)
                    {
                        var device = CanvasDevice.GetSharedDevice();
                        using var commands = new CanvasCommandList(device);
                        using (var recording = commands.CreateDrawingSession())
                            renderer.RenderDirect(recording, clip, pages, frame, System.Numerics.Matrix3x2.Identity);
                        var bounds = commands.GetBounds(device);
                        if (double.IsNaN(bounds.Width) || double.IsNaN(bounds.Height))
                            throw new InvalidDataException(tier + " frame " + frame + ": invalid RenderDirect command bounds");
                        using (var session = liveTarget.CreateDrawingSession())
                        {
                            session.Clear(Colors.Transparent);
                            session.DrawImage(commands);
                        }
                        target = liveTarget;
                    }
                    else target = renderer.Render(CanvasDevice.GetSharedDevice(), clip, pages, frame, 1f);
                    byte[] actual = target.GetPixelBytes(); // BGRA premultiplied
                    frameTimes.Add(clock.Elapsed.TotalMilliseconds);
                    if (actual.Where((b, i) => i % 4 == 3 && b > 0).Any()) visibleFrames++;
                    if (frame == clip.Frames.Length / 2)
                    {
                        using var png = File.Create(Path.Combine(output, tier + "-mid.png"));
                        await target.SaveAsync(png.AsRandomAccessStream(), CanvasBitmapFileFormat.Png);
                        await SaveOpaquePreviewAsync(target, Path.Combine(output, tier + "-mid-on-black.png"));
                        CanvasRenderTarget large = renderer.RenderAtTime(CanvasDevice.GetSharedDevice(), clip, pages,
                            TimeSpan.FromSeconds((frame + 0.5) / clip.Fps), 2f);
                        using var largePng = File.Create(Path.Combine(output, tier + "-mid-2x.png"));
                        await large.SaveAsync(largePng.AsRandomAccessStream(), CanvasBitmapFileFormat.Png);
                        await SaveOpaquePreviewAsync(large, Path.Combine(output, tier + "-mid-2x-on-black.png"));
                    }
                    if (!sampled) continue;
                    if (reference == null) continue;
                    string refPath = Path.Combine(reference, tier, frame.ToString("0000", CultureInfo.InvariantCulture) + ".png");
                    if (!File.Exists(refPath)) continue;
                    byte[] expected = await ReadRawRgbaAsync(refPath, (int)target.SizeInPixels.Width, (int)target.SizeInPixels.Height);
                    var diffs = new int[256];
                    long sum = 0;
                    for (int i = 0; i < actual.Length; i += 4)
                    {
                        // actual BGRA vs expected RGBA
                        int db = Math.Abs(actual[i] - expected[i + 2]);
                        int dg = Math.Abs(actual[i + 1] - expected[i + 1]);
                        int dr = Math.Abs(actual[i + 2] - expected[i]);
                        int da = Math.Abs(actual[i + 3] - expected[i + 3]);
                        sum += db + dg + dr + da;
                        diffs[db]++; diffs[dg]++; diffs[dr]++; diffs[da]++;
                    }
                    double mean = sum / (double)actual.Length;
                    long count = actual.Length, acc = 0; int p99 = 0;
                    for (int v = 0; v < 256; v++) { acc += diffs[v]; if (acc >= count * 0.99) { p99 = v; break; } }
                    worstMean = Math.Max(worstMean, mean);
                    worstP99 = Math.Max(worstP99, p99);
                    compared++;
                    report.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0} frame {1}: mean {2:0.000}/255 p99 {3}", tier, frame, mean, p99));
                }
                if (visibleFrames < 3) throw new InvalidDataException(tier + ": animation rendered empty");
                frameTimes.Sort();
                report.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "{0}: frames={1} pages={2} visible-frames={3} render+readback-p50={4:0.00}ms p95={5:0.00}ms",
                    tier, clip.Frames.Length, pages.Length, visibleFrames,
                    frameTimes[frameTimes.Count / 2], frameTimes[(int)((frameTimes.Count - 1) * .95)]));
            }
            finally
            {
                foreach (var page in pages) page?.Dispose();
            }
        }

        report.AppendLine(string.Format(CultureInfo.InvariantCulture, "compared={0} worst-mean={1:0.000} worst-p99={2}", compared, worstMean, worstP99));
        File.WriteAllText(Path.Combine(output, "report.txt"), report.ToString());
        if (reference != null && (compared < 50 || worstMean > 2.0 || worstP99 > 24))
            throw new Exception("Sanguosha replay differs from the original Laya/Spine render.\n" + report);
        File.WriteAllText(Path.Combine(output, "pass.txt"), "ok");
    }

    private static async Task SaveOpaquePreviewAsync(CanvasRenderTarget source, string path)
    {
        // PNG export unpremultiplies low-alpha glow; an image viewer that ignores alpha
        // shows saturated bands that are invisible in the HUD. Composite the actual GPU
        // surface first, retaining additive RGB values, so visual QA sees displayed pixels.
        using var preview = new CanvasRenderTarget(source.Device, source.SizeInPixels.Width, source.SizeInPixels.Height, 96);
        using (var session = preview.CreateDrawingSession())
        {
            session.Clear(Colors.Black);
            session.DrawImage(source);
        }
        using var png = File.Create(path);
        await preview.SaveAsync(png.AsRandomAccessStream(), CanvasBitmapFileFormat.Png);
    }

    private static void ValidateSampling(StringBuilder report)
    {
        // A steady motion sampled by a slightly jittery 60 Hz presentation must not
        // repeat pose 0 then jump to pose 2. Source frame boundaries remain exact.
        var clip = new SanguoshaClip { Fps = 60, Frames = Enumerable.Range(0, 8)
            .Select(i => new[] { new SanguoshaDraw { Region = 0, Rgba = 0xffffffff,
                X0 = i * 10, X1 = i * 10 + 10, X2 = i * 10 + 10, X3 = i * 10,
                Y2 = 10, Y3 = 10 } }).ToArray() };
        SanguoshaDraw[] buffer = null;
        foreach (double ms in new[] { 0, 16.5, 33.5, 49.8, 66.9, 83.1, 100.2 })
        {
            var frame = clip.SampleFrame(TimeSpan.FromMilliseconds(ms), ref buffer, out int count);
            if (count != 1 || Math.Abs(frame[0].X0 - ms * .6) > .001)
                throw new InvalidDataException("Jittered Sanguosha presentation repeated or skipped a pose.");
        }
        SanguoshaDraw[] reused = buffer;
        clip.SampleFrame(TimeSpan.FromMilliseconds(25), ref buffer, out _);
        if (!ReferenceEquals(reused, buffer)) throw new InvalidDataException("Sampling allocated a new steady-state buffer.");
        clip.Frames[2][0].Region = 1;
        var switched = clip.SampleFrame(TimeSpan.FromMilliseconds(25), ref buffer, out _);
        if (!ReferenceEquals(switched, clip.Frames[1])) throw new InvalidDataException("Attachment switch was interpolated.");
        clip.Frames[2][0].Region = 0;
        clip.Frames[2][0].Blend = SanguoshaBlend.Light;
        switched = clip.SampleFrame(TimeSpan.FromMilliseconds(25), ref buffer, out _);
        if (!ReferenceEquals(switched, clip.Frames[1])) throw new InvalidDataException("Blend switch was interpolated.");
        report.AppendLine("sampling: jittered 60Hz motion continuous; attachment/blend switches discrete; buffer reused");
    }

    private static async Task<byte[]> ReadRawRgbaAsync(string path, int width, int height)
    {
        var file = await StorageFile.GetFileFromPathAsync(Path.GetFullPath(path));
        using var stream = await file.OpenReadAsync();
        var decoder = await BitmapDecoder.CreateAsync(stream);
        if (decoder.PixelWidth != width || decoder.PixelHeight != height)
            throw new InvalidDataException("Reference frame size mismatch: " + path);
        // Straight + no colour management returns the stored bytes untouched; the baker
        // stored Laya's premultiplied framebuffer values directly in these channels.
        var pixels = await decoder.GetPixelDataAsync(BitmapPixelFormat.Rgba8, BitmapAlphaMode.Straight,
            new BitmapTransform(), ExifOrientationMode.IgnoreExifOrientation, ColorManagementMode.DoNotColorManage);
        return pixels.DetachPixelData();
    }
}

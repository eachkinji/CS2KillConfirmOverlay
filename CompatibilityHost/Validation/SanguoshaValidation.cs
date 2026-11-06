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

// Replays every 首破..七破 & 官阶破敌神兵 SGSD clip through the production Win2D renderer and, when a
// reference folder is supplied, compares the result with frames rendered by Sanguosha's own
// LayaAir/Spine runtime (premultiplied RGBA PNGs from tools/bake_server.py --page bake.html).
internal static class SanguoshaValidation
{
    private static readonly string[] Tiers =
    {
        "shoupo", "lianpo", "sanpo", "sipo", "wupo", "liupo", "qipo",
        "shenweizhengqiankun", "wanfumodi", "shenyimiaoshou", "qingnangjishi",
        "podi", "guanjie2", "guanjie3", "guanjie4", "guanjie5", "guanjie6", "guanjie7",
        "dashatesha", "shoujidonghua_chuizi", "shoujidonghua_jian", "shoujidonghua_fuzi2"
    };

    internal static async Task RunAsync(string output, string assets, string reference = null)
    {
        Directory.CreateDirectory(output);
        File.Delete(Path.Combine(output, "pass.txt"));
        File.Delete(Path.Combine(output, "failure.txt"));
        var load = typeof(KillConfirmAnimation).GetMethod("LoadBitmapFromStorageFileAsync", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new MissingMethodException("LoadBitmapFromStorageFileAsync");
        var report = new StringBuilder();
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
                for (int frame = 0; frame < clip.Frames.Length; frame++)
                {
                    bool sampled = reference != null ? frame % 10 == 0 : frame % 30 == 0;
                    bool checkVisible = frame % 5 == 0;
                    if (!sampled && !checkVisible) continue;
                    CanvasRenderTarget target = renderer.Render(CanvasDevice.GetSharedDevice(), clip, pages, frame, 1f);
                    byte[] actual = target.GetPixelBytes(); // BGRA premultiplied
                    if (actual.Where((b, i) => i % 4 == 3 && b > 0).Any()) visibleFrames++;
                    if (!sampled) continue;
                    if (frame == clip.Frames.Length / 2)
                    {
                        using var png = File.Create(Path.Combine(output, tier + "-mid.png"));
                        await target.SaveAsync(png.AsRandomAccessStream(), CanvasBitmapFileFormat.Png);
                    }
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
                report.AppendLine(tier + ": frames=" + clip.Frames.Length + " pages=" + pages.Length + " visible-samples=" + visibleFrames);
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

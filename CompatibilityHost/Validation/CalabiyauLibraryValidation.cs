using KillConfirmCompatibility.Contracts;
using KillConfirmCompatibility.Controls;
using Microsoft.Graphics.Canvas;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace KillConfirmCompatibility.Validation;

// Production decoder and deferred Win2D draw path, across every exported clip.
internal static class CalabiyauLibraryValidation
{
    internal static async Task RunAsync(string output, string source, string variantList = null)
    {
        if (Environment.GetEnvironmentVariable("KILLCONFIRM_CALABIYAU_SAMPLING_VALIDATION") == "1")
        {
            await CalabiyauSamplingValidation.RunAsync(output, source);
            return;
        }
        Directory.CreateDirectory(Path.Combine(output, "variants"));
        File.Delete(Path.Combine(output, "pass.txt"));
        File.Delete(Path.Combine(output, "failure.txt"));
        var flags = BindingFlags.NonPublic | BindingFlags.Static;
        var textures = (Dictionary<string, CanvasBitmap>)typeof(KillConfirmAnimation).GetField("CalabiyauTextures", flags).GetValue(null);
        var load = typeof(KillConfirmAnimation).GetMethod("LoadBitmapFromStorageFileAsync", flags);
        var draw = typeof(KillConfirmAnimation).GetMethod("DrawCalabiyauClipEventAt", flags);
        var selectedVariants = variantList == null ? null : variantList.Split(',').ToHashSet(StringComparer.Ordinal);
        if (selectedVariants != null && (selectedVariants.Count == 0 || selectedVariants.Any(v => v.Length != 8 || v.Any(c => c < '0' || c > '9'))))
            throw new ArgumentException("Expected comma-separated eight-digit variant IDs.");
        int expectedVariants = selectedVariants?.Count ?? 161;
        int variants = 0, clips = 0;
        try
        {
            // Exercise the zero-scale intro regression before the long sweep.
            foreach (string folder in Directory.EnumerateDirectories(Path.Combine(source, "iconpacks"))
                .Where(p => selectedVariants == null || selectedVariants.Contains(Path.GetFileName(p)))
                .OrderBy(p => Path.GetFileName(p) == "15401201" ? 0 : 1).ThenBy(p => p))
            {
                var paths = Directory.GetFiles(folder, "lower-*.json.gz").OrderBy(p => p).ToArray();
                if (paths.Length == 0) continue;
                if (paths.Length != 6) throw new Exception("Missing kill-count clip: " + folder);
                var animations = paths.Select(p => { using var stream = File.OpenRead(p); return CalabiyauClip.Read(stream); }).ToArray();
                if (animations[4].OverkillClip != "lower-6.json.gz" || animations.Take(5).Any(c => c.Textures.Contains("event-count.png"))
                    || !animations[5].Textures.Contains("event-count.png"))
                    throw new Exception("Native/compatibility counter separation is incorrect: " + folder);
                var names = animations.SelectMany(c => c.Textures).Concat(new[] { "event-icon-normal.png", "event-label-normal.png", "event-count-8.png" }).Distinct();
                foreach (string name in names)
                {
                    var file = await Windows.Storage.StorageFile.GetFileFromPathAsync(Path.GetFullPath(Path.Combine(source, "textures", name)));
                    textures.Add(name, await (Task<CanvasBitmap>)load.Invoke(null, new object[] { file }));
                }
                for (int index = 0; index < animations.Length; index++)
                {
                    var clip = animations[index]; int visible = 0;
                    foreach (double sample in selectedVariants == null ? new[] { .3, 1.7 } : new[] { .3, .8, 1.1, 1.7, 2.2 })
                    {
                        File.WriteAllText(Path.Combine(output, "current.txt"), Path.GetFileName(folder) + "/" + (index + 1) + " @ " + sample);
                        using var target = new CanvasRenderTarget(CanvasDevice.GetSharedDevice(), clip.Width, clip.Height, 96);
                        using var commands = new CanvasCommandList(CanvasDevice.GetSharedDevice());
                        using (var recording = commands.CreateDrawingSession())
                            draw.Invoke(null, new object[] { recording, clip, Math.Min(sample, clip.Duration - .05), "normal", 8 });
                        using (var session = target.CreateDrawingSession()) { session.Clear(Colors.Transparent); session.DrawImage(commands); }
                        byte[] pixels = target.GetPixelBytes();
                        for (int i = 3; i < pixels.Length; i += 4) if (pixels[i] > 0) visible++;
                        if (index == 4 && sample == 1.7)
                        {
                            using var stream = File.Create(Path.Combine(output, "variants", Path.GetFileName(folder) + ".png"));
                            await target.SaveAsync(stream.AsRandomAccessStream(), CanvasBitmapFileFormat.Png);
                        }
                        if (selectedVariants != null)
                        {
                            string name = Path.GetFileName(folder) + "-count-" + (index + 1) + "-" + sample.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + ".png";
                            using var stream = File.Create(Path.Combine(output, name));
                            await target.SaveAsync(stream.AsRandomAccessStream(), CanvasBitmapFileFormat.Png);
                        }
                    }
                    if (visible == 0) throw new Exception("Empty native animation: " + paths[index]);
                    clips++;
                }
                foreach (var bitmap in textures.Values) bitmap.Dispose();
                textures.Clear(); variants++;
                File.WriteAllText(Path.Combine(output, "progress.txt"), variants + " variants, " + clips + " clips");
            }
            if (variants != expectedVariants || clips != expectedVariants * 6) throw new Exception("Incomplete Calabiyau library.");
            File.WriteAllText(Path.Combine(output, "pass.txt"), $"PASS: {variants} variants / {clips} clips decoded and rendered through deferred production Win2D commands; non-empty intro and stable samples; {variants} native thumbnails.");
        }
        finally { foreach (var bitmap in textures.Values) bitmap.Dispose(); textures.Clear(); }
    }
}

using KillConfirmCompatibility.Contracts;
using KillConfirmCompatibility.Controls;
using Microsoft.Graphics.Canvas;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Threading.Tasks;

namespace KillConfirmCompatibility.Validation;

internal static class CalabiyauSamplingValidation
{
    internal static async Task RunAsync(string output, string source)
    {
        Directory.CreateDirectory(output);
        var flags = BindingFlags.NonPublic | BindingFlags.Static;
        var textures = (Dictionary<string, CanvasBitmap>)typeof(KillConfirmAnimation).GetField("CalabiyauTextures", flags).GetValue(null);
        var load = typeof(KillConfirmAnimation).GetMethod("LoadBitmapFromStorageFileAsync", flags);
        var draw = typeof(KillConfirmAnimation).GetMethod("DrawCalabiyauClipEventAtSampling", flags);
        var report = new List<string>();
        var device = CanvasDevice.GetSharedDevice();
        try
        {
            foreach (string variant in new[] { "10102206", "10502201" })
            {
                using var input = File.OpenRead(Path.Combine(source, "iconpacks", variant, "lower-5.json.gz"));
                var clip = CalabiyauClip.Read(input);
                var names = clip.Textures.Concat(new[] { "normal", "headshot", "knife", "grenade" }
                    .SelectMany(e => new[] { "event-icon-" + e + ".png", "event-label-" + e + ".png" })).Append("event-count-8.png").Distinct();
                foreach (string name in names)
                {
                    if (textures.ContainsKey(name)) continue;
                    var file = await Windows.Storage.StorageFile.GetFileFromPathAsync(Path.GetFullPath(Path.Combine(source, "textures", name)));
                    textures.Add(name, await (Task<CanvasBitmap>)load.Invoke(null, new object[] { file }));
                }
                if (variant == "10102206")
                {
                    int masks = 0;
                    foreach (double time in new[] { .8, 1.1 })
                    {
                        var command = clip.Frames[(int)(time * clip.Fps)].Single(c =>
                            clip.Textures[(int)c[0]] == "event-icon.png" && c.Length == 23);
                        var isolated = new CalabiyauClip { Version = 2, Fps = 60, Duration = 1d / 60, Width = clip.Width,
                            Height = clip.Height, Textures = clip.Textures, Frames = new[] { new[] { command } } };
                        foreach (string kind in new[] { "normal", "headshot", "knife", "grenade" })
                        foreach (float scale in new[] { 1f, 1.5f, 2f })
                        {
                            using var target = new CanvasRenderTarget(device, (int)(clip.Width * scale + 32), (int)(clip.Height * scale + 32), 96);
                            using var list = new CanvasCommandList(device);
                            using (var ds = list.CreateDrawingSession())
                            {
                                ds.Transform = Matrix3x2.CreateScale(scale) * Matrix3x2.CreateTranslation(7.25f, 9.5f);
                                draw.Invoke(null, new object[] { ds, isolated, 0d, kind, 5, false });
                            }
                            using (var ds = target.CreateDrawingSession()) { ds.Clear(Colors.Transparent); ds.DrawImage(list); }
                            byte[] pixels = target.GetPixelBytes();
                            int width = (int)target.SizeInPixels.Width, nonzero = 0;
                            double left = command[19] * scale + 7.25, top = command[20] * scale + 9.5;
                            double right = left + command[21] * scale, bottom = top + command[22] * scale;
                            for (int i = 3; i < pixels.Length; i += 4)
                            {
                                if (pixels[i] == 0) continue;
                                int pixel = i / 4; double x = pixel % width + .5, y = pixel / width + .5;
                                if (x < left - 1 || x > right + 1 || y < top - 1 || y > bottom + 1)
                                    throw new Exception($"{kind} escaped its transformed clip at {time}/{scale}: {x},{y}.");
                                nonzero++;
                            }
                            if (nonzero < 20) throw new Exception("Clipping erased the visible icon.");
                            masks++;
                        }
                    }
                    report.Add($"PASS: {masks} native deferred clip masks; four fitted event icons, two partially clipped times, three scales and fractional translated canvas.");
                    if (clip.Frames[(int)(1.7 * clip.Fps)].Any(c => clip.Textures[(int)c[0]] == "event-icon.png"))
                        throw new Exception("Fully outside moving icon remains at 1.7 seconds.");
                }
                foreach (float dpi in new[] { 96f, 144f })
                foreach (float scale in new[] { 1f, 1.5f, 2f })
                foreach (bool original in new[] { true, false })
                {
                    using var target = new CanvasRenderTarget(device, clip.Width * scale, clip.Height * scale, dpi);
                    var clock = Stopwatch.StartNew();
                    for (int frame = 0; frame < 8; frame++)
                    {
                        using var ds = target.CreateDrawingSession();
                        ds.Clear(Colors.Transparent);
                        ds.Transform = Matrix3x2.CreateScale(scale);
                        draw.Invoke(null, new object[] { ds, clip, 1.1, "headshot", 5, original });
                    }
                    byte[] pixels = target.GetPixelBytes();
                    clock.Stop();
                    if (!pixels.Where((b, i) => i % 4 == 3).Any(a => a > 0)) throw new Exception("Empty sampling capture.");
                    string mode = original ? "before" : "after";
                    string name = $"{variant}-{mode}-{scale:0.0}x-{dpi:0}dpi.png";
                    using var file = File.Create(Path.Combine(output, name));
                    await target.SaveAsync(file.AsRandomAccessStream(), CanvasBitmapFileFormat.Png);
                    report.Add($"{name}: {target.SizeInPixels.Width}x{target.SizeInPixels.Height}, {clock.Elapsed.TotalMilliseconds / 8:0.00} ms/draw incl. final GPU readback; {clip.Frames[66].Length} commands, original source textures.");
                }
                foreach (var bitmap in textures.Values) bitmap.Dispose();
                textures.Clear();
            }
            File.WriteAllText(Path.Combine(output, "pass.txt"), string.Join(Environment.NewLine, report));
        }
        finally { foreach (var bitmap in textures.Values) bitmap.Dispose(); textures.Clear(); }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using KillConfirmCompatibility.Contracts;
using KillConfirmCompatibility.Desktop.Runtime;
using KillConfirmCompatibility.Desktop.Windowing;
using KillConfirmCompatibility.Services;
using Microsoft.Graphics.Canvas;

namespace KillConfirmCompatibility.Controls
{
    public sealed partial class KillConfirmAnimation
    {
        // Targeted SGS validation: all encoded frames against bake evidence,
        // then the heaviest encoded tier against the complete desktop wrapper.
        internal static async Task ValidateSanguoshaLz4Async(string output, string assetsRoot, string profile)
        {
            Directory.CreateDirectory(output);
            DesktopStorage.TestDataRoot = Path.Combine(output, "isolated-profile");
            DesktopStorage.AssetsRoot = Path.GetFullPath(assetsRoot);
            GameStyleService.Current = GameStyleMode.Sanguosha;
            ConfigureIconPack(GameStyleService.SanguoshaPackKey);
            ConfigureRenderSettings(0, 0);
            string folder = Path.Combine(assetsRoot, "Assets", "GameStyles", "sanguosha", "killconfirm", "lz4", profile);
            var integrity = new List<object>();
            int selectedTier = 0; long largest = -1;
            for (int tier = 0; tier < SanguoshaTierAnimations.Length; tier++)
            {
                string name = SanguoshaTierAnimations[tier];
                string path = Path.Combine(folder, name + ".sgslz4");
                using var metadata = JsonDocument.Parse(File.ReadAllText(Path.Combine(folder, name + ".metadata.json")));
                string[] hashes = metadata.RootElement.GetProperty("FrameHashes").EnumerateArray().Select(x => x.GetString()).ToArray();
                using var source = SanguoshaLz4Source.Open(File.OpenRead(path));
                if (source.FrameCount != hashes.Length) throw new InvalidDataException(name + ": frame count mismatch.");
                var raw = new byte[source.FrameBytes];
                for (int i = 0; i < source.FrameCount; i++)
                {
                    source.ReadFrame(i, raw);
                    if (Convert.ToHexString(SHA256.HashData(raw)) != hashes[i]) throw new InvalidDataException(name + ": frame hash mismatch " + i);
                }
                integrity.Add(new { Tier = name, source.Width, source.Height, source.FrameCount, source.FileBytes, AllFrameHashesMatch = true });
                if (source.FileBytes > largest) { largest = source.FileBytes; selectedTier = tier + 1; }
            }

            SanguoshaTierAssets assets = await LoadSanguoshaTierAsync(selectedTier);
            var animation = new KillConfirmAnimation();
            animation.UseDesktopFrameClock();
            animation._sanguoshaCurrent = assets;
            animation._isSanguoshaActive = true;
            animation._logicalFrameWidth = SanguoshaFrameWidth;
            animation._logicalFrameHeight = SanguoshaFrameHeight;
            animation._sanguoshaShake = new SanguoshaShake(assets.Clip, 20261107);
            lock (SanguoshaTiersOnScreen) SanguoshaTiersOnScreen.Add(assets);
            await EnsureSanguoshaPagesAsync(assets);
            string selectedName = SanguoshaTierAnimations[selectedTier - 1];
            using var selected = SanguoshaLz4Source.Open(File.OpenRead(Path.Combine(folder, selectedName + ".sgslz4")));
            var device = CanvasDevice.GetSharedDevice();
            using var target = new CanvasRenderTarget(device, selected.Width, selected.Height, 96);
            using var native = new LayeredFrameWindow("SGS exact LZ4 validation");
            var expected = new byte[selected.FrameBytes];
            var committed = new byte[selected.FrameBytes];
            using var evidence = JsonDocument.Parse(File.ReadAllText(Path.Combine(folder, selectedName + ".metadata.json")));
            using var direct = new SanguoshaFrameRenderer();
            var differences = new List<object>();
            int differentFrames = 0, maximumChannelError = 0, nativeMismatch = 0;
            try
            {
                for (int frame = 0; frame < selected.FrameCount; frame++)
                {
                    selected.ReadFrame(frame, expected);
                    animation._sanguoshaStartOffsetMs = frame * 1000d / selected.Fps;
                    using (var draw = target.CreateDrawingSession())
                        animation.DrawDesktopFrame(draw, selected.Width, selected.Height, cropEmptySpace: true);
                    byte[] actual = target.GetPixelBytes();
                    int error = 0;
                    for (int i = 0; i < actual.Length; i++) error = Math.Max(error, Math.Abs(actual[i] - expected[i]));
                    if (error != 0)
                    {
                        differentFrames++;
                        var sample = evidence.RootElement.GetProperty("Samples")[frame];
                        float recordedX = sample.GetProperty("ShakeX").GetSingle(), recordedY = sample.GetProperty("ShakeY").GetSingle();
                        animation._sanguoshaShake.Offset(animation.SanguoshaElapsedMs, out float runtimeX, out float runtimeY);
                        using (var drawing = target.CreateDrawingSession())
                        {
                            drawing.Clear(Colors.Transparent);
                            direct.RenderDirectAtTime(drawing, assets.Clip, assets.Pages,
                                TimeSpan.FromTicks(sample.GetProperty("TimeTicks").GetInt64()),
                                System.Numerics.Matrix3x2.CreateTranslation(recordedX, recordedY) * DesktopSanguoshaVariantTransform(selected.Width, selected.Height));
                        }
                        byte[] diagnostic = target.GetPixelBytes();
                        int directDifferent = 0, wrapperDifferent = 0;
                        for (int i = 0; i < diagnostic.Length; i++)
                        { if (diagnostic[i] != expected[i]) directDifferent++; if (actual[i] != expected[i]) wrapperDifferent++; }
                        var repeatCounts = new List<int>();
                        for (int repeat = 0; repeat < 3; repeat++)
                        {
                            using (var drawing = target.CreateDrawingSession()) animation.DrawDesktopFrame(drawing, selected.Width, selected.Height, cropEmptySpace: true);
                            var retry = target.GetPixelBytes(); int different = 0;
                            for (int i = 0; i < retry.Length; i++) if (retry[i] != expected[i]) different++;
                            repeatCounts.Add(different);
                        }
                        differences.Add(new { Frame = frame, DifferentBytes = wrapperDifferent, DirectDifferentBytes = directDifferent, RepeatWrapperDifferentBytes = repeatCounts,
                            DifferentSamples = Enumerable.Range(0, actual.Length).Where(i => actual[i] != expected[i]).Take(8)
                                .Select(i => new { X = i / 4 % selected.Width, Y = i / 4 / selected.Width, Channel = i % 4, Expected = expected[i], Actual = actual[i] }).ToArray(),
                            RecordedTicks = sample.GetProperty("TimeTicks").GetInt64(), RuntimeTicks = (long)(animation.SanguoshaElapsedMs * TimeSpan.TicksPerMillisecond),
                            recordedX, recordedY, runtimeX, runtimeY });
                    }
                    maximumChannelError = Math.Max(maximumChannelError, error);
                    var crop = new System.Windows.Int32Rect(0, 0, selected.Width, selected.Height);
                    var place = new NativeWindows.Rect { Left = 120, Top = 100, Right = 120 + selected.Width, Bottom = 100 + selected.Height };
                    native.PresentPixels(expected, selected.Width, selected.Height, crop, place, 1);
                    native.CopyPresentedBitmap().CopyPixels(committed, selected.Width * 4, 0);
                    if (!expected.SequenceEqual(committed)) nativeMismatch++;
                    // Let the UI service window messages; these are correctness
                    // timings, deliberately separate from playback performance.
                    if (frame % 15 == 0) await Task.Yield();
                }
            }
            finally { animation.StopDesktopPlayback(); animation.ReleaseAnimationResourcesForPackChange(); }
            var report = new { Profile = profile, Integrity = integrity, WrapperTier = selectedName,
                WrapperFrames = selected.FrameCount, WrapperDifferentFrames = differentFrames, MaximumChannelError = maximumChannelError,
                NativeDibDifferentFrames = nativeMismatch, Pass = differentFrames == 0 && nativeMismatch == 0,
                Differences = differences,
                Scope = "All 11 LZ4 clips validated against every bake hash; largest encoded tier compared against full DrawDesktopFrame and actual native DIB submission at exact physical dimensions. No performance inference from this correctness pass." };
            File.WriteAllText(Path.Combine(output, "sanguosha-lz4-validation.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
            if (differentFrames != 0 || nativeMismatch != 0) throw new InvalidDataException("Sanguosha LZ4 differs from the full production desktop wrapper.");
        }
    }
}

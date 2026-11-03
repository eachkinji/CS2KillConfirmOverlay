using KillConfirmCompatibility.Desktop.Runtime;
using KillConfirmCompatibility.Services;
using KillConfirmCompatibility.Contracts;
using KillConfirmCompatibility.Desktop.Windowing;
using Microsoft.Graphics.Canvas;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Windows.Foundation;

namespace KillConfirmCompatibility.Controls
{
    public sealed partial class KillConfirmAnimation
    {
        internal static async Task ValidateValorantCompositionAsync(string output, string assetsRoot)
        {
            Directory.CreateDirectory(output);
            DesktopStorage.TestDataRoot = Path.Combine(output, "isolated-profile");
            DesktopStorage.AssetsRoot = Path.GetFullPath(assetsRoot) + Path.DirectorySeparatorChar;
            GameStyleService.Current = GameStyleMode.Valorant;
            ValorantPackService.RefreshExternalPacks();
            var animation = new KillConfirmAnimation();
            var device = CanvasDevice.GetSharedDevice();
            var report = new List<string> { "pack,kills,headshot,frame,different_pixels,missing_pixels,added_pixels,max_channel_error" };
            int failures = 0;
            bool catalogOnly = Environment.GetEnvironmentVariable("KILLCONFIRM_VALORANT_CATALOG_TEST") == "1";
            try
            {
                using var white = CanvasBitmap.CreateFromBytes(device, new byte[] { 255, 255, 255, 255 }, 1, 1, Windows.Graphics.DirectX.DirectXPixelFormat.B8G8R8A8UIntNormalized);
                Action<CanvasDrawingSession> sample = session =>
                {
                    animation.DrawMultiplyTintImage(session, white, new Rect(8, 8, 24, 24), new Rect(0, 0, 1, 1), Colors.Red, 1);
                    animation.DrawMultiplyTintImage(session, white, new Rect(40, 8, 24, 24), new Rect(0, 0, 1, 1), Colors.Blue, 1);
                };
                var isolation = await CompareCompositionAsync(output, "effect-isolation", 80, 48, sample, true);
                File.WriteAllText(Path.Combine(output, "effect-isolation.txt"), isolation.ToString());
                if (isolation.Different > 0) failures++;
                var packs = new List<string> { ValorantPackService.DefaultKey };
                string extraPack = Environment.GetEnvironmentVariable("KILLCONFIRM_VALORANT_TEST_PACK");
                if (catalogOnly) packs = new List<string>(System.Linq.Enumerable.Select(ValorantPackService.All, pack => pack.Key));
                else if (!string.IsNullOrWhiteSpace(extraPack)) packs.Add(extraPack);
                foreach (string pack in packs)
                foreach (int kills in catalogOnly ? new[] { 6 } : new[] { 1, 2, 3, 4, 5, 6 })
                foreach (bool headshot in catalogOnly ? new[] { true } : new[] { false, true })
                {
                    var asset = await animation.LoadValorantKillAssetAsync(pack, kills, headshot);
                    animation._currentValorantAsset = asset.ValorantAsset;
                    animation.ApplyViewportSize(ValorantFrameWidth, ValorantFrameHeight);
                    int[] frames = catalogOnly ? new[] { 30, 70, 200 } : kills >= 5 ? new[] { 0, 5, 10, 20, 30, 45, 50, 70, 100, 140, 200, 300, 375, 400, 420 }
                        : new[] { 0, 2, 5, 9, 15, 22, 30, 45, 60, 90, 110, 125 };
                    foreach (int frame in frames)
                    {
                        animation._currentFrame = frame;
                        string name = pack + "-" + kills + (headshot ? "-hs" : "") + "-frame" + frame;
                        var comparison = await CompareCompositionAsync(output, name, 1375, 988,
                            session => animation.DrawDesktopFrame(session, 1375, 988, clearTarget: false),
                            !catalogOnly && kills == 6 && headshot && (frame == 70 || frame == 200));
                        report.Add($"{pack},{kills},{headshot},{frame},{comparison.Different},{comparison.Missing},{comparison.Added},{comparison.MaxError}");
                        if (comparison.Different > 0) failures++;
                    }
                    File.WriteAllLines(Path.Combine(output, "composition.csv"), report);
                }
                File.WriteAllText(Path.Combine(output, "summary.txt"), $"Packs={packs.Count}; frames={report.Count - 1}; failed comparisons={failures}; identical physical resolution and animation time; compare direct drawing to recorded drawing.");
                if (failures > 0) throw new InvalidOperationException($"Deferred composition changed {failures} reference images. See composition.csv and effect-isolation.txt.");
                if (!catalogOnly) await ValidateNativeTimelineAsync(output, packs);
            }
            finally { animation.StopDesktopPlayback(); }
        }

        private static async Task ValidateNativeTimelineAsync(string output, List<string> packs)
        {
            var animation = new KillConfirmAnimation();
            animation.UseDesktopFrameClock();
            using var surface = new OverlaySurface("Lower", "VALORANT composition validation", animation);
            var screen = Array.Find(NativeWindows.Screens(), monitor => (monitor.Flags & 1) != 0).Monitor;
            var profile = new ElementLayout { X = 0.5, Y = 0.8, Scale = 2.0789281794113688, Visible = true };
            var report = new List<string> { "pack,frame,width,height,different_pixels,missing_pixels,added_pixels,max_channel_error" };
            foreach (string pack in packs)
            {
                var asset = await animation.LoadValorantKillAssetAsync(pack, 6, true);
                animation._currentValorantAsset = asset.ValorantAsset;
                animation.ApplyViewportSize(ValorantFrameWidth, ValorantFrameHeight);
                animation.Visibility = Windows.UI.Xaml.Visibility.Visible;
                animation.Opacity = 1;
                surface.Place(screen, profile, true, false);
                var size = surface.PresentedSize;
                using var reference = new CanvasRenderTarget(CanvasDevice.GetSharedDevice(), (int)size.Width, (int)size.Height, 96);
                for (int frame = 0; frame <= GetNativeValorantFrameCount(6); frame++)
                {
                    animation._currentFrame = frame;
                    animation.SpriteCanvas.Invalidate();
                    using (var session = reference.CreateDrawingSession()) animation.DrawDesktopFrame(session, size.Width, size.Height, cropEmptySpace: true);
                    byte[] pixels = reference.GetPixelBytes();
                    // Direct reference drawing marks its canvas clean. Force the
                    // actual surface to present this same deterministic frame.
                    animation.SpriteCanvas.Invalidate();
                    surface.Render();
                    var result = surface.CompareCommittedReference(pixels, (int)size.Width, (int)size.Height);
                    report.Add($"{pack},{frame},{size.Width},{size.Height},{result.Different},{result.Missing},{result.Added},{result.MaxError}");
                    if (result.Different > 0)
                    {
                        surface.ValidateCroppedPlacement(Path.Combine(output, "native-failure.png"));
                        File.WriteAllLines(Path.Combine(output, "native-timeline.csv"), report);
                        throw new InvalidOperationException($"Native crop/scale differs from full-frame reference: {pack}, frame {frame}: {result}.");
                    }
                    if (frame % 30 == 0)
                    {
                        File.WriteAllLines(Path.Combine(output, "native-timeline.csv"), report);
                        await Task.Delay(1);
                    }
                }
            }
            File.WriteAllLines(Path.Combine(output, "native-timeline.csv"), report);
            File.WriteAllText(Path.Combine(output, "native-pass.txt"), $"PASS: {report.Count - 1} actual native frames at {profile.Scale:P2} zoom match full-resolution direct rendering; no missing/added pixels, rescale, or anchor drift through the full six-kill entrance, particles, hold and outro.");
        }

        private static async Task<(int Different, int Missing, int Added, int MaxError)> CompareCompositionAsync(
            string output, string name, int width, int height, Action<CanvasDrawingSession> draw, bool save)
        {
            var device = CanvasDevice.GetSharedDevice();
            using var direct = new CanvasRenderTarget(device, width, height, 96);
            using (var session = direct.CreateDrawingSession()) { session.Clear(Colors.Transparent); draw(session); }
            byte[] reference = direct.GetPixelBytes();
            using var recorded = new CanvasRenderTarget(device, width, height, 96);
            using var commands = new CanvasCommandList(device);
            using (var session = commands.CreateDrawingSession()) draw(session);
            using (var session = recorded.CreateDrawingSession()) { session.Clear(Colors.Transparent); session.DrawImage(commands); }
            byte[] actual = recorded.GetPixelBytes();
            int different = 0, missing = 0, added = 0, maximum = 0;
            for (int i = 0; i < actual.Length; i += 4)
            {
                int error = 0;
                for (int channel = 0; channel < 4; channel++) error = Math.Max(error, Math.Abs(reference[i + channel] - actual[i + channel]));
                maximum = Math.Max(maximum, error);
                if (error > 3) different++;
                if (reference[i + 3] > 8 && actual[i + 3] == 0) missing++;
                if (actual[i + 3] > 8 && reference[i + 3] == 0) added++;
            }
            if (save)
            {
                using (var stream = File.Create(Path.Combine(output, name + "-direct.png"))) await direct.SaveAsync(stream.AsRandomAccessStream(), CanvasBitmapFileFormat.Png);
                using (var stream = File.Create(Path.Combine(output, name + "-recorded.png"))) await recorded.SaveAsync(stream.AsRandomAccessStream(), CanvasBitmapFileFormat.Png);
            }
            return (different, missing, added, maximum);
        }
    }
}

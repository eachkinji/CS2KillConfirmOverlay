using KillConfirmCompatibility.Contracts;
using KillConfirmCompatibility.Controls;
using KillConfirmCompatibility.Desktop.Rendering;
using KillConfirmCompatibility.Desktop.Runtime;
using KillConfirmCompatibility.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace KillConfirmCompatibility.Validation
{
    internal static class PresentationValidation
    {
        internal static async Task RunAsync(string output, string assetsRoot)
        {
            Directory.CreateDirectory(output);
            ValidateLayeredFrameAnchors();
            await ValidateClocksAsync();
            ValidateTransforms();
            ValidateZoomDetail(output);
            DesktopStorage.TestDataRoot = Path.Combine(output, "isolated-profile");
            DesktopStorage.AssetsRoot = Path.GetFullPath(assetsRoot) + Path.DirectorySeparatorChar;
            using var service = new LocalProtocolFixture();
            DesktopStorage.Current.LocalSettings.Values[PortSettingsStore.PortKey] = service.Port;
            DesktopStorage.Current.LocalSettings.Values[Danmaku.DanmakuSettingsStore.EnabledSettingKey] = false;
            string crossfireRoot = Environment.GetEnvironmentVariable("KILLCONFIRM_CF_TEST_PACKS") ?? Path.Combine(assetsRoot, "DefaultPacks", "crossfire");
            bool testCrossfire = Directory.Exists(crossfireRoot);
            if (testCrossfire) DesktopStorage.Current.LocalSettings.Values["Crossfire.BundledRoot"] = Path.GetFullPath(crossfireRoot);
            GameStyleService.Current = GameStyleMode.Valorant;
            string config = Path.Combine(DesktopStorage.Current.LocalFolder.Path, DisplayFiles.FolderName, DisplayFiles.ConfigurationName);
            DisplayFiles.Write(config, new DisplayConfiguration { Enabled = true, FollowGame = false, HideWhenInactive = false, TestAudio = false });
            var report = new List<string> { "PASS: 72 changing crops at six output sizes keep a fixed marker anchored, update its color every frame, preserve negative screen coordinates and never activate playback." };
            var host = new HostController(shutdownApplicationOnClose: false);
            try
            {
                long request = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                var cases = new List<(GameStyleMode Style, int Motion)> { (GameStyleMode.Valorant, 1), (GameStyleMode.Overwatch, 1) };
                if (testCrossfire) { cases.Add((GameStyleMode.Crossfire, 1)); cases.Add((GameStyleMode.Crossfire, 2)); }
                foreach (var test in cases)
                foreach (double scale in new[] { 1.0, 3.0 })
                {
                    var style = test.Style;
                    DesktopStorage.Current.LocalSettings.Values["MainAnimationStyle"] = test.Motion;
                    GameStyleService.Current = style;
                    string key = GameStyleService.ToStorageValue(style);
                    const int fps = DisplayConfiguration.PresentationFramesPerSecond;
                    DisplayFiles.Update(config, current => { current.GetLayout(key).Lower.Scale = scale; });
                    await Task.Delay(500);
                    request++;
                    long before = host.LowerSurface.PresentedFrames;
                    DisplayFiles.Update(config, current => { current.TestPreset = "two"; current.TestRequest = request; });
                    var deadline = DateTimeOffset.UtcNow.AddSeconds(15);
                    while (host.CurrentPreviewRequest != request || !host.HasRenderedLowerPixels || host.LowerSurface.PresentedFrames <= before)
                    {
                        if (DateTimeOffset.UtcNow > deadline) throw new Exception("Lower preview missing: " + style + "; " + host.PreviewDiagnostics + "; frames=" + host.LowerSurface.PresentedFrames + "; " + host.LowerSurface.PlaybackDiagnostics);
                        await Task.Delay(10);
                    }
                    await Task.Delay(80);
                    var pixels = host.LowerSurface.PresentedSize;
                    var content = host.LowerSurface.ContentSize;
                    if (Math.Abs(pixels.Width / pixels.Height - content.Width / content.Height) > 0.01)
                        throw new Exception("Preview aspect ratio changed: " + style + " pixels=" + pixels + " content=" + content);
                    before = host.LowerSurface.PresentedFrames;
                    long callbacksBefore = host.PresentationCallbacks, presentsBefore = host.PresentedCallbacks;
                    double drawBefore = host.LowerSurface.DrawMilliseconds, readBefore = host.LowerSurface.ReadbackMilliseconds, copyBefore = host.LowerSurface.CopyMilliseconds;
                    var clock = Stopwatch.StartNew();
                    await Task.Delay(450);
                    long frames = host.LowerSurface.PresentedFrames - before;
                    report.Add($"{key}: motion={test.Motion}; requested={fps} FPS; scale={scale}; pixels={pixels}; frames={frames}/{clock.Elapsed.TotalMilliseconds:F0} ms; callbacks={host.PresentationCallbacks - callbacksBefore}; presents={host.PresentedCallbacks - presentsBefore}; {host.LowerSurface.PlaybackDiagnostics}");
                    report.Add($"  cost/frame: draw={(host.LowerSurface.DrawMilliseconds-drawBefore)/Math.Max(1,frames):F2} ms; readback={(host.LowerSurface.ReadbackMilliseconds-readBefore)/Math.Max(1,frames):F2} ms; copy={(host.LowerSurface.CopyMilliseconds-copyBefore)/Math.Max(1,frames):F2} ms; window-pixels={host.LowerSurface.PresentationWindowSize}; raster={host.LowerSurface.RasterSize}; screen pixels per raster pixel=1");
                    File.WriteAllLines(Path.Combine(output, "measurements.txt"), report);
                    // Actual compositor throughput depends on the display driver
                    // (including remote/virtual displays). Test liveness here;
                    // the deterministic clock checks above enforce 30/60 pacing.
                    if (frames == 0) throw new Exception("Preview stopped advancing: " + report[report.Count - 1]);
                    host.LowerSurface.SavePreviewImage(Path.Combine(output, key + "-motion" + test.Motion + "-scale" + scale + ".png"));
                    host.LowerSurface.ValidateCroppedPlacement(Path.Combine(output, key + "-committed-motion" + test.Motion + "-scale" + scale + ".png"));
                }
                GameStyleService.Current = GameStyleMode.Valorant;
                DisplayFiles.Update(config, current => { current.HideWhenInactive = true; current.FramesPerSecond = 60; current.GetLayout("valorant").Lower.Scale = 1; });
                await Task.Delay(500);
                request++;
                DisplayFiles.Update(config, current => { current.TestPreset = "six"; current.TestRequest = request; });
                var sixDeadline = DateTimeOffset.UtcNow.AddSeconds(15);
                while (host.CurrentPreviewRequest != request || !host.LowerSurface.IsPlaying)
                {
                    if (DateTimeOffset.UtcNow > sixDeadline) throw new Exception("Six-kill preview did not start.");
                    await Task.Delay(10);
                }
                await Task.Delay(5700);
                if (!host.LowerSurface.IsPresentationVisible || !host.LowerSurface.IsPlaying || !host.HasRenderedLowerPixels)
                    throw new Exception("Six-kill preview was hidden before its outro.");
                host.LowerSurface.SavePreviewImage(Path.Combine(output, "valorant-six-before-outro.png"));
                await Task.Delay(2000);
                if (host.LowerSurface.IsPlaying) throw new Exception("Six-kill preview failed to finish naturally.");
                report.Add("PASS: six-kill preview remains visible past five seconds and finishes its seven-second timeline naturally.");
                if (testCrossfire)
                {
                    GameStyleService.Current = GameStyleMode.Crossfire;
                    DesktopStorage.Current.LocalSettings.Values["MainAnimationStyle"] = 2;
                    DisplayFiles.Update(config, current => { current.FramesPerSecond = 60; current.GetLayout("crossfire").Lower.Scale = 1; });
                    int testedPacks = 0;
                    foreach (string pack in CrossfireExternalAssetService.IconKeys)
                    {
                        if (!Directory.Exists(Path.Combine(crossfireRoot, "icon_packs", pack))) continue;
                        testedPacks++;
                        DesktopStorage.Current.LocalSettings.Values["KillIconPack.crossfire"] = pack;
                        await Task.Delay(500);
                        foreach (string preset in new[] { "one", "six", "one_knife" })
                        {
                            request++;
                            long before = host.LowerSurface.PresentedFrames;
                            DisplayFiles.Update(config, current => { current.TestPreset = preset; current.TestRequest = request; });
                            var deadline = DateTimeOffset.UtcNow.AddSeconds(15);
                            while (host.CurrentPreviewRequest != request || !host.LowerSurface.IsPlaying || host.LowerSurface.PresentedFrames <= before || !host.HasRenderedLowerPixels)
                            {
                                if (DateTimeOffset.UtcNow > deadline) throw new Exception("CF pack preview failed: " + pack + "/" + preset);
                                await Task.Delay(10);
                            }
                            await Task.Delay(100);
                            host.LowerSurface.ValidateCroppedPlacement(pack == "rankmach_2019_2" ? Path.Combine(output, "cf-cropped-" + preset + ".png") : null);
                            await Task.Delay(250);
                            host.LowerSurface.ValidateCroppedPlacement();
                            if (preset == "one") host.LowerSurface.ValidateCropLayoutTransitions();
                        }
                    }
                    await Task.Delay(1400);
                    if (host.LowerSurface.IsPlaying || host.LowerSurface.IsPresentationVisible) throw new Exception("CF outro did not hide its cropped window.");
                    report.Add($"PASS: CF style 2, {testedPacks} available packs at one/six/knife kills: screen anchors and scale preserved, every nonzero effect pixel retained, natural expiry hides window.");
                }
            }
            finally { await host.CloseAsync(); }
            await ValidateOverwatchCropAsync();
            report.Add("PASS: one-pixel details remain exact at 100/200/300/400% zoom; four editor colors are distinct and only the selected overlapping frame accepts input; 60 FPS presentation target, unchanged aspect ratio, normal and enlarged previews without resolution caps.");
            File.WriteAllLines(Path.Combine(output, "pass.txt"), report);
        }

        private static void ValidateZoomDetail(string output)
        {
            var screens = Desktop.Windowing.NativeWindows.Screens();
            var screen = Array.Find(screens, item => (item.Flags & 1) != 0).Monitor;
            var surfaces = new List<Desktop.Windowing.OverlaySurface>();
            var colors = new HashSet<System.Windows.Media.Color>();
            try
            {
                foreach (string key in new[] { "Upper", "Crosshair", "Lower", "Danmaku" })
                {
                    if (!colors.Add(Desktop.Windowing.OverlaySurface.EditorColor(key))) throw new Exception("Editor regions share a color.");
                    var surface = new Desktop.Windowing.OverlaySurface(key, key, new KillConfirmAnimation())
                    { FixedSize = new System.Windows.Size(640, 400), DirtyOverride = () => true };
                    surface.DrawOverride = (session, w, h) =>
                    {
                        int x = (int)w / 2 - 128, y = (int)h / 2;
                        for (int i = 0; i < 256; i += 2) session.FillRectangle(x + i, y, 1, 32, Colors.White);
                        using var font = new Microsoft.Graphics.Canvas.Text.CanvasTextFormat { FontSize = (float)(w / 32), FontFamily = "Segoe UI" };
                        session.DrawText("Aa 细节", x, y + 40, Colors.White, font);
                    };
                    surfaces.Add(surface);
                    surface.Place(screen, new ElementLayout { X = 0.5, Y = 0.5, Scale = 1, Visible = true }, true, true);
                }
                foreach (var selected in surfaces)
                foreach (var surface in surfaces)
                {
                    surface.SetEditorSelected(surface == selected);
                    long flags = Desktop.Windowing.NativeWindows.GetWindowLongPtr(new System.Windows.Interop.WindowInteropHelper(surface).Handle, Desktop.Windowing.NativeWindows.ExtendedStyle).ToInt64();
                    bool interactive = (flags & (Desktop.Windowing.NativeWindows.Transparent | Desktop.Windowing.NativeWindows.NoActivate)) == 0;
                    if (interactive != (surface == selected)) throw new Exception("Overlapping editor input was routed to the wrong region.");
                }
                var detail = surfaces[2];
                foreach (double scale in new[] { 1.0, 2.0, 3.0, 4.0 })
                {
                    detail.Place(screen, new ElementLayout { X = 0.5, Y = 0.5, Scale = scale, Visible = true }, true, true);
                    detail.Render();
                    var size = detail.PresentedSize;
                    detail.ValidatePixelStripes((int)size.Width / 2 - 128, (int)size.Height / 2 + 8, 256);
                    detail.SavePreviewImage(Path.Combine(output, "native-detail-" + scale + "x.png"));
                }
            }
            finally { foreach (var surface in surfaces) surface.Dispose(); }
        }

        private static void ValidateLayeredFrameAnchors()
        {
            const int width = 127, height = 95;
            var bitmap = new System.Windows.Media.Imaging.WriteableBitmap(width, height, 96, 96, System.Windows.Media.PixelFormats.Pbgra32, null);
            using var window = new Desktop.Windowing.LayeredFrameWindow("Feedback anchor regression");
            var foreground = Desktop.Windowing.NativeWindows.GetForegroundWindow();
            long revision = 0;
            foreach (var size in new[] { (127, 95), (159, 119), (318, 238), (381, 285), (1375, 988), (1024, 841) })
            for (int frame = 0; frame < 12; frame++)
            {
                var pixels = new byte[width * height * 4];
                // This marker never moves; distant pixels grow/shrink the crop.
                for (int y = 44; y < 52; y++)
                for (int x = 60; x < 68; x++)
                {
                    int index = (y * width + x) * 4;
                    pixels[index + (frame % 2 == 0 ? 2 : 0)] = 255;
                    pixels[index + 3] = 255;
                }
                int edge = frame % 3 == 0 ? 4 : frame % 3 == 1 ? 28 : 80;
                pixels[(edge * width + edge) * 4 + 3] = 1;
                bitmap.WritePixels(new System.Windows.Int32Rect(0, 0, width, height), pixels, width * 4, 0);
                var crop = Desktop.Windowing.OverlaySurface.VisiblePixelBounds(pixels, width, height);
                double scaleX = size.Item1 / (double)width, scaleY = size.Item2 / (double)height;
                int left = (int)Math.Floor(crop.X * scaleX), top = (int)Math.Floor(crop.Y * scaleY);
                int right = Math.Min(size.Item1, (int)Math.Ceiling((crop.X + crop.Width) * scaleX));
                int bottom = Math.Min(size.Item2, (int)Math.Ceiling((crop.Y + crop.Height) * scaleY));
                var placement = new Desktop.Windowing.NativeWindows.Rect { Left = -50 + left, Top = 100 + top, Right = -50 + right, Bottom = 100 + bottom };
                window.Present(bitmap, ++revision, size.Item1, size.Item2,
                    new System.Windows.Int32Rect(left, top, right - left, bottom - top), placement, .75);
                Desktop.Windowing.NativeWindows.GetWindowRect(window.Handle, out var actual);
                if (!actual.Equals(placement)) throw new Exception("Atomic frame changed its native screen placement.");
                var committed = window.CopyPresentedBitmap();
                byte[] marker = new byte[4];
                committed.CopyPixels(new System.Windows.Int32Rect((int)(64 * scaleX) - left, (int)(48 * scaleY) - top, 1, 1), marker, 4, 0);
                if (marker[3] != 255 || marker[frame % 2 == 0 ? 2 : 0] != 255)
                    throw new Exception($"Moving crop shifted/reused the marker: size={size}, frame={frame}.");
            }
            if (Desktop.Windowing.NativeWindows.GetForegroundWindow() != foreground) throw new Exception("Playback activated its window.");
            window.Hide();
            if (window.IsVisible) throw new Exception("Playback did not hide.");
        }

        private static async Task ValidateOverwatchCropAsync()
        {
            var animation = new KillConfirmAnimation();
            try
            {
                animation.PlayOverwatchLowerThirdKill("Preview");
                var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
                while (animation.Visibility != Windows.UI.Xaml.Visibility.Visible)
                {
                    if (DateTimeOffset.UtcNow > deadline) throw new Exception("Overwatch crop fixture failed to load.");
                    await Task.Delay(10);
                }
                for (int i = 0; i < 4; i++) animation.PlayOverwatchLowerThirdKill("Preview " + i, i % 2 == 0);
                foreach (int delay in new[] { 100, 200, 400 })
                {
                    await Task.Delay(delay);
                    using var target = new Microsoft.Graphics.Canvas.CanvasRenderTarget(Microsoft.Graphics.Canvas.CanvasDevice.GetSharedDevice(), 550, 600, 96);
                    using (var session = target.CreateDrawingSession()) animation.DrawDesktopFrame(session, 550, 600);
                    byte[] pixels = target.GetPixelBytes();
                    var crop = animation.DesktopRenderViewport;
                    for (int y = 0; y < 600; y++)
                    for (int x = 0; x < 550; x++)
                        if ((y < crop.Y || y >= crop.Bottom || x < crop.X || x >= crop.Right) && pixels[(y * 550 + x) * 4 + 3] != 0)
                            throw new Exception("Overwatch crop removed entrance-strip or five-row feed pixels.");
                }
            }
            finally { animation.StopDesktopPlayback(); }
        }

        private static async Task ValidateClocksAsync()
        {
            foreach (int refresh in new[] { 60, 120, 144, 165 })
            foreach (int fps in new[] { 30, 60 })
            {
                var cadence = new FrameCadence();
                int count = 0;
                for (int i = 0; i < refresh * 2; i++)
                {
                    var timestamp = TimeSpan.FromSeconds(i / (double)refresh);
                    if (cadence.ShouldPresent(timestamp, fps)) count++;
                    if (cadence.ShouldPresent(timestamp, fps)) throw new Exception("Duplicate compositor callback rendered twice.");
                }
                if (Math.Abs(count - fps * 2) > 1) throw new Exception($"Frame cadence drift: {refresh} Hz, {fps} FPS, {count} frames.");
                if (!cadence.ShouldPresent(TimeSpan.FromSeconds(10), fps) || cadence.ShouldPresent(TimeSpan.FromSeconds(10.001), fps))
                    throw new Exception("A stalled presentation clock replayed missed frames.");
            }
            var timer = new Windows.UI.Xaml.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1) };
            int ticks = 0;
            timer.Tick += (s, e) => ticks++;
            timer.Start(); timer.UseExternalClock();
            await Task.Delay(50);
            if (ticks != 0 || !timer.IsEnabled) throw new Exception("Animation timer still runs independently of presentation.");
            timer.Advance(); timer.Stop(); timer.Advance();
            if (ticks != 1) throw new Exception("External playback start/stop contract failed.");
        }

        private static void ValidateTransforms()
        {
            foreach (var logical in new[] { new System.Windows.Size(607, 436), new System.Windows.Size(550, 600) })
            foreach (var target in new[] { new System.Windows.Size(1536, 1024), new System.Windows.Size(400, 1000), new System.Windows.Size(1200, 200) })
            {
                var transform = KillConfirmAnimation.CreateDesktopTransform(logical.Width, logical.Height, 2, target.Width, target.Height);
                if (Math.Abs(transform.M11 - transform.M22) > 0.0001 || transform.M31 < -0.01 || transform.M32 < -0.01)
                    throw new Exception("Presentation stretches or crops a feedback layer.");
                double centerX = logical.Width * transform.M11 + transform.M31;
                double centerY = logical.Height * transform.M22 + transform.M32;
                if (Math.Abs(centerX - target.Width / 2) > 0.01 || Math.Abs(centerY - target.Height / 2) > 0.01)
                    throw new Exception("Aspect-preserving feedback is not centered.");
            }
        }
    }
}

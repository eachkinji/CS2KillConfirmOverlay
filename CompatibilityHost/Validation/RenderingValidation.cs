using KillConfirmCompatibility.Contracts;
using KillConfirmCompatibility.Controls;
using KillConfirmCompatibility.Desktop.Runtime;
using KillConfirmCompatibility.Desktop.Windowing;
using KillConfirmCompatibility.Services;
using Microsoft.Graphics.Canvas;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Interop;

namespace KillConfirmCompatibility.Validation
{
    internal static class RenderingValidation
    {
        internal static async Task RunAsync(string outputDirectory, string assetsRoot)
        {
            Directory.CreateDirectory(outputDirectory);
            DesktopStorage.TestDataRoot = Path.Combine(outputDirectory, "isolated-profile");
            // AppContext.BaseDirectory in a real installation ends in a separator.
            DesktopStorage.AssetsRoot = Path.GetFullPath(assetsRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var rootAsset = new Uri("ms-appx:///Assets/GameStyles/valorant/killconfirm/_native/shared/textures/Base_FrameBG.png");
            await DesktopStorage.AssetFileAsync(rootAsset);
            bool traversalRejected=false;
            try { await DesktopStorage.AssetFileAsync(new Uri("ms-appx:///Assets/%2e%2e%2f%2e%2e%2foutside.png")); }
            catch(InvalidDataException) { traversalRejected=true; }
            if(!traversalRejected) throw new Exception("Asset traversal was not rejected.");
            ValidateConfiguration(outputDirectory);
            ValidateNativeWindow();
            var presenter = new FeedbackPresenter();
            var animations = new[] { presenter.LowerFeedbackAnimation, presenter.CrosshairFeedbackAnimation, presenter.UpperFeedbackAnimation, presenter.LowerBadgeAnimation };
            var report = new List<string>();
            foreach (GameStyleMode style in Enum.GetValues(typeof(GameStyleMode)))
            {
                foreach (var animation in animations) animation.StopDesktopPlayback();
                GameStyleService.Current = style;
                await presenter.ApplyConfigurationAsync();
                presenter.HandleKillEvent(new KillEvent { KillCount = 3, IsHeadshot = true, PlayMainAnimation = true, EventChannel = "combat", EventKind = "kill", AnimationKey = "multi3", TargetName = "Validation", WeaponName = "AK-47", MoneyReward = 300 });
                int visiblePixels = 0;
                for (int sample = 0; sample < 25 && visiblePixels == 0; sample++)
                {
                    await Task.Delay(100);
                    foreach (KillConfirmAnimation animation in animations)
                    {
                        if (animation.Visibility != Windows.UI.Xaml.Visibility.Visible) continue;
                        double w = Math.Max(1, animation.DisplayViewportWidth), h = Math.Max(1, animation.DisplayViewportHeight);
                        double fit = Math.Min(1, Math.Min(960 / w, 540 / h));
                        int width = Math.Max(1, (int)(w * fit)), height = Math.Max(1, (int)(h * fit));
                        using var target = new CanvasRenderTarget(CanvasDevice.GetSharedDevice(), width, height, 96);
                        using (var session = target.CreateDrawingSession()) animation.DrawDesktopFrame(session, width, height);
                        byte[] pixels = target.GetPixelBytes();
                        int count = Enumerable.Range(0, pixels.Length / 4).Count(index => pixels[index * 4 + 3] > 0);
                        if (count == 0) continue;
                        visiblePixels += count;
                        using var stream = File.Open(Path.Combine(outputDirectory, style + "-" + Array.IndexOf(animations, animation) + ".png"), FileMode.Create);
                        await target.SaveAsync(stream.AsRandomAccessStream(), CanvasBitmapFileFormat.Png);
                    }
                }
                report.Add(style + ": visiblePixels=" + visiblePixels);
                File.WriteAllLines(Path.Combine(outputDirectory, "results.txt"), report);
            }
            foreach (var animation in animations) animation.StopDesktopPlayback();
            if (report.Any(line => line.EndsWith("=0"))) throw new InvalidOperationException("Styles failed pixel validation: " + string.Join(", ", report.Where(line => line.EndsWith("=0"))));
            await RuntimeValidation.RunAsync(outputDirectory);
        }
        private static void ValidateConfiguration(string output)
        {
            var legacy = new DisplayConfiguration { Version = 1, FollowGame = false, ScreenName = "retained-monitor", FramesPerSecond = 30 };
            legacy.GetLayout("default").Danmaku.Y = 0.2;
            legacy.GetLayout("custom").Danmaku.X = 0.7;
            legacy.GetLayout("custom").Danmaku.Scale = 1.4;
            legacy.GetLayout("default").Lower.X = 0.23;
            legacy.Normalize(); legacy.Normalize();
            if (legacy.Version != 2 || legacy.GetLayout("default").Danmaku.Y != 0.5 || legacy.GetLayout("custom").Danmaku.X != 0.7 || legacy.GetLayout("custom").Danmaku.Scale != 1.4 || legacy.GetLayout("default").Lower.X != 0.23 || legacy.FollowGame || legacy.FramesPerSecond != 30 || legacy.ScreenName != "retained-monitor")
                throw new Exception("Default danmaku migration changed custom layouts or shared settings.");
            string path = Path.Combine(output, "contracts", DisplayFiles.ConfigurationName);
            var config = new DisplayConfiguration { Enabled = true };
            var cf = config.GetLayout("crossfire"); cf.Lower.X = double.NaN; cf.Lower.Y = 2; cf.Lower.Scale = double.PositiveInfinity;
            config.Normalize();
            if (cf.Lower.X != 0.5 || cf.Lower.Y != 1 || cf.Lower.Scale != 1) throw new Exception("Invalid geometry was not normalized.");
            DisplayFiles.Write(path, config);
            Parallel.For(0, 30, index => DisplayFiles.Update(path, current => current.GetLayout("profile" + index).Lower.X = 0.25));
            var loaded = DisplayFiles.Read<DisplayConfiguration>(path);
            if (!loaded.Enabled || loaded.Layouts.Count != 31 || loaded.GetLayout("valorant").Lower.X != 0.5) throw new Exception("Concurrent layout updates were lost or leaked across styles.");
            using (var ready = new System.Threading.ManualResetEventSlim())
            {
                var reader = Task.Run(() => {
                    using var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    ready.Set();
                    System.Threading.Thread.Sleep(100);
                });
                if (!ready.Wait(3000)) throw new Exception("Could not create the transient file lock.");
                loaded.ModeRequest = 123;
                DisplayFiles.Write(path, loaded);
                reader.GetAwaiter().GetResult();
                if (DisplayFiles.Read<DisplayConfiguration>(path)?.ModeRequest != 123) throw new Exception("Atomic write did not recover from a transient replacement lock.");
            }
            using (var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                loaded.ModeRequest = 456;
                bool failed = false;
                try { DisplayFiles.Write(path, loaded); }
                catch (IOException) { failed = true; }
                if (!failed || DisplayFiles.Read<DisplayConfiguration>(path)?.ModeRequest != 123) throw new Exception("A persistent lock lost the previous valid configuration.");
            }
            if (Directory.GetFiles(Path.GetDirectoryName(path), Path.GetFileName(path) + ".*.tmp").Length != 0) throw new Exception("Replacement failure leaked temporary files.");
            File.WriteAllText(path, "{ broken json");
            if (DisplayFiles.Read<DisplayConfiguration>(path) != null) throw new Exception("Malformed configuration was not rejected.");
            File.WriteAllText(Path.Combine(output, "contracts.txt"), "PASS: bounds, concurrent updates, independent profiles, transient replacement-lock recovery, persistent lock preserves valid data, temporary file cleanup, malformed configuration");
        }
        private static void ValidateNativeWindow()
        {
            foreach (var viewport in new[] { new System.Windows.Size(1920, 1080), new System.Windows.Size(3840, 2160), new System.Windows.Size(5120, 1440), new System.Windows.Size(1440, 2560) })
            {
                var target = OverlaySurface.CalculateRenderSize(viewport.Width, viewport.Height, true);
                if (target.Width > 1536 || target.Height > 1024 || Math.Abs(target.Width / target.Height - viewport.Width / viewport.Height) > 0.005)
                    throw new Exception("Danmaku render texture distorted the screen aspect ratio: " + viewport);
            }
            using var surface = new OverlaySurface("Test", "Validation", new KillConfirmAnimation());
            IntPtr handle = new WindowInteropHelper(surface).Handle;
            NativeWindows.SetInputMode(handle, false);
            long styles = NativeWindows.GetWindowLongPtr(handle, NativeWindows.ExtendedStyle).ToInt64();
            if ((styles & (NativeWindows.Transparent | NativeWindows.NoActivate)) != (NativeWindows.Transparent | NativeWindows.NoActivate)) throw new Exception("Overlay steals mouse or focus.");
            NativeWindows.SetInputMode(handle, true);
            styles = NativeWindows.GetWindowLongPtr(handle, NativeWindows.ExtendedStyle).ToInt64();
            if ((styles & (NativeWindows.Transparent | NativeWindows.NoActivate)) != 0) throw new Exception("Editor remains click-through.");
            if (NativeWindows.Screens().Length == 0) throw new Exception("No display monitors enumerated.");
        }
    }
}

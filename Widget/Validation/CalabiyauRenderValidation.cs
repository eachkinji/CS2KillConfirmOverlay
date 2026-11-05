#if WIDGET_SWITCH_VALIDATION
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using KillConfirmGameBar.Services;
using Microsoft.Graphics.Canvas;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace KillConfirmGameBar.Controls
{
    public sealed partial class KillConfirmAnimation
    {
        internal async Task ValidateCalabiyauRenderingAsync(string output, Grid host, StringBuilder report)
        {
            await ValidateCalabiyauDefaultRenderingAsync(output, line => report.AppendLine(line));
            GameStyleService.Current = GameStyleMode.Calabiyau;
            const string key = "calabiyau_10305201";
            ReleaseAnimationResourcesForPackChange();
            ConfigureIconPack(key);
            var pack = KillConfirmCompatibility.Contracts.CalabiyauPacks.Find(key);
            if (pack == null || await PackCatalogService.GetIconPackAsync(key) == null)
                throw new Exception("Missing isolated Calabiyau rendering fixture.");

            // Reproduce the old one-tier preload against the actual broker and
            // Win2D loader, then time the first previously-unloaded five-kill.
            _calabiyauPackFolder = (await PackCatalogService.GetIconPackAsync(key)).FolderPath;
            await LoadCalabiyauClipAsync("crosshair.json.gz");
            await LoadCalabiyauClipAsync(pack.ClipPath(1), true);
            await LoadCalabiyauEventTexturesAsync();
            int oneTierTextures = CalabiyauTextures.Count;
            var clock = Stopwatch.StartNew();
            await LoadCalabiyauClipAsync(pack.ClipPath(5), true);
            report.AppendLine("OLD first five-kill load: " + clock.ElapsedMilliseconds + " ms; new textures=" + (CalabiyauTextures.Count - oneTierTextures));

            ReleaseAnimationResourcesForPackChange();
            ConfigureIconPack(key);
            clock.Restart();
            await PreloadCurrentPackAnimationsAsync(null);
            int warmedTextures = CalabiyauTextures.Count;
            for (int count = 1; count <= 5; count++)
                if (!CalabiyauClips.ContainsKey(pack.ClipPath(count))) throw new Exception("Tier was not preloaded: " + count);
            string overflowPath = CalabiyauClips[pack.ClipPath(5)].OverkillClip;
            if (overflowPath != null)
            {
                for (int count = 1; count <= 5; count++)
                    if (CalabiyauClips[pack.ClipPath(count)].Textures.Contains("event-count.png"))
                        throw new Exception("Native tier incorrectly includes the over-five count: " + count);
                if (!CalabiyauClips.ContainsKey(overflowPath) || !CalabiyauClips[overflowPath].Textures.Contains("event-count.png"))
                    throw new Exception("Over-five compatibility scene was not preloaded.");
                report.AppendLine("PASS native 1..5 omit the counter; optional over-five scene is cached.");
            }
            report.AppendLine("NEW all-five-tier preload: " + clock.ElapsedMilliseconds + " ms; cached textures=" + warmedTextures);
            clock.Restart();
            PlayCalabiyauKill(5);
            while (!_isCalabiyauActive)
            {
                if (clock.ElapsedMilliseconds > 5000) throw new Exception("Preloaded Calabiyau did not start.");
                await Task.Delay(1);
            }
            long startupMs = clock.ElapsedMilliseconds;
            if (CalabiyauTextures.Count != warmedTextures) throw new Exception("First five-kill still loaded new textures.");
            if (startupMs > 250) throw new Exception("Preloaded five-kill startup took " + startupMs + " ms.");
            report.AppendLine("PASS first five-kill starts in " + startupMs + " ms without loading any new texture.");

            _timer.Stop();
            _playbackClock.Stop();
            var savedTransform = host.RenderTransform;
            host.RenderTransform = new CompositeTransform { ScaleX = .93206534790699, ScaleY = .93206534790699 };
            foreach (double resolution in new[] { 1.0, 2.0 })
            {
                _renderResolutionScale = resolution;
                foreach (var size in new[] { new Size(452, 360), new Size(1000, 900) })
                {
                    host.Width = size.Width; host.Height = size.Height;
                    ApplyViewportSize(768, 768);
                    host.UpdateLayout();
                    await Task.Delay(50);
                    var transform = SpriteCanvas.TransformToVisual(host);
                    Point origin = transform.TransformPoint(new Point(0, 0));
                    Point x = transform.TransformPoint(new Point(SpriteCanvas.ActualWidth, 0));
                    Point y = transform.TransformPoint(new Point(0, SpriteCanvas.ActualHeight));
                    double renderedWidth = Math.Abs(x.X - origin.X), renderedHeight = Math.Abs(y.Y - origin.Y);
                    if (Math.Abs(renderedWidth - 768) > .5 || Math.Abs(renderedHeight - 768) > .5)
                        throw new Exception("Calabiyau canvas was fitted or distorted: " + renderedWidth + "x" + renderedHeight);
                    report.AppendLine("PASS host=" + size.Width + "x" + size.Height + ", resolution=" + resolution + ", displayed canvas=" + renderedWidth + "x" + renderedHeight);
                }
            }
            host.RenderTransform = savedTransform;
            _renderResolutionScale = 1;
            ApplyViewportSize(768, 768);
            foreach (double seconds in new[] { .55, 1.2, 1.8 })
                using (var frame = new CanvasRenderTarget(CanvasDevice.GetSharedDevice(), 768, 768, 96))
                {
                    using (var drawing = frame.CreateDrawingSession())
                    {
                        drawing.Clear(Colors.Transparent);
                        DrawCalabiyauClipEventAt(drawing, _calabiyauClip, seconds, "normal", 5);
                    }
                    if (!frame.GetPixelBytes().Where((value, index) => index % 4 == 3).Any(value => value > 0))
                        throw new Exception("Calabiyau frame is empty.");
                    using (var stream = File.Create(Path.Combine(output, "calabiyau-uwp-" + seconds.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + ".png")))
                        await frame.SaveAsync(stream.AsRandomAccessStream(), CanvasBitmapFileFormat.Png);
                }

            _isCalabiyauActive = false;
            clock.Restart();
            PlayCalabiyauKill(5, publishedUnixMs: (ulong)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - 400));
            while (!_isCalabiyauActive && clock.ElapsedMilliseconds < 5000) await Task.Delay(1);
            if (_calabiyauStartOffsetSeconds < .399 || _calabiyauStartOffsetSeconds > .7)
                throw new Exception("Publication timestamp was not retained: " + _calabiyauStartOffsetSeconds);
            report.AppendLine("PASS published event age is retained (" + _calabiyauStartOffsetSeconds.ToString("0.000") + " s), including asynchronous load time.");
            _isCalabiyauActive = false;
            PlayCalabiyauKill(8);
            clock.Restart();
            while (!_isCalabiyauActive && clock.ElapsedMilliseconds < 5000) await Task.Delay(1);
            var expectedOverflow = CalabiyauClips[overflowPath ?? pack.ClipPath(5)];
            if (!ReferenceEquals(_calabiyauClip, expectedOverflow) || CalabiyauTextures.Count != warmedTextures)
                throw new Exception("Over-five route did not reuse the cached optional scene or legacy tier-five fallback.");
            using (var frame = new CanvasRenderTarget(CanvasDevice.GetSharedDevice(), 768, 768, 96))
            {
                using (var drawing = frame.CreateDrawingSession())
                {
                    drawing.Clear(Colors.Transparent);
                    DrawCalabiyauClipEventAt(drawing, _calabiyauClip, 1.2, "normal", 8);
                }
                using (var stream = File.Create(Path.Combine(output, "calabiyau-uwp-over8.png")))
                    await frame.SaveAsync(stream.AsRandomAccessStream(), CanvasBitmapFileFormat.Png);
            }
            report.AppendLine("PASS eight-kill selects " + (overflowPath ?? "legacy tier-five fallback") + " without new texture loads.");
            _timer.Stop(); _playbackClock.Stop();
            ReleaseAnimationResourcesForPackChange();
        }
    }
}
#endif

using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using KillConfirmCompatibility.Contracts;
using KillConfirmCompatibility.Desktop.Runtime;
using KillConfirmCompatibility.Services;
using Microsoft.Graphics.Canvas;

namespace KillConfirmCompatibility.Controls
{
    public sealed partial class KillConfirmAnimation
    {
        internal static async Task ValidateSanguoshaLz4CacheAsync(string output, string assetsRoot)
        {
            Directory.CreateDirectory(output);
            DesktopStorage.TestDataRoot = Path.Combine(output, "isolated-profile-" + Guid.NewGuid().ToString("N"));
            DesktopStorage.AssetsRoot = Path.GetFullPath(assetsRoot);
            GameStyleService.Current = GameStyleMode.Sanguosha;
            ConfigureIconPack(GameStyleService.SanguoshaPackKey);
            ConfigureRenderSettings(.2, .1);
            var assets = await LoadSanguoshaTierAsync(8);
            var animation = new KillConfirmAnimation(); animation.UseDesktopFrameClock();
            animation.ConfigureAppearance(1.1, .9, .5);
            animation.PrepareSanguoshaPlayback(assets, 0);
            animation._timer.Stop(); animation._playbackClock.Reset();
            const int width = 513, height = 289;
            async Task<double> WaitForFrame()
            {
                var clock = Stopwatch.StartNew();
                while (clock.Elapsed < TimeSpan.FromSeconds(90))
                {
                    if (animation.TryAcquireDesktopSanguoshaFrame(width, height, out var lease) && lease != null)
                    { lease.Dispose(); return clock.Elapsed.TotalMilliseconds; }
                    await Task.Delay(20);
                }
                throw new TimeoutException("Exact local cache did not become ready: " + animation.DesktopSanguoshaPath);
            }
            double coldMs = 0, warmMs = 0; int mismatches = 0;
            try
            {
                coldMs = await WaitForFrame();
                await EnsureSanguoshaPagesAsync(assets);
                using var target = new CanvasRenderTarget(CanvasDevice.GetSharedDevice(), width, height, 96);
                foreach (int frame in new[] { 0, 45, 80, assets.Clip.Frames.Length - 2 })
                {
                    animation._sanguoshaStartOffsetMs = frame * 1000d / assets.Clip.Fps;
                    if (!animation.TryAcquireDesktopSanguoshaFrame(width, height, out var lease) || lease == null)
                        throw new InvalidOperationException("Generated cache stopped serving frames.");
                    using (lease)
                    {
                        using (var drawing = target.CreateDrawingSession()) animation.DrawDesktopFrame(drawing, width, height, cropEmptySpace: true);
                        byte[] reference = target.GetPixelBytes();
                        for (int i = 0; i < reference.Length; i++) if (reference[i] != lease.Bytes[i]) { mismatches++; break; }
                    }
                }
                animation._sanguoshaRenderer?.ReleaseFrameEffectSources();
                assets.Pages = Array.Empty<CanvasBitmap>(); TrimSanguoshaPages();
                animation.ResetDesktopSanguoshaLz4();
                animation._sanguoshaStartOffsetMs = 0;
                warmMs = await WaitForFrame();
                if (assets.Pages.Length != 0) throw new InvalidOperationException("Warm local cache loaded fallback GPU atlases.");
                File.WriteAllText(Path.Combine(output, "sanguosha-lz4-cache-validation.json"), JsonSerializer.Serialize(new
                {
                    Width = width, Height = height, TextureBrightnessBoost = .2, TextureContrastBoost = .1,
                    AppearanceBrightness = 1.1, AppearanceContrast = .9, ExternalOpacity = .5,
                    ComparedFrames = 4, DifferentFrames = mismatches, ColdPreparationMs = coldMs, WarmOpenMs = warmMs,
                    WarmAtlasPages = assets.Pages.Length, Pass = mismatches == 0,
                    Scope = "Unbundled exact dimensions and nonneutral color settings; generated through complete production desktop renderer, then reopened without loading GPU atlas pages."
                }, new JsonSerializerOptions { WriteIndented = true }));
                if (mismatches != 0) throw new InvalidDataException("Generated Sanguosha exact cache changed pixels.");
            }
            finally { animation.ReleaseAnimationResourcesForPackChange(); ConfigureRenderSettings(0, 0); }
        }
    }
}

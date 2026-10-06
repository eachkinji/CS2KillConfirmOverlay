using KillConfirmCompatibility.Danmaku;
using KillConfirmCompatibility.Danmaku.Engine;
using Microsoft.Graphics.Canvas;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace KillConfirmCompatibility.Validation
{
    internal static class DanmakuValidation
    {
        internal static async Task RunAsync(string output)
        {
            await DanmakuRepository.EnsureLoadedAsync();
            await DanmakuEventPoolRepository.EnsureLoadedAsync();
            foreach (DanmakuDisplayArea area in Enum.GetValues(typeof(DanmakuDisplayArea)))
            foreach (double height in new[] { 300.0, 720.0, 1080.0, 2160.0 })
            foreach (double font in new[] { 16.0, 32.0, 48.0 })
            {
                var lanes = DanmakuLaneLayout.Build(area, height, 20, font);
                if (lanes.Count == 0 || lanes.Any(y => y < 0 || y + font + 6 > height)) throw new Exception("Danmaku track is outside the viewport.");
                for (int i = 1; i < lanes.Count; i++)
                    if (lanes[i] - lanes[i - 1] < font + 6) throw new Exception("Dense danmaku tracks overlap at " + height + " / " + font);
                if (area == DanmakuDisplayArea.AvoidCenter && lanes.Any(y => y > height * 0.32 && y < height * 0.68)) throw new Exception("Danmaku entered the protected center area.");
            }
            var previousArea = DanmakuSettingsStore.Area;
            DanmakuSettingsStore.Area = DanmakuDisplayArea.All;
            var overlay = new DanmakuOverlay { Width = 1920, Height = 1080 };
            overlay.RaiseLoaded();
            try
            {
                overlay.TriggerBarrage(5, 3);
                for (int attempt = 0; attempt < 40; attempt++)
                {
                    await Task.Delay(50);
                    overlay.AdvanceDesktopFrame();
                    using var target = new CanvasRenderTarget(CanvasDevice.GetSharedDevice(), 1536, 864, 96);
                    using (var session = target.CreateDrawingSession())
                    {
                        session.Transform = System.Numerics.Matrix3x2.CreateScale(0.8f);
                        overlay.DrawDesktopFrame(session);
                    }
                    byte[] pixels = target.GetPixelBytes();
                    if (attempt < 10 || !Enumerable.Range(0, pixels.Length / 4).Any(index => pixels[index * 4 + 3] != 0)) continue;
                    var occupiedRows = Enumerable.Range(0, 864).Where(y => Enumerable.Range(0, 1536).Any(x => pixels[(y * 1536 + x) * 4 + 3] != 0)).ToArray();
                    if (occupiedRows.Last() - occupiedRows.First() < 432) continue;
                    using var stream = File.Create(Path.Combine(output, "danmaku.png"));
                    await target.SaveAsync(stream.AsRandomAccessStream(), CanvasBitmapFileFormat.Png);
                    return;
                }
                throw new Exception("Desktop danmaku produced no visible text pixels.");
            }
            finally { overlay.RaiseUnloaded(); DanmakuSettingsStore.Area = previousArea; }
        }
    }
}

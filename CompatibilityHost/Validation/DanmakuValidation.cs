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
            var overlay = new DanmakuOverlay { Width = 960, Height = 300 };
            overlay.RaiseLoaded();
            try
            {
                overlay.TriggerBarrage(5, 3);
                for (int attempt = 0; attempt < 40; attempt++)
                {
                    await Task.Delay(50);
                    overlay.AdvanceDesktopFrame();
                    using var target = new CanvasRenderTarget(CanvasDevice.GetSharedDevice(), 960, 300, 96);
                    using (var session = target.CreateDrawingSession()) overlay.DrawDesktopFrame(session);
                    byte[] pixels = target.GetPixelBytes();
                    if (attempt < 10 || !Enumerable.Range(0, pixels.Length / 4).Any(index => pixels[index * 4 + 3] != 0)) continue;
                    using var stream = File.Create(Path.Combine(output, "danmaku.png"));
                    await target.SaveAsync(stream.AsRandomAccessStream(), CanvasBitmapFileFormat.Png);
                    return;
                }
                throw new Exception("Desktop danmaku produced no visible text pixels.");
            }
            finally { overlay.RaiseUnloaded(); }
        }
    }
}

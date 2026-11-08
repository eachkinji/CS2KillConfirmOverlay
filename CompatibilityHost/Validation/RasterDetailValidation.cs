using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Threading.Tasks;
using KillConfirmCompatibility.Contracts;
using KillConfirmCompatibility.Desktop.Runtime;
using Microsoft.Graphics.Canvas;
using Windows.Foundation;
using Windows.UI;

namespace KillConfirmCompatibility.Controls
{
    public sealed partial class KillConfirmAnimation
    {
        internal static async Task ValidateRasterDetailAsync(string output, string assetsRoot)
        {
            Directory.CreateDirectory(output);
            DesktopStorage.TestDataRoot = Path.Combine(output, "isolated-profile");
            DesktopStorage.AssetsRoot = Path.GetFullPath(assetsRoot) + Path.DirectorySeparatorChar;
            var device = CanvasDevice.GetSharedDevice();
            var report = new List<string>();
            var animation = new KillConfirmAnimation();
            animation.UseDesktopFrameClock();
            using var skull = await LoadBitmapFromApplicationUriAsync("ms-appx:///Assets/GameStyles/battlefield2042/killconfirm/textures/NormalSkullSprite.png");
            using var delta = await LoadBitmapFromApplicationUriAsync("ms-appx:///Assets/GameStyles/deltaforce/killconfirm/textures/killicon_df_default.png");
            try
            {
                animation._isBattlefield2042HudActive = true;
                animation.ApplyViewportSize(Battlefield2042FrameWidth, Battlefield2042FrameHeight);
                var item = new Battlefield2042KillIconItem("NormalSkullSprite.png", false, false, 0) { Icon = skull };
                animation._battlefield2042HudState.KillIconItems.Add(item);
                foreach (int edge in new[] { 1280, 1920, 3200, 1280 })
                {
                    AnimationRasterPolicy.Configure(edge);
                    animation.RefreshRasterQuality();
                    float dpi = (float)(96 * edge / Battlefield2042FrameWidth);
                    var cache = item.RenderCache?.BaseSurface ?? throw new Exception("Skull cache missing.");
                    if (Math.Abs(cache.Dpi - dpi) > .1 || cache.SizeInPixels.Width < 64 * edge / Battlefield2042FrameWidth - 1)
                        throw new Exception("Skull cache ignores raster quality: " + edge);
                    using var reference = new CanvasRenderTarget(device, 64, 64, dpi);
                    using (var draw = reference.CreateDrawingSession())
                    {
                        draw.Clear(Colors.Transparent);
                        DrawBattlefield2042TintedImage(draw, skull, new Rect(17, 17, 30, 30), Colors.White, 1);
                    }
                    using var legacy = new CanvasRenderTarget(device, 64, 64, 96);
                    using (var draw = legacy.CreateDrawingSession())
                    {
                        draw.Clear(Colors.Transparent);
                        DrawBattlefield2042TintedImage(draw, skull, new Rect(17, 17, 30, 30), Colors.White, 1);
                    }
                    using var enlargedLegacy = new CanvasRenderTarget(device, 64, 64, dpi);
                    using (var draw = enlargedLegacy.CreateDrawingSession())
                    {
                        draw.Clear(Colors.Transparent);
                        draw.DrawImage(legacy);
                    }
                    double currentError = RasterDifference(cache.GetPixelBytes(), reference.GetPixelBytes());
                    double legacyError = RasterDifference(enlargedLegacy.GetPixelBytes(), reference.GetPixelBytes());
                    if (legacyError <= 0 || currentError >= legacyError * .1)
                        throw new Exception($"Skull cache lost source detail: {edge}: current={currentError}, legacy={legacyError}.");
                    await SaveRasterDetailAsync(cache, Path.Combine(output, "battlefield2042-" + edge + ".png"));
                    await SaveRasterDetailAsync(enlargedLegacy, Path.Combine(output, "battlefield2042-legacy-" + edge + ".png"));
                    report.Add($"PASS: BF2042 {edge}: cache={cache.SizeInPixels.Width}x{cache.SizeInPixels.Height}, DPI={cache.Dpi:F2}; error from direct original texture: new={currentError:F4}, old={legacyError:F4}.");
                }
                foreach (int edge in new[] { 1280, 1920, 3200 })
                {
                    float scale = (float)(edge / DeltaForceFrameWidth);
                    foreach (double size in new[] { DeltaForceBaseIconSize * DeltaForceIconScale, DeltaForceBaseIconSize * DeltaForceIconScale * DeltaForceIconStartScale })
                    {
                        int pixels = (int)Math.Ceiling((size + 8) * scale);
                        using var current = new CanvasRenderTarget(device, pixels, pixels, 96);
                        using var old = new CanvasRenderTarget(device, pixels, pixels, 96);
                        foreach (var entry in new[] { (current, CanvasImageInterpolation.HighQualityCubic), (old, CanvasImageInterpolation.NearestNeighbor) })
                        {
                            using var draw = entry.Item1.CreateDrawingSession();
                            draw.Clear(Colors.Transparent);
                            draw.Transform = Matrix3x2.CreateScale(scale);
                            DrawBattlefieldImageStretch(draw, delta, new Rect(4, 4, size, size), 1, entry.Item2);
                        }
                        byte[] pixelsCurrent = current.GetPixelBytes();
                        int transitions = 0;
                        for (int i = 3; i < pixelsCurrent.Length; i += 4) if (pixelsCurrent[i] > 0 && pixelsCurrent[i] < 255) transitions++;
                        if (transitions == 0 || RasterDifference(pixelsCurrent, old.GetPixelBytes()) <= 0)
                            throw new Exception("Delta Force reduction did not preserve filtered edges.");
                        string suffix = edge + "-" + (int)Math.Round(size);
                        await SaveRasterDetailAsync(current, Path.Combine(output, "deltaforce-" + suffix + ".png"));
                        await SaveRasterDetailAsync(old, Path.Combine(output, "deltaforce-legacy-" + suffix + ".png"));
                        report.Add($"PASS: Delta {suffix}: original {delta.SizeInPixels.Width}px texture, {pixels}px output, {transitions} antialiased edge pixels.");
                    }
                }
                File.WriteAllText(Path.Combine(output, "pass.txt"), string.Join(Environment.NewLine, report));
            }
            finally { animation.ResetBattlefield2042HudState(); AnimationRasterPolicy.Configure(AnimationRasterPolicy.DefaultEdge); }
        }

        private static double RasterDifference(byte[] left, byte[] right)
        {
            if (left.Length != right.Length) throw new Exception("Raster dimensions differ.");
            double sum = 0;
            for (int i = 0; i < left.Length; i++) sum += Math.Abs(left[i] - right[i]);
            return sum / left.Length;
        }

        private static async Task SaveRasterDetailAsync(CanvasRenderTarget target, string path)
        {
            using var file = File.Create(path);
            await target.SaveAsync(file.AsRandomAccessStream(), CanvasBitmapFileFormat.Png);
        }
    }
}

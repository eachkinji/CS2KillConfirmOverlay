using KillConfirmCompatibility.Contracts;
using KillConfirmCompatibility.Desktop.Runtime;
using KillConfirmCompatibility.Desktop.Windowing;
using KillConfirmCompatibility.Services;
using Microsoft.Graphics.Canvas;
using System;
using System.IO;
using System.Threading.Tasks;

namespace KillConfirmCompatibility.Controls
{
    public sealed partial class KillConfirmAnimation
    {
        internal static async Task ValidateAnimationReplacementAsync(string output, string assetsRoot)
        {
            Directory.CreateDirectory(output);
            DesktopStorage.TestDataRoot = Path.Combine(output, "isolated-profile");
            DesktopStorage.AssetsRoot = Path.GetFullPath(assetsRoot) + Path.DirectorySeparatorChar;
            GameStyleService.Current = GameStyleMode.Crossfire;
            ConfigureIconPack("default");
            ConfigureMainAnimationStyle(1);
            var animation = new KillConfirmAnimation();
            animation.UseDesktopFrameClock();
            using var red = CanvasBitmap.CreateFromBytes(CanvasDevice.GetSharedDevice(), new byte[] { 0, 0, 255, 255 }, 1, 1,
                Windows.Graphics.DirectX.DirectXPixelFormat.B8G8R8A8UIntNormalized);
            using var blue = CanvasBitmap.CreateFromBytes(CanvasDevice.GetSharedDevice(), new byte[] { 255, 0, 0, 255 }, 1, 1,
                Windows.Graphics.DirectX.DirectXPixelFormat.B8G8R8A8UIntNormalized);
            var first = CreateCodeKillAnimationAsset(new Code2KillAsset(red, null, null, null) { Action = "multi1" });
            var second = CreateCodeKillAnimationAsset(new Code2KillAsset(blue, null, null, null)
                { Action = "multi2", Sequence = new CanvasBitmap[10] });
            // Solid colors make a single surviving old pixel unambiguous. Seed
            // the normal preparation path without depending on installed packs.
            foreach (var request in GetCodeKillPreloadRequests())
                CodeKillCache[GetCodeKillCacheKey(request.Item1, request.Item2)] = first.CodeAsset;
            await animation.PreloadCurrentPackAnimationsAsync(null);
            using var surface = new OverlaySurface("Lower", "Replacement validation", animation);
            var screen = Array.Find(NativeWindows.Screens(), monitor => (monitor.Flags & 1) != 0).Monitor;
            var layout = new ElementLayout { X = 0.5, Y = 0.5, Scale = 1, Visible = true };
            try
            {
                foreach (bool failOldLoad in new[] { false, true })
                {
                    animation.PlayInternal(_ => Task.FromResult(first), false, first);
                    animation._currentFrame = 20;
                    animation.SpriteCanvas.Invalidate();
                    surface.Place(screen, layout, true, false);
                    surface.Render();
                    ValidateReplacementPixels(animation, requireBlue: false, requireEmpty: false);

                    var obsolete = new TaskCompletionSource<AnimationAsset>(TaskCreationOptions.RunContinuationsAsynchronously);
                    animation.PlayInternal(_ => obsolete.Task, false);
                    ValidateReplacementPixels(animation, requireBlue: false, requireEmpty: true);
                    surface.Render();
                    if (surface.IsPresentationVisible) throw new Exception("Native window retained the old frame while its replacement waited.");

                    animation.PlayInternal(_ => Task.FromResult(second), false, second);
                    animation._currentFrame = 20;
                    animation.SpriteCanvas.Invalidate();
                    surface.Place(screen, layout, true, false);
                    surface.Render();
                    ValidateReplacementPixels(animation, requireBlue: true, requireEmpty: false);
                    surface.ValidateCroppedPlacement();
                    surface.SavePreviewImage(Path.Combine(output, failOldLoad ? "replacement-after-failure.png" : "replacement-after-success.png"));
                    if (failOldLoad) obsolete.SetException(new IOException("Deliberately failed superseded load"));
                    else obsolete.SetResult(first);
                    await Task.Delay(30);
                    if (!ReferenceEquals(animation._currentCodeAsset, second.CodeAsset)
                        || animation.Visibility != Windows.UI.Xaml.Visibility.Visible || !animation._timer.IsEnabled)
                        throw new Exception("Old completion changed the replacement playback.");
                    ValidateReplacementPixels(animation, requireBlue: true, requireEmpty: false);
                    surface.Render();
                    if (!surface.IsPresentationVisible) throw new Exception("Superseded failure hid the new native frame.");
                    surface.ValidateCroppedPlacement();
                }
                File.WriteAllText(Path.Combine(output, "pass.txt"),
                    "PASS: actual Win2D frames clear immediately while awaiting replacement; changing viewport dimensions contains only new pixels; obsolete success/failure never restores or hides content; native cropped windows hide and atomically present the new frame.");
            }
            finally { animation.BeginReplacementPlayback(); }
        }

        private static void ValidateReplacementPixels(KillConfirmAnimation animation, bool requireBlue, bool requireEmpty)
        {
            using var target = new CanvasRenderTarget(CanvasDevice.GetSharedDevice(), 700, 500, 96);
            using (var drawing = target.CreateDrawingSession()) animation.DrawDesktopFrame(drawing, 700, 500);
            byte[] pixels = target.GetPixelBytes();
            int visible = 0;
            for (int i = 0; i < pixels.Length; i += 4)
            {
                if (pixels[i + 3] == 0) continue;
                visible++;
                if (requireEmpty) throw new Exception("Old frame pixels remained after starting a replacement.");
                if (requireBlue && pixels[i + 2] != 0) throw new Exception("Old red pixels remained beside the new blue frame.");
            }
            if (!requireEmpty && visible == 0) throw new Exception("Replacement pixel fixture drew no visible content.");
        }
    }
}

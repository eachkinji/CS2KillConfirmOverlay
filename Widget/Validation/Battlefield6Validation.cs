#if WIDGET_SWITCH_VALIDATION
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Graphics.Canvas;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Xaml;

namespace KillConfirmGameBar.Controls
{
    public sealed partial class KillConfirmAnimation
    {
        // Exercises the actual Win2D assets, public playback and real-time state machine.
        // The isolated validation package calls this on its loaded lower animation.
        internal async Task ValidateBattlefield6Async(string output)
        {
            Directory.CreateDirectory(output);
            var report = new StringBuilder();
            try
            {
                ReleaseAnimationResourcesForPackChange();
                ConfigureIconPack("battlefield6");
                CanvasBitmap normal = await LoadBattlefield6IconAsync("killicon_battlefield6_default.png");
                CanvasBitmap headshot = await LoadBattlefield6IconAsync("killicon_battlefield6_headshot.png");
                CanvasBitmap assist = await LoadBattlefield6IconAsync("killicon_battlefield6_assist.png");
                RequireBattlefield6(normal != null && headshot != null && assist != null,
                    "Normal, headshot or assist bitmap failed to load through the real resource loader.");
                foreach (CanvasBitmap bitmap in new[] { normal, headshot, assist })
                    RequireBattlefield6(bitmap.SizeInPixels.Width > 0 && bitmap.SizeInPixels.Height > 0,
                        "A Battlefield 6 bitmap decoded without pixels.");

                PlayBattlefield6Kill(1, true, false, false, false, "BF6 HEADSHOT", "rifle", 300, "kill", 1, 0);
                await WaitForBattlefield6Async(() => _battlefield6HudState.Icons.Any(icon => ReferenceEquals(icon.Bitmap, headshot)),
                    "Headshot was not admitted", 3000);
                await Task.Delay(350);
                RequireBattlefield6(_battlefield6HudState.Icons.Any(icon => icon.Headshot), "Headshot flag was lost.");
                double displayWidth = _displayViewportWidth, displayHeight = _displayViewportHeight;
                ValidateBattlefield6Viewport(displayWidth, displayHeight);
                await CaptureBattlefield6Async(output, "battlefield6-headshot.png", true);
                report.AppendLine("PASS: real headshot asset, orange feedback and Win2D frame rendered.");

                PlayBattlefield6Kill(2, false, false, false, false, "BF6 NORMAL", "rifle", 300, "kill", 1, 0);
                await WaitForBattlefield6Async(() => _battlefield6HudState.Icons.Any(icon => ReferenceEquals(icon.Bitmap, normal)),
                    "Normal kill used the wrong bitmap", 3000);
                await Task.Delay(150);
                using (var frame = RenderBattlefield6ValidationFrame())
                    RequireBattlefield6(CountBattlefield6VisiblePixels(frame) > 200, "Normal kill frame was blank.");

                PlayBattlefield6Kill(2, false, false, false, true, "BF6 ASSIST", "rifle", 50, "assist", 1, 0);
                await WaitForBattlefield6Async(() => _battlefield6HudState.Icons.Any(icon => ReferenceEquals(icon.Bitmap, assist)),
                    "Assist used the wrong bitmap", 3000);
                RequireBattlefield6(_battlefield6HudState.Bonuses.Any(bonus => bonus.Label == "助攻"),
                    "Assist bonus text was not routed.");
                await Task.Delay(150);
                using (var frame = RenderBattlefield6ValidationFrame())
                    RequireBattlefield6(CountBattlefield6VisiblePixels(frame) > 200, "Assist frame was blank.");
                report.AppendLine("PASS: normal and assist events use their own decoded icons and render nonempty frames.");

                for (int i = 0; i < 8; i++)
                    PlayBattlefield6Kill(i + 3, i % 3 == 0, false, false, i % 3 == 2,
                        "BF6 TARGET " + i, "rifle", 100, i % 3 == 2 ? "assist" : "kill", 1, 0);
                var lastKill = Stopwatch.StartNew();
                int maximumVisible = 0;
                while (_battlefield6HudState.Pending.Count > 0)
                {
                    ValidateBattlefield6Queue();
                    ValidateBattlefield6Viewport(displayWidth, displayHeight);
                    maximumVisible = Math.Max(maximumVisible, _battlefield6HudState.Icons.Count);
                    RequireBattlefield6(lastKill.ElapsedMilliseconds < 1800, "Eight-event burst did not drain promptly.");
                    await Task.Delay(20);
                }
                long drainMs = lastKill.ElapsedMilliseconds;
                double lastAdmissionMs = _battlefield6HudState.LastAdmissionMs;
                RequireBattlefield6(lastAdmissionMs >= 0, "Burst had no admitted event timestamp.");
                await Task.Delay(300);
                ValidateBattlefield6Queue();
                ValidateBattlefield6Viewport(displayWidth, displayHeight);
                await CaptureBattlefield6Async(output, "battlefield6-burst.png", false);
                while (_isBattlefield6HudActive)
                {
                    ValidateBattlefield6Queue();
                    ValidateBattlefield6Viewport(displayWidth, displayHeight);
                    maximumVisible = Math.Max(maximumVisible, _battlefield6HudState.Icons.Count);
                    RequireBattlefield6(_playbackClock.Elapsed.TotalMilliseconds - lastAdmissionMs <= 4250,
                        "Playback exceeded the authored four-second exit plus 250 ms frame grace after the final admitted event.");
                    await Task.Delay(20);
                }
                RequireBattlefield6(!_timer.IsEnabled && !_playbackClock.IsRunning && Visibility == Visibility.Collapsed,
                    "Natural completion left an active timer or visible empty control.");
                double finishMs = _playbackClock.Elapsed.TotalMilliseconds - lastAdmissionMs;
                RequireBattlefield6(finishMs <= 4250, "The final admitted event exceeded its authored exit duration.");
                report.AppendLine("PASS: 8-event burst, five steady icons plus at most two retiring; peak=" + maximumVisible
                    + "; fixed logical/selection/display viewport; drain=" + drainMs
                    + " ms; final admission=" + Math.Round(lastAdmissionMs)
                    + " ms; natural exit after final admission=" + Math.Round(finishMs) + " ms.");

                // Clear caches first so release races an actual asynchronous decode rather than a completed cache hit.
                ReleaseAnimationResourcesForPackChange();
                Task<CanvasBitmap> pending = LoadBattlefield6IconAsync("killicon_battlefield6_medic.png");
                RequireBattlefield6(!pending.IsCompleted, "Resource-release test did not reach an in-flight real load.");
                PlayBattlefield6Kill(1, false, false, false, false, "BF6 RESCUE", "", 100, "hostage_rescue", 2, 0);
                int generation = _resourceGeneration;
                ReleaseAnimationResourcesForPackChange();
                RequireBattlefield6(await Task.WhenAny(pending, Task.Delay(5000)) == pending, "Invalidated bitmap load did not finish.");
                RequireBattlefield6(await pending == null, "Invalidated load returned a disposed/stale bitmap.");
                await Task.Delay(100);
                RequireBattlefield6(_resourceGeneration > generation && !_isBattlefield6HudActive
                    && _battlefield6HudState.Pending.Count == 0 && _battlefield6HudState.Icons.Count == 0
                    && !_timer.IsEnabled && !_playbackClock.IsRunning && Visibility == Visibility.Collapsed,
                    "The asynchronous continuation revived released Battlefield 6 playback.");
                lock (Battlefield6IconCache)
                    RequireBattlefield6(Battlefield6IconCache.Count == 0 && Battlefield6IconLoads.Count == 0,
                        "An invalidated resource load repopulated cleared caches.");
                report.AppendLine("PASS: release during a real asynchronous load cannot revive playback or repopulate stale assets.");
                File.WriteAllText(Path.Combine(output, "battlefield6-pass.txt"), report.ToString());
            }
            catch (Exception error)
            {
                File.WriteAllText(Path.Combine(output, "battlefield6-failure.txt"), report + "\n" + error + "\n" + DescribeBattlefield6ValidationState());
                throw;
            }
            finally { ReleaseAnimationResourcesForPackChange(); }
        }

        private string DescribeBattlefield6ValidationState()
        {
            var state = _battlefield6HudState;
            return "clockMs=" + _playbackClock.ElapsedMilliseconds + "; running=" + _playbackClock.IsRunning
                + "; active=" + _isBattlefield6HudActive + "; timer=" + _timer.IsEnabled
                + "; visibility=" + Visibility + "; generation=" + _resourceGeneration
                + "; pending=" + state.Pending.Count + "; ready=" + state.Pending.Count(item => item.Ready)
                + "; icons=" + state.Icons.Count + "; retiring=" + state.Icons.Count(icon => icon.ForcedFadeMs >= 0)
                + "; bonuses=" + state.Bonuses.Count + "; lastAdmissionMs=" + state.LastAdmissionMs
                + "; lastUpdateMs=" + state.LastUpdateMs + "; scoreUpdatedMs=" + state.ScoreUpdatedMs
                + "; nextBonusFadeMs=" + state.NextBonusFadeMs + "; victimStartMs=" + state.VictimStartMs;
        }

        private void ValidateBattlefield6Viewport(double displayWidth, double displayHeight)
        {
            RequireBattlefield6(_logicalFrameWidth == Battlefield6FrameWidth && _logicalFrameHeight == Battlefield6FrameHeight
                && Battlefield6SelectionViewportWidth == Battlefield6FrameWidth
                && Battlefield6SelectionViewportHeight == Battlefield6FrameHeight
                && Battlefield6SelectionViewportCenterOffsetX == 0 && Battlefield6SelectionViewportCenterOffsetY == 0
                && Math.Abs(_displayViewportWidth - displayWidth) < 0.001
                && Math.Abs(_displayViewportHeight - displayHeight) < 0.001,
                "Battlefield 6 changed its viewport during entry, queue movement or exit.");
        }

        private void ValidateBattlefield6Queue()
        {
            RequireBattlefield6(_battlefield6HudState.Icons.Count(icon => icon.ForcedFadeMs < 0) <= 5
                && _battlefield6HudState.Icons.Count <= 7 && _battlefield6HudState.Pending.Count <= 30,
                "Battlefield 6 exceeded the five-icon queue plus its two-icon exit allowance.");
        }

        private async Task WaitForBattlefield6Async(Func<bool> ready, string message, int timeoutMs)
        {
            var clock = Stopwatch.StartNew();
            while (!ready())
            {
                RequireBattlefield6(clock.ElapsedMilliseconds < timeoutMs, message);
                await Task.Delay(20);
            }
        }

        private CanvasRenderTarget RenderBattlefield6ValidationFrame()
        {
            var frame = new CanvasRenderTarget(CanvasDevice.GetSharedDevice(),
                (float)Battlefield6FrameWidth, (float)Battlefield6FrameHeight, 96);
            using (CanvasDrawingSession drawing = frame.CreateDrawingSession())
            {
                drawing.Clear(Colors.Transparent);
                DrawBattlefield6HudFrame(drawing);
            }
            return frame;
        }

        private static int CountBattlefield6VisiblePixels(CanvasRenderTarget frame)
        {
            byte[] pixels = frame.GetPixelBytes();
            int count = 0;
            for (int i = 3; i < pixels.Length; i += 4) if (pixels[i] > 32) count++;
            return count;
        }

        private async Task CaptureBattlefield6Async(string output, string name, bool requireOrange)
        {
            using (CanvasRenderTarget frame = RenderBattlefield6ValidationFrame())
            {
                RequireBattlefield6(CountBattlefield6VisiblePixels(frame) > 200, "Captured Battlefield 6 frame was blank.");
                if (requireOrange)
                {
                    byte[] pixels = frame.GetPixelBytes();
                    int orange = 0;
                    for (int i = 0; i < pixels.Length; i += 4)
                        if (pixels[i + 3] > 64 && pixels[i + 2] > pixels[i + 1] + 15 && pixels[i + 1] > pixels[i] + 10) orange++;
                    RequireBattlefield6(orange > 5, "Headshot frame lost its orange feedback.");
                }
                StorageFolder folder = await StorageFolder.GetFolderFromPathAsync(output);
                StorageFile file = await folder.CreateFileAsync(name, CreationCollisionOption.ReplaceExisting);
                using (var stream = await file.OpenAsync(FileAccessMode.ReadWrite))
                    await frame.SaveAsync(stream, CanvasBitmapFileFormat.Png);
            }
        }

        private static void RequireBattlefield6(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
#endif

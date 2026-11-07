#if SANGUOSHA_LZ4_VALIDATION
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Security.Cryptography;
using System.Threading.Tasks;
using KillConfirmGameBar.Services;
using Microsoft.Graphics.Canvas;
using Windows.Data.Json;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace KillConfirmGameBar.Controls
{
    public sealed partial class KillConfirmAnimation
    {
        private bool _sanguoshaValidationLoop;
        internal static async Task RunSanguoshaLz4ValidationAsync(string arguments)
        {
            string output = Path.Combine(ApplicationData.Current.LocalFolder.Path, "SGS-LZ4-test"); Directory.CreateDirectory(output);
            KillConfirmAnimation animation = null;
            try
            {
                File.WriteAllText(Path.Combine(output, "phase.txt"), "initialize");
                Window.Current.Content = new Grid(); Window.Current.Activate();
                await SharedRuntime.InitializeValidationAsync();
                GameStyleService.Current = GameStyleMode.Sanguosha;
                animation = new KillConfirmAnimation();
                var host = new Grid(); host.Children.Add(animation); Window.Current.Content = host; Window.Current.Activate();
                await Task.Delay(250);
                ConfigureIconPack("sanguosha"); await animation.PreloadCurrentPackAnimationsAsync(null);
                var profile = animation.GetRequestedSanguoshaRasterProfile();
                var summary = new JsonObject {
                    ["profile"] = JsonValue.CreateStringValue(profile.Id), ["signature"] = JsonValue.CreateStringValue(profile.Signature),
                    ["dpi"] = JsonValue.CreateNumberValue(profile.Dpi), ["width"] = JsonValue.CreateNumberValue(profile.Width), ["height"] = JsonValue.CreateNumberValue(profile.Height),
                    ["scale"] = JsonValue.CreateNumberValue(profile.Scale), ["appMemoryAtMetadataReady"] = JsonValue.CreateNumberValue(Windows.System.MemoryManager.AppMemoryUsage)
                };
                File.WriteAllText(Path.Combine(output, "environment.json"), summary.Stringify());
                if ((arguments ?? "").IndexOf("pixels", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    await animation.ValidateSanguoshaLz4PixelsAsync(output, profile, summary); return;
                }
                animation.PlaySanguoshaKill(8);
                var wait = Stopwatch.StartNew();
                while (!animation._isSanguoshaActive && wait.Elapsed.TotalSeconds < 20) await Task.Delay(10);
                if (!animation._isSanguoshaActive || !animation._sanguoshaRoute.StartsWith("lz4:", StringComparison.Ordinal))
                    throw new InvalidOperationException("Performance run must use LZ4, actual=" + animation._sanguoshaRoute + "; " + animation._sanguoshaFallbackReason);
                animation._sanguoshaValidationLoop = true;
                if (animation._sanguoshaAnimatedSurface == null) throw new InvalidOperationException("Timed SGS playback did not create CanvasAnimatedControl.");
                animation._sanguoshaAnimatedSurface.Loop = true;
                bool staticFrame = (arguments ?? "").IndexOf("static", StringComparison.OrdinalIgnoreCase) >= 0;
                animation._sanguoshaAnimatedSurface.StaticFrame = staticFrame;
                var timestamps = new List<double>(); var indices = new List<int>(); var routes = new HashSet<string>();
                var uploads = new List<bool>(); var workTimes = new List<double>(); var uploadTimes = new List<double>();
                var diagnostic = Windows.System.Diagnostics.ProcessDiagnosticInfo.GetForCurrentProcess();
                var cpuBefore = diagnostic.CpuUsage.GetReport();
                var clock = Stopwatch.StartNew();
                animation._sanguoshaAnimatedSurface.FrameDrawn += (frame, uploaded, workMs, uploadMs) => {
                    timestamps.Add(clock.Elapsed.TotalMilliseconds); indices.Add(frame); routes.Add(animation._sanguoshaRoute);
                    uploads.Add(uploaded); workTimes.Add(workMs); uploadTimes.Add(uploadMs);
                };
                File.WriteAllText(Path.Combine(output, "phase.txt"), "steady-lz4");
                await Task.Delay(TimeSpan.FromSeconds((arguments ?? "").IndexOf("quick", StringComparison.OrdinalIgnoreCase) >= 0 ? 8 : 32));
                animation._timer.Stop(); animation.StopSanguoshaFrameClock(); animation._sanguoshaValidationLoop = false;
                var cpuAfter = diagnostic.CpuUsage.GetReport(); double wallSeconds = clock.Elapsed.TotalSeconds;
                double[] intervals = timestamps.Skip(1).Select((t, i) => t - timestamps[i]).OrderBy(x => x).ToArray();
                summary["route"] = JsonValue.CreateStringValue(animation._sanguoshaRoute);
                summary["presentation"] = JsonValue.CreateStringValue("CanvasAnimatedControl variable-step vsync Draw; 60 Hz content selection; exact-DPI LZ4 bitmap; Debug Native=false validation build");
                summary["fallbackReason"] = JsonValue.CreateStringValue(animation._sanguoshaFallbackReason);
                summary["staticFrameDiagnostic"] = JsonValue.CreateBooleanValue(staticFrame);
                summary["actualCanvasDrawCallbacks"] = JsonValue.CreateNumberValue(timestamps.Count);
                summary["observedCallbackFps"] = JsonValue.CreateNumberValue(timestamps.Count < 2 ? 0 : (timestamps.Count - 1) * 1000 / (timestamps.Last() - timestamps[0]));
                summary["intervalP99Ms"] = JsonValue.CreateNumberValue(intervals.Length == 0 ? 0 : intervals[(int)((intervals.Length - 1) * .99)]);
                summary["intervalMaxMs"] = JsonValue.CreateNumberValue(intervals.Length == 0 ? 0 : intervals.Last());
                summary["intervalsOver25Ms"] = JsonValue.CreateNumberValue(intervals.Count(x => x > 25));
                var contentTimes = new List<double>(); int duplicates = 0, skipped = 0;
                int frameCount = animation._sanguoshaLz4Playback.Source.FrameCount;
                for (int i = 0; i < indices.Count; i++)
                {
                    if (i == 0 || indices[i] != indices[i - 1]) contentTimes.Add(timestamps[i]); else duplicates++;
                    if (i > 0) { int step = (indices[i] - indices[i - 1] + frameCount) % frameCount; if (step > 1) skipped += step - 1; }
                }
                double[] contentIntervals = contentTimes.Skip(1).Select((t, i) => t - contentTimes[i]).OrderBy(x => x).ToArray();
                summary["contentFrames"] = JsonValue.CreateNumberValue(contentTimes.Count);
                summary["contentFps"] = JsonValue.CreateNumberValue(contentTimes.Count < 2 ? 0 : (contentTimes.Count - 1) * 1000 / (contentTimes.Last() - contentTimes[0]));
                summary["duplicateDraws"] = JsonValue.CreateNumberValue(duplicates); summary["skippedContentFrames"] = JsonValue.CreateNumberValue(skipped);
                summary["contentIntervalP99Ms"] = JsonValue.CreateNumberValue(SanguoshaPercentile(contentIntervals, .99));
                summary["contentIntervalMaxMs"] = JsonValue.CreateNumberValue(contentIntervals.Length == 0 ? 0 : contentIntervals.Last());
                summary["contentIntervalsOver25Ms"] = JsonValue.CreateNumberValue(contentIntervals.Count(x => x > 25));
                summary["uploadCount"] = JsonValue.CreateNumberValue(uploads.Count(x => x));
                summary["drawWorkP50Ms"] = JsonValue.CreateNumberValue(SanguoshaPercentile(workTimes, .5));
                summary["drawWorkP99Ms"] = JsonValue.CreateNumberValue(SanguoshaPercentile(workTimes, .99));
                summary["uploadP50Ms"] = JsonValue.CreateNumberValue(SanguoshaPercentile(uploadTimes.Where((t, i) => uploads[i]), .5));
                summary["uploadP99Ms"] = JsonValue.CreateNumberValue(SanguoshaPercentile(uploadTimes.Where((t, i) => uploads[i]), .99));
                summary["appMemoryAfterPlayback"] = JsonValue.CreateNumberValue(Windows.System.MemoryManager.AppMemoryUsage);
                summary["allDrawsLz4"] = JsonValue.CreateBooleanValue(routes.All(x => x.StartsWith("lz4:", StringComparison.Ordinal)));
                summary["cpuBeforeUserTicks"] = JsonValue.CreateNumberValue(cpuBefore.UserTime.Ticks);
                summary["cpuBeforeKernelTicks"] = JsonValue.CreateNumberValue(cpuBefore.KernelTime.Ticks);
                summary["cpuAfterUserTicks"] = JsonValue.CreateNumberValue(cpuAfter.UserTime.Ticks);
                summary["cpuAfterKernelTicks"] = JsonValue.CreateNumberValue(cpuAfter.KernelTime.Ticks);
                double cpuSeconds = ((cpuAfter.UserTime - cpuBefore.UserTime) + (cpuAfter.KernelTime - cpuBefore.KernelTime)).TotalSeconds;
                summary["cpuSeconds"] = JsonValue.CreateNumberValue(cpuSeconds); summary["wallSeconds"] = JsonValue.CreateNumberValue(wallSeconds);
                summary["averageLogicalCores"] = JsonValue.CreateNumberValue(cpuSeconds / wallSeconds);
                var rows = new JsonArray(); for (int i = 0; i < timestamps.Count; i++) rows.Add(new JsonObject {
                    ["ms"] = JsonValue.CreateNumberValue(timestamps[i]), ["frame"] = JsonValue.CreateNumberValue(indices[i]),
                    ["uploaded"] = JsonValue.CreateBooleanValue(uploads[i]), ["drawWorkMs"] = JsonValue.CreateNumberValue(workTimes[i]), ["uploadMs"] = JsonValue.CreateNumberValue(uploadTimes[i]) });
                File.WriteAllText(Path.Combine(output, "frames.json"), rows.Stringify());
                File.WriteAllText(Path.Combine(output, "performance.json"), summary.Stringify());
                File.WriteAllText(Path.Combine(output, "phase.txt"), "complete");
            }
            catch (Exception error) { File.WriteAllText(Path.Combine(output, "failure.txt"), error.ToString()); }
            finally { animation?.ReleaseAnimationResourcesForPackChange(); }
        }
        private static double SanguoshaPercentile(IEnumerable<double> values, double percentile)
        {
            double[] sorted = values.OrderBy(x => x).ToArray(); return sorted.Length == 0 ? 0 : sorted[(int)((sorted.Length - 1) * percentile)];
        }
        private async Task ValidateSanguoshaLz4PixelsAsync(string output, SanguoshaRasterProfile profile, JsonObject summary)
        {
            var assets = await LoadSanguoshaTierAsync(8); await EnsureSanguoshaPagesAsync(assets);
            int token = ++_playToken; if (!await TryPrepareSanguoshaLz4Async(assets, profile, token, _resourceGeneration)) throw new InvalidOperationException(_sanguoshaFallbackReason);
            // Preparing the LZ4 route released fallback pages; load reference pages
            // only in this separate validation invocation, never in the timed run.
            await EnsureSanguoshaPagesAsync(assets);
            PrepareSanguoshaPlayback(assets, 0); _timer.Stop(); StopSanguoshaFrameClock(); _playbackClock.Reset();
            var device = CanvasDevice.GetSharedDevice(); int mismatches = 0; long differentBytes = 0;
            int frameSelectionMismatches = 0, gpuCopyMismatchFrames = 0, sourceReferenceMismatchFrames = 0;
            var detail = new JsonArray();
            using (var renderer = new SanguoshaFrameRenderer())
            using (var reference = CreateSanguoshaRasterTarget(device, profile))
            using (var copied = CreateSanguoshaRasterTarget(device, profile))
            using (var sha = SHA256.Create())
            {
                var expected = new byte[checked(profile.Width * profile.Height * 4)]; var actual = new byte[expected.Length];
                var expectedBuffer = expected.AsBuffer(); var actualBuffer = actual.AsBuffer();
                for (int frame = 0; frame < assets.Clip.Frames.Length - 1; frame++)
                {
                    double ms = frame * 1000d / assets.Clip.Fps;
                    using (var ds = reference.CreateDrawingSession())
                    {
                        ds.Clear(Windows.UI.Colors.Transparent);
                        renderer.RenderDirectAtTime(ds, assets.Clip, assets.Pages, TimeSpan.FromTicks((long)(ms * TimeSpan.TicksPerMillisecond)), Matrix3x2.CreateScale(profile.LogicalScale));
                    }
                    _sanguoshaStartOffsetMs = ms;
                    using (var ds = copied.CreateDrawingSession()) { ds.Clear(Windows.UI.Colors.Transparent); _sanguoshaAnimatedSurface.DrawExactFrame(ds, frame); }
                    reference.GetPixelBytes(expectedBuffer); copied.GetPixelBytes(actualBuffer);
                    int uploadedFrame = _sanguoshaAnimatedSurface.UploadedFrame;
                    if (uploadedFrame != frame) frameSelectionMismatches++;
                    bool copyMismatch, sourceMismatch;
                    using (var lease = _sanguoshaLz4Playback.AcquireFrame(frame))
                    {
                        byte[] sourceHash = sha.ComputeHash(lease.Bytes);
                        copyMismatch = !sourceHash.SequenceEqual(sha.ComputeHash(actual));
                        sourceMismatch = !sourceHash.SequenceEqual(sha.ComputeHash(expected));
                    }
                    if (copyMismatch) gpuCopyMismatchFrames++; if (sourceMismatch) sourceReferenceMismatchFrames++;
                    detail.Add(new JsonObject { ["frame"] = JsonValue.CreateNumberValue(frame), ["uploadedFrame"] = JsonValue.CreateNumberValue(uploadedFrame),
                        ["decodedVsGpuMismatch"] = JsonValue.CreateBooleanValue(copyMismatch), ["decodedVsReferenceMismatch"] = JsonValue.CreateBooleanValue(sourceMismatch) });
                    if (!sha.ComputeHash(actual).SequenceEqual(sha.ComputeHash(expected)))
                    {
                        mismatches++; for (int i = 0; i < actual.Length; i++) if (actual[i] != expected[i]) differentBytes++;
                    }
                }
            }
            summary["route"] = JsonValue.CreateStringValue(_sanguoshaRoute); summary["comparedFrames"] = JsonValue.CreateNumberValue(assets.Clip.Frames.Length - 1);
            summary["mismatchFrames"] = JsonValue.CreateNumberValue(mismatches); summary["differentBytes"] = JsonValue.CreateNumberValue(differentBytes);
            summary["pass"] = JsonValue.CreateBooleanValue(mismatches == 0);
            summary["frameSelectionMismatches"] = JsonValue.CreateNumberValue(frameSelectionMismatches);
            summary["gpuCopyMismatchFrames"] = JsonValue.CreateNumberValue(gpuCopyMismatchFrames);
            summary["sourceReferenceMismatchFrames"] = JsonValue.CreateNumberValue(sourceReferenceMismatchFrames);
            File.WriteAllText(Path.Combine(output, "pixel-detail.json"), detail.Stringify());
            File.WriteAllText(Path.Combine(output, "pixels.json"), summary.Stringify()); File.WriteAllText(Path.Combine(output, "phase.txt"), "complete");
        }
    }
}
#endif

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using KillConfirmCompatibility.Contracts;
using KillConfirmCompatibility.Desktop.Rendering;
using KillConfirmCompatibility.Desktop.Runtime;
using KillConfirmCompatibility.Desktop.Windowing;
using KillConfirmCompatibility.Services;
using KillConfirmCompatibility.Validation;
using Microsoft.Graphics.Canvas;

namespace KillConfirmCompatibility.Controls
{
    public sealed partial class KillConfirmAnimation
    {
        internal static async Task MeasureSanguoshaLz4Async(string output, string assetsRoot, int tier)
        {
            Directory.CreateDirectory(output);
            DesktopStorage.TestDataRoot = Path.Combine(output, "isolated-profile");
            DesktopStorage.AssetsRoot = Path.GetFullPath(assetsRoot);
            GameStyleService.Current = GameStyleMode.Sanguosha;
            ConfigureIconPack(GameStyleService.SanguoshaPackKey);
            ConfigureRenderSettings(0, 0);
            await PreloadSanguoshaAsync(null);
            var assets = await LoadSanguoshaTierAsync(tier);
            var animation = new KillConfirmAnimation(); animation.UseDesktopFrameClock();
            using var surface = new OverlaySurface("Lower", "三国杀 LZ4 正式路径验证", animation);
            var screen = NativeWindows.Screens().OrderByDescending(item => item.Flags & 1).First().Monitor;
            var layout = new ElementLayout { X = .5, Y = .5, Scale = 1, Visible = true };
            using var process = Process.GetCurrentProcess();
            using var probe = new GpuMemoryProbe(CanvasDevice.GetSharedDevice());
            GpuMemoryProbe.AdapterMemory Sample()
            {
                var memory = probe.Capture().Single(item => item.IsRendererAdapter);
                if (memory.Error != null || !memory.LocalUsageBytes.HasValue || !memory.NonLocalUsageBytes.HasValue)
                    throw new InvalidOperationException("Cannot measure renderer-adapter GPU memory: " + memory.Error);
                return memory;
            }
            var baseline = Sample();
            var completion = new TaskCompletionSource<bool>();
            var samples = new List<object>(); var gpu = new List<GpuMemoryProbe.AdapterMemory>();
            const int measuredLoops = 8;
            var intervals = new List<double>(); var work = new List<double>();
            var withinEventIntervals = new List<double>(); var eventGaps = new List<double>();
            var framesPerEvent = new int[measuredLoops];
            var frameCadence = new FrameCadence();
            int completed = -1, measuredFrames = 0;
            double startCpu = 0, endCpu = 0; long start = 0, end = 0, previous = 0;
            int previousEvent = -1;
            DesktopFrameClock clock = null;
            animation.PrepareSanguoshaPlayback(assets, 0);
            surface.Place(screen, layout, true, false);
            surface.FramePresented = () =>
            {
                if (completed < 0) return;
                long now = Stopwatch.GetTimestamp();
                if (previous != 0)
                {
                    double interval = Stopwatch.GetElapsedTime(previous, now).TotalMilliseconds;
                    intervals.Add(interval);
                    if (previousEvent == completed) withinEventIntervals.Add(interval); else eventGaps.Add(interval);
                }
                previous = now; previousEvent = completed; measuredFrames++; framesPerEvent[completed]++;
            };
            try
            {
                clock = new DesktopFrameClock(System.Windows.Threading.Dispatcher.CurrentDispatcher, timestamp =>
                {
                    try
                    {
                        if (!frameCadence.ShouldPresent(timestamp, DisplayConfiguration.PresentationFramesPerSecond)) return;
                        long before = Stopwatch.GetTimestamp();
                        surface.AdvanceAndRender();
                        if (animation.DesktopSanguoshaPath.StartsWith("atlas:", StringComparison.Ordinal))
                            throw new InvalidOperationException("Performance run requires an exact cached profile: " + animation.DesktopSanguoshaPath);
                        if (completed >= 0) work.Add(Stopwatch.GetElapsedTime(before).TotalMilliseconds);
                        if (completed >= 0 && measuredFrames % 30 == 0)
                        {
                            var memory = Sample(); gpu.Add(memory); process.Refresh();
                            samples.Add(new { measuredFrames, memory.LocalUsageBytes, memory.NonLocalUsageBytes,
                                PrivateBytes = process.PrivateMemorySize64, CompressedScratchBytes = animation._desktopSanguoshaLz4?.Source.CompressedCacheBytes,
                                RawFramePoolBytes = animation._desktopSanguoshaLz4?.RawBufferBytes, Path = animation.DesktopSanguoshaPath });
                        }
                        if (!animation.IsDesktopPlaying)
                        {
                            completed++;
                            if (completed == 0)
                            {
                                process.Refresh(); startCpu = process.TotalProcessorTime.TotalSeconds; start = Stopwatch.GetTimestamp();
                            }
                            if (completed >= measuredLoops)
                            {
                                end = Stopwatch.GetTimestamp(); process.Refresh(); endCpu = process.TotalProcessorTime.TotalSeconds;
                                clock?.Dispose(); completion.TrySetResult(true); return;
                            }
                            animation.PrepareSanguoshaPlayback(assets, 0);
                        }
                    }
                    catch (Exception error) { clock?.Dispose(); completion.TrySetException(error); }
                });
                if (await Task.WhenAny(completion.Task, Task.Delay(TimeSpan.FromMinutes(3))) != completion.Task)
                    throw new TimeoutException("Sanguosha production playback measurement timed out.");
                await completion.Task;
                double seconds = Stopwatch.GetElapsedTime(start, end).TotalSeconds;
                object Statistics(List<double> values)
                {
                    var sorted = values.OrderBy(value => value).ToArray();
                    return sorted.Length == 0 ? null : new { Count = sorted.Length, Mean = sorted.Average(),
                        P95 = sorted[(int)((sorted.Length - 1) * .95)], P99 = sorted[(int)((sorted.Length - 1) * .99)], Max = sorted[^1] };
                }
                File.WriteAllText(Path.Combine(output, "sanguosha-lz4-performance.json"), JsonSerializer.Serialize(new
                {
                    Tier = SanguoshaTierAnimations[tier - 1], Loops = measuredLoops, Surface = "Production OverlaySurface + LayeredFrameWindow + DesktopFrameClock",
                    PhysicalWidth = surface.PresentedSize.Width, PhysicalHeight = surface.PresentedSize.Height,
                    MeasuredFrames = measuredFrames, Seconds = seconds, AveragePresentedFps = measuredFrames / seconds,
                    ProcessCpuSeconds = endCpu - startCpu, AverageLogicalCores = (endCpu - startCpu) / seconds,
                    WorkMs = Statistics(work), IntervalMs = Statistics(intervals), IntervalsOver25Ms = intervals.Count(value => value > 25),
                    WithinEventIntervalMs = Statistics(withinEventIntervals), WithinEventIntervalsOver25Ms = withinEventIntervals.Count(value => value > 25),
                    BetweenEventGapMs = Statistics(eventGaps), FramesPerEvent = framesPerEvent,
                    Baseline = baseline, SampledPeakDedicatedMiB = gpu.Max(value => value.LocalUsageBytes.Value) / 1048576d,
                    SampledPeakSharedMiB = gpu.Max(value => value.NonLocalUsageBytes.Value) / 1048576d, Samples = samples,
                    Scope = "Default layout at actual monitor DPI; one warm-up event excluded; process CPU includes presentation clock and decoder workers. Repeated event initialization included. Renderer process memory only, not external DWM; native submissions, not scanout capture."
                }, new JsonSerializerOptions { WriteIndented = true }));
            }
            finally { clock?.Dispose(); animation.ReleaseAnimationResourcesForPackChange(); }
        }
    }
}

using KillConfirmCompatibility.Contracts;
using KillConfirmCompatibility.Desktop.Runtime;
using KillConfirmCompatibility.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace KillConfirmCompatibility.Validation
{
    internal static class FrameRateAudit
    {
        internal static async Task RunAsync(string output, string assetsRoot)
        {
            Directory.CreateDirectory(output);
            DesktopStorage.TestDataRoot = Path.Combine(output, "isolated-profile");
            DesktopStorage.AssetsRoot = Path.GetFullPath(assetsRoot) + Path.DirectorySeparatorChar;
            var settings = DesktopStorage.Current.LocalSettings.Values;
            settings["Crossfire.BundledRoot"] = Path.Combine(assetsRoot, "DefaultPacks", "crossfire");
            settings[Danmaku.DanmakuSettingsStore.EnabledSettingKey] = false;
            using var service = new LocalProtocolFixture();
            settings[PortSettingsStore.PortKey] = service.Port;
            string config = Path.Combine(DesktopStorage.Current.LocalFolder.Path, DisplayFiles.FolderName, DisplayFiles.ConfigurationName);
            var initial = new DisplayConfiguration { Enabled = true, FollowGame = false, HideWhenInactive = false, TestAudio = false, FramesPerSecond = 30 };
            initial.Normalize();
            if (initial.FramesPerSecond != 60) throw new Exception("Legacy 30 FPS setting still limits presentation.");
            DisplayFiles.Write(config, initial);
            var report = new List<string> { "game,motion,preset,layer,frames,active_sample_ms,actual_updates_per_second,p95_frame_interval_ms,result" };
            var host = new HostController(shutdownApplicationOnClose: false);
            try
            {
                long request = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                foreach (GameStyleMode style in Enum.GetValues(typeof(GameStyleMode)))
                foreach (int motion in style == GameStyleMode.Crossfire ? new[] { 1, 2 } : new[] { 1 })
                {
                    GameStyleService.Current = style;
                    settings["MainAnimationStyle"] = motion;
                    string key = GameStyleService.ToStorageValue(style);
                    DisplayFiles.Update(config, current => {
                        var layout = current.GetLayout(key);
                        layout.Lower.Scale = layout.Crosshair.Scale = layout.Upper.Scale = 1;
                    });
                    await Task.Delay(600);
                    var presets = style == GameStyleMode.Crossfire ? new[] { "one_hs", "six", "one_first", "assist", "bomb_plant" }
                        : style == GameStyleMode.ModernWarfare2019 ? new[] { "one_hs", "six", "bomb_plant" }
                        : new[] { "one_hs", "six" };
                    foreach (string preset in presets)
                    {
                        string geometryError = null;
                        request++;
                        var timestamps = host.AuditSurfaces.Where(s => s.ElementKey != "Danmaku").ToDictionary(s => s.ElementKey, s => new List<long>());
                        foreach (var surface in host.AuditSurfaces.Where(s => s.ElementKey != "Danmaku"))
                            surface.FramePresented = () => {
                                if (host.CurrentPreviewRequest == request && surface.IsPlaying && surface.HasRenderedPixels)
                                {
                                    timestamps[surface.ElementKey].Add(Stopwatch.GetTimestamp());
                                    try { surface.ValidateCroppedPlacement(); }
                                    catch (Exception error) { geometryError ??= surface.ElementKey + ": " + error.Message; }
                                }
                            };
                        DisplayFiles.Update(config, current => { current.TestPreset = preset; current.TestRequest = request; });
                        var deadline = DateTimeOffset.UtcNow.AddSeconds(12);
                        while (!timestamps.Values.Any(samples => samples.Count > 0) && DateTimeOffset.UtcNow < deadline)
                            await Task.Delay(10);
                        if (timestamps.Values.Any(samples => samples.Count > 0)) await Task.Delay(1000);
                        foreach (var surface in host.AuditSurfaces) surface.FramePresented = null;
                        if (geometryError != null) throw new Exception(key + "/" + motion + "/" + preset + ": " + geometryError);
                        foreach (var surface in host.AuditSurfaces.Where(s => s.ElementKey != "Danmaku" && s.IsPresentationVisible && s.HasRenderedPixels))
                            surface.ValidateCroppedPlacement();
                        bool any = false;
                        foreach (var pair in timestamps.Where(pair => pair.Value.Count > 0))
                        {
                            any = true;
                            var samples = pair.Value;
                            double elapsed = Stopwatch.GetElapsedTime(samples[0], samples[samples.Count - 1]).TotalMilliseconds;
                            var intervals = samples.Zip(samples.Skip(1), (a, b) => Stopwatch.GetElapsedTime(a, b).TotalMilliseconds).OrderBy(v => v).ToArray();
                            double fps = elapsed > 0 ? (samples.Count - 1) * 1000 / elapsed : 0;
                            double p95 = intervals.Length > 0 ? intervals[(int)Math.Ceiling(intervals.Length * .95) - 1] : 0;
                            report.Add(FormattableString.Invariant($"{key},{motion},{preset},{pair.Key},{samples.Count},{elapsed:F1},{fps:F1},{p95:F1},{(samples.Count > 1 ? "measured" : "static_or_short")}"));
                        }
                        if (!any) report.Add($"{key},{motion},{preset},none,0,0,0,0,no_visible_animation");
                        File.WriteAllLines(Path.Combine(output, "frame-rates.csv"), report);
                        File.WriteAllText(Path.Combine(output, "progress.txt"), key + "/" + motion + "/" + preset);
                    }
                }
            }
            finally { await host.CloseAsync(); }
            File.WriteAllText(Path.Combine(output, "complete.txt"), "Fixed 60 Hz target; default packs; all layouts at 100%; no audio or danmaku; first 1 second after first visible frame; actual presented updates measured per layer. Static holds and source sprite rates can reduce updates. This is not a GPU benchmark or an FPS guarantee.");
        }
    }
}

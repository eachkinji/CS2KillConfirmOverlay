using KillConfirmCompatibility.Contracts;
using KillConfirmCompatibility.Desktop.Runtime;
using KillConfirmCompatibility.Services;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KillConfirmCompatibility.Validation;

internal static class InteractionValidation
{
    internal static async Task RunAsync(string output, string assets)
    {
        Directory.CreateDirectory(output);
        DesktopStorage.TestDataRoot = Path.Combine(output, "profile");
        DesktopStorage.AssetsRoot = Path.GetFullPath(assets) + Path.DirectorySeparatorChar;
        var settings = DesktopStorage.Current.LocalSettings.Values;
        using var service = new LocalProtocolFixture();
        settings[PortSettingsStore.PortKey] = service.Port;
        settings["Crossfire.BundledRoot"] = Path.Combine(assets, "DefaultPacks", "crossfire");
        settings[Danmaku.DanmakuSettingsStore.EnabledSettingKey] = false;
        GameStyleService.Current = GameStyleMode.Csol;
        var registration = new ServiceSession();
        await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => registration.EnsureRegisteredAsync()));
        if (service.Count("/client/register") != 1 || service.Count("/soundpack") != 1)
            throw new Exception("Concurrent callers did not share registration/audio preparation.");
        await registration.EnsureRegisteredAsync();
        if (service.Count("/client/register") != 1 || service.Count("/soundpack") != 1)
            throw new Exception("Warm preview repeated unchanged service configuration.");
        await registration.ReleaseAsync();
        var report = new StringBuilder("PASS: five concurrent registration callers joined one request; warm reuse sent no soundpack/configuration request.\n");
        string config = Path.Combine(DesktopStorage.Current.LocalFolder.Path, DisplayFiles.FolderName, DisplayFiles.ConfigurationName);
        string statusPath = Path.Combine(Path.GetDirectoryName(config), DisplayFiles.StatusName);
        DisplayFiles.Write(config, new DisplayConfiguration { Enabled = true, FollowGame = false, HideWhenInactive = true, TestAudio = true });
        var host = new HostController(shutdownApplicationOnClose: false);
        try
        {
            long request = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            foreach (var mode in new[] { GameStyleMode.Crossfire, GameStyleMode.Valorant, GameStyleMode.Overwatch })
            {
                GameStyleService.Current = mode;
                foreach (int motion in mode == GameStyleMode.Crossfire ? new[] { 1, 2 } : new[] { 1 })
                {
                    settings["MainAnimationStyle"] = motion;
                    for (int repeat = 0; repeat < 2; repeat++)
                    {
                        request++;
                        var clock = Stopwatch.StartNew();
                        double firstPixel = -1;
                        DisplayFiles.Update(config, c => { c.TestPreset = "one"; c.TestRequest = request; });
                        DisplayStatus status = null;
                        while (clock.Elapsed < TimeSpan.FromSeconds(30))
                        {
                            await Task.Delay(10);
                            if (firstPixel < 0 && host.CurrentPreviewRequest == request && host.HasRenderedPreviewPixels)
                                firstPixel = clock.Elapsed.TotalMilliseconds;
                            status = DisplayFiles.Read<DisplayStatus>(statusPath);
                            if (status?.LastTestRequest == request) break;
                        }
                        if (status?.LastTestRequest != request || !string.IsNullOrEmpty(status.TestError) || firstPixel < 0)
                            throw new Exception($"Preview failed {mode}/{motion}: {status?.TestError}; {host.PreviewDiagnostics}");
                        if (repeat == 1 && firstPixel > 1500) throw new Exception($"Warm preview too slow: {mode}/{motion}: {firstPixel:F0} ms.");
                        report.AppendLine($"{mode}/{motion}, {(repeat == 0 ? "first" : "repeat")}: pixels={firstPixel:F0} ms; confirmed={clock.ElapsedMilliseconds} ms.");
                    }
                }
            }
            DisplayFiles.Update(config, c => { c.TestPreset = "bomb_plant"; c.TestRequest = ++request; });
            var unsupportedClock = Stopwatch.StartNew();
            DisplayStatus unsupported;
            do { await Task.Delay(10); unsupported = DisplayFiles.Read<DisplayStatus>(statusPath); }
            while (unsupported?.LastTestRequest != request && unsupportedClock.ElapsedMilliseconds < 1500);
            if (unsupported?.LastTestRequest != request || unsupported.TestError?.Contains("不支持") != true)
                throw new Exception("Unsupported renderer test entered the pixel wait: " + unsupported?.TestError);
            report.AppendLine("Unsupported Overwatch objective rejected by renderer in " + unsupportedClock.ElapsedMilliseconds + " ms.");
            DisplayFiles.Update(config, c => c.EditRequest = ++request);
            for (int i = 0; i < 100 && DisplayFiles.Read<DisplayStatus>(statusPath)?.Editing != true; i++) await Task.Delay(25);
            var lower = host.LowerSurface;
            ElementLayout Current() => DisplayFiles.Read<DisplayConfiguration>(config).GetLayout("overwatch").Lower;
            lower.ApplyEditorCommand("top");
            if (Math.Abs(Current().Y - .2) > .001) throw new Exception("Editor top placement not saved.");
            lower.ApplyEditorCommand("bottom");
            if (Math.Abs(Current().Y - .8) > .001) throw new Exception("Editor bottom placement not saved.");
            lower.ApplyEditorCommand("center");
            if (Math.Abs(Current().X - .5) > .001 || Math.Abs(Current().Y - .5) > .001) throw new Exception("Editor center not saved.");
            double scale = Current().Scale;
            lower.ApplyEditorCommand("larger");
            if (Math.Abs(Current().Scale - scale * 1.1) > .001) throw new Exception("Editor scale not saved.");
            lower.ApplyEditorCommand("smaller");
            foreach (string command in new[] { "left", "up", "right", "down" }) lower.ApplyEditorCommand(command);
            if (Math.Abs(Current().X - .5) > .001 || Math.Abs(Current().Y - .5) > .001 || Math.Abs(Current().Scale - scale) > .001)
                throw new Exception("Editor nudge/scale commands did not round-trip.");
            report.AppendLine("PASS: editor top/center/bottom, enlarge/shrink and four nudge commands persisted; inactive-game previews show pixels and trigger audio.");
        }
        finally { await host.CloseAsync(); }
        File.WriteAllText(Path.Combine(output, "interaction-pass.txt"), report.ToString());
    }
}

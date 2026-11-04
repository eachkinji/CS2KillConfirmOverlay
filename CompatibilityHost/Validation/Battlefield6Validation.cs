using KillConfirmCompatibility.Contracts;
using KillConfirmCompatibility.Controls;
using KillConfirmCompatibility.Desktop.Runtime;
using KillConfirmCompatibility.Desktop.Windowing;
using KillConfirmCompatibility.Services;
using Microsoft.Graphics.Canvas;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Windows.Storage;

namespace KillConfirmCompatibility.Validation;

// Runs only via --validate-battlefield6 in a separate process. The real profile,
// service port, installed executable and resource catalog are never modified.
internal static class Battlefield6Validation
{
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    private static readonly List<string> Diagnostics = new();

    internal static async Task RunAsync(string output, string assetsRoot)
    {
        output = Path.GetFullPath(output);
        assetsRoot = Path.GetFullPath(assetsRoot);
        Directory.CreateDirectory(output);
        Diagnostics.Clear();
        string profile = Path.Combine(output, "profile-" + Guid.NewGuid().ToString("N"));
        string oldData = Environment.GetEnvironmentVariable("KILLCONFIRM_DATA_ROOT");
        string oldInstall = Environment.GetEnvironmentVariable("KILLCONFIRM_INSTALL_ROOT");
        Environment.SetEnvironmentVariable("KILLCONFIRM_DATA_ROOT", profile);
        Environment.SetEnvironmentVariable("KILLCONFIRM_INSTALL_ROOT", Path.Combine(profile, "no-installed-service"));
        DesktopStorage.TestDataRoot = profile;
        DesktopStorage.AssetsRoot = assetsRoot + Path.DirectorySeparatorChar;
        var report = new List<string>();
        HostController host = null;
        try
        {
            if (!string.Equals(Path.GetFullPath(DesktopStorage.Current.LocalFolder.Path), profile, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Battlefield 6 validation requires a fresh process and isolated profile.");
            using var service = new LocalProtocolFixture();
            var settings = DesktopStorage.Current.LocalSettings.Values;
            settings[PortSettingsStore.PortKey] = service.Port;
            settings[Danmaku.DanmakuSettingsStore.EnabledSettingKey] = false;
            settings["KillIconPack.battlefield6"] = "battlefield6";
            settings["VoicePack.battlefield6"] = "battlefield6";
            GameStyleService.Current = GameStyleMode.Battlefield6;
            string config = Path.Combine(profile, DisplayFiles.FolderName, DisplayFiles.ConfigurationName);
            string status = Path.Combine(profile, DisplayFiles.FolderName, DisplayFiles.StatusName);
            var initial = new DisplayConfiguration { Enabled = true, FollowGame = false, HideWhenInactive = false, TestAudio = true };
            initial.GetLayout("battlefield6").Lower.X = .5;
            initial.GetLayout("battlefield6").Lower.Y = .5;
            DisplayFiles.Write(config, initial);
            host = new HostController(shutdownApplicationOnClose: false);
            long request = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var starts = new Dictionary<double, System.Windows.Size>();

            foreach (double scale in new[] { 1.0, 3.0 })
            {
                DisplayFiles.Update(config, c => c.GetLayout("battlefield6").Lower.Scale = scale);
                await Task.Delay(500);
                foreach (string preset in new[] { "one", "one_hs", "one_knife", "one_grenade", "assist", "bomb_plant", "hostage_rescue", "round_win", "six", "nine" })
                {
                    var clock = Stopwatch.StartNew();
                    await StartPreviewAsync(host, config, status, preset, ++request);
                    double startup = clock.Elapsed.TotalMilliseconds;
                    var settleClock = Stopwatch.StartNew();
                    await Task.Delay(180);
                    Trace(host, $"{preset} after requested 180 ms settle; actual wait={settleClock.Elapsed.TotalMilliseconds:F0} ms");
                    AssertEventContent(host.LowerSurface, preset);
                    var size = host.LowerSurface.PresentedSize;
                    var content = host.LowerSurface.ContentSize;
                    if (Math.Abs(size.Width / size.Height - content.Width / content.Height) > .01)
                        throw new Exception("BF6 preview stretched: " + preset);
                    if (preset == "one") starts[scale] = size;
                    string name = "bf6-" + preset + "-" + scale + "x";
                    var observation = ObservePlayback(host.LowerSurface);
                    var captureClock = Stopwatch.StartNew();
                    host.LowerSurface.SavePreviewImage(Path.Combine(output, name + ".png"));
                    host.LowerSurface.ValidateCroppedPlacement(Path.Combine(output, name + "-committed.png"));
                    double captureMs = captureClock.Elapsed.TotalMilliseconds;
                    await WaitForNaturalFinishAsync(host.LowerSurface, size, observation);
                    if (startup > 1500 && report.Count > 0)
                        throw new Exception($"BF6 warm preview stalled: {preset}, {startup:F0} ms.");
                    report.Add($"PASS {preset}, {scale}x: first/confirmed frame {startup:F0} ms; actual outro {observation.Clock.Elapsed.TotalMilliseconds - observation.LastAdmission:F0} ms after admission; request-to-finish {clock.ElapsedMilliseconds} ms; full placement {size}; raster {observation.RasterSize}; screenshot/check {captureMs:F0} ms.");
                    report.Add(RenderCosts(host.LowerSurface, observation));
                    File.WriteAllLines(Path.Combine(output, "measurements.txt"), report);
                }
            }
            if (Math.Abs(starts[3].Width / starts[1].Width - 3) > .03 || Math.Abs(starts[3].Height / starts[1].Height - 3) > .03)
                throw new Exception("3x preview did not grow its physical raster resolution with the layout.");

            await ValidateBurstAsync(host, config, status, ++request, output, report);
            await ValidateExtremeContentAsync(host, config, status, ++request, output, report);
            await ValidateHostSwitchesAsync(host, config, status, ++request, assetsRoot, report);
            await host.CloseAsync(); host = null;
            await ValidatePendingCancellationAsync(report);
            if (service.MissingAuthentication) throw new Exception("Preview sent an unauthenticated fixture request.");
            if (service.Count("/test/0") == 0 || service.Count("/test/1") == 0 || service.Count("/test/6") == 0)
                throw new Exception("Preview did not exercise audio endpoints for assist/objective, normal and multi-kill events.");
            report.Add("PASS isolated loopback/profile, real rendering and audio-request path; 1x/3x native images, stable crop anchors, natural outro, queued multi-kills, style/pack switch cancellation.");
            File.WriteAllLines(Path.Combine(output, "pass.txt"), report);
        }
        catch (Exception error)
        {
            if (host != null) Trace(host, "FAIL: " + error.Message);
            throw;
        }
        finally
        {
            if (host != null) await host.CloseAsync();
            File.WriteAllLines(Path.Combine(output, "diagnostics.txt"), Diagnostics);
            Environment.SetEnvironmentVariable("KILLCONFIRM_DATA_ROOT", oldData);
            Environment.SetEnvironmentVariable("KILLCONFIRM_INSTALL_ROOT", oldInstall);
        }
    }

    private static async Task StartPreviewAsync(HostController host, string config, string statusPath, string preset, long request)
    {
        long before = host.LowerSurface.PresentedFrames;
        Trace(host, "Request " + preset + " id=" + request);
        DisplayFiles.Update(config, c => { c.TestPreset = preset; c.TestRequest = request; });
        var clock = Stopwatch.StartNew();
        double nextTrace = 500;
        while (clock.ElapsedMilliseconds < 8000)
        {
            await Task.Delay(10);
            var status = DisplayFiles.Read<DisplayStatus>(statusPath);
            if (clock.Elapsed.TotalMilliseconds >= nextTrace)
            {
                Trace(host, $"Waiting {preset}: {clock.ElapsedMilliseconds} ms; status request={status?.LastTestRequest}, loading={status?.Loading}, error={status?.Error}, test error={status?.TestError}");
                nextTrace = clock.Elapsed.TotalMilliseconds + 500;
            }
            if (status?.LastTestRequest == request && !string.IsNullOrEmpty(status.TestError))
                throw new Exception("BF6 " + preset + ": " + status.TestError);
            if (status?.LastTestRequest == request && host.CurrentPreviewRequest == request
                && host.LowerSurface.PresentedFrames > before && host.HasRenderedLowerPixels && host.LowerSurface.IsPlaying)
            {
                Trace(host, $"Confirmed {preset} after {clock.ElapsedMilliseconds} ms; status request={status.LastTestRequest}");
                return;
            }
        }
        throw new Exception("BF6 preview did not show/confirm " + preset + ": " + host.PreviewDiagnostics + "; " + host.LowerSurface.PlaybackDiagnostics);
    }

    private static void AssertEventContent(OverlaySurface surface, string preset)
    {
        object animation = Field(surface, "_animation");
        if (!(bool)Field(animation, "_isBattlefield6HudActive"))
            throw new Exception("BF6 preview is no longer active: " + preset + "; " + AnimationDiagnostics(animation)
                + "; inspect diagnostics.txt for the acknowledged state and actual 180 ms continuation delay.");
        object state = Field(animation, "_battlefield6HudState");
        var icons = (IList)Field(state, "Icons");
        bool round = preset.StartsWith("round_", StringComparison.Ordinal);
        if ((round && icons.Count != 0) || (!round && icons.Count == 0))
            throw new Exception("Wrong BF6 icon content for " + preset);
        if (preset == "one_hs" && !(bool)Field(icons[0], "Headshot"))
            throw new Exception("Headshot lost its distinct BF6 treatment.");
        if (((IList)Field(state, "Bonuses")).Count == 0)
            throw new Exception("BF6 event text is missing: " + preset);
        if (preset == "assist" && Convert.ToDouble(Field(state, "ScoreTarget")) != 0)
            throw new Exception("Assist invented a score absent from the input event.");
        if (preset != "assist" && Convert.ToDouble(Field(state, "ScoreTarget")) <= 0)
            throw new Exception("Event reward is missing: " + preset);
    }

    private sealed class PlaybackObservation
    {
        internal Stopwatch Clock;
        internal double LastAdmission, ExpectedFinish;
        internal long Frames;
        internal double Draw, Readback, Copy;
        internal System.Windows.Size RasterSize;
    }

    private static PlaybackObservation ObservePlayback(OverlaySurface surface)
    {
        object animation = Field(surface, "_animation");
        object state = Field(animation, "_battlefield6HudState");
        if (PendingCount(state) != 0) throw new Exception("Cannot time a completed BF6 event sequence while admissions are pending.");
        double admission = Convert.ToDouble(Field(state, "LastAdmissionMs"));
        bool score = Convert.ToDouble(Field(state, "ScoreTarget")) > 0;
        return new PlaybackObservation
        {
            Clock = (Stopwatch)Field(animation, "_playbackClock"), LastAdmission = admission,
            // Authored timelines: rewards hold/fade for 3700+300 ms; zero-score
            // assists retain their icon for 100+3300+200 ms after admission.
            ExpectedFinish = admission + (score ? 4000 : 3600),
            Frames = surface.PresentedFrames, Draw = surface.DrawMilliseconds,
            Readback = surface.ReadbackMilliseconds, Copy = surface.CopyMilliseconds,
            RasterSize = surface.RasterSize
        };
    }

    private static async Task WaitForNaturalFinishAsync(OverlaySurface surface, System.Windows.Size fixedSize, PlaybackObservation observation)
    {
        // The animation clock starts before preview acknowledgement and PNG I/O.
        // Measuring a new stopwatch here would falsely call a correct outro early.
        double deadline = observation.LastAdmission + 5200;
        while (surface.IsPlaying && observation.Clock.Elapsed.TotalMilliseconds < deadline)
        {
            await Task.Delay(160);
            if (!surface.IsPlaying) break;
            if (surface.PresentedSize != fixedSize) throw new Exception("BF6 playback changed scale or full-frame placement mid-animation.");
            if (surface.HasRenderedPixels) surface.ValidateCroppedPlacement();
        }
        double finished = observation.Clock.Elapsed.TotalMilliseconds;
        if (surface.IsPlaying || finished > deadline)
            throw new Exception($"BF6 natural finish exceeded 5200 ms after admission: actual={finished - observation.LastAdmission:F0} ms.");
        if (finished < observation.ExpectedFinish - 20)
            throw new Exception($"BF6 feedback disappeared before its authored outro: expected>={observation.ExpectedFinish:F0} ms, actual={finished:F0} ms on its playback clock.");
        await Task.Delay(80);
        if (surface.IsPresentationVisible || surface.HasRenderedPixels)
            throw new Exception("Finished BF6 animation left its cropped window visible.");
        if (surface.PresentedFrames <= observation.Frames + 2) throw new Exception("BF6 animation froze instead of advancing through its outro.");
    }

    private static string RenderCosts(OverlaySurface surface, PlaybackObservation observation)
    {
        long frames = Math.Max(1, surface.PresentedFrames - observation.Frames);
        return $"  render {frames} frames: draw={(surface.DrawMilliseconds - observation.Draw) / frames:F2} ms/frame; readback={(surface.ReadbackMilliseconds - observation.Readback) / frames:F2}; copy={(surface.CopyMilliseconds - observation.Copy) / frames:F2}. Screenshot encoding/check time is reported separately.";
    }

    private static async Task ValidateBurstAsync(HostController host, string config, string status, long request, string output, List<string> report)
    {
        await StartPreviewAsync(host, config, status, "one", request);
        var presenter = (FeedbackPresenter)Field(host, "_presenter");
        for (int i = 0; i < 8; i++)
        {
            var item = PreviewEvents.Create(i % 2 == 0 ? "one_hs" : "one_grenade");
            item.TargetName = "Burst " + (i + 2);
            item.KillCount = i + 2;
            presenter.HandleKillEvent(item);
        }
        var clock = Stopwatch.StartNew();
        object state = Field(presenter.LowerFeedbackAnimation, "_battlefield6HudState");
        while (PendingCount(state) > 0 && clock.ElapsedMilliseconds < 2500)
            await Task.Delay(20);
        if (PendingCount(state) != 0) throw new Exception("BF6 burst queue stalled before all events were admitted.");
        int icons = ((IList)Field(state, "Icons")).Count;
        if (icons < 2 || icons > 9) throw new Exception("BF6 burst did not retain a bounded multi-icon queue: " + icons);
        if (Convert.ToDouble(Field(state, "ScoreTarget")) != 2700) throw new Exception("BF6 burst lost or duplicated event rewards.");
        var observation = ObservePlayback(host.LowerSurface);
        host.LowerSurface.SavePreviewImage(Path.Combine(output, "bf6-nine-event-burst-3x.png"));
        host.LowerSurface.ValidateCroppedPlacement(Path.Combine(output, "bf6-nine-event-burst-3x-committed.png"));
        await WaitForNaturalFinishAsync(host.LowerSurface, host.LowerSurface.PresentedSize, observation);
        report.Add("PASS nine genuine consecutive events: queue drains, multiple icons remain, rewards total 2700, and final outro completes without scale jumps.");
        report.Add(RenderCosts(host.LowerSurface, observation));
    }

    private static async Task ValidateHostSwitchesAsync(HostController host, string config, string status, long request, string assetsRoot, List<string> report)
    {
        string icon = Path.Combine(assetsRoot, "Assets", "GameStyles", "battlefield6", "killconfirm", "textures", "killicon_battlefield6_default.png");
        await PackCatalogService.CreateBattlefield6IconPackAsync("BF6 isolated validation", new Dictionary<string, StorageFile>
        { ["killicon_battlefield6_default.png"] = await StorageFile.GetFileFromPathAsync(icon) });
        var custom = (await PackCatalogService.GetAllIconPacksAsync()).Single(p => p.DisplayName == "BF6 isolated validation");
        // Catalog writes legitimately cancel old rendering; let that settle before replaying.
        await Task.Delay(600);
        await StartPreviewAsync(host, config, status, "one_hs", request);
        DesktopStorage.Current.LocalSettings.Values["KillIconPack.battlefield6"] = custom.Key;
        await WaitUntilQuietAsync(host, "pack switch");
        await StartPreviewAsync(host, config, status, "one", ++request);
        GameStyleService.Current = GameStyleMode.Battlefield4;
        await WaitUntilQuietAsync(host, "style switch away from BF6");
        GameStyleService.Current = GameStyleMode.Battlefield6;
        DesktopStorage.Current.LocalSettings.Values["KillIconPack.battlefield6"] = "battlefield6";
        await WaitUntilQuietAsync(host, "switch back without a new event");
        report.Add("PASS actual imported pack and game-style changes cancel active BF6 playback; switching back never replays expired events.");
    }

    private static async Task ValidateExtremeContentAsync(HostController host, string config, string status, long request, string output, List<string> report)
    {
        await StartPreviewAsync(host, config, status, "one", request);
        var presenter = (FeedbackPresenter)Field(host, "_presenter");
        string victim = string.Concat(Enumerable.Repeat("测试", 12)); // Exactly 24 full-width CJK characters.
        foreach (string preset in new[] { "one", "one_hs", "one_grenade" })
        {
            var item = PreviewEvents.Create(preset);
            item.TargetName = victim;
            item.MoneyReward = int.MaxValue;
            presenter.HandleKillEvent(item);
        }
        object state = Field(presenter.LowerFeedbackAnimation, "_battlefield6HudState");
        var deadline = Stopwatch.StartNew();
        while (PendingCount(state) > 0 && deadline.ElapsedMilliseconds < 2500) await Task.Delay(20);
        if (PendingCount(state) != 0) throw new Exception("BF6 extreme-content events failed to enter the animation.");
        if (((IList)Field(state, "Bonuses")).Count != 3) throw new Exception("Three distinct BF6 bonus labels did not coexist.");
        if (Convert.ToDouble(Field(state, "ScoreTarget")) != int.MaxValue)
            throw new Exception("Maximum rewards overflowed or failed to saturate the BF6 score.");
        var observation = ObservePlayback(host.LowerSurface);
        await Task.Delay(1400); // Let the 1250 ms score counter reach its complete, widest text.
        if (Convert.ToInt32(Field(state, "DisplayScore")) != int.MaxValue)
            throw new Exception("BF6 maximum reward was not fully rendered before the hold period.");
        foreach (int scale in new[] { 1, 3 })
            await SavePaddedFrameAndCheckBoundsAsync(presenter.LowerFeedbackAnimation, output, scale);
        host.LowerSurface.ValidateCroppedPlacement(Path.Combine(output, "bf6-extreme-live-committed.png"));
        await WaitForNaturalFinishAsync(host.LowerSurface, host.LowerSurface.PresentedSize, observation);
        report.Add("PASS int.MaxValue rewards saturate without overflow; 24-character CJK victim and three simultaneous labels fit inside the logical viewport at 1x/3x with zero alpha outside every edge.");
        report.Add(RenderCosts(host.LowerSurface, observation));
    }

    private static async Task SavePaddedFrameAndCheckBoundsAsync(KillConfirmAnimation animation, string output, int scale)
    {
        int width = (int)animation.Battlefield6SelectionViewportWidth * scale;
        int height = (int)animation.Battlefield6SelectionViewportHeight * scale;
        int padding = 64 * scale;
        var device = CanvasDevice.GetSharedDevice();
        using var commands = new CanvasCommandList(device);
        using (var recording = commands.CreateDrawingSession())
            animation.DrawDesktopFrame(recording, width, height, clearTarget: false);
        using var target = new CanvasRenderTarget(device, width + 2 * padding, height + 2 * padding, 96);
        using (var drawing = target.CreateDrawingSession())
        {
            drawing.Clear(Colors.Transparent);
            drawing.DrawImage(commands, padding, padding);
        }
        // Draw onto a larger target so off-canvas pixels remain observable. Merely
        // checking an already-clipped 607x260 screenshot would hide this regression.
        string path = Path.Combine(output, "bf6-extreme-padded-" + scale + "x.png");
        using (var stream = File.Create(path))
            await target.SaveAsync(stream.AsRandomAccessStream(), CanvasBitmapFileFormat.Png);
        byte[] pixels = target.GetPixelBytes();
        int rasterWidth = width + 2 * padding, rasterHeight = height + 2 * padding;
        int visible = 0, overflow = 0;
        for (int y = 0; y < rasterHeight; y++)
        for (int x = 0; x < rasterWidth; x++)
        {
            if (pixels[(y * rasterWidth + x) * 4 + 3] == 0) continue;
            visible++;
            if (x < padding || x >= padding + width || y < padding || y >= padding + height) overflow++;
        }
        if (visible == 0) throw new Exception("BF6 boundary capture was unexpectedly blank: " + path);
        if (overflow != 0) throw new Exception($"BF6 extreme content clips {overflow} nontransparent pixels outside its logical viewport; inspect {path}.");
    }

    private static async Task WaitUntilQuietAsync(HostController host, string operation)
    {
        var clock = Stopwatch.StartNew();
        while (host.AuditSurfaces.Any(s => s.IsPlaying) && clock.ElapsedMilliseconds < 1600) await Task.Delay(20);
        if (host.AuditSurfaces.Any(s => s.IsPlaying)) throw new Exception(operation + " did not cancel playback.");
        for (int i = 0; i < 12; i++)
        {
            await Task.Delay(50);
            if (host.AuditSurfaces.Any(s => s.IsPlaying || s.HasRenderedPixels)) throw new Exception(operation + " revived stale playback.");
        }
    }

    private static async Task ValidatePendingCancellationAsync(List<string> report)
    {
        var animation = new KillConfirmAnimation();
        animation.UseDesktopFrameClock();
        try
        {
            for (int attempt = 0; attempt < 4; attempt++)
            {
                animation.ReleaseAnimationResourcesForPackChange();
                KillConfirmAnimation.ConfigureIconPack("battlefield6");
                animation.PlayBattlefield6Kill(1, true, false, false, false, "Pending", "AK-47", 300, "kill", 1, 1);
                if (attempt % 2 == 0) animation.StopDesktopPlayback();
                else
                {
                    animation.ReleaseAnimationResourcesForPackChange();
                    KillConfirmAnimation.ConfigureIconPack("bf4");
                }
                for (int i = 0; i < 20; i++)
                {
                    await Task.Delay(20);
                    animation.AdvanceDesktopFrame();
                    if (animation.IsDesktopPlaying || (bool)Field(animation, "_isBattlefield6HudActive"))
                        throw new Exception("A late asynchronous BF6 asset load revived stopped playback.");
                }
            }
            report.Add("PASS four uncached asynchronous playback cancellations: explicit stop and resource-generation changes do not revive stale assets.");
        }
        finally { animation.StopDesktopPlayback(); animation.ReleaseAnimationResourcesForPackChange(); }
    }

    private static object Field(object target, string name) => target.GetType().GetField(name, Fields)?.GetValue(target)
        ?? throw new MissingFieldException(target.GetType().FullName, name);

    private static int PendingCount(object state)
    {
        object pending = Field(state, "Pending");
        return Convert.ToInt32(pending.GetType().GetProperty("Count").GetValue(pending));
    }

    private static object ReadField(object target, string name)
        => target.GetType().GetField(name, Fields | BindingFlags.Static)?.GetValue(target);

    private static string AnimationDiagnostics(object animation)
    {
        var clock = (Stopwatch)ReadField(animation, "_playbackClock");
        object state = ReadField(animation, "_battlefield6HudState");
        return $"clock={clock?.Elapsed.TotalMilliseconds:F1}, running={clock?.IsRunning}, bf6={ReadField(animation, "_isBattlefield6HudActive")}, token={ReadField(animation, "_playToken")}, session={ReadField(animation, "_battlefield6Session")}, generation={ReadField(animation, "_resourceGeneration")}, assetGeneration={ReadField(animation, "_battlefield6AssetGeneration")}, admission={ReadField(state, "LastAdmissionMs")}, icons={((IList)ReadField(state, "Icons")).Count}, bonuses={((IList)ReadField(state, "Bonuses")).Count}, pending={PendingCount(state)}, score={ReadField(state, "ScoreTarget")}";
    }

    private static void Trace(HostController host, string phase)
    {
        var lower = host.LowerSurface;
        var animation = (KillConfirmAnimation)Field(lower, "_animation");
        var presenter = (FeedbackPresenter)Field(host, "_presenter");
        var applying = ReadField(host, "_applyingConfiguration") as Task;
        Diagnostics.Add($"{DateTimeOffset.UtcNow:O} {phase}\n  style={GameStyleService.Current}, request={host.CurrentPreviewRequest}, completed={ReadField(host, "_completedTestRequest")}, configTask={applying?.Status}, signature={ReadField(host, "_lastSignature")}; hostError={ReadField(host, "_renderError")}; testError={ReadField(host, "_testError")}; configError={presenter.ConfigurationError}; playbackError={animation.DesktopPlaybackError}\n  {AnimationDiagnostics(animation)}; frames={lower.PresentedFrames}; {lower.PlaybackDiagnostics}; {host.PreviewDiagnostics}");
    }
}

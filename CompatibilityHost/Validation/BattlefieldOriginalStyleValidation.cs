using KillConfirmCompatibility.Controls;
using KillConfirmCompatibility.Desktop.Runtime;
using KillConfirmCompatibility.Services;
using Microsoft.Graphics.Canvas;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace KillConfirmCompatibility.Validation;

// Capture the settled HUD, not the first nontransparent pixels during entry.
// Run in a fresh process with an isolated profile; no service or audio is started.
internal static class BattlefieldOriginalStyleValidation
{
    internal static async Task RunAsync(string output, string assetsRoot)
    {
        output = Path.GetFullPath(output);
        Directory.CreateDirectory(output);
        DesktopStorage.TestDataRoot = Path.Combine(output, "profile-" + Guid.NewGuid().ToString("N"));
        DesktopStorage.AssetsRoot = Path.GetFullPath(assetsRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var report = new List<string>();
        foreach (var style in new[] { GameStyleMode.Battlefield1, GameStyleMode.Battlefield5, GameStyleMode.Battlefield2042 })
        {
            GameStyleService.Current = style;
            foreach (string variant in new[] { "normal", "headshot", "long-name", "rapid-two-kills" })
            {
                // Fresh state per case: StopDesktopPlayback stops the clock but
                // intentionally does not clear all accumulated reward/feed data.
                var presenter = new FeedbackPresenter();
                var animations = new[] { presenter.LowerFeedbackAnimation, presenter.CrosshairFeedbackAnimation,
                    presenter.UpperFeedbackAnimation, presenter.LowerBadgeAnimation };
                try
                {
                    await presenter.ApplyConfigurationAsync();
                    presenter.HandleKillEvent(new KillEvent { KillCount = 1, IsHeadshot = variant == "headshot",
                        PlayMainAnimation = true, EventChannel = "combat", EventKind = "kill", AnimationKey = "multi1",
                        TargetName = variant == "long-name" ? "Player_With_A_Long_Name_123456789" : "Validation",
                        WeaponName = "AK-47", MoneyReward = 300 });
                    var main = presenter.LowerFeedbackAnimation;
                    var clock = (Stopwatch)typeof(KillConfirmAnimation).GetField("_playbackClock", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(main);
                    var timeout = Stopwatch.StartNew();
                    bool secondSent = false;
                    using var warmup = new CanvasRenderTarget(CanvasDevice.GetSharedDevice(), 607, 260, 96);
                    while ((!clock.IsRunning || clock.ElapsedMilliseconds < 1500) && timeout.ElapsedMilliseconds < 6000)
                    {
                        await Task.Delay(20);
                        // Feed positions interpolate while drawing, as in the live
                        // overlay. One draw only at 1500 ms would leave rows stacked.
                        using var session = warmup.CreateDrawingSession();
                        main.DrawDesktopFrame(session, 607, 260);
                        if (variant == "rapid-two-kills" && !secondSent && clock.ElapsedMilliseconds >= 180)
                        {
                            presenter.HandleKillEvent(new KillEvent { KillCount = 2, IsHeadshot = true,
                                PlayMainAnimation = true, EventChannel = "combat", EventKind = "kill", AnimationKey = "multi2",
                                TargetName = "SecondPlayer", WeaponName = "AK-47", MoneyReward = 300 });
                            secondSent = true;
                        }
                    }
                    if (!clock.IsRunning || clock.ElapsedMilliseconds < 1500 || clock.ElapsedMilliseconds > 2300)
                        throw new Exception("Could not capture the settled HUD for " + style + "/" + variant);
                    clock.Stop();
                    object state = Field(main, style == GameStyleMode.Battlefield2042 ? "_battlefield2042HudState" : "_battlefield5ScrollState");
                    int expectedEvents = variant == "rapid-two-kills" ? 2 : 1;
                    if (style != GameStyleMode.Battlefield2042 && Convert.ToDouble(Field(state, "MoneyTargetValue")) != 300 * expectedEvents)
                        throw new Exception("Rewards leaked between independent cases.");
                    if (style == GameStyleMode.Battlefield5 && ((System.Collections.ICollection)Field(state, "ActiveIcons")).Count != expectedEvents)
                        throw new Exception("Unexpected BFV icon count.");
                    int width = (int)Math.Ceiling(main.DisplayViewportWidth * 3);
                    int height = (int)Math.Ceiling(main.DisplayViewportHeight * 3);
                    using var target = new CanvasRenderTarget(CanvasDevice.GetSharedDevice(), width, height, 96);
                    using (var session = target.CreateDrawingSession()) main.DrawDesktopFrame(session, width, height);
                    byte[] pixels = target.GetPixelBytes();
                    int visible = Enumerable.Range(0, pixels.Length / 4).Count(i => pixels[i * 4 + 3] > 0);
                    if (visible < 100) throw new Exception("Missing settled HUD for " + style + "/" + variant);
                    using var stream = File.Create(Path.Combine(output, style + "-" + variant + "-hold.png"));
                    await target.SaveAsync(stream.AsRandomAccessStream(), CanvasBitmapFileFormat.Png);
                    report.Add($"PASS {style}/{variant}: settled {clock.ElapsedMilliseconds} ms, {width}x{height}, visiblePixels={visible}; {expectedEvents} event(s), source reward=300 per event unchanged.");
                }
                finally
                {
                    foreach (var animation in animations)
                    {
                        animation.StopDesktopPlayback();
                        animation.ReleaseAnimationResourcesForPackChange();
                    }
                }
            }
        }
        File.WriteAllLines(Path.Combine(output, "pass.txt"), report);
    }

    private static object Field(object target, string name) => target.GetType()
        .GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(target);
}

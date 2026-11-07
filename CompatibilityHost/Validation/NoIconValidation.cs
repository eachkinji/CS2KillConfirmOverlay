using KillConfirmCompatibility.Controls;
using KillConfirmCompatibility.Desktop.Runtime;
using KillConfirmCompatibility.Services;
using Microsoft.Graphics.Canvas;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage;

namespace KillConfirmCompatibility.Validation;

internal static class NoIconValidation
{
    internal static async Task RunAsync(string output)
    {
        Directory.CreateDirectory(output);
        DesktopStorage.TestDataRoot = Path.Combine(output, "profile");
        // No asset files exist here: selecting None must not try to decode any pack.
        DesktopStorage.AssetsRoot = Path.Combine(output, "empty-assets") + Path.DirectorySeparatorChar;
        Directory.CreateDirectory(DesktopStorage.AssetsRoot);
        var presenter = new FeedbackPresenter();
        var layers = new[] { presenter.LowerFeedbackAnimation, presenter.LowerBadgeAnimation,
            presenter.CrosshairFeedbackAnimation, presenter.UpperFeedbackAnimation };
        int danmakuEvents = 0;
        presenter.DanmakuEvent = _ => danmakuEvents++;
        var report = new List<string>();
        foreach (GameStyleMode style in Enum.GetValues(typeof(GameStyleMode)))
        {
            GameStyleService.Current = style;
            ApplicationData.Current.LocalSettings.Values["KillIconPack." + GameStyleService.ToStorageValue(style)] = "none";
            await presenter.ApplyConfigurationAsync();
            if (KillConfirmAnimation.IsIconFeedbackEnabled || !KillConfirmAnimation.IsIconPackConfigured("none")
                || !presenter.LowerFeedbackAnimation.IsCurrentPackPreloaded || presenter.ConfigurationError != null)
                throw new Exception("None failed to configure without assets: " + style + " " + presenter.ConfigurationError);
            foreach (string kind in new[] { "kill", "player_death", "round_win", "bomb_planted" })
            {
                int before = danmakuEvents;
                presenter.HandleKillEvent(new KillEvent { EventKind = kind, EventChannel = "combat", KillCount = 3,
                    IsHeadshot = true, PlayMainAnimation = true, AnimationKey = "multi3", TargetName = "No icons" });
                if (danmakuEvents != before + 1) throw new Exception("None suppressed independent danmaku routing.");
                foreach (var layer in layers)
                {
                    using var target = new CanvasRenderTarget(CanvasDevice.GetSharedDevice(), 320, 180, 96);
                    using (var drawing = target.CreateDrawingSession()) layer.DrawDesktopFrame(drawing, 320, 180);
                    byte[] pixels = target.GetPixelBytes();
                    for (int i = 3; i < pixels.Length; i += 4)
                        if (pixels[i] != 0) throw new Exception("None emitted icon pixels: " + style + "/" + kind);
                }
            }
            report.Add(style + ": no asset load, no icon pixels, independent event routing preserved");
        }
        KillConfirmAnimation.ConfigureIconPack("valorant");
        if (!KillConfirmAnimation.IsIconFeedbackEnabled) throw new Exception("Selecting a pack did not re-enable visuals.");
        foreach (var layer in layers) layer.StopDesktopPlayback();
        File.WriteAllLines(Path.Combine(output, "pass.txt"), report);
    }
}

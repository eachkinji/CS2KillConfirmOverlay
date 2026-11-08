using KillConfirmCompatibility.Desktop.Runtime;
using KillConfirmCompatibility.Services;
using Microsoft.Graphics.Canvas;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Windows.Data.Json;

namespace KillConfirmCompatibility.Controls
{
    // Replays service-produced LOL events through the current client parser and
    // presenter. This validates the desktop renderer, not a live Game Bar host.
    public sealed partial class KillConfirmAnimation
    {
        internal static async Task ValidateLolEventRenderingAsync(string output, string assetsRoot, string journalPath)
        {
            Directory.CreateDirectory(output);
            DesktopStorage.TestDataRoot = Path.Combine(output, "isolated-profile");
            DesktopStorage.AssetsRoot = Path.GetFullPath(assetsRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string crossfireRoot = Environment.GetEnvironmentVariable("KILLCONFIRM_CF_TEST_PACKS")
                ?? Path.Combine(assetsRoot, "DefaultPacks", "crossfire");
            if (!Directory.Exists(Path.Combine(crossfireRoot, "icon_packs", "default")))
                throw new DirectoryNotFoundException("CF default validation pack is missing: " + crossfireRoot);
            var settings = DesktopStorage.Current.LocalSettings.Values;
            settings["Crossfire.BundledRoot"] = Path.GetFullPath(crossfireRoot);
            settings["KillIconPack.crossfire"] = "default";
            settings["MainAnimationStyle"] = 1;
            settings["KillFxEnabled"] = 0; // Main icon pixels must stand on their own.
            settings["KillEliteEffect"] = 0;
            settings["KillWeaponBadge"] = 0;
            GameStyleService.Current = GameStyleMode.Crossfire;

            byte[] sourceBytes = File.ReadAllBytes(journalPath);
            JsonObject journal = JsonObject.Parse(System.Text.Encoding.UTF8.GetString(sourceBytes));
            JsonArray sourceEvents = journal.GetNamedArray("events");
            var cases = new Dictionary<int, (KillEvent Event, ulong Id)>();
            foreach (IJsonValue value in sourceEvents)
            {
                JsonObject json = value.GetObject();
                var parsed = KillEventParser.Parse(json);
                if (parsed.IsObservationEvent) continue; // records never render
                if (parsed.KillCount >= 1 && parsed.KillCount <= 5 && !cases.ContainsKey(parsed.KillCount))
                    cases.Add(parsed.KillCount, (parsed, (ulong)json.GetNamedNumber("id", 0)));
            }
            if (cases.Count != 5 || cases.Values.Any(item => item.Id == 0 || item.Event.SourceGeneration == 0)
                || cases.Values.Select(item => item.Id).Distinct().Count() != 5
                || cases.Values.Select(item => item.Event.SourceGeneration).Distinct().Count() != 1)
                throw new InvalidDataException("Replay needs five real journal events covering tiers 1..5 from one source generation.");

            var report = new List<string>
            {
                "Scope: captured LOL HTTPS integration events -> current KillEventClient parser -> FeedbackPresenter -> desktop Win2D frames. Live Game Bar hosting is not covered.",
                "Journal: " + Path.GetFullPath(journalPath),
                "Journal SHA256: " + Convert.ToHexString(SHA256.HashData(sourceBytes)),
                "CF default pack: " + Path.GetFullPath(crossfireRoot),
            };
            var presenter = new FeedbackPresenter();
            var animations = new[] { presenter.LowerFeedbackAnimation, presenter.LowerBadgeAnimation,
                presenter.CrosshairFeedbackAnimation, presenter.UpperFeedbackAnimation };
            foreach (var animation in animations) animation.UseDesktopFrameClock();
            var mainHashes = new HashSet<string>();
            try
            {
                await presenter.ApplyConfigurationAsync();
                if (!string.IsNullOrEmpty(presenter.ConfigurationError))
                    throw new InvalidOperationException(presenter.ConfigurationError);
                var lower = presenter.LowerFeedbackAnimation;
                if (!lower.IsCurrentPackPreloaded) throw new InvalidOperationException("CF default pack did not preload.");
                for (int count = 1; count <= 5; count++)
                {
                    foreach (var animation in animations) animation.StopDesktopPlayback();
                    KillEvent captured = cases[count].Event;
                    if (!captured.IsCombatEvent || !captured.PlayMainAnimation || captured.IsAssist
                        || captured.IsHeadshot || captured.IsKnifeKill || captured.IsGrenadeKill
                        || !string.IsNullOrEmpty(captured.AnimationKey))
                        throw new InvalidDataException("Captured LOL event cannot bypass ordinary tier routing: " + count);
                    string action = "multi" + count;
                    if (!CodeKillCache.TryGetValue(GetCodeKillCacheKey(action, string.Empty), out Code2KillAsset expected)
                        || expected.Main == null || expected.Action != action)
                        throw new InvalidOperationException("CF resource was not prepared: " + action);
                    presenter.HandleKillEvent(captured);
                    if (!ReferenceEquals(lower._currentCodeAsset, expected) || lower._currentCodeAsset.Action != action)
                        throw new InvalidOperationException("Presenter selected the wrong CF tier: " + action);
                    byte[] mainPixels = expected.Main.GetPixelBytes();
                    if (!Enumerable.Range(0, mainPixels.Length / 4).Any(index => mainPixels[index * 4 + 3] > 0))
                        throw new InvalidOperationException("CF main asset is transparent: " + action);
                    string mainHash = Convert.ToHexString(SHA256.HashData(mainPixels));
                    if (!mainHashes.Add(mainHash)) throw new InvalidOperationException("CF tier reused another tier's main pixels: " + action);

                    double width = Math.Max(1, lower.DisplayViewportWidth), height = Math.Max(1, lower.DisplayViewportHeight);
                    double fit = Math.Min(1, Math.Min(960 / width, 540 / height));
                    using var target = new CanvasRenderTarget(CanvasDevice.GetSharedDevice(),
                        Math.Max(1, (int)(width * fit)), Math.Max(1, (int)(height * fit)), 96);
                    int visiblePixels = 0;
                    for (int sample = 0; sample < 30 && visiblePixels == 0; sample++)
                    {
                        await Task.Delay(40);
                        lower.AdvanceDesktopFrame();
                        using (var drawing = target.CreateDrawingSession()) lower.DrawDesktopFrame(drawing, target.Size.Width, target.Size.Height);
                        byte[] pixels = target.GetPixelBytes();
                        visiblePixels = Enumerable.Range(0, pixels.Length / 4).Count(index => pixels[index * 4 + 3] > 0);
                    }
                    if (visiblePixels == 0) throw new InvalidOperationException("Presenter produced no pixels for " + action);
                    using (var stream = File.Create(Path.Combine(output, "lol-cf-" + action + ".png")))
                        await target.SaveAsync(stream.AsRandomAccessStream(), CanvasBitmapFileFormat.Png);
                    report.Add($"PASS: id={cases[count].Id}; generation={captured.SourceGeneration}; kill_count={count}; action={lower._currentCodeAsset.Action}; visiblePixels={visiblePixels}; mainSHA256={mainHash}");
                    File.WriteAllLines(Path.Combine(output, "results.txt"), report);
                }
                report.Add("PASS: all five captured tiers selected distinct CF default main images and rendered nonempty desktop frames.");
                File.WriteAllLines(Path.Combine(output, "pass.txt"), report);
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
}

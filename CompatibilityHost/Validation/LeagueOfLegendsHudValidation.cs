using KillConfirmCompatibility.Desktop.Runtime;
using KillConfirmCompatibility.Services;
using Microsoft.Graphics.Canvas;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace KillConfirmCompatibility.Controls
{
    public sealed partial class KillConfirmAnimation
    {
        internal static async Task ValidateLeagueOfLegendsHudAsync(string output, string assetsRoot)
        {
            Directory.CreateDirectory(output);
            DesktopStorage.TestDataRoot = Path.Combine(output, "isolated-profile");
            DesktopStorage.AssetsRoot = Path.GetFullPath(assetsRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            KillFeedbackVisibilitySettingsStore.Save(
                GameStyleMode.LeagueOfLegends,
                new KillFeedbackVisibilitySettingsValues
                {
                    UpperEnabled = true,
                    LowerEnabled = true,
                    CrosshairEnabled = true
                });
            GameStyleService.Current = GameStyleMode.LeagueOfLegends;
            ConfigureIconPack("leagueoflegends");
            ConfigurePlaybackFps(60);

            var presenter = new FeedbackPresenter();
            var animations = new[]
            {
                presenter.LowerFeedbackAnimation,
                presenter.LowerBadgeAnimation,
                presenter.CrosshairFeedbackAnimation,
                presenter.UpperFeedbackAnimation
            };
            foreach (var anim in animations) anim.UseDesktopFrameClock();

            var report = new List<string>();
            try
            {
                await presenter.ApplyConfigurationAsync();
                if (!string.IsNullOrEmpty(presenter.ConfigurationError))
                    throw new InvalidOperationException("Presenter error: " + presenter.ConfigurationError);
                if (!presenter.LowerFeedbackAnimation.IsCurrentPackPreloaded)
                    throw new InvalidOperationException("LeagueOfLegends built-in icon pack did not preload.");

                // Case 1: LOL Native Pentakill (SingleIconAnnouncementStyle + Slam + Callout with 3 Assisters)
                var pentaLol = new KillEvent
                {
                    Source = KillEventSources.Lol,
                    EventChannel = "combat",
                    EventKind = "kill",
                    KillCount = 5,
                    PlayMainAnimation = true,
                    PlayerName = "Faker",
                    TargetName = "Chovy",
                    Lol = new LolEventDetail
                    {
                        Event = "champion_kill",
                        Relation = "self",
                        LocalRole = "killer",
                        Multikill = 5,
                        Actor = "Faker",
                        Victim = "Chovy",
                        ActorChampion = "Ahri",
                        VictimChampion = "Zed",
                        Assisters = new[] { "Oner", "Gumayusi", "Keria" },
                        AssisterChampions = new[] { "LeeSin", "Jinx", "Lux" }
                    }
                };
                presenter.HandleKillEvent(pentaLol);
                await Task.Delay(140);
                int upperPentaPx = await RenderLayerPngAsync(presenter.UpperFeedbackAnimation, 760, 300, Path.Combine(output, "lol-upper-pentakill.png"));
                int lowerPentaPx = await RenderLayerPngAsync(presenter.LowerFeedbackAnimation, 310, 300, Path.Combine(output, "lol-lower-pentakill-assisters.png"));
                int crossPentaPx = await RenderLayerPngAsync(presenter.CrosshairFeedbackAnimation, 360, 220, Path.Combine(output, "lol-crosshair-pentakill.png"));
                if (upperPentaPx == 0 || lowerPentaPx == 0 || crossPentaPx == 0)
                    throw new InvalidOperationException($"Pentakill layer empty: upper={upperPentaPx}, lower={lowerPentaPx}, cross={crossPentaPx}");
                report.Add($"PASS: LOL Pentakill rendered (upperPx={upperPentaPx}, lowerPx={lowerPentaPx}, crosshairPx={crossPentaPx})");

                // Case 2: TwoIconAnnouncementStyle (First Blood + Enemy Shutdown + Ally Kill + CS2 Headshot Stack -> 4-slot feed)
                foreach (var anim in animations) anim.StopDesktopPlayback();
                var fbEvent = new KillEvent
                {
                    Source = KillEventSources.Lol,
                    EventChannel = "combat",
                    EventKind = "kill",
                    KillCount = 1,
                    IsFirstKill = true,
                    PlayMainAnimation = true,
                    PlayerName = "Zeus",
                    TargetName = "Bin",
                    Lol = new LolEventDetail
                    {
                        Event = "champion_kill",
                        Relation = "self",
                        LocalRole = "killer",
                        Actor = "Zeus",
                        Victim = "Bin",
                        ActorChampion = "Yone",
                        VictimChampion = "Darius"
                    }
                };
                var allyKillObs = new KillEvent
                {
                    Source = KillEventSources.Lol,
                    EventChannel = "observation",
                    EventKind = "lol_champion_kill",
                    PlayerName = "Gumayusi",
                    TargetName = "Elk",
                    Lol = new LolEventDetail
                    {
                        Event = "champion_kill",
                        Relation = "ally",
                        LocalRole = "none",
                        Actor = "Gumayusi",
                        Victim = "Elk",
                        ActorChampion = "Jinx",
                        VictimChampion = "Vayne",
                        Assisters = new[] { "Keria" },
                        AssisterChampions = new[] { "Lux" }
                    }
                };
                var enemyKillObs = new KillEvent
                {
                    Source = KillEventSources.Lol,
                    EventChannel = "observation",
                    EventKind = "lol_champion_kill",
                    PlayerName = "Knight",
                    TargetName = "Oner",
                    Lol = new LolEventDetail
                    {
                        Event = "champion_kill",
                        Relation = "enemy",
                        LocalRole = "none",
                        Actor = "Knight",
                        Victim = "Oner",
                        ActorChampion = "Zed",
                        VictimChampion = "LeeSin"
                    }
                };
                var cs2Shutdown = new KillEvent
                {
                    Source = KillEventSources.Cs2,
                    EventChannel = "combat",
                    EventKind = "kill",
                    KillCount = 1,
                    IsHeadshot = true,
                    IsRevenge = true,
                    PlayMainAnimation = true,
                    PlayerName = "s1mple",
                    TargetName = "NiKo",
                    WeaponName = "AWP",
                    MoneyReward = 300
                };

                presenter.HandleKillEvent(allyKillObs);
                await Task.Delay(30);
                presenter.HandleKillEvent(enemyKillObs);
                await Task.Delay(30);
                presenter.HandleKillEvent(cs2Shutdown);
                await Task.Delay(160);
                int upperTwoIconShutdownPx = await RenderLayerPngAsync(presenter.UpperFeedbackAnimation, 760, 300, Path.Combine(output, "lol-upper-twoicon-shutdown.png"));
                presenter.HandleKillEvent(fbEvent);
                await Task.Delay(180);

                int upperFirstBloodPx = await RenderLayerPngAsync(presenter.UpperFeedbackAnimation, 760, 300, Path.Combine(output, "lol-upper-firstblood.png"));
                int lowerFourSlotPx = await RenderLayerPngAsync(presenter.LowerFeedbackAnimation, 310, 300, Path.Combine(output, "lol-lower-4slot-mixed-sources.png"));
                if (upperTwoIconShutdownPx == 0 || upperFirstBloodPx == 0 || lowerFourSlotPx == 0)
                    throw new InvalidOperationException($"TwoIcon / 4-slot feed empty: shutdown={upperTwoIconShutdownPx}, fb={upperFirstBloodPx}, lower={lowerFourSlotPx}");
                report.Add($"PASS: TwoIcon Shutdown + FirstBlood + 4-slot mixed LOL/CS2 Callout feed rendered (shutdownPx={upperTwoIconShutdownPx}, fbPx={upperFirstBloodPx}, lowerPx={lowerFourSlotPx})");

                // Case 2b: TwoIconAnnouncementStyle MultiKill Crest (QuadraKillBlueBG + Left/Right flanking portraits)
                foreach (var anim in animations) anim.StopDesktopPlayback();
                var quadraCs2 = new KillEvent
                {
                    Source = KillEventSources.Cs2,
                    EventChannel = "combat",
                    EventKind = "kill",
                    KillCount = 4,
                    IsHeadshot = true,
                    PlayMainAnimation = true,
                    PlayerName = "s1mple",
                    TargetName = "ZywOo",
                    MoneyReward = 300
                };
                presenter.HandleKillEvent(quadraCs2);
                await Task.Delay(180);
                int quadraUpperPx = await RenderLayerPngAsync(presenter.UpperFeedbackAnimation, 760, 300, Path.Combine(output, "lol-upper-twoicon-quadrakill.png"));
                if (quadraUpperPx == 0)
                    throw new InvalidOperationException("QuadraKill TwoIcon banner rendered 0 pixels.");
                report.Add($"PASS: CS2 QuadraKill TwoIcon crest banner rendered (upperPx={quadraUpperPx})");

                // Case 3: Elemental Dragon (Infernal) Objective Banner
                foreach (var anim in animations) anim.StopDesktopPlayback();
                var dragonObs = new KillEvent
                {
                    Source = KillEventSources.Lol,
                    EventChannel = "observation",
                    EventKind = "lol_dragon_kill",
                    PlayerName = "Oner",
                    Lol = new LolEventDetail
                    {
                        Event = "dragon_kill",
                        Relation = "ally",
                        LocalRole = "killer",
                        Actor = "Oner",
                        ActorChampion = "LeeSin",
                        DragonType = "Fire",
                        Stolen = true
                    }
                };
                presenter.HandleKillEvent(dragonObs);
                await Task.Delay(160);
                int dragonUpperPx = await RenderLayerPngAsync(presenter.UpperFeedbackAnimation, 760, 300, Path.Combine(output, "lol-upper-infernal-dragon.png"));
                if (dragonUpperPx == 0)
                    throw new InvalidOperationException("Infernal Dragon banner rendered 0 pixels.");
                report.Add($"PASS: Infernal Dragon stolen banner rendered (upperPx={dragonUpperPx})");

                File.WriteAllLines(Path.Combine(output, "pass.txt"), report);
            }
            finally
            {
                foreach (var anim in animations)
                {
                    anim.StopDesktopPlayback();
                    anim.ReleaseAnimationResourcesForPackChange();
                }
            }
        }

        private static async Task<int> RenderLayerPngAsync(KillConfirmAnimation anim, int width, int height, string pngPath)
        {
            anim.AdvanceDesktopFrame();
            using var target = new CanvasRenderTarget(CanvasDevice.GetSharedDevice(), width, height, 96);
            using (var ds = target.CreateDrawingSession())
            {
                ds.Clear(Colors.Transparent);
                anim.DrawDesktopFrame(ds, width, height);
            }
            byte[] bytes = target.GetPixelBytes();
            int visible = 0;
            for (int i = 3; i < bytes.Length; i += 4)
            {
                if (bytes[i] > 0) visible++;
            }
            using (var stream = File.Create(pngPath))
            {
                await target.SaveAsync(stream.AsRandomAccessStream(), CanvasBitmapFileFormat.Png);
            }
            return visible;
        }
    }
}

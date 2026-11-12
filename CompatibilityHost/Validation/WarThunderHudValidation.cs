using KillConfirmCompatibility.Desktop.Runtime;
using KillConfirmCompatibility.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace KillConfirmCompatibility.Controls
{
    public sealed partial class KillConfirmAnimation
    {
        internal static async Task ValidateWarThunderHudAsync(string output, string assetsRoot)
        {
            Directory.CreateDirectory(output);
            DesktopStorage.TestDataRoot = Path.Combine(output, "isolated-profile");
            DesktopStorage.AssetsRoot = Path.GetFullPath(assetsRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            KillFeedbackVisibilitySettingsStore.Save(
                GameStyleMode.WarThunder,
                new KillFeedbackVisibilitySettingsValues
                {
                    UpperEnabled = true,
                    LowerEnabled = true,
                    CrosshairEnabled = true
                });
            GameStyleService.Current = GameStyleMode.WarThunder;
            ConfigureIconPack("warthunder");
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
                    throw new InvalidOperationException("WarThunder built-in icon pack did not preload.");

                // Case 1: Tank mode + Sight Reticle + CS2 Headshot Triple Kill (Ammo Explosion X-Ray + Multi-kill Streak + Sight Reticle)
                WarThunderSettingsStore.SaveBattleMode(WarThunderSettingsStore.BattleModeTank);
                WarThunderSettingsStore.SaveHudLanguage(WarThunderSettingsStore.LanguageSimplifiedChinese);
                WarThunderSettingsStore.SaveLowerDisplayMode(WarThunderSettingsStore.LowerXrayAndLog);
                WarThunderSettingsStore.SaveCrosshairStyle(WarThunderSettingsStore.CrosshairReticle);
                WarThunderSettingsStore.SaveDamagedPartsEnabled(true);
                WarThunderSettingsStore.SaveWorldKillMarkEnabled(true);
                WarThunderSettingsStore.SaveStreakAllKillsEnabled(true);

                var cs2TankKill = new KillEvent
                {
                    Source = KillEventSources.Cs2,
                    EventChannel = "combat",
                    EventKind = "kill",
                    KillCount = 3,
                    IsHeadshot = true,
                    IsRevenge = true,
                    PlayMainAnimation = true,
                    PlayerName = "s1mple",
                    TargetName = "ZywOo",
                    WeaponName = "AWP",
                    MoneyReward = 300
                };
                presenter.HandleKillEvent(cs2TankKill);
                await Task.Delay(90);
                int tankUpperPx = await RenderLayerPngAsync(presenter.UpperFeedbackAnimation, 860, 210, Path.Combine(output, "wt-tank-upper-triple-headshot.png"));
                int tankLowerPx = await RenderLayerPngAsync(presenter.LowerFeedbackAnimation, 560, 380, Path.Combine(output, "wt-tank-lower-ammo-explosion.png"));
                int tankCrossPx = await RenderLayerPngAsync(presenter.CrosshairFeedbackAnimation, 360, 360, Path.Combine(output, "wt-tank-crosshair-reticle-hitmark.png"));
                if (tankUpperPx == 0 || tankLowerPx == 0 || tankCrossPx == 0)
                    throw new InvalidOperationException($"Tank CS2 triple headshot layer empty: upper={tankUpperPx}, lower={tankLowerPx}, cross={tankCrossPx}");
                report.Add($"PASS: Tank mode CS2 AWP Triple Headshot rendered (upperPx={tankUpperPx}, lowerPx={tankLowerPx}, crosshairPx={tankCrossPx})");

                // Case 2: Air mode + English HUD + LOL Pentakill
                foreach (var anim in animations) anim.StopDesktopPlayback();
                WarThunderSettingsStore.SaveBattleMode(WarThunderSettingsStore.BattleModeAir);
                WarThunderSettingsStore.SaveHudLanguage(WarThunderSettingsStore.LanguageEnglish);
                WarThunderSettingsStore.SaveCrosshairStyle(WarThunderSettingsStore.CrosshairMinimal);

                var lolAirPenta = new KillEvent
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
                        VictimChampion = "Zed"
                    }
                };
                presenter.HandleKillEvent(lolAirPenta);
                await Task.Delay(90);
                int airUpperPx = await RenderLayerPngAsync(presenter.UpperFeedbackAnimation, 860, 210, Path.Combine(output, "wt-air-upper-lol-pentakill-en.png"));
                int airLowerPx = await RenderLayerPngAsync(presenter.LowerFeedbackAnimation, 560, 380, Path.Combine(output, "wt-air-lower-lol-pentakill-en.png"));
                int airCrossPx = await RenderLayerPngAsync(presenter.CrosshairFeedbackAnimation, 360, 360, Path.Combine(output, "wt-air-crosshair-lol-pentakill-en.png"));
                if (airUpperPx == 0 || airLowerPx == 0 || airCrossPx == 0)
                    throw new InvalidOperationException($"Air LOL pentakill layer empty: upper={airUpperPx}, lower={airLowerPx}, cross={airCrossPx}");
                report.Add($"PASS: Air mode (EN) LOL Pentakill rendered (upperPx={airUpperPx}, lowerPx={airLowerPx}, crosshairPx={airCrossPx})");

                // Case 3: Ship mode + Chinese HUD + LOL Baron Steal Objective (Citadel Banner + Objective Steal Streak)
                foreach (var anim in animations) anim.StopDesktopPlayback();
                WarThunderSettingsStore.SaveBattleMode(WarThunderSettingsStore.BattleModeShip);
                WarThunderSettingsStore.SaveHudLanguage(WarThunderSettingsStore.LanguageSimplifiedChinese);

                var baronStealObs = new KillEvent
                {
                    Source = KillEventSources.Lol,
                    EventChannel = "observation",
                    EventKind = "lol_baron_kill",
                    PlayerName = "Oner",
                    Lol = new LolEventDetail
                    {
                        Event = "baron_kill",
                        Relation = "self",
                        LocalRole = "killer",
                        Actor = "Oner",
                        ActorChampion = "LeeSin",
                        Stolen = true
                    }
                };
                presenter.HandleKillEvent(baronStealObs);
                await Task.Delay(90);
                int shipUpperPx = await RenderLayerPngAsync(presenter.UpperFeedbackAnimation, 860, 210, Path.Combine(output, "wt-ship-upper-baron-steal.png"));
                int shipLowerPx = await RenderLayerPngAsync(presenter.LowerFeedbackAnimation, 560, 380, Path.Combine(output, "wt-ship-lower-citadel-baron.png"));
                int shipCrossPx = await RenderLayerPngAsync(presenter.CrosshairFeedbackAnimation, 360, 360, Path.Combine(output, "wt-ship-crosshair-baron-steal.png"));
                if (shipUpperPx == 0 || shipLowerPx == 0 || shipCrossPx == 0)
                    throw new InvalidOperationException($"Ship LOL Baron steal layer empty: upper={shipUpperPx}, lower={shipLowerPx}, cross={shipCrossPx}");
                report.Add($"PASS: Ship mode (ZH) LOL Baron Steal rendered (upperPx={shipUpperPx}, lowerPx={shipLowerPx}, crosshairPx={shipCrossPx})");

                // Case 4: Infantry mode + Log-only Lower + CS2 Knife FirstBlood + Bomb Plant + Assist + Round Loss Ricochet
                foreach (var anim in animations) anim.StopDesktopPlayback();
                WarThunderSettingsStore.SaveBattleMode(WarThunderSettingsStore.BattleModeInfantry);
                WarThunderSettingsStore.SaveLowerDisplayMode(WarThunderSettingsStore.LowerLogOnly);

                var knifeFirstKill = new KillEvent
                {
                    Source = KillEventSources.Cs2,
                    EventChannel = "combat",
                    EventKind = "kill",
                    KillCount = 1,
                    IsKnifeKill = true,
                    IsFirstKill = true,
                    PlayMainAnimation = true,
                    PlayerName = "donk",
                    TargetName = "m0NESY",
                    WeaponName = "knife_karambit",
                    MoneyReward = 1500
                };
                var bombPlant = new KillEvent
                {
                    Source = KillEventSources.Cs2,
                    EventChannel = "economy",
                    EventKind = "bomb_plant",
                    PlayerName = "donk",
                    MoneyReward = 300
                };
                presenter.HandleKillEvent(knifeFirstKill);
                await Task.Delay(40);
                presenter.HandleKillEvent(bombPlant);
                await Task.Delay(80);
                int infUpperPx = await RenderLayerPngAsync(presenter.UpperFeedbackAnimation, 860, 210, Path.Combine(output, "wt-infantry-upper-knife-plant.png"));
                int infLowerLogPx = await RenderLayerPngAsync(presenter.LowerFeedbackAnimation, 560, 380, Path.Combine(output, "wt-infantry-lower-log-only.png"));
                if (infUpperPx == 0 || infLowerLogPx == 0)
                    throw new InvalidOperationException($"Infantry log-only layer empty: upper={infUpperPx}, lower={infLowerLogPx}");

                WarThunderSettingsStore.SaveLowerDisplayMode(WarThunderSettingsStore.LowerXrayOnly);
                var roundLoss = new KillEvent
                {
                    Source = KillEventSources.Cs2,
                    EventChannel = "economy",
                    EventKind = "round_loss",
                    MoneyReward = 1900
                };
                presenter.HandleKillEvent(roundLoss);
                await Task.Delay(70);
                int ricochetLowerPx = await RenderLayerPngAsync(presenter.LowerFeedbackAnimation, 560, 380, Path.Combine(output, "wt-infantry-lower-xray-ricochet.png"));
                int armorBlockCrossPx = await RenderLayerPngAsync(presenter.CrosshairFeedbackAnimation, 360, 360, Path.Combine(output, "wt-infantry-crosshair-armor-block.png"));
                if (ricochetLowerPx == 0 || armorBlockCrossPx == 0)
                    throw new InvalidOperationException($"Ricochet/ArmorBlock layer empty: lower={ricochetLowerPx}, cross={armorBlockCrossPx}");
                report.Add($"PASS: Infantry mode Log-Only & XRay-Only Ricochet/ArmorBlock rendered (upperPx={infUpperPx}, logOnlyPx={infLowerLogPx}, ricochetPx={ricochetLowerPx}, armorBlockPx={armorBlockCrossPx})");

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
    }
}

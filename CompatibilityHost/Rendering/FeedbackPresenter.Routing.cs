using System;
using KillConfirmCompatibility.Services;
using Windows.UI.Xaml;

namespace KillConfirmCompatibility
{
    internal sealed partial class FeedbackPresenter
    {
        public void HandleKillEvent(KillEvent killEvent)
        {
            if (!LolEventAdapter.TryPrepareForPlayback(killEvent, GameStyleService.Current, out killEvent)) return;
            if (killEvent.PublishedUnixMs > 0)
            {
                ulong nowMs = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                ulong totalMs = nowMs > killEvent.PublishedUnixMs
                    ? nowMs - killEvent.PublishedUnixMs
                    : 0;
                App.Log("[perf] publish_to_animation_ms=" + totalMs
                    + ", kills=" + killEvent.KillCount
                    + ", channel=" + killEvent.EventChannel);
            }

            // Danmaku has its own event classifier and reaction policy. Route every
            // service event before style-specific animation filtering so economy,
            // objective, assist, death and kill reactions stay independent.
            DanmakuEvent?.Invoke(killEvent);

            // An explicitly disabled icon pack leaves audio and danmaku independent.
            if (!Controls.KillConfirmAnimation.IsIconFeedbackEnabled) return;

            if (FeedbackRoutingPolicy.IsIgnoredByStyles(killEvent))
            {
                return;
            }

            GameStyleMode style = GameStyleService.Current;
            if (!CanStyleConsumeEvent(style, killEvent))
            {
                return;
            }

            if (FeedbackRoutingPolicy.ShouldPlayPrimaryAnimation(style, killEvent))
            {
                PlayPrimaryAnimation(killEvent);
            }

            PlayBadgeAnimation(killEvent);

        }

        private void PlayPrimaryAnimation(KillEvent killEvent)
        {
            GameStyleMode style = GameStyleService.Current;
            if (!FeedbackRoutingPolicy.PassesPrimaryGuard(style, killEvent))
            {
                return;
            }

            KillFeedbackVisibilitySettingsValues visibility =
                KillFeedbackVisibilitySettingsStore.Load(style);
            bool usesDedicatedLayerRouting = style == GameStyleMode.Overwatch
                || style == GameStyleMode.ModernWarfare2019
                || style == GameStyleMode.Cod2022
                || style == GameStyleMode.Apex
                || style == GameStyleMode.Titanfall2
                || style == GameStyleMode.LeagueOfLegends
                || style == GameStyleMode.Rdr2;
            if (!usesDedicatedLayerRouting)
            {
                ConfigureFeedbackAppearance(
                    LowerFeedbackAnimation,
                    visibility,
                    KillFeedbackLayer.Lower);
                if (!visibility.LowerEnabled)
                {
                    // The optional crosshair remains independent from the lower
                    // game-specific feedback layer.
                    PlayAuxiliaryKillMarkIfEnabled(killEvent);
                    return;
                }
            }

            switch (GameStyleService.Current)
            {
                case GameStyleMode.Valorant:
                    PlayValorantPrimaryAnimation(killEvent);
                    return;
                case GameStyleMode.Overwatch:
                    if (visibility.CrosshairEnabled && !killEvent.IsAssist)
                    {
                        ConfigureFeedbackAppearance(
                            CrosshairFeedbackAnimation,
                            visibility,
                            KillFeedbackLayer.Crosshair);
                        CrosshairFeedbackAnimation.PlayOverwatchCrosshairKill();
                    }
                    if (visibility.LowerEnabled)
                    {
                        ConfigureFeedbackAppearance(
                            LowerFeedbackAnimation,
                            visibility,
                            KillFeedbackLayer.Lower);
                        LowerFeedbackAnimation.PlayOverwatchLowerThirdKill(
                            GetKillTargetDisplayName(killEvent),
                            killEvent.IsAssist);
                    }
                    return;
                case GameStyleMode.ModernWarfare2019:
                    if (killEvent.IsEconomyEvent)
                    {
                        if (visibility.CrosshairEnabled)
                        {
                            ConfigureFeedbackAppearance(
                                CrosshairFeedbackAnimation,
                                visibility,
                                KillFeedbackLayer.Crosshair);
                            string eventKind = LolEventAdapter.IsLolObjectiveLabel(killEvent.WeaponName) ? killEvent.WeaponName : (killEvent.EventKind ?? killEvent.AnimationKey);
                            CrosshairFeedbackAnimation.PlayModernWarfare2019Objective(
                                eventKind,
                                killEvent.MoneyReward);
                        }
                        return;
                    }
                    if (killEvent.IsAssist)
                    {
                        if (visibility.CrosshairEnabled)
                        {
                            ConfigureFeedbackAppearance(
                                CrosshairFeedbackAnimation,
                                visibility,
                                KillFeedbackLayer.Crosshair);
                            CrosshairFeedbackAnimation.PlayModernWarfare2019Assist();
                        }
                        return;
                    }
                    if (visibility.CrosshairEnabled)
                    {
                        ConfigureFeedbackAppearance(
                            CrosshairFeedbackAnimation,
                            visibility,
                            KillFeedbackLayer.Crosshair);
                        CrosshairFeedbackAnimation.PlayModernWarfare2019CrosshairKill(
                            killEvent.IsHeadshot,
                            killEvent.KillCount,
                            killEvent.MoneyReward);
                    }
                    if (visibility.LowerEnabled)
                    {
                        ConfigureFeedbackAppearance(
                            LowerFeedbackAnimation,
                            visibility,
                            KillFeedbackLayer.Lower);
                        LowerFeedbackAnimation.PlayModernWarfare2019LowerKill(
                            killEvent.KillCount);
                    }
                    if (visibility.UpperEnabled)
                    {
                        ConfigureFeedbackAppearance(
                            UpperFeedbackAnimation,
                            visibility,
                            KillFeedbackLayer.Upper);
                        UpperFeedbackAnimation.PlayModernWarfare2019UpperKill(
                            killEvent.KillCount);
                    }
                    return;
                case GameStyleMode.Apex:
                    if (visibility.CrosshairEnabled && !killEvent.IsAssist)
                    {
                        ConfigureFeedbackAppearance(
                            CrosshairFeedbackAnimation,
                            visibility,
                            KillFeedbackLayer.Crosshair);
                        CrosshairFeedbackAnimation.PlayApexCrosshairKill(
                            killEvent.IsHeadshot,
                            killEvent.MoneyReward,
                            killEvent.KillCount);
                    }
                    if (visibility.LowerEnabled)
                    {
                        ConfigureFeedbackAppearance(
                            LowerFeedbackAnimation,
                            visibility,
                            KillFeedbackLayer.Lower);
                        LowerFeedbackAnimation.PlayApexFeedCard(
                            killEvent.IsAssist,
                            GetKillTargetDisplayName(killEvent),
                            killEvent.MoneyReward);
                    }
                    return;
                case GameStyleMode.Cod2022:
                    PlayCod2022PrimaryAnimation(killEvent, visibility);
                    return;
                case GameStyleMode.Titanfall2:
                    PlayTitanfall2PrimaryAnimation(killEvent, visibility);
                    return;
                case GameStyleMode.LeagueOfLegends:
                    PlayLeagueOfLegendsPrimaryAnimation(killEvent, visibility);
                    return;
                case GameStyleMode.Rdr2:
                    PlayRdr2PrimaryAnimation(killEvent, visibility);
                    return;
                case GameStyleMode.Csol:
                    PlayCsolPrimaryAnimation(killEvent);
                    return;
                case GameStyleMode.Battlefield1:
                    PlayBattlefield1PrimaryAnimation(killEvent);
                    return;
                case GameStyleMode.Battlefield5:
                    PlayBattlefield5PrimaryAnimation(killEvent);
                    return;
                case GameStyleMode.Battlefield4:
                    PlayBattlefield4PrimaryAnimation(killEvent);
                    return;
                case GameStyleMode.Battlefield2042:
                    PlayBattlefield2042PrimaryAnimation(killEvent);
                    return;
                case GameStyleMode.Battlefield6:
                    PlayBattlefield6PrimaryAnimation(killEvent);
                    break;
                case GameStyleMode.Pubg:
                    PlayPubgPrimaryAnimation(killEvent);
                    return;
                case GameStyleMode.DeltaForce:
                    PlayDeltaForcePrimaryAnimation(killEvent);
                    return;
                case GameStyleMode.Calabiyau:
                    if (GameStyleService.IsCalabiyauKillEvent(killEvent))
                    {
                        PlayAuxiliaryKillMarkIfEnabled(killEvent);
                        LowerFeedbackAnimation.PlayCalabiyauKill(killEvent.KillCount, killEvent.IsHeadshot, killEvent.IsKnifeKill, killEvent.IsGrenadeKill, killEvent.PublishedUnixMs);
                    }
                    return;
                case GameStyleMode.CustomModule:
                    if (!killEvent.IsAssist && !killEvent.IsEconomyEvent && !IsBombObjectiveEvent(killEvent))
                    {
                        PlayAuxiliaryKillMarkIfEnabled(killEvent);
                        LowerFeedbackAnimation.PlayCustomKill(killEvent.KillCount, killEvent.IsHeadshot);
                    }
                    break;
                case GameStyleMode.Doubao:
                    PlayDoubaoPrimaryAnimation(killEvent);
                    return;
                case GameStyleMode.Sanguosha:
                    PlaySanguoshaPrimaryAnimation(killEvent);
                    return;
                case GameStyleMode.Dagoujiao:
                    PlayDagoujiaoPrimaryAnimation(killEvent);
                    return;
                case GameStyleMode.Crossfire:
                default:
                    PlayCrossfirePrimaryAnimation(killEvent);
                    return;
            }
        }

    }
}

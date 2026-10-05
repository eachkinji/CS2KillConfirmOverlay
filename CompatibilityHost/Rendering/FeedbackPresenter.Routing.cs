using System;
using KillConfirmCompatibility.Services;
using Windows.UI.Xaml;

namespace KillConfirmCompatibility
{
    internal sealed partial class FeedbackPresenter
    {
        public void HandleKillEvent(KillEvent killEvent)
        {
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

            if (string.Equals(killEvent.EventKind, "player_death", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            GameStyleMode style = GameStyleService.Current;
            if (!CanStyleConsumeEvent(style, killEvent))
            {
                return;
            }

            bool isCrossfireObjective = style == GameStyleMode.Crossfire && IsBombObjectiveEvent(killEvent);
            bool isModernWarfare2019Objective = style == GameStyleMode.ModernWarfare2019
                && killEvent != null
                && killEvent.IsEconomyEvent;

            bool shouldPlayPrimaryAnimation = (killEvent.IsCombatEvent && killEvent.PlayMainAnimation)
                || (IsEconomyPresentationStyle(style) && IsBattlefieldTextEvent(killEvent))
                || (style == GameStyleMode.Csol && killEvent.IsCombatEvent)
                || (style == GameStyleMode.ModernWarfare2019 && (killEvent.IsCombatEvent || isModernWarfare2019Objective))
                || (style == GameStyleMode.Overwatch && killEvent.IsCombatEvent && killEvent.IsAssist)
                || (style == GameStyleMode.Apex && killEvent.IsCombatEvent && killEvent.IsAssist)
                || isCrossfireObjective;
            if (shouldPlayPrimaryAnimation)
            {
                PlayPrimaryAnimation(killEvent);
            }

            PlayBadgeAnimation(killEvent);

        }

        private void PlayPrimaryAnimation(KillEvent killEvent)
        {
            GameStyleMode style = GameStyleService.Current;
            bool isCsolAssist = GameStyleService.Current == GameStyleMode.Csol
                && killEvent != null
                && killEvent.IsAssist;
            bool isApexAssist = GameStyleService.Current == GameStyleMode.Apex
                && killEvent != null
                && killEvent.IsAssist;
            bool isOverwatchAssist = GameStyleService.Current == GameStyleMode.Overwatch
                && killEvent != null
                && killEvent.IsAssist;
            bool isModernWarfare2019Assist = GameStyleService.Current == GameStyleMode.ModernWarfare2019
                && killEvent != null
                && killEvent.IsAssist;
            bool isCrossfireObjective = style == GameStyleMode.Crossfire && IsBombObjectiveEvent(killEvent);
            bool isModernWarfare2019Objective = style == GameStyleMode.ModernWarfare2019
                && killEvent != null
                && killEvent.IsEconomyEvent;

            if (!CanStyleConsumeEvent(GameStyleService.Current, killEvent)
                || (killEvent.KillCount <= 0
                    && !IsBattlefieldTextEvent(killEvent)
                    && !isCsolAssist
                    && !isApexAssist
                    && !isOverwatchAssist
                    && !isModernWarfare2019Assist
                    && !isCrossfireObjective
                    && !isModernWarfare2019Objective))
            {
                return;
            }

            KillFeedbackVisibilitySettingsValues visibility =
                KillFeedbackVisibilitySettingsStore.Load(style);
            bool usesDedicatedLayerRouting = style == GameStyleMode.Overwatch
                || style == GameStyleMode.ModernWarfare2019
                || style == GameStyleMode.Apex;
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
                            string eventKind = killEvent.EventKind ?? killEvent.AnimationKey;
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
                case GameStyleMode.Pubg:
                    PlayPubgPrimaryAnimation(killEvent);
                    return;
                case GameStyleMode.DeltaForce:
                    PlayDeltaForcePrimaryAnimation(killEvent);
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

using System;
using KillConfirmCompatibility.Services;
using Windows.UI.Xaml;

namespace KillConfirmCompatibility
{
    internal sealed partial class FeedbackPresenter
    {
        private void PlayValorantPrimaryAnimation(KillEvent killEvent)
        {
            PlayAuxiliaryKillMarkIfEnabled(killEvent);
            string valorantPack = GetSelectedIconPack();
            if (!ValorantPackService.IsValorantPackKey(valorantPack))
            {
                valorantPack = GetSelectedVoicePackPreset();
            }

            LowerFeedbackAnimation.PlayNativeValorantKill(
                valorantPack,
                killEvent.KillCount,
                killEvent.IsHeadshot);
        }

        private static string GetKillTargetDisplayName(KillEvent killEvent)
        {
            string targetName = killEvent?.TargetName;
            return string.IsNullOrWhiteSpace(targetName)
                ? "\u654c\u65b9\u73a9\u5bb6"
                : targetName.Trim();
        }

        private void PlayBattlefield1PrimaryAnimation(KillEvent killEvent)
        {
            PlayBattlefieldKillMarkIfEnabled(killEvent);
            LowerFeedbackAnimation.PlayBattlefield1Kill(
                killEvent.KillCount,
                killEvent.IsHeadshot,
                killEvent.IsKnifeKill,
                killEvent.IsAssist,
                GetKillTargetDisplayName(killEvent),
                GetBattlefieldWeaponLabel(killEvent),
                killEvent.MoneyReward,
                GetBattlefieldEventKind(killEvent),
                killEvent.RoundNumber,
                killEvent.MoneyEpoch);
        }

        private void PlayBattlefield5PrimaryAnimation(KillEvent killEvent)
        {
            PlayBattlefieldKillMarkIfEnabled(killEvent);
            LowerFeedbackAnimation.PlayBattlefield5Kill(
                killEvent.KillCount,
                killEvent.IsHeadshot,
                killEvent.IsKnifeKill,
                killEvent.IsAssist,
                GetKillTargetDisplayName(killEvent),
                GetBattlefieldWeaponLabel(killEvent),
                killEvent.MoneyReward,
                GetBattlefieldEventKind(killEvent),
                killEvent.RoundNumber,
                killEvent.MoneyEpoch);
        }

        private void PlayBattlefield4PrimaryAnimation(KillEvent killEvent)
        {
            PlayBattlefieldKillMarkIfEnabled(killEvent);
            LowerFeedbackAnimation.PlayBattlefield4Kill(
                killEvent.KillCount,
                killEvent.IsHeadshot,
                killEvent.IsKnifeKill,
                killEvent.IsAssist,
                GetKillTargetDisplayName(killEvent),
                GetBattlefieldWeaponLabel(killEvent),
                killEvent.MoneyReward,
                GetBattlefieldEventKind(killEvent),
                killEvent.RoundNumber,
                killEvent.MoneyEpoch);
        }

        private void PlayBattlefield2042PrimaryAnimation(KillEvent killEvent)
        {
            PlayBattlefieldKillMarkIfEnabled(killEvent);
            LowerFeedbackAnimation.PlayBattlefield2042Kill(
                killEvent.KillCount,
                killEvent.IsHeadshot,
                killEvent.IsKnifeKill,
                killEvent.IsGrenadeKill,
                killEvent.IsAssist,
                GetKillTargetDisplayName(killEvent),
                GetBattlefieldWeaponLabel(killEvent),
                killEvent.MoneyReward,
                GetBattlefieldEventKind(killEvent),
                killEvent.RoundNumber,
                killEvent.MoneyEpoch);
        }

        private void PlayPubgPrimaryAnimation(KillEvent killEvent)
        {
            PlayAuxiliaryKillMarkIfEnabled(killEvent);
            LowerFeedbackAnimation.PlayPubgKill(
                killEvent.KillCount,
                killEvent.IsHeadshot,
                killEvent.IsKnifeKill,
                killEvent.IsAssist,
                GetKillTargetDisplayName(killEvent),
                GetBattlefieldWeaponLabel(killEvent),
                killEvent.MoneyReward,
                GetBattlefieldEventKind(killEvent),
                killEvent.RoundNumber,
                killEvent.MoneyEpoch);
        }

        private void PlayDeltaForcePrimaryAnimation(KillEvent killEvent)
        {
            PlayBattlefieldKillMarkIfEnabled(killEvent);
            LowerFeedbackAnimation.PlayDeltaForceKill(
                killEvent.KillCount,
                killEvent.IsHeadshot,
                killEvent.IsKnifeKill,
                killEvent.IsAssist,
                GetKillTargetDisplayName(killEvent),
                GetBattlefieldWeaponLabel(killEvent),
                killEvent.MoneyReward,
                GetBattlefieldEventKind(killEvent),
                killEvent.RoundNumber,
                killEvent.MoneyEpoch);
        }

        private void PlayBattlefieldKillMarkIfEnabled(KillEvent killEvent)
        {
            PlayAuxiliaryKillMarkIfEnabled(killEvent);
        }

        private void PlayAuxiliaryKillMarkIfEnabled(KillEvent killEvent)
        {
            GameStyleMode style = GameStyleService.Current;
            if (!GameStyleService.IsAuxiliaryKillMarkStyle(style)
                || killEvent == null
                || !killEvent.IsCombatEvent
                || killEvent.IsAssist
                || killEvent.KillCount <= 0)
            {
                return;
            }

            KillFeedbackVisibilitySettingsValues visibility =
                KillFeedbackVisibilitySettingsStore.Load(style);
            if (visibility.CrosshairEnabled)
            {
                ConfigureFeedbackAppearance(
                    CrosshairFeedbackAnimation,
                    visibility,
                    KillFeedbackLayer.Crosshair);
                CrosshairFeedbackAnimation.PlayModernWarfare2019KillMarkOnly(
                    killEvent.IsHeadshot);
            }
        }

        private void PlayDoubaoPrimaryAnimation(KillEvent killEvent)
        {
            PlayAuxiliaryKillMarkIfEnabled(killEvent);
            LowerFeedbackAnimation.PlayDoubaoKill(killEvent.KillCount);
        }

        private void PlayDagoujiaoPrimaryAnimation(KillEvent killEvent)
        {
            PlayAuxiliaryKillMarkIfEnabled(killEvent);
            LowerFeedbackAnimation.PlayDagoujiaoKill(killEvent.KillCount, killEvent.IsHeadshot);
        }

        private static void ConfigureFeedbackAppearance(
            Controls.KillConfirmAnimation animation,
            KillFeedbackVisibilitySettingsValues settings,
            KillFeedbackLayer layer)
        {
            if (animation == null)
            {
                return;
            }

            KillFeedbackVisibilitySettingsStore.GetAppearance(
                settings,
                layer,
                out double brightnessPercent,
                out double contrastPercent,
                out double opacityPercent);
            animation.ConfigureAppearance(
                brightnessPercent / 100.0,
                contrastPercent / 100.0,
                opacityPercent / 100.0);
        }

        private static bool IsBattlefieldTextEvent(KillEvent killEvent)
        {
            if (killEvent == null)
            {
                return false;
            }

            string eventKind = GetBattlefieldEventKind(killEvent);
            if (killEvent.IsCombatEvent)
            {
                return killEvent.IsAssist
                    || string.Equals(eventKind, "assist", StringComparison.OrdinalIgnoreCase);
            }

            return killEvent.IsEconomyEvent
                && (string.Equals(eventKind, "round_win", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(eventKind, "round_loss", StringComparison.OrdinalIgnoreCase)
                    || IsBattlefieldObjectiveEvent(eventKind));
        }

        private static bool IsEconomyPresentationStyle(GameStyleMode style)
        {
            return style == GameStyleMode.Battlefield1
                || style == GameStyleMode.Battlefield5
                || style == GameStyleMode.Battlefield4
                || style == GameStyleMode.Battlefield2042
                || style == GameStyleMode.Pubg
                || style == GameStyleMode.DeltaForce;
        }
    }
}

using KillConfirmGameBar.Services;

namespace KillConfirmGameBar
{
    public sealed partial class KillConfirmWidgetPage
    {
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
    }
}

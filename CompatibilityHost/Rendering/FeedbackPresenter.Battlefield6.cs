using KillConfirmCompatibility.Services;

namespace KillConfirmCompatibility
{
    internal sealed partial class FeedbackPresenter
    {
        private void PlayBattlefield6PrimaryAnimation(KillEvent killEvent)
        {
            PlayBattlefieldKillMarkIfEnabled(killEvent);
            LowerFeedbackAnimation.PlayBattlefield6Kill(
                killEvent.KillCount, killEvent.IsHeadshot, killEvent.IsKnifeKill,
                killEvent.IsGrenadeKill, killEvent.IsAssist,
                GetKillTargetDisplayName(killEvent), GetBattlefieldWeaponLabel(killEvent),
                killEvent.MoneyReward, GetBattlefieldEventKind(killEvent),
                killEvent.RoundNumber, killEvent.MoneyEpoch);
        }
    }
}

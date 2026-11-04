using KillConfirmGameBar.Services;

namespace KillConfirmGameBar
{
    public sealed partial class KillConfirmWidgetPage
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

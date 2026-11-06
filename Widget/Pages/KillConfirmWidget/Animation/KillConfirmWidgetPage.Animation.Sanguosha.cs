using KillConfirmGameBar.Services;

namespace KillConfirmGameBar
{
    public sealed partial class KillConfirmWidgetPage
    {
        // NewKillEffect.CreatePlay: the original client shows no extra crosshair mark.
        private void PlaySanguoshaPrimaryAnimation(KillEvent killEvent)
        {
            if (!GameStyleService.IsSanguoshaKillEvent(killEvent)) return;
            LowerFeedbackAnimation.PlaySanguoshaKill(killEvent);
        }
    }
}

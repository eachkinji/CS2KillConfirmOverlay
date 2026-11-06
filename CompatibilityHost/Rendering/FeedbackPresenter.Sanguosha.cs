using KillConfirmCompatibility.Services;

namespace KillConfirmCompatibility
{
    internal sealed partial class FeedbackPresenter
    {
        // NewKillEffect.CreatePlay: the original client shows no extra crosshair mark.
        private void PlaySanguoshaPrimaryAnimation(KillEvent killEvent)
        {
            if (!GameStyleService.IsSanguoshaKillEvent(killEvent)) return;
            LowerFeedbackAnimation.PlaySanguoshaKill(killEvent);
        }
    }
}

using KillConfirmCompatibility.Services;

namespace KillConfirmCompatibility
{
    internal sealed partial class FeedbackPresenter
    {
        private void PlayWarThunderPrimaryAnimation(KillEvent killEvent, KillFeedbackVisibilitySettingsValues visibility)
        {
            if (killEvent == null)
            {
                return;
            }

            string targetName = GetKillTargetDisplayName(killEvent);
            Controls.KillConfirmAnimation.WarThunderCombatDispatchPayload payload =
                Controls.KillConfirmAnimation.CreateWarThunderPayload(killEvent, targetName);

            if (visibility.CrosshairEnabled)
            {
                ConfigureFeedbackAppearance(
                    CrosshairFeedbackAnimation,
                    visibility,
                    KillFeedbackLayer.Crosshair);
                CrosshairFeedbackAnimation.PlayWarThunderCrosshair(payload);
            }

            if (visibility.UpperEnabled)
            {
                ConfigureFeedbackAppearance(
                    UpperFeedbackAnimation,
                    visibility,
                    KillFeedbackLayer.Upper);
                UpperFeedbackAnimation.PlayWarThunderUpperHud(payload);
            }

            if (visibility.LowerEnabled)
            {
                ConfigureFeedbackAppearance(
                    LowerFeedbackAnimation,
                    visibility,
                    KillFeedbackLayer.Lower);
                LowerFeedbackAnimation.PlayWarThunderLowerTactical(payload);
            }
        }
    }
}

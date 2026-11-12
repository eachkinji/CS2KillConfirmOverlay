using KillConfirmGameBar.Controls;
using KillConfirmGameBar.Services;

namespace KillConfirmGameBar
{
    public sealed partial class KillConfirmWidgetPage
    {
        private void PlayWarThunderPrimaryAnimation(KillEvent killEvent, KillFeedbackVisibilitySettingsValues visibility)
        {
            if (killEvent == null)
            {
                return;
            }

            string targetName = GetKillTargetDisplayName(killEvent);
            KillConfirmAnimation.WarThunderCombatDispatchPayload payload =
                KillConfirmAnimation.CreateWarThunderPayload(killEvent, targetName);

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

using KillConfirmGameBar.Services;

namespace KillConfirmGameBar
{
    public sealed partial class KillConfirmWidgetPage
    {
        private void PlayLeagueOfLegendsPrimaryAnimation(KillEvent killEvent, KillFeedbackVisibilitySettingsValues visibility)
        {
            if (killEvent == null)
            {
                return;
            }

            if (visibility.UpperEnabled)
            {
                ConfigureFeedbackAppearance(
                    UpperFeedbackAnimation,
                    visibility,
                    KillFeedbackLayer.Upper);
                UpperFeedbackAnimation.PlayLeagueOfLegendsUpperBanner(killEvent);
            }

            if (visibility.LowerEnabled)
            {
                ConfigureFeedbackAppearance(
                    LowerFeedbackAnimation,
                    visibility,
                    KillFeedbackLayer.Lower);
                LowerFeedbackAnimation.PlayLeagueOfLegendsLowerCallout(killEvent);
            }

            if (visibility.CrosshairEnabled)
            {
                ConfigureFeedbackAppearance(
                    CrosshairFeedbackAnimation,
                    visibility,
                    KillFeedbackLayer.Crosshair);
                CrosshairFeedbackAnimation.PlayLeagueOfLegendsCrosshair(killEvent);
            }
        }
    }
}

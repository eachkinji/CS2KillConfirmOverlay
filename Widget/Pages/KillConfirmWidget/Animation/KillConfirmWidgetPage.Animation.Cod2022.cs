using KillConfirmGameBar.Services;

namespace KillConfirmGameBar
{
    public sealed partial class KillConfirmWidgetPage
    {
        private void PlayCod2022PrimaryAnimation(KillEvent killEvent, KillFeedbackVisibilitySettingsValues visibility)
        {
            if (killEvent == null)
            {
                return;
            }

            string eventKind = LolEventAdapter.IsLolObjectiveLabel(killEvent.WeaponName)
                ? killEvent.WeaponName
                : (killEvent.EventKind ?? killEvent.AnimationKey);
            string targetName = GetKillTargetDisplayName(killEvent);

            if (visibility.CrosshairEnabled)
            {
                ConfigureFeedbackAppearance(
                    CrosshairFeedbackAnimation,
                    visibility,
                    KillFeedbackLayer.Crosshair);
                CrosshairFeedbackAnimation.PlayCod2022Crosshair(
                    killEvent.KillCount,
                    killEvent.IsHeadshot,
                    killEvent.IsKnifeKill,
                    killEvent.IsGrenadeKill,
                    killEvent.IsFirstKill,
                    killEvent.IsLastKill,
                    killEvent.IsRevenge,
                    killEvent.IsAssist,
                    targetName,
                    killEvent.WeaponName,
                    killEvent.MoneyReward,
                    eventKind,
                    0,
                    killEvent.IsFromLol);
            }

            if (visibility.UpperEnabled)
            {
                ConfigureFeedbackAppearance(
                    UpperFeedbackAnimation,
                    visibility,
                    KillFeedbackLayer.Upper);
                UpperFeedbackAnimation.PlayCod2022UpperSplash(
                    killEvent.KillCount,
                    killEvent.IsHeadshot,
                    killEvent.IsKnifeKill,
                    killEvent.IsGrenadeKill,
                    killEvent.IsFirstKill,
                    killEvent.IsLastKill,
                    killEvent.IsRevenge,
                    killEvent.IsAssist,
                    eventKind,
                    killEvent.WeaponName);
            }

            if (visibility.LowerEnabled)
            {
                ConfigureFeedbackAppearance(
                    LowerFeedbackAnimation,
                    visibility,
                    KillFeedbackLayer.Lower);
                LowerFeedbackAnimation.PlayCod2022LowerBanner(
                    killEvent.KillCount,
                    killEvent.IsHeadshot,
                    killEvent.IsKnifeKill,
                    killEvent.IsGrenadeKill,
                    killEvent.IsFirstKill,
                    killEvent.IsLastKill,
                    killEvent.IsRevenge,
                    killEvent.IsAssist,
                    targetName,
                    killEvent.WeaponName,
                    eventKind,
                    killEvent.IsFromLol);
            }
        }
    }
}

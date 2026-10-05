using System;
using KillConfirmCompatibility.Services;
using Windows.UI.Xaml;

namespace KillConfirmCompatibility
{
    internal sealed partial class FeedbackPresenter
    {
        private static bool IsBombObjectiveEvent(KillEvent killEvent)
        {
            return killEvent != null
                && (string.Equals(killEvent.EventKind, "bomb_plant", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(killEvent.EventKind, "bomb_defuse", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(killEvent.AnimationKey, "bomb_plant", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(killEvent.AnimationKey, "bomb_defuse", StringComparison.OrdinalIgnoreCase));
        }

        private static bool CanStyleConsumeEvent(GameStyleMode style, KillEvent killEvent)
        {
            if (killEvent == null || (style == GameStyleMode.Csol && IsBombObjectiveEvent(killEvent)))
            {
                return false;
            }

            if (killEvent.IsCombatEvent)
            {
                return true;
            }

            if (style == GameStyleMode.Crossfire && IsBombObjectiveEvent(killEvent))
            {
                return true;
            }

            if (style == GameStyleMode.ModernWarfare2019)
            {
                string eventKind = killEvent.EventKind ?? killEvent.AnimationKey;
                if (string.Equals(eventKind, "bomb_plant", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(eventKind, "bomb_defuse", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(eventKind, "hostage_interact", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(eventKind, "hostage_rescue", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(eventKind, "round_win", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(eventKind, "round_loss", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return killEvent.IsEconomyEvent && IsEconomyPresentationStyle(style);
        }

        private static bool IsBattlefieldObjectiveEvent(string eventKind)
        {
            return string.Equals(eventKind, "bomb_plant", StringComparison.OrdinalIgnoreCase)
                || string.Equals(eventKind, "bomb_defuse", StringComparison.OrdinalIgnoreCase)
                || string.Equals(eventKind, "hostage_interact", StringComparison.OrdinalIgnoreCase)
                || string.Equals(eventKind, "hostage_rescue", StringComparison.OrdinalIgnoreCase);
        }

        private static string GetBattlefieldEventKind(KillEvent killEvent)
        {
            if (!string.IsNullOrWhiteSpace(killEvent?.EventKind))
            {
                return killEvent.EventKind;
            }

            return killEvent?.IsAssist == true ? "assist" : "kill";
        }

        private static string GetBattlefieldWeaponLabel(KillEvent killEvent)
        {
            if (!string.IsNullOrWhiteSpace(killEvent?.WeaponName))
            {
                return killEvent.WeaponName;
            }

            return killEvent?.WeaponBadgeKey;
        }

        private void PlayCsolPrimaryAnimation(KillEvent killEvent)
        {
            if (IsBombObjectiveEvent(killEvent))
            {
                return;
            }
            PlayAuxiliaryKillMarkIfEnabled(killEvent);

            string specialKey = null;
            if (killEvent.IsFirstKill)
            {
                CsolVoiceSettingsValues settings = CsolVoiceSettingsStore.Load();
                specialKey = settings.FirstKillIcon;
            }
            else if (killEvent.IsLastKill)
            {
                CsolVoiceSettingsValues settings = CsolVoiceSettingsStore.Load();
                specialKey = settings.LastKillIcon;
            }
            else if (killEvent.IsAssist)
            {
                specialKey = "assist";
            }
            else if (killEvent.IsKnifeKill)
            {
                specialKey = "melee";
            }
            else if (killEvent.IsGrenadeKill)
            {
                specialKey = "grenade_kill";
            }
            else if (killEvent.IsHeadshot)
            {
                specialKey = "headshot";
            }

            LowerFeedbackAnimation.PlayCsolKill(killEvent.KillCount, specialKey);
        }

        private void PlayCrossfirePrimaryAnimation(KillEvent killEvent)
        {
            PlayAuxiliaryKillMarkIfEnabled(killEvent);
            CrossfireGameplaySettingsValues settings = CrossfireGameplaySettingsStore.Load();
            LowerFeedbackAnimation.PlayCodeKill(
                ResolveCrossfirePrimaryAnimationKey(killEvent, settings), killEvent.WeaponBadgeKey);
        }

        private static string ResolveCrossfirePrimaryAnimationKey(
            KillEvent killEvent, CrossfireGameplaySettingsValues settings)
        {
            string eventKind = killEvent.EventKind ?? killEvent.AnimationKey;
            if (string.Equals(eventKind, "bomb_plant", StringComparison.OrdinalIgnoreCase)
                || string.Equals(killEvent.AnimationKey, "bomb_plant", StringComparison.OrdinalIgnoreCase))
            {
                return "c4";
            }
            if (string.Equals(eventKind, "bomb_defuse", StringComparison.OrdinalIgnoreCase)
                || string.Equals(killEvent.AnimationKey, "bomb_defuse", StringComparison.OrdinalIgnoreCase))
            {
                return "c4defuse";
            }

            // Event-specific art can be supplied by an imported CF pack.
            // These keys require an explicit event; a normal kill is never inferred to be a wall shot.
            switch ((killEvent.AnimationKey ?? string.Empty).ToLowerInvariant())
            {
                case "wallshot": case "headwallshot": case "headwallshot_gold": case "revenge": case "smash":
                    return killEvent.AnimationKey.ToLowerInvariant();
            }

            bool knifeIconWins = killEvent.IsKnifeKill
                && (killEvent.KillCount < 2 || settings.KnifeSpecialIconPriority);
            if (knifeIconWins)
            {
                return "knife";
            }

            bool grenadeIconWins = killEvent.IsGrenadeKill
                && (killEvent.KillCount < 2 || settings.GrenadeSpecialIconPriority);
            if (grenadeIconWins)
            {
                return "grenade";
            }

            bool explicitHeadshotIcon = string.Equals(killEvent.AnimationKey, "headshot_vvip", StringComparison.OrdinalIgnoreCase)
                || string.Equals(killEvent.AnimationKey, "headshot_gold_vvip", StringComparison.OrdinalIgnoreCase);
            bool headshotIconWins = (killEvent.IsHeadshot || explicitHeadshotIcon)
                && !killEvent.IsKnifeKill && !killEvent.IsGrenadeKill
                && (killEvent.KillCount < 2 || settings.HeadshotSpecialIconPriority);
            if (headshotIconWins)
            {
                if (explicitHeadshotIcon)
                {
                    return killEvent.AnimationKey;
                }
                bool useFirstOrLastEffect = (killEvent.IsFirstKill && settings.FirstKillEffectEnabled)
                    || (killEvent.IsLastKill && settings.LastKillEffectEnabled);
                return useFirstOrLastEffect ? "headshot_gold" : "headshot";
            }

            // Explicit streak/preview keys must not bypass special-kill priorities.
            if (string.Equals(killEvent.AnimationKey, "code2kill", StringComparison.OrdinalIgnoreCase))
            {
                return "multi2";
            }

            if (killEvent.KillCount >= 2)
            {
                int codeKillCount = Math.Max(2, Math.Min(6, killEvent.KillCount));
                return "multi" + codeKillCount;
            }

            return "multi1";
        }

        private void PlayBadgeAnimation(KillEvent killEvent)
        {
            if (killEvent == null || !killEvent.IsCombatEvent)
            {
                return;
            }

            if (GameStyleService.Current != GameStyleMode.Crossfire)
            {
                return;
            }

            KillFeedbackVisibilitySettingsValues visibility =
                KillFeedbackVisibilitySettingsStore.Load(GameStyleMode.Crossfire);
            if (!visibility.LowerEnabled)
            {
                return;
            }
            ConfigureFeedbackAppearance(
                LowerBadgeAnimation,
                visibility,
                KillFeedbackLayer.Lower);

            CrossfireGameplaySettingsValues settings = CrossfireGameplaySettingsStore.Load();

            if (killEvent.IsAssist
                || string.Equals(killEvent.AnimationKey, "assist", StringComparison.OrdinalIgnoreCase))
            {
                LowerBadgeAnimation.PlayCodeKill("assist");
                return;
            }

            if (killEvent.IsLastKill)
            {
                if (!settings.LastKillEffectEnabled)
                {
                    return;
                }

                LowerBadgeAnimation.PlayCodeKill("lastkill");
                return;
            }

            if (killEvent.IsFirstKill)
            {
                if (!settings.FirstKillEffectEnabled)
                {
                    return;
                }

                LowerBadgeAnimation.PlayCodeKill("firstkill");
            }
        }

    }
}

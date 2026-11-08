// Shared by Widget (UWP) and CompatibilityHost; see KillEventContract.cs.
// The style routing decisions both hosts previously duplicated line for line.
// Style-specific playback stays in each host; only the decisions live here.
using System;

#if WINDOWS_UWP
namespace KillConfirmGameBar.Services
#else
namespace KillConfirmCompatibility.Services
#endif
{
    internal static class FeedbackRoutingPolicy
    {
        /// <summary>Events that never reach danmaku or any style.</summary>
        public static bool IsRecordOnly(KillEvent killEvent)
        {
            return killEvent == null || killEvent.IsObservationEvent;
        }

        /// <summary>Icon/animation styles ignore deaths; danmaku still receives them.</summary>
        public static bool IsIgnoredByStyles(KillEvent killEvent)
        {
            return IsRecordOnly(killEvent)
                || string.Equals(killEvent.EventKind, "player_death", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsBombObjectiveEvent(KillEvent killEvent)
        {
            return killEvent != null
                && (string.Equals(killEvent.EventKind, "bomb_plant", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(killEvent.EventKind, "bomb_defuse", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(killEvent.AnimationKey, "bomb_plant", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(killEvent.AnimationKey, "bomb_defuse", StringComparison.OrdinalIgnoreCase));
        }

        public static bool CanStyleConsumeEvent(GameStyleMode style, KillEvent killEvent)
        {
            if (IsRecordOnly(killEvent) || (style == GameStyleMode.Csol && IsBombObjectiveEvent(killEvent)))
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

        /// <summary>Whether the style's primary (lower/crosshair) animation runs at all.</summary>
        public static bool ShouldPlayPrimaryAnimation(GameStyleMode style, KillEvent killEvent)
        {
            if (!CanStyleConsumeEvent(style, killEvent))
            {
                return false;
            }

            bool isCrossfireObjective = style == GameStyleMode.Crossfire && IsBombObjectiveEvent(killEvent);
            bool isModernWarfare2019Objective = style == GameStyleMode.ModernWarfare2019 && killEvent.IsEconomyEvent;
            return (killEvent.IsCombatEvent && killEvent.PlayMainAnimation)
                || (IsEconomyPresentationStyle(style) && IsBattlefieldTextEvent(killEvent))
                || (style == GameStyleMode.Csol && killEvent.IsCombatEvent)
                || (style == GameStyleMode.ModernWarfare2019 && (killEvent.IsCombatEvent || isModernWarfare2019Objective))
                || (style == GameStyleMode.Overwatch && killEvent.IsCombatEvent && killEvent.IsAssist)
                || (style == GameStyleMode.Apex && killEvent.IsCombatEvent && killEvent.IsAssist)
                || isCrossfireObjective;
        }

        /// <summary>Guard at the start of primary playback: zero-kill events only pass for assist/objective styles.</summary>
        public static bool PassesPrimaryGuard(GameStyleMode style, KillEvent killEvent)
        {
            if (!CanStyleConsumeEvent(style, killEvent))
            {
                return false;
            }

            if (killEvent.KillCount > 0 || IsBattlefieldTextEvent(killEvent))
            {
                return true;
            }

            bool assistStyle = style == GameStyleMode.Csol
                || style == GameStyleMode.Apex
                || style == GameStyleMode.Overwatch
                || style == GameStyleMode.ModernWarfare2019;
            if (assistStyle && killEvent.IsAssist)
            {
                return true;
            }

            return (style == GameStyleMode.Crossfire && IsBombObjectiveEvent(killEvent))
                || (style == GameStyleMode.ModernWarfare2019 && killEvent.IsEconomyEvent);
        }

        public static bool IsBattlefieldObjectiveEvent(string eventKind)
        {
            return string.Equals(eventKind, "bomb_plant", StringComparison.OrdinalIgnoreCase)
                || string.Equals(eventKind, "bomb_defuse", StringComparison.OrdinalIgnoreCase)
                || string.Equals(eventKind, "hostage_interact", StringComparison.OrdinalIgnoreCase)
                || string.Equals(eventKind, "hostage_rescue", StringComparison.OrdinalIgnoreCase);
        }

        public static string GetBattlefieldEventKind(KillEvent killEvent)
        {
            if (!string.IsNullOrWhiteSpace(killEvent?.EventKind))
            {
                return killEvent.EventKind;
            }

            return killEvent?.IsAssist == true ? "assist" : "kill";
        }

        public static bool IsBattlefieldTextEvent(KillEvent killEvent)
        {
            if (killEvent == null)
            {
                return false;
            }

            string eventKind = GetBattlefieldEventKind(killEvent);
            if (killEvent.IsCombatEvent)
            {
                return killEvent.IsAssist
                    || string.Equals(eventKind, "assist", StringComparison.OrdinalIgnoreCase);
            }

            return killEvent.IsEconomyEvent
                && (string.Equals(eventKind, "round_win", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(eventKind, "round_loss", StringComparison.OrdinalIgnoreCase)
                    || IsBattlefieldObjectiveEvent(eventKind));
        }

        public static bool IsEconomyPresentationStyle(GameStyleMode style)
        {
            return style == GameStyleMode.Battlefield1
                || style == GameStyleMode.Battlefield5
                || style == GameStyleMode.Battlefield4
                || style == GameStyleMode.Battlefield2042
                || style == GameStyleMode.Battlefield6
                || style == GameStyleMode.Pubg
                || style == GameStyleMode.DeltaForce;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Threading.Tasks;
using KillConfirmGameBar.Services;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.Graphics.Canvas.Text;
using Windows.Foundation;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Text;

namespace KillConfirmGameBar.Controls
{
    public sealed partial class KillConfirmAnimation
    {
        private static string NormalizeBattlefieldEventKind(bool isAssist, string eventKind)
        {
            if (isAssist)
            {
                return "assist";
            }

            string normalized = string.IsNullOrWhiteSpace(eventKind)
                ? string.Empty
                : eventKind.Trim().ToLowerInvariant();
            switch (normalized)
            {
                case "assist":
                case "round_win":
                case "round_loss":
                case "bomb_plant":
                case "bomb_defuse":
                case "hostage_interact":
                case "hostage_rescue":
                case "kill":
                    return normalized;
                default:
                    return "kill";
            }
        }

        private static bool IsBattlefieldTextOnlyEvent(bool isAssist, string eventKind)
        {
            string normalized = NormalizeBattlefieldEventKind(isAssist, eventKind);
            return string.Equals(normalized, "assist", StringComparison.OrdinalIgnoreCase)
                || IsRoundBonusEvent(normalized)
                || IsObjectiveBonusEvent(normalized);
        }

        private static bool IsRoundBonusEvent(string eventKind)
        {
            return IsRoundWinEvent(eventKind) || IsRoundLossEvent(eventKind);
        }

        private static bool IsRoundWinEvent(string eventKind)
        {
            return string.Equals(eventKind, "round_win", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsRoundLossEvent(string eventKind)
        {
            return string.Equals(eventKind, "round_loss", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsObjectiveBonusEvent(string eventKind)
        {
            return string.Equals(eventKind, "bomb_plant", StringComparison.OrdinalIgnoreCase)
                || string.Equals(eventKind, "bomb_defuse", StringComparison.OrdinalIgnoreCase)
                || string.Equals(eventKind, "hostage_interact", StringComparison.OrdinalIgnoreCase)
                || string.Equals(eventKind, "hostage_rescue", StringComparison.OrdinalIgnoreCase);
        }

        private static string GetObjectiveBonusLabel(string eventKind)
        {
            switch (eventKind)
            {
                case "bomb_plant":
                    return "\u5b89\u653e\u70b8\u5f39";
                case "bomb_defuse":
                    return "\u62c6\u9664\u70b8\u5f39";
                case "hostage_interact":
                    return "\u63a5\u89e6\u4eba\u8d28";
                case "hostage_rescue":
                    return "\u6551\u51fa\u4eba\u8d28";
                default:
                    return "\u76ee\u6807\u5956\u52b1";
            }
        }

        private static string GetObjectiveBonusLabelEnglish(string eventKind)
        {
            switch (eventKind)
            {
                case "bomb_plant":
                    return "BOMB PLANTED";
                case "bomb_defuse":
                    return "BOMB DEFUSED";
                case "hostage_interact":
                    return "HOSTAGE SECURED";
                case "hostage_rescue":
                    return "HOSTAGE RESCUED";
                default:
                    return "OBJECTIVE BONUS";
            }
        }

    }
}

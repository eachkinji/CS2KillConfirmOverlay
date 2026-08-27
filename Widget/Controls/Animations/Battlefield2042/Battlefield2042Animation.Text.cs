using System;

namespace KillConfirmGameBar.Controls
{
    public sealed partial class KillConfirmAnimation
    {
        private static Battlefield2042FeedItem CreateBattlefield2042FeedItem(
            bool isHeadshot,
            bool isKnifeKill,
            bool isGrenadeKill,
            bool isAssist,
            string targetName,
            string weaponName,
            int moneyReward,
            string eventKind,
            double revealTimeMs,
            bool isChinese)
        {
            string kind = NormalizeBattlefieldEventKind(isAssist, eventKind);
            bool textOnly = IsRoundBonusEvent(kind) || IsObjectiveBonusEvent(kind);
            string label = ResolveBattlefield2042EventLabel(
                isHeadshot, isKnifeKill, isGrenadeKill, kind, isChinese);
            string target = textOnly ? string.Empty
                : string.IsNullOrWhiteSpace(targetName) ? (isChinese ? "敌人" : "ENEMY") : targetName.Trim();
            string weapon = textOnly || kind == "assist" ? string.Empty
                : string.IsNullOrWhiteSpace(weaponName) ? string.Empty : weaponName.Trim();
            return new Battlefield2042FeedItem(label, target, weapon, moneyReward, revealTimeMs);
        }

        private static string ResolveBattlefield2042EventLabel(
            bool isHeadshot, bool isKnifeKill, bool isGrenadeKill, string eventKind, bool isChinese)
        {
            if (IsObjectiveBonusEvent(eventKind))
            {
                return isChinese ? GetObjectiveBonusLabel(eventKind) : GetObjectiveBonusLabelEnglish(eventKind);
            }
            if (IsRoundWinEvent(eventKind))
            {
                return isChinese ? "回合胜利" : "ROUND WON";
            }
            if (IsRoundLossEvent(eventKind))
            {
                return isChinese ? "回合失败" : "ROUND LOST";
            }
            if (eventKind == "assist")
            {
                return isChinese ? "助攻" : "ASSIST";
            }
            if (isKnifeKill)
            {
                return isChinese ? "刀杀" : "MELEE KILL";
            }
            if (isGrenadeKill)
            {
                return isChinese ? "雷杀" : "GRENADE KILL";
            }
            if (isHeadshot)
            {
                return isChinese ? "爆头击杀" : "HEADSHOT KILL";
            }
            return isChinese ? "击杀" : "KILL";
        }

        private static string FormatBattlefield2042MoneyReward(int reward)
        {
            return reward > 0 ? "+" + FormatBattlefieldMoney(reward) : string.Empty;
        }

        private static string FormatBattlefield2042MoneyTotal(int total, bool isChinese)
        {
            return (isChinese ? "累计 " : "TOTAL ") + FormatBattlefieldMoney(total);
        }
    }
}

using System;
using System.Collections.Generic;
using Microsoft.Graphics.Canvas;

namespace KillConfirmGameBar.Controls
{
    public sealed partial class KillConfirmAnimation
    {
        private sealed class Battlefield5ScrollState
        {
            public readonly List<Battlefield5ScrollIcon> ActiveIcons = new List<Battlefield5ScrollIcon>();
            public readonly List<Battlefield5ScrollIcon> PendingIcons = new List<Battlefield5ScrollIcon>();
            public readonly List<Battlefield5TextItem> BonusItems = new List<Battlefield5TextItem>();
            public Battlefield5TextItem KillFeedItem;
            public double MoneyRoundValue;
            public double MoneyStartValue;
            public double MoneyTargetValue;
            public double MoneyAnimationStartMs = -1;
            public double MoneyFirstVisibleTimeMs = -1;
            public double MoneyLastEventTimeMs = -1;
            public double LastIconDisplayTimeMs = -Battlefield5DisplayIntervalMs;
            public string MoneyStyleKey;
            public int RoundNumber = -1;
            public int MoneyEpoch = -1;

            public void Clear()
            {
                ActiveIcons.Clear();
                PendingIcons.Clear();
                BonusItems.Clear();
                KillFeedItem = null;
                MoneyRoundValue = 0;
                MoneyStartValue = 0;
                MoneyTargetValue = 0;
                MoneyAnimationStartMs = -1;
                MoneyFirstVisibleTimeMs = -1;
                MoneyLastEventTimeMs = -1;
                LastIconDisplayTimeMs = -Battlefield5DisplayIntervalMs;
                MoneyStyleKey = null;
                RoundNumber = -1;
                MoneyEpoch = -1;
            }
        }

        private sealed class Battlefield5ScrollIcon
        {
            public Battlefield5ScrollIcon(
                int killType,
                CanvasBitmap icon,
                double displayDurationMs,
                int killCount,
                string playerName,
                string weaponName,
                int moneyReward,
                string eventKind,
                int roundNumber,
                int moneyEpoch)
            {
                KillType = killType;
                Icon = icon;
                DisplayDurationMs = displayDurationMs;
                KillCount = killCount;
                PlayerName = playerName;
                WeaponName = string.IsNullOrWhiteSpace(weaponName) ? "Unknown" : weaponName;
                MoneyReward = moneyReward;
                EventKind = NormalizeBattlefieldEventKind(killType == BattlefieldKillTypeAssist, eventKind);
                RoundNumber = Math.Max(0, roundNumber);
                MoneyEpoch = Math.Max(0, moneyEpoch);
                ForcedFadeStartTimeMs = -1;
                RingStartTimeMs = -1;
            }

            public int KillType { get; }
            public CanvasBitmap Icon { get; }
            public double DisplayDurationMs { get; }
            public int KillCount { get; }
            public string PlayerName { get; }
            public string WeaponName { get; }
            public int MoneyReward { get; }
            public string EventKind { get; }
            public int RoundNumber { get; }
            public int MoneyEpoch { get; }
            public double StartTimeMs { get; set; }
            public double PrevX { get; set; }
            public double CurrentX { get; set; }
            public double TargetX { get; set; }
            public double PositionAnimationStartMs { get; set; }
            public double ForcedFadeStartTimeMs { get; set; }
            public double RingStartTimeMs { get; set; }
        }

        private sealed class Battlefield5TextItem
        {
            public Battlefield5TextItem(string text, double startTimeMs, double displayDurationMs, double scale)
            {
                Text = string.IsNullOrWhiteSpace(text) ? string.Empty : text;
                StartTimeMs = startTimeMs;
                DisplayDurationMs = displayDurationMs;
                Scale = scale;
            }

            public string Text { get; }
            public double StartTimeMs { get; }
            public double DisplayDurationMs { get; }
            public double Scale { get; }
            public double CurrentRelY { get; set; }
        }
    }
}

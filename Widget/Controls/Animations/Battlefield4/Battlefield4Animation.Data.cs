using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Text;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Text;

namespace KillConfirmGameBar.Controls
{
    public sealed partial class KillConfirmAnimation
    {
        private const double Battlefield4FrameWidth = 607;
        private const double Battlefield4FrameHeight = 260;

        // gd656killicon official preset 00005: subtitle/score.
        private const double Battlefield4ScoreDisplayMs = 4500;
        private const double Battlefield4ScoreFadeMs = 300;
        private const double Battlefield4ScoreFadeInScaleMs = 250;
        private const double Battlefield4ScorePulsePhaseMs = 100;
        private const double Battlefield4ScoreScale = 2.0;

        // gd656killicon official preset 00005: subtitle/bonus_list.
        private const double Battlefield4BonusDisplayMs = 3000;
        private const double Battlefield4BonusFadeIntervalMs = 200;
        private const double Battlefield4BonusFadeMs = 300;
        private const double Battlefield4BonusEnterMs = 200;
        private const double Battlefield4KillFeedStartMs = 800;
        private const double Battlefield4KillFeedEntryScaleMs = 350;
        private const double Battlefield4PendingIntervalMs = 100;
        private const double Battlefield4MergeWindowMs = 500;
        private const double Battlefield4PositionAnimationSpeed = 40;
        private const double Battlefield4LineSpacing = 12;
        private const int Battlefield4MaxFeedLines = 5;

        private readonly Battlefield4HudState _battlefield4HudState = new Battlefield4HudState();
        private bool _isBattlefield4HudActive;


        private sealed class Battlefield4HudState
        {
            public readonly List<Battlefield4BonusItem> Items = new List<Battlefield4BonusItem>();
            public readonly Queue<Battlefield4BonusItem> PendingItems = new Queue<Battlefield4BonusItem>();
            public double LastPendingProcessTimeMs = -Battlefield4PendingIntervalMs;
            public double NextFadeTriggerTimeMs = -1;
            public double LastFrameTimeMs = -1;

            public void Clear()
            {
                Items.Clear();
                PendingItems.Clear();
                LastPendingProcessTimeMs = -Battlefield4PendingIntervalMs;
                NextFadeTriggerTimeMs = -1;
                LastFrameTimeMs = -1;
            }
        }
        private sealed class Battlefield4BonusItem
        {
            public Battlefield4BonusItem(
                string bonusLabel,
                int score,
                bool isKillBonus,
                string weaponName,
                string targetName,
                double spawnTimeMs)
            {
                BonusLabel = bonusLabel;
                Score = score;
                IsKillBonus = isKillBonus;
                WeaponName = weaponName;
                TargetName = targetName;
                SpawnTimeMs = spawnTimeMs;
            }

            public string BonusLabel { get; }
            public int Score { get; set; }
            public bool IsKillBonus { get; }
            public string WeaponName { get; }
            public string TargetName { get; }
            public double SpawnTimeMs { get; set; }
            public double CurrentY { get; set; }
            public bool IsFading { get; set; }
            public double FadeStartTimeMs { get; set; }
        }
    }
}

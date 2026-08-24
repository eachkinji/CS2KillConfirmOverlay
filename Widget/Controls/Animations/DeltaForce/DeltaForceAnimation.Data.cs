using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Threading.Tasks;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.Graphics.Canvas.Text;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Text;

namespace KillConfirmGameBar.Controls
{
    public sealed partial class KillConfirmAnimation
    {
        private const double DeltaForceFrameWidth = 607;
        private const double DeltaForceFrameHeight = 260;
        private const double DeltaForceIconDisplayMs = 3250;
        private const double DeltaForceIconAnimationMs = 300;
        private const double DeltaForceIconPositionAnimationMs = 300;
        private const double DeltaForceQueueIntervalMs = 100;
        private const double DeltaForceBaseIconSize = 64;
        private const double DeltaForceIconScale = 0.32;
        private const double DeltaForceIconStartScale = 4.0;
        private const double DeltaForceIconYOffset = 107;
        private const double DeltaForceIconSpacing = 1;
        private const double DeltaForceScoreYOffset = 92;
        private const double DeltaForceScoreEntryMs = 250;
        private const int DeltaForceScoreThreshold = 1000;
        private const double DeltaForceBonusYOffset = 75;
        private const double DeltaForceBonusDisplayMs = 3000;
        private const double DeltaForceBonusFadeIntervalMs = 200;
        private const double DeltaForceBonusFadeMs = 300;
        private const double DeltaForceBonusEntryMs = 200;
        private const double DeltaForceBonusMergeWindowMs = 1000;
        private const double DeltaForceBonusAnimationMs = 500;
        private const double DeltaForceBonusAnimationSpeed = 8;
        private const double DeltaForceLineSpacing = 10;
        private const int DeltaForceMaxFeedLines = 4;
        private const int DeltaForceMaxVisibleIcons = 7;
        private const int DeltaForceMaxPendingIcons = 30;
        private static readonly Dictionary<string, CanvasBitmap> DeltaForceIconCache =
            new Dictionary<string, CanvasBitmap>(StringComparer.OrdinalIgnoreCase);

        private readonly DeltaForceHudState _deltaForceHudState = new DeltaForceHudState();
        private bool _isDeltaForceHudActive;


        private sealed class DeltaForceHudState
        {
            public readonly List<DeltaForceIconItem> IconItems =
                new List<DeltaForceIconItem>();
            public readonly Queue<DeltaForceIconItem> PendingIcons =
                new Queue<DeltaForceIconItem>();
            public readonly List<DeltaForceFeedItem> FeedItems =
                new List<DeltaForceFeedItem>();
            public readonly Queue<DeltaForceFeedItem> PendingFeedItems =
                new Queue<DeltaForceFeedItem>();

            public double LastIconDisplayTimeMs { get; set; } =
                -DeltaForceQueueIntervalMs;
            public double LastFeedProcessTimeMs { get; set; } =
                -DeltaForceQueueIntervalMs;
            public double NextFeedFadeTimeMs { get; set; } = -1;
            public double LastFeedUpdateTimeMs { get; set; } = -1;

            public void Clear()
            {
                IconItems.Clear();
                PendingIcons.Clear();
                FeedItems.Clear();
                PendingFeedItems.Clear();
                LastIconDisplayTimeMs = -DeltaForceQueueIntervalMs;
                LastFeedProcessTimeMs = -DeltaForceQueueIntervalMs;
                NextFeedFadeTimeMs = -1;
                LastFeedUpdateTimeMs = -1;
            }
        }

        private sealed class DeltaForceIconItem
        {
            public DeltaForceIconItem(CanvasBitmap icon, bool isHeadshot)
            {
                Icon = icon;
                IsHeadshot = isHeadshot;
            }

            public CanvasBitmap Icon { get; }
            public bool IsHeadshot { get; }
            public double StartTimeMs { get; set; }
            public double PreviousX { get; set; }
            public double CurrentX { get; set; }
            public double TargetX { get; set; }
            public double PositionAnimationStartMs { get; set; }
            public double ForcedFadeStartTimeMs { get; set; } = -1;
        }

        private sealed class DeltaForceFeedItem
        {
            public DeltaForceFeedItem(string label, int reward)
            {
                Label = string.IsNullOrWhiteSpace(label) ? string.Empty : label;
                RewardTarget = Math.Max(0, reward);
            }

            public string Label { get; }
            public double RewardTarget { get; private set; }
            public double DisplayReward { get; private set; }
            public double StartTimeMs { get; private set; } = -1;
            public double CurrentY { get; set; }
            public bool IsFading { get; set; }
            public double FadeStartTimeMs { get; set; } = -1;

            private double RewardStart { get; set; }
            private double RewardAnimationStartMs { get; set; } = -1;

            public void Activate(double now)
            {
                StartTimeMs = now;
                CurrentY = 0;
                IsFading = false;
                FadeStartTimeMs = -1;
                RewardStart = 0;
                DisplayReward = 0;
                RewardAnimationStartMs = now;
            }

            public void MergeReward(int reward, double now)
            {
                UpdateReward(now);
                RewardStart = DisplayReward;
                RewardTarget += Math.Max(0, reward);
                RewardAnimationStartMs = now;
            }

            public void UpdateReward(double now)
            {
                if (RewardTarget <= 0)
                {
                    DisplayReward = 0;
                    return;
                }

                double progress = RewardAnimationStartMs < 0
                    ? 1.0
                    : KillConfirmAnimation.Clamp01(
                        (now - RewardAnimationStartMs)
                        / DeltaForceBonusAnimationMs);
                double eased = KillConfirmAnimation.EaseOutCubic(progress);
                DisplayReward = KillConfirmAnimation.Lerp(
                    RewardStart,
                    RewardTarget,
                    eased);
            }
        }
    }
}

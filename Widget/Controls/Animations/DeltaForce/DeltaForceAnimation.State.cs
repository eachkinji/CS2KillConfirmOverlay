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
        private void UpdateDeltaForceHudFrame()
        {
            double now = _playbackClock.Elapsed.TotalMilliseconds;

            ProcessPendingDeltaForceIcons(now);
            bool removedIcon = false;
            for (int i = _deltaForceHudState.IconItems.Count - 1; i >= 0; i--)
            {
                if (ShouldRemoveDeltaForceIcon(_deltaForceHudState.IconItems[i], now))
                {
                    _deltaForceHudState.IconItems.RemoveAt(i);
                    removedIcon = true;
                }
            }

            if (removedIcon)
            {
                UpdateAllDeltaForceIconTargets(now);
            }

            for (int i = 0; i < _deltaForceHudState.IconItems.Count; i++)
            {
                UpdateDeltaForceIconPosition(_deltaForceHudState.IconItems[i], now);
            }

            ProcessPendingDeltaForceFeed(now);
            ProcessDeltaForceFeedFade(now);
            UpdateDeltaForceFeedItems(now);

            if (_deltaForceHudState.IconItems.Count == 0
                && _deltaForceHudState.PendingIcons.Count == 0
                && _deltaForceHudState.FeedItems.Count == 0
                && _deltaForceHudState.PendingFeedItems.Count == 0
                && !IsBattlefield5MoneyVisible(now))
            {
                ResetDeltaForceHudState();
                _timer.Stop();
                _playbackClock.Stop();
                Visibility = Windows.UI.Xaml.Visibility.Collapsed;
                return;
            }

            SpriteCanvas.Invalidate();
        }

        private void ProcessPendingDeltaForceIcons(double now)
        {
            if (_deltaForceHudState.PendingIcons.Count == 0
                || now - _deltaForceHudState.LastIconDisplayTimeMs < DeltaForceQueueIntervalMs)
            {
                return;
            }

            DeltaForceIconItem item = _deltaForceHudState.PendingIcons.Dequeue();
            item.StartTimeMs = now;
            _deltaForceHudState.LastIconDisplayTimeMs = now;
            _deltaForceHudState.IconItems.Add(item);
            UpdateAllDeltaForceIconTargets(now);

            item.PreviousX = item.TargetX;
            item.CurrentX = item.TargetX;
            item.PositionAnimationStartMs = now;
        }

        private void UpdateAllDeltaForceIconTargets(double now)
        {
            int count = _deltaForceHudState.IconItems.Count;
            if (count == 0)
            {
                return;
            }

            double spacing = (DeltaForceBaseIconSize * DeltaForceIconScale) + DeltaForceIconSpacing;
            int visibleStart = Math.Max(0, count - DeltaForceMaxVisibleIcons);
            int visibleCount = count - visibleStart;
            double centerX = DeltaForceFrameWidth / 2.0;
            double rightmostSlotX = centerX + ((visibleCount - 1) / 2.0 * spacing);

            for (int i = 0; i < visibleStart; i++)
            {
                DeltaForceIconItem item = _deltaForceHudState.IconItems[i];
                double overflowX = rightmostSlotX + ((visibleStart - i) * spacing);
                UpdateDeltaForceIconTarget(item, overflowX, now);
                if (item.ForcedFadeStartTimeMs < 0)
                {
                    item.ForcedFadeStartTimeMs = now;
                }
            }

            for (int i = visibleStart; i < count; i++)
            {
                double position = (i - visibleStart) - ((visibleCount - 1) / 2.0);
                double targetX = centerX - (position * spacing);
                UpdateDeltaForceIconTarget(_deltaForceHudState.IconItems[i], targetX, now);
            }
        }

        private static void UpdateDeltaForceIconTarget(
            DeltaForceIconItem item,
            double targetX,
            double now)
        {
            if (Math.Abs(item.TargetX - targetX) <= 0.1)
            {
                return;
            }

            item.PreviousX = item.CurrentX;
            item.TargetX = targetX;
            item.PositionAnimationStartMs = now;
        }

        private static void UpdateDeltaForceIconPosition(
            DeltaForceIconItem item,
            double now)
        {
            if (Math.Abs(item.CurrentX - item.TargetX) <= 0.1)
            {
                item.CurrentX = item.TargetX;
                return;
            }

            double progress = Clamp01(
                (now - item.PositionAnimationStartMs) / DeltaForceIconPositionAnimationMs);
            double eased = 1.0 - ((1.0 - progress) * (1.0 - progress));
            item.CurrentX = Lerp(item.PreviousX, item.TargetX, eased);
        }

        private static bool ShouldRemoveDeltaForceIcon(
            DeltaForceIconItem item,
            double now)
        {
            if (item.ForcedFadeStartTimeMs >= 0)
            {
                return now - item.ForcedFadeStartTimeMs >= DeltaForceIconAnimationMs;
            }

            return now - item.StartTimeMs
                >= DeltaForceIconDisplayMs + DeltaForceIconAnimationMs;
        }

        private void ProcessPendingDeltaForceFeed(double now)
        {
            if (_deltaForceHudState.PendingFeedItems.Count == 0
                || now - _deltaForceHudState.LastFeedProcessTimeMs < DeltaForceQueueIntervalMs)
            {
                return;
            }

            DeltaForceFeedItem item = _deltaForceHudState.PendingFeedItems.Dequeue();
            item.Activate(now);
            _deltaForceHudState.FeedItems.Insert(0, item);
            _deltaForceHudState.LastFeedProcessTimeMs = now;
            _deltaForceHudState.NextFeedFadeTimeMs = now + DeltaForceBonusDisplayMs;
        }

        private void ProcessDeltaForceFeedFade(double now)
        {
            if (_deltaForceHudState.PendingFeedItems.Count != 0
                || _deltaForceHudState.FeedItems.Count == 0
                || now <= _deltaForceHudState.NextFeedFadeTimeMs)
            {
                return;
            }

            for (int i = _deltaForceHudState.FeedItems.Count - 1; i >= 0; i--)
            {
                DeltaForceFeedItem item = _deltaForceHudState.FeedItems[i];
                if (!item.IsFading)
                {
                    item.IsFading = true;
                    item.FadeStartTimeMs = now;
                    _deltaForceHudState.NextFeedFadeTimeMs += DeltaForceBonusFadeIntervalMs;
                    return;
                }
            }

            _deltaForceHudState.NextFeedFadeTimeMs = now + DeltaForceBonusFadeIntervalMs;
        }

        private void UpdateDeltaForceFeedItems(double now)
        {
            double deltaSeconds = _deltaForceHudState.LastFeedUpdateTimeMs < 0
                ? 0
                : Math.Max(0, (now - _deltaForceHudState.LastFeedUpdateTimeMs) / 1000.0);
            _deltaForceHudState.LastFeedUpdateTimeMs = now;
            double smoothFactor = 1.0 - Math.Exp(-DeltaForceBonusAnimationSpeed * deltaSeconds);

            for (int i = _deltaForceHudState.FeedItems.Count - 1; i >= 0; i--)
            {
                DeltaForceFeedItem item = _deltaForceHudState.FeedItems[i];
                double targetY = i * DeltaForceLineSpacing;
                item.CurrentY += (targetY - item.CurrentY) * smoothFactor;
                item.UpdateReward(now);

                double alpha = ResolveDeltaForceFeedAlpha(item, now);
                double lineIndex = item.CurrentY / DeltaForceLineSpacing;
                if (lineIndex >= DeltaForceMaxFeedLines
                    || (item.IsFading && alpha <= 0.01))
                {
                    _deltaForceHudState.FeedItems.RemoveAt(i);
                }
            }
        }

    }
}

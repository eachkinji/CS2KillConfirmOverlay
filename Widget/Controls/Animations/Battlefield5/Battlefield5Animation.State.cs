using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.Graphics.Canvas.Text;
using Windows.Foundation;
using Windows.UI;

namespace KillConfirmGameBar.Controls
{
    public sealed partial class KillConfirmAnimation
    {
        private void UpdateBattlefield5ScrollingFrame()
        {
            double currentTimeMs = _playbackClock.Elapsed.TotalMilliseconds;
            ProcessBattlefield5PendingIcons(currentTimeMs);
            UpdateBattlefield5TextItems(currentTimeMs);

            bool removedAny = false;
            for (int i = _battlefield5ScrollState.ActiveIcons.Count - 1; i >= 0; i--)
            {
                Battlefield5ScrollIcon icon = _battlefield5ScrollState.ActiveIcons[i];
                double elapsed = currentTimeMs - icon.StartTimeMs;
                UpdateBattlefield5IconPosition(icon, currentTimeMs);
                if (ShouldRemoveBattlefield5Icon(icon, currentTimeMs, elapsed))
                {
                    _battlefield5ScrollState.ActiveIcons.RemoveAt(i);
                    removedAny = true;
                }
            }

            if (removedAny)
            {
                UpdateBattlefield5TargetPositions(currentTimeMs);
            }

            if (_battlefield5ScrollState.ActiveIcons.Count == 0
                && _battlefield5ScrollState.PendingIcons.Count == 0
                && _battlefield5ScrollState.KillFeedItem == null
                && _battlefield5ScrollState.BonusItems.Count == 0
                && !IsBattlefield5MoneyVisible(currentTimeMs))
            {
                _isBattlefield5ScrollingActive = false;
                _timer.Stop();
                _playbackClock.Stop();
                Visibility = Windows.UI.Xaml.Visibility.Collapsed;
                return;
            }

            SpriteCanvas.Invalidate();
        }

        private bool HasBattlefieldTextOverlayVisible(double currentTimeMs)
        {
            return _battlefield5ScrollState.KillFeedItem != null
                || _battlefield5ScrollState.BonusItems.Count > 0
                || IsBattlefield5MoneyVisible(currentTimeMs);
        }

        private void ProcessBattlefield5PendingIcons(double currentTimeMs)
        {
            while (_battlefield5ScrollState.PendingIcons.Count > 0
                && currentTimeMs - _battlefield5ScrollState.LastIconDisplayTimeMs >= Battlefield5DisplayIntervalMs)
            {
                Battlefield5ScrollIcon nextIcon = _battlefield5ScrollState.PendingIcons[0];
                _battlefield5ScrollState.PendingIcons.RemoveAt(0);
                nextIcon.StartTimeMs = currentTimeMs;
                nextIcon.RingStartTimeMs = nextIcon.KillType == BattlefieldKillTypeHeadshot
                    ? currentTimeMs + Battlefield5RingDelayMs
                    : -1;
                _battlefield5ScrollState.LastIconDisplayTimeMs = currentTimeMs;
                AddBattlefield5Icon(nextIcon, currentTimeMs);
            }
        }

        private void AddBattlefield5Icon(Battlefield5ScrollIcon icon, double currentTimeMs)
        {
            _battlefield5ScrollState.ActiveIcons.Add(icon);
            AddBattlefield5TextEvent(icon, currentTimeMs);
            UpdateBattlefield5TargetPositions(currentTimeMs);
            icon.PrevX = icon.TargetX;
            icon.CurrentX = icon.TargetX;
            icon.PositionAnimationStartMs = currentTimeMs;
        }

        private void UpdateBattlefield5TargetPositions(double currentTimeMs)
        {
            int size = _battlefield5ScrollState.ActiveIcons.Count;
            if (size == 0)
            {
                return;
            }

            double centerX = BattlefieldFrameWidth / 2.0;
            double spacing = (Battlefield5BaseIconSize * Battlefield5Scale) + Battlefield5IconSpacing;
            int visibleStart = Math.Max(0, size - Battlefield5MaxVisibleIcons);
            int visibleCount = size - visibleStart;
            double rightmostSlotX = centerX + ((visibleCount - 1) / 2.0) * spacing;

            for (int i = 0; i < visibleStart; i++)
            {
                Battlefield5ScrollIcon icon = _battlefield5ScrollState.ActiveIcons[i];
                double overflowX = rightmostSlotX + (visibleStart - i) * spacing;
                UpdateBattlefield5Target(icon, overflowX, currentTimeMs);
                if (icon.ForcedFadeStartTimeMs < 0)
                {
                    icon.ForcedFadeStartTimeMs = currentTimeMs;
                }
            }

            for (int i = visibleStart; i < size; i++)
            {
                Battlefield5ScrollIcon icon = _battlefield5ScrollState.ActiveIcons[i];
                double position = (i - visibleStart) - ((visibleCount - 1) / 2.0);
                double newTargetX = centerX - (position * spacing);
                UpdateBattlefield5Target(icon, newTargetX, currentTimeMs);
            }
        }

        private static void UpdateBattlefield5Target(Battlefield5ScrollIcon icon, double newTargetX, double currentTimeMs)
        {
            if (Math.Abs(icon.TargetX - newTargetX) <= 0.1)
            {
                return;
            }

            icon.PrevX = icon.CurrentX;
            icon.TargetX = newTargetX;
            icon.PositionAnimationStartMs = currentTimeMs;
        }

        private static void UpdateBattlefield5IconPosition(Battlefield5ScrollIcon icon, double currentTimeMs)
        {
            if (Math.Abs(icon.CurrentX - icon.TargetX) <= 0.1)
            {
                return;
            }

            double moveElapsed = currentTimeMs - icon.PositionAnimationStartMs;
            double progress = Clamp01(moveElapsed / Battlefield5PositionAnimationMs);
            double easedProgress = 1.0 - ((1.0 - progress) * (1.0 - progress));
            icon.CurrentX = Lerp(icon.PrevX, icon.TargetX, easedProgress);
        }

        private static bool ShouldRemoveBattlefield5Icon(Battlefield5ScrollIcon icon, double currentTimeMs, double elapsedMs)
        {
            double fadeDurationMs = Math.Max(1, Battlefield5AnimationSeconds * 1000);
            if (icon.ForcedFadeStartTimeMs >= 0)
            {
                return currentTimeMs - icon.ForcedFadeStartTimeMs >= fadeDurationMs;
            }

            return elapsedMs >= icon.DisplayDurationMs + fadeDurationMs;
        }

    }
}

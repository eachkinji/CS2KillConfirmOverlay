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
        private void UpdateBattlefield4HudFrame()
        {
            double now = _playbackClock.Elapsed.TotalMilliseconds;
            double deltaSeconds = _battlefield4HudState.LastFrameTimeMs < 0
                ? 0
                : Math.Max(0, (now - _battlefield4HudState.LastFrameTimeMs) / 1000.0);
            _battlefield4HudState.LastFrameTimeMs = now;

            ProcessBattlefield4PendingItems(now);
            ProcessBattlefield4IdleFade(now);
            UpdateBattlefield4ItemPositions(deltaSeconds);
            RemoveHiddenBattlefield4Items(now);

            if (_battlefield4HudState.Items.Count == 0
                && _battlefield4HudState.PendingItems.Count == 0
                && !IsBattlefield5MoneyVisible(now))
            {
                ResetBattlefield4HudState();
                _timer.Stop();
                _playbackClock.Stop();
                Visibility = Windows.UI.Xaml.Visibility.Collapsed;
                return;
            }

            SpriteCanvas.Invalidate();
        }
        private void ProcessBattlefield4PendingItems(double now)
        {
            if (_battlefield4HudState.PendingItems.Count == 0
                || now - _battlefield4HudState.LastPendingProcessTimeMs < Battlefield4PendingIntervalMs)
            {
                return;
            }

            Battlefield4BonusItem item = _battlefield4HudState.PendingItems.Dequeue();
            item.SpawnTimeMs = now;
            _battlefield4HudState.Items.Insert(0, item);
            _battlefield4HudState.LastPendingProcessTimeMs = now;
            _battlefield4HudState.NextFadeTriggerTimeMs = now + Battlefield4BonusDisplayMs;
        }

        private void ProcessBattlefield4IdleFade(double now)
        {
            if (_battlefield4HudState.PendingItems.Count != 0
                || _battlefield4HudState.Items.Count == 0
                || now <= _battlefield4HudState.NextFadeTriggerTimeMs)
            {
                return;
            }

            for (int i = _battlefield4HudState.Items.Count - 1; i >= 0; i--)
            {
                Battlefield4BonusItem item = _battlefield4HudState.Items[i];
                if (!item.IsFading)
                {
                    item.IsFading = true;
                    item.FadeStartTimeMs = now;
                    _battlefield4HudState.NextFadeTriggerTimeMs += Battlefield4BonusFadeIntervalMs;
                    return;
                }
            }

            _battlefield4HudState.NextFadeTriggerTimeMs = now + Battlefield4BonusFadeIntervalMs;
        }

        private void UpdateBattlefield4ItemPositions(double deltaSeconds)
        {
            double smoothFactor = 1.0 - Math.Exp(-Battlefield4PositionAnimationSpeed * deltaSeconds);
            for (int i = 0; i < _battlefield4HudState.Items.Count; i++)
            {
                Battlefield4BonusItem item = _battlefield4HudState.Items[i];
                double targetY = i * Battlefield4LineSpacing;
                item.CurrentY += (targetY - item.CurrentY) * smoothFactor;
            }
        }

        private void RemoveHiddenBattlefield4Items(double now)
        {
            for (int i = _battlefield4HudState.Items.Count - 1; i >= 0; i--)
            {
                Battlefield4BonusItem item = _battlefield4HudState.Items[i];
                double lineIndex = item.CurrentY / Battlefield4LineSpacing;
                double alpha = ResolveBattlefield4BonusAlpha(item, now);
                if (lineIndex >= Battlefield4MaxFeedLines
                    || (item.IsFading && alpha <= 0.01))
                {
                    _battlefield4HudState.Items.RemoveAt(i);
                }
            }
        }

    }
}

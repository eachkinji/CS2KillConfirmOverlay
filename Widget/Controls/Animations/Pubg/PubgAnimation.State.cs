using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Text;
using Windows.UI;
using Windows.UI.Text;

namespace KillConfirmGameBar.Controls
{
    public sealed partial class KillConfirmAnimation
    {
        private void UpdatePubgHudFrame()
        {
            double now = _playbackClock.Elapsed.TotalMilliseconds;
            ProcessPubgFeedQueue(now);
            ProcessPubgComboQueue(now);
            UpdatePubgFeedItems(now);
            UpdatePubgComboState(now);

            if (_pubgHudState.FeedItems.Count == 0
                && _pubgHudState.PendingFeedItems.Count == 0
                && !_pubgHudState.ComboVisible
                && _pubgHudState.PendingComboItems.Count == 0)
            {
                ResetPubgHudState();
                _timer.Stop();
                _playbackClock.Stop();
                Visibility = Windows.UI.Xaml.Visibility.Collapsed;
                return;
            }

            SpriteCanvas.Invalidate();
        }

        private void ProcessPubgFeedQueue(double now)
        {
            if (_pubgHudState.PendingFeedItems.Count == 0
                || now - _pubgHudState.LastFeedDequeueTimeMs < PubgQueueIntervalMs)
            {
                return;
            }

            PubgFeedItem item = _pubgHudState.PendingFeedItems.Dequeue();
            item.SpawnTimeMs = now;
            _pubgHudState.FeedItems.Add(item);
            while (_pubgHudState.FeedItems.Count > PubgMaxFeedLines)
            {
                _pubgHudState.FeedItems.RemoveAt(0);
            }

            _pubgHudState.LastFeedDequeueTimeMs = now;
        }

        private void ProcessPubgComboQueue(double now)
        {
            if (_pubgHudState.PendingComboItems.Count == 0
                || now - _pubgHudState.LastComboDequeueTimeMs < PubgQueueIntervalMs)
            {
                return;
            }

            PubgComboItem item = _pubgHudState.PendingComboItems.Dequeue();
            _pubgHudState.CurrentCombo = Math.Max(1, item.Combo);
            _pubgHudState.ComboIsAssist = item.IsAssist;
            _pubgHudState.ComboStartTimeMs = now;
            _pubgHudState.ComboVisible = true;
            _pubgHudState.LastComboDequeueTimeMs = now;
        }

        private void UpdatePubgFeedItems(double now)
        {
            for (int i = _pubgHudState.FeedItems.Count - 1; i >= 0; i--)
            {
                PubgFeedItem item = _pubgHudState.FeedItems[i];
                if (now >= item.SpawnTimeMs + PubgFeedDisplayMs + PubgFeedFadeOutMs)
                {
                    _pubgHudState.FeedItems.RemoveAt(i);
                }
            }

            for (int i = 0; i < _pubgHudState.FeedItems.Count; i++)
            {
                PubgFeedItem item = _pubgHudState.FeedItems[i];
                int positionFromBottom = _pubgHudState.FeedItems.Count - 1 - i;
                double targetY = -(positionFromBottom * PubgFeedLineSpacing);
                item.CurrentY = Lerp(item.CurrentY, targetY, 0.2);
                if (Math.Abs(item.CurrentY - targetY) < 0.5)
                {
                    item.CurrentY = targetY;
                }
            }
        }

        private void UpdatePubgComboState(double now)
        {
            if (!_pubgHudState.ComboVisible)
            {
                return;
            }

            double elapsed = now - _pubgHudState.ComboStartTimeMs;
            if (elapsed > PubgComboDisplayMs + PubgComboExitMs
                || (elapsed > PubgComboDisplayMs && ResolvePubgComboAlpha(elapsed) <= 0.05))
            {
                _pubgHudState.ComboVisible = false;
                _pubgHudState.ComboStartTimeMs = -1;
            }
        }

    }
}

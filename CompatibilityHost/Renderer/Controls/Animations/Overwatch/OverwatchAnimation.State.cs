using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Text;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Text;
using Windows.UI.Xaml;

namespace KillConfirmCompatibility.Controls
{
    public sealed partial class KillConfirmAnimation
    {
        private void UpdateOverwatchFrame()
        {
            double now = _playbackClock.Elapsed.TotalMilliseconds;
            double elapsed = Math.Max(0, now - _overwatchLastUpdateMs);
            _overwatchLastUpdateMs = now;
            double movement = 1 - Math.Pow(0.72, elapsed / (1000.0 / 60));
            if (_drawOverwatchCard)
            {
                int previousCount = _overwatchFeedItems.Count;
                _overwatchFeedItems.RemoveAll(item =>
                {
                    if (now - item.SpawnTimeMs < OverwatchCardDurationMs) return false;
                    item.TextLayout?.Dispose(); return true;
                });
                for (int index = 0; index < _overwatchFeedItems.Count; index++)
                {
                    int positionFromBottom = _overwatchFeedItems.Count - 1 - index;
                    double targetCenterY = OverwatchCardCenterY
                        - (positionFromBottom * (OverwatchCardHeight + OverwatchCardGap));
                    OverwatchFeedItem item = _overwatchFeedItems[index];
                    item.CurrentCenterY += (targetCenterY - item.CurrentCenterY) * movement;
                }

                if (previousCount != _overwatchFeedItems.Count && _overwatchFeedItems.Count > 0)
                {
                    UpdateOverwatchCardSelectionBounds();
                }
            }

            bool finished = _drawOverwatchCard
                ? _overwatchFeedItems.Count == 0
                : now >= OverwatchCrosshairDurationMs;
            if (finished)
            {
                _timer.Stop();
                _playbackClock.Stop();
                ResetOverwatchState();
                Visibility = Visibility.Collapsed;
                return;
            }

            SpriteCanvas.Invalidate();
        }

    }
}

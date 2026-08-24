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

namespace KillConfirmGameBar.Controls
{
    public sealed partial class KillConfirmAnimation
    {
        private void UpdateOverwatchFrame()
        {
            double now = _playbackClock.Elapsed.TotalMilliseconds;
            if (_drawOverwatchCard)
            {
                int previousCount = _overwatchFeedItems.Count;
                _overwatchFeedItems.RemoveAll(
                    item => now - item.SpawnTimeMs >= OverwatchCardDurationMs);
                for (int index = 0; index < _overwatchFeedItems.Count; index++)
                {
                    int positionFromBottom = _overwatchFeedItems.Count - 1 - index;
                    double targetCenterY = OverwatchCardCenterY
                        - (positionFromBottom * (OverwatchCardHeight + OverwatchCardGap));
                    OverwatchFeedItem item = _overwatchFeedItems[index];
                    item.CurrentCenterY += (targetCenterY - item.CurrentCenterY) * 0.28;
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

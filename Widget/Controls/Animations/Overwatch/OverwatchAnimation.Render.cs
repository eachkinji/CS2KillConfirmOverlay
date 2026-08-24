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
        private void DrawOverwatchFrame(CanvasDrawingSession drawingSession)
        {
            if (!_isOverwatchActive)
            {
                return;
            }

            double elapsedMs = _playbackClock.Elapsed.TotalMilliseconds;
            int frameIndex = (int)Math.Floor(
                _playbackClock.Elapsed.TotalSeconds * OverwatchSourceFps);
            if (_drawOverwatchCrosshair
                && _overwatchEffectSheetBitmap != null
                && elapsedMs < OverwatchCrosshairDurationMs
                && frameIndex >= 0
                && frameIndex < OverwatchVisibleFrameCount)
            {
                int column = frameIndex % OverwatchSheetColumns;
                int row = frameIndex / OverwatchSheetColumns;
                var source = new Rect(
                    column * OverwatchCellSize,
                    row * OverwatchCellSize,
                    OverwatchCellSize,
                    OverwatchCellSize);
                var target = new Rect(
                    (OverwatchFrameWidth - OverwatchCellSize) / 2.0,
                    (OverwatchFrameHeight - OverwatchCellSize) / 2.0,
                    OverwatchCellSize,
                    OverwatchCellSize);

                drawingSession.DrawImage(
                    _overwatchEffectSheetBitmap,
                    target,
                    source,
                    1.0f,
                    CanvasImageInterpolation.Linear);
            }

            if (_drawOverwatchCard && _overwatchKillIconBitmap != null)
            {
                foreach (OverwatchFeedItem item in _overwatchFeedItems)
                {
                    DrawOverwatchLowerThirdCard(
                        drawingSession,
                        item,
                        Math.Max(0, elapsedMs - item.SpawnTimeMs));
                }
            }
        }

    }
}

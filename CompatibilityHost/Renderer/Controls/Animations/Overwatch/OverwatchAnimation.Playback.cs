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
        public void PlayOverwatchCrosshairKill()
        {
            PlayOverwatchMode(null, drawCrosshair: true, drawCard: false);
        }

        public void PlayOverwatchLowerThirdKill(string targetName, bool isAssist = false)
        {
            PlayOverwatchMode(targetName, drawCrosshair: false, drawCard: true, isAssist: isAssist);
        }

        private void PlayOverwatchMode(
            string targetName,
            bool drawCrosshair,
            bool drawCard,
            bool isAssist = false)
        {
            QueuePreparedPlayback("Overwatch", () =>
            {
                if ((drawCrosshair && _overwatchEffectSheetBitmap == null)
                    || (drawCard && _overwatchKillIconBitmap == null))
                    throw new InvalidOperationException("Overwatch resources were not preloaded.");

                PrepareOverwatchPlayback(
                    NormalizeOverwatchTargetName(targetName),
                    drawCrosshair,
                    drawCard,
                    isAssist);
            });
        }

        private void PrepareOverwatchPlayback(
            string targetName,
            bool drawCrosshair,
            bool drawCard,
            bool isAssist = false)
        {
            bool continuingCardFeed = drawCard
                && _isOverwatchActive
                && _drawOverwatchCard
                && _playbackClock.IsRunning;
            if (continuingCardFeed)
            {
                AddOverwatchFeedItem(
                    targetName,
                    isAssist,
                    _playbackClock.Elapsed.TotalMilliseconds);
                UpdateOverwatchCardSelectionBounds();
                SpriteCanvas.Invalidate();
                return;
            }

            _timer.Stop();
            _playbackClock.Stop();
            _isBattlefieldTextOverlayActive = false;
            ResetBattlefield5ScrollingState();
            ResetBattlefield4HudState();
            ResetBattlefield2042HudState();
            ResetBattlefield6HudState();
            ResetSanguoshaState();
            ResetPubgHudState();
            ResetDeltaForceHudState();
            ResetDoubaoState();
            ResetDagoujiaoState();
            ResetModernWarfare2019State();
            ResetApexFeedState();
            _contentSizedViewport = false;
            _isBattlefield1CompactLayoutActive = false;
            _currentCodeAsset = null;
            _currentValorantAsset = null;
            _currentBattlefieldAsset = null;
            _currentCsolAsset = null;
            _isOverwatchActive = true;
            _drawOverwatchCrosshair = drawCrosshair;
            _drawOverwatchCard = drawCard;
            foreach (var item in _overwatchFeedItems) item.TextLayout?.Dispose();
            _overwatchFeedItems.Clear();
            _overwatchLastUpdateMs = 0;
            _overwatchSelectionViewportCenterOffsetX = 0;
            _overwatchSelectionViewportCenterOffsetY = 0;
            if (drawCard)
            {
                AddOverwatchFeedItem(targetName, isAssist, 0);
                UpdateOverwatchCardSelectionBounds();
            }
            else
            {
                _overwatchSelectionViewportWidth = OverwatchCellSize;
                _overwatchSelectionViewportHeight = OverwatchCellSize;
            }
            _currentMetadata = new SpriteMetadata
            {
                FrameWidth = (int)OverwatchFrameWidth,
                FrameHeight = (int)OverwatchFrameHeight,
                Frames = drawCard
                    ? (int)Math.Ceiling(OverwatchCardDurationMs / 1000.0 * FrameSequenceFps)
                    : OverwatchPlaybackFrameCount,
                Fps = 30
            };

            ApplyViewportSize(OverwatchFrameWidth, OverwatchFrameHeight);
            LogicalViewportSizeChanged?.Invoke(this, EventArgs.Empty);
            HideLoadingProgress();
            Visibility = Visibility.Visible;
            _timer.Interval = TimeSpan.FromMilliseconds(1000.0 / FrameSequenceFps);
            _playbackClock.Restart();
            SpriteCanvas.Invalidate();
            _timer.Start();
        }

        private void ResetOverwatchState()
        {
            _isOverwatchActive = false;
            _drawOverwatchCrosshair = false;
            _drawOverwatchCard = false;
            foreach (var item in _overwatchFeedItems) item.TextLayout?.Dispose();
            _overwatchFeedItems.Clear();
            _overwatchSelectionViewportCenterOffsetX = 0;
            _overwatchSelectionViewportCenterOffsetY = 0;
        }

    }
}

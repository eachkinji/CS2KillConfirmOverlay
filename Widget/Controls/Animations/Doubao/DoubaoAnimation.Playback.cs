using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KillConfirmGameBar.Services;
using Microsoft.Graphics.Canvas;
using Windows.Foundation;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Xaml;

namespace KillConfirmGameBar.Controls
{
    public sealed partial class KillConfirmAnimation
    {
        public async void PlayDoubaoKill(int killCount)
        {
            int normalizedKillCount = Math.Max(1, Math.Min(5, killCount));
            int generation = _resourceGeneration;
            int token = BeginReplacementPlayback();

            try
            {
                await WaitForCurrentPackPreloadAsync();
                CanvasBitmap bitmap = GetPreparedDoubaoKillBitmap(normalizedKillCount);
                CanvasBitmap flash = _doubaoFlashBitmapCache;

                if (bitmap == null || generation != _resourceGeneration || token != _playToken)
                {
                    return;
                }

                PrepareDoubaoPlayback(bitmap, flash, normalizedKillCount);
            }
            catch
            {
                if (token == _playToken)
                {
                    ResetDoubaoState();
                    Visibility = Visibility.Collapsed;
                }
            }
        }


        private void PrepareDoubaoPlayback(CanvasBitmap bitmap, CanvasBitmap flash, int killCount)
        {
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
            _contentSizedViewport = false;
            _isBattlefield1CompactLayoutActive = false;
            _currentCodeAsset = null;
            _currentValorantAsset = null;
            _currentBattlefieldAsset = null;
            _currentCsolAsset = null;
            _currentDoubaoBitmap = bitmap;
            _currentDoubaoFlashBitmap = flash;
            _doubaoKillCount = killCount;
            _isCalabiyauActive = false;
            _isDoubaoActive = true;
            _currentMetadata = new SpriteMetadata
            {
                FrameWidth = (int)DoubaoFrameWidth,
                FrameHeight = (int)DoubaoFrameHeight,
                Frames = (int)Math.Ceiling(DoubaoDurationMs / 1000.0 * FrameSequenceFps),
                Fps = FrameSequenceFps
            };

            ApplyViewportSize(DoubaoFrameWidth, DoubaoFrameHeight);
            HideLoadingProgress();
            Visibility = Visibility.Visible;
            _timer.Interval = TimeSpan.FromMilliseconds(1000.0 / FrameSequenceFps);
            _playbackClock.Restart();
            SpriteCanvas.Invalidate();
            _timer.Start();
        }

        private void ResetDoubaoState()
        {
            _isCalabiyauActive = false;
            _isDoubaoActive = false;
            _currentDoubaoBitmap = null;
            _currentDoubaoFlashBitmap = null;
            _doubaoKillCount = 1;
        }

        private void UpdateDoubaoFrame()
        {
            if (_playbackClock.Elapsed.TotalMilliseconds >= DoubaoDurationMs)
            {
                _timer.Stop();
                _playbackClock.Stop();
                ResetDoubaoState();
                Visibility = Visibility.Collapsed;
                return;
            }

            SpriteCanvas.Invalidate();
        }

    }
}

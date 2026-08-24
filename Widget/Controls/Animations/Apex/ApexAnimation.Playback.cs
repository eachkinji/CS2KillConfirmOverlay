using System;
using System.Collections.Generic;
using System.Globalization;
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
        public void PlayApexCrosshairKill(bool isHeadshot, int moneyReward, int killCount)
        {
            PrepareApexPlayback(drawCards: false, drawCrosshair: true);

            double now = _playbackClock.Elapsed.TotalMilliseconds;
            int normalizedKillCount = Math.Max(1, killCount);
            int normalizedReward = Math.Max(0, moneyReward);
            if (normalizedKillCount <= 1 || normalizedKillCount <= _apexLastMoneyKillCount)
            {
                _apexAccumulatedMoney = normalizedReward;
            }
            else
            {
                _apexAccumulatedMoney = (int)Math.Min(
                    int.MaxValue,
                    (long)_apexAccumulatedMoney + normalizedReward);
            }
            _apexLastMoneyKillCount = normalizedKillCount;
            _apexCrosshairEffect = new ApexCrosshairEffect
            {
                IsHeadshot = isHeadshot,
                MoneyReward = _apexAccumulatedMoney,
                SpawnTimeMs = now
            };
            EnsureApexHitmarkReadyAsync();
            SpriteCanvas.Invalidate();
        }

        public void PlayApexFeedCard(bool isAssist, string targetName, int moneyReward)
        {
            PrepareApexPlayback(drawCards: true, drawCrosshair: false);

            double now = _playbackClock.Elapsed.TotalMilliseconds;
            var item = new ApexFeedItem
            {
                IsAssist = isAssist,
                TargetName = string.IsNullOrWhiteSpace(targetName) ? "敌方玩家" : targetName.Trim(),
                MoneyReward = Math.Max(0, moneyReward),
                SpawnTimeMs = now,
                CurrentY = ApexCardBottomY
            };
            _apexFeedState.Items.Add(item);

            if (_apexFeedState.Items.Count > ApexMaxCards)
            {
                ApexFeedItem oldest = _apexFeedState.Items[0];
                if (oldest.ExitStartTimeMs < 0)
                {
                    oldest.ExitStartTimeMs = now;
                    _apexFeedState.LastExitStartTimeMs = now;
                }
            }

            UpdateApexCardSelectionBounds();

            SpriteCanvas.Invalidate();
        }


        private void PrepareApexPlayback(bool drawCards, bool drawCrosshair)
        {
            bool continuingApex = _isApexFeedActive && _playbackClock.IsRunning;
            if (!continuingApex)
            {
                _apexFeedState.Clear();
                _playbackClock.Restart();
            }

            _timer.Stop();
            _isBattlefieldTextOverlayActive = false;
            ResetBattlefield5ScrollingState();
            ResetBattlefield4HudState();
            ResetBattlefield2042HudState();
            ResetPubgHudState();
            ResetDeltaForceHudState();
            ResetDoubaoState();
            ResetDagoujiaoState();
            ResetOverwatchState();
            ResetModernWarfare2019State();
            _contentSizedViewport = false;
            _isBattlefield1CompactLayoutActive = false;
            _currentCodeAsset = null;
            _currentValorantAsset = null;
            _currentBattlefieldAsset = null;
            _currentCsolAsset = null;
            _isApexFeedActive = true;
            _drawApexCards = drawCards;
            _drawApexCrosshair = drawCrosshair;
            _apexSelectionViewportWidth = drawCards
                ? ApexCardMinimumWidth
                : ApexCrosshairSelectionWidth;
            _apexSelectionViewportHeight = drawCards
                ? ApexCardHeight
                : ApexCrosshairSelectionHeight;
            _apexSelectionViewportCenterOffsetX = 0;
            _apexSelectionViewportCenterOffsetY = drawCards
                ? ApexCardBottomY + (ApexCardHeight / 2.0) - (ApexFrameHeight / 2.0)
                : 0;
            _currentMetadata = new SpriteMetadata
            {
                FrameWidth = (int)ApexFrameWidth,
                FrameHeight = (int)ApexFrameHeight,
                Frames = (int)Math.Ceiling((ApexCardHoldMs + ApexCardExitMs) / 1000.0 * FrameSequenceFps),
                Fps = FrameSequenceFps
            };

            ApplyViewportSize(ApexFrameWidth, ApexFrameHeight);
            LogicalViewportSizeChanged?.Invoke(this, EventArgs.Empty);
            HideLoadingProgress();
            Visibility = Visibility.Visible;
            _timer.Interval = TimeSpan.FromMilliseconds(1000.0 / FrameSequenceFps);
            if (!_playbackClock.IsRunning)
            {
                _playbackClock.Restart();
            }
            _timer.Start();
        }


        private void ResetApexFeedState()
        {
            _isApexFeedActive = false;
            _drawApexCards = false;
            _drawApexCrosshair = false;
            _apexFeedState.Clear();
            _apexCrosshairEffect = null;
        }
    }
}

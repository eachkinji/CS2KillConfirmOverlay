using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.Graphics.Canvas.Text;
using Windows.Foundation;
using Windows.UI;

namespace KillConfirmGameBar.Controls
{
    public sealed partial class KillConfirmAnimation
    {
        private void UpdateBattlefield1CompositeFrame()
        {
            double currentTimeMs = _playbackClock.Elapsed.TotalMilliseconds;
            UpdateBattlefield5TextItems(currentTimeMs);

            bool mainVisible = false;
            if (_currentBattlefieldAsset != null && _currentMetadata != null)
            {
                double elapsedSeconds = Math.Max(0, currentTimeMs - _battlefieldPrimaryStartTimeMs) / 1000.0;
                int elapsedFrame = (int)Math.Floor(elapsedSeconds * Math.Max(1, _currentMetadata.Fps));
                if (elapsedFrame >= _currentMetadata.Frames)
                {
                    _currentBattlefieldAsset = null;
                    _currentCsolAsset = null;
                }
                else
                {
                    _currentFrame = Math.Max(0, elapsedFrame);
                    mainVisible = true;
                }
            }

            if (!mainVisible && !HasBattlefieldTextOverlayVisible(currentTimeMs))
            {
                _isBattlefieldTextOverlayActive = false;
                _timer.Stop();
                _playbackClock.Stop();
                Visibility = Windows.UI.Xaml.Visibility.Collapsed;
                return;
            }

            SpriteCanvas.Invalidate();
        }

        private void ApplyBattlefield1CompositionViewport(BattlefieldKillAsset asset)
        {
            using (var textFormat = CreateBattlefieldTextFormat())
            {
                Rect cardBounds = MeasureBattlefield1CardBounds(asset, textFormat);
                double cardWidth = Math.Ceiling(Math.Max(1, cardBounds.Width));
                double cardHeight = Math.Ceiling(Math.Max(1, cardBounds.Height));
                double bonusWidth = MeasureBattlefield1BonusColumnWidth(asset, textFormat);
                double moneyWidth = MeasureBattlefield1MoneyColumnWidth(asset, textFormat);
                ApplyBattlefield1CompactViewport(cardWidth, cardHeight, bonusWidth, moneyWidth, includeCard: true);
            }
        }

    }
}

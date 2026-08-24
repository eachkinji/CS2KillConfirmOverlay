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
        private void UpdateApexFeedFrame()
        {
            double now = _playbackClock.Elapsed.TotalMilliseconds;

            if (_drawApexCards
                && now - _apexFeedState.LastExitStartTimeMs >= ApexExitStaggerMs)
            {
                for (int i = 0; i < _apexFeedState.Items.Count; i++)
                {
                    ApexFeedItem candidate = _apexFeedState.Items[i];
                    if (candidate.ExitStartTimeMs < 0
                        && now >= candidate.SpawnTimeMs + ApexCardHoldMs)
                    {
                        candidate.ExitStartTimeMs = now;
                        _apexFeedState.LastExitStartTimeMs = now;
                        break;
                    }
                }
            }

            bool cardCountChanged = false;
            for (int i = _apexFeedState.Items.Count - 1; i >= 0; i--)
            {
                ApexFeedItem item = _apexFeedState.Items[i];
                if (item.ExitStartTimeMs >= 0
                    && now >= item.ExitStartTimeMs + ApexCardExitMs)
                {
                    _apexFeedState.Items.RemoveAt(i);
                    cardCountChanged = true;
                }
            }

            if (cardCountChanged && _apexFeedState.Items.Count > 0)
            {
                UpdateApexCardSelectionBounds();
            }

            if (_drawApexCrosshair
                && _apexCrosshairEffect != null
                && now >= _apexCrosshairEffect.SpawnTimeMs + ApexHitmarkDurationMs)
            {
                _apexCrosshairEffect = null;
            }

            if (_drawApexCards)
            {
                for (int i = 0; i < _apexFeedState.Items.Count; i++)
                {
                    int positionFromBottom = _apexFeedState.Items.Count - 1 - i;
                    double targetY = ApexCardBottomY - positionFromBottom * (ApexCardHeight + ApexCardGap);
                    ApexFeedItem item = _apexFeedState.Items[i];
                    item.CurrentY += (targetY - item.CurrentY) * 0.24;
                }
            }

            bool hasCards = _drawApexCards && _apexFeedState.Items.Count > 0;
            bool hasCrosshair = _drawApexCrosshair && _apexCrosshairEffect != null;
            if (!hasCards && !hasCrosshair)
            {
                ResetApexFeedState();
                _timer.Stop();
                _playbackClock.Stop();
                Visibility = Visibility.Collapsed;
                return;
            }

            SpriteCanvas.Invalidate();
        }


        private void UpdateApexCardSelectionBounds()
        {
            int cardCount = _apexFeedState.Items.Count;
            if (cardCount <= 0)
            {
                return;
            }

            double maximumCardWidth = ApexCardMinimumWidth;
            using (CanvasTextFormat primaryFormat = CreateApexPrimaryTextFormat())
            using (CanvasTextFormat secondaryFormat = CreateApexSecondaryTextFormat())
            {
                foreach (ApexFeedItem item in _apexFeedState.Items)
                {
                    string rewardText = "$" + item.MoneyReward.ToString(CultureInfo.InvariantCulture);
                    string firstPrefix = item.IsAssist ? "助攻，击倒" : "消灭了";
                    double firstWidth = MeasureApexText(firstPrefix + " " + item.TargetName, primaryFormat);
                    double secondWidth = item.IsAssist
                        ? 0
                        : MeasureApexText("得到 " + rewardText + " 金钱", secondaryFormat);
                    double cardWidth = Math.Max(
                        ApexCardMinimumWidth,
                        Math.Min(ApexCardMaximumWidth, Math.Max(firstWidth, secondWidth) + 12));
                    maximumCardWidth = Math.Max(maximumCardWidth, cardWidth);
                }
            }

            double topY = ApexCardBottomY - ((cardCount - 1) * (ApexCardHeight + ApexCardGap));
            double selectionHeight = (cardCount * ApexCardHeight) + ((cardCount - 1) * ApexCardGap);
            double selectionCenterY = topY + (selectionHeight / 2.0);

            _apexSelectionViewportWidth = maximumCardWidth;
            _apexSelectionViewportHeight = selectionHeight;
            _apexSelectionViewportCenterOffsetX = 0;
            _apexSelectionViewportCenterOffsetY = selectionCenterY - (ApexFrameHeight / 2.0);
            LogicalViewportSizeChanged?.Invoke(this, EventArgs.Empty);
        }

    }
}

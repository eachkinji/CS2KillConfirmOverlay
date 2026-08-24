using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Threading.Tasks;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.Graphics.Canvas.Text;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Text;

namespace KillConfirmGameBar.Controls
{
    public sealed partial class KillConfirmAnimation
    {
        private void DrawDeltaForceHudFrame(CanvasDrawingSession drawingSession)
        {
            double now = _playbackClock.Elapsed.TotalMilliseconds;
            using (CanvasTextFormat textFormat = new CanvasTextFormat
            {
                FontFamily = "Segoe UI",
                FontSize = 9,
                FontWeight = FontWeights.Bold
            })
            {
                DrawDeltaForceIcons(drawingSession, now);
                DrawDeltaForceFeed(drawingSession, textFormat, now);
                DrawDeltaForceScore(drawingSession, textFormat, now);
            }
        }

        private void DrawDeltaForceIcons(CanvasDrawingSession drawingSession, double now)
        {
            double centerY = DeltaForceFrameHeight - DeltaForceIconYOffset;
            for (int i = 0; i < _deltaForceHudState.IconItems.Count; i++)
            {
                DeltaForceIconItem item = _deltaForceHudState.IconItems[i];
                double elapsed = now - item.StartTimeMs;
                double alpha = ResolveDeltaForceIconAlpha(item, now);
                if (alpha <= 0.02)
                {
                    continue;
                }

                double entry = EaseOutCubic(
                    Clamp01(elapsed / DeltaForceIconAnimationMs));
                double size = DeltaForceBaseIconSize
                    * Lerp(DeltaForceIconStartScale, 1.0, entry)
                    * DeltaForceIconScale;
                DrawBattlefieldImageStretch(
                    drawingSession,
                    item.Icon,
                    new Rect(
                        item.CurrentX - (size / 2.0),
                        centerY - (size / 2.0),
                        size,
                        size),
                    alpha);

                if (item.IsHeadshot)
                {
                    double ringProgress = (elapsed - 100) / 300.0;
                    if (ringProgress >= 0 && ringProgress <= 1)
                    {
                        double easedRing = EaseOutCubic(ringProgress);
                        double ringAlpha = (1.0 - ringProgress) * (1.0 - ringProgress);
                        float ringRadius = (float)Lerp(10, 42, easedRing);
                        float ringThickness = (float)(3.0 * (1.0 - ringProgress));
                        byte ringAlphaByte = (byte)Math.Max(
                            0,
                            Math.Min(255, Math.Round(ringAlpha * 255)));
                        if (ringThickness > 0.01f && ringAlphaByte > 0)
                        {
                            using (CanvasSolidColorBrush ringBrush =
                                new CanvasSolidColorBrush(
                                    drawingSession,
                                    Color.FromArgb(ringAlphaByte, 255, 174, 75)))
                            {
                                drawingSession.DrawCircle(
                                    (float)item.CurrentX,
                                    (float)centerY,
                                    ringRadius,
                                    ringBrush,
                                    ringThickness);
                            }
                        }
                    }
                }
            }
        }

        private void DrawDeltaForceFeed(
            CanvasDrawingSession drawingSession,
            CanvasTextFormat textFormat,
            double now)
        {
            double centerX = DeltaForceFrameWidth / 2.0;
            double baseY = DeltaForceFrameHeight - DeltaForceBonusYOffset;
            for (int i = 0; i < _deltaForceHudState.FeedItems.Count; i++)
            {
                DeltaForceFeedItem item = _deltaForceHudState.FeedItems[i];
                double alpha = ResolveDeltaForceFeedAlpha(item, now);
                if (alpha <= 0.02)
                {
                    continue;
                }

                string label = item.Label;
                string rewardText = item.RewardTarget > 0
                    ? " +" + FormatBattlefieldMoney(
                        (int)Math.Round(item.DisplayReward))
                    : string.Empty;
                double labelWidth = MeasureBattlefieldTextWidth(label, textFormat);
                double rewardWidth = MeasureBattlefieldTextWidth(rewardText, textFormat);
                double currentX = centerX - ((labelWidth + rewardWidth) / 2.0);
                double y = baseY + item.CurrentY;
                byte alphaByte = (byte)Math.Max(
                    0,
                    Math.Min(255, Math.Round(alpha * 255)));

                DrawDeltaForceText(
                    drawingSession,
                    label,
                    currentX,
                    y,
                    1.0,
                    Color.FromArgb(alphaByte, 255, 255, 255),
                    textFormat);
                if (!string.IsNullOrEmpty(rewardText))
                {
                    DrawDeltaForceText(
                        drawingSession,
                        rewardText,
                        currentX + labelWidth,
                        y,
                        1.0,
                        Color.FromArgb(alphaByte, 212, 184, 0),
                        textFormat);
                }
            }
        }

        private void DrawDeltaForceScore(
            CanvasDrawingSession drawingSession,
            CanvasTextFormat textFormat,
            double now)
        {
            if (!IsBattlefield5MoneyVisible(now))
            {
                return;
            }

            double alpha = ResolveBattlefield5MoneyAlpha(now);
            double displayValue = ResolveBattlefield5MoneyValue(now);
            int roundedValue = (int)Math.Round(displayValue);
            byte alphaByte = (byte)Math.Max(
                0,
                Math.Min(255, Math.Round(alpha * 255)));
            Color color = roundedValue >= DeltaForceScoreThreshold
                ? Color.FromArgb(alphaByte, 255, 174, 75)
                : Color.FromArgb(alphaByte, 255, 255, 255);

            DrawDeltaForceTextCentered(
                drawingSession,
                FormatBattlefieldMoney(roundedValue),
                DeltaForceFrameWidth / 2.0,
                DeltaForceFrameHeight - DeltaForceScoreYOffset,
                ResolveDeltaForceScoreScale(now),
                color,
                textFormat);
        }

        private double ResolveDeltaForceScoreScale(double now)
        {
            double elapsed = now - _battlefield5ScrollState.MoneyFirstVisibleTimeMs;
            if (_battlefield5ScrollState.MoneyFirstVisibleTimeMs < 0 || elapsed < 0)
            {
                return 1.5;
            }

            if (elapsed >= DeltaForceScoreEntryMs)
            {
                return 2.0;
            }

            double progress = EaseOutCubic(
                Clamp01(elapsed / DeltaForceScoreEntryMs));
            return Lerp(1.5, 2.0, progress);
        }

    }
}

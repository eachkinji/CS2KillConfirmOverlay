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
        private void DrawBattlefield1TextOverlayFrame(CanvasDrawingSession drawingSession)
        {
            double currentTimeMs = _playbackClock.Elapsed.TotalMilliseconds;
            using (CanvasTextFormat textFormat = CreateBattlefieldTextFormat())
            {
                double moneyY = _isBattlefield1CompactLayoutActive
                    ? _battlefield1CompactMoneyY
                    : BattlefieldFrameHeight - Battlefield1BonusScoreYOffset;
                double bonusX = _isBattlefield1CompactLayoutActive
                    ? _battlefield1CompactBonusCenterX
                    : Math.Round(BattlefieldFrameWidth / 2.0) + Battlefield1BonusListXOffset;
                double bonusY = _isBattlefield1CompactLayoutActive
                    ? _battlefield1CompactBonusBaseY
                    : BattlefieldFrameHeight - Battlefield1BonusListYOffset;

                if (_isBattlefield1CompactLayoutActive)
                {
                    DrawBattlefield1MoneyScore(drawingSession, textFormat, currentTimeMs, _battlefield1CompactMoneyLeftX, moneyY);
                }
                else
                {
                    DrawBattlefield5MoneyScore(
                        drawingSession,
                        textFormat,
                        currentTimeMs,
                        Battlefield1BonusScoreRight,
                        moneyY,
                        rightAligned: true,
                        pulseOnUpdate: true);
                }

                DrawBattlefield1BonusList(drawingSession, textFormat, currentTimeMs, bonusX, bonusY);
            }
        }

        private void DrawBattlefield1MoneyScore(CanvasDrawingSession drawingSession, CanvasTextFormat textFormat, double currentTimeMs, double leftX, double y)
        {
            if (!IsBattlefield5MoneyVisible(currentTimeMs))
            {
                return;
            }

            double alpha = ResolveBattlefield5MoneyAlpha(currentTimeMs);
            if (alpha <= 0)
            {
                return;
            }

            double scale = ResolveBattlefield5MoneyScale(currentTimeMs, pulseOnUpdate: true);
            byte textAlpha = (byte)Math.Max(0, Math.Min(255, Math.Round(alpha * 255)));
            string moneyText = FormatBattlefield5Money((int)Math.Round(ResolveBattlefield5MoneyValue(currentTimeMs)));
            DrawBattlefieldText(
                drawingSession,
                moneyText,
                leftX,
                y,
                scale,
                Color.FromArgb(textAlpha, 255, 255, 255),
                textFormat,
                true);
        }

        private void DrawBattlefield1BonusList(CanvasDrawingSession drawingSession, CanvasTextFormat textFormat, double currentTimeMs, double centerX, double baseY)
        {
            int count = _battlefield5ScrollState.BonusItems.Count;
            if (count == 0)
            {
                return;
            }

            for (int visualIndex = 0; visualIndex < count; visualIndex++)
            {
                int itemIndex = count - 1 - visualIndex;
                Battlefield5TextItem item = _battlefield5ScrollState.BonusItems[itemIndex];
                double targetRelY = visualIndex * Battlefield5BonusLineSpacing;
                item.CurrentRelY = Lerp(item.CurrentRelY, targetRelY, 0.24);
                if (Math.Abs(item.CurrentRelY - targetRelY) < 0.5)
                {
                    item.CurrentRelY = targetRelY;
                }

                double alpha = ResolveBattlefield5TextAlpha(item, currentTimeMs, Battlefield5TextFadeInMs);
                if (Battlefield5MaxBonusLines > 1)
                {
                    alpha *= Math.Max(0, 1.0 - (visualIndex / (double)(Battlefield5MaxBonusLines - 1)));
                }

                if (alpha <= 0.05)
                {
                    continue;
                }

                double elapsedMs = currentTimeMs - item.StartTimeMs;
                double entryProgress = EaseOutCubic(Clamp01(elapsedMs / Battlefield1BonusPopMs));
                double y = baseY + item.CurrentRelY + Lerp(-5.0, 0.0, entryProgress);
                double scale = item.Scale * Lerp(1.42, 1.0, entryProgress);
                byte textAlpha = (byte)Math.Max(0, Math.Min(255, Math.Round(alpha * 255)));

                if (entryProgress < 1.0)
                {
                    byte flashAlpha = (byte)Math.Max(0, Math.Min(255, Math.Round(textAlpha * (1.0 - entryProgress) * 0.55)));
                    DrawBattlefieldTextCentered(
                        drawingSession,
                        item.Text,
                        centerX,
                        y,
                        scale * 1.04,
                        Color.FromArgb(flashAlpha, 255, 255, 255),
                        textFormat,
                        true);
                }

                DrawBattlefieldTextCentered(
                    drawingSession,
                    item.Text,
                    centerX,
                    y,
                    scale,
                    Color.FromArgb(textAlpha, 255, 255, 255),
                    textFormat,
                    true);
            }
        }

        private static double ResolveBattlefield1CardFoldScale(double elapsedMs)
        {
            double displayMs = Battlefield1DisplaySeconds * 1000.0;
            if (elapsedMs < Battlefield1CardFoldMs)
            {
                double progress = EaseOutCubic(Clamp01(elapsedMs / Battlefield1CardFoldMs));
                return Lerp(0.06, 1.0, progress);
            }

            if (elapsedMs > displayMs)
            {
                double progress = EaseOutCubic(Clamp01((elapsedMs - displayMs) / Battlefield1CardFoldMs));
                return Lerp(1.0, 0.06, progress);
            }

            return 1.0;
        }

        private static double ResolveBattlefield1CardContentAlpha(double elapsedMs, double baseAlpha)
        {
            double displayMs = Battlefield1DisplaySeconds * 1000.0;
            double enter = EaseOutCubic(Clamp01((elapsedMs - Battlefield1CardContentDelayMs) / Battlefield1CardContentRevealMs));
            double exit = elapsedMs > displayMs
                ? Clamp01(1.0 - ((elapsedMs - displayMs) / Math.Max(1.0, Battlefield1CardFoldMs * 0.65)))
                : 1.0;
            return baseAlpha * enter * exit;
        }

        private static string FormatBattlefield1ScoreNumber(int value)
        {
            return FormatBattlefieldMoney(value);
        }

    }
}

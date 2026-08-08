using System;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Text;
using Windows.UI;

namespace KillConfirmGameBar.Controls
{
    public sealed partial class KillConfirmAnimation
    {
        private void AddBattlefield5TextEvent(
            Battlefield5ScrollIcon icon,
            double currentTimeMs,
            bool includeKillFeed = true,
            string moneyScopeKey = "bf5")
        {
            AddBattlefieldMoneyReward(
                moneyScopeKey,
                icon.MoneyReward,
                icon.RoundNumber,
                icon.MoneyEpoch,
                currentTimeMs);
            string moneyText = FormatBattlefield5Money(icon.MoneyReward);
            bool textOnlyEvent = IsBattlefieldTextOnlyEvent(icon.KillType == BattlefieldKillTypeAssist, icon.EventKind);
            if (textOnlyEvent || !includeKillFeed)
            {
                _battlefield5ScrollState.KillFeedItem = null;
            }
            else
            {
                string feedText = BuildBattlefield5FeedText(icon, moneyText);
                _battlefield5ScrollState.KillFeedItem = new Battlefield5TextItem(
                    feedText,
                    currentTimeMs,
                    Battlefield5KillFeedDisplayMs,
                    Battlefield5KillFeedScale);
            }

            _battlefield5ScrollState.BonusItems.Add(new Battlefield5TextItem(
                BuildBattlefield5BonusText(icon, moneyText),
                currentTimeMs,
                Battlefield5BonusDisplayMs,
                Battlefield5BonusScale));

            while (_battlefield5ScrollState.BonusItems.Count > Battlefield5MaxBonusLines)
            {
                _battlefield5ScrollState.BonusItems.RemoveAt(0);
            }


        }

        private static string BuildBattlefield5FeedText(Battlefield5ScrollIcon icon, string moneyText)
        {
            string rewardSuffix = icon.MoneyReward > 0 ? " +" + moneyText : string.Empty;
            if (IsRoundBonusEvent(icon.EventKind))
            {
                return (IsRoundWinEvent(icon.EventKind)
                    ? "\u56de\u5408\u80dc\u5229"
                    : "\u56de\u5408\u5931\u8d25") + rewardSuffix;
            }

            if (IsObjectiveBonusEvent(icon.EventKind))
            {
                return GetObjectiveBonusLabel(icon.EventKind) + rewardSuffix;
            }

            if (icon.KillType == BattlefieldKillTypeAssist)
            {
                return "\u52a9\u653b" + rewardSuffix;
            }

            return ResolveBattlefield5TargetName(icon.PlayerName)
                + " [" + icon.WeaponName + "]"
                + rewardSuffix;
        }

        private static string BuildBattlefield5BonusText(Battlefield5ScrollIcon icon, string moneyText)
        {
            string rewardSuffix = icon.MoneyReward > 0 ? " +" + moneyText : string.Empty;
            if (IsRoundBonusEvent(icon.EventKind))
            {
                return (IsRoundWinEvent(icon.EventKind)
                    ? "\u56de\u5408\u80dc\u5229"
                    : "\u56de\u5408\u5931\u8d25") + rewardSuffix;
            }

            if (IsObjectiveBonusEvent(icon.EventKind))
            {
                return GetObjectiveBonusLabel(icon.EventKind) + rewardSuffix;
            }

            if (icon.KillType == BattlefieldKillTypeAssist)
            {
                return "\u52a9\u653b" + rewardSuffix;
            }

            if (icon.KillType == BattlefieldKillTypeHeadshot)
            {
                return "\u7cbe\u51c6\u51fb\u8d25" + rewardSuffix;
            }

            if (icon.KillType == BattlefieldKillTypeCrit)
            {
                return "\u66b4\u51fb\u51fb\u8d25" + rewardSuffix;
            }

            if (icon.KillType == BattlefieldKillTypeDestroyVehicle)
            {
                return "\u8f7d\u5177\u5df2\u6467\u6bc1" + rewardSuffix;
            }

            return "\u51fb\u6740" + rewardSuffix;
        }
        private static string ResolveBattlefield5TargetName(string playerName)
        {
            return string.IsNullOrWhiteSpace(playerName) ? "ENEMY" : playerName.Trim();
        }

        private void UpdateBattlefield5TextItems(double currentTimeMs)
        {
            Battlefield5TextItem feedItem = _battlefield5ScrollState.KillFeedItem;
            if (feedItem != null && ShouldRemoveBattlefield5Text(feedItem, currentTimeMs))
            {
                _battlefield5ScrollState.KillFeedItem = null;
            }

            for (int i = _battlefield5ScrollState.BonusItems.Count - 1; i >= 0; i--)
            {
                if (ShouldRemoveBattlefield5Text(_battlefield5ScrollState.BonusItems[i], currentTimeMs))
                {
                    _battlefield5ScrollState.BonusItems.RemoveAt(i);
                }
            }
        }

        private static bool ShouldRemoveBattlefield5Text(Battlefield5TextItem item, double currentTimeMs)
        {
            return currentTimeMs - item.StartTimeMs >= item.DisplayDurationMs + Battlefield5TextFadeOutMs;
        }

        private void DrawBattlefield5TextOverlayFrame(CanvasDrawingSession drawingSession)
        {
            double currentTimeMs = _playbackClock.Elapsed.TotalMilliseconds;
            double centerX = BattlefieldFrameWidth / 2.0;
            double moneyY = BattlefieldFrameHeight - Battlefield5ScoreYOffset;
            double bonusY = BattlefieldFrameHeight - Battlefield5BonusListYOffset;

            using (CanvasTextFormat textFormat = CreateBattlefieldTextFormat())
            {
                DrawBattlefield5KillFeed(
                    drawingSession,
                    textFormat,
                    currentTimeMs,
                    centerX - 1.0,
                    BattlefieldFrameHeight - Battlefield5KillFeedYOffset);

                DrawBattlefield5MoneyScore(
                    drawingSession,
                    textFormat,
                    currentTimeMs,
                    centerX,
                    moneyY,
                    rightAligned: false,
                    pulseOnUpdate: false);
                DrawBattlefield5BonusList(drawingSession, textFormat, currentTimeMs, centerX, bonusY);
            }
        }

        private void DrawBattlefield5KillFeed(CanvasDrawingSession drawingSession, CanvasTextFormat textFormat, double currentTimeMs, double centerX, double y)
        {
            Battlefield5TextItem item = _battlefield5ScrollState.KillFeedItem;
            if (item == null)
            {
                return;
            }

            double alpha = ResolveBattlefield5TextAlpha(item, currentTimeMs, Battlefield5TextFadeInMs);
            if (alpha <= 0)
            {
                return;
            }

            double scale = ResolveBattlefield5EntryScale(item, currentTimeMs, Battlefield5TextFadeInMs, 1.5);
            byte textAlpha = (byte)Math.Max(0, Math.Min(255, Math.Round(alpha * 255)));
            DrawBattlefieldTextCentered(
                drawingSession,
                item.Text,
                centerX,
                y,
                scale,
                Color.FromArgb(textAlpha, 255, 255, 255),
                textFormat);
        }

        private void DrawBattlefield5MoneyScore(
            CanvasDrawingSession drawingSession,
            CanvasTextFormat textFormat,
            double currentTimeMs,
            double anchorX,
            double y,
            bool rightAligned = false,
            bool pulseOnUpdate = false)
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

            double scale = ResolveBattlefield5MoneyScale(currentTimeMs, pulseOnUpdate);
            byte textAlpha = (byte)Math.Max(0, Math.Min(255, Math.Round(alpha * 255)));
            string moneyText = FormatBattlefield5Money((int)Math.Round(ResolveBattlefield5MoneyValue(currentTimeMs)));
            if (rightAligned)
            {
                DrawBattlefieldTextRightAligned(
                    drawingSession,
                    moneyText,
                    anchorX,
                    y,
                    scale,
                    Color.FromArgb(textAlpha, 255, 255, 255),
                    textFormat);
                return;
            }

            DrawBattlefieldTextCentered(
                drawingSession,
                moneyText,
                anchorX,
                y,
                scale,
                Color.FromArgb(textAlpha, 255, 255, 255),
                textFormat);
        }

        private void DrawBattlefield5BonusList(CanvasDrawingSession drawingSession, CanvasTextFormat textFormat, double currentTimeMs, double centerX, double baseY)
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
                double entryProgress = EaseOutCubic(Clamp01(elapsedMs / Battlefield5BonusPopMs));
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
                        textFormat);
                }

                DrawBattlefieldTextCentered(
                    drawingSession,
                    item.Text,
                    centerX,
                    y,
                    scale,
                    Color.FromArgb(textAlpha, 255, 255, 255),
                    textFormat);
            }
        }

        private static double ResolveBattlefield5TextAlpha(Battlefield5TextItem item, double currentTimeMs, double fadeInMs)
        {
            double elapsedMs = currentTimeMs - item.StartTimeMs;
            if (elapsedMs < 0)
            {
                return 0;
            }

            double alpha = elapsedMs < fadeInMs
                ? Clamp01(elapsedMs / Math.Max(1, fadeInMs))
                : 1.0;
            if (elapsedMs > item.DisplayDurationMs)
            {
                alpha *= Clamp01(1.0 - ((elapsedMs - item.DisplayDurationMs) / Battlefield5TextFadeOutMs));
            }

            return alpha;
        }

        private static double ResolveBattlefield5EntryScale(Battlefield5TextItem item, double currentTimeMs, double fadeInMs, double startMultiplier)
        {
            double elapsedMs = currentTimeMs - item.StartTimeMs;
            if (elapsedMs >= fadeInMs)
            {
                return item.Scale;
            }

            double progress = EaseOutCubic(Clamp01(elapsedMs / Math.Max(1, fadeInMs)));
            return item.Scale * Lerp(startMultiplier, 1.0, progress);
        }
    }
}

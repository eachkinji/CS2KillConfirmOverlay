using System;
using Microsoft.Graphics.Canvas.Text;
using Windows.Foundation;

namespace KillConfirmGameBar.Controls
{
    public sealed partial class KillConfirmAnimation
    {
        private enum Battlefield1PanelSegment
        {
            Icon,
            Middle,
            Right
        }

        private struct Battlefield1CardLayout
        {
            public Rect CardBounds;
            public Rect IconPanel;
            public Rect MiddlePanel;
            public Rect RightPanel;
            public double IconCenterX;
            public double IconCenterY;
            public double WeaponX;
            public double WeaponY;
            public double VictimX;
            public double VictimY;
            public double HealthX;
            public double HealthY;
        }

        private static Battlefield1CardLayout CreateBattlefield1CardLayout(BattlefieldKillAsset asset, CanvasTextFormat textFormat)
        {
            string weaponName = string.IsNullOrWhiteSpace(asset.WeaponLabel) ? "Unknown" : asset.WeaponLabel;
            string victimName = string.IsNullOrWhiteSpace(asset.PlayerName) ? "Unknown" : asset.PlayerName;
            string healthText = string.IsNullOrWhiteSpace(asset.HealthText) ? "?" : asset.HealthText;

            double weaponW = MeasureBattlefieldTextWidth(weaponName, textFormat);
            double victimW = MeasureBattlefieldTextWidth(victimName, textFormat);
            double healthW = MeasureBattlefieldTextWidth(healthText, textFormat);

            double effWeaponW = weaponW * Battlefield1WeaponScale;
            double effWeaponH = BattlefieldTextLineHeight * Battlefield1WeaponScale;
            double effVictimW = victimW * Battlefield1VictimScale;
            double effVictimH = BattlefieldTextLineHeight * Battlefield1VictimScale;
            double effHealthW = healthW * Battlefield1HealthScale;
            double effHealthH = BattlefieldTextLineHeight * Battlefield1HealthScale;

            double stackGap = Battlefield1TextStackGap;
            double textStackHeight = effVictimH + stackGap + effWeaponH;
            double middleContentWidth = Math.Max(effVictimW, effWeaponW);
            double middleContentHeight = textStackHeight;
            double panelContentHeight = Math.Max(middleContentHeight, effHealthH);
            double panelHeight = Math.Ceiling(Math.Max(
                Battlefield1IconSegmentMinWidth,
                panelContentHeight + (Battlefield1BorderSize * 2.0)));
            double iconWidth = Math.Ceiling(Math.Max(Battlefield1IconSegmentMinWidth, panelHeight));
            double rightWidth = Math.Ceiling(Math.Max(
                Battlefield1RightSegmentMinWidth,
                effHealthW + (Battlefield1RightHorizontalPadding * 2.0)));
            double middleWidth = Math.Ceiling(Math.Max(
                Battlefield1MiddleSegmentMinWidth,
                middleContentWidth + (Battlefield1MiddleHorizontalPadding * 2.0)));
            double totalWidth = iconWidth + middleWidth + rightWidth;
            if (totalWidth < Battlefield1CardMinWidth)
            {
                middleWidth += Battlefield1CardMinWidth - totalWidth;
                totalWidth = Battlefield1CardMinWidth;
            }

            double cardLeft = -totalWidth / 2.0;
            double cardTop = -panelHeight / 2.0;
            double iconLeft = cardLeft;
            double middleLeft = iconLeft + iconWidth;
            double rightLeft = middleLeft + middleWidth;

            double victimY = -textStackHeight / 2.0;
            double weaponY = victimY + effVictimH + stackGap;
            double healthY = -effHealthH / 2.0;

            double middleTextRight = middleLeft + middleWidth - Battlefield1MiddleHorizontalPadding;
            double weaponX = middleTextRight - effWeaponW;
            double victimX = middleTextRight - effVictimW;
            double healthX = rightLeft + ((rightWidth - effHealthW) / 2.0);

            return new Battlefield1CardLayout
            {
                CardBounds = new Rect(cardLeft, cardTop, totalWidth, panelHeight),
                IconPanel = new Rect(iconLeft, cardTop, iconWidth, panelHeight),
                MiddlePanel = new Rect(middleLeft, cardTop, middleWidth, panelHeight),
                RightPanel = new Rect(rightLeft, cardTop, rightWidth, panelHeight),
                IconCenterX = iconLeft + (iconWidth / 2.0),
                IconCenterY = 0,
                WeaponX = weaponX,
                WeaponY = weaponY,
                VictimX = victimX,
                VictimY = victimY,
                HealthX = healthX,
                HealthY = healthY
            };
        }

        private static Rect MeasureBattlefield1CardBounds(BattlefieldKillAsset asset, CanvasTextFormat textFormat)
        {
            return CreateBattlefield1CardLayout(asset, textFormat).CardBounds;
        }

        private void ApplyBattlefield1TextOnlyViewport()
        {
            using (var textFormat = CreateBattlefieldTextFormat())
            {
                double bonusWidth = MeasureBattlefield1BonusColumnWidth(null, textFormat);
                double moneyWidth = MeasureBattlefield1MoneyColumnWidth(null, textFormat);
                ApplyBattlefield1CompactViewport(0, 0, bonusWidth, moneyWidth, includeCard: false);
            }
        }

        private void ApplyBattlefield1CompactViewport(
            double cardWidth,
            double cardHeight,
            double bonusWidth,
            double moneyWidth,
            bool includeCard)
        {
            double cardHalfWidth = includeCard ? Math.Ceiling(Math.Max(1, cardWidth)) / 2.0 : 0;
            double bonusColumnWidth = Math.Ceiling(Math.Max(1, bonusWidth));
            double moneyColumnWidth = Math.Ceiling(Math.Max(Battlefield1CompactMinMoneyWidth, moneyWidth));
            double bottomLeftReach = bonusColumnWidth / 2.0;
            double bottomRightReach = (bonusColumnWidth / 2.0) + Battlefield1CompactColumnGap + moneyColumnWidth;
            double contentHalfWidth = Math.Ceiling(Math.Max(Battlefield1CompactMinHalfWidth, Math.Max(cardHalfWidth, Math.Max(bottomLeftReach, bottomRightReach))));
            double totalWidth = Battlefield1CompactPadding + (contentHalfWidth * 2.0) + Battlefield1CompactPadding;
            double centerX = Battlefield1CompactPadding + contentHalfWidth;
            double textTop = Battlefield1CompactPadding + (includeCard ? cardHeight + Battlefield1CompactGapY : 0);
            double textHeight = MeasureBattlefield1TextRowsHeight();
            double moneyHeight = BattlefieldTextLineHeight * Battlefield5ScoreScale * 1.32 + 4;
            double totalHeight = Math.Ceiling(textTop + Math.Max(textHeight, moneyHeight) + Battlefield1CompactPadding);

            _battlefield1CompactCardCenterX = centerX;
            _battlefield1CompactCardCenterY = Battlefield1CompactPadding + (cardHeight / 2.0);
            _battlefield1CompactBonusCenterX = centerX;
            _battlefield1CompactBonusBaseY = textTop;
            _battlefield1CompactMoneyLeftX = centerX + bottomLeftReach + Battlefield1CompactColumnGap;
            _battlefield1CompactMoneyY = textTop;
            _contentSizedViewport = true;
            _isBattlefield1CompactLayoutActive = true;
            ApplyViewportSize(Math.Ceiling(totalWidth), totalHeight);
        }

        private double MeasureBattlefield1BonusColumnWidth(BattlefieldKillAsset asset, CanvasTextFormat textFormat)
        {
            double maxWidth = 1;
            int count = Math.Min(_battlefield5ScrollState.BonusItems.Count, Battlefield5MaxBonusLines);
            for (int i = 0; i < count; i++)
            {
                maxWidth = Math.Max(maxWidth, MeasureBattlefieldTextWidth(_battlefield5ScrollState.BonusItems[i].Text, textFormat) * Battlefield5BonusScale * 1.18);
            }

            if (asset != null && count == 0)
            {
                string fallbackText = asset.MoneyReward > 0
                    ? "\u51fb\u6740 +" + FormatBattlefieldMoney(asset.MoneyReward)
                    : "\u51fb\u6740";
                maxWidth = Math.Max(maxWidth, MeasureBattlefieldTextWidth(fallbackText, textFormat) * Battlefield5BonusScale * 1.18);
            }

            return Math.Ceiling(maxWidth);
        }

        private double MeasureBattlefield1MoneyColumnWidth(BattlefieldKillAsset asset, CanvasTextFormat textFormat)
        {
            double visibleMoney = Math.Max(_battlefield5ScrollState.MoneyTargetValue, asset == null ? 0 : Math.Max(0, asset.MoneyReward));
            string moneyText = FormatBattlefield5Money((int)Math.Round(Math.Max(0, visibleMoney)));
            return Math.Ceiling(MeasureBattlefieldTextWidth(moneyText, textFormat) * Battlefield5ScoreScale * 1.32);
        }

        private double MeasureBattlefield1TextRowsHeight()
        {
            int rowCount = Math.Max(1, Math.Min(_battlefield5ScrollState.BonusItems.Count, Battlefield5MaxBonusLines));
            double lineHeight = BattlefieldTextLineHeight * Battlefield5BonusScale * 1.18;
            return ((rowCount - 1) * Battlefield5BonusLineSpacing) + lineHeight + 4;
        }
    }
}

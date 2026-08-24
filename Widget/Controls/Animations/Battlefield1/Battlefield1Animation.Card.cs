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
        private static void DrawBattlefield1TextOnlyFrame(CanvasDrawingSession drawingSession, BattlefieldKillAsset asset, int frame)
        {
            double elapsedSeconds = frame / (double)FrameSequenceFps;
            double alpha = ResolveBattlefieldAlpha(elapsedSeconds, Battlefield1AnimationSeconds, Battlefield1DisplaySeconds);
            if (alpha <= 0)
            {
                return;
            }

            double globalScale = elapsedSeconds < Battlefield1AnimationSeconds
                ? Lerp(0.75, 1.0, EaseOutCubic(Clamp01(elapsedSeconds / Battlefield1AnimationSeconds)))
                : 1.0;

            string label;
            if (IsRoundWinEvent(asset.EventKind))
            {
                label = "\u56de\u5408\u80dc\u5229";
            }
            else if (IsRoundLossEvent(asset.EventKind))
            {
                label = "\u56de\u5408\u5931\u8d25";
            }
            else
            {
                label = "\u534f\u52a9\u51fb\u6740";
            }

            string rewardText = IsRoundBonusEvent(asset.EventKind) && asset.MoneyReward > 0
                ? "+" + FormatBattlefield1ScoreNumber(asset.MoneyReward)
                : string.Empty;

            Matrix3x2 previousTransform = drawingSession.Transform;
            drawingSession.Transform =
                Matrix3x2.CreateScale((float)globalScale)
                * Matrix3x2.CreateTranslation(
                    (float)Math.Round(BattlefieldFrameWidth / 2.0),
                    (float)(BattlefieldFrameHeight - Battlefield1YOffset))
                * previousTransform;

            try
            {
                using (CanvasTextFormat textFormat = CreateBattlefieldTextFormat())
                {
                    double labelScale = 1.18;
                    double rewardScale = 1.55;
                    double gap = string.IsNullOrEmpty(rewardText) ? 0 : 10;
                    double labelWidth = MeasureBattlefieldTextWidth(label, textFormat) * labelScale;
                    double rewardWidth = MeasureBattlefieldTextWidth(rewardText, textFormat) * rewardScale;
                    double contentWidth = labelWidth + gap + rewardWidth;
                    byte textAlpha = (byte)Math.Max(5, Math.Min(255, Math.Round(alpha * 255)));
                    Color white = Color.FromArgb(textAlpha, 255, 255, 255);
                    double textLeft = -contentWidth / 2.0;
                    DrawBattlefieldText(drawingSession, label, textLeft, -9, labelScale, white, textFormat, true);
                    if (!string.IsNullOrEmpty(rewardText))
                    {
                        DrawBattlefieldText(
                            drawingSession,
                            rewardText,
                            textLeft + labelWidth + gap,
                            -12,
                            rewardScale,
                            white,
                            textFormat,
                            true);
                    }
                }
            }
            finally
            {
                drawingSession.Transform = previousTransform;
            }
        }

        private static void DrawBattlefield1FrostedPanel(CanvasDrawingSession drawingSession, Rect panel, double alpha, Battlefield1PanelSegment segment)
        {
            if (alpha <= 0 || panel.Width <= 0 || panel.Height <= 0)
            {
                return;
            }

            Rect snappedPanel = new Rect(
                Math.Round(panel.X),
                Math.Round(panel.Y),
                Math.Round(panel.Width),
                Math.Round(panel.Height));
            double opacity = Clamp01(alpha);
            double baseAlpha = Battlefield1FrostedTextBaseAlpha * opacity;
            double mistAlpha = Battlefield1FrostedMistAlpha * opacity;
            byte baseR = 14;
            byte baseG = 16;
            byte baseB = 18;

            if (segment == Battlefield1PanelSegment.Icon)
            {
                baseAlpha = Battlefield1FrostedIconBaseAlpha * opacity;
                mistAlpha = Battlefield1FrostedMistAlpha * 0.9 * opacity;
                baseR = 10;
                baseG = 12;
                baseB = 14;
            }
            else if (segment == Battlefield1PanelSegment.Right)
            {
                baseAlpha = (Battlefield1FrostedTextBaseAlpha + 0.04) * opacity;
                mistAlpha = Battlefield1FrostedMistAlpha * 0.75 * opacity;
                baseR = 16;
                baseG = 18;
                baseB = 20;
            }

            using (CanvasSolidColorBrush baseBrush = new CanvasSolidColorBrush(
                drawingSession,
                Color.FromArgb(ToBattlefieldAlphaByte(baseAlpha), baseR, baseG, baseB)))
            using (CanvasSolidColorBrush mistBrush = new CanvasSolidColorBrush(
                drawingSession,
                Color.FromArgb(ToBattlefieldAlphaByte(mistAlpha), 238, 242, 246)))
            using (CanvasSolidColorBrush topHighlightBrush = new CanvasSolidColorBrush(
                drawingSession,
                Color.FromArgb(ToBattlefieldAlphaByte(0.06 * opacity), 255, 255, 255)))
            using (CanvasSolidColorBrush leftHighlightBrush = new CanvasSolidColorBrush(
                drawingSession,
                Color.FromArgb(ToBattlefieldAlphaByte(0.035 * opacity), 255, 255, 255)))
            using (CanvasSolidColorBrush bottomShadowBrush = new CanvasSolidColorBrush(
                drawingSession,
                Color.FromArgb(ToBattlefieldAlphaByte(0.10 * opacity), 0, 0, 0)))
            {
                drawingSession.FillRectangle(snappedPanel, baseBrush);
                drawingSession.FillRectangle(snappedPanel, mistBrush);
                drawingSession.FillRectangle(new Rect(snappedPanel.X, snappedPanel.Y, snappedPanel.Width, 1), topHighlightBrush);
                drawingSession.FillRectangle(new Rect(snappedPanel.X, snappedPanel.Y, 1, snappedPanel.Height), leftHighlightBrush);
                drawingSession.FillRectangle(new Rect(snappedPanel.X, snappedPanel.Y + snappedPanel.Height - 1, snappedPanel.Width, 1), bottomShadowBrush);
            }
        }

        private static byte ToBattlefieldAlphaByte(double alpha)
        {
            return (byte)Math.Max(0, Math.Min(255, Math.Round(255 * Clamp01(alpha))));
        }

        private void DrawBattlefield1Frame(CanvasDrawingSession drawingSession, BattlefieldKillAsset asset, int frame)
        {
            double elapsedSeconds = frame / (double)FrameSequenceFps;
            double alpha = ResolveBattlefieldAlpha(elapsedSeconds, Battlefield1AnimationSeconds, Battlefield1DisplaySeconds);
            if (alpha <= 0)
            {
                return;
            }

            double elapsedMs = elapsedSeconds * 1000.0;
            double globalScale = 1.0;
            double foldScaleY = ResolveBattlefield1CardFoldScale(elapsedMs);
            double contentAlpha = ResolveBattlefield1CardContentAlpha(elapsedMs, alpha);

            string weaponName = string.IsNullOrWhiteSpace(asset.WeaponLabel) ? "Unknown" : asset.WeaponLabel;
            string victimName = string.IsNullOrWhiteSpace(asset.PlayerName) ? "Unknown" : asset.PlayerName;
            string healthText = string.IsNullOrWhiteSpace(asset.HealthText) ? "?" : asset.HealthText;

            using (CanvasTextFormat textFormat = CreateBattlefieldTextFormat())
            {
                Battlefield1CardLayout layout = CreateBattlefield1CardLayout(asset, textFormat);
                double cardCenterX = layout.CardBounds.X + (layout.CardBounds.Width / 2.0);
                double cardCenterY = layout.CardBounds.Y + (layout.CardBounds.Height / 2.0);
                double translateX = Math.Round(BattlefieldFrameWidth / 2.0);
                double translateY = BattlefieldFrameHeight - Battlefield1YOffset;
                if (_isBattlefield1CompactLayoutActive)
                {
                    translateX = Math.Round(_battlefield1CompactCardCenterX - (cardCenterX * globalScale));
                    translateY = Math.Round(_battlefield1CompactCardCenterY - (cardCenterY * globalScale));
                }

                double targetCenterX = translateX + (cardCenterX * globalScale);
                double targetCenterY = translateY + (cardCenterY * globalScale);
                Matrix3x2 previousTransform = drawingSession.Transform;
                Matrix3x2 foldedPanelTransform =
                    Matrix3x2.CreateTranslation((float)-cardCenterX, (float)-cardCenterY)
                    * Matrix3x2.CreateScale((float)globalScale, (float)(globalScale * foldScaleY))
                    * Matrix3x2.CreateTranslation(
                        (float)targetCenterX,
                        (float)targetCenterY)
                    * previousTransform;
                Matrix3x2 stableContentTransform =
                    Matrix3x2.CreateScale((float)globalScale)
                    * Matrix3x2.CreateTranslation((float)translateX, (float)translateY)
                    * previousTransform;

                try
                {
                    drawingSession.Transform = foldedPanelTransform;
                    DrawBattlefield1FrostedPanel(
                        drawingSession,
                        layout.IconPanel,
                        alpha,
                        Battlefield1PanelSegment.Icon);
                    DrawBattlefield1FrostedPanel(
                        drawingSession,
                        layout.MiddlePanel,
                        alpha,
                        Battlefield1PanelSegment.Middle);
                    DrawBattlefield1FrostedPanel(
                        drawingSession,
                        layout.RightPanel,
                        alpha,
                        Battlefield1PanelSegment.Right);

                    // Keep icon and glyph rasterization at a fixed scale. Applying the
                    // panel's per-frame fold transform here made the text shimmer/jitter.
                    drawingSession.Transform = stableContentTransform;
                    DrawBattlefieldImageStretch(
                        drawingSession,
                        asset.Icon,
                        new Rect(
                            layout.IconCenterX - (Battlefield1IconSize / 2.0),
                            layout.IconCenterY - (Battlefield1IconSize / 2.0),
                            Battlefield1IconSize,
                            Battlefield1IconSize),
                        contentAlpha);

                    byte textAlpha = (byte)Math.Max(0, Math.Min(255, Math.Round(contentAlpha * 255)));
                    DrawBattlefieldText(drawingSession, weaponName, layout.WeaponX, layout.WeaponY, Battlefield1WeaponScale, Color.FromArgb(textAlpha, 255, 255, 255), textFormat, true);
                    DrawBattlefieldText(drawingSession, victimName, layout.VictimX, layout.VictimY, Battlefield1VictimScale, Color.FromArgb(textAlpha, 255, 0, 0), textFormat, true);

                    DrawBattlefieldText(drawingSession, healthText, layout.HealthX, layout.HealthY, Battlefield1HealthScale, Color.FromArgb(textAlpha, 255, 255, 255), textFormat, true);
                }
                finally
                {
                    drawingSession.Transform = previousTransform;
                }
            }
        }
    }
}

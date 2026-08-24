using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Text;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Text;

namespace KillConfirmGameBar.Controls
{
    public sealed partial class KillConfirmAnimation
    {
        private void DrawBattlefield4HudFrame(CanvasDrawingSession drawingSession)
        {
            double now = _playbackClock.Elapsed.TotalMilliseconds;
            using (CanvasTextFormat textFormat = new CanvasTextFormat
            {
                FontFamily = "Segoe UI",
                FontSize = 10,
                FontWeight = FontWeights.Bold
            })
            {
                DrawBattlefield4BonusList(drawingSession, textFormat, now);
                DrawBattlefield4Score(drawingSession, textFormat, now);
            }
        }

        private void DrawBattlefield4BonusList(
            CanvasDrawingSession drawingSession,
            CanvasTextFormat textFormat,
            double now)
        {
            double anchorX = Battlefield4FrameWidth / 2.0 + 20;
            double baseY = Battlefield4FrameHeight - 80;
            for (int i = 0; i < _battlefield4HudState.Items.Count; i++)
            {
                Battlefield4BonusItem item = _battlefield4HudState.Items[i];
                double alpha = ResolveBattlefield4BonusAlpha(item, now);
                if (alpha <= 0.05)
                {
                    continue;
                }

                DrawBattlefield4BonusItem(
                    drawingSession,
                    textFormat,
                    item,
                    anchorX,
                    baseY + item.CurrentY,
                    alpha,
                    now);
            }
        }

        private static double ResolveBattlefield4BonusAlpha(Battlefield4BonusItem item, double now)
        {
            double lineIndex = item.CurrentY / Battlefield4LineSpacing;
            double fadeRange = Math.Max(1.0, Battlefield4MaxFeedLines - 1.0);
            double alpha = Math.Max(0, 1.0 - (lineIndex / fadeRange));
            if (item.IsFading)
            {
                alpha *= Math.Max(0, 1.0 - ((now - item.FadeStartTimeMs) / Battlefield4BonusFadeMs));
            }

            return Clamp01(alpha);
        }

        private void DrawBattlefield4BonusItem(
            CanvasDrawingSession drawingSession,
            CanvasTextFormat textFormat,
            Battlefield4BonusItem item,
            double anchorX,
            double y,
            double alpha,
            double now)
        {
            double elapsed = Math.Max(0, now - item.SpawnTimeMs);
            double itemScale = item.IsKillBonus ? 1.2 : 1.0;
            double currentScale = itemScale;
            if (item.IsKillBonus && elapsed < Battlefield4KillFeedEntryScaleMs)
            {
                double progress = EaseOutCubic(Clamp01(elapsed / Battlefield4KillFeedEntryScaleMs));
                currentScale *= Lerp(1.8, 1.0, progress);
            }

            string originalText = item.BonusLabel + FormatBattlefield4RewardSuffix(item.Score);
            double originalWidth = MeasureBattlefieldTextWidth(originalText, textFormat) * currentScale;
            double entryProgress = Clamp01(elapsed / Battlefield4BonusEnterMs);
            double feedProgress = item.IsKillBonus
                ? Clamp01((elapsed - Battlefield4KillFeedStartMs) / Battlefield4BonusEnterMs)
                : 0;

            if (!item.IsKillBonus || feedProgress < 1)
            {
                double originalLeft = anchorX - originalWidth;
                double entryLeft = anchorX - (originalWidth * entryProgress);
                double exitLeft = originalLeft + (originalWidth * feedProgress);
                Rect clip = CreateBattlefield4TextClip(
                    Math.Max(entryLeft, exitLeft),
                    anchorX,
                    y,
                    currentScale,
                    textFormat);
                DrawBattlefield4ClippedGlowText(
                    drawingSession,
                    originalText,
                    anchorX - originalWidth,
                    y,
                    currentScale,
                    Color.FromArgb(ToByte(alpha * 255), 255, 255, 255),
                    textFormat,
                    clip);
            }

            if (item.IsKillBonus && feedProgress > 0)
            {
                DrawBattlefield4KillFeed(
                    drawingSession,
                    textFormat,
                    item,
                    anchorX,
                    y,
                    currentScale,
                    alpha,
                    feedProgress);
            }
        }

        private void DrawBattlefield4KillFeed(
            CanvasDrawingSession drawingSession,
            CanvasTextFormat textFormat,
            Battlefield4BonusItem item,
            double anchorX,
            double y,
            double scale,
            double alpha,
            double feedProgress)
        {
            string prefix = "[" + item.WeaponName + "] ";
            string target = item.TargetName;
            string suffix = FormatBattlefield4RewardSuffix(item.Score);
            double prefixWidth = MeasureBattlefieldTextWidth(prefix, textFormat) * scale;
            double targetWidth = MeasureBattlefieldTextWidth(target, textFormat) * scale;
            double suffixWidth = MeasureBattlefieldTextWidth(suffix, textFormat) * scale;
            double totalWidth = prefixWidth + targetWidth + suffixWidth;
            double left = anchorX - totalWidth;
            Rect clip = CreateBattlefield4TextClip(
                left,
                left + (totalWidth * feedProgress),
                y,
                scale,
                textFormat);

            if (clip.Width <= 0)
            {
                return;
            }

            using (drawingSession.CreateLayer(1.0f, clip))
            {
                Color white = Color.FromArgb(ToByte(alpha * 255), 255, 255, 255);
                Color victimRed = Color.FromArgb(ToByte(alpha * 255), 255, 0, 0);
                DrawBattlefield4GlowText(drawingSession, prefix, left, y, scale, white, textFormat);
                DrawBattlefield4GlowText(drawingSession, target, left + prefixWidth, y, scale, victimRed, textFormat);
                DrawBattlefield4GlowText(drawingSession, suffix, left + prefixWidth + targetWidth, y, scale, white, textFormat);
            }
        }

        private void DrawBattlefield4Score(
            CanvasDrawingSession drawingSession,
            CanvasTextFormat textFormat,
            double now)
        {
            if (!IsBattlefield5MoneyVisible(now))
            {
                return;
            }

            double alpha = ResolveBattlefield5MoneyAlpha(now);
            double scale = ResolveBattlefield5MoneyScale(now, true);
            DrawBattlefield4GlowText(
                drawingSession,
                FormatBattlefieldMoney(
                    (int)Math.Round(ResolveBattlefield5MoneyValue(now))),
                Battlefield4FrameWidth / 2.0 + 30,
                Battlefield4FrameHeight - 80,
                scale,
                Color.FromArgb(ToByte(alpha * 255), 255, 255, 255),
                textFormat);
        }

    }
}

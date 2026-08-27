using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Threading.Tasks;
using KillConfirmGameBar.Services;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.Graphics.Canvas.Effects;
using Microsoft.Graphics.Canvas.Text;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Text;

namespace KillConfirmGameBar.Controls
{
    public sealed partial class KillConfirmAnimation
    {
        private void DrawBattlefield2042Feed(
            CanvasDrawingSession drawingSession,
            CanvasTextFormat textFormat,
            double now)
        {
            int row = 0;
            for (int i = _battlefield2042HudState.FeedItems.Count - 1; i >= 0; i--)
            {
                Battlefield2042FeedItem item = _battlefield2042HudState.FeedItems[i];
                double elapsed = now - item.RevealTimeMs;
                if (elapsed < 0)
                {
                    continue;
                }

                int visualRow = Math.Min(row, Battlefield2042MaxFeedLines - 1);
                double exitProgress = ResolveBattlefield2042ExitProgress(item.ExitStartTimeMs, now);
                double exitEase = EaseOutCubic(exitProgress);
                if (!item.IsCachePrepared)
                {
                    PrepareBattlefield2042FeedItemCache(item);
                }

                string weaponText = item.WeaponText;
                string targetText = item.TargetName;
                Rect textBounds = item.TextBounds;
                double weaponAdvance = item.WeaponAdvance;
                double moneyTextWidth = item.MoneyTextWidth;
                double moneyX = ResolveBattlefield2042MoneyFeedX(moneyTextWidth, 0);
                double rightX = moneyX - Battlefield2042FeedMoneyGap + (42 * exitEase);
                double centerY = Battlefield2042FeedBaseY
                    + visualRow * Battlefield2042FeedLineSpacing
                    + Battlefield2042FeedObjectHeight / 2.0
                    + (7 * exitEase);
                double originX = rightX - ((textBounds.X + textBounds.Width) * Battlefield2042FeedTextScale);
                double originY = centerY
                    - ((textBounds.Y + (textBounds.Height / 2.0)) * Battlefield2042FeedTextScale);
                double x = originX + (textBounds.X * Battlefield2042FeedTextScale);
                double totalWidth = textBounds.Width * Battlefield2042FeedTextScale;
                double weaponWidth = weaponAdvance * Battlefield2042FeedTextScale;
                double targetWidth = Math.Max(0, totalWidth - weaponWidth);
                double feedLeft = x - 3.5;
                double rowTextRight = item.MoneyReward > 0
                    ? moneyX + moneyTextWidth
                    : x + totalWidth;
                double cursorStopX = rowTextRight + Battlefield2042MoneyCursorGap;
                double cursorStopCenterX = cursorStopX + Battlefield2042MoneyCursorWidth / 2.0;
                double rootAlpha = Clamp01(EvaluateBattlefield2042LegacyCurve(
                    Battlefield2042FeedRootAlphaCurve,
                    elapsed)) * (1.0 - exitProgress);

                if (rootAlpha > 0.0001)
                {
                    Rect clip = LimitBattlefield2042FeedClip(
                        CreateBattlefield2042FeedClipRect(rightX, centerY, false, elapsed),
                        feedLeft - 6,
                        cursorStopX + Battlefield2042MoneyCursorWidth + 6);
                    using (drawingSession.CreateLayer((float)rootAlpha, clip))
                    {
                        double backgroundAlpha = Clamp01(EvaluateBattlefield2042LegacyCurve(
                            Battlefield2042FeedBackgroundAlphaCurve,
                            elapsed));
                        if (weaponWidth > 0.1)
                        {
                            DrawBattlefield2042CachedGlowingRectangle(
                                drawingSession,
                                new Rect(
                                    x - 3.5,
                                    centerY - 6,
                                    weaponWidth + 4.5,
                                    12),
                                Color.FromArgb(255, 245, 249, 249),
                                backgroundAlpha,
                                0.58,
                                item.WeaponBackgroundGlow);
                        }

                        if (targetWidth > 0.1)
                        {
                            DrawBattlefield2042CachedGlowingRectangle(
                                drawingSession,
                                new Rect(
                                    x + weaponWidth - 0.5,
                                    centerY - 6,
                                    targetWidth + 5,
                                    12),
                                Battlefield2042EnemyColor,
                                backgroundAlpha,
                                0.84,
                                item.TargetBackgroundGlow);
                        }

                        double textAlpha = Clamp01(EvaluateBattlefield2042LegacyCurve(
                            Battlefield2042FeedTextAlphaCurve,
                            elapsed));
                        byte alpha = (byte)Math.Max(0, Math.Min(255, textAlpha * 255));
                        DrawBattlefield2042CachedGlowingTextAtLayoutOrigin(
                            drawingSession,
                            weaponText,
                            originX,
                            originY,
                            Battlefield2042FeedTextScale,
                            Color.FromArgb(alpha, 245, 249, 249),
                            0.72,
                            textFormat,
                            item.WeaponTextGlow);
                        DrawBattlefield2042CachedGlowingTextAtLayoutOrigin(
                            drawingSession,
                            targetText,
                            originX + (weaponAdvance * Battlefield2042FeedTextScale),
                            originY,
                            Battlefield2042FeedTextScale,
                            Color.FromArgb(
                                alpha,
                                Battlefield2042EnemyColor.R,
                                Battlefield2042EnemyColor.G,
                            Battlefield2042EnemyColor.B),
                            1.0,
                            textFormat,
                            item.TargetTextGlow);
                        DrawBattlefield2042FeedGlitches(
                            drawingSession,
                            elapsed,
                            rightX,
                            centerY);

                        if (elapsed <= Battlefield2042FeedEffectDurationMs)
                        {
                            double cursorAlpha = Clamp01(EvaluateBattlefield2042LegacyCurve(
                                Battlefield2042FeedCursorAlphaCurve,
                                elapsed));
                            double sourceCursorX = EvaluateBattlefield2042LegacyCurve(
                                Battlefield2042FeedCursorXCurve,
                                elapsed);
                            double sourceCursorWidth = Math.Max(0, EvaluateBattlefield2042LegacyCurve(
                                Battlefield2042FeedCursorWidthCurve,
                                elapsed));
                            double cursorProgress = Clamp01(
                                (sourceCursorX + 173.5233154)
                                / (194.1999969 + 173.5233154));
                            double cursorCenterX = Lerp(
                                feedLeft,
                                cursorStopCenterX,
                                cursorProgress);
                            double sourceSpan = 194.1999969 + 173.5233154;
                            double rowSpan = Math.Max(
                                Battlefield2042MoneyCursorWidth,
                                cursorStopCenterX - feedLeft);
                            double cursorWidth = Math.Max(
                                4,
                                sourceCursorWidth * Math.Min(1, rowSpan / sourceSpan));
                            double settleProgress = EaseOutCubic(Clamp01(
                                (elapsed - 716.6667) / 150.0));
                            cursorCenterX = Lerp(
                                cursorCenterX,
                                cursorStopCenterX,
                                settleProgress);
                            cursorWidth = Lerp(
                                cursorWidth,
                                Battlefield2042MoneyCursorWidth,
                                settleProgress);
                            DrawBattlefield2042GlowingRectangle(
                                drawingSession,
                                new Rect(
                                    cursorCenterX - cursorWidth / 2.0,
                                    centerY - Battlefield2042FeedCursorHalfHeight,
                                    cursorWidth,
                                    Battlefield2042FeedCursorHalfHeight * 2.0),
                                Battlefield2042EnemyColor,
                                cursorAlpha,
                                0.88);
                        }

                        if (exitProgress > 0 && exitProgress < 0.72)
                        {
                            byte glitchAlpha = (byte)Math.Max(
                                0,
                                Math.Min(255, (1.0 - (exitProgress / 0.72)) * 210));
                            DrawBattlefield2042GlitchBars(
                                drawingSession,
                                Battlefield2042FeedGlitchBarsB,
                                rightX + Lerp(-24, 44, exitEase),
                                centerY,
                                Color.FromArgb(
                                    glitchAlpha,
                                    Battlefield2042EnemyColor.R,
                                    Battlefield2042EnemyColor.G,
                                    Battlefield2042EnemyColor.B),
                                0);
                        }
                    }
                }

                row++;
            }
        }

        private void DrawBattlefield2042MoneyFeed(
            CanvasDrawingSession drawingSession,
            CanvasTextFormat textFormat,
            double now)
        {
            int row = 0;
            for (int i = _battlefield2042HudState.MoneyItems.Count - 1; i >= 0; i--)
            {
                Battlefield2042MoneyItem item = _battlefield2042HudState.MoneyItems[i];
                double elapsed = now - item.RevealTimeMs;
                if (elapsed < 0)
                {
                    continue;
                }

                int visualRow = Math.Min(row, Battlefield2042MaxFeedLines - 1);
                double exitProgress = ResolveBattlefield2042ExitProgress(item.ExitStartTimeMs, now);
                double exitEase = EaseOutCubic(exitProgress);
                if (!item.IsCachePrepared)
                {
                    PrepareBattlefield2042MoneyItemCache(item);
                }

                string text = item.Text;
                if (string.IsNullOrEmpty(text))
                {
                    row++;
                    continue;
                }
                Rect textBounds = item.TextBounds;
                double textWidth = item.TextWidth;
                double x = ResolveBattlefield2042MoneyFeedX(textWidth, exitEase);
                double centerY = Battlefield2042FeedBaseY
                    + visualRow * Battlefield2042FeedLineSpacing
                    + Battlefield2042FeedObjectHeight / 2.0
                    + (7 * exitEase);
                double originX = x - (textBounds.X * Battlefield2042FeedTextScale);
                double originY = centerY
                    - ((textBounds.Y + (textBounds.Height / 2.0)) * Battlefield2042FeedTextScale);
                double cursorStopX = x + textWidth + Battlefield2042MoneyCursorGap;
                double rootAlpha = Clamp01(EvaluateBattlefield2042LegacyCurve(
                    Battlefield2042FeedRootAlphaCurve,
                    elapsed)) * (1.0 - exitProgress);

                if (rootAlpha > 0.0001)
                {
                    Rect clip = LimitBattlefield2042FeedClip(
                        CreateBattlefield2042FeedClipRect(x, centerY, true, elapsed),
                        x - 10,
                        cursorStopX + Battlefield2042MoneyCursorWidth + 6);
                    using (drawingSession.CreateLayer((float)rootAlpha, clip))
                    {
                        double backgroundAlpha = Clamp01(EvaluateBattlefield2042LegacyCurve(
                            Battlefield2042FeedBackgroundAlphaCurve,
                            elapsed));
                        DrawBattlefield2042CachedGlowingRectangle(
                            drawingSession,
                            new Rect(x - 4, centerY - 6, textWidth + 8, 12),
                            Colors.White,
                            backgroundAlpha,
                            0.52,
                            item.BackgroundGlow);

                        double textAlpha = Clamp01(EvaluateBattlefield2042LegacyCurve(
                            Battlefield2042FeedTextAlphaCurve,
                            elapsed));
                        byte alpha = (byte)Math.Max(0, Math.Min(255, textAlpha * 255));
                        DrawBattlefield2042CachedGlowingTextAtLayoutOrigin(
                            drawingSession,
                            text,
                            originX,
                            originY,
                            Battlefield2042FeedTextScale,
                            Color.FromArgb(alpha, 245, 249, 249),
                            0.78,
                            textFormat,
                            item.TextGlow);
                        if (elapsed <= Battlefield2042FeedEffectDurationMs)
                        {
                            double cursorAlpha = Clamp01(EvaluateBattlefield2042LegacyCurve(
                                Battlefield2042MoneyCursorAlphaCurve,
                                elapsed));
                            cursorAlpha *= EaseOutCubic(Clamp01(
                                (elapsed - 716.6667) / 150.0));
                            double cursorHeight = Math.Max(0, EvaluateBattlefield2042LegacyCurve(
                                Battlefield2042MoneyCursorHeightCurve,
                                elapsed));
                            DrawBattlefield2042GlowingRectangle(
                                drawingSession,
                                new Rect(
                                    cursorStopX,
                                    centerY - cursorHeight / 2.0,
                                    Battlefield2042MoneyCursorWidth,
                                    cursorHeight),
                                Battlefield2042EnemyColor,
                                cursorAlpha,
                                0.78);
                        }

                        if (exitProgress > 0 && exitProgress < 0.72)
                        {
                            byte glitchAlpha = (byte)Math.Max(
                                0,
                                Math.Min(255, (1.0 - (exitProgress / 0.72)) * 185));
                            DrawBattlefield2042GlitchBars(
                                drawingSession,
                                Battlefield2042FeedGlitchBarsA,
                                x + Lerp(18, -30, exitEase),
                                centerY,
                                Color.FromArgb(glitchAlpha, 245, 249, 249),
                                0);
                        }
                    }
                }

                row++;
            }
        }

        private void DrawBattlefield2042MoneyTotal(
            CanvasDrawingSession drawingSession,
            CanvasTextFormat textFormat,
            double now)
        {
            if (!IsBattlefield5MoneyVisible(now))
            {
                return;
            }

            double alpha = ResolveBattlefield5MoneyAlpha(now);
            double scale = ResolveBattlefield5MoneyScale(now, true) * 0.74;
            string text = FormatBattlefield2042MoneyTotal(
                (int)Math.Round(ResolveBattlefield5MoneyValue(now)),
                LocalizationManager.Current == UiLanguage.SimplifiedChinese);
            double width = MeasureBattlefieldTextWidth(text, textFormat) * scale;
            DrawBattlefield2042Text(
                drawingSession,
                text,
                Battlefield2042FrameWidth / 2.0 + 155 - width,
                Battlefield2042MoneyTotalY,
                scale,
                Color.FromArgb(
                    (byte)Math.Max(0, Math.Min(255, alpha * 255)),
                    245,
                    249,
                    249),
                textFormat);
        }

    }
}

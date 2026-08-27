using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Threading.Tasks;
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
        private static double ResolveBattlefield2042MoneyTotalX()
        {
            // The feed cursor ends at FeedRowRightOffset. Anchor the total's
            // left edge beyond it so longer amounts cannot grow back over rows.
            return Battlefield2042FrameWidth / 2.0
                + Battlefield2042FeedRowRightOffset + Battlefield2042MoneyTotalGap;
        }

        private static double ResolveBattlefield2042MoneyTotalScale(double textWidth, double requestedScale)
        {
            double availableWidth = Battlefield2042FrameWidth
                - Battlefield2042MoneyTotalRightPadding - ResolveBattlefield2042MoneyTotalX();
            return textWidth > 0 ? Math.Min(requestedScale, availableWidth / textWidth) : requestedScale;
        }

        private static double ResolveBattlefield2042MoneyFeedX(
            double textWidth,
            double exitEase)
        {
            double defaultX = Battlefield2042FrameWidth / 2.0
                + Battlefield2042MoneyFeedLeftOffset;
            double rowRightLimit = Battlefield2042FrameWidth / 2.0
                + Battlefield2042FeedRowRightOffset;
            double rightConstrainedX = rowRightLimit
                - Battlefield2042MoneyCursorWidth
                - Battlefield2042MoneyCursorGap
                - Math.Max(0, textWidth);
            return Math.Min(defaultX, rightConstrainedX) - (36 * exitEase);
        }

        private static Rect LimitBattlefield2042FeedClip(
            Rect legacyClip,
            double contentLeft,
            double contentRight)
        {
            double left = Math.Max(legacyClip.X, contentLeft);
            double right = Math.Min(legacyClip.X + legacyClip.Width, contentRight);
            return new Rect(
                left,
                legacyClip.Y,
                Math.Max(0, right - left),
                legacyClip.Height);
        }

        private static double ResolveBattlefield2042ExitProgress(
            double exitStartTimeMs,
            double now)
        {
            if (exitStartTimeMs < 0 || now < exitStartTimeMs)
            {
                return 0;
            }

            return Clamp01((now - exitStartTimeMs) / Battlefield2042FeedExitDurationMs);
        }
        private static Rect CreateBattlefield2042FeedClipRect(
            double anchorX,
            double centerY,
            bool anchoredLeft,
            double elapsed)
        {
            double paddingX = EvaluateBattlefield2042LegacyCurve(
                Battlefield2042FeedMaskPaddingXCurve,
                elapsed);
            double paddingY = EvaluateBattlefield2042LegacyCurve(
                Battlefield2042FeedMaskPaddingYCurve,
                elapsed);
            double width = Math.Max(0, Battlefield2042FeedObjectWidth - paddingX * 2.0);
            double height = Math.Max(0, Battlefield2042FeedObjectHeight - paddingY * 2.0);
            double centerX = anchoredLeft
                ? anchorX + Battlefield2042FeedObjectWidth / 2.0
                : anchorX - Battlefield2042FeedObjectWidth / 2.0;
            return new Rect(centerX - width / 2.0, centerY - height / 2.0, width, height);
        }

        private static double EvaluateBattlefield2042LegacyCurve(
            Battlefield2042LegacyCurveKey[] keys,
            double elapsedMs)
        {
            if (keys == null || keys.Length == 0)
            {
                return 0;
            }

            if (elapsedMs <= keys[0].TimeMs)
            {
                return keys[0].Value;
            }

            for (int i = 0; i < keys.Length - 1; i++)
            {
                Battlefield2042LegacyCurveKey current = keys[i];
                Battlefield2042LegacyCurveKey next = keys[i + 1];
                if (elapsedMs > next.TimeMs)
                {
                    continue;
                }

                double durationMs = next.TimeMs - current.TimeMs;
                if (durationMs <= 0)
                {
                    return next.Value;
                }

                double t = Clamp01((elapsedMs - current.TimeMs) / durationMs);
                double t2 = t * t;
                double t3 = t2 * t;
                double durationSeconds = durationMs / 1000.0;
                double m0 = current.OutSlope * durationSeconds;
                double m1 = next.InSlope * durationSeconds;
                return (2 * t3 - 3 * t2 + 1) * current.Value
                    + (t3 - 2 * t2 + t) * m0
                    + (-2 * t3 + 3 * t2) * next.Value
                    + (t3 - t2) * m1;
            }

            return keys[keys.Length - 1].Value;
        }

        private static void DrawBattlefield2042FeedGlitches(
            CanvasDrawingSession drawingSession,
            double elapsed,
            double originX,
            double originY)
        {
            if (elapsed >= 433.3333 && elapsed < 633.3333)
            {
                double x = elapsed >= 616.6667
                    ? 107.87
                    : elapsed >= 566.6667
                        ? -46.1
                        : elapsed >= 500
                            ? 15.3
                            : 0;
                double y = elapsed >= 566.6667
                    ? -1.3
                    : elapsed >= 500
                        ? 6
                        : 0;
                DrawBattlefield2042GlitchBars(
                    drawingSession,
                    Battlefield2042FeedGlitchBarsA,
                    originX + x,
                    originY - y,
                    Colors.White,
                    0);
            }

            if (elapsed >= 533.3333 && elapsed < 600)
            {
                double x = elapsed >= 583.3333 ? 29.1 : 0;
                double y = elapsed >= 583.3333 ? 3.21 : 0;
                DrawBattlefield2042GlitchBars(
                    drawingSession,
                    Battlefield2042FeedGlitchBarsB,
                    originX + x,
                    originY - y,
                    Colors.White,
                    0);
            }
        }
    }
}

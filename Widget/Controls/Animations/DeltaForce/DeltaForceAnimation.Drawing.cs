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
        private static double ResolveDeltaForceIconAlpha(
            DeltaForceIconItem item,
            double now)
        {
            double elapsed = now - item.StartTimeMs;
            if (elapsed < 0)
            {
                return 0;
            }

            double entryProgress = Clamp01(
                elapsed / DeltaForceIconAnimationMs);
            double baseAlpha = EaseOutCubic(entryProgress);

            if (item.ForcedFadeStartTimeMs >= 0)
            {
                double fade = 1.0 - (
                    (now - item.ForcedFadeStartTimeMs)
                    / DeltaForceIconAnimationMs);
                return baseAlpha * Clamp01(fade);
            }

            if (elapsed <= DeltaForceIconDisplayMs)
            {
                return baseAlpha;
            }

            double regularFade = 1.0 - (
                (elapsed - DeltaForceIconDisplayMs)
                / DeltaForceIconAnimationMs);
            return baseAlpha * Clamp01(regularFade);
        }

        private static double ResolveDeltaForceFeedAlpha(
            DeltaForceFeedItem item,
            double now)
        {
            double elapsed = now - item.StartTimeMs;
            double alpha = Clamp01(elapsed / DeltaForceBonusEntryMs);
            double lineIndex = item.CurrentY / DeltaForceLineSpacing;
            double fadeRange = Math.Max(1.0, DeltaForceMaxFeedLines - 1.0);
            alpha *= Math.Max(0, 1.0 - (lineIndex / fadeRange));

            if (item.IsFading)
            {
                double fadeProgress = (
                    now - item.FadeStartTimeMs)
                    / DeltaForceBonusFadeMs;
                alpha *= Math.Max(0, 1.0 - fadeProgress);
            }

            return Clamp01(alpha);
        }

        private static void DrawDeltaForceTextCentered(
            CanvasDrawingSession drawingSession,
            string text,
            double centerX,
            double y,
            double scale,
            Color color,
            CanvasTextFormat format)
        {
            double width = MeasureBattlefieldTextWidth(text, format) * scale;
            DrawDeltaForceText(
                drawingSession,
                text,
                centerX - (width / 2.0),
                y,
                scale,
                color,
                format);
        }

        private static void DrawDeltaForceText(
            CanvasDrawingSession drawingSession,
            string text,
            double x,
            double y,
            double scale,
            Color color,
            CanvasTextFormat format)
        {
            if (string.IsNullOrEmpty(text) || scale <= 0 || color.A == 0)
            {
                return;
            }

            Rect bounds = MeasureBattlefieldTextBounds(text, format);
            double snappedX = Math.Round(x - (bounds.X * scale));
            double snappedY = Math.Round(y - (bounds.Y * scale));
            Matrix3x2 previousTransform = drawingSession.Transform;
            drawingSession.Transform =
                Matrix3x2.CreateScale((float)scale)
                * Matrix3x2.CreateTranslation((float)snappedX, (float)snappedY)
                * previousTransform;

            try
            {
                Color shadowColor = Color.FromArgb(
                    color.A,
                    (byte)(color.R / 4),
                    (byte)(color.G / 4),
                    (byte)(color.B / 4));
                using (CanvasSolidColorBrush shadowBrush =
                    new CanvasSolidColorBrush(drawingSession, shadowColor))
                using (CanvasSolidColorBrush textBrush =
                    new CanvasSolidColorBrush(drawingSession, color))
                {
                    drawingSession.DrawText(text, 1, 1, shadowBrush, format);
                    drawingSession.DrawText(text, 0, 0, textBrush, format);
                }
            }
            finally
            {
                drawingSession.Transform = previousTransform;
            }
        }

    }
}

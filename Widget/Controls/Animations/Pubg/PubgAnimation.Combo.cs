using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Text;
using Windows.UI;
using Windows.UI.Text;

namespace KillConfirmGameBar.Controls
{
    public sealed partial class KillConfirmAnimation
    {
        private void DrawPubgCombo(
            CanvasDrawingSession drawingSession,
            CanvasTextFormat textFormat,
            double now)
        {
            if (!_pubgHudState.ComboVisible)
            {
                return;
            }

            double elapsed = now - _pubgHudState.ComboStartTimeMs;
            double alpha = ResolvePubgComboAlpha(elapsed);
            if (alpha <= 0.05)
            {
                return;
            }

            int combo = Math.Max(1, _pubgHudState.CurrentCombo);
            string text;
            if (_pubgHudState.ComboIsAssist)
            {
                text = combo == 1
                    ? "1 \u52a9\u653b"
                    : combo.ToString(CultureInfo.InvariantCulture) + " \u52a9\u653b\u6570";
            }
            else
            {
                text = combo == 1
                    ? "1 \u6dd8\u6c70"
                    : combo.ToString(CultureInfo.InvariantCulture) + " \u6dd8\u6c70\u6570";
            }

            double centerX = PubgFrameWidth / 2.0;
            double centerY = PubgFrameHeight - 70;
            var textBounds = MeasureBattlefieldTextBounds(text, textFormat);
            double textWidth = textBounds.Width * PubgComboScale;
            double textHeight = textBounds.Height * PubgComboScale;
            double textX = centerX - (textWidth / 2.0);
            double textY = centerY - (textHeight / 2.0);
            DrawPubgComboLight(drawingSession, centerX, centerY, elapsed);

            Color baseColor = _pubgHudState.ComboIsAssist
                ? Color.FromArgb(255, 255, 215, 0)
                : Color.FromArgb(255, 255, 53, 0);
            Color color = Color.FromArgb(PubgByte(alpha * 255), baseColor.R, baseColor.G, baseColor.B);
            DrawBattlefieldText(
                drawingSession,
                text,
                textX,
                textY,
                PubgComboScale,
                color,
                textFormat);
        }
        private static double ResolvePubgComboAlpha(double elapsed)
        {
            if (elapsed < 0)
            {
                return 0;
            }

            if (elapsed < PubgComboFadeInMs)
            {
                return Clamp01(elapsed / PubgComboFadeInMs);
            }

            if (elapsed <= PubgComboDisplayMs)
            {
                return 1;
            }

            return Clamp01(1.0 - ((elapsed - PubgComboDisplayMs) / PubgComboExitMs));
        }

        private static void DrawPubgComboLight(
            CanvasDrawingSession drawingSession,
            double centerX,
            double centerY,
            double elapsed)
        {
            if (elapsed < 0 || elapsed > PubgLightScanMs + PubgLightFadeMs)
            {
                return;
            }

            double scanDistance;
            if (elapsed < PubgLightScanMs)
            {
                scanDistance = EaseOutCubic(Clamp01(elapsed / PubgLightScanMs)) * PubgLightScanDistance;
            }
            else
            {
                scanDistance = PubgLightScanDistance;
            }

            double baseAlpha = elapsed <= PubgLightScanMs
                ? 1.0
                : Clamp01(1.0 - ((elapsed - PubgLightScanMs) / PubgLightFadeMs));
            double halfWidth = scanDistance * PubgComboScale;
            double halfHeight = (PubgLightHeight / 2.0) * PubgComboScale;
            int pixelHalfWidth = Math.Max(1, (int)Math.Ceiling(halfWidth));
            for (int dx = -pixelHalfWidth; dx <= pixelHalfWidth; dx++)
            {
                double edge = 1.0 - (Math.Abs(dx) / (double)pixelHalfWidth);
                byte alpha = PubgByte(200 * baseAlpha * edge);
                if (alpha == 0)
                {
                    continue;
                }

                drawingSession.DrawLine(
                    (float)(centerX + dx),
                    (float)(centerY - halfHeight),
                    (float)(centerX + dx),
                    (float)(centerY + halfHeight),
                    Color.FromArgb(alpha, 255, 255, 255),
                    1.0f);
            }
        }

        private static Color InterpolatePubgColor(Color from, Color to, double progress)
        {
            double t = Clamp01(progress);
            return Color.FromArgb(
                255,
                PubgByte(Lerp(from.R, to.R, t)),
                PubgByte(Lerp(from.G, to.G, t)),
                PubgByte(Lerp(from.B, to.B, t)));
        }

        private static byte PubgByte(double value)
        {
            return (byte)Math.Max(0, Math.Min(255, value));
        }

    }
}

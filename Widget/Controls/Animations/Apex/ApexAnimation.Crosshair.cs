using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Threading.Tasks;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Text;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Text;
using Windows.UI.Xaml;

namespace KillConfirmGameBar.Controls
{
    public sealed partial class KillConfirmAnimation
    {
        private void DrawApexCrosshairEffect(
            CanvasDrawingSession drawingSession,
            CanvasTextFormat moneyFormat,
            double now)
        {
            ApexCrosshairEffect effect = _apexCrosshairEffect;
            if (effect == null)
            {
                return;
            }

            double elapsed = Math.Max(0, now - effect.SpawnTimeMs);
            if (elapsed >= ApexHitmarkDurationMs)
            {
                return;
            }

            double centerX = ApexFrameWidth / 2.0;
            double centerY = ApexFrameHeight / 2.0;
            double exitProgress = elapsed <= ApexHitmarkHoldEndMs
                ? 0
                : Clamp01((elapsed - ApexHitmarkHoldEndMs)
                    / (ApexHitmarkDurationMs - ApexHitmarkHoldEndMs));

            CanvasBitmap hitmark = _apexHitmarkBitmap;
            if (hitmark != null)
            {
                double scale = ResolveApexHitmarkScale(elapsed);
                double opacity = 1.0 - exitProgress;
                double size = ApexHitmarkSize * scale;
                var target = new Rect(
                    centerX - (size / 2.0),
                    centerY - (size / 2.0),
                    size,
                    size);
                var source = new Rect(
                    0,
                    0,
                    hitmark.SizeInPixels.Width,
                    hitmark.SizeInPixels.Height);
                drawingSession.DrawImage(
                    hitmark,
                    target,
                    source,
                    (float)Clamp01(opacity),
                    CanvasImageInterpolation.Linear);
            }

            string moneyText = "$" + effect.MoneyReward.ToString(CultureInfo.InvariantCulture);
            Color moneyColor = effect.IsHeadshot
                ? Color.FromArgb(ApexByte((1.0 - exitProgress) * 255), 255, 198, 42)
                : Color.FromArgb(ApexByte((1.0 - exitProgress) * 255), 255, 255, 255);
            double moneyRise = exitProgress * 22;
            var moneyPosition = new Vector2(
                (float)(centerX + 82),
                (float)(centerY - 82 - moneyRise));
            Color moneyShadowColor = Color.FromArgb(
                ApexByte((1.0 - exitProgress) * 90),
                0,
                0,
                0);
            drawingSession.DrawText(
                moneyText,
                moneyPosition + new Vector2(1.5f, 1.5f),
                moneyShadowColor,
                moneyFormat);
            if (effect.IsHeadshot)
            {
                Color outlineColor = Color.FromArgb(
                    ApexByte((1.0 - exitProgress) * 255),
                    188,
                    28,
                    24);
                DrawApexTextOutline(
                    drawingSession,
                    moneyText,
                    moneyPosition,
                    outlineColor,
                    moneyFormat);
            }
            drawingSession.DrawText(
                moneyText,
                moneyPosition,
                moneyColor,
                moneyFormat);
        }

        private static void DrawApexTextOutline(
            CanvasDrawingSession drawingSession,
            string text,
            Vector2 position,
            Color color,
            CanvasTextFormat format)
        {
            drawingSession.DrawText(text, position + new Vector2(-1, -1), color, format);
            drawingSession.DrawText(text, position + new Vector2(0, -1), color, format);
            drawingSession.DrawText(text, position + new Vector2(1, -1), color, format);
            drawingSession.DrawText(text, position + new Vector2(-1, 0), color, format);
            drawingSession.DrawText(text, position + new Vector2(1, 0), color, format);
            drawingSession.DrawText(text, position + new Vector2(-1, 1), color, format);
            drawingSession.DrawText(text, position + new Vector2(0, 1), color, format);
            drawingSession.DrawText(text, position + new Vector2(1, 1), color, format);
        }

        private static double ResolveApexHitmarkScale(double elapsedMs)
        {
            if (elapsedMs < 55)
            {
                return Lerp(1.0, 0.62, Clamp01(elapsedMs / 55.0));
            }

            if (elapsedMs < 130)
            {
                return Lerp(0.62, 1.2, Clamp01((elapsedMs - 55) / 75.0));
            }

            if (elapsedMs < 195)
            {
                return Lerp(1.2, 1.0, Clamp01((elapsedMs - 130) / 65.0));
            }

            if (elapsedMs < ApexHitmarkHoldEndMs)
            {
                return 1.0;
            }

            return Lerp(
                1.0,
                0.12,
                Clamp01((elapsedMs - ApexHitmarkHoldEndMs)
                    / (ApexHitmarkDurationMs - ApexHitmarkHoldEndMs)));
        }

    }
}

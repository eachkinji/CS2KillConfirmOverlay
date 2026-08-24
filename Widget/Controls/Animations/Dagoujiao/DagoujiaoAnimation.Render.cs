using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KillConfirmGameBar.Services;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Effects;
using Windows.Foundation;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Xaml;

namespace KillConfirmGameBar.Controls
{
    public sealed partial class KillConfirmAnimation
    {
        private void DrawDagoujiaoFrame(CanvasDrawingSession drawingSession)
        {
            if (!_isDagoujiaoActive || _currentDagoujiaoBitmap == null) return;
            double elapsed = _playbackClock.Elapsed.TotalMilliseconds;
            double entry = EaseOutCubic(Clamp01(elapsed / _currentDagoujiaoImpactMs));
            double settle = EaseOutCubic(Clamp01((elapsed - _currentDagoujiaoImpactMs) /
                (_currentDagoujiaoSettleMs - _currentDagoujiaoImpactMs)));
            double impactScale = elapsed <= _currentDagoujiaoImpactMs
                ? Lerp(0.08, 1.18, entry)
                : Lerp(1.18, 1.0, settle);
            double alpha = _currentDagoujiaoOpacity * Clamp01(elapsed / 70.0);
            if (elapsed > _currentDagoujiaoFadeStartMs)
            {
                double exit = EaseOutCubic(Clamp01(
                    (elapsed - _currentDagoujiaoFadeStartMs) /
                    (_currentDagoujiaoDurationMs - _currentDagoujiaoFadeStartMs)));
                alpha *= 1.0 - exit;
                impactScale *= Lerp(1.0, 1.05, exit);
            }

            double shake = Math.Max(0, 1.0 - elapsed / 390.0);
            double centerX = DagoujiaoFrameWidth / 2.0 + Math.Sin(elapsed * 0.12) * 9.0 * shake;
            double centerY = DagoujiaoFrameHeight / 2.0 + Math.Cos(elapsed * 0.16) * 6.0 * shake;
            DrawDagoujiaoBitmap(
                drawingSession,
                centerX,
                centerY,
                _currentDagoujiaoBaseScale * impactScale,
                alpha);
        }

        private void DrawDagoujiaoBitmap(
            CanvasDrawingSession drawingSession,
            double centerX,
            double centerY,
            double scale,
            double opacity)
        {
            double imageWidth = _currentDagoujiaoBitmap.SizeInPixels.Width;
            double imageHeight = _currentDagoujiaoBitmap.SizeInPixels.Height;
            if (imageWidth <= 0 || imageHeight <= 0 || opacity <= 0) return;
            double fit = Math.Min(360.0 / imageWidth, 360.0 / imageHeight) * scale;
            double width = imageWidth * fit;
            double height = imageHeight * fit;
            var target = new Rect(centerX - width / 2.0, centerY - height / 2.0, width, height);
            var source = new Rect(0, 0, imageWidth, imageHeight);
            bool chromaKey = string.Equals(
                    _currentDagoujiaoImageKey,
                    DagoujiaoSettingsStore.DefaultCommonImageKey,
                    StringComparison.OrdinalIgnoreCase)
                || string.Equals(
                    _currentDagoujiaoImageKey,
                    DagoujiaoSettingsStore.DefaultHeadshotImageKey,
                    StringComparison.OrdinalIgnoreCase);
            if (chromaKey)
            {
                using (var effect = new ChromaKeyEffect
                {
                    Source = _currentDagoujiaoBitmap,
                    Color = Color.FromArgb(255, 0, 255, 0),
                    Tolerance = 0.24f,
                    Feather = true,
                    InvertAlpha = false
                })
                {
                    drawingSession.DrawImage(effect, target, source, (float)Clamp01(opacity), CanvasImageInterpolation.Linear);
                }
                return;
            }
            drawingSession.DrawImage(
                _currentDagoujiaoBitmap,
                target,
                source,
                (float)Clamp01(opacity),
                CanvasImageInterpolation.Linear);
        }
    }
}

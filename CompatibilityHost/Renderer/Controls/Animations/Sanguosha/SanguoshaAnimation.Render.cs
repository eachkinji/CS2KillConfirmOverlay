using System;
using System.Numerics;
using KillConfirmCompatibility.Contracts;
using Microsoft.Graphics.Canvas;
using Windows.Foundation;
using Windows.UI;

namespace KillConfirmCompatibility.Controls
{
    public sealed partial class KillConfirmAnimation
    {
        private void DrawSanguoshaFrame(CanvasDrawingSession drawingSession)
        {
            SanguoshaTierAssets assets = _sanguoshaCurrent;
            if (!_isSanguoshaActive || assets == null) return;
            SanguoshaClip clip = assets.Clip;
            double elapsedMs = SanguoshaElapsedMs;
            if (elapsedMs >= clip.Length.TotalMilliseconds) return;
            var stage = new Rect(0, 0, clip.DesignWidth, clip.DesignHeight);

            Matrix3x2 previous = drawingSession.Transform;
            float pixelScale = Math.Max(0.05f, new Vector2(previous.M11, previous.M12).Length() * drawingSession.Dpi / 96f);
            if (_sanguoshaRenderer == null) _sanguoshaRenderer = new SanguoshaFrameRenderer();
            int frame = clip.FrameAt(TimeSpan.FromMilliseconds(elapsedMs));
            CanvasRenderTarget composed = _sanguoshaRenderer.Render(
                drawingSession.Device, clip, assets.Pages, frame, pixelScale);

            float shakeX = 0, shakeY = 0;
            _sanguoshaShake?.Offset(elapsedMs, out shakeX, out shakeY);
            try
            {
                drawingSession.Transform = Matrix3x2.CreateTranslation(shakeX, shakeY) * previous;
                drawingSession.DrawImage(
                    composed,
                    stage,
                    new Rect(0, 0, composed.SizeInPixels.Width, composed.SizeInPixels.Height),
                    1f,
                    CanvasImageInterpolation.Linear);
            }
            finally
            {
                drawingSession.Transform = previous;
            }
        }

        private void DisposeSanguoshaRenderer()
        {
            _sanguoshaRenderer?.Dispose();
            _sanguoshaRenderer = null;
        }
    }
}

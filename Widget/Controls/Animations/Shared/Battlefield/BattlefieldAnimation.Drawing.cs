using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Threading.Tasks;
using KillConfirmGameBar.Services;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.Graphics.Canvas.Text;
using Windows.Foundation;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Text;

namespace KillConfirmGameBar.Controls
{
    public sealed partial class KillConfirmAnimation
    {
        private static void DrawBattlefieldImageStretch(CanvasDrawingSession drawingSession, CanvasBitmap image, Rect target, double opacity)
        {
            if (image == null || opacity <= 0)
            {
                return;
            }

            drawingSession.DrawImage(
                image,
                target,
                new Rect(0, 0, image.SizeInPixels.Width, image.SizeInPixels.Height),
                (float)Clamp01(opacity),
                CanvasImageInterpolation.NearestNeighbor);
        }

        private static double EaseOutCubic(double value)
        {
            double t = Clamp01(value);
            return 1.0 - Math.Pow(1.0 - t, 3);
        }

        private static double EaseOutQuint(double value)
        {
            double t = Clamp01(value);
            return 1.0 - Math.Pow(1.0 - t, 5);
        }

    }
}

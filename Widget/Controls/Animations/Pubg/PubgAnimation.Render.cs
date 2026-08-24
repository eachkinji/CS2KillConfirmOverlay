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
        private void DrawPubgHudFrame(CanvasDrawingSession drawingSession)
        {
            double now = _playbackClock.Elapsed.TotalMilliseconds;
            using (CanvasTextFormat feedFormat = new CanvasTextFormat
            {
                FontFamily = "Segoe UI",
                FontSize = 12,
                FontWeight = FontWeights.Normal
            })
            using (CanvasTextFormat comboFormat = new CanvasTextFormat
            {
                FontFamily = "Segoe UI",
                FontSize = 12,
                FontWeight = FontWeights.Bold
            })
            {
                DrawPubgFeed(drawingSession, feedFormat, now);
                DrawPubgCombo(drawingSession, comboFormat, now);
            }
        }

    }
}

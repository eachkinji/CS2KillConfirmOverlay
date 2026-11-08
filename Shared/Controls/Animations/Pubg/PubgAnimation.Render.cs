// Shared by Widget (UWP) and CompatibilityHost; extracted from both former copies.
// Per-host lines stay inside #if WINDOWS_UWP blocks. See Tools/SharedSource/extract_shared.py.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Text;
using Windows.UI;
using Windows.UI.Text;

#if WINDOWS_UWP
namespace KillConfirmGameBar.Controls
#else
namespace KillConfirmCompatibility.Controls
#endif
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
                FontWeight = FontWeights.Bold
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

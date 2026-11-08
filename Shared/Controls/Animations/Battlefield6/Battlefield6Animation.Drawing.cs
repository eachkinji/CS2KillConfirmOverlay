// Shared by Widget (UWP) and CompatibilityHost; extracted from both former copies.
// Per-host lines stay inside #if WINDOWS_UWP blocks. See Tools/SharedSource/extract_shared.py.
using System;
using System.Numerics;
using Microsoft.Graphics.Canvas;
using Windows.Foundation;
using Windows.UI;

#if WINDOWS_UWP
namespace KillConfirmGameBar.Controls
#else
namespace KillConfirmCompatibility.Controls
#endif
{
    public sealed partial class KillConfirmAnimation
    {
        private static void DrawBattlefield6EntranceSquare(CanvasDrawingSession ds, Battlefield6Icon icon, double age)
        {
            if (age < 0 || age >= 400) return;
            double phase = age < 200 ? age / 200 : 1 - (age - 200) / 200;
            double size = age < 200 ? 15 : 15 * phase;
            double half = size / 2;
            Color fill = icon.Headshot ? Color.FromArgb(255, 255, 80, 0) : Color.FromArgb(255, 180, 180, 180);
            Color border = icon.Headshot ? Color.FromArgb(255, 255, 67, 0) : Color.FromArgb(255, 202, 202, 202);
            var bounds = new Rect(icon.X - half, Battlefield6IconY - half, size, size);
            ds.FillRectangle(bounds, Battlefield6WithAlpha(fill, phase * 0.3));
            ds.DrawRectangle(bounds, Battlefield6WithAlpha(border, phase * 0.8), 0.25f);
            double diagonal = age < 50 ? 1 - age / 50 : age < 200 ? (age - 50) / 150
                : age < 200 + 400.0 / 3 ? 1 - (age - 200) / (400.0 / 3) : 0;
            if (diagonal <= 0) return;
            double length = half * diagonal;
            Color line = Battlefield6WithAlpha(border, phase * 0.8);
            for (int dx = -1; dx <= 1; dx += 2)
                for (int dy = -1; dy <= 1; dy += 2)
                    ds.DrawLine(new Vector2((float)icon.X, (float)Battlefield6IconY),
                        new Vector2((float)(icon.X + dx * length), (float)(Battlefield6IconY + dy * length)), line, 0.25f);
        }

        private static void DrawBattlefield6TextEntrance(CanvasDrawingSession ds, double textX, double textY,
            double textWidth, double height, double age, double sweepMs, double peakAlpha, Color color, bool mirror)
        {
            const double fadeIn = 1000.0 / 15;
            const double fadeOut = 2000.0 / 15;
            if (age < 0 || age >= fadeIn + sweepMs + fadeOut || textWidth <= 0) return;
            double width;
            double alpha;
            if (age < fadeIn) { width = 2; alpha = peakAlpha * age / fadeIn; }
            else if (age < fadeIn + sweepMs)
            { width = Lerp(2, textWidth + 4, EaseOutCubic((age - fadeIn) / sweepMs)); alpha = peakAlpha; }
            else
            { width = textWidth + 4; alpha = peakAlpha * (1 - (age - fadeIn - sweepMs) / fadeOut); }
            double left = mirror ? textX + textWidth + 2 - width : textX - 2;
            ds.FillRectangle(new Rect(left, textY - 1, width, height + 2), Battlefield6WithAlpha(color, alpha));
        }
    }
}

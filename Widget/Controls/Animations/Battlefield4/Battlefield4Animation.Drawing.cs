using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Text;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Text;

namespace KillConfirmGameBar.Controls
{
    public sealed partial class KillConfirmAnimation
    {
        private static string FormatBattlefield4RewardSuffix(int reward)
        {
            return reward > 0
                ? " +" + FormatBattlefieldMoney(reward)
                : string.Empty;
        }
        private static Rect CreateBattlefield4TextClip(
            double left,
            double right,
            double y,
            double scale,
            CanvasTextFormat format)
        {
            double clippedLeft = Math.Max(0, left);
            double height = Math.Max(12, format.FontSize * scale + 6);
            return new Rect(
                clippedLeft,
                Math.Max(0, y - 3),
                Math.Max(0, right - clippedLeft),
                height);
        }

        private static void DrawBattlefield4ClippedGlowText(
            CanvasDrawingSession drawingSession,
            string text,
            double x,
            double y,
            double scale,
            Color color,
            CanvasTextFormat format,
            Rect clip)
        {
            if (clip.Width <= 0 || clip.Height <= 0)
            {
                return;
            }

            using (drawingSession.CreateLayer(1.0f, clip))
            {
                DrawBattlefield4GlowText(drawingSession, text, x, y, scale, color, format);
            }
        }

        private static void DrawBattlefield4GlowText(
            CanvasDrawingSession drawingSession,
            string text,
            double x,
            double y,
            double scale,
            Color color,
            CanvasTextFormat format)
        {
            byte glowAlpha = ToByte(color.A * 0.3);
            Color glow = Color.FromArgb(glowAlpha, 255, 255, 255);
            double[,] offsets =
            {
                { -0.3, 0 }, { 0.3, 0 }, { 0, -0.3 }, { 0, 0.3 },
                { -0.3, -0.3 }, { 0.3, -0.3 }, { -0.3, 0.3 }, { 0.3, 0.3 }
            };

            for (int i = 0; i < offsets.GetLength(0); i++)
            {
                DrawBattlefieldText(
                    drawingSession,
                    text,
                    x + offsets[i, 0],
                    y + offsets[i, 1],
                    scale,
                    glow,
                    format);
            }

            DrawBattlefieldText(
                drawingSession,
                text,
                x + 1,
                y + 1,
                scale,
                Color.FromArgb(ToByte(color.A * 0.65), 0, 0, 0),
                format);
            DrawBattlefieldText(drawingSession, text, x, y, scale, color, format);
        }

        private static byte ToByte(double value)
        {
            return (byte)Math.Max(0, Math.Min(255, value));
        }

    }
}

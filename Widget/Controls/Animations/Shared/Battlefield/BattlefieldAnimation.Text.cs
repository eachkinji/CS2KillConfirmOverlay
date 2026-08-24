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
        private static CanvasTextFormat CreateBattlefieldTextFormat()
        {
            return new CanvasTextFormat
            {
                FontFamily = BattlefieldFontFamily,
                FontSize = BattlefieldTextLineHeight,
                FontWeight = FontWeights.Bold
            };
        }

        private static double MeasureBattlefieldTextWidth(string text, CanvasTextFormat format)
        {
            Rect bounds = MeasureBattlefieldTextBounds(text, format);
            return Math.Max(0, Math.Ceiling(bounds.Width));
        }

        private static Rect MeasureBattlefieldTextBounds(string text, CanvasTextFormat format)
        {
            using (CanvasTextLayout layout = new CanvasTextLayout(
                CanvasDevice.GetSharedDevice(),
                string.IsNullOrEmpty(text) ? " " : text,
                format,
                1000,
                100))
            {
                return layout.DrawBounds;
            }
        }

        private static double MeasureBattlefieldTextAdvance(string text, CanvasTextFormat format)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 0;
            }

            using (CanvasTextLayout layout = new CanvasTextLayout(
                CanvasDevice.GetSharedDevice(),
                text,
                format,
                1000,
                100))
            {
                return Math.Max(0, layout.LayoutBounds.Width);
            }
        }

        private static void DrawBattlefieldTextAtLayoutOrigin(
            CanvasDrawingSession drawingSession,
            string text,
            double originX,
            double originY,
            double scale,
            Color color,
            CanvasTextFormat format)
        {
            if (string.IsNullOrEmpty(text) || scale <= 0 || color.A == 0)
            {
                return;
            }

            Matrix3x2 previousTransform = drawingSession.Transform;
            drawingSession.Transform =
                Matrix3x2.CreateScale((float)scale)
                * Matrix3x2.CreateTranslation(
                    (float)Math.Round(originX),
                    (float)Math.Round(originY))
                * previousTransform;

            try
            {
                float shadowOffset = (float)(1.0 / scale);
                using (CanvasSolidColorBrush shadowBrush = new CanvasSolidColorBrush(
                    drawingSession,
                    Color.FromArgb((byte)Math.Max(0, color.A * 0.65), 0, 0, 0)))
                using (CanvasSolidColorBrush textBrush = new CanvasSolidColorBrush(
                    drawingSession,
                    color))
                {
                    drawingSession.DrawText(
                        text,
                        shadowOffset,
                        shadowOffset,
                        shadowBrush,
                        format);
                    drawingSession.DrawText(text, 0, 0, textBrush, format);
                }
            }
            finally
            {
                drawingSession.Transform = previousTransform;
            }
        }
        private static void DrawBattlefieldText(
            CanvasDrawingSession drawingSession,
            string text,
            double x,
            double y,
            double scale,
            Color color,
            CanvasTextFormat format,
            bool useVisibleShadow = false)
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
                double shadowOffset = useVisibleShadow ? 1.25 / scale : 0.0;
                using (CanvasSolidColorBrush shadowBrush = new CanvasSolidColorBrush(
                    drawingSession,
                    Color.FromArgb((byte)Math.Max(0, color.A * 0.65), 0, 0, 0)))
                using (CanvasSolidColorBrush textBrush = new CanvasSolidColorBrush(drawingSession, color))
                {
                    drawingSession.DrawText(text, (float)shadowOffset, (float)shadowOffset, shadowBrush, format);
                    drawingSession.DrawText(text, 0, 0, textBrush, format);
                }
            }
            finally
            {
                drawingSession.Transform = previousTransform;
            }
        }

        private static void DrawBattlefieldTextCentered(
            CanvasDrawingSession drawingSession,
            string text,
            double centerX,
            double y,
            double scale,
            Color color,
            CanvasTextFormat format,
            bool useVisibleShadow = false)
        {
            if (string.IsNullOrEmpty(text) || scale <= 0 || color.A == 0)
            {
                return;
            }

            double width = MeasureBattlefieldTextWidth(text, format) * scale;
            DrawBattlefieldText(drawingSession, text, centerX - (width / 2.0), y, scale, color, format, useVisibleShadow);
        }

        private static void DrawBattlefieldTextRightAligned(
            CanvasDrawingSession drawingSession,
            string text,
            double rightX,
            double y,
            double scale,
            Color color,
            CanvasTextFormat format,
            bool useVisibleShadow = false)
        {
            if (string.IsNullOrEmpty(text) || scale <= 0 || color.A == 0)
            {
                return;
            }

            double width = MeasureBattlefieldTextWidth(text, format) * scale;
            DrawBattlefieldText(drawingSession, text, rightX - width, y, scale, color, format, useVisibleShadow);
        }


    }
}

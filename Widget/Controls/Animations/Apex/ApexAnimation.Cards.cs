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
        private void DrawApexCard(
            CanvasDrawingSession drawingSession,
            CanvasTextFormat primaryFormat,
            CanvasTextFormat secondaryFormat,
            ApexFeedItem item,
            double now)
        {
            string rewardText = "$" + item.MoneyReward.ToString(CultureInfo.InvariantCulture);
            string firstPrefix = item.IsAssist ? "助攻，击倒" : "消灭了";
            string firstText = firstPrefix + " " + item.TargetName;
            string secondText = item.IsAssist ? string.Empty : "得到 " + rewardText + " 金钱";
            double firstWidth = MeasureApexText(firstText, primaryFormat);
            double secondWidth = item.IsAssist ? 0 : MeasureApexText(secondText, secondaryFormat);
            double cardWidth = Math.Max(
                ApexCardMinimumWidth,
                Math.Min(ApexCardMaximumWidth, Math.Max(firstWidth, secondWidth) + 12));

            double enterElapsed = Math.Max(0, now - item.SpawnTimeMs);
            double enterScale = ResolveApexImpactScale(enterElapsed);
            double enterDrop = ResolveApexImpactDrop(enterElapsed);
            double enterAlpha = 1.0;
            double exitProgress = item.ExitStartTimeMs < 0
                ? 0
                : Clamp01((now - item.ExitStartTimeMs) / ApexCardExitMs);
            double opacity = enterAlpha * (1.0 - exitProgress);
            if (opacity <= 0.001)
            {
                return;
            }

            double cardX = (ApexFrameWidth - cardWidth) / 2.0;
            double cardY = item.CurrentY + enterDrop;
            Vector2 center = new Vector2(
                (float)(cardX + cardWidth / 2.0),
                (float)(cardY + ApexCardHeight / 2.0));
            Matrix3x2 previous = drawingSession.Transform;
            drawingSession.Transform = Matrix3x2.CreateScale((float)enterScale, center) * previous;

            try
            {
                var bounds = new Rect(cardX, cardY, cardWidth, ApexCardHeight);
                DrawApexTranslucentPanel(drawingSession, bounds, opacity);

                Color white = Color.FromArgb(ApexByte(opacity * 255), 247, 249, 250);
                Color red = item.IsAssist
                    ? white
                    : Color.FromArgb(ApexByte(opacity * 255), 242, 64, 54);
                double centerX = cardX + (cardWidth / 2.0);
                DrawApexCenteredSegments(
                    drawingSession,
                    primaryFormat,
                    centerX,
                    cardY + (item.IsAssist ? 17 : 6),
                    new ApexTextSegment(firstPrefix, white),
                    new ApexTextSegment(" " + item.TargetName, red));
                if (!item.IsAssist)
                {
                    DrawApexCenteredSegments(
                        drawingSession,
                        secondaryFormat,
                        centerX,
                        cardY + 30,
                        new ApexTextSegment("得到", white),
                        new ApexTextSegment(" " + rewardText + " ", red),
                        new ApexTextSegment("金钱", white));
                }
            }
            finally
            {
                drawingSession.Transform = previous;
            }
        }

        private static double ResolveApexImpactScale(double elapsedMs)
        {
            if (elapsedMs < 18)
            {
                return Lerp(11.5, 8.2, Clamp01(elapsedMs / 18.0));
            }

            if (elapsedMs < 48)
            {
                return Lerp(8.2, 3.9, Clamp01((elapsedMs - 18) / 30.0));
            }

            if (elapsedMs < 78)
            {
                return Lerp(3.9, 1.55, Clamp01((elapsedMs - 48) / 30.0));
            }

            if (elapsedMs < ApexImpactEnterMs)
            {
                return Lerp(1.55, 1.0, Clamp01((elapsedMs - 78) / (ApexImpactEnterMs - 78)));
            }

            return 1.0;
        }

        private static double ResolveApexImpactDrop(double elapsedMs)
        {
            if (elapsedMs < 18)
            {
                return Lerp(270, 216, Clamp01(elapsedMs / 18.0));
            }

            if (elapsedMs < 48)
            {
                return Lerp(216, 112, Clamp01((elapsedMs - 18) / 30.0));
            }

            if (elapsedMs < 78)
            {
                return Lerp(112, 32, Clamp01((elapsedMs - 48) / 30.0));
            }

            if (elapsedMs < ApexImpactEnterMs)
            {
                return Lerp(32, 0, Clamp01((elapsedMs - 78) / (ApexImpactEnterMs - 78)));
            }

            return 0;
        }

        private static void DrawApexTranslucentPanel(
            CanvasDrawingSession drawingSession,
            Rect bounds,
            double opacity)
        {
            byte cardAlpha = ApexByte(opacity * 255 * 0.24);
            drawingSession.FillRectangle(bounds, Color.FromArgb(cardAlpha, 52, 55, 59));
        }

        private static void DrawApexCenteredSegments(
            CanvasDrawingSession drawingSession,
            CanvasTextFormat format,
            double centerX,
            double y,
            params ApexTextSegment[] segments)
        {
            double totalWidth = 0;
            foreach (ApexTextSegment segment in segments)
            {
                totalWidth += MeasureApexText(segment.Text, format);
            }

            double advance = centerX - (totalWidth / 2.0);
            foreach (ApexTextSegment segment in segments)
            {
                drawingSession.DrawText(segment.Text, new Vector2((float)advance, (float)y), segment.Color, format);
                advance += MeasureApexText(segment.Text, format);
            }
        }

        private static double MeasureApexText(string text, CanvasTextFormat format)
        {
            using (var layout = new CanvasTextLayout(
                CanvasDevice.GetSharedDevice(),
                text ?? string.Empty,
                format,
                1000,
                60))
            {
                return Math.Ceiling(Math.Max(0, layout.LayoutBounds.Width));
            }
        }

    }
}

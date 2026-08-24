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
        private void DrawPubgFeed(
            CanvasDrawingSession drawingSession,
            CanvasTextFormat textFormat,
            double now)
        {
            double centerX = PubgFrameWidth / 2.0;
            double baseY = PubgFrameHeight - 96;
            for (int i = 0; i < _pubgHudState.FeedItems.Count; i++)
            {
                PubgFeedItem item = _pubgHudState.FeedItems[i];
                int positionFromBottom = _pubgHudState.FeedItems.Count - 1 - i;
                double elapsed = now - item.SpawnTimeMs;
                double alpha = ResolvePubgFeedAlpha(elapsed, positionFromBottom, i == _pubgHudState.FeedItems.Count - 1);
                if (alpha <= 0.05)
                {
                    continue;
                }

                DrawPubgFeedItem(
                    drawingSession,
                    textFormat,
                    item,
                    centerX,
                    baseY + item.CurrentY,
                    alpha,
                    Clamp01(elapsed / PubgFeedFadeInMs));
            }
        }

        private static double ResolvePubgFeedAlpha(
            double elapsed,
            int positionFromBottom,
            bool isNewest)
        {
            double alpha = 1;
            if (elapsed >= PubgFeedDisplayMs)
            {
                alpha = Math.Max(0, 1.0 - ((elapsed - PubgFeedDisplayMs) / PubgFeedFadeOutMs));
            }

            if (isNewest && elapsed < PubgFeedFadeInMs)
            {
                alpha = Math.Min(alpha, Clamp01(elapsed / PubgFeedFadeInMs));
            }

            if (PubgMaxFeedLines > 1)
            {
                alpha *= Math.Max(0, 1.0 - (positionFromBottom / (double)(PubgMaxFeedLines - 1)));
            }

            return Clamp01(alpha);
        }

        private void DrawPubgFeedItem(
            CanvasDrawingSession drawingSession,
            CanvasTextFormat textFormat,
            PubgFeedItem item,
            double centerX,
            double y,
            double alpha,
            double colorProgress)
        {
            var segments = new List<PubgTextSegment>();
            Color white = Color.FromArgb(255, 255, 255, 255);
            Color orange = InterpolatePubgColor(white, Color.FromArgb(255, 255, 53, 0), colorProgress);
            Color gold = InterpolatePubgColor(white, Color.FromArgb(255, 255, 215, 0), colorProgress);

            switch (item.Kind)
            {
                case PubgFeedKind.Headshot:
                    segments.Add(new PubgTextSegment("你用", white));
                    segments.Add(new PubgTextSegment(item.WeaponName, white));
                    segments.Add(new PubgTextSegment("命中头部", white));
                    segments.Add(new PubgTextSegment("淘汰", orange));
                    segments.Add(new PubgTextSegment("了 ", white));
                    segments.Add(new PubgTextSegment(item.TargetName, white));
                    break;
                case PubgFeedKind.Assist:
                    segments.Add(new PubgTextSegment("你", white));
                    segments.Add(new PubgTextSegment("助攻", gold));
                    segments.Add(new PubgTextSegment("淘汰了 ", white));
                    segments.Add(new PubgTextSegment(item.TargetName, white));
                    break;
                case PubgFeedKind.Normal:
                    segments.Add(new PubgTextSegment("你用", white));
                    segments.Add(new PubgTextSegment(item.WeaponName, white));
                    segments.Add(new PubgTextSegment("淘汰", orange));
                    segments.Add(new PubgTextSegment("了 ", white));
                    segments.Add(new PubgTextSegment(item.TargetName, white));
                    break;
                default:
                    segments.Add(new PubgTextSegment(item.PlainText, white));
                    break;
            }

            DrawPubgSegmentsCentered(drawingSession, textFormat, segments, centerX, y, alpha);
        }

        private static void DrawPubgSegmentsCentered(
            CanvasDrawingSession drawingSession,
            CanvasTextFormat textFormat,
            IList<PubgTextSegment> segments,
            double centerX,
            double y,
            double alpha)
        {
            string fullText = string.Empty;
            for (int i = 0; i < segments.Count; i++)
            {
                fullText += segments[i].Text;
            }

            if (string.IsNullOrEmpty(fullText))
            {
                return;
            }

            var fullBounds = MeasureBattlefieldTextBounds(fullText, textFormat);
            double originX = centerX - fullBounds.X - (fullBounds.Width / 2.0);
            double originY = y - fullBounds.Y;
            double advance = 0;
            byte a = PubgByte(alpha * 255);
            for (int i = 0; i < segments.Count; i++)
            {
                PubgTextSegment segment = segments[i];
                Color color = Color.FromArgb(a, segment.Color.R, segment.Color.G, segment.Color.B);
                DrawBattlefieldTextAtLayoutOrigin(
                    drawingSession,
                    segment.Text,
                    originX + advance,
                    originY,
                    1.0,
                    color,
                    textFormat);
                advance += MeasureBattlefieldTextAdvance(segment.Text, textFormat);
            }
        }
    }
}

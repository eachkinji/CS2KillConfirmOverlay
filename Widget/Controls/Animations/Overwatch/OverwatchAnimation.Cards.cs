using System;
using System.Collections.Generic;
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
        private void DrawOverwatchLowerThirdCard(
            CanvasDrawingSession drawingSession,
            OverwatchFeedItem item,
            double elapsedMs)
        {
            using (CanvasTextFormat textFormat = CreateOverwatchCardTextFormat())
            using (CanvasTextLayout textLayout = new CanvasTextLayout(
                CanvasDevice.GetSharedDevice(),
                GetOverwatchFeedText(item.TargetName, item.IsAssist),
                textFormat,
                1000,
                (float)OverwatchCardHeight))
            {
                double textWidth = Math.Ceiling(Math.Max(1, textLayout.LayoutBounds.Width));
                double cardWidth = OverwatchCardLeftPadding
                    + OverwatchCardIconSize
                    + OverwatchCardIconGap
                    + textWidth
                    + OverwatchCardRightPadding;

                double stripWidth;
                double stripHeight;
                double contentOpacity = 0;
                Color stripColor;

                if (elapsedMs < 90)
                {
                    double progress = EaseOutCubic(Clamp01(elapsedMs / 90.0));
                    stripWidth = Lerp(0, OverwatchCardMaximumStripWidth, progress);
                    stripHeight = 2;
                    stripColor = Color.FromArgb(235, 246, 218, 224);
                }
                else if (elapsedMs < 210)
                {
                    double progress = EaseOutCubic(Clamp01((elapsedMs - 90) / 120.0));
                    stripWidth = OverwatchCardMaximumStripWidth;
                    stripHeight = Lerp(2, OverwatchCardHeight, progress);
                    stripColor = OverwatchBlendColor(
                        Color.FromArgb(235, 246, 218, 224),
                        Color.FromArgb(238, 229, 112, 134),
                        progress);
                }
                else if (elapsedMs < 380)
                {
                    double progress = EaseOutCubic(Clamp01((elapsedMs - 210) / 170.0));
                    stripWidth = Lerp(OverwatchCardMaximumStripWidth, cardWidth, progress);
                    stripHeight = OverwatchCardHeight;
                    stripColor = OverwatchBlendColor(
                        Color.FromArgb(238, 229, 112, 134),
                        Color.FromArgb(242, 215, 49, 76),
                        progress);
                }
                else if (elapsedMs < 2760)
                {
                    stripWidth = cardWidth;
                    stripHeight = OverwatchCardHeight;
                    stripColor = Color.FromArgb(242, 215, 49, 76);
                    contentOpacity = EaseOutCubic(Clamp01((elapsedMs - 380) / 180.0));
                }
                else if (elapsedMs < 2940)
                {
                    double progress = EaseOutCubic(Clamp01((elapsedMs - 2760) / 180.0));
                    stripWidth = cardWidth;
                    stripHeight = Lerp(OverwatchCardHeight, 2, progress);
                    stripColor = Color.FromArgb(242, 215, 49, 76);
                    contentOpacity = 1.0 - EaseOutCubic(Clamp01((elapsedMs - 2760) / 100.0));
                }
                else
                {
                    double progress = EaseOutCubic(Clamp01(
                        (elapsedMs - 2940) / (OverwatchCardDurationMs - 2940)));
                    stripWidth = Lerp(cardWidth, 0, progress);
                    stripHeight = 2;
                    stripColor = Color.FromArgb(235, 246, 218, 224);
                }

                if (stripWidth <= 0.5 || stripHeight <= 0.5)
                {
                    return;
                }

                double stripX = (OverwatchFrameWidth - stripWidth) / 2.0;
                double stripY = item.CurrentCenterY - (stripHeight / 2.0);
                float cornerRadius = (float)Math.Min(2.5, stripHeight / 2.0);
                drawingSession.FillRoundedRectangle(
                    new Rect(stripX, stripY, stripWidth, stripHeight),
                    cornerRadius,
                    cornerRadius,
                    stripColor);

                if (contentOpacity <= 0.001)
                {
                    return;
                }

                double cardX = (OverwatchFrameWidth - cardWidth) / 2.0;
                double cardY = item.CurrentCenterY - (OverwatchCardHeight / 2.0);
                double iconX = cardX + OverwatchCardLeftPadding;
                double iconY = cardY + ((OverwatchCardHeight - OverwatchCardIconSize) / 2.0);
                drawingSession.DrawImage(
                    _overwatchKillIconBitmap,
                    new Rect(iconX, iconY, OverwatchCardIconSize, OverwatchCardIconSize),
                    new Rect(0, 0, 320, 320),
                    (float)Clamp01(contentOpacity),
                    CanvasImageInterpolation.Linear);

                byte textAlpha = (byte)Math.Max(
                    0,
                    Math.Min(255, Math.Round(contentOpacity * 255)));
                double textX = iconX + OverwatchCardIconSize + OverwatchCardIconGap;
                double textY = cardY + ((OverwatchCardHeight - OverwatchCardTextFontSize) / 2.0) - 2;
                drawingSession.DrawText(
                    GetOverwatchFeedText(item.TargetName, item.IsAssist),
                    (float)textX,
                    (float)textY,
                    Color.FromArgb(textAlpha, 255, 255, 255),
                    textFormat);
            }
        }

        private static string NormalizeOverwatchTargetName(string targetName)
        {
            string normalized = string.IsNullOrWhiteSpace(targetName)
                ? "敌方玩家"
                : targetName.Trim();
            return normalized.Length <= 32
                ? normalized
                : normalized.Substring(0, 31) + "…";
        }

        private void AddOverwatchFeedItem(
            string targetName,
            bool isAssist,
            double spawnTimeMs)
        {
            _overwatchFeedItems.Add(new OverwatchFeedItem
            {
                TargetName = NormalizeOverwatchTargetName(targetName),
                IsAssist = isAssist,
                SpawnTimeMs = spawnTimeMs,
                CurrentCenterY = OverwatchCardCenterY + 14
            });
            while (_overwatchFeedItems.Count > OverwatchMaximumCardCount)
            {
                _overwatchFeedItems.RemoveAt(0);
            }
        }

        private void UpdateOverwatchCardSelectionBounds()
        {
            int count = _overwatchFeedItems.Count;
            if (count <= 0)
            {
                return;
            }

            double maximumWidth = 180;
            foreach (OverwatchFeedItem item in _overwatchFeedItems)
            {
                maximumWidth = Math.Max(
                    maximumWidth,
                    MeasureOverwatchCardWidth(item.TargetName, item.IsAssist));
            }

            double height = (count * OverwatchCardHeight) + ((count - 1) * OverwatchCardGap);
            double top = OverwatchCardCenterY
                - ((count - 1) * (OverwatchCardHeight + OverwatchCardGap))
                - (OverwatchCardHeight / 2.0);
            _overwatchSelectionViewportWidth = maximumWidth;
            _overwatchSelectionViewportHeight = height;
            _overwatchSelectionViewportCenterOffsetX = 0;
            _overwatchSelectionViewportCenterOffsetY = top
                + (height / 2.0)
                - (OverwatchFrameHeight / 2.0);
            LogicalViewportSizeChanged?.Invoke(this, EventArgs.Empty);
        }

        private static CanvasTextFormat CreateOverwatchCardTextFormat()
        {
            return new CanvasTextFormat
            {
                FontFamily = "Microsoft YaHei UI",
                FontSize = (float)OverwatchCardTextFontSize,
                FontWeight = FontWeights.SemiBold,
                WordWrapping = CanvasWordWrapping.NoWrap
            };
        }

        private static double MeasureOverwatchCardWidth(string targetName, bool isAssist)
        {
            using (CanvasTextFormat format = CreateOverwatchCardTextFormat())
            using (CanvasTextLayout layout = new CanvasTextLayout(
                CanvasDevice.GetSharedDevice(),
                GetOverwatchFeedText(targetName, isAssist),
                format,
                1000,
                (float)OverwatchCardHeight))
            {
                double textWidth = Math.Ceiling(Math.Max(1, layout.LayoutBounds.Width));
                return OverwatchCardLeftPadding
                    + OverwatchCardIconSize
                    + OverwatchCardIconGap
                    + textWidth
                    + OverwatchCardRightPadding;
            }
        }

        private static string GetOverwatchFeedText(string targetName, bool isAssist)
        {
            string normalized = NormalizeOverwatchTargetName(targetName);
            return isAssist ? "助攻  " + normalized : normalized;
        }

        private static Color OverwatchBlendColor(Color from, Color to, double progress)
        {
            progress = Clamp01(progress);
            return Color.FromArgb(
                (byte)Math.Round(Lerp(from.A, to.A, progress)),
                (byte)Math.Round(Lerp(from.R, to.R, progress)),
                (byte)Math.Round(Lerp(from.G, to.G, progress)),
                (byte)Math.Round(Lerp(from.B, to.B, progress)));
        }

    }
}

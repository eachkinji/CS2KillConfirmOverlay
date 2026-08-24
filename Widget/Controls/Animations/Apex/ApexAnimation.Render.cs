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
        private void DrawApexFeedFrame(CanvasDrawingSession drawingSession)
        {
            if (!_isApexFeedActive)
            {
                return;
            }

            double now = _playbackClock.Elapsed.TotalMilliseconds;
            using (CanvasTextFormat primaryFormat = CreateApexPrimaryTextFormat())
            using (CanvasTextFormat secondaryFormat = CreateApexSecondaryTextFormat())
            using (CanvasTextFormat moneyFormat = CreateApexMoneyTextFormat())
            {
                if (_drawApexCards)
                {
                    for (int i = 0; i < _apexFeedState.Items.Count; i++)
                    {
                        DrawApexCard(
                            drawingSession,
                            primaryFormat,
                            secondaryFormat,
                            _apexFeedState.Items[i],
                            now);
                    }
                }

                if (_drawApexCrosshair)
                {
                    DrawApexCrosshairEffect(drawingSession, moneyFormat, now);
                }
            }
        }

        private static CanvasTextFormat CreateApexPrimaryTextFormat()
        {
            return new CanvasTextFormat
            {
                FontFamily = "Microsoft YaHei UI",
                FontSize = 17,
                FontWeight = FontWeights.SemiBold,
                WordWrapping = CanvasWordWrapping.NoWrap,
                HorizontalAlignment = CanvasHorizontalAlignment.Left,
                VerticalAlignment = CanvasVerticalAlignment.Top
            };
        }

        private static CanvasTextFormat CreateApexSecondaryTextFormat()
        {
            return new CanvasTextFormat
            {
                FontFamily = "Microsoft YaHei UI",
                FontSize = 14,
                FontWeight = FontWeights.Normal,
                WordWrapping = CanvasWordWrapping.NoWrap,
                HorizontalAlignment = CanvasHorizontalAlignment.Left,
                VerticalAlignment = CanvasVerticalAlignment.Top
            };
        }

        private static CanvasTextFormat CreateApexMoneyTextFormat()
        {
            return new CanvasTextFormat
            {
                FontFamily = "Bahnschrift",
                FontSize = 68,
                FontWeight = FontWeights.SemiBold,
                WordWrapping = CanvasWordWrapping.NoWrap,
                HorizontalAlignment = CanvasHorizontalAlignment.Left,
                VerticalAlignment = CanvasVerticalAlignment.Top
            };
        }

    }
}

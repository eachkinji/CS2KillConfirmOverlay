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
        private void UpdateModernWarfare2019Frame()
        {
            long nowUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            _modernWarfare2019FeedItems.RemoveAll(
                item => nowUnixMs - item.SpawnUnixMs >= ModernWarfare2019FeedEndMs);

            double elapsedMs = _playbackClock.Elapsed.TotalMilliseconds;
            bool currentImpactActive = (_drawModernWarfare2019Primary
                    && elapsedMs < ModernWarfare2019MoneyEndMs)
                || (_drawModernWarfare2019LowerBanner
                    && elapsedMs < ModernWarfare2019LowerBannerEndMs)
                || (_drawModernWarfare2019UpperBanner
                    && elapsedMs < ModernWarfare2019UpperEndMs);
            bool hasFeed = _drawModernWarfare2019Primary
                && _modernWarfare2019FeedItems.Count > 0;
            if (!currentImpactActive && !hasFeed)
            {
                _timer.Stop();
                _playbackClock.Stop();
                ResetModernWarfare2019State();
                Visibility = Visibility.Collapsed;
                return;
            }

            SpriteCanvas.Invalidate();
        }

        private void DrawModernWarfare2019Frame(CanvasDrawingSession drawingSession)
        {
            if (!_isModernWarfare2019Active)
            {
                return;
            }

            double elapsedMs = _playbackClock.Elapsed.TotalMilliseconds;
            if (_drawModernWarfare2019Primary
                && !_modernWarfare2019IsAssist
                && !_modernWarfare2019IsObjective
                && elapsedMs < ModernWarfare2019MarkerEndMs)
            {
                DrawModernWarfare2019Marker(drawingSession, elapsedMs);
            }

            if (_drawModernWarfare2019Primary && !_modernWarfare2019KillMarkOnly)
            {
                using (CanvasTextFormat moneyFormat = CreateModernWarfare2019MoneyFormat())
                using (CanvasTextFormat feedFormat = CreateModernWarfare2019FeedFormat())
                {
                    if (_modernWarfare2019MoneyReward > 0
                        && elapsedMs < ModernWarfare2019MoneyEndMs)
                    {
                        DrawModernWarfare2019Money(drawingSession, moneyFormat, elapsedMs);
                    }

                    DrawModernWarfare2019Feed(drawingSession, feedFormat);
                }
            }

            if (_drawModernWarfare2019LowerBanner
                && elapsedMs < ModernWarfare2019LowerBannerEndMs)
            {
                DrawModernWarfare2019LowerBanner(drawingSession, elapsedMs);
            }

            if (_drawModernWarfare2019UpperBanner
                && elapsedMs < ModernWarfare2019UpperEndMs)
            {
                DrawModernWarfare2019UpperBanner(drawingSession, elapsedMs);
            }
        }

        private void DrawModernWarfare2019Marker(CanvasDrawingSession drawingSession, double elapsedMs)
        {
            double centerX = ModernWarfare2019PrimaryFrameWidth / 2.0;
            double centerY = ModernWarfare2019FrameHeight / 2.0;
            double opacity = elapsedMs <= ModernWarfare2019MarkerHoldEndMs
                ? 1.0
                : 1.0 - ModernWarfare2019SmoothStep(
                    (elapsedMs - ModernWarfare2019MarkerHoldEndMs)
                    / (ModernWarfare2019MarkerEndMs - ModernWarfare2019MarkerHoldEndMs));

            double scale;
            double angleDegrees;
            if (elapsedMs < 125)
            {
                double progress = ModernWarfare2019EaseOutCubic(elapsedMs / 125.0);
                scale = Lerp(1.72, 0.88, progress);
                angleDegrees = Lerp(
                    _modernWarfare2019ImpactAngleDegrees,
                    -_modernWarfare2019ImpactAngleDegrees * 0.32,
                    progress);
            }
            else if (elapsedMs < 245)
            {
                double progress = ModernWarfare2019EaseOutCubic((elapsedMs - 125) / 120.0);
                scale = Lerp(0.88, 1.19, progress);
                angleDegrees = Lerp(
                    -_modernWarfare2019ImpactAngleDegrees * 0.32,
                    _modernWarfare2019ImpactAngleDegrees * 0.58,
                    progress);
            }
            else if (elapsedMs < 385)
            {
                double progress = ModernWarfare2019EaseOutCubic((elapsedMs - 245) / 140.0);
                scale = Lerp(1.19, 0.96, progress);
                angleDegrees = Lerp(
                    _modernWarfare2019ImpactAngleDegrees * 0.58,
                    -_modernWarfare2019ImpactAngleDegrees * 0.18,
                    progress);
            }
            else if (elapsedMs < 520)
            {
                double progress = ModernWarfare2019EaseOutBack((elapsedMs - 385) / 135.0);
                scale = Lerp(0.96, 1.0, progress);
                angleDegrees = Lerp(
                    -_modernWarfare2019ImpactAngleDegrees * 0.18,
                    0,
                    progress);
            }
            else
            {
                scale = 1.0;
                angleDegrees = 0;
            }

            byte alpha = ToModernWarfare2019Byte(opacity * 255.0);
            Color core = Color.FromArgb(alpha, 244, 36, 29);
            Color glow = Color.FromArgb(ToModernWarfare2019Byte(opacity * 76.0), 255, 38, 26);

            Matrix3x2 previous = drawingSession.Transform;
            Vector2 center = new Vector2((float)centerX, (float)centerY);
            drawingSession.Transform =
                Matrix3x2.CreateScale((float)scale, center)
                * Matrix3x2.CreateRotation((float)(angleDegrees * Math.PI / 180.0), center)
                * previous;
            try
            {
                DrawModernWarfare2019DiagonalArms(
                    drawingSession,
                    centerX,
                    centerY,
                    31,
                    65,
                    5.5,
                    core,
                    glow);

                if (_modernWarfare2019IsHeadshot)
                {
                    DrawModernWarfare2019DiagonalArms(
                        drawingSession,
                        centerX,
                        centerY,
                        74,
                        91,
                        4.2,
                        core,
                        glow);
                }
            }
            finally
            {
                drawingSession.Transform = previous;
            }
        }

    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.Graphics.Canvas.Text;
using Windows.Foundation;
using Windows.UI;

namespace KillConfirmGameBar.Controls
{
    public sealed partial class KillConfirmAnimation
    {
        private void DrawBattlefield5ScrollingFrame(CanvasDrawingSession drawingSession)
        {
            double currentTimeMs = _playbackClock.Elapsed.TotalMilliseconds;
            double centerY = BattlefieldFrameHeight - Battlefield5YOffset;

            foreach (Battlefield5ScrollIcon icon in _battlefield5ScrollState.ActiveIcons)
            {
                double elapsedMs = currentTimeMs - icon.StartTimeMs;
                double scale = ResolveBattlefield5Scale(elapsedMs);
                double alpha = ResolveBattlefield5Alpha(icon, currentTimeMs, elapsedMs);

                DrawBattlefield5Icon(drawingSession, icon.Icon, icon.CurrentX, centerY, scale, alpha);
                DrawBattlefield5HeadshotRing(drawingSession, icon, currentTimeMs, centerY);
            }

            using (CanvasTextFormat textFormat = CreateBattlefieldTextFormat())
            {
                DrawBattlefield5KillFeed(drawingSession, textFormat, currentTimeMs, BattlefieldFrameWidth / 2.0 - 1.0, BattlefieldFrameHeight - Battlefield5KillFeedYOffset);
                DrawBattlefield5MoneyScore(drawingSession, textFormat, currentTimeMs, BattlefieldFrameWidth / 2.0, BattlefieldFrameHeight - Battlefield5ScoreYOffset);
                DrawBattlefield5BonusList(drawingSession, textFormat, currentTimeMs, BattlefieldFrameWidth / 2.0, BattlefieldFrameHeight - Battlefield5BonusListYOffset);
            }
        }

        private static void DrawBattlefield5SingleFrame(CanvasDrawingSession drawingSession, BattlefieldKillAsset asset, int frame)
        {
            double currentTimeMs = frame * (1000.0 / FrameSequenceFps);
            var icon = new Battlefield5ScrollIcon(
                ResolveBattlefieldKillType(asset.IsHeadshot, asset.IsCrit, asset.IsAssist),
                asset.Icon,
                Battlefield5DisplaySeconds * 1000,
                asset.KillCount,
                asset.PlayerName,
                asset.WeaponLabel,
                asset.MoneyReward,
                asset.EventKind,
                asset.RoundNumber,
                asset.MoneyEpoch)
            {
                StartTimeMs = 0,
                CurrentX = BattlefieldFrameWidth / 2.0,
                RingStartTimeMs = asset.IsHeadshot ? Battlefield5RingDelayMs : -1
            };

            double scale = ResolveBattlefield5Scale(currentTimeMs);
            double alpha = ResolveBattlefield5Alpha(icon, currentTimeMs, currentTimeMs);
            double centerY = BattlefieldFrameHeight - Battlefield5YOffset;
            DrawBattlefield5Icon(drawingSession, asset.Icon, BattlefieldFrameWidth / 2.0, centerY, scale, alpha);
            DrawBattlefield5HeadshotRing(drawingSession, icon, currentTimeMs, centerY);
        }

        private static void DrawBattlefield5Icon(CanvasDrawingSession drawingSession, CanvasBitmap icon, double centerX, double centerY, double scale, double alpha)
        {
            if (icon == null || scale <= 0 || alpha <= 0)
            {
                return;
            }

            double size = Battlefield5BaseIconSize * scale;
            var target = new Rect(centerX - (size / 2.0), centerY - (size / 2.0), size, size);
            var source = new Rect(0, 0, icon.SizeInPixels.Width, icon.SizeInPixels.Height);
            drawingSession.DrawImage(icon, target, source, (float)Clamp01(alpha), CanvasImageInterpolation.NearestNeighbor);
        }

        private static void DrawBattlefield5HeadshotRing(CanvasDrawingSession drawingSession, Battlefield5ScrollIcon icon, double currentTimeMs, double centerY)
        {
            if (icon == null || icon.KillType != BattlefieldKillTypeHeadshot || icon.RingStartTimeMs < 0)
            {
                return;
            }

            double effectElapsed = currentTimeMs - icon.RingStartTimeMs;
            if (effectElapsed < 0 || effectElapsed > Battlefield5RingDurationMs)
            {
                return;
            }

            double t = Clamp01(effectElapsed / Battlefield5RingDurationMs);
            double eased = EaseOutCubic(t);
            double effectAlpha = (1.0 - t) * (1.0 - t);
            double baseRatio = 10.0 / 42.0;
            double minRadius = Battlefield5RingMaxRadius * baseRatio;
            double radius = minRadius + ((Battlefield5RingMaxRadius - minRadius) * eased);
            double thickness = Battlefield5RingThickness * (1.0 - t);
            if (thickness <= 0 || effectAlpha <= 0)
            {
                return;
            }

            using (CanvasSolidColorBrush brush = new CanvasSolidColorBrush(
                drawingSession,
                Color.FromArgb((byte)Math.Round(255 * effectAlpha), 0xF7, 0x7F, 0x00)))
            {
                drawingSession.DrawCircle(
                    (float)icon.CurrentX,
                    (float)centerY,
                    (float)radius,
                    brush,
                    (float)thickness);
            }
        }

        private static double ResolveBattlefield5Scale(double elapsedMs)
        {
            double endScale = Battlefield5Scale;
            double animationMs = Battlefield5AnimationSeconds * 1000;
            if (elapsedMs >= animationMs)
            {
                return endScale;
            }

            double initialScale = Battlefield5StartScale * Battlefield5Scale;
            double progress = EaseOutCubic(Clamp01(elapsedMs / animationMs));
            return Lerp(initialScale, endScale, progress);
        }

        private static double ResolveBattlefield5Alpha(Battlefield5ScrollIcon icon, double currentTimeMs, double elapsedMs)
        {
            double fadeDurationMs = Math.Max(1, Battlefield5AnimationSeconds * 1000);
            double fadeInProgress = Clamp01(elapsedMs / fadeDurationMs);
            double baseAlpha = EaseOutCubic(fadeInProgress);

            if (icon.ForcedFadeStartTimeMs >= 0)
            {
                double fadeProgress = (currentTimeMs - icon.ForcedFadeStartTimeMs) / fadeDurationMs;
                return Clamp01(baseAlpha * (1.0 - fadeProgress));
            }

            if (elapsedMs <= icon.DisplayDurationMs)
            {
                return baseAlpha;
            }

            double fadeElapsed = elapsedMs - icon.DisplayDurationMs;
            double normalFadeProgress = fadeElapsed / fadeDurationMs;
            return Clamp01(baseAlpha * (1.0 - normalFadeProgress));
        }
    }
}

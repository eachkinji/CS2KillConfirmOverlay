using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Threading.Tasks;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.Graphics.Canvas.Effects;
using Microsoft.Graphics.Canvas.Text;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Text;

namespace KillConfirmGameBar.Controls
{
    public sealed partial class KillConfirmAnimation
    {
        private void DrawBattlefield2042HudFrame(CanvasDrawingSession drawingSession)
        {
            double now = _playbackClock.Elapsed.TotalMilliseconds;
            DrawBattlefield2042KillIcons(drawingSession, now);
            DrawBattlefield2042Feed(drawingSession, _battlefield2042TextFormat, now);
            DrawBattlefield2042MoneyFeed(drawingSession, _battlefield2042TextFormat, now);
            DrawBattlefield2042MoneyTotal(drawingSession, _battlefield2042TextFormat, now);
            DrawBattlefield2042KilllogExitGlitch(drawingSession, now);
        }

        private void DrawBattlefield2042KillIcons(CanvasDrawingSession drawingSession, double now)
        {
            int visibleCount = 0;
            for (int i = 0; i < _battlefield2042HudState.KillIconItems.Count; i++)
            {
                if (now >= _battlefield2042HudState.KillIconItems[i].RevealTimeMs)
                {
                    visibleCount++;
                }
            }

            if (visibleCount == 0)
            {
                return;
            }

            double firstCenterX = Battlefield2042FrameWidth / 2.0
                - ((visibleCount - 1) * Battlefield2042KillIconSlotWidth / 2.0);
            int visibleIndex = 0;
            for (int i = 0; i < _battlefield2042HudState.KillIconItems.Count; i++)
            {
                Battlefield2042KillIconItem item = _battlefield2042HudState.KillIconItems[i];
                double elapsed = now - item.RevealTimeMs;
                if (elapsed < 0)
                {
                    continue;
                }

                double centerX = firstCenterX + visibleIndex * Battlefield2042KillIconSlotWidth;
                DrawBattlefield2042KillIcon(
                    drawingSession,
                    item,
                    centerX,
                    Battlefield2042KillIconCenterY,
                    elapsed);
                visibleIndex++;
            }
        }

        private static void DrawBattlefield2042KillIcon(
            CanvasDrawingSession drawingSession,
            Battlefield2042KillIconItem item,
            double centerX,
            double centerY,
            double elapsed)
        {
            if (item.Icon == null)
            {
                return;
            }

            string glitchFrameName = GetBattlefield2042GlitchFrameName(elapsed);
            CanvasBitmap glitchFrame = glitchFrameName == null
                ? null
                : GetCachedBattlefield2042Icon(glitchFrameName);
            if (glitchFrame != null)
            {
                var maskRect = new Rect(centerX - 19, centerY - 15.4, 38, 30.8);
                var glitchRect = new Rect(
                    centerX - 40.18595,
                    centerY - 26.32505,
                    80.3719,
                    52.6501);
                using (drawingSession.CreateLayer(1.0f, maskRect))
                {
                    DrawBattlefield2042Image(drawingSession, glitchFrame, glitchRect, 0.27058825);
                }
            }

            if (item.RenderCache != null)
            {
                DrawBattlefield2042CachedKillIcon(
                    drawingSession,
                    item.RenderCache,
                    centerX,
                    centerY);
            }
            else
            {
                CanvasBitmap shadow = GetCachedBattlefield2042Icon("SmoothCircle.png");
                if (shadow != null)
                {
                    DrawBattlefield2042TintedImage(
                        drawingSession,
                        shadow,
                        new Rect(centerX - 20, centerY - 20, 40, 40),
                        Color.FromArgb(255, 0, 0, 0),
                        0.27058825);
                }

                Color skullColor = item.IsHeadshot ? Battlefield2042HeadshotColor : Colors.White;
                double skullOpacity = item.IsHeadshot ? Battlefield2042HeadshotColor.A / 255.0 : 1.0;
                var skullRect = new Rect(
                    centerX - Battlefield2042KillIconSize / 2.0,
                    centerY - Battlefield2042KillIconSize / 2.0,
                    Battlefield2042KillIconSize,
                    Battlefield2042KillIconSize);
                DrawBattlefield2042TintedImageWithBloom(
                    drawingSession,
                    item.Icon,
                    skullRect,
                    skullColor,
                    skullOpacity,
                    item.IsHeadshot ? 0.55 : 0.42);
            }

            if (elapsed <= Battlefield2042KillstreakEntryMs)
            {
                double animAlpha = Clamp01(EvaluateBattlefield2042Curve(
                    Battlefield2042AnimSkullAlphaCurve,
                    elapsed));
                if (animAlpha > 0.0001)
                {
                    double animSize = EvaluateBattlefield2042Curve(
                        Battlefield2042AnimSkullSizeCurve,
                        elapsed);
                    double animX = EvaluateBattlefield2042Curve(
                        Battlefield2042AnimSkullXCurve,
                        elapsed);
                    Color animColor = item.IsHeadshot
                        ? Battlefield2042HeadshotHaloColor
                        : Colors.White;
                    var animRect = new Rect(
                        centerX + animX - animSize / 2.0,
                        centerY - animSize / 2.0,
                        animSize,
                        animSize);
                    DrawBattlefield2042TintedImageWithBloom(
                        drawingSession,
                        item.Icon,
                        animRect,
                        animColor,
                        animAlpha,
                        1.0);
                }

                if (elapsed >= 250 && elapsed < 416.6667)
                {
                    DrawBattlefield2042GlitchBars(
                        drawingSession,
                        Battlefield2042IconGlitchBarsA,
                        centerX + EvaluateBattlefield2042Curve(Battlefield2042IconGlitchAXCurve, elapsed),
                        centerY - EvaluateBattlefield2042Curve(Battlefield2042IconGlitchAYCurve, elapsed),
                        Colors.White,
                        0);
                }

                if (elapsed >= 300 && elapsed < 450)
                {
                    DrawBattlefield2042GlitchBars(
                        drawingSession,
                        Battlefield2042IconGlitchBarsB,
                        centerX + EvaluateBattlefield2042Curve(Battlefield2042IconGlitchBXCurve, elapsed),
                        centerY - EvaluateBattlefield2042Curve(Battlefield2042IconGlitchBYCurve, elapsed),
                        Colors.White,
                        item.IsHeadshot ? 8.92 : 0);
                }
            }
        }

        private static string GetBattlefield2042GlitchFrameName(double elapsed)
        {
            if (elapsed >= 166.6667 && elapsed < 183.3333)
            {
                return "Glitch0.png";
            }

            if (elapsed >= 183.3333 && elapsed < 250)
            {
                return "Glitch1.png";
            }

            if (elapsed >= 250 && elapsed < 266.6667)
            {
                return "Glitch0.png";
            }

            if (elapsed >= 316.6667 && elapsed < 333.3333)
            {
                return "Glitch2.png";
            }

            if (elapsed >= 333.3333 && elapsed < 366.6667)
            {
                return "Glitch3.png";
            }

            if (elapsed >= 366.6667 && elapsed < 383.3333)
            {
                return "Glitch1.png";
            }

            return null;
        }

        private static double EvaluateBattlefield2042Curve(
            Battlefield2042CurveKey[] keys,
            double elapsedMs)
        {
            if (keys == null || keys.Length == 0)
            {
                return 0;
            }

            int index = 0;
            for (int i = keys.Length - 1; i >= 0; i--)
            {
                if (elapsedMs >= keys[i].TimeMs)
                {
                    index = i;
                    break;
                }
            }

            Battlefield2042CurveKey key = keys[index];
            double deltaSeconds = Math.Max(0, elapsedMs - key.TimeMs) / 1000.0;
            return ((key.A * deltaSeconds + key.B) * deltaSeconds + key.C) * deltaSeconds + key.D;
        }

        private static void DrawBattlefield2042GlitchBars(
            CanvasDrawingSession drawingSession,
            Battlefield2042GlitchBar[] bars,
            double centerX,
            double centerY,
            Color color,
            double finalHeightOverride)
        {
            if (bars == null)
            {
                return;
            }

            CanvasBlend previousBlend = drawingSession.Blend;
            try
            {
                drawingSession.Blend = CanvasBlend.Add;
                for (int i = 0; i < bars.Length; i++)
                {
                    Battlefield2042GlitchBar bar = bars[i];
                    double height = finalHeightOverride > 0 && i == bars.Length - 1
                        ? finalHeightOverride
                        : bar.Height;
                    var rect = new Rect(
                        centerX + bar.X - bar.Width / 2.0,
                        centerY - bar.Y - height / 2.0,
                        bar.Width,
                        height);
                    drawingSession.FillRectangle(
                        new Rect(rect.X - 1, rect.Y - 1, rect.Width + 2, rect.Height + 2),
                        Color.FromArgb(32, color.R, color.G, color.B));
                    drawingSession.FillRectangle(rect, color);
                }
            }
            finally
            {
                drawingSession.Blend = previousBlend;
            }
        }

    }
}

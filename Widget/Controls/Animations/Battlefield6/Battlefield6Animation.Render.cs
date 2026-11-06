using System;
using System.Numerics;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Text;
using Windows.Foundation;
using Windows.UI;

namespace KillConfirmGameBar.Controls
{
    public sealed partial class KillConfirmAnimation
    {
        private void DrawBattlefield6HudFrame(CanvasDrawingSession ds)
        {
            EnsureBattlefield6TextFormats();
            double now = _playbackClock.Elapsed.TotalMilliseconds;
            Matrix3x2 previous = ds.Transform;
            // Upstream positions are Minecraft GUI units. A fixed 2x presentation gives
            // legible desktop text while retaining every relative spacing and motion.
            ds.Transform = Matrix3x2.CreateTranslation(-(float)Battlefield6TextX, -(float)Battlefield6IconY)
                * Matrix3x2.CreateScale(2)
                * Matrix3x2.CreateTranslation(400, 140) * previous;
            try
            {
                DrawBattlefield6Icons(ds, now);
                DrawBattlefield6Victim(ds, now);
                DrawBattlefield6Bonuses(ds, now);
                DrawBattlefield6Score(ds, now);
            }
            finally { ds.Transform = previous; }
        }

        private void DrawBattlefield6Icons(CanvasDrawingSession ds, double now)
        {
            foreach (Battlefield6Icon icon in _battlefield6HudState.Icons)
            {
                double age = now - icon.StartMs;
                DrawBattlefield6EntranceSquare(ds, icon, age);
                double animationAge = Math.Max(0, age - Battlefield6IconDelayMs);
                double entry = EaseOutCubic(animationAge / Battlefield6IconEntryMs);
                double fade = icon.ForcedFadeMs >= 0
                    ? Battlefield6Blink((now - icon.ForcedFadeMs) / Battlefield6IconFadeMs)
                    : animationAge <= Battlefield6IconHoldMs ? 1
                    : Battlefield6Blink((animationAge - Battlefield6IconHoldMs) / Battlefield6IconFadeMs);
                double alpha = entry * fade;
                double size = Battlefield6IconSize * entry;
                if (alpha <= 0 || size <= 0) continue;
                double x = icon.X - size / 2;
                double y = Battlefield6IconY - size / 2;
                Battlefield6RasterIcon raster = GetBattlefield6RasterIcon(ds, icon.Bitmap);
                DrawBattlefield6Image(ds, raster.Glow, x - size / 8, y - size / 8, size * 1.25, alpha * alpha);
                DrawBattlefield6Image(ds, raster.Base, x, y, size, alpha);
            }
        }

        private static void DrawBattlefield6Image(CanvasDrawingSession ds, CanvasBitmap bitmap,
            double x, double y, double size, double alpha)
        {
            ds.DrawImage(bitmap, new Rect(x, y, size, size),
                new Rect(0, 0, bitmap.SizeInPixels.Width, bitmap.SizeInPixels.Height),
                (float)Clamp01(alpha), CanvasImageInterpolation.Linear);
        }

        private void DrawBattlefield6Victim(CanvasDrawingSession ds, double now)
        {
            Battlefield6HudState state = _battlefield6HudState;
            if (string.IsNullOrEmpty(state.Victim) || state.VictimStartMs < 0) return;
            double age = now - state.VictimStartMs;
            if (age >= Battlefield6TextHoldMs + Battlefield6TextFadeMs) return;
            double alpha = age < 200 ? Clamp01(age / 200) : age <= Battlefield6TextHoldMs ? 1
                : Battlefield6Blink((age - Battlefield6TextHoldMs) / Battlefield6TextFadeMs);
            Color target = state.VictimIsRescue ? Color.FromArgb(255, 139, 226, 118) : Battlefield6Orange;
            double colorProgress = Clamp01(age / 200);
            Color color = Color.FromArgb(255,
                (byte)Lerp(255, target.R, colorProgress), (byte)Lerp(255, target.G, colorProgress), (byte)Lerp(255, target.B, colorProgress));
            DrawBattlefield6TextEntrance(ds, Battlefield6TextX - state.VictimWidth, state.VictimY,
                state.VictimWidth, 9, age, 200, 0.8, Color.FromArgb(255, 202, 110, 68), true);
            Color glow = state.VictimIsRescue ? Color.FromArgb(255, 99, 208, 72) : Color.FromArgb(255, 169, 80, 40);
            DrawBattlefield6Text(ds, state.Victim, Battlefield6TextX - state.VictimWidth, state.VictimY,
                color, alpha, _battlefield6TextFormat, glow, 0.2, 1.2);
        }

        private void DrawBattlefield6Bonuses(CanvasDrawingSession ds, double now)
        {
            Battlefield6HudState state = _battlefield6HudState;
            foreach (Battlefield6Bonus bonus in state.Bonuses)
            {
                double alpha = Clamp01((now - bonus.StartMs) / 20);
                if (bonus.FadeStartMs >= 0) alpha *= Battlefield6Blink((now - bonus.FadeStartMs) / Battlefield6BonusFadeMs);
                double x = Battlefield6TextX + bonus.X - bonus.Width;
                ds.DrawRectangle(new Rect(x - 2, state.BonusY - 1, bonus.Width + 4, 11),
                    Battlefield6WithAlpha(Colors.White, alpha), 0.25f);
                DrawBattlefield6Text(ds, bonus.Text, x, state.BonusY, Colors.White, alpha,
                    _battlefield6BonusFormat, Color.FromArgb(255, 180, 180, 180), 0.3, 0.5);
            }
        }

        private void DrawBattlefield6Score(CanvasDrawingSession ds, double now)
        {
            Battlefield6HudState state = _battlefield6HudState;
            if (!IsBattlefield6ScoreVisible(now)) return;
            double age = now - state.ScoreUpdatedMs;
            double alpha = age <= Battlefield6ScoreHoldMs ? 1
                : Battlefield6Blink((age - Battlefield6ScoreHoldMs) / Battlefield6ScoreFadeMs);
            DrawBattlefield6TextEntrance(ds, Battlefield6ScoreX, Battlefield6ScoreY, state.ScoreWidth, 13.5,
                now - state.ScoreBackgroundMs, 100, 0.6, Color.FromArgb(255, 231, 231, 231), false);
            DrawBattlefield6Text(ds, state.ScoreText, Battlefield6ScoreX, Battlefield6ScoreY,
                Colors.White, alpha, _battlefield6ScoreFormat, Color.FromArgb(255, 153, 153, 153), 0.2, 0.9);
        }

        private void DrawBattlefield6Text(CanvasDrawingSession ds, string text, double x, double y,
            Color color, double alpha, CanvasTextFormat format, Color glow, double intensity, double spread)
        {
            if (string.IsNullOrEmpty(text) || alpha <= 0) return;
            CanvasTextLayout layout = GetBattlefield6TextLayout(text, format);
            // Upstream y is the top of a Minecraft font line (9 units, baseline 7 down). DirectWrite
            // lines carry far more top leading (worse with CJK fallback), which dropped every BF6
            // text 2-4 units: names sank, bonus glyphs spilled out of their box and the score sat
            // below the icon. Pin the real baseline and ink-left edge to the upstream line box.
            double baseline = ReferenceEquals(format, _battlefield6ScoreFormat)
                ? Battlefield6ScoreBaseline : Battlefield6TextBaseline;
            CanvasLineMetrics[] lines = layout.LineMetrics;
            y += baseline - (lines.Length > 0 ? lines[0].Baseline : format.FontSize * 0.8);
            x -= layout.DrawBounds.X;
            Battlefield6TextGlow cached = GetBattlefield6TextGlow(ds, text, format, layout, glow, intensity, spread);
            if (cached != null)
            {
                Rect bounds = cached.Bounds;
                ds.DrawImage(cached.Surface, new Rect(x + bounds.X, y + bounds.Y, bounds.Width, bounds.Height),
                    cached.Surface.Bounds, (float)Clamp01(alpha), CanvasImageInterpolation.Linear);
            }
            ds.DrawTextLayout(layout, (float)x, (float)y, Battlefield6WithAlpha(color, alpha));
        }
    }
}

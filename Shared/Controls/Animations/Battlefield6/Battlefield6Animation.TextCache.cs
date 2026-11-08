// Shared by Widget (UWP) and CompatibilityHost; extracted from both former copies.
// Per-host lines stay inside #if WINDOWS_UWP blocks. See Tools/SharedSource/extract_shared.py.
using System;
using System.Collections.Generic;
using System.Numerics;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Effects;
using Microsoft.Graphics.Canvas.Text;
using Windows.Foundation;
using Windows.UI;

#if WINDOWS_UWP
namespace KillConfirmGameBar.Controls
#else
namespace KillConfirmCompatibility.Controls
#endif
{
    public sealed partial class KillConfirmAnimation
    {
        private readonly Dictionary<string, CanvasTextLayout> _battlefield6TextLayouts =
            new Dictionary<string, CanvasTextLayout>(StringComparer.Ordinal);
        private readonly Dictionary<string, Battlefield6TextGlow> _battlefield6TextGlows =
            new Dictionary<string, Battlefield6TextGlow>(StringComparer.Ordinal);
        private CanvasTextLayout _battlefield6ScoreLayout;
        private string _battlefield6ScoreLayoutText;
        private Battlefield6TextGlow _battlefield6ScoreGlow;
        private string _battlefield6ScoreGlowKey;

        private sealed class Battlefield6TextGlow : IDisposable
        {
            public CanvasRenderTarget Surface;
            // GUI units relative to the text layout origin.
            public Rect Bounds;
            public void Dispose() => Surface?.Dispose();
        }

        private CanvasTextLayout GetBattlefield6TextLayout(string text, CanvasTextFormat format)
        {
            if (ReferenceEquals(format, _battlefield6ScoreFormat))
            {
                if (_battlefield6ScoreLayout != null && text == _battlefield6ScoreLayoutText)
                    return _battlefield6ScoreLayout;
                _battlefield6ScoreLayout?.Dispose();
                _battlefield6ScoreLayout = new CanvasTextLayout(CanvasDevice.GetSharedDevice(), text, format, 1000, 100);
                _battlefield6ScoreLayoutText = text;
                return _battlefield6ScoreLayout;
            }
            // Victim and bonus lines use different fonts; the same string must not share a layout.
            string layoutKey = (ReferenceEquals(format, _battlefield6BonusFormat) ? "b|" : "t|") + text;
            if (_battlefield6TextLayouts.TryGetValue(layoutKey, out CanvasTextLayout layout)) return layout;
            // Bound distinct victims and temporary long-name trimming candidates per session.
            if (_battlefield6TextLayouts.Count >= 128)
            {
                foreach (CanvasTextLayout cached in _battlefield6TextLayouts.Values) cached.Dispose();
                _battlefield6TextLayouts.Clear();
            }
            layout = new CanvasTextLayout(CanvasDevice.GetSharedDevice(), text, format, 1000, 100);
            _battlefield6TextLayouts[layoutKey] = layout;
            return layout;
        }

        private Battlefield6TextGlow GetBattlefield6TextGlow(CanvasDrawingSession ds, string text, CanvasTextFormat format,
            CanvasTextLayout layout, Color glow, double intensity, double spread)
        {
            // Upstream stacks eight hard copies at +/-glow_size, which suits Minecraft's pixel font
            // but leaves stepped ghost edges around anti-aliased text once scaled up. Bake one
            // Gaussian halo instead: the same coverage those copies reach, softened by glow_size.
            var matrix = ds.Transform;
            double scale = Math.Max(Math.Sqrt(matrix.M11 * matrix.M11 + matrix.M12 * matrix.M12),
                Math.Sqrt(matrix.M21 * matrix.M21 + matrix.M22 * matrix.M22)) * ds.Dpi / 96.0;
            int pixelsPerUnit = 1;
            while (pixelsPerUnit < scale && pixelsPerUnit < 16) pixelsPerUnit *= 2;
            bool score = ReferenceEquals(format, _battlefield6ScoreFormat);
            string font = ReferenceEquals(format, _battlefield6BonusFormat) ? "b" : "t";
            string key = $"{font}|{glow}|{intensity:R}|{spread:R}|{pixelsPerUnit}|{text}";
            if (score)
            {
                if (_battlefield6ScoreGlow != null && key == _battlefield6ScoreGlowKey) return _battlefield6ScoreGlow;
            }
            else if (_battlefield6TextGlows.TryGetValue(key, out Battlefield6TextGlow existing)) return existing;

            Rect draw = layout.DrawBounds;
            if (draw.Width <= 0 || draw.Height <= 0 || intensity <= 0) return null;
            double padding = Math.Ceiling(spread * 3 + 1);
            var result = new Battlefield6TextGlow
            {
                Bounds = new Rect(draw.X - padding, draw.Y - padding, draw.Width + padding * 2, draw.Height + padding * 2)
            };
            try
            {
                result.Surface = new CanvasRenderTarget(ds.Device,
                    (float)Math.Ceiling(result.Bounds.Width * pixelsPerUnit),
                    (float)Math.Ceiling(result.Bounds.Height * pixelsPerUnit), 96);
                double coverage = 1 - Math.Pow(1 - Clamp01(intensity), Battlefield6GlowOffsets.Length);
                using (var source = new CanvasCommandList(result.Surface))
                {
                    using (CanvasDrawingSession glyphs = source.CreateDrawingSession())
                    {
                        glyphs.Transform = Matrix3x2.CreateTranslation((float)-result.Bounds.X, (float)-result.Bounds.Y)
                            * Matrix3x2.CreateScale(pixelsPerUnit);
                        glyphs.DrawTextLayout(layout, 0, 0, Battlefield6WithAlpha(glow, coverage));
                    }
                    using (var blur = new GaussianBlurEffect
                    {
                        Source = source,
                        BlurAmount = (float)Math.Max(0.5, spread * pixelsPerUnit),
                        BorderMode = EffectBorderMode.Soft
                    })
                    using (CanvasDrawingSession target = result.Surface.CreateDrawingSession())
                    {
                        target.Clear(Colors.Transparent);
                        target.DrawImage(blur);
                    }
                }
            }
            catch { result.Dispose(); throw; }

            if (score)
            {
                // The score counter re-formats while it rolls, so keep only its latest glow.
                _battlefield6ScoreGlow?.Dispose();
                _battlefield6ScoreGlow = result;
                _battlefield6ScoreGlowKey = key;
                return result;
            }
            if (_battlefield6TextGlows.Count >= 128)
            {
                foreach (Battlefield6TextGlow cached in _battlefield6TextGlows.Values) cached.Dispose();
                _battlefield6TextGlows.Clear();
            }
            _battlefield6TextGlows[key] = result;
            return result;
        }

        private double MeasureBattlefield6TextWidth(string text, CanvasTextFormat format)
        {
            return string.IsNullOrEmpty(text) ? 0 : Math.Max(0, Math.Ceiling(GetBattlefield6TextLayout(text, format).DrawBounds.Width));
        }

        private void ClearBattlefield6TextLayouts()
        {
            _battlefield6ScoreLayout?.Dispose();
            _battlefield6ScoreLayout = null;
            _battlefield6ScoreLayoutText = null;
            foreach (CanvasTextLayout layout in _battlefield6TextLayouts.Values) layout.Dispose();
            _battlefield6TextLayouts.Clear();
            _battlefield6ScoreGlow?.Dispose();
            _battlefield6ScoreGlow = null;
            _battlefield6ScoreGlowKey = null;
            foreach (Battlefield6TextGlow glow in _battlefield6TextGlows.Values) glow.Dispose();
            _battlefield6TextGlows.Clear();
        }
    }
}

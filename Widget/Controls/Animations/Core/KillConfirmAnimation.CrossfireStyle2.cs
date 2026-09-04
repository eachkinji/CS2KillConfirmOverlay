using System;
using System.Collections.Generic;
using System.Numerics;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Effects;
using KillConfirmGameBar.Services;
using Windows.Foundation;
using Windows.UI;

namespace KillConfirmGameBar.Controls
{
    public sealed partial class KillConfirmAnimation
    {
        private void DrawCrossfireStyle2(CanvasDrawingSession session, double elapsedMs)
        {
            var resources = new List<IDisposable>();
            try
            {
                ICanvasImage composed = null;
                var main = CrossfireStyle2Motion.Sample(elapsedMs);
                var badgeLayout = new CrossfireStyle2Motion.Layout(160, 160);
                ComposeCrossfireStyle2Layer(ref composed, resources, _currentCodeAsset.Main,
                    CrossfireStyle2Motion.MainLayout(_currentCodeAsset.Action), main, true, true);
                // Soldier badge is normal-alpha artwork, above the main image.
                ComposeCrossfireStyle2Layer(ref composed, resources, _currentCodeAsset.WeaponBadge,
                    badgeLayout, main, false, false);
                ComposeCrossfireStyle2Layer(ref composed, resources, _currentCodeAsset.Overlay,
                    badgeLayout, main, true, true);
                ComposeCrossfireStyle2Layer(ref composed, resources, _currentCodeAsset.Fx,
                    new CrossfireStyle2Motion.Layout(95, 95), CrossfireStyle2Motion.Sample(elapsedMs, true), true, false, true);
                if (_currentCodeAsset.EventOverlay != null)
                {
                    var size = _currentCodeAsset.EventOverlay.SizeInPixels;
                    ComposeCrossfireStyle2Layer(ref composed, resources, _currentCodeAsset.EventOverlay,
                        new CrossfireStyle2Motion.Layout(size.Width, size.Height), main, true, true);
                }
                int frame = CrossfirePackFormat.SequenceFrame(elapsedMs);
                if (_currentCodeAsset.Sequence != null && frame >= 0)
                {
                    var sequenceState = new CrossfireStyle2Motion.State { Alpha = 1,
                        Scale = Math.Min(1.4, Math.Floor(elapsedMs / 15) * 0.33) / 1.4 };
                    ComposeCrossfireStyle2Layer(ref composed, resources, _currentCodeAsset.Sequence[frame],
                        new CrossfireStyle2Motion.Layout(400, 158), sequenceState, true, true);
                }
                if (composed != null) session.DrawImage(composed);
            }
            finally
            {
                for (int i = resources.Count - 1; i >= 0; i--) resources[i].Dispose();
            }
        }

        private static void ComposeCrossfireStyle2Layer(ref ICanvasImage composed, List<IDisposable> resources,
            CanvasBitmap bitmap, CrossfireStyle2Motion.Layout layout, CrossfireStyle2Motion.State state,
            bool screen, bool boost, bool glow = false)
        {
            if (bitmap == null || state.Scale <= 0 || state.Alpha <= 0) return;
            const double unit = CrossfireStyle2Motion.Unit;
            double width = layout.Width * unit;
            double height = layout.Height * unit;
            var layer = new CanvasCommandList(CanvasDevice.GetSharedDevice());
            resources.Add(layer);
            using (var draw = layer.CreateDrawingSession())
            {
                DrawCenteredScaledImage(draw, bitmap, (layout.X + state.X) * unit - width / 2,
                    (layout.Y + state.Y) * unit - height / 2, width, height, state.Scale, 1);
            }
            ICanvasImage foreground = layer;
            if (boost)
            {
                // CSS brightness(1.2) saturate(1.3), including saturation above 1.
                const float b = 1.2f, s = 1.3f;
                float r = (1 - s) * 0.213f, g = (1 - s) * 0.715f, blue = (1 - s) * 0.072f;
                var effect = new ColorMatrixEffect { Source = foreground, ClampOutput = true,
                    ColorMatrix = new Matrix5x4 {
                        M11 = b * (r + s), M12 = b * r, M13 = b * r,
                        M21 = b * g, M22 = b * (g + s), M23 = b * g,
                        M31 = b * blue, M32 = b * blue, M33 = b * (blue + s), M44 = 1 } };
                resources.Add(effect);
                foreground = effect;
            }
            if (glow)
            {
                // CSS chains the three drop shadows, each including the previous result.
                for (int i = 0; i < 3; i++)
                {
                    var shadow = new ShadowEffect { Source = foreground,
                        BlurAmount = (float)((i + 1) * 5 * unit * state.Scale),
                        ShadowColor = Color.FromArgb((byte)(204 - i * 51), 255, 255, 255) };
                    var glowLayer = new CompositeEffect { Mode = CanvasComposite.SourceOver, Sources = { shadow, foreground } };
                    resources.Add(shadow);
                    resources.Add(glowLayer);
                    foreground = glowLayer;
                }
            }
            var opacity = new OpacityEffect { Source = foreground, Opacity = (float)state.Alpha };
            resources.Add(opacity);
            foreground = opacity;
            if (composed == null) composed = foreground;
            else if (screen)
            {
                var blend = new BlendEffect { Background = composed, Foreground = foreground, Mode = BlendEffectMode.Screen };
                resources.Add(blend);
                composed = blend;
            }
            else
            {
                var blend = new CompositeEffect { Mode = CanvasComposite.SourceOver, Sources = { composed, foreground } };
                resources.Add(blend);
                composed = blend;
            }
        }
    }
}

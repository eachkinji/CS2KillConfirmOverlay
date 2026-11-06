using System;
using System.Collections.Generic;
using System.Numerics;
using KillConfirmCompatibility.Contracts;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Effects;
using Windows.Foundation;
using Windows.UI;

namespace KillConfirmGameBar.Controls
{
    // Replays one SGSD frame exactly as LayaAir's WebGL 2D pipeline drew it:
    //  * texels are premultiplied (WebGL UNPACK_PREMULTIPLY_ALPHA);
    //  * TEXTURE2D shader: v_color = attribColor/255, v_color.rgb *= v_color.a,
    //    color.a *= v_color.a, color.rgb *= v_color.rgb  =>  texel * (r*a, g*a, b*a, a);
    //  * BlendMode normal = (ONE, ONE_MINUS_SRC_ALPHA), "light" = (ONE, ONE),
    //    screen = (ONE, ONE_MINUS_SRC_COLOR), all applied to every channel.
    internal sealed class SanguoshaFrameRenderer : IDisposable
    {
        private CanvasRenderTarget _accumulate;
        private CanvasRenderTarget _spare;
        private CanvasRenderTarget _screenLayer;
        private readonly List<CropEffect> _crops = new List<CropEffect>();
        private readonly List<ColorMatrixEffect> _tints = new List<ColorMatrixEffect>();
        private int _tintCursor;

        // Direct hardware sprite-batch path used during live HUD playback when a clip contains
        // no Screen-blend draws (all 22 built-in Sanguosha clips have HasScreen == false).
        // Avoids allocating/clearing/blitting 3 full-screen offscreen CanvasRenderTargets per frame.
        public void RenderDirect(CanvasDrawingSession session, SanguoshaClip clip, CanvasBitmap[] pages, int frame, Matrix3x2 baseTransform)
        {
            if (clip?.Frames == null || clip.Frames.Length == 0) return;
            SanguoshaDraw[] draws = clip.Frames[Math.Max(0, Math.Min(clip.Frames.Length - 1, frame))];
            _tintCursor = 0;
            Matrix3x2 previous = session.Transform;
            try
            {
                for (int i = 0; i < draws.Length; i++)
                {
                    DrawCommand(session, clip, pages, ref draws[i], baseTransform);
                }
            }
            finally
            {
                session.Transform = previous;
                ReleaseFrameEffectSources();
            }
        }

        public CanvasRenderTarget Render(CanvasDevice device, SanguoshaClip clip, CanvasBitmap[] pages, int frame, float pixelScale)
        {
            int width = Math.Max(1, (int)Math.Ceiling(clip.DesignWidth * pixelScale));
            int height = Math.Max(1, (int)Math.Ceiling(clip.DesignHeight * pixelScale));
            Ensure(ref _accumulate, device, width, height);
            if (clip.HasScreen)
            {
                Ensure(ref _spare, device, width, height);
                Ensure(ref _screenLayer, device, width, height);
            }
            _tintCursor = 0;

            SanguoshaDraw[] draws = clip.Frames[Math.Max(0, Math.Min(clip.Frames.Length - 1, frame))];
            Matrix3x2 toPixels = Matrix3x2.CreateScale(pixelScale);
            CanvasDrawingSession session = _accumulate.CreateDrawingSession();
            try
            {
                session.Clear(Colors.Transparent);
                for (int i = 0; i < draws.Length; i++)
                {
                    if (draws[i].Blend != SanguoshaBlend.Screen)
                    {
                        DrawCommand(session, clip, pages, ref draws[i], toPixels);
                        continue;
                    }

                    session.Dispose();
                    session = null;
                    using (CanvasDrawingSession layer = _screenLayer.CreateDrawingSession())
                    {
                        layer.Clear(Colors.Transparent);
                        DrawCommand(layer, clip, pages, ref draws[i], toPixels);
                    }

                    // S + D - S*D on premultiplied values for every channel, i.e.
                    // gl.blendFunc(ONE, ONE_MINUS_SRC_COLOR) applied to RGB and A.
                    using (var screen = new ArithmeticCompositeEffect
                    {
                        Source1 = _screenLayer,
                        Source2 = _accumulate,
                        MultiplyAmount = -1,
                        Source1Amount = 1,
                        Source2Amount = 1,
                        Offset = 0,
                        ClampOutput = true
                    })
                    using (CanvasDrawingSession compose = _spare.CreateDrawingSession())
                    {
                        compose.Blend = CanvasBlend.Copy;
                        compose.DrawImage(screen);
                    }

                    CanvasRenderTarget swap = _accumulate;
                    _accumulate = _spare;
                    _spare = swap;
                    session = _accumulate.CreateDrawingSession();
                }
            }
            finally
            {
                session?.Dispose();
                ReleaseFrameEffectSources();
            }
            return _accumulate;
        }

        private void DrawCommand(CanvasDrawingSession session, SanguoshaClip clip, CanvasBitmap[] pages, ref SanguoshaDraw d, Matrix3x2 toPixels)
        {
            if ((d.Rgba >> 24) == 0) return;
            SanguoshaRegion r = clip.Regions[d.Region];
            CanvasBitmap page = pages[r.Page];
            if (page == null) return;
            float pw = page.SizeInPixels.Width, ph = page.SizeInPixels.Height;

            // Texture-space corners of the region (rotated atlas regions included).
            float ax = r.U0 * pw, ay = r.V0 * ph;
            float bx = r.U1 * pw, by = r.V1 * ph;
            float cx = r.U2 * pw, cy = r.V2 * ph;
            float dx = r.U3 * pw, dy = r.V3 * ph;
            float minX = Math.Min(Math.Min(ax, bx), Math.Min(cx, dx));
            float minY = Math.Min(Math.Min(ay, by), Math.Min(cy, dy));
            float maxX = Math.Max(Math.Max(ax, bx), Math.Max(cx, dx));
            float maxY = Math.Max(Math.Max(ay, by), Math.Max(cy, dy));
            if (maxX - minX <= 0 || maxY - minY <= 0) return;

            Matrix3x2 affine;
            if (!SolveAffine(
                ax - minX, ay - minY, bx - minX, by - minY, cx - minX, cy - minY,
                d.X0 + clip.OriginX, d.Y0 + clip.OriginY,
                d.X1 + clip.OriginX, d.Y1 + clip.OriginY,
                d.X2 + clip.OriginX, d.Y2 + clip.OriginY,
                out affine))
                return;

            session.Transform = affine * toPixels;
            var source = new Rect(minX, minY, maxX - minX, maxY - minY);
            CanvasComposite composite = d.Blend == SanguoshaBlend.Light ? CanvasComposite.Add : CanvasComposite.SourceOver;

            // Fast hardware sprite-batch path when RGB is untinted white (255,255,255) and only alpha varies.
            // Covers ~88% of all non-opaque draws in Sanguosha clips without invoking Direct2D's effect graph.
            if ((d.Rgba & 0x00FFFFFFu) == 0x00FFFFFFu)
            {
                session.DrawImage(page, Vector2.Zero, source, d.A, CanvasImageInterpolation.Linear, composite);
                return;
            }

            float a = d.A;
            ColorMatrixEffect tint = NextTint(out CropEffect crop);
            crop.Source = page;
            crop.SourceRectangle = source;
            tint.Source = crop;
            // Straight mode: the matrix multiplies the premultiplied texel directly,
            // exactly like Laya's fragment shader (no un-premultiply round trip).
            tint.AlphaMode = CanvasAlphaMode.Straight;
            tint.ColorMatrix = new Matrix5x4 { M11 = d.R * a, M22 = d.G * a, M33 = d.B * a, M44 = a };
            session.DrawImage(tint, Vector2.Zero, source, 1f, CanvasImageInterpolation.Linear, composite);
        }

        // Effects stay referenced by Direct2D until the session flushes, so every tinted draw in a
        // frame gets its own instance; the pool is reused on the next frame.
        private ColorMatrixEffect NextTint(out CropEffect crop)
        {
            if (_tintCursor == _tints.Count)
            {
                _crops.Add(new CropEffect { BorderMode = EffectBorderMode.Hard });
                _tints.Add(new ColorMatrixEffect { ClampOutput = false });
            }
            crop = _crops[_tintCursor];
            return _tints[_tintCursor++];
        }

        private void ReleaseFrameEffectSources()
        {
            for (int i = 0; i < _tintCursor; i++)
            {
                _tints[i].Source = null;
                _crops[i].Source = null;
            }
        }

        internal static bool SolveAffine(
            float q0x, float q0y, float q1x, float q1y, float q2x, float q2y,
            float v0x, float v0y, float v1x, float v1y, float v2x, float v2y,
            out Matrix3x2 m)
        {
            double a1 = q1x - q0x, b1 = q1y - q0y, a2 = q2x - q0x, b2 = q2y - q0y;
            double det = a1 * b2 - b1 * a2;
            m = Matrix3x2.Identity;
            if (Math.Abs(det) < 1e-9) return false;
            double e1 = v1x - v0x, f1 = v1y - v0y, e2 = v2x - v0x, f2 = v2y - v0y;
            double m11 = (b2 * e1 - b1 * e2) / det;
            double m12 = (b2 * f1 - b1 * f2) / det;
            double m21 = (a1 * e2 - a2 * e1) / det;
            double m22 = (a1 * f2 - a2 * f1) / det;
            // A bone scaled to zero collapses the quad; Direct2D rejects singular transforms.
            if (Math.Abs(m11 * m22 - m12 * m21) < 1e-10) return false;
            double m31 = v0x - (q0x * m11 + q0y * m21);
            double m32 = v0y - (q0x * m12 + q0y * m22);
            m = new Matrix3x2((float)m11, (float)m12, (float)m21, (float)m22, (float)m31, (float)m32);
            return true;
        }

        private static void Ensure(ref CanvasRenderTarget target, CanvasDevice device, int width, int height)
        {
            if (target != null && target.Device == device
                && target.SizeInPixels.Width == width && target.SizeInPixels.Height == height)
                return;
            target?.Dispose();
            target = new CanvasRenderTarget(device, width, height, 96f);
        }

        public void Dispose()
        {
            _accumulate?.Dispose(); _accumulate = null;
            _spare?.Dispose(); _spare = null;
            _screenLayer?.Dispose(); _screenLayer = null;
            foreach (ColorMatrixEffect tint in _tints) tint.Dispose();
            foreach (CropEffect crop in _crops) crop.Dispose();
            _tints.Clear();
            _crops.Clear();
            _tintCursor = 0;
        }
    }
}

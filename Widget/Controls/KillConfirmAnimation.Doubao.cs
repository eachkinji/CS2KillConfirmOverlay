using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KillConfirmGameBar.Services;
using Microsoft.Graphics.Canvas;
using Windows.Foundation;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Xaml;

namespace KillConfirmGameBar.Controls
{
    public sealed partial class KillConfirmAnimation
    {
        private const double DoubaoFrameWidth = 720;
        private const double DoubaoFrameHeight = 440;
        private const double DoubaoImpactMs = 220;
        private const double DoubaoSettleEndMs = 480;
        private const double DoubaoFadeStartMs = 1720;
        private const double DoubaoDurationMs = 2250;
        private static readonly Dictionary<string, CanvasBitmap> DoubaoKillCache =
            new Dictionary<string, CanvasBitmap>();

        private bool _isDoubaoActive;
        private CanvasBitmap _currentDoubaoBitmap;
        private int _doubaoKillCount = 1;

        public async void PlayDoubaoKill(int killCount)
        {
            int normalizedKillCount = Math.Max(1, Math.Min(5, killCount));
            int generation = _resourceGeneration;
            int token = ++_playToken;

            try
            {
                CanvasBitmap bitmap;
                await PreloadGate.WaitAsync();
                try
                {
                    if (generation != _resourceGeneration || token != _playToken)
                    {
                        return;
                    }

                    bitmap = await LoadDoubaoKillBitmapAsync(normalizedKillCount);
                }
                finally
                {
                    if (generation != _resourceGeneration)
                    {
                        ReleaseAllAnimationResourceCaches();
                    }
                    PreloadGate.Release();
                }

                if (bitmap == null || generation != _resourceGeneration || token != _playToken)
                {
                    return;
                }

                PrepareDoubaoPlayback(bitmap, normalizedKillCount);
            }
            catch
            {
                if (token == _playToken)
                {
                    ResetDoubaoState();
                    Visibility = Visibility.Collapsed;
                }
            }
        }

        private async Task PreloadDoubaoAnimationsAsync(IProgress<int> progress)
        {
            progress?.Report(0);
            for (int killCount = 1; killCount <= 5; killCount++)
            {
                try
                {
                    await LoadDoubaoKillBitmapAsync(killCount);
                }
                catch
                {
                }

                progress?.Report(killCount * 20);
            }
        }

        private static async Task<CanvasBitmap> LoadDoubaoKillBitmapAsync(int killCount)
        {
            int normalized = Math.Max(1, Math.Min(5, killCount));
            string cacheKey = $"{normalized}:{_iconPack}";
            lock (DoubaoKillCache)
            {
                if (DoubaoKillCache.TryGetValue(cacheKey, out CanvasBitmap cached))
                {
                    return cached;
                }
            }

            CanvasBitmap loaded = null;
            if (PackCatalogService.IsImportedIconPackKey(_iconPack))
            {
                StorageFolder packFolder = await PackCatalogService.GetImportedIconFolderAsync(_iconPack);
                if (packFolder != null)
                {
                    loaded = await TryLoadDoubaoBitmapFromFolderAsync(packFolder, $"{normalized}kill.png");
                }
            }

            if (loaded == null)
            {
                loaded = await LoadBitmapFromApplicationUriAsync(
                    $"ms-appx:///Assets/GameStyles/doubao/killconfirm/textures/{normalized}kill.png");
            }

            lock (DoubaoKillCache)
            {
                if (DoubaoKillCache.TryGetValue(cacheKey, out CanvasBitmap cached))
                {
                    loaded?.Dispose();
                    return cached;
                }

                DoubaoKillCache[cacheKey] = loaded;
                return loaded;
            }
        }

        private static async Task<CanvasBitmap> TryLoadDoubaoBitmapFromFolderAsync(StorageFolder folder, string fileName)
        {
            if (folder == null || string.IsNullOrWhiteSpace(fileName)) return null;
            try
            {
                StorageFile file = await folder.GetFileAsync(fileName);
                if (file != null)
                {
                    return await LoadBitmapFromStorageFileAsync(file);
                }
            }
            catch { }
            return null;
        }

        private static void ClearDoubaoIconCache()
        {
            lock (DoubaoKillCache)
            {
                DoubaoKillCache.Clear();
            }
        }

        private void PrepareDoubaoPlayback(CanvasBitmap bitmap, int killCount)
        {
            _timer.Stop();
            _playbackClock.Stop();
            _isBattlefieldTextOverlayActive = false;
            ResetBattlefield5ScrollingState();
            ResetBattlefield4HudState();
            ResetBattlefield2042HudState();
            ResetPubgHudState();
            ResetDeltaForceHudState();
            _contentSizedViewport = false;
            _isBattlefield1CompactLayoutActive = false;
            _currentCodeAsset = null;
            _currentValorantAsset = null;
            _currentBattlefieldAsset = null;
            _currentCsolAsset = null;
            _currentDoubaoBitmap = bitmap;
            _doubaoKillCount = killCount;
            _isDoubaoActive = true;
            _currentMetadata = new SpriteMetadata
            {
                FrameWidth = (int)DoubaoFrameWidth,
                FrameHeight = (int)DoubaoFrameHeight,
                Frames = (int)Math.Ceiling(DoubaoDurationMs / 1000.0 * FrameSequenceFps),
                Fps = FrameSequenceFps
            };

            ApplyViewportSize(DoubaoFrameWidth, DoubaoFrameHeight);
            HideLoadingProgress();
            Visibility = Visibility.Visible;
            _timer.Interval = TimeSpan.FromMilliseconds(1000.0 / FrameSequenceFps);
            _playbackClock.Restart();
            SpriteCanvas.Invalidate();
            _timer.Start();
        }

        private void ResetDoubaoState()
        {
            _isDoubaoActive = false;
            _currentDoubaoBitmap = null;
            _doubaoKillCount = 1;
        }

        private void UpdateDoubaoFrame()
        {
            if (_playbackClock.Elapsed.TotalMilliseconds >= DoubaoDurationMs)
            {
                _timer.Stop();
                _playbackClock.Stop();
                ResetDoubaoState();
                Visibility = Visibility.Collapsed;
                return;
            }

            SpriteCanvas.Invalidate();
        }

        private void DrawDoubaoFrame(CanvasDrawingSession drawingSession)
        {
            if (!_isDoubaoActive || _currentDoubaoBitmap == null)
            {
                return;
            }

            double elapsed = _playbackClock.Elapsed.TotalMilliseconds;
            double centerX = DoubaoFrameWidth / 2.0;
            double centerY = DoubaoFrameHeight / 2.0;

            // --- 1. Snappy Motion Physics (弹性缩放与悬浮呼吸) ---
            double entry = EaseOutBack(Clamp01(elapsed / DoubaoImpactMs));
            double settle = EaseOutQuad(Clamp01((elapsed - DoubaoImpactMs) / (DoubaoSettleEndMs - DoubaoImpactMs)));
            double scale = elapsed <= DoubaoImpactMs
                ? Lerp(0.25, 1.15, entry)
                : Lerp(1.15, 1.0, settle);

            // Floating breathe oscillation
            double floatY = 0;
            if (elapsed > DoubaoSettleEndMs && elapsed <= DoubaoFadeStartMs)
            {
                double hoverTime = (elapsed - DoubaoSettleEndMs) / 1000.0;
                floatY = Math.Sin(hoverTime * 3.5) * 3.0;
                scale += Math.Sin(hoverTime * 2.8) * 0.012;
            }

            // Alpha opacity & exit fade
            double opacity = Clamp01(elapsed / 70.0);
            if (elapsed > DoubaoFadeStartMs)
            {
                double exitT = Clamp01((elapsed - DoubaoFadeStartMs) / (DoubaoDurationMs - DoubaoFadeStartMs));
                double exitEase = EaseInCubic(exitT);
                opacity *= 1.0 - exitEase;
                scale *= Lerp(1.0, 1.06, exitEase);
                floatY -= exitEase * 18.0;
            }

            // Micro camera shake at impact
            double shakePower = Math.Max(0.0, 1.0 - elapsed / 280.0);
            double shakeX = Math.Sin(elapsed * 0.16) * 5.0 * shakePower;
            double shakeY = Math.Cos(elapsed * 0.21) * 3.5 * shakePower;

            double finalCenterX = centerX + shakeX;
            double finalCenterY = centerY + floatY + shakeY;

            // --- 2. Color Themes based on Kill Count ---
            DoubaoTheme theme = GetDoubaoTheme(_doubaoKillCount);

            // --- 3. Layer 0: Ambient Backlight & Energy Shockwaves (冲击波与背光) ---
            CanvasBlend previousBlend = drawingSession.Blend;
            drawingSession.Blend = CanvasBlend.Add;

            // Ambient background glow
            double ambientPulse = 0.5 + 0.5 * Math.Sin(elapsed * 0.006);
            double ambientAlpha = opacity * (0.28 + 0.12 * ambientPulse);
            DrawDoubaoAmbientGlow(drawingSession, finalCenterX, finalCenterY, theme, ambientAlpha);

            // Expanding Shockwave Rings
            DrawDoubaoShockwaveRings(drawingSession, finalCenterX, finalCenterY, elapsed, theme, opacity);

            // Stardust & Sparkle Particle Cascade
            DrawDoubaoSparkleParticles(drawingSession, finalCenterX, finalCenterY, elapsed, theme, opacity, _doubaoKillCount);

            drawingSession.Blend = previousBlend;

            // --- 4. Layer 1: Badge Image Rendering (主体图标) ---
            Rect imageRect = DrawDoubaoBitmap(drawingSession, finalCenterX, finalCenterY, scale, opacity);

            // --- 5. Layer 2: Tech-Chic Holographic Corner Brackets & Energy Line (全息切角与基准光条) ---
            if (opacity > 0.05 && imageRect.Width > 10)
            {
                drawingSession.Blend = CanvasBlend.Add;

                // Holographic Framing Brackets
                DrawDoubaoHoloBrackets(drawingSession, imageRect, elapsed, theme, opacity);

                // Sleek Energy Accent Baseline
                DrawDoubaoEnergyBaseline(drawingSession, finalCenterX, imageRect.Bottom + 8.0, elapsed, theme, opacity);

                // Peak impact bloom on badge
                double peak = Math.Max(0.0, 1.0 - Math.Abs(elapsed - DoubaoImpactMs) / 160.0);
                if (peak > 0.02)
                {
                    DrawDoubaoBitmap(
                        drawingSession,
                        finalCenterX,
                        finalCenterY,
                        scale * (1.0 + peak * 0.02),
                        opacity * peak * 0.38);
                }

                drawingSession.Blend = previousBlend;
            }
        }

        private static void DrawDoubaoAmbientGlow(
            CanvasDrawingSession ds,
            double cx,
            double cy,
            DoubaoTheme theme,
            double alpha)
        {
            if (alpha <= 0.01) return;

            ds.FillCircle((float)cx, (float)cy, 140f, Color.FromArgb(DoubaoByte(22.0 * alpha), theme.Primary.R, theme.Primary.G, theme.Primary.B));
            ds.FillCircle((float)cx, (float)cy, 90f, Color.FromArgb(DoubaoByte(45.0 * alpha), theme.Secondary.R, theme.Secondary.G, theme.Secondary.B));
            ds.FillCircle((float)cx, (float)cy, 45f, Color.FromArgb(DoubaoByte(75.0 * alpha), theme.Highlight.R, theme.Highlight.G, theme.Highlight.B));
        }

        private static void DrawDoubaoShockwaveRings(
            CanvasDrawingSession ds,
            double cx,
            double cy,
            double elapsed,
            DoubaoTheme theme,
            double parentOpacity)
        {
            const double ShockwaveDuration = 520.0;
            if (elapsed > ShockwaveDuration || parentOpacity <= 0) return;

            // Primary shockwave ring
            double t1 = Clamp01(elapsed / ShockwaveDuration);
            double ease1 = EaseOutQuad(t1);
            float radius1 = (float)(24.0 + 175.0 * ease1);
            float stroke1 = (float)(3.2 * (1.0 - ease1));
            byte alpha1 = DoubaoByte((1.0 - ease1) * 180.0 * parentOpacity);

            if (alpha1 > 0 && stroke1 > 0.3f)
            {
                ds.DrawCircle((float)cx, (float)cy, radius1, Color.FromArgb(alpha1, theme.Primary.R, theme.Primary.G, theme.Primary.B), stroke1);
            }

            // Secondary delayed ring (starts at 70ms)
            const double Delay2 = 70.0;
            if (elapsed > Delay2)
            {
                double t2 = Clamp01((elapsed - Delay2) / (ShockwaveDuration - Delay2));
                double ease2 = EaseOutQuad(t2);
                float radius2 = (float)(16.0 + 225.0 * ease2);
                float stroke2 = (float)(2.2 * (1.0 - ease2));
                byte alpha2 = DoubaoByte((1.0 - ease2) * 130.0 * parentOpacity);

                if (alpha2 > 0 && stroke2 > 0.3f)
                {
                    ds.DrawCircle((float)cx, (float)cy, radius2, Color.FromArgb(alpha2, theme.Secondary.R, theme.Secondary.G, theme.Secondary.B), stroke2);
                }
            }
        }

        private static void DrawDoubaoSparkleParticles(
            CanvasDrawingSession ds,
            double cx,
            double cy,
            double elapsed,
            DoubaoTheme theme,
            double parentOpacity,
            int killCount)
        {
            if (parentOpacity <= 0 || elapsed < 30.0) return;

            int particleCount = 18 + killCount * 4; // 22 ~ 38 particles
            double timeSec = elapsed / 1000.0;

            for (int i = 0; i < particleCount; i++)
            {
                // Deterministic pseudo-random seed per particle
                double seedA = Math.Sin(i * 12.9898 + 78.233) * 43758.5453;
                seedA = seedA - Math.Floor(seedA);
                double seedB = Math.Sin(i * 39.346 + 11.135) * 23421.631;
                seedB = seedB - Math.Floor(seedB);

                double angle = (Math.PI * 2.0 * i / (double)particleCount) + (seedA * 0.4 - 0.2);
                double maxSpeed = 160.0 + seedB * 190.0 + killCount * 25.0;
                double decay = 2.8 + seedA * 1.2;

                // Physics: fast initial burst then deceleration + subtle upward float
                double progress = 1.0 - Math.Exp(-timeSec * decay);
                double distance = maxSpeed * progress;
                double px = cx + Math.Cos(angle) * distance;
                double py = cy + Math.Sin(angle) * distance - (timeSec * 18.0 * (0.6 + 0.4 * seedB));

                // Twinkle & fade
                double life = Math.Max(0.0, 1.0 - timeSec / (1.4 + seedA * 0.6));
                double twinkle = 0.75 + 0.25 * Math.Sin(timeSec * 16.0 + i * 2.0);
                byte pAlpha = DoubaoByte(life * twinkle * 220.0 * parentOpacity);

                if (pAlpha <= 0) continue;

                Color pColor = (i % 3 == 0) ? theme.Highlight : ((i % 2 == 0) ? theme.Primary : theme.Secondary);
                float pSize = (float)((1.8 + seedB * 2.6) * Math.Min(1.0, life * 1.5));

                if (i % 4 == 0)
                {
                    // 4-point star sparkle
                    DrawFourPointStar(ds, px, py, pSize * 2.2f, Color.FromArgb(pAlpha, pColor.R, pColor.G, pColor.B));
                }
                else
                {
                    // Circular glowing spark
                    ds.FillCircle((float)px, (float)py, pSize, Color.FromArgb(pAlpha, pColor.R, pColor.G, pColor.B));
                }
            }
        }

        private static void DrawFourPointStar(CanvasDrawingSession ds, double x, double y, float size, Color color)
        {
            ds.DrawLine((float)(x - size), (float)y, (float)(x + size), (float)y, color, 1.2f);
            ds.DrawLine((float)x, (float)(y - size), (float)x, (float)(y + size), color, 1.2f);
            ds.FillCircle((float)x, (float)y, size * 0.35f, color);
        }

        private static void DrawDoubaoHoloBrackets(
            CanvasDrawingSession ds,
            Rect imageRect,
            double elapsed,
            DoubaoTheme theme,
            double parentOpacity)
        {
            double entry = Clamp01(elapsed / DoubaoSettleEndMs);
            double ease = EaseOutCubic(entry);

            // Pad outside image
            double pad = Lerp(26.0, 10.0, ease);
            double left = imageRect.Left - pad;
            double right = imageRect.Right + pad;
            double top = imageRect.Top - pad;
            double bottom = imageRect.Bottom + pad;
            double bracketLen = 16.0;

            byte alpha = DoubaoByte(ease * 170.0 * parentOpacity);
            if (alpha <= 0) return;

            Color bracketColor = Color.FromArgb(alpha, theme.Primary.R, theme.Primary.G, theme.Primary.B);
            float thickness = 1.8f;

            // Top-Left corner
            ds.DrawLine((float)left, (float)(top + bracketLen), (float)left, (float)top, bracketColor, thickness);
            ds.DrawLine((float)left, (float)top, (float)(left + bracketLen), (float)top, bracketColor, thickness);

            // Top-Right corner
            ds.DrawLine((float)(right - bracketLen), (float)top, (float)right, (float)top, bracketColor, thickness);
            ds.DrawLine((float)right, (float)top, (float)right, (float)(top + bracketLen), bracketColor, thickness);

            // Bottom-Left corner
            ds.DrawLine((float)left, (float)(bottom - bracketLen), (float)left, (float)bottom, bracketColor, thickness);
            ds.DrawLine((float)left, (float)bottom, (float)(left + bracketLen), (float)bottom, bracketColor, thickness);

            // Bottom-Right corner
            ds.DrawLine((float)(right - bracketLen), (float)bottom, (float)right, (float)bottom, bracketColor, thickness);
            ds.DrawLine((float)right, (float)bottom, (float)right, (float)(bottom - bracketLen), bracketColor, thickness);
        }

        private static void DrawDoubaoEnergyBaseline(
            CanvasDrawingSession ds,
            double cx,
            double y,
            double elapsed,
            DoubaoTheme theme,
            double parentOpacity)
        {
            double progress = Clamp01(elapsed / 300.0);
            double width = EaseOutQuad(progress) * 190.0;
            if (width <= 2.0 || parentOpacity <= 0) return;

            byte centerAlpha = DoubaoByte(Clamp01(1.0 - (elapsed - 800.0) / 1400.0) * 160.0 * parentOpacity);
            if (centerAlpha <= 0) return;

            // Gradient line effect by drawing overlapping segments
            Color coreColor = Color.FromArgb(centerAlpha, theme.Highlight.R, theme.Highlight.G, theme.Highlight.B);
            Color edgeColor = Color.FromArgb((byte)(centerAlpha * 0.4), theme.Primary.R, theme.Primary.G, theme.Primary.B);

            ds.DrawLine((float)(cx - width / 2.0), (float)y, (float)(cx + width / 2.0), (float)y, edgeColor, 2.0f);
            ds.DrawLine((float)(cx - width * 0.25), (float)y, (float)(cx + width * 0.25), (float)y, coreColor, 2.4f);
            ds.FillCircle((float)cx, (float)y, 2.5f, coreColor);
        }

        private Rect DrawDoubaoBitmap(
            CanvasDrawingSession drawingSession,
            double centerX,
            double centerY,
            double scale,
            double opacity)
        {
            double imageWidth = _currentDoubaoBitmap.SizeInPixels.Width;
            double imageHeight = _currentDoubaoBitmap.SizeInPixels.Height;
            if (imageWidth <= 0 || imageHeight <= 0 || opacity <= 0)
            {
                return Rect.Empty;
            }

            double fitScale = Math.Min(640.0 / imageWidth, 380.0 / imageHeight) * scale;
            double width = imageWidth * fitScale;
            double height = imageHeight * fitScale;
            var target = new Rect(centerX - width / 2.0, centerY - height / 2.0, width, height);
            var source = new Rect(0, 0, imageWidth, imageHeight);
            drawingSession.DrawImage(
                _currentDoubaoBitmap,
                target,
                source,
                (float)Clamp01(opacity),
                CanvasImageInterpolation.Linear);

            return target;
        }

        private struct DoubaoTheme
        {
            public Color Primary;
            public Color Secondary;
            public Color Highlight;
        }

        private static DoubaoTheme GetDoubaoTheme(int killCount)
        {
            switch (killCount)
            {
                case 1:
                    // Electric Cyan & Sky Blue
                    return new DoubaoTheme
                    {
                        Primary = Color.FromArgb(255, 0, 229, 255),
                        Secondary = Color.FromArgb(255, 41, 121, 255),
                        Highlight = Color.FromArgb(255, 224, 247, 250)
                    };
                case 2:
                    // Cyber Cyan & Purple Dual-Tone
                    return new DoubaoTheme
                    {
                        Primary = Color.FromArgb(255, 0, 242, 254),
                        Secondary = Color.FromArgb(255, 127, 0, 255),
                        Highlight = Color.FromArgb(255, 243, 229, 245)
                    };
                case 3:
                    // Solar Gold & Amber Flame
                    return new DoubaoTheme
                    {
                        Primary = Color.FromArgb(255, 255, 179, 0),
                        Secondary = Color.FromArgb(255, 255, 109, 0),
                        Highlight = Color.FromArgb(255, 255, 249, 196)
                    };
                case 4:
                    // Neon Magenta & Electric Crimson
                    return new DoubaoTheme
                    {
                        Primary = Color.FromArgb(255, 255, 23, 68),
                        Secondary = Color.FromArgb(255, 213, 0, 249),
                        Highlight = Color.FromArgb(255, 255, 255, 255)
                    };
                case 5:
                default:
                    // Cosmic Supernova Prism
                    return new DoubaoTheme
                    {
                        Primary = Color.FromArgb(255, 224, 64, 251),
                        Secondary = Color.FromArgb(255, 0, 229, 255),
                        Highlight = Color.FromArgb(255, 255, 235, 59)
                    };
            }
        }

        private static double EaseOutBack(double t)
        {
            const double c1 = 1.70158;
            const double c3 = c1 + 1.0;
            return 1.0 + c3 * Math.Pow(t - 1.0, 3.0) + c1 * Math.Pow(t - 1.0, 2.0);
        }

        private static double EaseOutQuad(double t)
        {
            return 1.0 - (1.0 - t) * (1.0 - t);
        }

        private static double EaseInCubic(double t)
        {
            return t * t * t;
        }

        private static byte DoubaoByte(double value)
        {
            return (byte)Math.Max(0, Math.Min(255, Math.Round(value)));
        }
    }
}

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
        private const double DoubaoImpactMs = 300;
        private const double DoubaoSettleEndMs = 620;
        private const double DoubaoFadeStartMs = 1850;
        private const double DoubaoDurationMs = 2480;
        private static readonly Dictionary<string, CanvasBitmap> DoubaoKillCache =
            new Dictionary<string, CanvasBitmap>();

        private bool _isDoubaoActive;
        private CanvasBitmap _currentDoubaoBitmap;

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

                PrepareDoubaoPlayback(bitmap);
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
            DoubaoSettingsValues settings = DoubaoSettingsStore.Load();
            string key = settings.KillImageKeys.TryGetValue(normalized, out string k) ? k : DoubaoSettingsStore.DefaultImageKey(normalized);
            string cacheKey = $"{normalized}:{key}";
            lock (DoubaoKillCache)
            {
                if (DoubaoKillCache.TryGetValue(cacheKey, out CanvasBitmap cached))
                {
                    return cached;
                }
            }

            CanvasBitmap loaded = null;
            if (string.IsNullOrWhiteSpace(key) || key.StartsWith("builtin:", StringComparison.OrdinalIgnoreCase))
            {
                loaded = await LoadBitmapFromApplicationUriAsync(
                    $"ms-appx:///Assets/GameStyles/doubao/killconfirm/textures/{normalized}kill.png");
            }
            else
            {
                StorageFile imported = await DoubaoSettingsStore.GetImportedImageFileAsync(key);
                if (imported != null)
                {
                    loaded = await LoadBitmapFromStorageFileAsync(imported);
                }
                if (loaded == null)
                {
                    loaded = await LoadBitmapFromApplicationUriAsync(
                        $"ms-appx:///Assets/GameStyles/doubao/killconfirm/textures/{normalized}kill.png");
                }
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

        private static void ClearDoubaoIconCache()
        {
            lock (DoubaoKillCache)
            {
                DoubaoKillCache.Clear();
            }
        }

        private void PrepareDoubaoPlayback(CanvasBitmap bitmap)
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
            double entry = EaseOutCubic(Clamp01(elapsed / DoubaoImpactMs));
            double settle = EaseOutCubic(Clamp01((elapsed - DoubaoImpactMs) /
                (DoubaoSettleEndMs - DoubaoImpactMs)));
            double scale = elapsed <= DoubaoImpactMs
                ? Lerp(0.06, 1.18, entry)
                : Lerp(1.18, 1.0, settle);
            double opacity = Clamp01(elapsed / 85.0);
            if (elapsed > DoubaoFadeStartMs)
            {
                double exit = EaseOutCubic(Clamp01(
                    (elapsed - DoubaoFadeStartMs) / (DoubaoDurationMs - DoubaoFadeStartMs)));
                opacity *= 1.0 - exit;
                scale *= Lerp(1.0, 1.055, exit);
            }

            double impact = elapsed <= DoubaoImpactMs
                ? entry
                : 1.0 - Clamp01((elapsed - DoubaoImpactMs) / 720.0);
            double peak = Math.Max(0.0, 1.0 - Math.Abs(elapsed - DoubaoImpactMs) / 260.0);
            double flash = Clamp01(0.16 + impact * 0.84) * opacity;

            CanvasBlend previousBlend = drawingSession.Blend;
            drawingSession.Blend = CanvasBlend.Add;
            DrawDoubaoGoldenBurst(drawingSession, centerX, centerY, flash, peak);
            drawingSession.Blend = previousBlend;

            double shake = Math.Max(0.0, 1.0 - elapsed / 430.0);
            double shakeX = Math.Sin(elapsed * 0.105) * 8.0 * shake;
            double shakeY = Math.Cos(elapsed * 0.137) * 5.0 * shake;
            DrawDoubaoBitmap(drawingSession, centerX + shakeX, centerY + shakeY, scale, opacity);

            if (peak > 0.01)
            {
                drawingSession.Blend = CanvasBlend.Add;
                DrawDoubaoBitmap(
                    drawingSession,
                    centerX + shakeX,
                    centerY + shakeY,
                    scale * (1.0 + peak * 0.014),
                    opacity * peak * 0.42);
                DrawDoubaoCoreFlash(drawingSession, centerX, centerY, peak * opacity);
                drawingSession.Blend = previousBlend;
            }
        }

        private static void DrawDoubaoGoldenBurst(
            CanvasDrawingSession drawingSession,
            double centerX,
            double centerY,
            double flash,
            double peak)
        {
            if (flash <= 0)
            {
                return;
            }

            for (int index = 0; index < 16; index++)
            {
                double angle = (Math.PI * 2.0 * index / 16.0) + 0.09;
                double alternating = index % 2 == 0 ? 1.0 : 0.64;
                double length = (125.0 + 235.0 * peak) * alternating;
                double inner = 8.0 + 20.0 * peak;
                byte alpha = DoubaoByte((30.0 + 92.0 * peak) * flash * alternating);
                drawingSession.DrawLine(
                    (float)(centerX + Math.Cos(angle) * inner),
                    (float)(centerY + Math.Sin(angle) * inner),
                    (float)(centerX + Math.Cos(angle) * length),
                    (float)(centerY + Math.Sin(angle) * length),
                    Color.FromArgb(alpha, 255, 205, 78),
                    (float)(1.4 + 3.0 * peak * alternating));
            }

            byte axisAlpha = DoubaoByte((72.0 + 120.0 * peak) * flash);
            Color axisColor = Color.FromArgb(axisAlpha, 255, 226, 139);
            drawingSession.DrawLine((float)centerX, 8, (float)centerX, (float)(DoubaoFrameHeight - 8), axisColor, (float)(3.0 + 7.0 * peak));
            drawingSession.DrawLine(26, (float)centerY, (float)(DoubaoFrameWidth - 26), (float)centerY, axisColor, (float)(2.0 + 5.0 * peak));

            drawingSession.FillCircle(
                (float)centerX,
                (float)centerY,
                (float)(30.0 + 62.0 * peak),
                Color.FromArgb(DoubaoByte((54.0 + 86.0 * peak) * flash), 255, 179, 38));
        }

        private static void DrawDoubaoCoreFlash(
            CanvasDrawingSession drawingSession,
            double centerX,
            double centerY,
            double intensity)
        {
            drawingSession.FillCircle(
                (float)centerX,
                (float)centerY,
                (float)(12.0 + 42.0 * intensity),
                Color.FromArgb(DoubaoByte(220.0 * intensity), 255, 244, 202));
            drawingSession.FillCircle(
                (float)centerX,
                (float)centerY,
                (float)(4.0 + 18.0 * intensity),
                Color.FromArgb(DoubaoByte(255.0 * intensity), 255, 255, 255));
        }

        private void DrawDoubaoBitmap(
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
                return;
            }

            double fitScale = Math.Min(680.0 / imageWidth, 400.0 / imageHeight) * scale;
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
        }

        private static byte DoubaoByte(double value)
        {
            return (byte)Math.Max(0, Math.Min(255, Math.Round(value)));
        }
    }
}

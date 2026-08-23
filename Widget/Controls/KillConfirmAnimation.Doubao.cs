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
        private const double DoubaoFadeStartMs = 1720;
        private const double DoubaoDurationMs = 2250;
        // The flash overlay peaks at impact and decays over this window, carrying the
        // "闪光" effect in place of the retired procedural VFX (shockwaves/sparkles/etc).
        private const double DoubaoFlashMs = 520;
        private static readonly Dictionary<string, CanvasBitmap> DoubaoKillCache =
            new Dictionary<string, CanvasBitmap>();

        private bool _isDoubaoActive;
        private CanvasBitmap _currentDoubaoBitmap;
        private CanvasBitmap _currentDoubaoFlashBitmap;
        private int _doubaoKillCount = 1;

        public async void PlayDoubaoKill(int killCount)
        {
            int normalizedKillCount = Math.Max(1, Math.Min(5, killCount));
            int generation = _resourceGeneration;
            int token = ++_playToken;

            try
            {
                CanvasBitmap bitmap;
                CanvasBitmap flash;
                await PreloadGate.WaitAsync();
                try
                {
                    if (generation != _resourceGeneration || token != _playToken)
                    {
                        return;
                    }

                    bitmap = await LoadDoubaoKillBitmapAsync(normalizedKillCount);
                    flash = await LoadDoubaoFlashBitmapAsync();
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

                PrepareDoubaoPlayback(bitmap, flash, normalizedKillCount);
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

                progress?.Report(killCount * 18);
            }

            try
            {
                await LoadDoubaoFlashBitmapAsync();
            }
            catch
            {
            }
            progress?.Report(100);
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

        // The flash overlay is a single shared asset (not per-icon-pack), so it is cached
        // independently of DoubaoKillCache and survives icon-pack switches.
        private static CanvasBitmap _doubaoFlashBitmapCache;

        private static async Task<CanvasBitmap> LoadDoubaoFlashBitmapAsync()
        {
            if (_doubaoFlashBitmapCache != null)
            {
                return _doubaoFlashBitmapCache;
            }

            try
            {
                _doubaoFlashBitmapCache = await LoadBitmapFromApplicationUriAsync(
                    "ms-appx:///Assets/GameStyles/doubao/killconfirm/textures/flash.png");
            }
            catch
            {
            }
            return _doubaoFlashBitmapCache;
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

        private void PrepareDoubaoPlayback(CanvasBitmap bitmap, CanvasBitmap flash, int killCount)
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
            _currentDoubaoFlashBitmap = flash;
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
            _currentDoubaoFlashBitmap = null;
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

            // Simple scale-in (no elastic overshoot — the flash layer carries the impact)
            // and exit fade.
            double entry = EaseOutQuad(Clamp01(elapsed / DoubaoImpactMs));
            double scale = Lerp(0.7, 1.0, entry);
            double opacity = Clamp01(elapsed / 70.0);
            if (elapsed > DoubaoFadeStartMs)
            {
                double exitT = Clamp01((elapsed - DoubaoFadeStartMs) / (DoubaoDurationMs - DoubaoFadeStartMs));
                opacity *= 1.0 - EaseInCubic(exitT);
            }

            // Layer 1: kill badge image.
            DrawDoubaoBitmap(drawingSession, centerX, centerY, scale, opacity);

            // Layer 2: flash overlay — bright at impact, fades over DoubaoFlashMs.
            // Replaces the retired procedural shockwaves / sparkles / holo brackets.
            CanvasBitmap flash = _currentDoubaoFlashBitmap;
            if (flash != null && opacity > 0.01)
            {
                double flashRamp = Clamp01(elapsed / 60.0);
                double flashDecay = Clamp01(1.0 - elapsed / DoubaoFlashMs);
                double flashAlpha = flashRamp * flashDecay * opacity;
                if (flashAlpha > 0.01)
                {
                    DrawDoubaoFlashOverlay(drawingSession, centerX, centerY, flash, scale, flashAlpha);
                }
            }
        }

        private static void DrawDoubaoFlashOverlay(
            CanvasDrawingSession drawingSession,
            double centerX,
            double centerY,
            CanvasBitmap flash,
            double scale,
            double alpha)
        {
            double imageWidth = flash.SizeInPixels.Width;
            double imageHeight = flash.SizeInPixels.Height;
            if (imageWidth <= 0 || imageHeight <= 0)
            {
                return;
            }

            // Cover the frame area (a touch larger than the badge), centered, rising with
            // the badge scale-in. Additive blend so the flash reads as a bright glint.
            double fit = Math.Min(DoubaoFrameWidth / imageWidth, DoubaoFrameHeight / imageHeight) * scale;
            double width = imageWidth * fit;
            double height = imageHeight * fit;
            var target = new Rect(centerX - width / 2.0, centerY - height / 2.0, width, height);
            var source = new Rect(0, 0, imageWidth, imageHeight);

            CanvasBlend previousBlend = drawingSession.Blend;
            drawingSession.Blend = CanvasBlend.Add;
            drawingSession.DrawImage(
                flash,
                target,
                source,
                (float)Clamp01(alpha),
                CanvasImageInterpolation.Linear);
            drawingSession.Blend = previousBlend;
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

        private static double EaseOutQuad(double t)
        {
            return 1.0 - (1.0 - t) * (1.0 - t);
        }

        private static double EaseInCubic(double t)
        {
            return t * t * t;
        }
    }
}
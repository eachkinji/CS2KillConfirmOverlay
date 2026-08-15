using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KillConfirmGameBar.Services;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Effects;
using Windows.Foundation;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Xaml;

namespace KillConfirmGameBar.Controls
{
    public sealed partial class KillConfirmAnimation
    {
        private const double DagoujiaoFrameWidth = 720;
        private const double DagoujiaoFrameHeight = 500;
        private const double DagoujiaoFallbackDurationMs = 2150;
        private static readonly Dictionary<string, CanvasBitmap> DagoujiaoImageCache =
            new Dictionary<string, CanvasBitmap>(StringComparer.OrdinalIgnoreCase);

        private bool _isDagoujiaoActive;
        private CanvasBitmap _currentDagoujiaoBitmap;
        private string _currentDagoujiaoImageKey;
        private double _currentDagoujiaoOpacity;
        private double _currentDagoujiaoBaseScale;
        private double _currentDagoujiaoImpactMs;
        private double _currentDagoujiaoSettleMs;
        private double _currentDagoujiaoFadeStartMs;
        private double _currentDagoujiaoDurationMs;

        public async void PlayDagoujiaoKill(int killCount, bool isHeadshot)
        {
            int generation = _resourceGeneration;
            int token = ++_playToken;
            DagoujiaoSettingsValues settings = DagoujiaoSettingsStore.Load();
            string imageKey = DagoujiaoSettingsStore.ResolveImageKey(settings, killCount, isHeadshot);
            double progress = DagoujiaoSettingsStore.ResolveProgress(killCount, settings.EpicKillCount);
            double baseScale = Lerp(settings.InitialScale, settings.MaximumScale, progress);
            if (killCount >= settings.EpicKillCount && !(isHeadshot && settings.HeadshotPriority))
            {
                baseScale = settings.MaximumScale;
            }

            try
            {
                Task<double> durationTask = DagoujiaoSettingsStore.GetPlaybackDurationMillisecondsAsync(
                    settings,
                    killCount,
                    isHeadshot);
                CanvasBitmap bitmap;
                await PreloadGate.WaitAsync();
                try
                {
                    if (generation != _resourceGeneration || token != _playToken) return;
                    bitmap = await LoadDagoujiaoImageAsync(imageKey);
                }
                finally
                {
                    if (generation != _resourceGeneration) ReleaseAllAnimationResourceCaches();
                    PreloadGate.Release();
                }

                if (bitmap == null || generation != _resourceGeneration || token != _playToken) return;
                double durationMs = await durationTask;
                if (generation != _resourceGeneration || token != _playToken) return;
                PrepareDagoujiaoPlayback(bitmap, imageKey, settings.Opacity, baseScale, durationMs);
            }
            catch (Exception ex)
            {
                App.Log("Play Dagoujiao animation failed: " + ex.Message);
                if (token == _playToken)
                {
                    ResetDagoujiaoState();
                    Visibility = Visibility.Collapsed;
                }
            }
        }

        public static void InvalidateDagoujiaoImageCache()
        {
            // Imported images use unique file names and setting changes only switch
            // cache keys, so active bitmaps remain valid and cannot be disposed mid-frame.
            _startupPreloadTask = null;
        }

        private async Task PreloadDagoujiaoAnimationsAsync(IProgress<int> progress)
        {
            string[] defaults =
            {
                DagoujiaoSettingsStore.DefaultCommonImageKey,
                DagoujiaoSettingsStore.DefaultHeadshotImageKey,
                DagoujiaoSettingsStore.EpicImageKey
            };
            progress?.Report(0);
            for (int index = 0; index < defaults.Length; index++)
            {
                try { await LoadDagoujiaoImageAsync(defaults[index]); }
                catch { }
                progress?.Report((index + 1) * 100 / defaults.Length);
            }
        }

        private static async Task<CanvasBitmap> LoadDagoujiaoImageAsync(string imageKey)
        {
            string normalizedKey = string.IsNullOrWhiteSpace(imageKey)
                ? DagoujiaoSettingsStore.DefaultCommonImageKey
                : imageKey.Trim();
            lock (DagoujiaoImageCache)
            {
                if (DagoujiaoImageCache.TryGetValue(normalizedKey, out CanvasBitmap cached)) return cached;
            }

            CanvasBitmap loaded = null;
            string builtInFile = DagoujiaoSettingsStore.GetBuiltInFileName(normalizedKey);
            if (!string.IsNullOrWhiteSpace(builtInFile))
            {
                loaded = await LoadBitmapFromApplicationUriAsync(
                    "ms-appx:///Assets/GameStyles/dagoujiao/killconfirm/textures/" + builtInFile);
            }
            else
            {
                StorageFile imported = await DagoujiaoSettingsStore.GetImportedImageFileAsync(normalizedKey);
                if (imported != null) loaded = await LoadBitmapFromStorageFileAsync(imported);
            }
            if (loaded == null && !string.Equals(normalizedKey, DagoujiaoSettingsStore.DefaultCommonImageKey, StringComparison.OrdinalIgnoreCase))
            {
                return await LoadDagoujiaoImageAsync(DagoujiaoSettingsStore.DefaultCommonImageKey);
            }

            lock (DagoujiaoImageCache)
            {
                if (DagoujiaoImageCache.TryGetValue(normalizedKey, out CanvasBitmap cached))
                {
                    loaded?.Dispose();
                    return cached;
                }
                DagoujiaoImageCache[normalizedKey] = loaded;
                return loaded;
            }
        }

        private static void ClearDagoujiaoImageCache()
        {
            lock (DagoujiaoImageCache) DagoujiaoImageCache.Clear();
        }

        private void PrepareDagoujiaoPlayback(
            CanvasBitmap bitmap,
            string imageKey,
            double opacity,
            double baseScale,
            double durationMs)
        {
            _timer.Stop();
            _playbackClock.Stop();
            _isBattlefieldTextOverlayActive = false;
            ResetBattlefield5ScrollingState();
            ResetBattlefield4HudState();
            ResetBattlefield2042HudState();
            ResetPubgHudState();
            ResetDeltaForceHudState();
            ResetDoubaoState();
            _contentSizedViewport = false;
            _isBattlefield1CompactLayoutActive = false;
            _currentCodeAsset = null;
            _currentValorantAsset = null;
            _currentBattlefieldAsset = null;
            _currentCsolAsset = null;
            _currentDagoujiaoBitmap = bitmap;
            _currentDagoujiaoImageKey = imageKey;
            _currentDagoujiaoOpacity = Clamp01(opacity);
            _currentDagoujiaoBaseScale = Math.Max(0.1, Math.Min(4.0, baseScale));
            _currentDagoujiaoDurationMs = durationMs > 0
                ? Math.Max(100.0, durationMs)
                : DagoujiaoFallbackDurationMs;
            _currentDagoujiaoImpactMs = Math.Min(250.0, _currentDagoujiaoDurationMs * 0.24);
            _currentDagoujiaoSettleMs = Math.Min(520.0, _currentDagoujiaoDurationMs * 0.46);
            _currentDagoujiaoSettleMs = Math.Max(_currentDagoujiaoImpactMs + 1.0, _currentDagoujiaoSettleMs);
            _currentDagoujiaoFadeStartMs = _currentDagoujiaoDurationMs * 0.68;
            _isDagoujiaoActive = true;
            _currentMetadata = new SpriteMetadata
            {
                FrameWidth = (int)DagoujiaoFrameWidth,
                FrameHeight = (int)DagoujiaoFrameHeight,
                Frames = (int)Math.Ceiling(_currentDagoujiaoDurationMs / 1000.0 * FrameSequenceFps),
                Fps = FrameSequenceFps
            };
            ApplyViewportSize(DagoujiaoFrameWidth, DagoujiaoFrameHeight);
            HideLoadingProgress();
            Visibility = Visibility.Visible;
            _timer.Interval = TimeSpan.FromMilliseconds(1000.0 / FrameSequenceFps);
            _playbackClock.Restart();
            SpriteCanvas.Invalidate();
            _timer.Start();
        }

        private void ResetDagoujiaoState()
        {
            _isDagoujiaoActive = false;
            _currentDagoujiaoBitmap = null;
            _currentDagoujiaoImageKey = null;
        }

        private void UpdateDagoujiaoFrame()
        {
            if (_playbackClock.Elapsed.TotalMilliseconds >= _currentDagoujiaoDurationMs)
            {
                _timer.Stop();
                _playbackClock.Stop();
                ResetDagoujiaoState();
                Visibility = Visibility.Collapsed;
                return;
            }
            SpriteCanvas.Invalidate();
        }

        private void DrawDagoujiaoFrame(CanvasDrawingSession drawingSession)
        {
            if (!_isDagoujiaoActive || _currentDagoujiaoBitmap == null) return;
            double elapsed = _playbackClock.Elapsed.TotalMilliseconds;
            double entry = EaseOutCubic(Clamp01(elapsed / _currentDagoujiaoImpactMs));
            double settle = EaseOutCubic(Clamp01((elapsed - _currentDagoujiaoImpactMs) /
                (_currentDagoujiaoSettleMs - _currentDagoujiaoImpactMs)));
            double impactScale = elapsed <= _currentDagoujiaoImpactMs
                ? Lerp(0.08, 1.18, entry)
                : Lerp(1.18, 1.0, settle);
            double alpha = _currentDagoujiaoOpacity * Clamp01(elapsed / 70.0);
            if (elapsed > _currentDagoujiaoFadeStartMs)
            {
                double exit = EaseOutCubic(Clamp01(
                    (elapsed - _currentDagoujiaoFadeStartMs) /
                    (_currentDagoujiaoDurationMs - _currentDagoujiaoFadeStartMs)));
                alpha *= 1.0 - exit;
                impactScale *= Lerp(1.0, 1.05, exit);
            }

            double shake = Math.Max(0, 1.0 - elapsed / 390.0);
            double centerX = DagoujiaoFrameWidth / 2.0 + Math.Sin(elapsed * 0.12) * 9.0 * shake;
            double centerY = DagoujiaoFrameHeight / 2.0 + Math.Cos(elapsed * 0.16) * 6.0 * shake;
            DrawDagoujiaoBitmap(
                drawingSession,
                centerX,
                centerY,
                _currentDagoujiaoBaseScale * impactScale,
                alpha);
        }

        private void DrawDagoujiaoBitmap(
            CanvasDrawingSession drawingSession,
            double centerX,
            double centerY,
            double scale,
            double opacity)
        {
            double imageWidth = _currentDagoujiaoBitmap.SizeInPixels.Width;
            double imageHeight = _currentDagoujiaoBitmap.SizeInPixels.Height;
            if (imageWidth <= 0 || imageHeight <= 0 || opacity <= 0) return;
            double fit = Math.Min(360.0 / imageWidth, 360.0 / imageHeight) * scale;
            double width = imageWidth * fit;
            double height = imageHeight * fit;
            var target = new Rect(centerX - width / 2.0, centerY - height / 2.0, width, height);
            var source = new Rect(0, 0, imageWidth, imageHeight);
            bool chromaKey = string.Equals(
                    _currentDagoujiaoImageKey,
                    DagoujiaoSettingsStore.DefaultCommonImageKey,
                    StringComparison.OrdinalIgnoreCase)
                || string.Equals(
                    _currentDagoujiaoImageKey,
                    DagoujiaoSettingsStore.DefaultHeadshotImageKey,
                    StringComparison.OrdinalIgnoreCase);
            if (chromaKey)
            {
                using (var effect = new ChromaKeyEffect
                {
                    Source = _currentDagoujiaoBitmap,
                    Color = Color.FromArgb(255, 0, 255, 0),
                    Tolerance = 0.24f,
                    Feather = true,
                    InvertAlpha = false
                })
                {
                    drawingSession.DrawImage(effect, target, source, (float)Clamp01(opacity), CanvasImageInterpolation.Linear);
                }
                return;
            }
            drawingSession.DrawImage(
                _currentDagoujiaoBitmap,
                target,
                source,
                (float)Clamp01(opacity),
                CanvasImageInterpolation.Linear);
        }
    }
}

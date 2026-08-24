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
                    bitmap = await LoadDagoujiaoImageForKillAsync(settings, killCount, isHeadshot);
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
                // A missing/invalid built-in image must be diagnosable even when
                // developer logging is disabled; this path means the visual was
                // actually lost while its companion audio may still have played.
                App.LogCrash("Play Dagoujiao animation failed: " + ex);
                if (token == _playToken)
                {
                    ResetDagoujiaoState();
                    Visibility = Visibility.Collapsed;
                }
            }
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

    }
}

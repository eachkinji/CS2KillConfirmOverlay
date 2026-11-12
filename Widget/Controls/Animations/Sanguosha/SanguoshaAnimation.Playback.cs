using System;
using System.Linq;
using KillConfirmCompatibility.Contracts;
using KillConfirmGameBar.Services;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;

namespace KillConfirmGameBar.Controls
{
    public sealed partial class KillConfirmAnimation
    {
        public void PlaySanguoshaKill(KillEvent killEvent)
        {
            PlaySanguoshaKill(GameStyleService.SanguoshaTier(killEvent), killEvent?.PublishedUnixMs ?? 0);
        }

        // NewKillEffect.CreatePlay(nBroadCastAnimationType): one broadcast per kill tier.
        public async void PlaySanguoshaKill(int killCount, ulong publishedUnixMs = 0)
        {
            // Same event anchor as the audio service: compensate transport and decode latency
            // so the voice (played by the service at publication) stays on the original timing.
            long requestedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var loadingClock = System.Diagnostics.Stopwatch.StartNew();
            double transportMs = publishedUnixMs > 0 && publishedUnixMs < (ulong)requestedAt
                ? requestedAt - (double)publishedUnixMs : 0;
            int tier = GameStyleService.SanguoshaTier(killCount);
            int generation = _resourceGeneration;
            _sanguoshaRetainWarmOnce = _sanguoshaLz4Tier == tier && IsSanguoshaLz4ConfigurationCurrent();
            int token;
            try { token = BeginReplacementPlayback(); }
            finally { _sanguoshaRetainWarmOnce = false; }

            try
            {
                SanguoshaTierAssets assets;
                await WaitForCurrentPackPreloadAsync();
                if (generation != _resourceGeneration || token != _playToken) return;
                lock (SanguoshaTiers)
                {
                    if (!SanguoshaTiers.TryGetValue(tier, out assets))
                        throw new InvalidOperationException("Sanguosha animation was not preloaded: " + tier);
                }

                if (assets == null || generation != _resourceGeneration || token != _playToken) return;
                if (assets.Pages == null || assets.Pages.Length == 0)
                    throw new InvalidOperationException("Sanguosha atlas pages were not preloaded: " + tier);
                if (generation != _resourceGeneration || token != _playToken) { if (!IsSanguoshaTierOnScreen(assets)) ReleaseSanguoshaPages(assets); return; }
                PrepareSanguoshaPlayback(assets, Math.Min(transportMs + loadingClock.Elapsed.TotalMilliseconds, 1000));
            }
            catch (Exception error)
            {
                App.Log("Sanguosha playback failed: " + error);
                if (token == _playToken)
                {
                    _timer.Stop();
                    _playbackClock.Stop();
                    ResetSanguoshaState();
                    Visibility = Visibility.Collapsed;
                }
            }
        }

        private void PrepareSanguoshaPlayback(SanguoshaTierAssets assets, double startOffsetMs)
        {
            _timer.Stop();
            _playbackClock.Stop();
            _customSequencePlaying = false;
            ResetDoubaoState();
            ResetDagoujiaoState();
            ResetOverwatchState();
            ResetApexFeedState();
            ResetModernWarfare2019State();
            ResetCod2022State();
            ResetLeagueOfLegendsState();
            ResetTitanfall2State();
            ResetRdr2State();
            ResetWarThunderState();
            ResetBattlefield5ScrollingState();
            ResetBattlefield4HudState();
            ResetBattlefield2042HudState();
            ResetBattlefield6HudState();
            ResetPubgHudState();
            ResetDeltaForceHudState();
            ResetSanguoshaState(assets);
            _isBattlefieldTextOverlayActive = false;
            _currentCodeAsset = null;
            _currentValorantAsset = null;
            _currentBattlefieldAsset = null;
            _currentCsolAsset = null;
            _contentSizedViewport = false;
            _isBattlefield1CompactLayoutActive = false;
            _isCalabiyauActive = false;

            lock (SanguoshaTiersOnScreen) SanguoshaTiersOnScreen.Add(assets);
            _sanguoshaCurrent = assets;
            _sanguoshaShake = new SanguoshaShake(assets.Clip, 20261107);
            _sanguoshaStartOffsetMs = Math.Max(0, startOffsetMs);
            _isSanguoshaActive = true;
            _currentMetadata = new SpriteMetadata
            {
                FrameWidth = (int)SanguoshaFrameWidth,
                FrameHeight = (int)SanguoshaFrameHeight,
                Frames = assets.Clip.Frames.Length,
                Fps = (int)Math.Round(assets.Clip.Fps)
            };

            ApplyViewportSize(SanguoshaFrameWidth, SanguoshaFrameHeight);
            HideLoadingProgress();
            Visibility = Visibility.Visible;
            _timer.Interval = TimeSpan.FromMilliseconds(1000.0 / FrameSequenceFps);
            _playbackClock.Restart();
            SpriteCanvas.Invalidate();
            StartSanguoshaFrameClock();
        }

        private void ResetSanguoshaState() => ResetSanguoshaState(null, _sanguoshaRetainWarmOnce);

        private void ResetSanguoshaState(SanguoshaTierAssets keep) => ResetSanguoshaState(keep, false);
        private void ResetSanguoshaState(SanguoshaTierAssets keep, bool retainWarm)
        {
            StopSanguoshaFrameClock();
            if (keep == null && !retainWarm) DisposeSanguoshaLz4Playback();
            _isSanguoshaActive = false;
            _sanguoshaShake = null;
            _sanguoshaStartOffsetMs = 0;
            if (keep == null) _sanguoshaRenderer?.ReleaseFrameEffectSources();
            SanguoshaTierAssets finished = _sanguoshaCurrent;
            _sanguoshaCurrent = null;
            if (finished == null || finished == keep) return;
            lock (SanguoshaTiersOnScreen) SanguoshaTiersOnScreen.Remove(finished);
            bool cached;
            lock (SanguoshaTiers) cached = SanguoshaTiers.Values.Contains(finished);
            if (cached) TrimNativeSanguoshaPages(); else ReleaseSanguoshaPages(finished);
            if (retainWarm && _sanguoshaLz4Playback != null)
            {
                try { _sanguoshaLz4Playback.RequestFrame(0); }
                catch (Exception error) { _sanguoshaFallbackReason = "warm prefetch: " + error.Message; DisposeSanguoshaLz4Playback(); }
            }
        }

        private void UpdateSanguoshaFrame()
        {
            SanguoshaTierAssets assets = _sanguoshaCurrent;
#if SANGUOSHA_LZ4_VALIDATION
            if (_sanguoshaValidationLoop && assets != null && SanguoshaElapsedMs >= assets.Clip.Length.TotalMilliseconds)
                _sanguoshaStartOffsetMs -= assets.Clip.Length.TotalMilliseconds;
#endif
            // SgsSkeletonEffect.PLAY_COMPLETE at the end of the Spine animation also removes
            // the modal background (NewKillEffect.hideModalBg).
            if (assets == null || SanguoshaElapsedMs >= assets.Clip.Length.TotalMilliseconds)
            {
                _timer.Stop();
                _playbackClock.Stop();
                ResetSanguoshaState(null, IsSanguoshaLz4ConfigurationCurrent());
                Visibility = Visibility.Collapsed;
                return;
            }

            SpriteCanvas.Invalidate();
        }
        private void StartSanguoshaFrameClock()
        {
            _timer.Stop(); StopSanguoshaFrameClock();
            if (_sanguoshaLz4Playback != null && IsSanguoshaLz4ConfigurationCurrent())
            {
                if (_sanguoshaAnimatedSurface == null)
                {
                    _sanguoshaAnimatedSurface = new SanguoshaAnimatedSurface(Dispatcher);
                    Viewport.Children.Insert(1, _sanguoshaAnimatedSurface.Control);
                }
                var profile = _sanguoshaLz4Profile; int token = _playToken;
                SpriteCanvas.Visibility = Visibility.Collapsed; SpriteCanvas.Width = 0; SpriteCanvas.Height = 0;
                _sanguoshaAnimatedSurface.Start(_sanguoshaLz4Playback, profile.Width, profile.Height, profile.WidthDips, profile.HeightDips,
                    profile.Dpi, SanguoshaElapsedMs,
                    () => { if (token == _playToken) { _playbackClock.Stop(); ResetSanguoshaState(null, true); Visibility = Visibility.Collapsed; } },
                    error => { if (token == _playToken) { _sanguoshaFallbackReason = "animated decode: " + error.Message; DisposeSanguoshaLz4Playback(); RequestSanguoshaRasterChange(_sanguoshaCurrent); } });
                return;
            }
            SpriteCanvas.Width = Viewport.Width; SpriteCanvas.Height = Viewport.Height; SpriteCanvas.Visibility = Visibility.Visible;
            _sanguoshaLastCompositionTicks = -1; _sanguoshaNextPresentationMs = 0;
            _sanguoshaRenderingAttached = true; CompositionTarget.Rendering += OnSanguoshaCompositionRendering;
        }
        private void StopSanguoshaFrameClock()
        {
            _sanguoshaAnimatedSurface?.Pause();
            if (!_sanguoshaRenderingAttached) return;
            CompositionTarget.Rendering -= OnSanguoshaCompositionRendering; _sanguoshaRenderingAttached = false;
        }
        private void OnSanguoshaAnimationUnloaded(object sender, RoutedEventArgs e)
        {
            if (!_isSanguoshaActive && _sanguoshaLz4Playback == null && _sanguoshaPreparingToken == 0 && !_sanguoshaRasterChangePending && !_sanguoshaPreloadPending) return;
            _playToken++; _sanguoshaPreparationRevision++; _sanguoshaRetainWarmOnce = false;
            if (_isSanguoshaActive) { _timer.Stop(); _playbackClock.Stop(); }
            ResetSanguoshaState(); DisposeSanguoshaRenderer();
        }
        private void OnSanguoshaCompositionRendering(object sender, object value)
        {
            if (!_isSanguoshaActive) { StopSanguoshaFrameClock(); return; }
            var rendering = value as RenderingEventArgs;
            long stamp = rendering == null ? _playbackClock.Elapsed.Ticks : rendering.RenderingTime.Ticks;
            if (stamp == _sanguoshaLastCompositionTicks) return;
            if (_sanguoshaLastCompositionTicks < 0) _sanguoshaCompositionOriginTicks = stamp;
            _sanguoshaLastCompositionTicks = stamp;
            double ms = (stamp - _sanguoshaCompositionOriginTicks) / (double)TimeSpan.TicksPerMillisecond;
            const double period = 1000.0 / 60;
            if (ms + .1 < _sanguoshaNextPresentationMs) return;
            _sanguoshaNextPresentationMs = (Math.Floor((ms + .1) / period) + 1) * period;
            UpdateSanguoshaFrame();
        }
    }
}

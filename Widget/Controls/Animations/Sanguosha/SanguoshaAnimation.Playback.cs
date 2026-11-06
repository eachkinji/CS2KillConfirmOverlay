using System;
using KillConfirmCompatibility.Contracts;
using KillConfirmGameBar.Services;
using Windows.UI.Xaml;

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
            int token = BeginReplacementPlayback();

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
            _sanguoshaShake = new SanguoshaShake(assets.Clip, Environment.TickCount);
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
            _timer.Start();
        }

        private void ResetSanguoshaState() => ResetSanguoshaState(null);

        private void ResetSanguoshaState(SanguoshaTierAssets keep)
        {
            _isSanguoshaActive = false;
            _sanguoshaShake = null;
            _sanguoshaStartOffsetMs = 0;
            SanguoshaTierAssets finished = _sanguoshaCurrent;
            _sanguoshaCurrent = null;
            if (finished == null || finished == keep) return;
            lock (SanguoshaTiersOnScreen) SanguoshaTiersOnScreen.Remove(finished);
            bool stillCached;
            lock (SanguoshaTiers)
                stillCached = SanguoshaTiers.TryGetValue(finished.Tier, out SanguoshaTierAssets cached) && cached == finished;
            if (!stillCached)
                ReleaseUnusedSanguoshaPages(finished.Pages);
        }

        private void UpdateSanguoshaFrame()
        {
            SanguoshaTierAssets assets = _sanguoshaCurrent;
            // SgsSkeletonEffect.PLAY_COMPLETE at the end of the Spine animation also removes
            // the modal background (NewKillEffect.hideModalBg).
            if (assets == null || SanguoshaElapsedMs >= assets.Clip.Length.TotalMilliseconds)
            {
                _timer.Stop();
                _playbackClock.Stop();
                ResetSanguoshaState();
                Visibility = Visibility.Collapsed;
                return;
            }

            SpriteCanvas.Invalidate();
        }
    }
}

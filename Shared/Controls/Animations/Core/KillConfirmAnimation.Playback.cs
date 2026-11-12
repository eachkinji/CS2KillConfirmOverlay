// Shared by Widget (UWP) and CompatibilityHost; extracted from both former copies.
// Per-host lines stay inside #if WINDOWS_UWP blocks. See Tools/SharedSource/extract_shared.py.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Xaml;
#if WINDOWS_UWP
using KillConfirmGameBar.Services;
#else
using KillConfirmCompatibility.Services;
#endif
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

#if WINDOWS_UWP
namespace KillConfirmGameBar.Controls
#else
namespace KillConfirmCompatibility.Controls
#endif
{
    public sealed partial class KillConfirmAnimation : UserControl
    {
        private string _preparedPlaybackQueueKind;
        private int _preparedPlaybackQueueToken = -1;
        private int _preparedPlaybackQueueGeneration = -1;
        private Task _preparedPlaybackQueue = Task.CompletedTask;

        // Feed events retain their order while preparation runs. A new game,
        // resource generation, or replacement/stop cancels the obsolete queue.
        private void QueuePreparedPlayback(string kind, Action playback)
        {
            if (_preparedPlaybackQueueKind != kind
                || _preparedPlaybackQueueToken != _playToken
                || _preparedPlaybackQueueGeneration != _resourceGeneration)
            {
                _preparedPlaybackQueueKind = kind;
                _preparedPlaybackQueueToken = ++_playToken;
                _preparedPlaybackQueueGeneration = _resourceGeneration;
                _preparedPlaybackQueue = Task.CompletedTask;
            }

            _preparedPlaybackQueue = RunPreparedPlaybackAsync(
                _preparedPlaybackQueue, _preparedPlaybackQueueToken,
                _preparedPlaybackQueueGeneration, playback);
        }

        private async Task RunPreparedPlaybackAsync(
            Task previous, int token, int generation, Action playback)
        {
            try
            {
                await previous;
                if (token != _playToken || generation != _resourceGeneration) return;
                await WaitForCurrentPackPreloadAsync();
                if (token != _playToken || generation != _resourceGeneration) return;
                playback();
            }
            catch (OperationCanceledException) { }
            catch (Exception error)
            {
                App.Log("Queued animation playback failed: " + error);
#if WINDOWS_UWP
#else
                if (token == _playToken && generation == _resourceGeneration)
                    ReportDesktopPlaybackFailure(error);
#endif
            }
        }

        // A replacement owns the canvas immediately, including while it waits for
        // a preload. Leaving the previous asset active lets layout/loading redraw
        // that old frame at the new viewport and lets its timer outlive the event.
        private int BeginReplacementPlayback()
        {
            int token = ++_playToken;
            _timer.Stop();
            _playbackClock.Stop();
            _customSequencePlaying = false;
            _isCalabiyauActive = false;
            _isBattlefieldTextOverlayActive = false;
            _contentSizedViewport = false;
            _isBattlefield1CompactLayoutActive = false;
            _currentMetadata = null;
            _currentCodeAsset = null;
            _currentValorantAsset = null;
            _currentBattlefieldAsset = null;
            _currentCsolAsset = null;
            _currentFrame = 0;
            ResetBattlefield5ScrollingState();
            ResetBattlefield4HudState();
            ResetBattlefield2042HudState();
            ResetBattlefield6HudState();
            ResetSanguoshaState();
            ResetPubgHudState();
            ResetDeltaForceHudState();
            ResetDoubaoState();
            ResetDagoujiaoState();
            ResetOverwatchState();
            ResetApexFeedState();
            ResetModernWarfare2019State();
            ResetCod2022State();
            ResetTitanfall2State();
            ResetLeagueOfLegendsState();
            ResetRdr2State();
            ResetWarThunderState();
            HideLoadingProgress();
            Visibility = Visibility.Collapsed;
            SpriteCanvas.Invalidate();
            return token;
        }

        private async void PlayInternal(Func<IProgress<int>, Task<AnimationAsset>> assetLoader, bool showLoading = true, AnimationAsset cachedAsset = null)
        {
            int token = BeginReplacementPlayback();
            int resourceGeneration = _resourceGeneration;
            try
            {
                // Configuration starts the preload. Playback only waits for it;
                // it must not resize the old image into a Loading viewport.
                await WaitForCurrentPackPreloadAsync();
                if (token != _playToken || resourceGeneration != _resourceGeneration) return;
                AnimationAsset asset = cachedAsset;
                if (asset == null)
                {
                    await PreloadGate.WaitAsync();
                    try
                    {
                        if (token != _playToken || resourceGeneration != _resourceGeneration) return;
                        asset = await assetLoader(null);
                    }
                    finally
                    {
                        if (resourceGeneration != _resourceGeneration)
                        {
                            ReleaseAllAnimationResourceCaches(false);
                        }
                        PreloadGate.Release();
                    }
                }

                if (token != _playToken || resourceGeneration != _resourceGeneration || asset == null) return;
                _currentMetadata = asset.Metadata;
                _currentCodeAsset = asset.CodeAsset;
                _currentValorantAsset = asset.ValorantAsset;
                _currentBattlefieldAsset = asset.BattlefieldAsset;
                _currentCsolAsset = asset.CsolAsset;
                _currentFrame = 0;

                ApplyViewportSize(asset.Metadata.FrameWidth, asset.Metadata.FrameHeight);
                Visibility = Visibility.Visible;
                double samplingFps = _currentCodeAsset != null && _mainAnimationStyle == 2
                    ? Math.Max(30, Math.Min(60, _targetPlaybackFps)) : FrameSequenceFps;
                _timer.Interval = TimeSpan.FromMilliseconds(1000.0 / samplingFps);
                _playbackClock.Restart();
                ShowFrame(0);
                _timer.Start();
            }
            catch (Exception ex)
            {
                App.Log("Animation asset load/playback failed: " + ex);
                // A superseded load can fail after a newer cached event has
                // started. It no longer owns the canvas or the playback timer.
                if (token != _playToken || resourceGeneration != _resourceGeneration) return;
                BeginReplacementPlayback();
            }
        }

        private async Task ShowLoadingProgressIfStillLoadingAsync(int token, IProgress<int> progress)
        {
            await Task.Delay(LoadingIndicatorDelayMs);
            if (token == _playToken)
            {
                progress?.Report(0);
            }
        }

    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Xaml;
using KillConfirmGameBar.Services;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace KillConfirmGameBar.Controls
{
    public sealed partial class KillConfirmAnimation : UserControl
    {
        private async void PlayInternal(Func<IProgress<int>, Task<AnimationAsset>> assetLoader, bool showLoading = true, AnimationAsset cachedAsset = null)
        {
            _customSequencePlaying = false;
            int resourceGeneration = _resourceGeneration;
            _contentSizedViewport = false;
            _isBattlefield1CompactLayoutActive = false;
            ResetBattlefield5ScrollingState();
            ResetBattlefield4HudState();
            ResetBattlefield2042HudState();
            ResetPubgHudState();
            ResetDeltaForceHudState();
            ResetDoubaoState();
            ResetDagoujiaoState();
            ResetOverwatchState();
            ResetApexFeedState();
            ResetModernWarfare2019State();
            int token = ++_playToken;
            bool isLoading = true;
            var progress = new Progress<int>(value =>
            {
                if (showLoading && isLoading && token == _playToken)
                {
                    ShowLoadingProgress(value);
                }
            });

            try
            {
                if (showLoading) _ = ShowLoadingProgressIfStillLoadingAsync(token, progress);
                AnimationAsset asset = cachedAsset;
                if (asset == null)
                {
                    await PreloadGate.WaitAsync();
                    try
                    {
                        if (resourceGeneration != _resourceGeneration)
                        {
                            return;
                        }
                        asset = await assetLoader(progress);
                    }
                    finally
                    {
                        if (resourceGeneration != _resourceGeneration)
                        {
                            ReleaseAllAnimationResourceCaches();
                        }
                        PreloadGate.Release();
                    }
                }

                if (token != _playToken || resourceGeneration != _resourceGeneration)
                {
                    return;
                }

                isLoading = false;
                _timer.Stop();
                _currentMetadata = asset.Metadata;
                _currentCodeAsset = asset.CodeAsset;
                _currentValorantAsset = asset.ValorantAsset;
                _currentBattlefieldAsset = asset.BattlefieldAsset;
                _currentCsolAsset = asset.CsolAsset;
                _currentFrame = 0;

                ApplyViewportSize(asset.Metadata.FrameWidth, asset.Metadata.FrameHeight);

                HideLoadingProgress();
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
                isLoading = false;
                HideLoadingProgress();
                Visibility = Visibility.Collapsed;
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

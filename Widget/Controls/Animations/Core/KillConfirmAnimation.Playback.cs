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
        private async void PlayInternal(Func<IProgress<int>, Task<AnimationAsset>> assetLoader)
        {
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
                if (isLoading && token == _playToken)
                {
                    ShowLoadingProgress(value);
                }
            });

            try
            {
                _ = ShowLoadingProgressIfStillLoadingAsync(token, progress);
                AnimationAsset asset;
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
                _timer.Interval = TimeSpan.FromMilliseconds(1000.0 / FrameSequenceFps);
                ShowFrame(0);
                _playbackClock.Restart();
                _timer.Start();
            }
            catch
            {
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

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KillConfirmGameBar.Services;
using Microsoft.Graphics.Canvas;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.UI.Xaml;

namespace KillConfirmGameBar.Controls
{
    public sealed partial class KillConfirmAnimation
    {
        private sealed class CustomSequenceAsset : IDisposable
        {
            public CustomSequenceMetadata Metadata;
            public readonly List<CanvasBitmap> Pages = new List<CanvasBitmap>();
            public int FramesPerPage;
            public int Columns;
            public void Dispose() { foreach (var page in Pages) page.Dispose(); Pages.Clear(); }
        }

        private CustomSequenceAsset _customSequence;
        private string _customSequenceKey;
        private bool _customSequencePlaying;
        private CustomSequenceState _customState;
        private CustomModuleSettings _customSettings;
        public event EventHandler<string> CustomSequenceStatusChanged;

        public async void PlayCustomKill(int kills, bool headshot, string packKey = null)
        {
            int token = ++_playToken;
            int generation = _resourceGeneration;
            _timer.Stop();
            _playbackClock.Stop();
            _customSequencePlaying = false;
            Visibility = Visibility.Collapsed;
            string key = packKey ?? _iconPack;
            if (!GameStyleService.IsCustomModuleKey(key)) return;
            try
            {
                await PreloadGate.WaitAsync();
                try
                {
                    if (token != _playToken || generation != _resourceGeneration) return;
                    _customSettings = CustomModuleSettingsStore.Load();
                    bool ready = await LoadCustomSequenceAsync(key, Math.Max(1, Math.Min(5, kills)), headshot && _customSettings.Headshots);
                    if (token != _playToken || generation != _resourceGeneration)
                    {
                        ReleaseCustomSequence();
                        return;
                    }
                    if (!ready)
                    {
                        CustomSequenceStatusChanged?.Invoke(this, "Missing animation for this level / 当前等级没有可播放的素材。");
                        return;
                    }
                }
                finally { PreloadGate.Release(); }
                ResetDoubaoState(); ResetDagoujiaoState(); ResetOverwatchState();
                ResetApexFeedState(); ResetModernWarfare2019State();
                ResetBattlefield5ScrollingState(); ResetBattlefield4HudState();
                ResetBattlefield2042HudState(); ResetPubgHudState(); ResetDeltaForceHudState();
                _currentCodeAsset = null; _currentValorantAsset = null;
                _currentBattlefieldAsset = null; _currentCsolAsset = null;
                _isBattlefieldTextOverlayActive = false;
                _contentSizedViewport = true;
                _isBattlefield1CompactLayoutActive = false;
                _customSequencePlaying = true;
                // Reference player displays a 350px-wide frame, preserving its aspect ratio.
                ApplyViewportSize(350, 350.0 * _customSequence.Metadata.Height / _customSequence.Metadata.Width);
                HideLoadingProgress();
                Visibility = Visibility.Visible;
                _playbackClock.Restart();
                _timer.Interval = TimeSpan.FromMilliseconds(1000.0 / 60);
                UpdateCustomSequenceFrame();
                _timer.Start();
            }
            catch (Exception ex)
            {
                App.Log("Custom sequence playback: " + ex.Message);
                if (token == _playToken)
                {
                    _customSequencePlaying = false; Visibility = Visibility.Collapsed;
                    CustomSequenceStatusChanged?.Invoke(this, ex.Message);
                }
            }
        }

        private async Task<bool> LoadCustomSequenceAsync(string key, int level, bool headshot)
        {
            StorageFolder folder = await PackCatalogService.GetImportedIconFolderAsync(key);
            if (folder == null) return false;
            string slot = await CustomSequencePackService.ResolveSlotAsync(folder, level, headshot);
            if (slot == null) return false;
            string cacheKey = key + "/" + folder.Path + "/" + slot;
            if (_customSequence != null && _customSequenceKey == cacheKey) return true;
            var next = new CustomSequenceAsset { Metadata = await CustomSequencePackService.ReadMetadataAsync(folder, slot) };
            try
            {
                var m = next.Metadata;
                next.Columns = Math.Max(1, Math.Min(m.Frames, 4096 / m.Width));
                int rowsPerPage = Math.Max(1, 2048 / m.Height);
                next.FramesPerPage = next.Columns * rowsPerPage;
                await Task.Run(async () =>
                {
                    using (var stream = await (await folder.GetFileAsync(slot + ".png")).OpenReadAsync())
                    {
                        BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);
                        byte[] source = (await decoder.GetPixelDataAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied,
                            new BitmapTransform(), ExifOrientationMode.IgnoreExifOrientation, ColorManagementMode.DoNotColorManage)).DetachPixelData();
                        int sourceWidth = checked((int)decoder.PixelWidth);
                        for (int start = 0; start < m.Frames; start += next.FramesPerPage)
                        {
                            int count = Math.Min(next.FramesPerPage, m.Frames - start);
                            byte[] page = CustomSequenceFormat.RepackPage(source, sourceWidth, m, start, count,
                                next.Columns, out int width, out int height);
                            next.Pages.Add(CanvasBitmap.CreateFromBytes(CanvasDevice.GetSharedDevice(), page, width, height,
                                Windows.Graphics.DirectX.DirectXPixelFormat.B8G8R8A8UIntNormalized, 96, CanvasAlphaMode.Premultiplied));
                        }
                    }
                });
                _customSequence?.Dispose();
                _customSequence = next;
                _customSequenceKey = cacheKey;
                return true;
            }
            catch { next.Dispose(); throw; }
        }

        private async Task PreloadCustomSequenceAsync(IProgress<int> progress)
        {
            progress?.Report(0);
            // Keep only one level's textures resident, not all 10 variants.
            await LoadCustomSequenceAsync(_iconPack, 1, false);
            progress?.Report(100);
        }

        private void UpdateCustomSequenceFrame()
        {
            if (_customSequence == null) return;
            var m = _customSequence.Metadata;
            var state = CustomSequenceFormat.At(_playbackClock.Elapsed.TotalSeconds, m.Frames,
                _customSettings.Fps > 0 ? _customSettings.Fps : m.Fps,
                _customSettings.Hold >= 0 ? _customSettings.Hold : m.HoldSeconds, _customSettings.Fade);
            if (state.Finished)
            {
                _timer.Stop(); _playbackClock.Stop();
                _customSequencePlaying = false; Visibility = Visibility.Collapsed;
                return;
            }
            bool changed = state.Frame != _customState.Frame || Math.Abs(state.Opacity - _customState.Opacity) >= 0.005;
            _customState = state;
            if (changed || _playbackClock.ElapsedMilliseconds < 25) SpriteCanvas.Invalidate();
        }

        private void DrawCustomSequenceFrame(CanvasDrawingSession session)
        {
            if (_customSequence == null) return;
            var asset = _customSequence;
            var m = asset.Metadata;
            int page = _customState.Frame / asset.FramesPerPage;
            int frame = _customState.Frame % asset.FramesPerPage;
            session.DrawImage(asset.Pages[page], new Rect(0, 0, _logicalFrameWidth, _logicalFrameHeight),
                new Rect((frame % asset.Columns) * m.Width, (frame / asset.Columns) * m.Height, m.Width, m.Height),
                _customState.Opacity, CanvasImageInterpolation.Linear);
        }

        public void StopCustomSequence()
        {
            _playToken++;
            _timer.Stop(); _playbackClock.Stop();
            _customSequencePlaying = false;
            Visibility = Visibility.Collapsed;
        }

        public void ReleaseCustomSequenceResources() { StopCustomSequence(); ReleaseCustomSequence(); }

        private void ReleaseCustomSequence()
        {
            _customSequencePlaying = false;
            _customSequence?.Dispose();
            _customSequence = null; _customSequenceKey = null;
        }
    }
}

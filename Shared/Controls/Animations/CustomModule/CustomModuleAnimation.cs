// Shared by Widget (UWP) and CompatibilityHost; extracted from both former copies.
// Per-host lines stay inside #if WINDOWS_UWP blocks. See Tools/SharedSource/extract_shared.py.
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
#if WINDOWS_UWP
using KillConfirmGameBar.Services;
#else
using KillConfirmCompatibility.Services;
#endif
using Microsoft.Graphics.Canvas;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.UI.Xaml;

#if WINDOWS_UWP
namespace KillConfirmGameBar.Controls
#else
namespace KillConfirmCompatibility.Controls
#endif
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
        private static readonly Dictionary<string, CustomSequenceAsset> CustomSequenceAssets = new Dictionary<string, CustomSequenceAsset>();
        private static readonly Dictionary<string, string> CustomSequenceSlots = new Dictionary<string, string>();
        private bool _customSequencePlaying;
        private CustomSequenceState _customState;
        public event EventHandler<string> CustomSequenceStatusChanged;

        public async void PlayCustomKill(int kills, bool headshot, string packKey = null)
        {
            int token = BeginReplacementPlayback();
            int generation = _resourceGeneration;
            _timer.Stop();
            _playbackClock.Stop();
            _customSequencePlaying = false;
            Visibility = Visibility.Collapsed;
            string key = packKey ?? _iconPack;
            if (!GameStyleService.IsCustomModuleKey(key)) return;
            try
            {
                await WaitForCurrentPackPreloadAsync();
                if (token != _playToken || generation != _resourceGeneration) return;
                if (!SelectPreloadedCustomSequence(key, Math.Max(1, Math.Min(5, kills)), headshot))
                {
                    CustomSequenceStatusChanged?.Invoke(this, "Missing animation for this level / 当前等级没有可播放的素材。");
                    return;
                }
                ResetDoubaoState(); ResetDagoujiaoState(); ResetOverwatchState();
                ResetApexFeedState(); ResetModernWarfare2019State();
                ResetBattlefield5ScrollingState(); ResetBattlefield4HudState();
                ResetBattlefield2042HudState();
            ResetBattlefield6HudState(); ResetSanguoshaState(); ResetPubgHudState(); ResetDeltaForceHudState();
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

        private static string CustomSequenceRequestKey(string key, int level, bool headshot)
            => key + ":" + level + ":" + headshot;

        private bool SelectPreloadedCustomSequence(string key, int level, bool headshot)
        {
            if (!CustomSequenceSlots.TryGetValue(CustomSequenceRequestKey(key, level, headshot), out string cacheKey))
                throw new InvalidOperationException("Custom animation slot was not preloaded.");
            _customSequenceKey = cacheKey;
            _customSequence = cacheKey == null ? null : CustomSequenceAssets[cacheKey];
            return _customSequence != null;
        }

        private async Task<bool> LoadCustomSequenceAsync(string key, int level, bool headshot)
        {
            string request = CustomSequenceRequestKey(key, level, headshot);
            if (CustomSequenceSlots.ContainsKey(request)) return SelectPreloadedCustomSequence(key, level, headshot);
            StorageFolder folder = await PackCatalogService.GetImportedIconFolderAsync(key);
            if (folder == null) throw new System.IO.FileNotFoundException("The selected custom icon pack is not installed.");
            string slot = await CustomSequencePackService.ResolveSlotAsync(folder, level, headshot);
            if (slot == null) { CustomSequenceSlots[request] = null; return false; }
            string cacheKey = key + "/" + folder.Path + "/" + slot;
            if (CustomSequenceAssets.ContainsKey(cacheKey))
            {
                CustomSequenceSlots[request] = cacheKey;
                return SelectPreloadedCustomSequence(key, level, headshot);
            }
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
                CustomSequenceAssets.Add(cacheKey, next);
                CustomSequenceSlots[request] = cacheKey;
                _customSequence = next;
                _customSequenceKey = cacheKey;
                return true;
            }
            catch { next.Dispose(); throw; }
        }

        private async Task PreloadCustomSequenceAsync(IProgress<int> progress)
        {
            progress?.Report(0);
            for (int level = 1; level <= 5; level++)
            {
                await LoadCustomSequenceAsync(_iconPack, level, false);
                await LoadCustomSequenceAsync(_iconPack, level, true);
                progress?.Report(level * 19);
            }
            _customSequence = null;
            _customSequenceKey = null;
            progress?.Report(100);
        }

        private void UpdateCustomSequenceFrame()
        {
            if (_customSequence == null) return;
            var m = _customSequence.Metadata;
            var state = CustomSequenceFormat.At(
                _playbackClock.Elapsed.TotalSeconds,
                m.Frames,
                m.Fps,
                m.HoldSeconds);
            if (state.Finished)
            {
                _timer.Stop(); _playbackClock.Stop();
                _customSequencePlaying = false; Visibility = Visibility.Collapsed;
                return;
            }
            bool changed = state.Frame != _customState.Frame;
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
                1, CanvasImageInterpolation.Linear);
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
            _customSequence = null; _customSequenceKey = null;
        }

        private static void ClearCustomSequenceCache()
        {
            foreach (CustomSequenceAsset asset in CustomSequenceAssets.Values) asset.Dispose();
            CustomSequenceAssets.Clear();
            CustomSequenceSlots.Clear();
        }
    }
}

using System;
using System.IO;
using System.Numerics;
using System.Threading.Tasks;
using KillConfirmCompatibility.Contracts;

namespace KillConfirmCompatibility.Controls
{
    public sealed partial class KillConfirmAnimation
    {
        private SanguoshaLz4Playback _desktopSanguoshaLz4;
        private string _desktopSanguoshaVariant;
        private bool _desktopSanguoshaVariantLoading, _desktopSanguoshaVariantMissing;
        private int _desktopSanguoshaVariantGeneration;
        private Task _desktopSanguoshaAtlasTask;
        private string _desktopSanguoshaPath = "inactive";
        internal string DesktopSanguoshaPath => _desktopSanguoshaPath;

        private bool RetainDesktopSanguoshaLz4()
        {
            if (_desktopSanguoshaLz4 == null || !Services.GameStyleService.IsSanguoshaKey(_iconPack)) return false;
            _desktopSanguoshaPath = "lz4: idle exact cache";
            return true;
        }

        private void WarmDesktopSanguoshaLz4(SanguoshaTierAssets assets)
        {
            string name = GetSanguoshaAnimationName(_iconPack, assets.Tier);
            if (_desktopSanguoshaLz4 == null || _desktopSanguoshaVariant == null
                || !_desktopSanguoshaVariant.Contains("/" + name + ".sgslz4|", StringComparison.Ordinal)) return;
            try { _desktopSanguoshaLz4.RequestFrame(Math.Max(0, assets.Clip.FrameAt(TimeSpan.FromMilliseconds(_sanguoshaStartOffsetMs)))); }
            catch { ResetDesktopSanguoshaLz4(); }
        }

        // Keep the same float matrix products as the existing desktop wrapper.
        // A reduced stage bitmap scaled afterwards is not an equivalent raster.
        internal static Matrix3x2 DesktopSanguoshaVariantTransform(int width, int height) =>
            Matrix3x2.CreateScale(0.6875f) * CreateDesktopTransform(1600, 900, 0.6875, width, height);

        internal bool TryAcquireDesktopSanguoshaFrame(int width, int height, out SanguoshaLz4FrameLease frame)
        {
            frame = null;
            if (!_isSanguoshaActive || _sanguoshaCurrent == null) return false;
            string name = GetSanguoshaAnimationName(_iconPack, _sanguoshaCurrent.Tier);
            string variant = "lz4/desktop-" + width + "x" + height + "/" + name + ".sgslz4";
            string signature = variant + "|" + DesktopSanguoshaColorSignature;
            if (_desktopSanguoshaVariant != signature)
            {
                ResetDesktopSanguoshaLz4();
                _desktopSanguoshaVariant = signature;
                _desktopSanguoshaVariantLoading = true;
                _desktopSanguoshaPath = "lz4: opening " + width + "x" + height;
                _ = OpenDesktopSanguoshaVariantAsync(variant, width, height, _sanguoshaCurrent,
                    _desktopSanguoshaVariantGeneration);
            }
            if (_desktopSanguoshaVariantLoading) return true;
            if (_desktopSanguoshaVariantMissing) return false;
            if (_desktopSanguoshaLz4 == null) return true;
            int index = Math.Min(_sanguoshaCurrent.Clip.Frames.Length - 2,
                _sanguoshaCurrent.Clip.FrameAt(TimeSpan.FromMilliseconds(SanguoshaElapsedMs)));
            try
            {
                frame = _desktopSanguoshaLz4.AcquireFrame(Math.Max(0, index));
                _desktopSanguoshaPath = "lz4: " + width + "x" + height + ", frame=" + index;
                SpriteCanvas.MarkPresented();
                return true;
            }
            catch (Exception error)
            {
                _desktopSanguoshaLz4.Dispose();
                _desktopSanguoshaLz4 = null;
                _desktopSanguoshaVariantMissing = true;
                _desktopSanguoshaPath = "atlas: LZ4 decode failure";
                App.Log("Sanguosha LZ4 fallback: " + error);
                return false;
            }
        }

        private async Task OpenDesktopSanguoshaVariantAsync(string variant, int width, int height,
            SanguoshaTierAssets assets, int generation)
        {
            SanguoshaLz4Playback playback = null;
            try
            {
                playback = await Task.Run(async () =>
                {
                    if (!DesktopSanguoshaUsesBundledColors) throw new FileNotFoundException("Appearance needs an exact local variant.");
                    var file = await SanguoshaAssetFileAsync(variant);
                    var stream = await file.OpenStreamForReadAsync();
                    SanguoshaLz4Source source = null;
                    try
                    {
                        source = SanguoshaLz4Source.Open(stream);
                        if (source.Width != width || source.Height != height || source.FrameCount != assets.Clip.Frames.Length - 1
                            || Math.Abs(source.Fps - assets.Clip.Fps) > .0001)
                            throw new InvalidDataException("Sanguosha LZ4 variant does not match its source clip.");
                        return new SanguoshaLz4Playback(source);
                    }
                    catch { if (source != null) source.Dispose(); else stream.Dispose(); throw; }
                });
                if (generation != _desktopSanguoshaVariantGeneration || assets != _sanguoshaCurrent)
                {
                    playback.Dispose(); return;
                }
                AttachDesktopSanguoshaPlayback(playback, assets);
            }
            catch (Exception error)
            {
                if (generation != _desktopSanguoshaVariantGeneration || assets != _sanguoshaCurrent) return;
                if (!(error is FileNotFoundException) && !(error is DirectoryNotFoundException))
                    App.Log("Sanguosha exact variant unavailable: " + error.Message);
                await PrepareDesktopSanguoshaCacheAsync(width, height, assets, generation);
            }
        }

        private void AttachDesktopSanguoshaPlayback(SanguoshaLz4Playback playback, SanguoshaTierAssets assets)
        {
            _desktopSanguoshaLz4?.Dispose();
            _desktopSanguoshaLz4 = playback;
            _desktopSanguoshaVariantLoading = _desktopSanguoshaVariantMissing = false;
            _sanguoshaRenderer?.ReleaseFrameEffectSources();
            assets.Pages = Array.Empty<Microsoft.Graphics.Canvas.CanvasBitmap>();
            TrimSanguoshaPages();
            SpriteCanvas.Invalidate();
        }

        private async void RequestDesktopSanguoshaAtlas(SanguoshaTierAssets assets)
        {
            if (assets.Pages.Length != 0 || (_desktopSanguoshaAtlasTask != null && !_desktopSanguoshaAtlasTask.IsCompleted)) return;
            try
            {
                _desktopSanguoshaAtlasTask = EnsureSanguoshaPagesAsync(assets);
                await _desktopSanguoshaAtlasTask;
                if (assets == _sanguoshaCurrent)
                {
                    if (_desktopSanguoshaLz4 != null)
                    {
                        assets.Pages = Array.Empty<Microsoft.Graphics.Canvas.CanvasBitmap>();
                        TrimSanguoshaPages();
                    }
                    SpriteCanvas.Invalidate();
                }
            }
            catch (Exception error)
            {
                if (assets != _sanguoshaCurrent) return;
                ReportDesktopPlaybackFailure(error);
                App.Log("Sanguosha current-tier atlas fallback: " + error);
            }
        }

        private void ResetDesktopSanguoshaLz4()
        {
            _desktopSanguoshaVariantGeneration++;
            _desktopSanguoshaLz4?.Dispose();
            _desktopSanguoshaLz4 = null;
            _desktopSanguoshaVariant = null;
            _desktopSanguoshaVariantLoading = _desktopSanguoshaVariantMissing = false;
            _desktopSanguoshaPath = "inactive";
        }
    }
}

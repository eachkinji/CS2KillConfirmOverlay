using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using KillConfirmCompatibility.Contracts;
using KillConfirmCompatibility.Desktop.Runtime;
using Microsoft.Graphics.Canvas;

namespace KillConfirmCompatibility.Controls
{
    public sealed partial class KillConfirmAnimation
    {
        private static readonly SemaphoreSlim SanguoshaCacheBuildGate = new SemaphoreSlim(1, 1);
        private const long SanguoshaCacheFileLimit = 2L * 1024 * 1024 * 1024;
        private const long SanguoshaDiskCacheLimit = 8L * 1024 * 1024 * 1024;
        private bool DesktopSanguoshaUsesBundledColors => _brightnessBoost == 0 && _contrastBoost == 0
            && _appearanceBrightness == 1 && _appearanceContrast == 1 && _renderResolutionScale == 1;
        private string DesktopSanguoshaColorSignature => string.Join(",", new[] { _brightnessBoost, _contrastBoost,
            _appearanceBrightness, _appearanceContrast, _renderResolutionScale }.Select(value => value.ToString("R", CultureInfo.InvariantCulture)));

        private async Task PrepareDesktopSanguoshaCacheAsync(int width, int height, SanguoshaTierAssets assets, int generation)
        {
            string temporary = null;
            bool gateHeld = false, pinned = false;
            int resourceGeneration = _resourceGeneration;
            string color = DesktopSanguoshaColorSignature;
            string requestedSignature = _desktopSanguoshaVariant;
            bool Current() => requestedSignature == _desktopSanguoshaVariant && assets == _sanguoshaCurrent
                && resourceGeneration == _resourceGeneration && color == DesktopSanguoshaColorSignature;
            // Finishing the event must not discard a nearly finished cache.
            // A different active event/size or pack cancels this preparation.
            bool BuildAllowed() => resourceGeneration == _resourceGeneration && color == DesktopSanguoshaColorSignature
                && (_sanguoshaCurrent == null || Current());
            try
            {
                if (width <= 0 || height <= 0 || width > 4096 || height > 4096 || (long)width * height > 8388608)
                    throw new InvalidOperationException("Exact raster exceeds the bounded LZ4 frame-cache size.");
                assets.SourceFingerprint ??= ComputeSanguoshaSourceFingerprintAsync(assets);
                string fingerprint = await assets.SourceFingerprint;
                if (!Current()) return;
                string key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
                    "desktop-raster-v2|fixed-shake-20261107|" + width + "x" + height + "|" + color + "|" + fingerprint)));
                string directory = Path.Combine(DesktopStorage.Current.LocalFolder.Path, "Cache", "SanguoshaLz4");
                string path = Path.Combine(directory, key + ".sgslz4");
                if (File.Exists(path))
                {
                    try
                    {
                        if (new FileInfo(path).Length > SanguoshaCacheFileLimit) throw new InvalidDataException("Exact raster cache exceeds the per-file limit.");
                        try { File.SetLastWriteTimeUtc(path, DateTime.UtcNow); }
                        catch (IOException) { }
                        catch (UnauthorizedAccessException) { }
                        var source = SanguoshaLz4Source.Open(File.OpenRead(path));
                        if (source.Width != width || source.Height != height || source.FrameCount != assets.Clip.Frames.Length - 1)
                        { source.Dispose(); throw new InvalidDataException("Exact raster cache header mismatch."); }
                        AttachDesktopSanguoshaPlayback(new SanguoshaLz4Playback(source), assets);
                        TrimDesktopSanguoshaDiskCache(directory, path);
                        return;
                    }
                    catch (Exception failure)
                    {
                        App.Log("Rebuilding invalid Sanguosha raster cache: " + failure.Message);
                        try { File.Delete(path); } catch (IOException) { } catch (UnauthorizedAccessException) { }
                    }
                }
                _desktopSanguoshaVariantLoading = false;
                _desktopSanguoshaVariantMissing = true;
                _desktopSanguoshaPath = "atlas: preparing exact LZ4 " + width + "x" + height;
                RequestDesktopSanguoshaAtlas(assets);
                await SanguoshaCacheBuildGate.WaitAsync(); gateHeld = true;
                if (!Current()) return;
                // A previous occurrence of this same event can finish its
                // cache while this request waits. Reuse that one completed file.
                if (File.Exists(path))
                {
                    var completed = SanguoshaLz4Source.Open(File.OpenRead(path));
                    if (completed.Width != width || completed.Height != height || completed.FrameCount != assets.Clip.Frames.Length - 1)
                    { completed.Dispose(); throw new InvalidDataException("Exact raster cache header mismatch."); }
                    AttachDesktopSanguoshaPlayback(new SanguoshaLz4Playback(completed), assets);
                    return;
                }
                lock (SanguoshaTiersOnScreen) SanguoshaCacheBuilds.Add(assets);
                pinned = true;
                if (_desktopSanguoshaAtlasTask != null) await _desktopSanguoshaAtlasTask;
                if (!Current()) return;
                if (assets.Pages.Length == 0) await EnsureSanguoshaPagesAsync(assets);
                if (!Current()) return;
                Directory.CreateDirectory(directory);
                TrimDesktopSanguoshaDiskCache(directory, path);
                temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                var bake = new KillConfirmAnimation
                {
                    _sanguoshaCurrent = assets, _isSanguoshaActive = true,
                    _logicalFrameWidth = SanguoshaFrameWidth, _logicalFrameHeight = SanguoshaFrameHeight,
                    _sanguoshaShake = new SanguoshaShake(assets.Clip, 20261107),
                    _appearanceBrightness = _appearanceBrightness, _appearanceContrast = _appearanceContrast,
                    _renderResolutionScale = _renderResolutionScale
                };
                using var target = new CanvasRenderTarget(CanvasDevice.GetSharedDevice(), width, height, 96);
                int count = assets.Clip.Frames.Length - 1, bytes = checked(width * height * 4);
                var raw = new byte[bytes]; var rawBuffer = raw.AsBuffer();
                var compressed = new byte[SanguoshaLz4Codec.MaximumCompressedLength(bytes)];
                try
                {
                    using (var file = File.Create(temporary))
                    using (var writer = new BinaryWriter(file))
                    {
                        writer.Write(Encoding.ASCII.GetBytes("KCLZ4F01")); writer.Write(1);
                        writer.Write(width); writer.Write(height); writer.Write(checked(width * 4)); writer.Write(1);
                        writer.Write((int)assets.Clip.Fps); writer.Write(1); writer.Write(count); writer.Write(bytes);
                        for (int frame = 0; frame < count; frame++)
                        {
                            if (!BuildAllowed()) throw new OperationCanceledException();
                            bake._sanguoshaStartOffsetMs = frame * 1000d / assets.Clip.Fps;
                            using (var draw = target.CreateDrawingSession()) bake.DrawDesktopFrame(draw, width, height, cropEmptySpace: true);
                            target.GetPixelBytes(rawBuffer);
                            int length = SanguoshaLz4Codec.Compress(raw, compressed);
                            if (file.Position + sizeof(int) + length > SanguoshaCacheFileLimit)
                                throw new InvalidDataException("Exact raster cache exceeds the 2 GiB per-file limit.");
                            writer.Write(length); writer.Write(compressed, 0, length);
                            // This one-time build shares the UI-owned renderer;
                            // yield between frames rather than monopolizing it.
                            await Task.Delay(1);
                        }
                    }
                }
                finally { bake.DisposeSanguoshaRenderer(); }
                if (!BuildAllowed()) return;
                File.Move(temporary, path, true); temporary = null;
                TrimDesktopSanguoshaDiskCache(directory, path);
                lock (SanguoshaTiersOnScreen) SanguoshaCacheBuilds.Remove(assets);
                pinned = false;
                if (!Current()) { ReleaseUnusedSanguoshaPages(assets.Pages); TrimSanguoshaPages(); return; }
                var ready = SanguoshaLz4Source.Open(File.OpenRead(path));
                AttachDesktopSanguoshaPlayback(new SanguoshaLz4Playback(ready), assets);
                _desktopSanguoshaPath = "lz4: exact generated cache " + width + "x" + height;
            }
            catch (OperationCanceledException) { }
            catch (Exception error)
            {
                if (Current())
                {
                    _desktopSanguoshaVariantLoading = false;
                    _desktopSanguoshaVariantMissing = true;
                    _desktopSanguoshaPath = "atlas: exact-cache preparation failed: " + error.Message;
                    RequestDesktopSanguoshaAtlas(assets);
                    SpriteCanvas.Invalidate();
                }
                App.Log("Sanguosha exact-cache preparation: " + error.Message);
            }
            finally
            {
                if (temporary != null) { try { File.Delete(temporary); } catch (IOException) { } }
                if (pinned) lock (SanguoshaTiersOnScreen) SanguoshaCacheBuilds.Remove(assets);
                if (gateHeld) SanguoshaCacheBuildGate.Release();
                if (pinned) { ReleaseUnusedSanguoshaPages(assets.Pages); TrimSanguoshaPages(); }
            }
        }

        private static void TrimDesktopSanguoshaDiskCache(string directory, string currentPath)
        {
            // This folder is exclusively owned by SGS generated rasters. Never
            // touch temporary builds, bundled media or another game's cache.
            if (!Directory.Exists(directory)) return;
            var files = new DirectoryInfo(directory).EnumerateFiles("*.sgslz4", SearchOption.TopDirectoryOnly)
                .Where(file => Path.GetFileNameWithoutExtension(file.Name).Length == 64 && file.Name.Take(64).All(Uri.IsHexDigit))
                .OrderBy(file => file.LastWriteTimeUtc).ToArray();
            long total = files.Sum(file => file.Length);
            foreach (var file in files)
            {
                if (total <= SanguoshaDiskCacheLimit && file.Length <= SanguoshaCacheFileLimit) continue;
                if (string.Equals(file.FullName, currentPath, StringComparison.OrdinalIgnoreCase)) continue;
                try
                {
                    long bytes = file.Length;
                    // Source streams intentionally use FileShare.Read, so an
                    // active decoder prevents deletion while it owns the file.
                    file.Delete(); total -= bytes;
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }

        private static Task<string> ComputeSanguoshaSourceFingerprintAsync(SanguoshaTierAssets assets) => Task.Run(async () =>
        {
            string name = GetSanguoshaAnimationName(_iconPack, assets.Tier);
            var paths = new[] { name + "/" + name + ".sgsd.gz" }.Concat(assets.Clip.Pages.Select(page =>
                page.File.Contains('/') ? page.File : name + "/" + page.File));
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var buffer = new byte[65536];
            foreach (string path in paths)
            {
                hash.AppendData(Encoding.UTF8.GetBytes(path));
                var file = await SanguoshaAssetFileAsync(path);
                using var stream = await file.OpenStreamForReadAsync();
                int read;
                while ((read = await stream.ReadAsync(buffer, 0, buffer.Length)) != 0) hash.AppendData(buffer, 0, read);
            }
            return Convert.ToHexString(hash.GetHashAndReset());
        });
    }
}

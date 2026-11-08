using System;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using KillConfirmCompatibility.Contracts;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Effects;
using Windows.Graphics.DirectX;
using Windows.Storage;

namespace KillConfirmGameBar.Controls
{
    public sealed partial class KillConfirmAnimation
    {
        private static readonly SemaphoreSlim SanguoshaPreparationGate = new SemaphoreSlim(1, 1);
        private bool _sanguoshaRasterChangePending;
        private sealed class SanguoshaRasterProfile
        {
            internal int Width, Height;
            internal float Dpi, Scale, LogicalScale, WidthDips, HeightDips;
            internal double BrightnessBoost, ContrastBoost, AppearanceBrightness, AppearanceContrast;
            internal string Id => "uwp-" + Width + "x" + Height + "-s" + Scale.ToString("R", CultureInfo.InvariantCulture).Replace('.', 'p');
            internal string Signature => Id + ";dpi=" + Dpi.ToString("R", CultureInfo.InvariantCulture) + ";boost=" + BrightnessBoost.ToString("R", CultureInfo.InvariantCulture) + "," + ContrastBoost.ToString("R", CultureInfo.InvariantCulture)
                + ";appearance=" + AppearanceBrightness.ToString("R", CultureInfo.InvariantCulture) + "," + AppearanceContrast.ToString("R", CultureInfo.InvariantCulture) + ";seed=20261107;renderer=4";
            internal bool Neutral => BrightnessBoost == 0 && ContrastBoost == 0 && AppearanceBrightness == 1 && AppearanceContrast == 1;
        }
        private SanguoshaRasterProfile GetRequestedSanguoshaRasterProfile()
        {
            double fit = Math.Min(ReferenceDisplayWidth / SanguoshaFrameWidth, ReferenceDisplayHeight / SanguoshaFrameHeight);
            double scale = fit * Math.Max(2.0, Math.Min(4.0, _renderResolutionScale * 2.0));
            scale = Math.Max(0.1, Math.Min(scale, Math.Min(Math.Sqrt(MaxCanvasPixelArea / (SanguoshaFrameWidth * SanguoshaFrameHeight)),
                Math.Min(MaxCanvasPixelWidth / SanguoshaFrameWidth, MaxCanvasPixelHeight / SanguoshaFrameHeight))));
            var animated = _sanguoshaAnimatedSurface?.Control;
            bool useAnimatedDpi = animated != null && animated.ReadyToDraw;
            float dpi = useAnimatedDpi ? animated.Dpi : SpriteCanvas.Dpi;
            float widthDips = (float)Math.Ceiling(SanguoshaFrameWidth * scale), heightDips = (float)Math.Ceiling(SanguoshaFrameHeight * scale);
            bool appearanceEffect = Math.Abs(_appearanceBrightness - 1.0) > .001 || Math.Abs(_appearanceContrast - 1.0) > .001;
            return new SanguoshaRasterProfile
            {
                Width = useAnimatedDpi ? animated.ConvertDipsToPixels(widthDips, CanvasDpiRounding.Ceiling) : SpriteCanvas.ConvertDipsToPixels(widthDips, CanvasDpiRounding.Ceiling),
                Height = useAnimatedDpi ? animated.ConvertDipsToPixels(heightDips, CanvasDpiRounding.Ceiling) : SpriteCanvas.ConvertDipsToPixels(heightDips, CanvasDpiRounding.Ceiling),
                Dpi = dpi, Scale = (float)scale * (dpi / 96f), BrightnessBoost = _brightnessBoost, ContrastBoost = _contrastBoost,
                LogicalScale = (float)scale, WidthDips = widthDips, HeightDips = heightDips,
                AppearanceBrightness = appearanceEffect ? _appearanceBrightness : 1, AppearanceContrast = appearanceEffect ? _appearanceContrast : 1
            };
        }
        private static string SanguoshaLocalCachePath(SanguoshaTierAssets assets, SanguoshaRasterProfile profile)
        {
            var v = Windows.ApplicationModel.Package.Current.Id.Version;
            string version = v.Major + "." + v.Minor + "." + v.Build + "." + v.Revision;
            string key = version + ";" + assets.SourceRevision + ";" + profile.Signature;
            string hash;
            using (var sha = SHA256.Create()) hash = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(key))).Replace("-", "");
            string directory = Path.Combine(ApplicationData.Current.LocalCacheFolder.Path, "SanguoshaLz4-v2", version);
            Directory.CreateDirectory(directory);
            return Path.Combine(directory, assets.Name + "-" + hash + ".sgslz4");
        }
        private async Task<bool> TryPrepareSanguoshaLz4Async(SanguoshaTierAssets assets, SanguoshaRasterProfile profile, int token, int generation)
        {
            SanguoshaLz4Playback playback = null;
            try
            {
                if (_sanguoshaLz4Playback != null && _sanguoshaLz4Tier == assets.Tier && _sanguoshaLz4Profile.Signature == profile.Signature)
                {
                    var warm = _sanguoshaLz4Playback;
                    await Task.Run(() => { using (warm.AcquireFrame(0)) { } });
                    return token == _playToken && generation == _resourceGeneration;
                }
                string cache = SanguoshaLocalCachePath(assets, profile);
                StorageFile file;
                if (File.Exists(cache)) file = await StorageFile.GetFileFromPathAsync(cache);
                else if (profile.Neutral && profile.LogicalScale == .6875f) file = await SanguoshaAssetFileAsync("lz4/" + profile.Id + "/" + assets.Name + ".sgslz4");
                else { _sanguoshaFallbackReason = "exact color/DPI/layout variant not prepared"; return false; }
                var source = SanguoshaLz4Source.Open(await file.OpenStreamForReadAsync());
                try { ValidateSanguoshaRasterSource(source, assets, profile); }
                catch { source.Dispose(); throw; }
                playback = new SanguoshaLz4Playback(source);
                await Task.Run(() => { using (playback.AcquireFrame(0)) { } });
                if (token != _playToken || generation != _resourceGeneration) { playback.Dispose(); return false; }
                DisposeSanguoshaLz4Playback(); _sanguoshaLz4Playback = playback; playback = null;
                _sanguoshaLz4Profile = profile; _sanguoshaLz4Tier = assets.Tier; _sanguoshaRoute = "lz4:" + profile.Id; _sanguoshaFallbackReason = "";
                _sanguoshaRenderer?.ReleaseFrameEffectSources(); ReleaseSanguoshaPages(assets);
                return true;
            }
            catch (Exception error)
            {
                playback?.Dispose();
                if (token != _playToken || generation != _resourceGeneration) return false;
                DisposeSanguoshaLz4Playback(); _sanguoshaRoute = "atlas-fallback"; _sanguoshaFallbackReason = error.GetType().Name + ": " + error.Message;
                App.Log("Sanguosha LZ4 prepare: " + _sanguoshaFallbackReason); return false;
            }
        }
        private bool IsSanguoshaLz4ConfigurationCurrent()
        {
            if (_sanguoshaLz4Playback == null || _sanguoshaLz4Profile == null) return false;
            return _sanguoshaLz4Profile.Signature == GetRequestedSanguoshaRasterProfile().Signature;
        }
        private bool DrawSanguoshaLz4Frame(CanvasDrawingSession drawingSession, double elapsedMs)
        {
            if (!IsSanguoshaLz4ConfigurationCurrent()) return false;
            var profile = _sanguoshaLz4Profile;
            Matrix3x2 previous = drawingSession.Transform;
            if (Math.Abs(previous.M11 * drawingSession.Dpi / 96f - profile.Scale) > .00001f || Math.Abs(previous.M22 * drawingSession.Dpi / 96f - profile.Scale) > .00001f
                || previous.M12 != 0 || previous.M21 != 0 || previous.M31 != 0 || previous.M32 != 0)
            { _sanguoshaFallbackReason = "drawing transform differs from exact raster variant"; return false; }
            int frame = _sanguoshaLz4Playback.Source.FrameAt(TimeSpan.FromTicks((long)(elapsedMs * TimeSpan.TicksPerMillisecond)));
            if (_sanguoshaLz4Bitmap == null || frame != _sanguoshaLz4UploadedFrame)
            {
                using (var lease = _sanguoshaLz4Playback.AcquireFrame(frame))
                {
                    if (_sanguoshaLz4Bitmap == null)
                        _sanguoshaLz4Bitmap = CanvasBitmap.CreateFromBytes(drawingSession.Device, lease.Bytes, profile.Width, profile.Height,
                            DirectXPixelFormat.B8G8R8A8UIntNormalized, profile.Dpi, CanvasAlphaMode.Premultiplied);
                    else _sanguoshaLz4Bitmap.SetPixelBytes(lease.Bytes);
                }
                _sanguoshaLz4UploadedFrame = frame;
            }
            drawingSession.Transform = Matrix3x2.Identity;
            try
            {
                // Exact physical-pixel variants need no second bilinear filter.
                drawingSession.DrawImage(_sanguoshaLz4Bitmap, Vector2.Zero,
                    new Windows.Foundation.Rect(0, 0, _sanguoshaLz4Bitmap.Size.Width, _sanguoshaLz4Bitmap.Size.Height),
                    1, CanvasImageInterpolation.NearestNeighbor, CanvasComposite.Copy);
            }
            finally { drawingSession.Transform = previous; }
            return true;
        }
        private void DisposeSanguoshaLz4Playback()
        {
            _sanguoshaAnimatedSurface?.DetachSource();
            if (_sanguoshaAnimatedSurface != null)
            {
                SpriteCanvas.Width = Viewport.Width; SpriteCanvas.Height = Viewport.Height; SpriteCanvas.Visibility = Windows.UI.Xaml.Visibility.Visible;
            }
            _sanguoshaLz4Playback?.Dispose(); _sanguoshaLz4Playback = null;
            _sanguoshaLz4Bitmap?.Dispose(); _sanguoshaLz4Bitmap = null;
            _sanguoshaLz4Profile = null; _sanguoshaLz4UploadedFrame = -1; _sanguoshaLz4Tier = 0;
        }
        private void InvalidateSanguoshaRasterConfiguration()
        {
            if (_sanguoshaLz4Playback == null || IsSanguoshaLz4ConfigurationCurrent()) return;
            DisposeSanguoshaLz4Playback();
            if (_isSanguoshaActive && _sanguoshaCurrent != null) RequestSanguoshaRasterChange(_sanguoshaCurrent);
        }
        private async void RequestSanguoshaRasterChange(SanguoshaTierAssets assets)
        {
            if (_sanguoshaRasterChangePending) return; _sanguoshaRasterChangePending = true;
            int token = _playToken, generation = _resourceGeneration;
            try
            {
                var profile = GetRequestedSanguoshaRasterProfile();
                if (await TryPrepareSanguoshaLz4Async(assets, profile, token, generation))
                { if (_isSanguoshaActive) StartSanguoshaFrameClock(); return; }
                await EnsureSanguoshaPagesAsync(assets);
                if (token != _playToken || generation != _resourceGeneration) { if (!IsSanguoshaTierOnScreen(assets)) ReleaseSanguoshaPages(assets); return; }
                DisposeSanguoshaLz4Playback(); _sanguoshaRoute = "atlas-fallback";
                if (_isSanguoshaActive) StartSanguoshaFrameClock();
                PrepareMissingSanguoshaVariantAsync(assets, profile, token, generation);
            }
            catch (Exception error) { App.Log("Sanguosha raster change: " + error); }
            finally { _sanguoshaRasterChangePending = false; SpriteCanvas.Invalidate(); }
        }
        private async void PrepareMissingSanguoshaVariantAsync(SanguoshaTierAssets assets, SanguoshaRasterProfile profile, int token, int generation)
        {
            if (_sanguoshaPreparingToken == token) return; _sanguoshaPreparingToken = token;
            bool held = false;
            try
            {
                await SanguoshaPreparationGate.WaitAsync(); held = true;
                if (token != _playToken || generation != _resourceGeneration) return;
                await EnsureSanguoshaPagesAsync(assets);
                if (token != _playToken || generation != _resourceGeneration || profile.Signature != GetRequestedSanguoshaRasterProfile().Signature) return;
                lock (assets) assets.PreparationUsers++;
                try
                {
                    string path = SanguoshaLocalCachePath(assets, profile);
                    if (!File.Exists(path)) await Task.Run(() => BakeSanguoshaVariant(assets, profile, path,
                        () => token != _playToken || generation != _resourceGeneration));
                }
                finally { lock (assets) assets.PreparationUsers--; }
                if (token == _playToken && generation == _resourceGeneration && ReferenceEquals(_sanguoshaCurrent, assets)
                    && profile.Signature == GetRequestedSanguoshaRasterProfile().Signature)
                {
                    await TryPrepareSanguoshaLz4Async(assets, profile, token, generation);
                    if (_isSanguoshaActive) StartSanguoshaFrameClock();
                    SpriteCanvas.Invalidate();
                }
                if (!IsSanguoshaTierOnScreen(assets) || _sanguoshaLz4Playback != null) ReleaseSanguoshaPages(assets);
            }
            catch (Exception error) { App.Log("Sanguosha exact variant preparation failed: " + error); _sanguoshaFallbackReason = "cache preparation: " + error.Message; }
            finally
            {
                if (!IsSanguoshaTierOnScreen(assets) || _sanguoshaLz4Playback != null) ReleaseSanguoshaPages(assets);
                if (held) SanguoshaPreparationGate.Release(); if (_sanguoshaPreparingToken == token) _sanguoshaPreparingToken = 0;
            }
        }
        private static void ValidateSanguoshaRasterSource(SanguoshaLz4Source source, SanguoshaTierAssets assets, SanguoshaRasterProfile profile)
        {
            if (source.Width != profile.Width || source.Height != profile.Height || Math.Abs(source.Fps - assets.Clip.Fps) > .0001 || source.FrameCount != assets.Clip.Frames.Length - 1)
                throw new InvalidDataException("Sanguosha exact raster variant does not match its clip/profile.");
        }
        private static void BakeSanguoshaVariant(SanguoshaTierAssets assets, SanguoshaRasterProfile profile, string path, Func<bool> cancelled = null)
        {
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                var device = CanvasDevice.GetSharedDevice();
                using (var renderer = new SanguoshaFrameRenderer())
                using (var target = CreateSanguoshaRasterTarget(device, profile))
                using (var stream = File.Create(temporary))
                using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
                {
                    int bytes = checked(profile.Width * profile.Height * 4);
                    var raw = new byte[bytes]; var buffer = raw.AsBuffer(); var compressed = new byte[SanguoshaLz4Codec.MaximumCompressedLength(bytes)];
                    writer.Write(Encoding.ASCII.GetBytes("KCLZ4F01")); writer.Write(1); writer.Write(profile.Width); writer.Write(profile.Height); writer.Write(profile.Width * 4);
                    writer.Write(1); writer.Write((int)assets.Clip.Fps); writer.Write(1); writer.Write(assets.Clip.Frames.Length - 1); writer.Write(bytes);
                    var shake = new SanguoshaShake(assets.Clip, 20261107);
                    for (int frame = 0; frame < assets.Clip.Frames.Length - 1; frame++)
                    {
                        if (cancelled != null && cancelled()) throw new OperationCanceledException("Sanguosha cache preparation was cancelled.");
                        double ms = frame * 1000d / assets.Clip.Fps; float x, y; shake.Offset(ms, out x, out y);
                        Matrix3x2 matrix = Matrix3x2.CreateTranslation(x, y) * Matrix3x2.CreateScale(profile.LogicalScale);
                        using (var ds = target.CreateDrawingSession())
                        {
                            ds.Clear(Windows.UI.Colors.Transparent);
                            if (profile.AppearanceBrightness == 1 && profile.AppearanceContrast == 1)
                                renderer.RenderDirectAtTime(ds, assets.Clip, assets.Pages, TimeSpan.FromTicks((long)(ms * TimeSpan.TicksPerMillisecond)), matrix);
                            else
                            {
                                using (var commands = new CanvasCommandList(device))
                                {
                                    using (var recording = commands.CreateDrawingSession()) renderer.RenderDirectAtTime(recording, assets.Clip, assets.Pages, TimeSpan.FromTicks((long)(ms * TimeSpan.TicksPerMillisecond)), matrix);
                                    using (var effect = new ColorMatrixEffect { Source = commands, ColorMatrix = CreateBrightnessContrastMatrix((float)profile.AppearanceBrightness, (float)profile.AppearanceContrast), ClampOutput = true }) ds.DrawImage(effect);
                                }
                            }
                        }
                        target.GetPixelBytes(buffer); int length = SanguoshaLz4Codec.Compress(raw, compressed); writer.Write(length); writer.Write(compressed, 0, length);
                    }
                }
                if (!File.Exists(path)) File.Move(temporary, path);
                File.WriteAllText(path + ".profile", assets.SourceRevision + "\n" + profile.Signature);
                TrimSanguoshaLocalCache(path);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        private static CanvasRenderTarget CreateSanguoshaRasterTarget(CanvasDevice device, SanguoshaRasterProfile profile)
        {
            var target = new CanvasRenderTarget(device, profile.WidthDips, profile.HeightDips, profile.Dpi);
            if (target.SizeInPixels.Width != profile.Width || target.SizeInPixels.Height != profile.Height)
            { target.Dispose(); throw new InvalidDataException("Win2D raster size differs from CanvasControl DIP conversion."); }
            return target;
        }
        private static void TrimSanguoshaLocalCache(string keep)
        {
            const long budget = 8L * 1024 * 1024 * 1024;
            string root = Path.Combine(ApplicationData.Current.LocalCacheFolder.Path, "SanguoshaLz4-v2");
            var files = new System.Collections.Generic.List<FileInfo>();
            foreach (string folder in Directory.GetDirectories(root))
            {
                if ((File.GetAttributes(folder) & System.IO.FileAttributes.ReparsePoint) != 0) continue;
                foreach (string path in Directory.GetFiles(folder, "*.sgslz4", SearchOption.TopDirectoryOnly))
                    if ((File.GetAttributes(path) & System.IO.FileAttributes.ReparsePoint) == 0) files.Add(new FileInfo(path));
            }
            files.Sort((a, b) => a.LastWriteTimeUtc.CompareTo(b.LastWriteTimeUtc));
            long total = 0; foreach (var file in files) total += file.Length;
            foreach (var file in files)
            {
                if (total <= budget) break;
                if (string.Equals(file.FullName, keep, StringComparison.OrdinalIgnoreCase)) continue;
                try { long length = file.Length; file.Delete(); total -= length; if (File.Exists(file.FullName + ".profile")) File.Delete(file.FullName + ".profile"); }
                catch (IOException) { } // Open readers deliberately deny deletion.
                catch (UnauthorizedAccessException) { }
            }
        }
    }
}

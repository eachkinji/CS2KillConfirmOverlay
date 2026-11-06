using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Threading.Tasks;
using KillConfirmCompatibility.Contracts;
using KillConfirmGameBar.Services;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Effects;
using Windows.Foundation;
using Windows.UI.Xaml;

namespace KillConfirmGameBar.Controls
{
    public sealed partial class KillConfirmAnimation
    {
        private static readonly Dictionary<string, CanvasBitmap> CalabiyauTextures = new Dictionary<string, CanvasBitmap>();
        private static string _calabiyauPackFolder;
        private bool _isCalabiyauActive;
        private CalabiyauClip _calabiyauClip;
        private string _calabiyauEventType = "normal";
        private int _calabiyauKillCount = 1;
        private double _calabiyauStartOffsetSeconds;
        private double CalabiyauElapsedSeconds => _calabiyauStartOffsetSeconds + _playbackClock.Elapsed.TotalSeconds;
        private static readonly Dictionary<string, CalabiyauClip> CalabiyauClips = new Dictionary<string, CalabiyauClip>();
        // Includes the original expanding side lines and outer frame without clipping.
        private const double CalabiyauCanvasSize = 320;

        private static async Task PreloadCalabiyauAsync(IProgress<int> progress)
        {
            CalabiyauPack pack = CalabiyauPacks.Find(_iconPack);
            string folder = pack == null ? "" : (await PackCatalogService.GetIconPackAsync(pack.Key))?.FolderPath;
            if (pack != null && string.IsNullOrWhiteSpace(folder)) throw new FileNotFoundException("请先导入所选卡拉彼丘图标包。");
            if (_calabiyauPackFolder != folder)
            {
                foreach (var bitmap in CalabiyauTextures.Values) bitmap.Dispose();
                CalabiyauTextures.Clear(); CalabiyauClips.Clear();
                _calabiyauPackFolder = folder;
            }
            await LoadCalabiyauClipAsync("crosshair.json.gz");
            if (pack != null)
            {
                // A multi-kill can introduce atlases absent from the one-kill
                // clip. Warm every tier when selecting a pack so its first
                // five-kill animation does not start after the audio's stars.
                for (int count = 1; count <= 5; count++)
                {
                    await LoadCalabiyauClipAsync(pack.ClipPath(count), true);
                    progress?.Report(count * 18);
                }
                string overkill = CalabiyauClips[pack.ClipPath(5)].OverkillClip;
                if (overkill != null) await LoadCalabiyauClipAsync(overkill, true);
                await LoadCalabiyauEventTexturesAsync();
                progress?.Report(100);
                return;
            }
            for (int count = 1; count <= 6; count++)
            {
                await LoadCalabiyauClipAsync(DefaultCalabiyauClipPath(count));
                progress?.Report(count * 15);
            }
            await LoadCalabiyauEventTexturesAsync();
            progress?.Report(100);
        }

        private static string DefaultCalabiyauClipPath(int count) =>
            "killconfirm/default-lower-" + Math.Max(1, Math.Min(6, count)) + ".json.gz";

        private static async Task LoadCalabiyauEventTexturesAsync()
        {
            foreach (string kind in new[] { "normal", "headshot", "knife", "grenade" })
                foreach (string layer in new[] { "icon", "label" })
                    await LoadCalabiyauTextureAsync("event-" + layer + "-" + kind + ".png");
            for (int i = 1; i < 100; i++) await LoadCalabiyauTextureAsync("event-count-" + i + ".png");
        }

        private static async Task LoadCalabiyauTextureAsync(string name, bool external = false)
        {
            if (CalabiyauTextures.ContainsKey(name)) return;
            var bitmap = external && !name.StartsWith("event-", StringComparison.Ordinal) && !name.StartsWith("label-", StringComparison.Ordinal)
                ? await LoadBitmapFromStorageFileAsync(await CalabiyauExternalAssetService.FileAsync(_iconPack, "textures/" + name))
                : await LoadBitmapFromApplicationUriAsync("ms-appx:///Assets/GameStyles/calabiyau/textures/" + name);
            if (bitmap == null) throw new InvalidDataException("Missing Calabiyau texture: " + name);
            CalabiyauTextures.Add(name, bitmap);
        }

        private static async Task<CalabiyauClip> LoadCalabiyauClipAsync(string path, bool external = false)
        {
            if (CalabiyauClips.TryGetValue(path, out CalabiyauClip cached)) return cached;
            var file = external ? await CalabiyauExternalAssetService.FileAsync(_iconPack, path)
                : await SharedResources.AssetFileAsync(new Uri("ms-appx:///Assets/GameStyles/calabiyau/" + path));
            CalabiyauClip clip;
            using (var stream = await file.OpenStreamForReadAsync())
                clip = CalabiyauClip.Read(stream);
            foreach (string texture in clip.Textures)
            {
                if (CalabiyauTextures.ContainsKey(texture)) continue;
                await LoadCalabiyauTextureAsync(texture, external);
            }
            CalabiyauClips.Add(path, clip);
            return clip;
        }

        public void PlayCalabiyauKill(int killCount = 1, bool headshot = false, bool knife = false, bool grenade = false, ulong publishedUnixMs = 0)
        {
            PlayCalabiyau(false, killCount, knife ? "knife" : grenade ? "grenade" : headshot ? "headshot" : "normal", publishedUnixMs);
        }
        public void PlayCalabiyauCrosshairKill(ulong publishedUnixMs = 0) { PlayCalabiyau(true, publishedUnixMs: publishedUnixMs); }

        private async void PlayCalabiyau(bool crosshair, int killCount = 1, string eventType = "normal", ulong publishedUnixMs = 0)
        {
            // Use event publication as the shared event anchor and compensate UI
            // loading latency. Audio may be scheduled slightly before publication.
            long requestedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var loadingClock = System.Diagnostics.Stopwatch.StartNew();
            double transportSeconds = publishedUnixMs > 0 && publishedUnixMs < (ulong)requestedAt
                ? (requestedAt - (double)publishedUnixMs) / 1000.0 : 0;
            int generation = _resourceGeneration, token = BeginReplacementPlayback();
            CalabiyauClip clip = null;
            try
            {
                await WaitForCurrentPackPreloadAsync();
                if (generation != _resourceGeneration || token != _playToken) return;
                string path;
                if (crosshair) path = "crosshair.json.gz";
                else
                {
                    CalabiyauPack pack = CalabiyauPacks.Find(_iconPack);
                    path = pack != null ? pack.ClipPath(killCount) : DefaultCalabiyauClipPath(killCount);
                }
                if (!CalabiyauClips.TryGetValue(path, out clip))
                    throw new InvalidOperationException("Calabiyau animation was not preloaded: " + path);
                if (!crosshair && killCount > 5 && clip.OverkillClip != null
                    && !CalabiyauClips.TryGetValue(clip.OverkillClip, out clip))
                    throw new InvalidOperationException("Calabiyau overkill animation was not preloaded.");
                if (generation != _resourceGeneration || token != _playToken) return;
                _timer.Stop();
                _playbackClock.Stop();
                _customSequencePlaying = false;
                ResetDoubaoState(); ResetDagoujiaoState(); ResetOverwatchState();
                ResetApexFeedState(); ResetModernWarfare2019State();
                ResetBattlefield5ScrollingState(); ResetBattlefield4HudState();
                ResetBattlefield2042HudState(); ResetBattlefield6HudState(); ResetSanguoshaState();
                ResetPubgHudState(); ResetDeltaForceHudState();
                _isBattlefieldTextOverlayActive = false;
                _currentCodeAsset = null; _currentValorantAsset = null;
                _currentBattlefieldAsset = null; _currentCsolAsset = null;
                _contentSizedViewport = true;
                _isBattlefield1CompactLayoutActive = false;
                _isCalabiyauActive = true;
                _calabiyauClip = clip;
                _calabiyauKillCount = Math.Max(1, Math.Min(99, killCount));
                _calabiyauEventType = eventType;
                ApplyViewportSize(clip?.Width ?? CalabiyauCanvasSize, clip?.Height ?? CalabiyauCanvasSize);
                HideLoadingProgress();
                Visibility = Visibility.Visible;
                _timer.Interval = TimeSpan.FromMilliseconds(1000.0 / 60);
                _calabiyauStartOffsetSeconds = transportSeconds + loadingClock.Elapsed.TotalSeconds;
                _playbackClock.Restart();
                SpriteCanvas.Invalidate();
                _timer.Start();
            }
            catch (Exception error)
            {
                App.Log("Calabiyau playback failed: " + error);
                if (token == _playToken)
                {
                    _timer.Stop(); _playbackClock.Stop();
                    _isCalabiyauActive = false;
                    Visibility = Visibility.Collapsed;
                }
            }
        }

        private void UpdateCalabiyauFrame()
        {
            if (CalabiyauElapsedSeconds >= (_calabiyauClip?.Duration ?? CalabiyauMotion.Duration))
            {
                _timer.Stop(); _playbackClock.Stop();
                _isCalabiyauActive = false;
                Visibility = Visibility.Collapsed;
                return;
            }
            SpriteCanvas.Invalidate();
        }

        private void DrawCalabiyauFrame(CanvasDrawingSession session)
        {
            if (_calabiyauClip == null) DrawCalabiyauAt(session, CalabiyauElapsedSeconds);
            else DrawCalabiyauClipEventAt(session, _calabiyauClip, CalabiyauElapsedSeconds, _calabiyauEventType, _calabiyauKillCount);
        }

        private static void DrawCalabiyauClipAt(CanvasDrawingSession session, CalabiyauClip clip, double seconds)
        {
            DrawCalabiyauClipEventAt(session, clip, seconds, "normal", 1);
        }

        private static void DrawCalabiyauClipEventAt(CanvasDrawingSession session, CalabiyauClip clip, double seconds, string eventType, int killCount)
            => DrawCalabiyauClipEventAtSampling(session, clip, seconds, eventType, killCount, false);

        private static void DrawCalabiyauClipEventAtSampling(CanvasDrawingSession session, CalabiyauClip clip, double seconds, string eventType, int killCount, bool originalSampling)
        {
            if (seconds < 0 || seconds >= clip.Duration) return;
            int frame = Math.Min(clip.Frames.Length - 1, (int)(seconds * clip.Fps));
            Matrix3x2 previous = session.Transform;
            try
            {
                foreach (float[] c in clip.Frames[frame])
                {
                    // A collapsed UMG layer has no visible area. Feeding its
                    // singular transform into cubic filtering can fail the
                    // entire deferred Direct2D command list.
                    if (c[18] <= 0 || c[3] <= 0 || c[4] <= 0 ||
                        Math.Abs((double)c[9] * c[12] - (double)c[10] * c[11]) < 1e-8) continue;
                    if (c.Length == 23 && (c[21] <= 0 || c[22] <= 0)) continue;
                    string texture = clip.Textures[(int)c[0]];
                    if (texture == "event-icon.png") texture = "event-icon-" + eventType + ".png";
                    else if (texture == "event-label.png") texture = "event-label-" + eventType + ".png";
                    else if (texture == "event-count.png") texture = "event-count-" + killCount + ".png";
                    if (!CalabiyauTextures.TryGetValue(texture, out CanvasBitmap bitmap)) continue;
                    // The UMG clipping window belongs to the canvas, not to the
                    // moving sprite. Keep it fixed while replacing dynamic icons.
                    session.Transform = previous;
                    using (var clipLayer = c.Length == 23
                        ? session.CreateLayer(1.0f, new Rect(c[19], c[20], c[21], c[22])) : null)
                    {
                        session.Transform = new Matrix3x2(c[9], c[10], c[11], c[12], c[13], c[14]) * previous;
                        var destination = new Rect(c[1], c[2], c[3], c[4]);
                        var source = new Rect(c[5], c[6], c[7], c[8]);
                        bool eventIcon = texture.StartsWith("event-icon-", StringComparison.Ordinal);
                        bool eventCount = texture.StartsWith("event-count-", StringComparison.Ordinal);
                        if (eventIcon || eventCount || texture.StartsWith("event-label-", StringComparison.Ordinal) || texture.StartsWith("label-", StringComparison.Ordinal))
                            source = new Rect(0, 0, bitmap.Size.Width, bitmap.Size.Height);
                        if (eventIcon)
                        {
                            double fit = Math.Min(destination.Width / source.Width, destination.Height / source.Height);
                            double width = source.Width * fit, height = source.Height * fit;
                            destination = new Rect(destination.X + (destination.Width - width) / 2, destination.Y + (destination.Height - height) / 2, width, height);
                        }
                        else if (eventCount)
                            destination = new Rect(destination.X, destination.Y, destination.Height * source.Width / source.Height, destination.Height);
                        bool effectAtlas = texture.StartsWith("fx-", StringComparison.Ordinal);
                        if (!originalSampling && !effectAtlas)
                            destination = SnapCalabiyauSprite(destination, session.Transform, session.Dpi);
                        var interpolation = originalSampling
                            ? (effectAtlas ? CanvasImageInterpolation.Linear : CanvasImageInterpolation.HighQualityCubic)
                            : CalabiyauInterpolation(destination, source, session.Transform, session.Dpi, effectAtlas);
                        var composite = texture.StartsWith("fx-add-", StringComparison.Ordinal) ? CanvasComposite.Add : CanvasComposite.SourceOver;
                        if (c[15] == 1 && c[16] == 1 && c[17] == 1)
                        {
                            session.DrawImage(bitmap, destination, source, c[18], interpolation, composite);
                            continue;
                        }
                        // Each draw must retain its own effect graph. The UWP
                        // renderer can evaluate effects after subsequent draws;
                        // mutating one shared Source makes labels become digits.
                        using (var tint = new ColorMatrixEffect
                        {
                            Source = bitmap,
                            ColorMatrix = new Matrix5x4 { M11 = c[15], M22 = c[16], M33 = c[17], M44 = 1 }
                        })
                            session.DrawImage(tint, destination, source, c[18], interpolation, composite);
                    }
                }
            }
            finally { session.Transform = previous; }
        }

        private static void DrawCalabiyauAt(CanvasDrawingSession session, double seconds)
        {
            var root = CalabiyauMotion.At(0, seconds);
            var parent = CalabiyauMotion.At(1, seconds);
            if (!root.Visible || !parent.Visible) return;
            Matrix3x2 previous = session.Transform;
            try
            {
                for (int i = 2; i < CalabiyauMotion.LayerCount; i++)
                {
                    var layer = CalabiyauMotion.At(i, seconds);
                    float opacity = (float)Math.Max(0, Math.Min(1, layer.Opacity * layer.Alpha * root.Opacity * parent.Opacity));
                    if (!layer.Visible || opacity <= 0 || layer.Width <= 0 || layer.Height <= 0) continue;
                    if (!CalabiyauTextures.TryGetValue(layer.Texture, out CanvasBitmap image)) continue;
                    session.Transform = Matrix3x2.CreateScale((float)layer.ScaleX, (float)layer.ScaleY)
                        * Matrix3x2.CreateRotation((float)(layer.Angle * Math.PI / 180))
                        * Matrix3x2.CreateTranslation((float)(CalabiyauCanvasSize / 2 + layer.X), (float)(CalabiyauCanvasSize / 2 + layer.Y))
                        * previous;
                    double x = -layer.Width / 2, y = -layer.Height / 2;
                    if (layer.NineSlice)
                    {
                        // These three brushes have 0.5 margins: four corners, no center.
                        double sw = image.Size.Width / 2, sh = image.Size.Height / 2;
                        double dw = Math.Min(sw, layer.Width / 2), dh = Math.Min(sh, layer.Height / 2);
                        for (int row = 0; row < 2; row++)
                            for (int col = 0; col < 2; col++)
                                session.DrawImage(image,
                                    new Rect(x + col * (layer.Width - dw), y + row * (layer.Height - dh), dw, dh),
                                    new Rect(col * sw, row * sh, sw, sh), opacity, CanvasImageInterpolation.Linear);
                    }
                    else
                        session.DrawImage(image, new Rect(x, y, layer.Width, layer.Height),
                            new Rect(0, 0, image.Size.Width, image.Size.Height), opacity, CanvasImageInterpolation.Linear);
                }
            }
            finally { session.Transform = previous; }
        }
    }
}

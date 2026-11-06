using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Threading.Tasks;
using KillConfirmCompatibility.Services;
using Microsoft.Graphics.Canvas;
using Windows.Foundation;
using Windows.Storage;
using Windows.UI;

namespace KillConfirmCompatibility.Controls
{
    public sealed partial class KillConfirmAnimation
    {
        // Optional v2 package extension. Old packages and Base keep the cooked
        // common renderer. Each kill tier retains source textures and scalar
        // MovieScene channels rather than a full-screen frame sequence.
        [DataContract]
        private sealed class ValorantNativeAnimation : IDisposable
        {
            [DataMember(Name = "format_version")] public int FormatVersion { get; set; }
            [DataMember(Name = "tiers")] public ValorantNativeTier[] Tiers { get; set; }
            [DataMember(Name = "hide_ring")] public bool HideRing { get; set; }
            [DataMember(Name = "hide_pips")] public bool HidePips { get; set; }
            public ValorantNativeTier ActiveTier;
            public bool IsDisposed;
            public int Generation;
            public void Dispose()
            {
                lock (this)
                {
                    IsDisposed = true;
                    Generation++;
                    ActiveTier?.ReleaseTextures();
                    ActiveTier = null;
                }
            }
        }

        [DataContract]
        private sealed class ValorantNativeTier
        {
            [DataMember(Name = "source")] public string Source { get; set; }
            [DataMember(Name = "emblem")] public string Emblem { get; set; }
            [DataMember(Name = "frame")] public string Frame { get; set; }
            [DataMember(Name = "frame_dissolve")] public string FrameDissolve { get; set; }
            [DataMember(Name = "nodes")] public ValorantNativeNode[] Nodes { get; set; }
            [DataMember(Name = "has_outro")] public bool HasOutro { get; set; }
            [DataMember(Name = "intro_start_ms")] public double IntroStart { get; set; }
            [DataMember(Name = "intro_end_ms")] public double IntroEnd { get; set; }
            [DataMember(Name = "outro_start_ms")] public double OutroStart { get; set; }
            [DataMember(Name = "outro_end_ms")] public double OutroEnd { get; set; }
            [DataMember(Name = "intro_speed")] public double IntroSpeed { get; set; }
            [DataMember(Name = "outro_speed")] public double OutroSpeed { get; set; }
            [DataMember(Name = "intro_reverse")] public bool IntroReverse { get; set; }
            [DataMember(Name = "outro_reverse")] public bool OutroReverse { get; set; }
            [DataMember(Name = "intro_start_at_ms")] public double IntroStartAt { get; set; }
            [DataMember(Name = "outro_start_at_ms")] public double OutroStartAt { get; set; }
            public Dictionary<string, CanvasBitmap> Bitmaps;
            public Matrix3x2[] Transforms;
            public double[] Opacities;
            public double[] Sample;
            public CanvasBitmap Get(string name) => name != null && Bitmaps != null && Bitmaps.TryGetValue(name, out var bitmap) ? bitmap : null;
            public double OutroDuration => HasOutro
                ? Math.Max(0, OutroEnd - OutroStart - Math.Max(0, OutroStartAt)) / OutroSpeed
                : Math.Max(0, IntroEnd - IntroStart);
            public void ReleaseTextures()
            {
                if (Bitmaps != null) foreach (var bitmap in Bitmaps.Values) bitmap.Dispose();
                Bitmaps = null;
            }
        }

        [DataContract]
        private sealed class ValorantNativeNode
        {
            [DataMember(Name = "name")] public string Name { get; set; }
            [DataMember(Name = "parent")] public int Parent { get; set; }
            [DataMember(Name = "texture")] public string Texture { get; set; }
            [DataMember(Name = "x")] public double X { get; set; }
            [DataMember(Name = "y")] public double Y { get; set; }
            [DataMember(Name = "width")] public double Width { get; set; }
            [DataMember(Name = "height")] public double Height { get; set; }
            [DataMember(Name = "pivot_x")] public double PivotX { get; set; }
            [DataMember(Name = "pivot_y")] public double PivotY { get; set; }
            // Translation, scale, angle, render opacity, color RGBA, shear.
            [DataMember(Name = "values")] public double[] Values { get; set; }
            [DataMember(Name = "intro")] public ValorantNativeChannel[] Intro { get; set; }
            [DataMember(Name = "outro")] public ValorantNativeChannel[] Outro { get; set; }
        }

        [DataContract]
        private sealed class ValorantNativeChannel
        {
            [DataMember(Name = "property")] public int Property { get; set; }
            [DataMember(Name = "times")] public double[] Times { get; set; }
            [DataMember(Name = "values")] public double[] Values { get; set; }
            [DataMember(Name = "arrive")] public double[] Arrive { get; set; }
            [DataMember(Name = "leave")] public double[] Leave { get; set; }
            [DataMember(Name = "modes")] public int[] Modes { get; set; }
        }

        private static async Task<ValorantNativeAnimation> LoadValorantNativeAnimationAsync(string packKey)
        {
            StorageFile file = await ValorantExternalAssetService.TryGetNativeAnimationAsync(packKey);
            if (file == null) return null;
            using (Stream stream = await file.OpenStreamForReadAsync())
            {
                if (stream.Length > 4 * 1024 * 1024) throw new InvalidDataException("VALORANT native animation metadata is too large.");
                var result = (ValorantNativeAnimation)new DataContractJsonSerializer(typeof(ValorantNativeAnimation)).ReadObject(stream);
                if (result?.FormatVersion != 1 || result.Tiers == null || result.Tiers.Length == 0 || result.Tiers.Length > 8)
                    throw new InvalidDataException("Unsupported VALORANT native animation version/tiers.");
                foreach (var tier in result.Tiers)
                {
                    // The optional extension initially used unit playback.
                    // Missing speed fields preserve that older JSON contract.
                    if (tier != null && tier.IntroSpeed == 0) tier.IntroSpeed = 1;
                    if (tier != null && tier.OutroSpeed == 0) tier.OutroSpeed = 1;
                    if (tier == null || tier.Nodes == null || tier.Nodes.Length == 0 || tier.Nodes.Length > 256
                        || !IsNativeFinite(tier.IntroStart) || !IsNativeFinite(tier.IntroEnd)
                        || !IsNativeFinite(tier.OutroStart) || !IsNativeFinite(tier.OutroEnd)
                        || Math.Abs(tier.IntroStart) > 10000 || Math.Abs(tier.OutroStart) > 10000
                        || tier.IntroEnd < tier.IntroStart || tier.IntroEnd - tier.IntroStart > 10000
                        || tier.OutroEnd < tier.OutroStart || tier.OutroDuration > 10000
                        || !IsNativeFinite(tier.IntroSpeed) || !IsNativeFinite(tier.OutroSpeed)
                        || tier.IntroSpeed <= 0 || tier.IntroSpeed > 10 || tier.OutroSpeed <= 0 || tier.OutroSpeed > 10
                        || !IsNativeFinite(tier.IntroStartAt) || !IsNativeFinite(tier.OutroStartAt)
                        || Math.Abs(tier.IntroStartAt) > 10000 || Math.Abs(tier.OutroStartAt) > 10000)
                        throw new InvalidDataException("Invalid VALORANT native animation nodes/timing.");
                    for (int i = 0; i < tier.Nodes.Length; i++)
                    {
                        var node = tier.Nodes[i];
                        if (node == null || node.Parent >= i || node.Parent < -1 || node.Values?.Length != 12
                            || !IsNativeFinite(node.X) || !IsNativeFinite(node.Y)
                            || !IsNativeFinite(node.Width) || !IsNativeFinite(node.Height)
                            || !IsNativeFinite(node.PivotX) || !IsNativeFinite(node.PivotY)
                            || Math.Abs(node.X) > 16384 || Math.Abs(node.Y) > 16384
                            || node.Width <= 0 || node.Height <= 0 || node.Width > 16384 || node.Height > 16384)
                            throw new InvalidDataException("Invalid VALORANT native animation hierarchy.");
                        foreach (double value in node.Values)
                            if (!IsNativeFinite(value) || Math.Abs(value) > 1000000)
                                throw new InvalidDataException("Invalid VALORANT native animation transform.");
                        ValidateNativeChannels(node.Intro);
                        ValidateNativeChannels(node.Outro);
                    }
                }
                return result;
            }
        }

        private static bool IsNativeFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

        private static void ValidateNativeChannels(ValorantNativeChannel[] channels)
        {
            if (channels == null) return;
            if (channels.Length > 48) throw new InvalidDataException("Too many native animation channels.");
            foreach (var channel in channels)
            {
                int count = channel?.Times?.Length ?? 0;
                if (channel == null || channel.Property < 0 || channel.Property >= 12 || count == 0 || count > 4096 || channel.Values?.Length != count
                    || channel.Arrive?.Length != count || channel.Leave?.Length != count || channel.Modes?.Length != count)
                    throw new InvalidDataException("Invalid native animation curve.");
                for (int i = 0; i < count; i++)
                    if (!IsNativeFinite(channel.Times[i]) || !IsNativeFinite(channel.Values[i])
                        || !IsNativeFinite(channel.Arrive[i]) || !IsNativeFinite(channel.Leave[i])
                        || Math.Abs(channel.Times[i]) > 100000 || Math.Abs(channel.Values[i]) > 1000000
                        || Math.Abs(channel.Arrive[i]) > 1000000 || Math.Abs(channel.Leave[i]) > 1000000
                        || channel.Modes[i] < 0 || channel.Modes[i] > 2
                        || (i > 0 && channel.Times[i] < channel.Times[i - 1]))
                        throw new InvalidDataException("Invalid native animation key.");
            }
        }

        private static async Task PrepareValorantNativeTierAsync(ValorantTextureSet textures, int kills,
            Func<string, Task<CanvasBitmap>> loadTexture = null)
        {
            var animation = textures.NativeAnimation;
            if (animation == null) return;
            var tier = animation.Tiers[Math.Max(0, Math.Min(animation.Tiers.Length - 1, kills - 1))];
            int generation;
            lock (animation)
            {
                if (animation.IsDisposed) throw new OperationCanceledException();
                generation = ++animation.Generation;
                if (ReferenceEquals(animation.ActiveTier, tier)) return;
            }
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (tier.Emblem != null) names.Add(tier.Emblem);
            if (tier.Frame != null) names.Add(tier.Frame);
            if (tier.FrameDissolve != null) names.Add(tier.FrameDissolve);
            foreach (var node in tier.Nodes) if (node.Texture != null) names.Add(node.Texture);
            var loaded = new Dictionary<string, CanvasBitmap>(StringComparer.OrdinalIgnoreCase);
            try
            {
                foreach (string name in names)
                {
                    if (loadTexture != null) loaded.Add(name, await loadTexture(name));
                    else
                    {
                        StorageFile file = await ValorantExternalAssetService.TryGetVisualTextureAsync(textures.PackKey, null, name);
                        if (file == null) throw new InvalidDataException("Missing native VALORANT texture: " + name);
                        loaded.Add(name, await LoadBitmapFromStorageFileAsync(file));
                    }
                }
                lock (animation)
                {
                    // A pack/style switch can dispose the texture cache while
                    // the StorageFile/bitmap awaits are in flight. Never
                    // publish GPU resources back into that detached cache.
                    if (animation.IsDisposed || generation != animation.Generation || !ReferenceEquals(textures.NativeAnimation, animation))
                        throw new OperationCanceledException();
                    tier.Transforms = new Matrix3x2[tier.Nodes.Length];
                    tier.Opacities = new double[tier.Nodes.Length];
                    tier.Sample = new double[12];
                    animation.ActiveTier?.ReleaseTextures();
                    tier.Bitmaps = loaded;
                    animation.ActiveTier = tier;
                }
            }
            catch
            {
                foreach (var bitmap in loaded.Values) bitmap.Dispose();
                throw;
            }
        }

        private void DrawValorantNativeLayers(CanvasDrawingSession ds, ValorantKillAsset asset, double realMs)
        {
            var tier = asset.Textures.NativeAnimation?.ActiveTier;
            if (tier?.Bitmaps == null) return;
            double fadeOutMs = 1803.2667 / GetNativeAfterglowPlaybackSpeed(asset.KillCount);
            double outroMs = realMs - fadeOutMs;
            if (outroMs > tier.OutroDuration) return;
            double introTime = NativeSequenceTime(tier.IntroStart, tier.IntroEnd, tier.IntroStartAt,
                tier.IntroReverse, realMs * tier.IntroSpeed);
            if (outroMs >= 0 && !tier.HasOutro) introTime = Math.Max(tier.IntroStart, tier.IntroEnd - outroMs);
            double outroTime = NativeSequenceTime(tier.OutroStart, tier.OutroEnd, tier.OutroStartAt,
                tier.OutroReverse, Math.Max(0, outroMs) * tier.OutroSpeed);
            var root = Matrix3x2.CreateScale((float)CookedAfterglowUmgScale)
                * Matrix3x2.CreateTranslation((float)(ValorantFrameWidth / 2), (float)(ValorantFrameHeight / 2));
            Matrix3x2 previous = ds.Transform;
            try
            {
                for (int i = 0; i < tier.Nodes.Length; i++)
                {
                    var node = tier.Nodes[i];
                    double[] v = tier.Sample;
                    Array.Copy(node.Values, v, 12);
                    SampleNativeChannels(node.Intro, introTime, v);
                    if (tier.HasOutro && outroMs >= 0) SampleNativeChannels(node.Outro, outroTime, v);
                    var pivot = new Vector2((float)(node.Width * node.PivotX), (float)(node.Height * node.PivotY));
                    var shear = new Matrix3x2(1, (float)Math.Tan(v[11] * Math.PI / 180), (float)Math.Tan(v[10] * Math.PI / 180), 1, 0, 0);
                    Matrix3x2 local = Matrix3x2.CreateTranslation(-pivot)
                        * Matrix3x2.CreateScale((float)v[2], (float)v[3]) * shear
                        * Matrix3x2.CreateRotation((float)(v[4] * Math.PI / 180))
                        * Matrix3x2.CreateTranslation(pivot + new Vector2((float)(node.X + v[0]), (float)(node.Y + v[1])));
                    tier.Transforms[i] = local * (node.Parent < 0 ? root : tier.Transforms[node.Parent]);
                    tier.Opacities[i] = Clamp01(v[5]) * (node.Parent < 0 ? 1 : tier.Opacities[node.Parent]);
                    var transform = tier.Transforms[i];
                    if (!IsNativeFinite(transform.M11) || !IsNativeFinite(transform.M12)
                        || !IsNativeFinite(transform.M21) || !IsNativeFinite(transform.M22)
                        || !IsNativeFinite(transform.M31) || !IsNativeFinite(transform.M32))
                    {
                        tier.Opacities[i] = 0;
                        continue;
                    }
                    CanvasBitmap image = tier.Get(node.Texture);
                    double opacity = tier.Opacities[i] * Clamp01(v[9]);
                    if (image == null || opacity <= 0 || Math.Abs(v[2] * v[3]) < 0.000001) continue;
                    ds.Transform = tier.Transforms[i] * previous;
                    var tint = Color.FromArgb(255, (byte)(Clamp01(v[6]) * 255), (byte)(Clamp01(v[7]) * 255), (byte)(Clamp01(v[8]) * 255));
                    DrawNativeTintedSource(ds, image, new Rect(0, 0, node.Width, node.Height),
                        new Rect(0, 0, image.SizeInPixels.Width, image.SizeInPixels.Height), tint, opacity, false);
                }
            }
            finally { ds.Transform = previous; }
        }

        private static double NativeSequenceTime(double start, double end, double startAt, bool reverse, double elapsed)
        {
            // UUMGSequencePlayer::PlayInternal starts Reverse at
            // LastValidFrame - StartAtTime, then clamps to the play range.
            // Negative cooked StartAtTime values therefore start at the end;
            // they do not add an artificial delay to the outro.
            double duration = Math.Max(0, end - start);
            double position = Math.Max(0, Math.Min(duration, reverse ? duration - startAt : startAt));
            return start + Math.Max(0, Math.Min(duration, position + (reverse ? -elapsed : elapsed)));
        }

        private static void SampleNativeChannels(ValorantNativeChannel[] channels, double ms, double[] values)
        {
            if (channels == null) return;
            foreach (var channel in channels) values[channel.Property] = SampleNativeChannel(channel, ms);
        }

        private static double SampleNativeChannel(ValorantNativeChannel c, double ms)
        {
            if (ms <= c.Times[0]) return c.Values[0];
            for (int i = 0; i + 1 < c.Times.Length; i++)
            {
                if (ms >= c.Times[i + 1]) continue;
                double span = c.Times[i + 1] - c.Times[i];
                double t = (ms - c.Times[i]) / span;
                if (c.Modes[i] == 1) return c.Values[i];
                if (c.Modes[i] != 2) return Lerp(c.Values[i], c.Values[i + 1], t);
                double t2 = t * t, t3 = t2 * t;
                return (2 * t3 - 3 * t2 + 1) * c.Values[i]
                    + (t3 - 2 * t2 + t) * c.Leave[i] * span
                    + (-2 * t3 + 3 * t2) * c.Values[i + 1]
                    + (t3 - t2) * c.Arrive[i + 1] * span;
            }
            return c.Values[c.Values.Length - 1];
        }
    }
}
